using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

public class RecipeManager : MonoBehaviour
{
    public static RecipeManager Instance { get; private set; }

    public event EventHandler<RecipeOrder> OnOrderSpawned;
    public event EventHandler<RecipeOrder> OnOrderRemoved;

    [Header("Recipe Source")]
    [SerializeField] private RecipeListSO recipeListSO;

    [Header("Limits / Defaults")]
    [SerializeField] private int scheduledRecipeMax = 3;
    [SerializeField] private float orderDuration = 10f;

    private readonly List<RecipeOrder> scheduledOrders = new();
    private int nextOrderId = 1;

    [Header("Network")]
    [SerializeField] private RecipeManagerNetSync netSync;

    private int sucessfulCraftsQTY;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (netSync == null)
            netSync = GetComponent<RecipeManagerNetSync>();
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            return;

        double now = NetworkManager.Singleton.ServerTime.Time;

        for (int i = scheduledOrders.Count - 1; i >= 0; i--)
        {
            RecipeOrder order = scheduledOrders[i];

            if (order.assignedToWorkstation)
                continue;

            if (now >= order.endTime)
            {
                scheduledOrders.RemoveAt(i);
                OnOrderRemoved?.Invoke(this, order);
            }
        }
    }

    public IReadOnlyList<RecipeOrder> GetScheduledOrders() => scheduledOrders;

    public int GetSuccessfulCraftsQTY() => sucessfulCraftsQTY;

    public RecipeOrder RequestNewOrder()
    {
        if (scheduledOrders.Count >= scheduledRecipeMax)
            return null;

        RecipeSO recipe = recipeListSO.recipeSOList[
            UnityEngine.Random.Range(0, recipeListSO.recipeSOList.Count)
        ];

        double now = NetworkManager.Singleton.ServerTime.Time;
        double endTime = now + orderDuration;

        var order = new RecipeOrder(nextOrderId++, recipe, orderDuration, endTime);

        scheduledOrders.Add(order);
        OnOrderSpawned?.Invoke(this, order);

        return order;
    }

    public void SetOrderTimer(int orderId, float remainingTime, float duration)
    {
        RecipeOrder match = scheduledOrders.Find(o => o.id == orderId);
        if (match == null) return;

        double now = NetworkManager.Singleton.ServerTime.Time;

        match.duration = duration;
        match.endTime = now + Mathf.Clamp(remainingTime, 0f, duration);

        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            netSync != null &&
            netSync.IsSpawned)
        {
            netSync.OrderTimerClientRpc(orderId, match.duration, match.endTime);
        }
    }

    public void CompleteOrder(int orderId)
    {
        RecipeOrder match = scheduledOrders.Find(o => o.id == orderId);
        if (match == null) return;

        scheduledOrders.Remove(match);
        OnOrderRemoved?.Invoke(this, match);

        sucessfulCraftsQTY++;
        Debug.Log("Order sucess");
    }

    public void RemoveOrder(int orderId)
    {
        RecipeOrder match = scheduledOrders.Find(o => o.id == orderId);
        if (match == null) return;

        scheduledOrders.Remove(match);
        OnOrderRemoved?.Invoke(this, match);

        Debug.Log("Order failed");
    }

    public int GetRecipeIndex(RecipeSO recipe)
    {
        if (recipeListSO == null || recipeListSO.recipeSOList == null) return -1;
        return recipeListSO.recipeSOList.IndexOf(recipe);
    }

    public RecipeSO GetRecipeByIndex(int index)
    {
        if (recipeListSO == null || recipeListSO.recipeSOList == null) return null;
        if (index < 0 || index >= recipeListSO.recipeSOList.Count) return null;
        return recipeListSO.recipeSOList[index];
    }

    public void SetOrderStateColor(int orderId, int stateIndex)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
        if (netSync == null || !netSync.IsSpawned) return;

        netSync.BroadcastOrderStateColor(orderId, Mathf.Clamp(stateIndex, 0, 3));
    }

    public void AssignOrderToTable(int orderId, ulong tableNetId, string tableDisplayName)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        RecipeOrder match = scheduledOrders.Find(o => o.id == orderId);
        if (match == null) return;

        match.tableNetId = tableNetId;
        match.tableDisplayName = string.IsNullOrWhiteSpace(tableDisplayName) ? "Table" : tableDisplayName;

        if (netSync != null && netSync.IsSpawned)
            netSync.BroadcastOrderSnapshot(match);
    }

    public void SetOrderOwnerTeam(int orderId, int ownerTeamId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

        RecipeOrder match = scheduledOrders.Find(o => o.id == orderId);
        if (match == null) return;

        match.ownerTeamId = ownerTeamId;

        if (netSync != null && netSync.IsSpawned)
            netSync.BroadcastOrderSnapshot(match);
    }
}