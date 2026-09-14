using Unity.Netcode;
using UnityEngine;

public class SceneObjectPriceManager : NetworkBehaviour
{
    public static SceneObjectPriceManager Instance { get; private set; }

    [SerializeField] private SceneObjectCatalogSO catalog;

    private NetworkList<int> runtimePrices;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        runtimePrices = new NetworkList<int>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            GeneratePrices();
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    private void GeneratePrices()
    {
        runtimePrices.Clear();

        foreach (var so in catalog.all)
        {
            if (so == null)
            {
                runtimePrices.Add(0);
                continue;
            }

            int delta = Random.Range(so.minRandomDeltaCoins, so.maxRandomDeltaCoins + 1);
            int value = Mathf.Max(0, so.baseCoinValue + delta);
            runtimePrices.Add(value);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[CoinValue] {so.objectName} = {value}");
#endif
        }
    }

    public int GetCoinValue(SceneObjectSO so)
    {
        int idx = catalog.IndexOf(so);
        if (idx < 0 || idx >= runtimePrices.Count) return 0;
        return runtimePrices[idx];
    }

    // Backward compatibility
    public int GetPrice(SceneObjectSO so) => GetCoinValue(so);
}