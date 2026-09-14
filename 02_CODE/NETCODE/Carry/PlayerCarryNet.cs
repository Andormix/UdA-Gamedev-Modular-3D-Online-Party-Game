using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// Server-authoritative multi-item carry for Player.
[RequireComponent(typeof(Player))]
public class PlayerCarryNet : NetworkBehaviour
{
    public const int MaxCarry = 3;
    public event Action<SceneObject> OnLocalItemPickedUp;
    public event Action<SceneObject> OnLocalItemDropped;
    public event Action<SceneObject, bool> OnReplicatedItemPickedUp;
    public event Action<SceneObject, bool> OnReplicatedItemDropped;

    [Header("Tuto Event")]
    [SerializeField] private TutorialEventChannelSO tutorialEvents;

    [Header("Sockets (size must be 3)")]
    [SerializeField] private Transform[] carrySockets = new Transform[MaxCarry];

    [Header("Animation")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string carryBoolParam = "IsCarrying";
    private int carryBoolHash;

    // Order is bottom a top (0..Count-1). "Top" is last.
    private NetworkList<NetworkObjectReference> heldStack; // Millora 01
    private Player player;

    // CLIENT-ONLY: track what we had bound last frame so we can unbind removed ones.
    private readonly HashSet<ulong> clientPreviouslyHeldIds = new HashSet<ulong>();

    // Reused per ClientRebindAll call to avoid per-event heap allocation.
    private readonly HashSet<ulong> _rebindCurrentIds = new HashSet<ulong>();

    private void Awake()
    {
        player = GetComponent<Player>();
        heldStack = new NetworkList<NetworkObjectReference>(); // Millora 01

        carryBoolHash = Animator.StringToHash(carryBoolParam);

        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>(true);
    }

    public override void OnNetworkSpawn()
    {
        heldStack.OnListChanged += HeldStack_OnListChanged;

        if (IsClient)
        {
            ClientRebindAll();
            RefreshCarryAnimation();
        }
    }


    public override void OnNetworkDespawn()
    {
        if (heldStack != null)
            heldStack.OnListChanged -= HeldStack_OnListChanged;
        if (playerAnimator != null) playerAnimator.SetBool(carryBoolHash, false);
        
    }

    private void HeldStack_OnListChanged(NetworkListEvent<NetworkObjectReference> changeEvent)
    {
        if (!IsClient) return;

        bool isAdd = changeEvent.Type == NetworkListEvent<NetworkObjectReference>.EventType.Add;
        bool isRemove = changeEvent.Type == NetworkListEvent<NetworkObjectReference>.EventType.RemoveAt
                     || changeEvent.Type == NetworkListEvent<NetworkObjectReference>.EventType.Remove;

        if ((isAdd || isRemove) && changeEvent.Value.TryGet(out NetworkObject no) && no != null)
        {
            var sceneObj = no.GetComponent<SceneObject>();

            if (isAdd)
            {
                OnReplicatedItemPickedUp?.Invoke(sceneObj, IsOwner);
                if (IsOwner) OnLocalItemPickedUp?.Invoke(sceneObj);
            }
            else
            {
                OnReplicatedItemDropped?.Invoke(sceneObj, IsOwner);
                if (IsOwner) OnLocalItemDropped?.Invoke(sceneObj);
            }
        }

        ClientRebindAll();
        RefreshCarryAnimation();
    }

    public Transform GetCarrySocket(int index)
    {
        if (carrySockets == null) return null;
        if (index < 0 || index >= carrySockets.Length) return null;
        return carrySockets[index];
    }

    // ----- Query API -----
    public int Count => heldStack.Count;
    public bool HasAny => heldStack.Count > 0;
    //public bool HasFreeSlot => heldStack.Count < MaxCarry;
    public bool HasFreeSlot => heldStack.Count < MaxCarry; // && !IsHoldingTPV()

    public bool TryGetHeldAt(int index, out SceneObject obj)
    {
        obj = null;
        if (index < 0 || index >= heldStack.Count) return false;

        try
        {
            if (!heldStack[index].TryGet(out NetworkObject no) || no == null) return false;
            obj = no.GetComponent<SceneObject>();
            return obj != null;
        }
        catch (System.Exception)
        {
            // Race condition: reference invalid during despawn / scene transition frame
            return false;
        }
    }

    public bool TryGetTop(out SceneObject obj)
    {
        obj = null;
        if (heldStack.Count <= 0) return false;
        return TryGetHeldAt(heldStack.Count - 1, out obj);
    }

    public NetworkList<NetworkObjectReference> GetHeldRefs() => heldStack;

    // ----- Server API -----
    public bool ServerTryPush(SceneObject obj)
    {
        if (!IsServer || obj == null) return false;
        if (heldStack.Count >= MaxCarry) return false;

        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned) return false;
        if (!obj.CanBePickedUp()) return false;

        // Parent under player root (NGO allowed)
        netObj.TrySetParent(player.NetworkObject, worldPositionStays: false);

        heldStack.Add(netObj);

        // Snap server-side for immediate host visuals
        ServerSnapToSocketIndex(obj, heldStack.Count - 1);

        obj.ServerSetHeldState(true);

        // Tutorial event (server-aut)
        SceneObjectSO so = obj.GetSceneObjectSO();
        if (so != null && !string.IsNullOrWhiteSpace(so.tutorialId))
        {
            // Server->Owner ACK (alfred) (tutorial is now local UI) 
            var sendParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };
            TutorialAckClientRpc((int)TutorialEventType.PickedUpItem, so.tutorialId, sendParams);
        }



