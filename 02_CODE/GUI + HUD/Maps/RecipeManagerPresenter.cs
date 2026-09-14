using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public sealed class RecipeManagerPresenter : MonoBehaviour
{
    [SerializeField] private RecipeManager recipeManager;
    [SerializeField] private Transform container;
    [SerializeField] private Transform recipeTemplate;

    [Header("Team Filter")]
    [SerializeField] private bool filterByOwnerTeam = false;
    [SerializeField] private int ownerTeamFilter = 0; // 0 blue, 1 red

    private readonly Dictionary<int, RecipeMangerUniqueUI> rowsByOrderId = new();
    private readonly Dictionary<int, int> ownerTeamByOrderId = new();

    private bool IsServer => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

    private void Awake()
    {
        if (recipeManager == null || container == null || recipeTemplate == null)
        {
            Debug.LogError($"{nameof(RecipeManagerPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }

        recipeTemplate.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (IsServer)
        {
            recipeManager.OnOrderSpawned += OnOrderSpawned;
            recipeManager.OnOrderRemoved += OnOrderRemoved;
            RecipeUiBus.OnTimer += OnNetOrderTimer;
            RecipeUiBus.OnStateColor += OnNetOrderStateColor;
            RecipeUiBus.OnSpawned += OnNetOrderSpawned;
        }
        else
        {
            RecipeUiBus.OnSpawned += OnNetOrderSpawned;
            RecipeUiBus.OnRemoved += OnNetOrderRemoved;
            RecipeUiBus.OnTimer += OnNetOrderTimer;
            RecipeUiBus.OnStateColor += OnNetOrderStateColor;
        }
    }

    private void OnDisable()
    {
        if (IsServer)
        {
            if (recipeManager != null)
            {
                recipeManager.OnOrderSpawned -= OnOrderSpawned;
                recipeManager.OnOrderRemoved -= OnOrderRemoved;
            }

            RecipeUiBus.OnTimer -= OnNetOrderTimer;
            RecipeUiBus.OnStateColor -= OnNetOrderStateColor;
            RecipeUiBus.OnSpawned -= OnNetOrderSpawned;
        }
        else
        {
            RecipeUiBus.OnSpawned -= OnNetOrderSpawned;
            RecipeUiBus.OnRemoved -= OnNetOrderRemoved;
            RecipeUiBus.OnTimer -= OnNetOrderTimer;
            RecipeUiBus.OnStateColor -= OnNetOrderStateColor;
        }
    }

    private void Start()
    {
        if (IsServer) RebuildAllServer();
    }

    private bool PassTeamFilter(int ownerTeamId)
    {
        if (!filterByOwnerTeam) return true;
        return ownerTeamId == ownerTeamFilter;
    }

    private void RebuildAllServer()
    {
        foreach (var kv in rowsByOrderId)
            if (kv.Value != null) Destroy(kv.Value.gameObject);

        rowsByOrderId.Clear();
        ownerTeamByOrderId.Clear();

        foreach (var order in recipeManager.GetScheduledOrders())
        {
            int ownerTeamId = order.ownerTeamId;
            ownerTeamByOrderId[order.id] = ownerTeamId;

            if (!PassTeamFilter(ownerTeamId)) continue;

            UpsertRow(order.recipe, order.id, order.duration, order.endTime, order.tableNetId, order.tableDisplayName);
        }
    }

    private void OnOrderSpawned(object sender, RecipeOrder order)
    {
        ownerTeamByOrderId[order.id] = order.ownerTeamId;

        if (!PassTeamFilter(order.ownerTeamId))
        {
            RemoveRow(order.id);
            return;
        }

        UpsertRow(order.recipe, order.id, order.duration, order.endTime, order.tableNetId, order.tableDisplayName);
    }

    private void OnOrderRemoved(object sender, RecipeOrder order) => RemoveRow(order.id);

    private void OnNetOrderSpawned(OrderUiData data)
    {
        ownerTeamByOrderId[data.orderId] = data.ownerTeamId;

        if (!PassTeamFilter(data.ownerTeamId))
        {
            RemoveRow(data.orderId);
            return;
        }

        RecipeSO recipe = recipeManager.GetRecipeByIndex(data.recipeIndex);
        if (recipe == null)
        {
            Debug.LogWarning($"Recipe index {data.recipeIndex} not found; cannot build UI row.");
            return;
        }

        UpsertRow(recipe, data.orderId, data.duration, data.endTime, data.tableNetId, data.tableDisplayName.ToString());
    }

    private void OnNetOrderTimer(int orderId, float duration, double endTime)
    {
        if (!rowsByOrderId.TryGetValue(orderId, out var row) || row == null) return;
        row.ApplyTimer(duration, endTime);
    }

    private void OnNetOrderStateColor(int orderId, int stateIndex)
    {
        if (!rowsByOrderId.TryGetValue(orderId, out var row) || row == null) return;
        row.SetStateColorIndex(stateIndex);
    }

    private void OnNetOrderRemoved(int orderId) => RemoveRow(orderId);

    private void UpsertRow(RecipeSO recipe, int orderId, float duration, double endTime, ulong tableNetId, string tableDisplayName)
    {
        if (rowsByOrderId.TryGetValue(orderId, out var existing) && existing != null)
        {
            existing.SetOrderFromNet(recipe, orderId, duration, endTime, tableNetId, tableDisplayName);
            return;
        }

        Transform t = Instantiate(recipeTemplate, container);
        t.gameObject.SetActive(true);

        var row = t.GetComponent<RecipeMangerUniqueUI>();
        if (row == null)
        {
            Debug.LogError("Recipe template is missing RecipeMangerUniqueUI component.", t);
            Destroy(t.gameObject);
            return;
        }

        row.SetOrderFromNet(recipe, orderId, duration, endTime, tableNetId, tableDisplayName);
        rowsByOrderId.Add(orderId, row);
    }

    private void RemoveRow(int orderId)
    {
        if (!rowsByOrderId.TryGetValue(orderId, out var row)) return;
        rowsByOrderId.Remove(orderId);
        if (row != null) Destroy(row.gameObject);
    }
}