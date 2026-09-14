using Unity.Netcode;
using UnityEngine;

public class ObjectSpawner : InteractableAsset
{
    [Header("Spawned SO")]
    [SerializeField] private SceneObjectSO sceneObjectSO;

    [Header("Stock Mode")]
    [SerializeField] private bool useFiniteStock = false;
    [SerializeField] private int initialStock = 6;
    [SerializeField] private int maxStock = 12;

    [Header("Optional stock net sync")]
    [SerializeField] private ObjectSpawnerStockNetSync stockNetSync;

    [Header("Visual stock slots (optional)")]
    [SerializeField] private Transform[] stockVisualSlots;

    [Header("Optional visual prefabs")]
    [SerializeField] private GameObject stockVisualPrefab;

    [Header("Interaction Access")]
    [SerializeField] private TeamInteractionAccess interactionAccess = TeamInteractionAccess.All;

    [SerializeField] private TutorialEventChannelSO tutorialEvents;

    private GameObject[] visualInstances;

    private void Awake()
    {
        if (stockNetSync == null)
            stockNetSync = GetComponent<ObjectSpawnerStockNetSync>();

        if (stockVisualSlots != null && stockVisualSlots.Length > 0)
            visualInstances = new GameObject[stockVisualSlots.Length];
    }

    private void OnEnable()
    {
        if (stockNetSync != null)
            stockNetSync.OnStockChanged += HandleStockChanged;
    }

    private void OnDisable()
    {
        if (stockNetSync != null)
            stockNetSync.OnStockChanged -= HandleStockChanged;
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && useFiniteStock)
        {
            int clamped = Mathf.Clamp(initialStock, 0, Mathf.Max(0, maxStock));
            if (stockNetSync != null)
                stockNetSync.ServerSetStock(clamped);
        }

        RefreshStockVisuals();
    }

    private void HandleStockChanged(int _) => RefreshStockVisuals();

    public int GetCurrentStock()
    {
        if (!useFiniteStock) return int.MaxValue;
        if (stockNetSync == null) return 0;
        return stockNetSync.Stock;
    }

    public void ServerAddStock(int amount)
    {
        if (!useFiniteStock) return;
        if (amount <= 0) return;
        if (stockNetSync == null || !stockNetSync.IsServer) return;

        int target = Mathf.Clamp(stockNetSync.Stock + amount, 0, Mathf.Max(0, maxStock));
        stockNetSync.ServerSetStock(target);
    }

    public bool ServerTryConsumeStockOne()
    {
        if (!useFiniteStock) return true;
        if (stockNetSync == null) return false;
        return stockNetSync.ServerTryConsumeOne();
    }

    public void ServerGiveTo(Player player)
    {
        if (player == null) return;
        if (!CanPlayerUse(player)) return;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return;
        if (!carry.HasFreeSlot) return;

        if (!ServerTryConsumeStockOne())
            return;

        var spawned = SceneObjectSpawn.SpawnServerUnparented(sceneObjectSO);
        if (spawned == null)
        {
            if (useFiniteStock) ServerAddStock(1);
            return;
        }

        if (!carry.ServerTryPush(spawned))
        {
            spawned.DeleteObjectServer();
            if (useFiniteStock) ServerAddStock(1);
        }
        else
        {
            // Pickup succeeded -> raise tutorial pickup with payload
            if (TutorialRuntimeContext.IsTutorialRun && tutorialEvents != null)
            {
                string itemId = ResolveTutorialItemId(sceneObjectSO, spawned);
                tutorialEvents.Raise(TutorialEventType.PickedUpItem, itemId);
                Debug.Log($"[Tutorial] PickedUpItem raised from spawner '{name}' itemId='{itemId}'");
            }
        }
    }

    public override void Interact(Player player)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            if (TryGetComponent<ObjectSpawnerNetSync>(out var net))
                net.RequestInteract();
            return;
        }

        if (player != null)
            ServerGiveTo(player);
    }

    public override bool CanAccept(SceneObject sceneObject) => false;

    private void RefreshStockVisuals()
    {
        if (stockVisualSlots == null || stockVisualSlots.Length == 0) return;

        int visibleCount = useFiniteStock
            ? Mathf.Clamp(GetCurrentStock(), 0, stockVisualSlots.Length)
            : stockVisualSlots.Length;

        for (int i = 0; i < stockVisualSlots.Length; i++)
        {
            Transform slot = stockVisualSlots[i];
            if (slot == null) continue;

            bool shouldShow = i < visibleCount;

            if (stockVisualPrefab != null)
            {
                if (shouldShow)
                {
                    if (visualInstances[i] == null)
                    {
                        visualInstances[i] = Instantiate(stockVisualPrefab, slot);
                        visualInstances[i].transform.localPosition = Vector3.zero;
                        visualInstances[i].transform.localRotation = Quaternion.identity;
                        visualInstances[i].transform.localScale = Vector3.one;
                    }
                    else if (!visualInstances[i].activeSelf)
                    {
                        visualInstances[i].SetActive(true);
                    }
                }
                else
                {
                    if (visualInstances[i] != null && visualInstances[i].activeSelf)
                        visualInstances[i].SetActive(false);
                }
            }
            else
            {
                // custom slot mode: toggle entire slot root (safe)
                if (slot.gameObject.activeSelf != shouldShow)
                    slot.gameObject.SetActive(shouldShow);
            }
        }
    }

    private bool CanPlayerUse(Player player)
    {
        if (player == null) return false;
        return TeamInteractionRules.CanPlayerInteract(player.OwnerClientId, interactionAccess);
    }

    private string ResolveTutorialItemId(SceneObjectSO so, SceneObject spawned)
    {
        if (so != null && !string.IsNullOrWhiteSpace(so.tutorialId))
            return so.tutorialId;

        if (so != null && !string.IsNullOrWhiteSpace(so.objectName))
            return so.objectName;

        if (spawned != null && spawned.TryGetComponent<SceneObject>(out _))
        {
            string n = spawned.name.Replace("(Clone)", "").Trim();
            return n;
        }

        return "UnknownItem";
    }
}