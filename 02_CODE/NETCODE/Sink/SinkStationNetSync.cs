using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class SinkStationNetSync : NetworkBehaviour
{
    [SerializeField] private SinkStation sink;

    private void Awake()
    {
        if (sink == null) sink = GetComponent<SinkStation>();
    }

    public void RequestDepositInteract()
    {
        if (!IsClient) return;
        RequestDepositInteractServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestDepositInteractServerRpc(ServerRpcParams rpcParams = default)
    {
        if (sink == null) return;
        if (NetworkManager.Singleton == null) return;

        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) || client?.PlayerObject == null)
            return;

        var player = client.PlayerObject.GetComponent<Player>();
        if (player == null) return;

        sink.ServerHandleDepositInteract(player);
    }
}