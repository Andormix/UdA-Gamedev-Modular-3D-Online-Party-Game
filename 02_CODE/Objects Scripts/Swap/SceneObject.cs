using Unity.Netcode;
using UnityEngine;

public class SceneObject : MonoBehaviour
{
    [SerializeField] private SceneObjectSO sceneObjectSO;
    private InterfaceSceneObjectParent sceneObjectParent;
    [SerializeField] private bool followSocketWhileHeld = true;

    // if parent is PlayerCarryNet, we need an index into the player's sockets
    private bool followPlayerCarrySocket;
    private int followSocketIndex;
    private PlayerCarryNet _cachedCarryNet;
    private Transform _cachedCarrySocket;

    public SceneObjectSO GetSceneObjectSO() => sceneObjectSO;

    // Called by SceneObjectNet
    public void SetSOFromNet(SceneObjectSO so) => sceneObjectSO = so;

    // --- called by PlayerCarryNet (client-side bind) ---
    public void ClientBindCarryParent(Player player, int socketIndex)
    {
        if (player == null) return;

        sceneObjectParent = player;
        followPlayerCarrySocket = true;
        followSocketIndex = socketIndex;
        _cachedCarryNet = player.GetComponent<PlayerCarryNet>();
        _cachedCarrySocket = _cachedCarryNet != null ? _cachedCarryNet.GetCarrySocket(socketIndex) : null;

        SetNetworkTransformEnabled(false);
        ApplyHeldPhysics();
    }

    // --- server helper to toggle held state consistently ---
    public void ServerSetHeldState(bool held)
    {
        // Server toggles physical/nt state; clients handle visuals through bind calls.
        if (held)
        {
            ApplyHeldPhysics();
            SetNetworkTransformEnabled(false);
        }
        else
        {
            ApplyWorldPhysics();
            SetNetworkTransformEnabled(true);
            followPlayerCarrySocket = false;
        }
    }

    public void SetSceneObjectParent(InterfaceSceneObjectParent newParent)
    {
        if (newParent == null) return;
        bool parentIsPlayer = newParent is Player;

        if (sceneObjectParent != null)
            sceneObjectParent.ClearSceneObject();

        sceneObjectParent = newParent;

        if (sceneObjectParent.HasSceneObject())
            Debug.Log("Error 01: InterfaceSceneObjectParent already have an object.");

        sceneObjectParent.SetSceneObject(this);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (sceneObjectParent is Player p && p != null)
            {
                p.ServerSetHeld(this);
            }
        }

