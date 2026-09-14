using System;

public static class RecipeUiBus
{
    public static event Action<OrderUiData> OnSpawned;
    public static event Action<int> OnRemoved;
    public static event Action<int, float, double> OnTimer; // orderId, duration, endTime

    public static event Action<int, int> OnStateColor;

    public static void RaiseSpawned(OrderUiData data) => OnSpawned?.Invoke(data);
    public static void RaiseRemoved(int orderId) => OnRemoved?.Invoke(orderId);
    public static void RaiseTimer(int orderId, float duration, double endTime) => OnTimer?.Invoke(orderId, duration, endTime);

    public static void RaiseStateColor(int orderId, int stateIndex) => OnStateColor?.Invoke(orderId, stateIndex);
}