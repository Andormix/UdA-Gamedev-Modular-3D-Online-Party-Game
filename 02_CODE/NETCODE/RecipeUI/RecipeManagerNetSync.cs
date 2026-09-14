using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class RecipeManagerNetSync : NetworkBehaviour
{
    [SerializeField] private RecipeManager recipeManager;

    private void Awake()
    {
        if (recipeManager == null)
            recipeManager = GetComponent<RecipeManager>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer || recipeManager == null) return;

        recipeManager.OnOrderSpawned += OnOrderSpawnedServer;
        recipeManager.OnOrderRemoved += OnOrderRemovedServer;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer || recipeManager == null) return;

        recipeManager.OnOrderSpawned -= OnOrderSpawnedServer;
        recipeManager.OnOrderRemoved -= OnOrderRemovedServer;
    }

    private void OnOrderSpawnedServer(object sender, RecipeOrder order)
    {
        OrderSpawnedClientRpc(BuildUiData(order));
    }

    private void OnOrderRemovedServer(object sender, RecipeOrder order)
    {
        OrderRemovedClientRpc(order.id);
    }

    private OrderUiData BuildUiData(RecipeOrder order)
    {
        int recipeIndex = recipeManager.GetRecipeIndex(order.recipe);

        return new OrderUiData
        {
            orderId = order.id,
            recipeIndex = recipeIndex,
            duration = order.duration,
            endTime = order.endTime,
            assignedToWorkstation = order.assignedToWorkstation,
            tableNetId = order.tableNetId,
            tableDisplayName = string.IsNullOrWhiteSpace(order.tableDisplayName) ? "Table" : order.tableDisplayName,
            ownerTeamId = order.ownerTeamId
        };
    }

    [ClientRpc]
    private void OrderSpawnedClientRpc(OrderUiData data)
    {
        RecipeUiBus.RaiseSpawned(data);
    }

    [ClientRpc]
    private void OrderRemovedClientRpc(int orderId)
    {
        RecipeUiBus.RaiseRemoved(orderId);
    }

    [ClientRpc]
    public void OrderTimerClientRpc(int orderId, float duration, double endTime)
    {
        RecipeUiBus.RaiseTimer(orderId, duration, endTime);
    }

    [ClientRpc]
    public void OrderStateColorClientRpc(int orderId, int stateIndex)
    {
        RecipeUiBus.RaiseStateColor(orderId, stateIndex);
    }

    public void BroadcastOrderStateColor(int orderId, int stateIndex)
    {
        if (!IsServer || !IsSpawned) return;
        OrderStateColorClientRpc(orderId, stateIndex);
    }

    public void BroadcastOrderSnapshot(RecipeOrder order)
    {
        if (!IsServer || !IsSpawned || order == null) return;
        OrderSpawnedClientRpc(BuildUiData(order));
    }
}