        return true;
    }

    public bool ServerTryPop(out SceneObject popped)
    {
        popped = null;
        if (!IsServer) return false;
        if (heldStack.Count <= 0) return false;

        int top = heldStack.Count - 1;

        if (!heldStack[top].TryGet(out NetworkObject no) || no == null)
        {
            heldStack.RemoveAt(top);
            return false;
        }

        popped = no.GetComponent<SceneObject>();
        heldStack.RemoveAt(top);

        no.TrySetParent((NetworkObject)null, worldPositionStays: true);

        if (popped != null)
            popped.ServerSetHeldState(false);

        ServerResnapAll();
        return popped != null;
    }

    public void ServerDropTop(Vector3 dropPos)
    {
        ServerDropTop(dropPos, Vector3.zero, ForceMode.Impulse);
    }

    public void ServerDropTop(Vector3 dropPos, Vector3 impulse, ForceMode impulseMode)
    {
        if (!IsServer) return;

        if (!ServerTryPop(out var obj) || obj == null) return;

        obj.transform.position = dropPos;

        var nt = obj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
            nt.Teleport(obj.transform.position, obj.transform.rotation, obj.transform.localScale);

        if (impulse.sqrMagnitude <= 0.0001f)
            return;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null || rb.isKinematic)
            return;

        rb.AddForce(impulse, impulseMode);
    }

    public int ServerDeliverAllFinalItemsToTable(Table_StatePattern table)
    {
        if (!IsServer || table == null) return 0;

        int delivered = 0;

        while (heldStack.Count > 0)
        {
            if (!TryGetTop(out var obj) || obj == null)
            {
                ServerTryPop(out _);
                continue;
            }

            if (!obj.IsFinalObject())
                break;

            SceneObjectSO so = obj.GetSceneObjectSO();

            ServerTryPop(out var popped);
            if (popped != null)
                popped.DeleteObjectServer();

            delivered++;

            int idx = table.RemainingRequired.FindIndex(x => x == so);
            if (idx >= 0)
            {
                table.RemainingRequired.RemoveAt(idx);
                var net = table.GetComponent<TableNetSync>();
                if (net != null)
                    net.ServerRemoveOneRequired(so);
            }

            table.ThrownObjects.Add(so);
        }

        return delivered;
    }

    private void ServerResnapAll()
    {
        if (!IsServer) return;

        for (int i = 0; i < heldStack.Count; i++)
        {
            if (!heldStack[i].TryGet(out NetworkObject no) || no == null) continue;
            var obj = no.GetComponent<SceneObject>();
            if (obj == null) continue;

            ServerSnapToSocketIndex(obj, i);
        }
    }

    private void ServerSnapToSocketIndex(SceneObject obj, int socketIndex)
    {
        Transform socket = GetCarrySocket(socketIndex);
        if (socket == null) return;

        Transform root = player.transform;

        Vector3 localPos = root.InverseTransformPoint(socket.position);
        Quaternion localRot = Quaternion.Inverse(root.rotation) * socket.rotation;

        obj.transform.localPosition = localPos;
        obj.transform.localRotation = localRot;

        var nt = obj.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
            nt.Teleport(obj.transform.position, obj.transform.rotation, obj.transform.localScale);
    }

    // ----- Client binding -----
    private void ClientRebindAll()
    {
        // Build current set (reuse field to avoid per-call heap allocation)
        _rebindCurrentIds.Clear();

        for (int i = 0; i < heldStack.Count; i++)
        {
            if (!heldStack[i].TryGet(out NetworkObject no) || no == null) continue;

            _rebindCurrentIds.Add(no.NetworkObjectId);

            var obj = no.GetComponent<SceneObject>();
            if (obj == null) continue;

            obj.ClientBindCarryParent(player, socketIndex: i);
        }

        // Unbind anything that we previously held but no longer do
        foreach (ulong oldId in clientPreviouslyHeldIds)
        {
            if (_rebindCurrentIds.Contains(oldId)) continue;

            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(oldId, out NetworkObject oldNo) &&
                oldNo != null)
            {
                var so = oldNo.GetComponent<SceneObject>();
                if (so != null)
                    so.ClientUnbindCarryParent();
            }
        }

        clientPreviouslyHeldIds.Clear();
        foreach (var id in _rebindCurrentIds)
            clientPreviouslyHeldIds.Add(id);
    }

    public bool ServerTryRemoveAt(int index, out SceneObject removed)
    {
        removed = null;

        if (!IsServer) return false;
        if (index < 0 || index >= heldStack.Count) return false;

        if (!heldStack[index].TryGet(out NetworkObject no) || no == null)
        {
            // TODO 198: Bad reference, just remove it and resnap
            heldStack.RemoveAt(index);
            ServerResnapAll();
            return false;
        }

        removed = no.GetComponent<SceneObject>();

        // Remove from replicated list first (clients will rebind)
        heldStack.RemoveAt(index);

        // Detach from player root
        no.TrySetParent((NetworkObject)null, worldPositionStays: true);

        if (removed != null)
            removed.ServerSetHeldState(false);

        // Re-stack remaining items (their indices changed)
        ServerResnapAll();

        return removed != null;
    }

    public int ServerDeliverOnlyRequiredToTable(Table_StatePattern table)
    {
        if (!IsServer || table == null) return 0;

        int delivered = 0;

        // We keep trying to deliver while we find at least one match per pass.
        bool deliveredSomething;
        do
        {
            deliveredSomething = false;

            // Scan from top -> bottom (LIFO preference)
            for (int i = heldStack.Count - 1; i >= 0; i--)
            {
                if (!TryGetHeldAt(i, out SceneObject obj) || obj == null)
                    continue;

                // Table currently expects "final objects" for delivery.
                if (!obj.IsFinalObject())
                    continue;

                SceneObjectSO so = obj.GetSceneObjectSO();
                if (so == null)
                    continue;

                // Is this object required right now?
                int reqIdx = table.RemainingRequired.FindIndex(x => x == so);
                if (reqIdx < 0)
                    continue;

                // Remove from player stack (re-stacks sockets)
                if (!ServerTryRemoveAt(i, out SceneObject removed) || removed == null)
                    return delivered; // something went weird; return what we managed

                // Consume the object
                removed.DeleteObjectServer();

                delivered++;
                deliveredSomething = true;

                // Update table state
                table.ThrownObjects.Add(so);
                table.RemainingRequired.RemoveAt(reqIdx);

                // Keep net sync consistent
                var net = table.GetComponent<TableNetSync>();
                if (net != null)
                    net.ServerRemoveOneRequired(so);

                // Break to restart scan because indices/requirements changed
                break;
            }
        }
        while (deliveredSomething);

        if (delivered > 0 && table.RemainingRequired.Count == 0)
        {
            var sendParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            };

            TutorialAckClientRpc((int)TutorialEventType.AllItemsDelivered, payload: null, sendParams);
        }

        return delivered;
    }

    public bool IsHoldingTPV()
    {
        for (int i = 0; i < heldStack.Count; i++)
        {
            if (!TryGetHeldAt(i, out var obj) || obj == null) continue;

            var so = obj.GetSceneObjectSO();
            if (so != null && so.objectName == "TPV")
                return true;
        }
        return false;
    }

    [ClientRpc]
    private void TutorialAckClientRpc(int eventType, string payload, ClientRpcParams clientRpcParams = default)
    {
        NetRpcStats.Hit(nameof(TutorialAckClientRpc));
        // Runs on clients. We only send it to the owner, but wwe keep it safe anyway.
        if (!IsOwner) return;
        if (tutorialEvents == null) return;

        tutorialEvents.Raise((TutorialEventType)eventType, payload);
    }

    private void RefreshCarryAnimation()
    {
        if (playerAnimator == null) return;
        playerAnimator.SetBool(carryBoolHash, HasAny);
    }


}