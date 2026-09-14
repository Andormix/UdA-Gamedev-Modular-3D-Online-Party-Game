using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ObjectSpawnerNetSync : NetworkBehaviour
{
    [SerializeField] private ObjectSpawner spawner;

    private void Awake()
    {
        if (spawner == null) spawner = GetComponent<ObjectSpawner>();
    }

    public void RequestInteract()
    {
        if (!IsClient) return;
        RequestInteractServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null)
            return;

        var player = client.PlayerObject.GetComponent<Player>();
        if (player == null) return;

        spawner.ServerGiveTo(player);
    }
}