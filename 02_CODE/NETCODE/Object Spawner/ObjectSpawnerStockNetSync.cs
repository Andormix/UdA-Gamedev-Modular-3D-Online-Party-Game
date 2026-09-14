using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class ObjectSpawnerStockNetSync : NetworkBehaviour
{
    private readonly NetworkVariable<int> stock =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public int Stock => stock.Value;

    public event System.Action<int> OnStockChanged;

    public override void OnNetworkSpawn()
    {
        stock.OnValueChanged += OnStockValueChanged;
        OnStockChanged?.Invoke(stock.Value);
    }

    public override void OnNetworkDespawn()
    {
        stock.OnValueChanged -= OnStockValueChanged;
    }

    private void OnStockValueChanged(int previousValue, int newValue)
    {
        OnStockChanged?.Invoke(newValue);
    }

    public void ServerSetStock(int value)
    {
        if (!IsServer) return;
        stock.Value = Mathf.Max(0, value);
    }

    public bool ServerTryConsumeOne()
    {
        if (!IsServer) return false;
        if (stock.Value <= 0) return false;
        stock.Value -= 1;
        return true;
    }

    public void ServerAddOne()
    {
        if (!IsServer) return;
        stock.Value += 1;
    }
}