using System;
using System.Collections.Generic;
using UnityEngine;

public class Workstation : InteractableAsset, InterfaceProgressBar
{
    private const float MinWorkProgressMax = 0.01f;
    public event EventHandler<InterfaceProgressBar.OnProgressChangedEventArgs> OnProgressChanged;

    [SerializeField] private SceneObjectTransformSO[] sceneObjectTransformArray;
    [SerializeField] private float workSpeed = 1f;
    [SerializeField] private Transform[] processingPoints;
    [SerializeField] private int maxConcurrentItems = 2;

    [Header("Interaction Access")]
    [SerializeField] private TeamInteractionAccess interactionAccess = TeamInteractionAccess.All;

    [SerializeField] private TutorialEventChannelSO tutorialEvents;

    private bool isWorking;
    private Player workingPlayer;
    private WorkstationNetSync netSync;
    private readonly List<WorkSlot> slots = new();
    private float _serverProgressTick;
    private const float ServerProgressInterval = 0.1f;

    private sealed class WorkSlot : InterfaceSceneObjectParent
    {
        private readonly Workstation owner;
        private readonly Transform anchor;
        private SceneObject sceneObject;
        private bool hasFinishedOutput;

        public float Progress { get; set; }

        public WorkSlot(Workstation owner, Transform anchor)
        {
            this.owner = owner;
            this.anchor = anchor ?? owner.transform;
        }

        public Transform GetSceneObjectSpawnReference() => anchor;
        public void SetSceneObject(SceneObject sceneObject) => this.sceneObject = sceneObject;
        public SceneObject GetSceneObject() => sceneObject;
        public void ClearSceneObject()
        {
            sceneObject = null;
            hasFinishedOutput = false;
        }
        public bool HasSceneObject() => sceneObject != null;
        public bool HasFinishedOutput() => hasFinishedOutput && sceneObject != null;
        public void MarkAsFinishedOutput() => hasFinishedOutput = true;
        public void MarkAsInput() => hasFinishedOutput = false;

        public bool CanAccept(SceneObject candidate)
        {
            if (candidate == null) return false;
            if (HasSceneObject()) return false;
            return owner.HasObjectTransform(candidate.GetSceneObjectSO());
        }

        public void ClearSlot()
        {
            sceneObject = null;
            Progress = 0f;
            hasFinishedOutput = false;
        }
    }

    private void Awake()
    {
        netSync = GetComponent<WorkstationNetSync>();
        BuildSlots();
    }

    private void BuildSlots()
    {
        slots.Clear();

        int desiredSlots = Mathf.Max(1, maxConcurrentItems);
        if (processingPoints != null)
        {
            for (int i = 0; i < processingPoints.Length && slots.Count < desiredSlots; i++)
            {
                if (processingPoints[i] == null) continue;
                slots.Add(new WorkSlot(this, processingPoints[i]));
            }
        }

        while (slots.Count < desiredSlots)
            slots.Add(new WorkSlot(this, transform));
    }

