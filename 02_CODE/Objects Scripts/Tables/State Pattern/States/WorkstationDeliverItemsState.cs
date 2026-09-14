using UnityEngine;
using Unity.Netcode;

public class WorkstationDeliverItemsState : IWorkstationState
{
    private readonly IWorkstationContext ctx;
    private float remaining;

    public WorkstationDeliverItemsState(IWorkstationContext ctx) => this.ctx = ctx;
    public WorkstationPhaseId Id => WorkstationPhaseId.DeliverItems;

    public void Enter()
    {
        remaining = ctx.DeliverItemsSeconds;
        ctx.PushProgress(1f);
        ctx.ApplyVisuals(Id);

        if (ctx.ActiveOrder != null)
        {
            ctx.Orders.SetOrderTimer(ctx.ActiveOrder.id, remaining, ctx.DeliverItemsSeconds);
            ctx.Orders.SetOrderStateColor(ctx.ActiveOrder.id, 1);
        }
    }

    public void Exit() { }

    public void Tick(float dt)
    {
        if (ctx.ActiveOrder == null)
        {
            ctx.ResetWorkstation();
            return;
        }

        remaining -= dt;
        ctx.PushProgress(Mathf.Clamp01(remaining / ctx.DeliverItemsSeconds));

        if (remaining <= 0f)
        {
            var table = ctx as Table_StatePattern;
            if (table != null && TutorialRuntimeContext.IsTutorialRun && table.GetTutorialEvents() != null)
                table.GetTutorialEvents().Raise(TutorialEventType.DeliverItemsTimeout, table.GetTutorialTableId());

            if (TutorialGameplayPolicy.DisableDeliverFailure)
            {
                remaining = 0f;
                ctx.PushProgress(0f);
                return;
            }

            ScoreManager.Instance.RegisterClientLost();
            ResetOwnerTeamComboOnFail();

            int failedOrderId = ctx.ActiveOrder != null ? ctx.ActiveOrder.id : -1;
            if (failedOrderId >= 0) ctx.Orders.RemoveOrder(failedOrderId);

            if (table != null) table.EnterNeedsCleanupAfterNpcLeaveServer();
            else ctx.ResetWorkstation();
        }
    }

    public void Interact(Player player)
    {
        if (ctx.ActiveOrder == null) return;

        var table = ctx as Table_StatePattern;
        if (table == null) return;
        if (player != null && !table.CanPlayerInteractByTeam(player.OwnerClientId)) return;

        var carry = player != null ? player.GetComponent<PlayerCarryNet>() : null;
        if (carry == null || !carry.HasAny) return;

        int delivered = carry.ServerDeliverOnlyRequiredToTable(table);
        table.NotifyRequiredChanged();

        if (delivered > 0)
        {
            table.RefreshDeliveredVisualsServer();

            var ev = table.GetTutorialEvents();
            if (TutorialRuntimeContext.IsTutorialRun && ev != null)
                ev.Raise(TutorialEventType.DeliveredRequiredItem, table.GetTutorialTableId());
        }

        if (table.RemainingRequired.Count == 0 && delivered > 0)
        {
            var ev = table.GetTutorialEvents();
            if (TutorialRuntimeContext.IsTutorialRun && ev != null)
                ev.Raise(TutorialEventType.AllItemsDelivered, table.GetTutorialTableId());

            ctx.ChangeState(WorkstationPhaseId.Consuming);
        }
    }

    private void ResetOwnerTeamComboOnFail()
    {
        var table = ctx as Table_StatePattern;
        if (table == null || ScoreManager.Instance == null || NetworkManager.Singleton == null) return;

        bool isCoop = CoopCampaignSessionContext.IsCoopCampaignRun;
        var ids = NetworkManager.Singleton.ConnectedClientsIds;

        for (int i = 0; i < ids.Count; i++)
        {
            ulong clientId = ids[i];
            MatchTeam team = isCoop ? MatchTeam.Blue : TeamInteractionRules.ResolveTeam(clientId);

            if (!GameMultiplayerManager.playMultiplayer || isCoop)
            {
                if (team == MatchTeam.Blue) ScoreManager.Instance.ResetComboForClient(clientId);
            }
            else
            {
                int owner = table.OwnerTeamId;
                if ((int)team == owner) ScoreManager.Instance.ResetComboForClient(clientId);
            }
        }
    }
}