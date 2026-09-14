using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;

[RequireComponent(typeof(NetworkObject))]
public class TableNetSync : NetworkBehaviour
{
    public enum PaymentFeedbackResult : byte
    {
        Failed = 0,
        Succeeded = 1,
    }

    // Raised only on clients that receive payer-targeted payment feedback.
    public static event Action<PaymentFeedbackResult> OnLocalPaymentFeedback;

    [System.Serializable]
    private struct PhaseTransitionAudio
    {
        public WorkstationPhaseId fromPhase;
        public WorkstationPhaseId toPhase;
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume;
    }

    [SerializeField] private Table_StatePattern table;

    [Header("Required Elements Sync")]
    [SerializeField] private SceneObjectCatalogSO catalog;
    [SerializeField] private TutorialEventChannelSO tutorialEvents;

    [Header("Table Transition Audio (3D at Table)")]
    [SerializeField] private AudioSource networkAudioSource;
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField] private AudioClip[] readyToRequestClips;
    [SerializeField, Range(0f, 1f)] private float readyToRequestVolume = 0.9f;
    [SerializeField] private List<PhaseTransitionAudio> optionalTransitionClips = new();

    [Header("Global NPC Batch Audio (3D Door/Spawner Source)")]
    [SerializeField] private NpcBatchAudioController npcBatchAudioController;

    [Header("Table Spatial Settings")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 24f;

    private NetworkList<int> remainingRequiredIdx;

    private readonly NetworkVariable<int> phaseId =
        new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public WorkstationPhaseId CurrentPhase => (WorkstationPhaseId)phaseId.Value;

    private readonly NetworkVariable<double> phaseStartServerTime =
        new(0d, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> phaseDuration =
        new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<ushort> npcBatchDespawnEpoch =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public double PhaseStartServerTime => phaseStartServerTime.Value;
    public float PhaseDuration => phaseDuration.Value;

    public bool HasReplicatedTimerForCurrentPhase =>
        phaseDuration.Value > 0f && phaseId.Value >= 0 &&
        IsTimedPhase((WorkstationPhaseId)phaseId.Value);

    private static bool IsTimedPhase(WorkstationPhaseId p)
    {
        return p == WorkstationPhaseId.ReadyToRequest
            || p == WorkstationPhaseId.DeliverItems
            || p == WorkstationPhaseId.Consuming
            || p == WorkstationPhaseId.AwaitingPayment
            || p == WorkstationPhaseId.SeatingNPCs;
    }

    public float GetReplicatedTimerNormalized()
    {
        if (!HasReplicatedTimerForCurrentPhase) return 0f;
        if (NetworkManager.Singleton == null) return 0f;

        double now = NetworkManager.Singleton.LocalTime.Time;
        double elapsed = now - phaseStartServerTime.Value;
        float n = 1f - (float)(elapsed / Mathf.Max(0.0001f, phaseDuration.Value));
        return Mathf.Clamp01(n);
    }

    private void Awake()
    {
        if (table == null) table = GetComponent<Table_StatePattern>();
        remainingRequiredIdx = new NetworkList<int>();
        ResolveNetworkAudioSource();
        ResolveNpcBatchAudioController();
    }

    public override void OnNetworkSpawn()
    {
        remainingRequiredIdx.OnListChanged += OnRemainingRequiredChanged;
        phaseId.OnValueChanged += OnPhaseIdValueChanged;
        npcBatchDespawnEpoch.OnValueChanged += OnNpcBatchDespawnEpochChanged;
        if (IsClient) ApplyRemainingRequiredFromNet();
    }

    public override void OnNetworkDespawn()
    {
        if (remainingRequiredIdx != null)
            remainingRequiredIdx.OnListChanged -= OnRemainingRequiredChanged;
        phaseId.OnValueChanged -= OnPhaseIdValueChanged;
        npcBatchDespawnEpoch.OnValueChanged -= OnNpcBatchDespawnEpochChanged;
    }

    private void OnRemainingRequiredChanged(NetworkListEvent<int> changeEvent)
    {
        if (IsServer) return;
        ApplyRemainingRequiredFromNet();
    }

    private void ApplyRemainingRequiredFromNet()
    {
        if (table == null || catalog == null) return;

        table.RemainingRequired.Clear();
        for (int i = 0; i < remainingRequiredIdx.Count; i++)
        {
            var so = catalog.Get(remainingRequiredIdx[i]);
            if (so != null) table.RemainingRequired.Add(so);
        }

        table.NotifyRequiredChanged();
    }

    public void ServerSetRemainingRequired(IReadOnlyList<SceneObjectSO> list)
    {
        if (!IsServer || catalog == null) return;

        remainingRequiredIdx.Clear();
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            int idx = catalog.IndexOf(list[i]);
            if (idx >= 0) remainingRequiredIdx.Add(idx);
        }
    }

    public void ServerRemoveOneRequired(SceneObjectSO so)
    {
        if (!IsServer || catalog == null) return;

        int idx = catalog.IndexOf(so);
        if (idx < 0) return;

        for (int i = 0; i < remainingRequiredIdx.Count; i++)
        {
            if (remainingRequiredIdx[i] == idx)
            {
                remainingRequiredIdx.RemoveAt(i);
                break;
            }
        }
    }

    public void ServerClearRemainingRequired()
    {
        if (!IsServer) return;
        remainingRequiredIdx.Clear();
    }

    public void ServerSetPhaseTimer(WorkstationPhaseId phase, float durationSeconds)
    {
        if (!IsServer) return;

        phaseId.Value = (int)phase;

        if (durationSeconds > 0f && NetworkManager.Singleton != null)
        {
            phaseStartServerTime.Value = NetworkManager.Singleton.ServerTime.Time;
            phaseDuration.Value = durationSeconds;
        }
        else
        {
            phaseStartServerTime.Value = 0d;
            phaseDuration.Value = 0f;
        }
    }

    public void ServerNotifyNpcBatchDespawned()
    {
        if (!IsServer) return;
        unchecked
        {
            npcBatchDespawnEpoch.Value++;
        }
    }

    public void ServerSetReady(bool ready)
    {
        if (!IsServer) return;
        table.SetOrderRequestReady(ready);
    }

    public void RequestInteract()
    {
        if (!IsClient) return;
        RequestInteractServerRpc();
    }

    public bool ServerTryAcceptDroppedFromPlayer(Player player)
    {
        if (!IsServer || table == null) return false;
        if (player == null) return false;
        if (table.CurrentPhaseForNetSync != WorkstationPhaseId.DeliverItems) return false;
        if (!table.CanPlayerInteractByTeam(player.OwnerClientId)) return false;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.HasAny) return false;

        int topIndex = carry.Count - 1;
        if (!carry.TryGetHeldAt(topIndex, out SceneObject topObject) || topObject == null) return false;
        if (!topObject.IsFinalObject()) return false;

        SceneObjectSO so = topObject.GetSceneObjectSO();
        if (so == null) return false;

        int reqIdx = table.RemainingRequired.FindIndex(x => x == so);
        if (reqIdx < 0) return false;

        if (!carry.ServerTryRemoveAt(topIndex, out SceneObject removed) || removed == null) return false;

        removed.DeleteObjectServer();
        table.ThrownObjects.Add(so);
        table.RemainingRequired.RemoveAt(reqIdx);
        ServerRemoveOneRequired(so);

        table.NotifyRequiredChanged();
        table.RefreshDeliveredVisualsServer();

        var ev = table.GetTutorialEvents();
        if (TutorialRuntimeContext.IsTutorialRun && ev != null)
            ev.Raise(TutorialEventType.DeliveredRequiredItem, table.GetTutorialTableId());

        if (table.RemainingRequired.Count == 0)
        {
            if (TutorialRuntimeContext.IsTutorialRun && ev != null)
                ev.Raise(TutorialEventType.AllItemsDelivered, table.GetTutorialTableId());

            table.ChangeState(WorkstationPhaseId.Consuming);
        }

        return true;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        if (table == null) return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client?.PlayerObject == null)
            return;

        Player player = client.PlayerObject.GetComponent<Player>();
        if (player == null) return;

        if (table.CurrentPhaseForNetSync == WorkstationPhaseId.AwaitingPayment) return;
        if (!table.CanPlayerInteractByTeam(player.OwnerClientId)) return;

        var phaseBefore = table.CurrentPhaseForNetSync;
        table.ServerInteract(player);
        var phaseAfter = table.CurrentPhaseForNetSync;

        if (phaseBefore == WorkstationPhaseId.ReadyToRequest &&
            phaseAfter != WorkstationPhaseId.ReadyToRequest)
        {
            string payload = table.GetTutorialTableId();
            TutorialAckClientRpc((int)TutorialEventType.OrderAccepted, payload, ToSingleClient(clientId));
        }
    }

    [ClientRpc]
    private void ResyncClientRpc(bool isReady, int phaseIdValue)
    {
        if (table == null) return;
        if (phaseIdValue < 0) return;
        table.ApplyNetState(isReady, (WorkstationPhaseId)phaseIdValue);
    }

    public void ServerResyncState(bool isReady, int phaseIdValue)
    {
        if (!IsServer) return;

        if (this.phaseId.Value != phaseIdValue) this.phaseId.Value = phaseIdValue;
        ResyncClientRpc(isReady, phaseIdValue);
    }

    public void RequestPayment()
    {
        if (!IsClient) return;
        RequestPaymentServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPaymentServerRpc(ServerRpcParams rpcParams = default)
    {
        if (table == null) return;

        ulong clientId = rpcParams.Receive.SenderClientId;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client?.PlayerObject == null)
            return;

        Player player = client.PlayerObject.GetComponent<Player>();
        if (player == null) return;

        if (table.CurrentPhaseForNetSync != WorkstationPhaseId.AwaitingPayment) return;
        if (!table.CanPlayerInteractByTeam(player.OwnerClientId)) return;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.IsHoldingTPV()) return;

        table.ServerInteract(player);
    }

    public void RequestPayment(string input)
    {
        if (!IsClient) return;
        RequestPaymentServerRpc(input);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPaymentServerRpc(string input, ServerRpcParams rpcParams = default)
    {
        if (table == null) return;
        if (table.CurrentPhaseForNetSync != WorkstationPhaseId.AwaitingPayment) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client?.PlayerObject == null)
            return;

        Player player = client.PlayerObject.GetComponent<Player>();
        if (player == null) return;
        if (!table.CanPlayerInteractByTeam(player.OwnerClientId)) return;

        string payload = table.GetTutorialTableId();
        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.IsHoldingTPV())
        {
            tutorialEvents?.Raise(TutorialEventType.PaymentAttemptFailed, payload);
            SendPaymentFeedbackToClient(clientId, PaymentFeedbackResult.Failed);
            return;
        }

        if (!TryParseCoins(input, out int paidCoins))
        {
            table.PaymentAttempted = true;
            tutorialEvents?.Raise(TutorialEventType.PaymentAttemptFailed, payload);
            SendPaymentFeedbackToClient(clientId, PaymentFeedbackResult.Failed);
            return;
        }

        if (paidCoins != table.CurrentOrderCoinTotal)
        {
            table.PaymentAttempted = true;
            tutorialEvents?.Raise(TutorialEventType.PaymentAttemptFailed, payload);
            SendPaymentFeedbackToClient(clientId, PaymentFeedbackResult.Failed);
            return;
        }

        double now = NetworkManager.Singleton.ServerTime.Time;
        double elapsed = now - table.PaymentPhaseStartTime;

        bool firstAttempt = !table.PaymentAttempted;
        bool beforeHalf = elapsed <= (table.PaymentSeconds * 0.5f);
        bool perfect = firstAttempt && beforeHalf;
        table.PaymentAttempted = true;

        ScoreManager.Instance.RegisterPayment(perfect);

        ulong scorerId = player.OwnerClientId;
        float combo = ScoreManager.Instance.GetComboForClient(scorerId);
        int basePayment = table.CurrentOrderCoinTotal;

        float perfectMult = table.ScoringConfig != null ? table.ScoringConfig.perfectPaymentMultiplier : 1.5f;

        int payout = perfect
            ? Mathf.RoundToInt(basePayment * combo * perfectMult)
            : Mathf.RoundToInt(basePayment * combo);

        ScoreManager.Instance.AddScoreForClient(scorerId, payout);
        BroadcastTablePointsPopup(payout, 1);
        if (perfect) ScoreManager.Instance.IncreaseComboForClient(scorerId);

        tutorialEvents?.Raise(TutorialEventType.PaymentSucceeded, payload);
        SendPaymentFeedbackToClient(clientId, PaymentFeedbackResult.Succeeded);
        table.ServerInteract(player);
    }

    private bool TryParseCoins(string input, out int coins)
    {
        coins = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!int.TryParse(input, out coins)) return false;
        if (coins < 0) return false;
        return true;
    }

    public void RaiseTutorialPhaseEvent(WorkstationPhaseId phase)
    {
        if (!IsServer) return;
        string payload = table != null ? table.GetTutorialTableId() : null;

        switch (phase)
        {
            case WorkstationPhaseId.ReadyToRequest:
                tutorialEvents?.Raise(TutorialEventType.TableReadyToRequest, payload);
                break;
            case WorkstationPhaseId.AwaitingPayment:
                tutorialEvents?.Raise(TutorialEventType.PaymentPhaseStarted, payload);
                break;
        }
    }

    [ClientRpc]
    private void TutorialAckClientRpc(int eventType, string payload, ClientRpcParams clientRpcParams = default)
    {
        if (tutorialEvents == null) return;
        tutorialEvents.Raise((TutorialEventType)eventType, payload);
    }

    private static ClientRpcParams ToSingleClient(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { clientId }
            }
        };
    }

    private void SendPaymentFeedbackToClient(ulong clientId, PaymentFeedbackResult result)
    {
        PaymentFeedbackClientRpc((byte)result, ToSingleClient(clientId));
    }

    [ClientRpc]
    private void PaymentFeedbackClientRpc(byte resultValue, ClientRpcParams clientRpcParams = default)
    {
        if (!Enum.IsDefined(typeof(PaymentFeedbackResult), resultValue))
            return;

        OnLocalPaymentFeedback?.Invoke((PaymentFeedbackResult)resultValue);
    }

    [ClientRpc]
    private void TablePointsPopupClientRpc(int points, int kind)
    {
        var presenter = GetComponent<TablePointsPopupPresenter>();
        if (presenter == null) return;

        switch (kind)
        {
            case 0: presenter.ShowTip(points); break;
            case 1: presenter.ShowPayment(points); break;
            case 2: presenter.ShowPenalty(points); break;
        }
    }

    public void BroadcastTablePointsPopup(int points, int kind)
    {
        if (!IsServer || !IsSpawned) return;
        if (points <= 0) return;
        TablePointsPopupClientRpc(points, kind);
    }

    private void OnPhaseIdValueChanged(int previousValue, int currentValue)
    {
        if (currentValue < 0) return;

        WorkstationPhaseId currentPhase = (WorkstationPhaseId)currentValue;
        if (currentPhase == WorkstationPhaseId.SeatingNPCs)
            npcBatchAudioController?.PlayNpcSpawnBatch();

        bool playedOptional = false;
        if (previousValue >= 0)
            playedOptional = TryPlayOptionalTransitionClip((WorkstationPhaseId)previousValue, currentPhase);

        if (!playedOptional &&
            previousValue >= 0 &&
            (WorkstationPhaseId)previousValue == WorkstationPhaseId.SeatingNPCs &&
            currentPhase == WorkstationPhaseId.ReadyToRequest)
        {
            PlayReplicatedWorldOneShotFromPool(readyToRequestClips, readyToRequestVolume);
        }
    }

    private void OnNpcBatchDespawnEpochChanged(ushort previousValue, ushort currentValue)
    {
        if (currentValue == previousValue) return;
        npcBatchAudioController?.PlayNpcDespawnBatch();
    }

    private void PlayReplicatedWorldOneShot(AudioClip clip, float volume)
    {
        if (networkAudioSource == null || clip == null) return;
        networkAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void PlayReplicatedWorldOneShotFromPool(AudioClip[] clipPool, float volume)
    {
        AudioClip clip = PickRandomClip(clipPool);
        if (clip == null) return;
        PlayReplicatedWorldOneShot(clip, volume);
    }

    private void ResolveNetworkAudioSource()
    {
        if (networkAudioSource == null)
            networkAudioSource = GetComponent<AudioSource>();
        if (networkAudioSource == null)
            networkAudioSource = gameObject.AddComponent<AudioSource>();

        networkAudioSource.playOnAwake = false;
        networkAudioSource.loop = false;
        networkAudioSource.spatialBlend = spatialBlend;
        networkAudioSource.minDistance = minDistance;
        networkAudioSource.maxDistance = maxDistance;
        networkAudioSource.rolloffMode = AudioRolloffMode.Linear;
        if (outputMixerGroup != null)
            networkAudioSource.outputAudioMixerGroup = outputMixerGroup;
    }

    private void ResolveNpcBatchAudioController()
    {
        if (npcBatchAudioController != null) return;
        if (NpcBatchAudioController.Instance != null)
        {
            npcBatchAudioController = NpcBatchAudioController.Instance;
            return;
        }

        npcBatchAudioController = FindFirstObjectByType<NpcBatchAudioController>(FindObjectsInactive.Exclude);
    }

    private bool TryPlayOptionalTransitionClip(WorkstationPhaseId fromPhase, WorkstationPhaseId toPhase)
    {
        if (optionalTransitionClips == null || optionalTransitionClips.Count == 0) return false;

        for (int i = 0; i < optionalTransitionClips.Count; i++)
        {
            PhaseTransitionAudio cfg = optionalTransitionClips[i];
            if (cfg.fromPhase != fromPhase || cfg.toPhase != toPhase) continue;
            AudioClip picked = PickRandomClip(cfg.clips);
            if (picked == null) return false;

            PlayReplicatedWorldOneShot(picked, cfg.volume);
            return true;
        }

        return false;
    }

    private static AudioClip PickRandomClip(AudioClip[] clipPool)
    {
        if (clipPool == null || clipPool.Length == 0) return null;
        int start = UnityEngine.Random.Range(0, clipPool.Length);
        for (int i = 0; i < clipPool.Length; i++)
        {
            AudioClip clip = clipPool[(start + i) % clipPool.Length];
            if (clip != null) return clip;
        }
        return null;
    }

    private void OnValidate()
    {
        readyToRequestVolume = Mathf.Clamp01(readyToRequestVolume);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        ResolveNetworkAudioSource();
        ResolveNpcBatchAudioController();
    }
}