        var netObj = GetComponent<NetworkObject>();

        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (newParent is Player player && player != null && player.NetworkObject != null)
            {
                netObj.TrySetParent(player.NetworkObject, worldPositionStays: false);

                Transform socket = player.GetSceneObjectSpawnReference();
                if (socket != null)
                {
                    Transform root = player.transform;

                    Vector3 localPos = root.InverseTransformPoint(socket.position);
                    Quaternion localRot = Quaternion.Inverse(root.rotation) * socket.rotation;

                    transform.localPosition = localPos;
                    transform.localRotation = localRot;

                    var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
                    if (nt != null)
                        nt.Teleport(transform.position, transform.rotation, transform.localScale);
                }
                else
                {
                    transform.localPosition = Vector3.zero;
                    transform.localRotation = Quaternion.identity;
                }
            }
            else if (newParent is NetworkBehaviour nb && nb != null && nb.NetworkObject != null)
            {
                netObj.TrySetParent(nb.NetworkObject, worldPositionStays: false);
            }
            else
            {
                netObj.TrySetParent((NetworkObject)null);
            }
        }
        else
        {
            Transform parentRef = sceneObjectParent.GetSceneObjectSpawnReference();
            if (parentRef != null)
                transform.SetParent(parentRef, worldPositionStays: false);
        }

        ApplyHeldPhysics();

        if (parentIsPlayer)
        {
            SetNetworkTransformEnabled(false);
            return;
        }

        Transform anchor = sceneObjectParent.GetSceneObjectSpawnReference();
        SetNetworkTransformEnabled(true);

        if (anchor != null)
        {
            transform.SetPositionAndRotation(anchor.position, anchor.rotation);

            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt != null && nt.enabled)
                nt.Teleport(transform.position, transform.rotation, transform.localScale);
        }
    }

    public InterfaceSceneObjectParent GetSceneObjectParent() => sceneObjectParent;
    public bool IsFinalObject() => sceneObjectSO.finalComponent;

    public void ClientUnbindCarryParent()
    {
        // Stop following player socket locally
        followPlayerCarrySocket = false;
        followSocketIndex = 0;
        _cachedCarryNet = null;
        _cachedCarrySocket = null;

        // We are now a world object locally
        sceneObjectParent = null;

        // Re-enable transform smoothing for non-authority peers
        SetNetworkTransformEnabled(true);
    }

    public void DeleteObject()
    {
        if (sceneObjectParent != null)
            sceneObjectParent.ClearSceneObject();

        Destroy(gameObject);
    }

    public void DeleteObjectServer()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // clear logical parent on server before despawn
            if (sceneObjectParent != null)
                sceneObjectParent.ClearSceneObject();

            netObj.Despawn(true);
            return;
        }

        DeleteObject();
    }

    public bool CanBePickedUp()
    {
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned)
        {
            return netObj.transform.parent == null;
        }

        return transform.parent == null && gameObject.activeSelf;
    }

    public void ClearSceneObjectParent()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            if (sceneObjectParent is Player p && p != null)
            {
                p.ServerSetHeld(null);
            }
        }

        if (sceneObjectParent != null)
        {
            sceneObjectParent.ClearSceneObject();
            sceneObjectParent = null;
        }

        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            netObj.TrySetParent((NetworkObject)null, worldPositionStays: true);
        }
        else
        {
            transform.SetParent(null, worldPositionStays: true);
        }

        followPlayerCarrySocket = false;
        followSocketIndex = 0;
        _cachedCarryNet = null;
        _cachedCarrySocket = null;

        ApplyWorldPhysics();
        SetNetworkTransformEnabled(true);
    }

    private void ApplyHeldPhysics()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // IMPORTANT ERIC FROM THE FUTURE: zero velocities BEFORE setting kinematic, otherwise Unity warns about setting velocity on kinematic body.
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = true;
            rb.Sleep();
        }

        foreach (var c in GetComponentsInChildren<Collider>(includeInactive: true))
            c.enabled = false;
    }

    private void ApplyWorldPhysics()
    {
        foreach (var c in GetComponentsInChildren<Collider>(includeInactive: true))
            c.enabled = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }
    }

    private void SetNetworkTransformEnabled(bool enabled)
    {
        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null) nt.enabled = enabled;
    }

    private void LateUpdate()
    {
        if (!followSocketWhileHeld) return;
        if (sceneObjectParent == null) return;

        var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null && nt.enabled) return;

        // NEW: if following player carry sockets, resolve correct one
        if (followPlayerCarrySocket)
        {
            // Parent might have been destroyed while this object still exists for a frame
            if (!(sceneObjectParent is Player p) || p == null || p.gameObject == null)
            {
                sceneObjectParent = null;
                followPlayerCarrySocket = false;
                followSocketIndex = 0;
                SetNetworkTransformEnabled(true);
                return;
            }

            if (_cachedCarryNet == null || _cachedCarryNet.gameObject != p.gameObject)
            {
                _cachedCarryNet = p.GetComponent<PlayerCarryNet>();
                _cachedCarrySocket = _cachedCarryNet != null ? _cachedCarryNet.GetCarrySocket(followSocketIndex) : null;
            }

            Transform socket = _cachedCarrySocket;
            if (socket == null) return;

            transform.position = socket.position;
            transform.rotation = socket.rotation;
            return;
        }

        // Legacy: single socket parent
        Transform legacySocket = sceneObjectParent.GetSceneObjectSpawnReference();
        if (legacySocket == null) return;

        transform.position = legacySocket.position;
        transform.rotation = legacySocket.rotation;
    }

    public void ClientBindParent(InterfaceSceneObjectParent newParent)
    {
        sceneObjectParent = newParent;
        followPlayerCarrySocket = false;
        followSocketIndex = 0;
        SetNetworkTransformEnabled(false);
    }

    public void ClientUnbindParent()
    {
        sceneObjectParent = null;
        followPlayerCarrySocket = false;
        followSocketIndex = 0;
        SetNetworkTransformEnabled(true);
    }
}
