using System;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class WorkstationNetSync : NetworkBehaviour, InterfaceProgressBar
{
    [SerializeField] private Workstation workstation;
    [SerializeField] private SinkStation sinkStation;
    [SerializeField] private InteractableSoundEmitter interactableSoundEmitter;

    public event EventHandler<InterfaceProgressBar.OnProgressChangedEventArgs> OnProgressChanged;
    [SerializeField] private TutorialEventChannelSO tutorialEvents;

    [Header("Net Progress Tuning")]
    [SerializeField] private float progressMinInterval = 0.15f;
    [SerializeField] private float progressMinDelta = 0.04f;

    [Header("Stale Hold Watchdog")]
    [Tooltip("Maximum seconds a hold lock can stay active server-side before it is force-released.")]
    [SerializeField] private float maxHoldSeconds = 6f;

    private float lastSentProgress = -1f;
    // Client-side: which workstation the local player is currently holding
    private bool holdActive;

    private ulong activeHolderClientId = ulong.MaxValue;
    private bool serverWorking;
    // Server-side: wall-clock timestamp when current hold started (for watchdog)
    private float holdStartTime;

    // Replicated edge state for low-cost workstation loop audio on all clients.
    private readonly NetworkVariable<bool> workLoopActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        if (sinkStation == null) sinkStation = GetComponent<SinkStation>();
        if (workstation == null) workstation = GetComponent<Workstation>();
        if (interactableSoundEmitter == null) interactableSoundEmitter = GetComponent<InteractableSoundEmitter>();
    }

    // ──────────────────────────────────────────────────────────
    // Server-side stale-hold watchdog
    // ──────────────────────────────────────────────────────────
    private void Update()
    {
        if (!IsServer) return;
        if (!serverWorking) return;

        if (Time.time - holdStartTime > maxHoldSeconds)
        {
            Debug.LogWarning($"[WorkstationNetSync] Stale hold watchdog triggered on '{name}' for clientId={activeHolderClientId}. Force-releasing.");
            ServerClearHolderLock();
        }
    }

    private void ApplyWorkLoopAudio(bool active, bool playStopOneShot)
    {
        if (interactableSoundEmitter == null) return;

        if (active) interactableSoundEmitter.StartWorkLoop();
        else interactableSoundEmitter.StopWorkLoop(playStopOneShot);
    }

    private void ServerSetWorkLoopActive(bool active)
    {
        if (!IsServer) return;

        bool previous = workLoopActive.Value;
        if (previous == active) return;

        workLoopActive.Value = active;
        ApplyWorkLoopAudio(active, playStopOneShot: previous && !active);
    }

    private void WorkLoopActive_OnValueChanged(bool previous, bool current)
    {
        if (IsServer) return;
        ApplyWorkLoopAudio(current, playStopOneShot: previous && !current);
    }

    // ──────────────────────────────────────────────────────────
    // Progress replication
    // ──────────────────────────────────────────────────────────
    public void ServerSetProgress(float normalized)
    {
        if (!IsServer) return;

        float clamped = Mathf.Clamp01(normalized);

        OnProgressChanged?.Invoke(this, new InterfaceProgressBar.OnProgressChangedEventArgs
        {
            progressNormalized = clamped
        });

        bool forceEdge = clamped <= 0f || clamped >= 1f;
        bool deltaEnough = Mathf.Abs(clamped - lastSentProgress) >= progressMinDelta;
        bool tickBudgetOk = NetTickBudget.CanSend(NetworkObjectId, 0, progressMinInterval);

        if (forceEdge || (deltaEnough && tickBudgetOk))
        {
            lastSentProgress = clamped;
            ProgressClientRpc(clamped);
        }
    }

    [ClientRpc]
    private void ProgressClientRpc(float normalized)
    {
        if (IsServer) return;
        OnProgressChanged?.Invoke(this, new InterfaceProgressBar.OnProgressChangedEventArgs
        {
            progressNormalized = normalized
        });
    }

    // ──────────────────────────────────────────────────────────
    // Client-side: hold request
    // ──────────────────────────────────────────────────────────
    public void RequestStartWork()
    {
        if (!IsClient) return;
        if (holdActive) return;
        holdActive = true;

        if (TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
            tutorialEvents.Raise(TutorialEventType.WorkStarted);

        RequestStartWorkServerRpc();
    }

    public void RequestStopWork()
    {
        if (!IsClient) return;
        if (!holdActive) return;
        holdActive = false;

        if (TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
            tutorialEvents.Raise(TutorialEventType.WorkStopped);

        RequestStopWorkServerRpc();
    }

    // ──────────────────────────────────────────────────────────
    // Server RPCs
    // ──────────────────────────────────────────────────────────
    [ServerRpc(RequireOwnership = false)]
    private void RequestStartWorkServerRpc(ServerRpcParams rpcParams = default)
    {
        if (workstation == null && sinkStation == null) return;
        if (NetworkManager.Singleton == null) return;

        ulong sender = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(sender, out var client) || client?.PlayerObject == null)
            return;

        var player = client.PlayerObject.GetComponent<Player>();
        if (player == null) return;

        // Block if a DIFFERENT player is already holding the lock
        if (serverWorking && activeHolderClientId != sender) return;

        bool started = false;
        if (workstation != null) started |= workstation.ServerStartWork(player);
        if (sinkStation != null) started |= sinkStation.ServerStartWork(player);

        if (!started) return;

        serverWorking = true;
        activeHolderClientId = sender;
        holdStartTime = Time.time;
        ServerSetWorkLoopActive(true);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStopWorkServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!serverWorking || activeHolderClientId != sender) return;

        if (NetworkManager.Singleton == null)
        {
            ServerClearHolderLock();
            return;
        }

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(sender, out var client) || client?.PlayerObject == null)
        {
            ServerClearHolderLock();
            return;
        }

        var player = client.PlayerObject.GetComponent<Player>();
        if (player == null)
        {
            ServerClearHolderLock();
            return;
        }

        if (workstation != null) workstation.ServerStopWork(player);
        if (sinkStation != null) sinkStation.ServerStopWork(player);

        ServerClearHolderLock();
    }

    public void ServerClearHolderLock()
    {
        if (!IsServer) return;
        serverWorking = false;
        activeHolderClientId = ulong.MaxValue;
        holdStartTime = 0f;
        ServerSetWorkLoopActive(false);
    }

    public override void OnNetworkSpawn()
    {
        workLoopActive.OnValueChanged += WorkLoopActive_OnValueChanged;

        // Apply initial replicated value (without stop one-shot).
        ApplyWorkLoopAudio(workLoopActive.Value, playStopOneShot: false);

        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    public override void OnNetworkDespawn()
    {
        workLoopActive.OnValueChanged -= WorkLoopActive_OnValueChanged;

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;

        // Scene teardown should not emit stop one-shot.
        ApplyWorkLoopAudio(false, playStopOneShot: false);

        NetTickBudget.ResetChannel(NetworkObjectId, 0);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        if (!serverWorking) return;
        if (activeHolderClientId != clientId) return;

        ServerClearHolderLock();
    }

    private void OnValidate()
    {
        progressMinInterval = Mathf.Clamp(progressMinInterval, 0.05f, 0.5f);
        progressMinDelta = Mathf.Clamp(progressMinDelta, 0.005f, 0.25f);
        maxHoldSeconds = Mathf.Max(1f, maxHoldSeconds);
    }
}