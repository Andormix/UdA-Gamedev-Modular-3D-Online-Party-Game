public class WorkstationIdleState : IWorkstationState
{
    private readonly IWorkstationContext ctx;

    public WorkstationIdleState(IWorkstationContext ctx) => this.ctx = ctx;

    public WorkstationPhaseId Id => WorkstationPhaseId.Idle;

    public void Enter()
    {
        ctx.PushProgress(0f);
        ctx.ApplyVisuals(Id);
    }

    public void Exit() { }

    public void Tick(float dt) { }

    public void Interact(Player player)
    {
        // Intentionally empty.
        // Scheduler should transition to ReadyToRequest when appropriate.
    }
}