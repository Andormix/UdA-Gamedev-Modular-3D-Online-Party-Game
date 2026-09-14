using System;
using System.Collections.Generic;
using UnityEngine;

public class Table_StatePattern : InteractableAsset, InterfaceProgressBar, IWorkstationContext
{
    public event EventHandler<InterfaceProgressBar.OnProgressChangedEventArgs> OnProgressChanged;
    public event EventHandler OnThrownElement;

    [Header("Scheduler / Ready Window")]
    [SerializeField] private bool isOrderRequestReady;
    [SerializeField] private float readyWindowSeconds = 8f;

    [Header("Order Phase Timers")]
    [SerializeField] private float deliverItemsSeconds = 10f;
    [SerializeField] private float consumeSeconds = 1.5f;
    [SerializeField] private float paymentSeconds = 2f;

    [Header("Visuals")]
    [SerializeField] private WorkstationBVisuals visuals;

    [Header("Network")]
    [SerializeField] private TableNetSync netSync;

    [Header("Scoring")]
    [SerializeField] private ScoringConfigSO scoringConfig;
    public ScoringConfigSO ScoringConfig => scoringConfig;

    [Header("NPC Seating")]
    [SerializeField] private List<TableSeatPoint> seatPoints = new();

    [Header("Delivered Visuals")]
    [SerializeField] private TableDeliveredVisualNetSync deliveredVisualNetSync;

    [Header("Dirty Visuals")]
    [SerializeField] private TableDirtyVisualNetSync dirtyVisualNetSync;

    [Header("Tuto")]
    [SerializeField] private TutorialEventChannelSO tutorialEvents;
    [SerializeField] private string tutorialTableId = "TableA";

    private bool npcSeatingCycleActive;
    public bool IsNpcSeatingCycleActive() => npcSeatingCycleActive;
    public void SetNpcSeatingCycleActive(bool value) => npcSeatingCycleActive = value;

    private int ownerTeamId = -1; // -1 unassigned, 0 blue, 1 red
    public int OwnerTeamId => ownerTeamId;

    [SerializeField] private TableNpcSeatingController seatingController;
    public TableNpcSeatingController SeatingController => seatingController;

    private readonly IWorkstationContext ctx;

    public RecipeOrder ActiveOrder { get; set; }
    public List<SceneObjectSO> RemainingRequired { get; } = new();
    public List<SceneObjectSO> ThrownObjects { get; } = new();

    //  dirty queue generated from consumed items
    public List<SceneObjectSO> DirtyItems { get; } = new();

    public bool HasActiveCommand { get; set; }

    public int CurrentOrderCoinTotal { get; set; }

    private bool suppressNetSync;
    public bool ReadyToPayBonusGiven { get; set; }
    public double PaymentPhaseStartTime { get; set; }
    public bool PaymentAttempted { get; set; }

    public bool IsOrderRequestReady
    {
        get => isOrderRequestReady;
        set => isOrderRequestReady = value;
    }

    public float ReadyWindowSeconds => readyWindowSeconds;
    public float DeliverItemsSeconds => deliverItemsSeconds;
    public float ConsumeSeconds => consumeSeconds;
    public float PaymentSeconds => paymentSeconds;

    public IOrderService Orders { get; private set; }

    private readonly Dictionary<WorkstationPhaseId, IWorkstationState> states = new();
    private IWorkstationState current;

    private void Awake()
    {
        if (netSync == null) netSync = GetComponent<TableNetSync>();
        if (visuals == null) visuals = GetComponentInChildren<WorkstationBVisuals>(true);
        if (seatingController == null) seatingController = GetComponent<TableNpcSeatingController>();
        if (deliveredVisualNetSync == null) deliveredVisualNetSync = GetComponent<TableDeliveredVisualNetSync>();
        if (dirtyVisualNetSync == null) dirtyVisualNetSync = GetComponent<TableDirtyVisualNetSync>();

        states[WorkstationPhaseId.Idle] = new WorkstationIdleState(this);
        states[WorkstationPhaseId.SeatingNPCs] = new WorkstationSeatingNPCsState(this);
        states[WorkstationPhaseId.ReadyToRequest] = new WorkstationReadyToRequestState(this);
        states[WorkstationPhaseId.DeliverItems] = new WorkstationDeliverItemsState(this);
        states[WorkstationPhaseId.Consuming] = new WorkstationConsumingState(this);
        states[WorkstationPhaseId.AwaitingPayment] = new WorkstationAwaitingPaymentState(this);
        states[WorkstationPhaseId.NeedsCleanup] = new WorkstationNeedsCleanupState(this);

        ChangeState(WorkstationPhaseId.Idle);
    }

