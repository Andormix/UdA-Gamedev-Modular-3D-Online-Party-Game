using UnityEngine;

public class WorkstationSeatingNPCsState : IWorkstationState
{
    private readonly IWorkstationContext ctx;
    private float timeout;
    private const float MAX_WAIT_SECONDS = 12f;
    private bool started;

    public WorkstationSeatingNPCsState(IWorkstationContext ctx) => this.ctx = ctx;

    public WorkstationPhaseId Id => WorkstationPhaseId.SeatingNPCs;

    public void Enter()
    {
        timeout = MAX_WAIT_SECONDS;
        started = false;

        ctx.IsOrderRequestReady = false;
        ctx.PushProgress(0f);
        ctx.ApplyVisuals(Id);

        var table = ctx as Table_StatePattern;
        if (table == null || table.SeatingController == null)
        {
            table?.SetNpcSeatingCycleActive(false);
            ctx.ChangeState(WorkstationPhaseId.Idle);
            return;
        }

        bool startedOk = table.SeatingController.BeginSeating(OnAllSeatedServer);
        if (!startedOk)
        {
            table.SetNpcSeatingCycleActive(false);
            ctx.ChangeState(WorkstationPhaseId.Idle);   
            return;
        }

        started = true;
    }

    public void Exit() { }

    public void Tick(float dt)
    {
        if (!started) return;

        timeout -= dt;
        float normalized = Mathf.Clamp01(1f - (timeout / MAX_WAIT_SECONDS));
        ctx.PushProgress(normalized);

        if (timeout <= 0f)
        {
            var table = ctx as Table_StatePattern;
            table?.SetNpcSeatingCycleActive(false);
            ctx.ChangeState(WorkstationPhaseId.Idle);   // <- fail closed
        }
    }

    public void Interact(Player player)
    {
        // No interaction during seating phase
    }

    private void OnAllSeatedServer()
    {
        ctx.IsOrderRequestReady = true;
        ctx.PushProgress(1f);
        ctx.ChangeState(WorkstationPhaseId.ReadyToRequest);
    }
}