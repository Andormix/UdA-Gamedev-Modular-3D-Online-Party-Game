public interface IOrderService
{
    RecipeOrder RequestNewOrder();
    void CompleteOrder(int orderId);
    void RemoveOrder(int orderId);
    void SetOrderTimer(int orderId, float remainingTime, float duration);
    void SetOrderStateColor(int orderId, int stateIndex);
}