    private void Start()
    {
        if (RecipeManager.Instance != null) Orders = new RecipeManagerOrderServiceAdapter(RecipeManager.Instance);
        else Debug.LogError("RecipeManager.Instance is null! Check execution order.");
    }

    private void Update()
    {
        if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        current?.Tick(Time.deltaTime);
    }

    public override void Interact(Player player)
    {
        if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        current?.Interact(player);
    }

    public void ServerInteract(Player player)
    {
        if (player == null) return;
        current?.Interact(player);
    }

    public void SetOrderRequestReady(bool ready)
    {
        if (ready && (HasActiveCommand || ActiveOrder != null || DirtyItems.Count > 0)) return;

        IsOrderRequestReady = ready;

        if (ready) ChangeState(WorkstationPhaseId.ReadyToRequest);
        else if (current != null && current.Id == WorkstationPhaseId.ReadyToRequest) ChangeState(WorkstationPhaseId.Idle);

        ApplyVisuals(current?.Id ?? WorkstationPhaseId.Idle);
    }

    public void ChangeState(WorkstationPhaseId next)
    {
        if (current != null && current.Id == next)
        {
            ApplyVisuals(next);
            return;
        }

        current?.Exit();

        if (!states.TryGetValue(next, out var st))
            throw new InvalidOperationException($"State not registered: {next}");

        current = st;
        current.Enter();

        if (next == WorkstationPhaseId.Idle &&
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsServer &&
            seatingController != null &&
            seatingController.IsSpawned)
        {
            seatingController.CleanupAllServer();
        }
        else if (next == WorkstationPhaseId.Idle)
        {
            ReleaseAllSeats();
            npcSeatingCycleActive = false;
        }

        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsServer &&
            netSync != null)
        {
            netSync.RaiseTutorialPhaseEvent(next);
        }

        ApplyVisuals(next);

