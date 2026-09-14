using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(WorkstationNetSync))]
public class SinkStation : InteractableAsset, InterfaceProgressBar
{
    public event EventHandler<InterfaceProgressBar.OnProgressChangedEventArgs> OnProgressChanged;

    [Serializable]
    public class CleanReturnEntry
    {
        public SceneObjectSO cleanSo;
        public ObjectSpawner targetSpawner;
    }

    [Header("Cleaning")]
    [SerializeField] private float cleanSecondsPerItem = 1.5f;
    [SerializeField] private int maxQueue = 3;

    [Header("Sink-local return mapping")]
    [SerializeField] private List<CleanReturnEntry> returnMap = new();

    [Header("Visual queue slots (size should match maxQueue)")]
    [SerializeField] private Transform[] queueSlots;

    [Header("Catalog for visual net sync")]
    [SerializeField] private SceneObjectCatalogSO catalog;

    [Header("Optional net visual component (auto if null)")]
    [SerializeField] private SinkQueueVisualNetSync queueVisualNetSync;

    [Header("Washing FX")]
    [SerializeField] private SinkWashFxController washFx; 

    [Header("Interaction Access")]
    [SerializeField] private TeamInteractionAccess interactionAccess = TeamInteractionAccess.All;

    [Header("Tutorial")]
    [SerializeField] private TutorialEventChannelSO tutorialEvents;
    [SerializeField] private string tutorialSinkId = "Sink";

    private WorkstationNetSync netSync;
    private readonly List<SceneObjectSO> queuedDirtySOs = new();

    private float accumulatedProgressSeconds;
    private ulong activeWasherClientId = ulong.MaxValue;
    private bool isWashing;
    private bool lastFxState;
    private float _serverProgressTick;
    private const float ServerProgressInterval = 0.1f;

    private void Awake()
    {
        netSync = GetComponent<WorkstationNetSync>();
        if (queueVisualNetSync == null) queueVisualNetSync = GetComponent<SinkQueueVisualNetSync>();
        if (washFx == null) washFx = GetComponentInChildren<SinkWashFxController>(true); 
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            SyncQueueVisualsServer();

        SetWashingFx(false); 
    }

