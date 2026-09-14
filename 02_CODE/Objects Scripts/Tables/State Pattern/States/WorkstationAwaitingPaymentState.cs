using UnityEngine;
using Unity.Netcode;

public class WorkstationAwaitingPaymentState : IWorkstationState
{
    private readonly IWorkstationContext ctx;
    private float remaining;

    public WorkstationAwaitingPaymentState(IWorkstationContext ctx) => this.ctx = ctx;
    public WorkstationPhaseId Id => WorkstationPhaseId.AwaitingPayment;

    public void Enter()
    {
        if (ctx.ActiveOrder == null)
        {
            ctx.ResetWorkstation();
            return;
        }

        var table = ctx as Table_StatePattern;
        if (table != null)
        {
            table.PaymentPhaseStartTime = NetworkManager.Singleton.ServerTime.Time;
            table.PaymentAttempted = false;

            var ev = table.GetTutorialEvents();
            if (TutorialRuntimeContext.IsTutorialRun && ev != null)
                ev.Raise(TutorialEventType.PaymentPhaseStarted, table.GetTutorialTableId());
        }

        remaining = ctx.PaymentSeconds;
        ctx.PushProgress(1f);
        ctx.ApplyVisuals(Id);

        ctx.Orders.SetOrderTimer(ctx.ActiveOrder.id, remaining, ctx.PaymentSeconds);
        ctx.Orders.SetOrderStateColor(ctx.ActiveOrder.id, 3);
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
        ctx.PushProgress(Mathf.Clamp01(remaining / ctx.PaymentSeconds));

        if (remaining <= 0f)
        {
            var table = ctx as Table_StatePattern;
            if (table != null && TutorialRuntimeContext.IsTutorialRun && table.GetTutorialEvents() != null)
                table.GetTutorialEvents().Raise(TutorialEventType.OrderFailedTimeout, table.GetTutorialTableId());

            if (TutorialGameplayPolicy.DisablePaymentFailure)
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
        if (ctx.ActiveOrder == null || player == null) return;

        var table = ctx as Table_StatePattern;
        var ev = table != null ? table.GetTutorialEvents() : null;
        string tableId = table != null ? table.GetTutorialTableId() : "";

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.IsHoldingTPV())
        {
            if (TutorialRuntimeContext.IsTutorialRun && ev != null)
                ev.Raise(TutorialEventType.PaymentAttemptFailed, tableId);
            return;
        }

        ctx.Orders.CompleteOrder(ctx.ActiveOrder.id);

        if (TutorialRuntimeContext.IsTutorialRun && ev != null)
            ev.Raise(TutorialEventType.PaymentSucceeded, tableId);

        if (table != null) table.EnterNeedsCleanupAfterNpcLeaveServer();
        else ctx.ChangeState(WorkstationPhaseId.NeedsCleanup);
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