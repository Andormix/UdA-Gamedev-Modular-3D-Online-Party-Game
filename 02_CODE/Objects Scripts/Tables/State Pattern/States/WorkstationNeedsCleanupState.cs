using UnityEngine;

public class WorkstationNeedsCleanupState : IWorkstationState
{
    private readonly IWorkstationContext ctx;

    public WorkstationNeedsCleanupState(IWorkstationContext ctx) => this.ctx = ctx;

    public WorkstationPhaseId Id => WorkstationPhaseId.NeedsCleanup;

    public void Enter()
    {
        ctx.PushProgress(0f);
        ctx.ApplyVisuals(Id);
    }

    public void Exit() { }

    public void Tick(float dt)
    {
        var table = ctx as Table_StatePattern;
        if (table == null) return;

        // Fail-safe: if nothing remains to clean, free table immediately.
        if (!table.HasDirtyItems())
        {
            table.ResetWorkstation();
        }
    }

    public void Interact(Player player)
    {
        if (player == null) return;

        var table = ctx as Table_StatePattern;
        if (table == null) return;

        if (!table.CanPlayerInteractByTeam(player.OwnerClientId))
            return;

        int moved = table.ServerPickupDirtyItemsToPlayer(player);

        if (moved > 0)
            Debug.Log($"[TableCleanup] Picked dirty x{moved} by client={player.OwnerClientId}");
    }
}