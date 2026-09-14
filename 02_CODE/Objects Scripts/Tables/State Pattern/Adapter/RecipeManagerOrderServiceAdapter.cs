public class RecipeManagerOrderServiceAdapter : IOrderService
{
    private readonly RecipeManager recipeManager;

    public RecipeManagerOrderServiceAdapter(RecipeManager recipeManager)
    {
        this.recipeManager = recipeManager;
    }

    public void SetOrderStateColor(int orderId, int stateIndex)
    {
        recipeManager.SetOrderStateColor(orderId, stateIndex);
    }

    public RecipeOrder RequestNewOrder() => recipeManager.RequestNewOrder();
    public void CompleteOrder(int orderId) => recipeManager.CompleteOrder(orderId);
    public void RemoveOrder(int orderId) => recipeManager.RemoveOrder(orderId);
    public void SetOrderTimer(int orderId, float remainingTime, float duration) =>
        recipeManager.SetOrderTimer(orderId, remainingTime, duration);
}