        if (!suppressNetSync &&
            Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsServer &&
            netSync != null &&
            netSync.IsSpawned)
        {
            netSync.ServerSetPhaseTimer(next, GetPhaseDurationForReplication(next));
            netSync.ServerResyncState(IsOrderRequestReady, (int)CurrentPhaseForNetSync);
        }
    }

    public void ResetWorkstation()
    {
        HasActiveCommand = false;
        ActiveOrder = null;
        CurrentOrderCoinTotal = 0;
        ReadyToPayBonusGiven = false;
        PaymentAttempted = false;
        PaymentPhaseStartTime = 0;
        ownerTeamId = -1;

        RemainingRequired.Clear();
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsServer &&
            netSync != null &&
            netSync.IsSpawned)
        {
            netSync.ServerClearRemainingRequired();
        }

        ThrownObjects.Clear();
        ClearDeliveredVisualsServer();

        DirtyItems.Clear();
        ClearDirtyVisualsServer();

        PushProgress(0f);
        NotifyRequiredChanged();

        IsOrderRequestReady = false;
        ChangeState(WorkstationPhaseId.Idle);
    }

    public void PushProgress(float normalized)
    {
        bool shouldShow = ShouldReplicateProgressForCurrentPhase();
        ApplyNetProgress(shouldShow ? Mathf.Clamp01(normalized) : 0f);
    }

    public void NotifyRequiredChanged() => OnThrownElement?.Invoke(this, EventArgs.Empty);

    public void ApplyVisuals(WorkstationPhaseId phase) => visuals?.Apply(phase, IsOrderRequestReady);

    public List<SceneObjectSO> GetTrownSceneObjectsArraySO() => ThrownObjects;
    public bool IsBusy() => HasActiveCommand || ActiveOrder != null || DirtyItems.Count > 0;
    public IReadOnlyList<SceneObjectSO> GetRemainingRequiredSceneObjectsSO() => RemainingRequired;
    public WorkstationPhaseId CurrentPhaseForNetSync => current != null ? current.Id : WorkstationPhaseId.Idle;

    public void ApplyNetState(bool isReady, WorkstationPhaseId phase)
    {
        suppressNetSync = true;
        IsOrderRequestReady = isReady;
        ChangeState(phase);
        suppressNetSync = false;
    }

    public void ApplyNetProgress(float normalized)
    {
        suppressNetSync = true;
        OnProgressChanged?.Invoke(this, new InterfaceProgressBar.OnProgressChangedEventArgs
        {
            progressNormalized = Mathf.Clamp01(normalized)
        });
        suppressNetSync = false;
    }

    public IReadOnlyList<TableSeatPoint> GetFreeSeats()
    {
        List<TableSeatPoint> free = new();
        for (int i = 0; i < seatPoints.Count; i++)
        {
            var seat = seatPoints[i];
            if (seat == null) continue;
            if (!seat.IsOccupied) free.Add(seat);
        }
        return free;
    }

    public int GetSeatCount()
    {
        int count = 0;
        for (int i = 0; i < seatPoints.Count; i++)
            if (seatPoints[i] != null) count++;
        return count;
    }

    public bool TryReserveSeat(out TableSeatPoint seat)
    {
        seat = null;
        if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer) return false;

        for (int i = 0; i < seatPoints.Count; i++)
        {
            var s = seatPoints[i];
            if (s == null) continue;

            if (!s.IsOccupied)
            {
                s.SetOccupied(true);
                seat = s;
                return true;
            }
        }

        return false;
    }

    public void ReleaseAllSeats()
    {
        for (int i = 0; i < seatPoints.Count; i++)
        {
            var seat = seatPoints[i];
            if (seat == null) continue;
            seat.SetOccupied(false);
        }
    }

    public void BeginPreServiceSeating()
    {
        if (!CanStartSeatingNow()) return;
        npcSeatingCycleActive = true;
        IsOrderRequestReady = false;
        ChangeState(WorkstationPhaseId.SeatingNPCs);
    }

    public bool CanStartSeatingNow()
    {
        if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer) return false;
        if (HasActiveCommand || ActiveOrder != null) return false;
        if (DirtyItems.Count > 0) return false;
        if (IsOrderRequestReady) return false;
        if (current != null && current.Id != WorkstationPhaseId.Idle) return false;
        if (npcSeatingCycleActive) return false;
        if (seatingController != null && seatingController.HasActiveNpcsOrCleanup()) return false;
        return true;
    }

    private bool ShouldReplicateProgressForCurrentPhase()
    {
        if (current == null) return false;
        return current.Id == WorkstationPhaseId.ReadyToRequest
            || current.Id == WorkstationPhaseId.DeliverItems
            || current.Id == WorkstationPhaseId.Consuming
            || current.Id == WorkstationPhaseId.AwaitingPayment;
    }

    private float GetPhaseDurationForReplication(WorkstationPhaseId phase)
    {
        return phase switch
        {
            WorkstationPhaseId.ReadyToRequest => ReadyWindowSeconds,
            WorkstationPhaseId.DeliverItems => DeliverItemsSeconds,
            WorkstationPhaseId.Consuming => ConsumeSeconds,
            WorkstationPhaseId.AwaitingPayment => PaymentSeconds,
            WorkstationPhaseId.SeatingNPCs => 12f,
            _ => 0f
        };
    }

    private int ResolvePlayerTeamId(ulong clientId)
    {
        if (CoopCampaignSessionContext.IsCoopCampaignRun) return (int)MatchTeam.Blue;
        if (!GameMultiplayerManager.playMultiplayer) return (int)MatchTeam.Blue;

        return (int)TeamInteractionRules.ResolveTeam(clientId);
    }

    public void AssignOwnerTeamFromPlayer(ulong clientId)
    {
        ownerTeamId = ResolvePlayerTeamId(clientId);
        if (ActiveOrder != null && RecipeManager.Instance != null)
            RecipeManager.Instance.SetOrderOwnerTeam(ActiveOrder.id, ownerTeamId);
    }

    public bool CanPlayerInteractByTeam(ulong clientId)
    {
        if (!GameMultiplayerManager.playMultiplayer || CoopCampaignSessionContext.IsCoopCampaignRun) return true;
        if (ownerTeamId < 0) return true;
        return ResolvePlayerTeamId(clientId) == ownerTeamId;
    }

    // ----- Delivered visuals -----
    public void RefreshDeliveredVisualsServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        if (deliveredVisualNetSync == null) return;
        deliveredVisualNetSync.ServerSetDeliveredVisuals(ThrownObjects);
    }

    public void ClearDeliveredVisualsServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        if (deliveredVisualNetSync == null) return;
        deliveredVisualNetSync.ServerClearDeliveredVisuals();
    }

    // ----- Dirty visuals -----
    public void GenerateDirtyItemsFromThrownServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;

        DirtyItems.Clear();

        for (int i = 0; i < ThrownObjects.Count; i++)
        {
            SceneObjectSO consumed = ThrownObjects[i];
            if (consumed == null) continue;

            SceneObjectSO dirty = consumed.dirtyVariantSO;
            if (dirty == null)
            {
                Debug.LogWarning($"[Table] Missing dirtyVariantSO for consumed item '{consumed.objectName}'.");
                continue;
            }

            DirtyItems.Add(dirty);
        }

        RefreshDirtyVisualsServer();
    }

    public void RefreshDirtyVisualsServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        if (dirtyVisualNetSync == null) return;
        dirtyVisualNetSync.ServerSetDirtyItems(DirtyItems);
    }

    public void ClearDirtyVisualsServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return;
        if (dirtyVisualNetSync == null) return;
        dirtyVisualNetSync.ServerClearDirtyItems();
    }

    public void EnterNeedsCleanupAfterNpcLeaveServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        PrepareForCleanupAfterOrderClosure();

        // Dirty appears instantly
        GenerateDirtyItemsFromThrownServer();

        // If NPCs still active, trigger async cleanup first.
        if (seatingController != null && seatingController.IsSpawned && seatingController.HasActiveNpcsOrCleanup())
        {
            seatingController.CleanupAllServer(() =>
            {
                // Re-evaluate after NPC cleanup finishes
                ResolveCleanupStateOrReturnIdleServer();
            });

            // while NPCs are exiting, keep cleanup state visible/locked
            ChangeState(WorkstationPhaseId.NeedsCleanup);
            return;
        }

        // No NPC cleanup pending => resolve immediately
        ResolveCleanupStateOrReturnIdleServer();
    }

    private void PrepareForCleanupAfterOrderClosure()
    {
        HasActiveCommand = false;
        ActiveOrder = null;
        IsOrderRequestReady = false;

        CurrentOrderCoinTotal = 0;
        ReadyToPayBonusGiven = false;
        PaymentAttempted = false;
        PaymentPhaseStartTime = 0;

        // CRITICAL: always release owner lock after order closes
        ownerTeamId = -1;
    }

    public int ServerPickupDirtyItemsToPlayer(Player player)
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer) return 0;
        if (player == null) return 0;
        if (DirtyItems.Count <= 0) return 0;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return 0;

        int moved = 0;

        // Move while we have dirty items and free carry slots
        while (DirtyItems.Count > 0 && carry.HasFreeSlot)
        {
            SceneObjectSO dirtySo = DirtyItems[0];
            DirtyItems.RemoveAt(0);

            if (dirtySo == null || dirtySo.prefab == null)
            {
                // skip broken entry
                continue;
            }

            var spawned = SceneObjectSpawn.SpawnServerUnparented(dirtySo);
            if (spawned == null)
            {
                // failed spawn; stop to avoid losing more entries silently
                break;
            }

            if (!carry.ServerTryPush(spawned))
            {
                // could happen if carry changed mid-loop
                spawned.DeleteObjectServer();
                // restore item back to queue head to avoid loss
                DirtyItems.Insert(0, dirtySo);
                break;
            }

            moved++;
        }

        RefreshDirtyVisualsServer();

        if (DirtyItems.Count == 0)
        {
            // Cleanup finished for this round
            ThrownObjects.Clear();
            ClearDeliveredVisualsServer();

            // release team ownership for next cycle
            ownerTeamId = -1;

            ChangeState(WorkstationPhaseId.Idle);
        }

        return moved;
    }

    public bool HasDirtyItems() => DirtyItems.Count > 0;

    public TutorialEventChannelSO GetTutorialEvents() => tutorialEvents;
    public string GetTutorialTableId() => tutorialTableId;

    private void ResolveCleanupStateOrReturnIdleServer()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        // If nothing to clean and no active NPC cleanup, table should be freed immediately.
        bool hasDirty = DirtyItems.Count > 0;
        bool hasNpcActivity = seatingController != null && seatingController.HasActiveNpcsOrCleanup();

        if (!hasDirty && !hasNpcActivity)
        {
            // Hard reset ownership/order leftovers
            HasActiveCommand = false;
            ActiveOrder = null;
            IsOrderRequestReady = false;
            CurrentOrderCoinTotal = 0;
            ReadyToPayBonusGiven = false;
            PaymentAttempted = false;
            PaymentPhaseStartTime = 0;
            ownerTeamId = -1;

            RemainingRequired.Clear();
            ThrownObjects.Clear();
            DirtyItems.Clear();

            if (netSync != null && netSync.IsSpawned)
                netSync.ServerClearRemainingRequired();

            ClearDeliveredVisualsServer();
            ClearDirtyVisualsServer();

            ChangeState(WorkstationPhaseId.Idle);
        }
        else
        {
            ChangeState(WorkstationPhaseId.NeedsCleanup);
        }
    }
}