    public override void Interact(Player player)
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsServer)
            ServerHandleDepositInteract(player);
    }

    public bool ServerStartWork(Player player)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return false;
        if (player == null) return false;
        if (!CanPlayerUse(player)) return false;

        ulong clientId = player.OwnerClientId;

        if (activeWasherClientId != ulong.MaxValue && activeWasherClientId != clientId)
            return false;

        if (queuedDirtySOs.Count <= 0)
        {
            isWashing = false;
            activeWasherClientId = ulong.MaxValue;
            netSync?.ServerSetProgress(0f);
            SetWashingFx(false); 
            return false;
        }

        activeWasherClientId = clientId;
        isWashing = true;
        SetWashingFx(true); 

        float normalized = Mathf.Clamp01(accumulatedProgressSeconds / Mathf.Max(0.01f, cleanSecondsPerItem));
        netSync?.ServerSetProgress(normalized);
        return true;
    }

    public void ServerStopWork(Player player)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (player == null) return;
        if (activeWasherClientId != player.OwnerClientId) return;

        isWashing = false;
        activeWasherClientId = ulong.MaxValue;
        SetWashingFx(false); 

        float normalized = Mathf.Clamp01(accumulatedProgressSeconds / Mathf.Max(0.01f, cleanSecondsPerItem));
        netSync?.ServerSetProgress(normalized);
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (!isWashing) return;

        if (queuedDirtySOs.Count <= 0)
        {
            isWashing = false;
            activeWasherClientId = ulong.MaxValue;
            accumulatedProgressSeconds = 0f;
            netSync?.ServerSetProgress(0f);
            SetWashingFx(false); 
            return;
        }

        _serverProgressTick += Time.deltaTime;
        if (_serverProgressTick < ServerProgressInterval)
            return;

        float dt = _serverProgressTick;
        _serverProgressTick = 0f;

        accumulatedProgressSeconds += dt;
        float normalized = Mathf.Clamp01(accumulatedProgressSeconds / Mathf.Max(0.01f, cleanSecondsPerItem));
        netSync?.ServerSetProgress(normalized);

        if (accumulatedProgressSeconds >= cleanSecondsPerItem)
        {
            CompleteFrontItemWashServer();
            accumulatedProgressSeconds = 0f;

            if (queuedDirtySOs.Count > 0)
            {
                netSync?.ServerSetProgress(0f);
                SetWashingFx(true); // keep on
            }
            else
            {
                isWashing = false;
                activeWasherClientId = ulong.MaxValue;
                netSync?.ServerSetProgress(0f);
                SetWashingFx(false); 
            }
        }
    }

    public int ServerDepositDirtyFromPlayer(Player player)
    {
        if (player == null) return 0;
        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return 0;

        int moved = 0;

        while (queuedDirtySOs.Count < maxQueue)
        {
            int dirtyIndex = FindTopDirtyIndex(carry);
            if (dirtyIndex < 0) break;

            if (!carry.ServerTryRemoveAt(dirtyIndex, out SceneObject removed) || removed == null)
                break;

            SceneObjectSO so = removed.GetSceneObjectSO();
            removed.DeleteObjectServer();

            if (so == null || !so.isDirtyItem)
                continue;

            queuedDirtySOs.Add(so);
            moved++;
        }

        if (moved > 0)
        {
            SyncQueueVisualsServer();
            if (!isWashing)
            {
                float normalized = Mathf.Clamp01(accumulatedProgressSeconds / Mathf.Max(0.01f, cleanSecondsPerItem));
                netSync?.ServerSetProgress(normalized);
            }
        }

        return moved;
    }

    private int FindTopDirtyIndex(PlayerCarryNet carry)
    {
        for (int i = carry.Count - 1; i >= 0; i--)
        {
            if (!carry.TryGetHeldAt(i, out SceneObject obj) || obj == null) continue;
            SceneObjectSO so = obj.GetSceneObjectSO();
            if (so == null || !so.isDirtyItem) continue;
            return i;
        }
        return -1;
    }

    private void CompleteFrontItemWashServer()
    {
        if (queuedDirtySOs.Count <= 0) return;

        SceneObjectSO dirtySo = queuedDirtySOs[0];
        queuedDirtySOs.RemoveAt(0);

        SceneObjectSO cleanSo = dirtySo != null ? dirtySo.cleanedResultSO : null;
        if (cleanSo == null)
        {
            Debug.LogWarning($"[SinkStation] Dirty item missing cleanedResultSO: {dirtySo?.objectName}");
            SyncQueueVisualsServer();
            return;
        }

        ObjectSpawner spawner = FindSpawnerForCleanSo(cleanSo);
        if (spawner == null)
        {
            Debug.LogWarning($"[SinkStation] No mapping on sink '{name}' for clean SO '{cleanSo.objectName}'");
            SyncQueueVisualsServer();
            return;
        }

        spawner.ServerAddStock(1);




        if (TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
        {
            // Implementation A: sink-based payload
            tutorialEvents.Raise(TutorialEventType.SinkItemCleaned, tutorialSinkId);

            // Implementation B: cleaned item id
            // string cleanedId = !string.IsNullOrWhiteSpace(cleanSo.tutorialId) ? cleanSo.tutorialId : cleanSo.objectName;
            // tutorialEvents.Raise(TutorialEventType.SinkItemCleaned, cleanedId);
        }
        SyncQueueVisualsServer();
    }

    private ObjectSpawner FindSpawnerForCleanSo(SceneObjectSO cleanSo)
    {
        if (cleanSo == null) return null;
        for (int i = 0; i < returnMap.Count; i++)
        {
            var e = returnMap[i];
            if (e == null || e.cleanSo == null || e.targetSpawner == null) continue;
            if (e.cleanSo == cleanSo) return e.targetSpawner;
        }
        return null;
    }

    private void SyncQueueVisualsServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (queueVisualNetSync == null) return;
        queueVisualNetSync.ServerSetQueue(queuedDirtySOs);
    }

    private void SetWashingFx(bool on)
    {
        if (washFx != null)
            washFx.SetWashingVisual(on);
    }

    public int GetQueuedCount() => queuedDirtySOs.Count;
    public float GetNormalizedProgress() => Mathf.Clamp01(accumulatedProgressSeconds / Mathf.Max(0.01f, cleanSecondsPerItem));

    public void ServerHandleDepositInteract(Player player)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (player == null) return;
        if (!CanPlayerUse(player)) return;

        int moved = ServerDepositDirtyFromPlayer(player);

        if (moved > 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SinkStation] Deposited {moved} dirty item(s) into sink '{name}' by client={player.OwnerClientId}");
#endif
            if (TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
                tutorialEvents.Raise(TutorialEventType.SinkItemDeposited, tutorialSinkId);
        }
    }

    public bool ServerTryAcceptDroppedFromPlayer(Player player)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return false;
        if (player == null) return false;
        if (!CanPlayerUse(player)) return false;

        int moved = ServerDepositDirtyFromPlayer(player);
        if (moved <= 0) return false;

        if (TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
            tutorialEvents.Raise(TutorialEventType.SinkItemDeposited, tutorialSinkId);

        return true;
    }

    private bool CanPlayerUse(Player player)
    {
        if (player == null) return false;
        return TeamInteractionRules.CanPlayerInteract(player.OwnerClientId, interactionAccess);
    }

}