    private void Update()
    {
        if (!Unity.Netcode.NetworkManager.Singleton || !Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        if (!isWorking || workingPlayer == null) return;

        _serverProgressTick += Time.deltaTime;
        if (_serverProgressTick < ServerProgressInterval)
            return;

        float dt = _serverProgressTick;
        _serverProgressTick = 0f;

        bool anyProcessable = false;
        float maxNormalized = 0f;

        for (int i = 0; i < slots.Count; i++)
        {
            WorkSlot slot = slots[i];
            SceneObject input = slot.GetSceneObject();
            if (input == null) continue;
            if (slot.HasFinishedOutput()) continue;

            SceneObjectTransformSO transformSO = GetSceneObjectTransformSO(input.GetSceneObjectSO());
            if (transformSO == null) continue;

            anyProcessable = true;
            slot.Progress += workSpeed * dt;

            float normalized = slot.Progress / Mathf.Max(MinWorkProgressMax, transformSO.workProgressMax);
            maxNormalized = Mathf.Max(maxNormalized, Mathf.Clamp01(normalized));

            if (slot.Progress >= transformSO.workProgressMax)
                CompleteSlotServer(slot, transformSO);
        }

        if (!anyProcessable)
        {
            isWorking = false;
            workingPlayer = null;
            netSync?.ServerSetProgress(0f);
            // Release the NetSync hold-lock so the next player (any team) can start
            netSync?.ServerClearHolderLock();
            return;
        }

        netSync?.ServerSetProgress(maxNormalized);
    }

    public bool ServerStartWork(Player player)
    {
        if (player == null) return false;
        if (!CanPlayerUse(player)) return false;

        ResolveImmediateInteractionServer(player);

        if (!HasAnyProcessableSceneObject()) return false;

        workingPlayer = player;
        isWorking = true;
        PushProgressForCurrentInputs();
        return true;
    }

    public void ServerStopWork(Player player)
    {
        if (workingPlayer == player)
        {
            isWorking = false;
            workingPlayer = null;
        }
    }

    public override bool CanAccept(SceneObject sceneObject)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].CanAccept(sceneObject))
                return true;
        }
        return false;
    }

    public override void Interact(Player player)
    {
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            !Unity.Netcode.NetworkManager.Singleton.IsServer)
            return;

        ServerInteract(player);
    }

    public void ServerInteract(Player player)
    {
        if (player == null) return;
        if (!CanPlayerUse(player)) return;

        if (ResolveImmediateInteractionServer(player))
            PushProgressForCurrentInputs();
    }

    public bool ServerTryAcceptDroppedFromPlayer(Player player)
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsServer)
            return false;
        if (player == null) return false;
        if (!CanPlayerUse(player)) return false;

        if (!TryPlaceOneItemFromPlayer(player))
            return false;

        PushProgressForCurrentInputs();
        return true;
    }

    public override Transform GetSceneObjectSpawnReference()
    {
        if (slots.Count <= 0) return transform;
        return slots[0].GetSceneObjectSpawnReference();
    }

    private bool TryPlaceOneItemFromPlayer(Player player)
    {
        if (player == null) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            WorkSlot slot = slots[i];
            if (slot.HasSceneObject()) continue;

            if (SceneObjectTransferCarry.TryTransferAnyFromPlayerTo(player, slot))
            {
                slot.Progress = 0f;
                slot.MarkAsInput();
                return true;
            }
        }
        return false;
    }

    private bool TryCollectOneFinishedOutputToPlayer(Player player)
    {
        if (player == null) return false;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.HasFreeSlot) return false;

        for (int i = 0; i < slots.Count; i++)
        {
            WorkSlot slot = slots[i];
            if (!slot.HasFinishedOutput()) continue;

            SceneObject output = slot.GetSceneObject();

            output.ClearSceneObjectParent();
            slot.ClearSlot();

            if (carry.ServerTryPush(output))
                return true;

            output.SetSceneObjectParent(slot);
            slot.MarkAsFinishedOutput();
            return false;
        }

        return false;
    }

    private bool ResolveImmediateInteractionServer(Player player)
    {
        if (player == null) return false;

        bool changed = false;
        bool progressed;

        do
        {
            progressed = false;

            if (TryCollectOneFinishedOutputToPlayer(player))
            {
                changed = true;
                progressed = true;
            }

            if (TryPlaceOneItemFromPlayer(player))
            {
                changed = true;
                progressed = true;
            }
        }
        while (progressed);

        return changed;
    }

    private bool HasAnyProcessableSceneObject()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            SceneObject obj = slots[i].GetSceneObject();
            if (obj == null) continue;
            if (HasObjectTransform(obj.GetSceneObjectSO())) return true;
        }
        return false;
    }

    private void PushProgressForCurrentInputs()
    {
        float maxNormalized = 0f;

        for (int i = 0; i < slots.Count; i++)
        {
            SceneObject obj = slots[i].GetSceneObject();
            if (obj == null) continue;

            SceneObjectTransformSO transformSO = GetSceneObjectTransformSO(obj.GetSceneObjectSO());
            if (transformSO == null) continue;

            float normalized = slots[i].Progress / Mathf.Max(MinWorkProgressMax, transformSO.workProgressMax);
            maxNormalized = Mathf.Max(maxNormalized, Mathf.Clamp01(normalized));
        }

        netSync?.ServerSetProgress(maxNormalized);
    }

    private void CompleteSlotServer(WorkSlot slot, SceneObjectTransformSO transformSO)
    {
        SceneObject inputObj = slot.GetSceneObject();
        if (inputObj != null)
            inputObj.DeleteObjectServer();

        slot.ClearSlot();

        SceneObjectSO outputSceneObjectSO = transformSO != null ? transformSO.output : null;
        if (outputSceneObjectSO == null) return;

        SceneObject outputObj = SceneObjectSpawn.SpawnServerUnparented(outputSceneObjectSO);
        if (outputObj == null) return;

        bool transferred = false;
        if (workingPlayer != null)
        {
            var carry = workingPlayer.GetComponent<PlayerCarryNet>();
            if (carry != null && carry.HasFreeSlot)
                transferred = carry.ServerTryPush(outputObj);
        }

        if (!transferred)
        {
            outputObj.SetSceneObjectParent(slot);
            slot.MarkAsFinishedOutput();
        }

        if (transferred && TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
        {
            string itemId = !string.IsNullOrWhiteSpace(outputSceneObjectSO.tutorialId)
                ? outputSceneObjectSO.tutorialId
                : outputSceneObjectSO.objectName;

            tutorialEvents.Raise(TutorialEventType.PickedUpItem, itemId);
            Debug.Log($"[Tutorial] PickedUpItem from Workstation itemId='{itemId}'");
        }
    }

    private bool HasObjectTransform(SceneObjectSO inputSceneObjectSO)
    {
        return GetSceneObjectTransformSO(inputSceneObjectSO) != null;
    }

    private SceneObjectTransformSO GetSceneObjectTransformSO(SceneObjectSO inputSceneObjectSO)
    {
        foreach (SceneObjectTransformSO sceneObjectTransformSO in sceneObjectTransformArray)
        {
            if (sceneObjectTransformSO.input == inputSceneObjectSO)
                return sceneObjectTransformSO;
        }
        return null;
    }

    private bool CanPlayerUse(Player player)
    {
        if (player == null) return false;
        return TeamInteractionRules.CanPlayerInteract(player.OwnerClientId, interactionAccess);
    }
}
