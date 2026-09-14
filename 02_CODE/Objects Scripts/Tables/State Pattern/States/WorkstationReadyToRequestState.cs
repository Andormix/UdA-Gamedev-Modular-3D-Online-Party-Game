using UnityEngine;

public class WorkstationReadyToRequestState : IWorkstationState
{
    private readonly IWorkstationContext ctx;
    private float remaining;

    public WorkstationReadyToRequestState(IWorkstationContext ctx) => this.ctx = ctx;

    public WorkstationPhaseId Id => WorkstationPhaseId.ReadyToRequest;

    public void Enter()
    {
        if (!ctx.IsOrderRequestReady)
        {
            ctx.ChangeState(WorkstationPhaseId.Idle);
            return;
        }

        remaining = ctx.ReadyWindowSeconds;
        ctx.PushProgress(1f);
        ctx.ApplyVisuals(Id);
    }

    public void Exit() { }

    public void Tick(float dt)
    {
        if (!ctx.IsOrderRequestReady)
        {
            ctx.ChangeState(WorkstationPhaseId.Idle);
            return;
        }

        remaining -= dt;
        ctx.PushProgress(Mathf.Clamp01(remaining / ctx.ReadyWindowSeconds));

        if (remaining <= 0f)
        {
            if (TutorialGameplayPolicy.DisableReadyWindowFailure)
            {
                // Tutorial: no fail on expiry, wait for player to accept order.
                remaining = 0f;
                ctx.PushProgress(0f);
                //Debug.Log("[TutorialPolicy] DisableReadyWindowFailure ...");
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[CLIENT LOST] ReadyToRequest timeout");
#endif
            ScoreManager.Instance.RegisterClientLost();
            ctx.IsOrderRequestReady = false;
            ctx.PushProgress(0f);
            ctx.ApplyVisuals(WorkstationPhaseId.Idle);
            ctx.ChangeState(WorkstationPhaseId.Idle);
        }
    }

    public void Interact(Player player)
    {
        if (!ctx.IsOrderRequestReady) return;
        if (ctx.HasActiveCommand || ctx.ActiveOrder != null) return;

        ctx.IsOrderRequestReady = false;
        ctx.PushProgress(0f);

        RecipeOrder order = ctx.Orders.RequestNewOrder();
        if (order == null)
        {
            ctx.ChangeState(WorkstationPhaseId.Idle);
            return;
        }

        var table = ctx as Table_StatePattern;
        var scoreCfg = table != null ? table.ScoringConfig : null;

        float elapsedRatio = 1f - (remaining / ctx.ReadyWindowSeconds);
        if (elapsedRatio <= 0.2f)
        {
            int fastPts = scoreCfg != null ? scoreCfg.fastAcceptPoints : 50;
            ScoreManager.Instance.AddScoreForClient(player.OwnerClientId, fastPts);
            var net = table.GetComponent<TableNetSync>();
            if (net != null) net.BroadcastTablePointsPopup(fastPts, 0);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FAST SERVICE] +{fastPts} | elapsedRatio={elapsedRatio:0.00}");
#endif
        }

        int totalCoins = 0;
        var valueMgr = SceneObjectPriceManager.Instance;
        foreach (var so in order.recipe.sceneObjectSOList)
        {
            if (so == null) continue;
            if (valueMgr != null) totalCoins += valueMgr.GetCoinValue(so);
        }
        ctx.CurrentOrderCoinTotal = totalCoins; // RENAMED

        ctx.ActiveOrder = order;
        ctx.ActiveOrder.assignedToWorkstation = true;
        ctx.HasActiveCommand = true;

        if (table != null && player != null)
            table.AssignOwnerTeamFromPlayer(player.OwnerClientId);

        if (table != null)
        {
            var no = table.GetComponent<Unity.Netcode.NetworkObject>();
            var idComp = table.GetComponent<TableIdentity>();

            ulong tableId = no != null ? no.NetworkObjectId : 0;
            string displayName = idComp != null ? idComp.DisplayName : table.name;

            RecipeManager.Instance?.AssignOrderToTable(ctx.ActiveOrder.id, tableId, displayName);
        }

        ctx.Orders.SetOrderStateColor(ctx.ActiveOrder.id, 0);

        ctx.RemainingRequired.Clear();
        ctx.RemainingRequired.AddRange(order.recipe.sceneObjectSOList);
        ctx.NotifyRequiredChanged();

        if (table != null)
        {
            var net = table.GetComponent<TableNetSync>();
            if (net != null) net.ServerSetRemainingRequired(table.RemainingRequired);
        }

        // Tutorial hook: order accepted
        if (table != null)
        {
            var events = table.GetTutorialEvents();
            if (TutorialRuntimeContext.IsTutorialRun && events != null)
            {
                // Use a stable payload id that matches your step requiredPayloadId
                string payload = table.GetTutorialTableId(); // e.g. "TableA"
                events.Raise(TutorialEventType.OrderAccepted, payload);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[Tutorial] OrderAccepted raised payload='{payload}'");
#endif
            }
        }

        ctx.ChangeState(WorkstationPhaseId.DeliverItems);
    }
}