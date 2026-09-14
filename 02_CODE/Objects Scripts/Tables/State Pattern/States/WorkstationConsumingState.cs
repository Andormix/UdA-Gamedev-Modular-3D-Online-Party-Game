using UnityEngine;

public class WorkstationConsumingState : IWorkstationState
{
    private readonly IWorkstationContext ctx;
    private float remaining;

    // Cached player list for the ready-to-pay bonus scan; populated once per Enter().
    // Using an array avoids repeated heap allocation of FindObjectsByType results every tick.
    private Player[] _cachedPlayers;

    public WorkstationConsumingState(IWorkstationContext ctx) => this.ctx = ctx;

    public WorkstationPhaseId Id => WorkstationPhaseId.Consuming;

    public void Enter()
    {
        if (ctx.ActiveOrder == null)
        {
            ctx.ResetWorkstation();
            return;
        }

        remaining = ctx.ConsumeSeconds;

        // Cache the player list once per consuming cycle instead of querying every tick.
        _cachedPlayers = UnityEngine.Object.FindObjectsByType<Player>(FindObjectsSortMode.None);

        ctx.PushProgress(1f);
        ctx.ApplyVisuals(Id);

        ctx.Orders.SetOrderTimer(ctx.ActiveOrder.id, remaining, ctx.ConsumeSeconds);
        ctx.Orders.SetOrderStateColor(ctx.ActiveOrder.id, 2);

        var table = ctx as Table_StatePattern;
        if (table != null)
        {
            table.ClearDeliveredVisualsServer();

            var seating = table.SeatingController;
            if (seating != null && seating.IsSpawned)
                seating.SetAllNpcsConsumingServer(true);
        }    }

    public void Exit()
    {
        _cachedPlayers = null;

        var table = ctx as Table_StatePattern;
        if (table == null) return;

        var seating = table.SeatingController;
        if (seating != null && seating.IsSpawned)
            seating.SetAllNpcsConsumingServer(false);
    }

    public void Tick(float dt)
    {
        if (!ctx.ReadyToPayBonusGiven)
        {
            Table_StatePattern table = ctx as Table_StatePattern;
            if (table != null && _cachedPlayers != null)
            {
                bool isMultiplayer = GameMultiplayerManager.playMultiplayer && !CoopCampaignSessionContext.IsCoopCampaignRun;
                int readyBonus = table.ScoringConfig != null ? table.ScoringConfig.readyToPayBonus : 30;

                foreach (var player in _cachedPlayers)
                {
                    // Guard against players that disconnected after Enter() cached the list.
                    if (player == null) continue;

                    var carry = player.GetComponent<PlayerCarryNet>();
                    if (carry == null || !carry.IsHoldingTPV()) continue;

                    if (isMultiplayer && !table.CanPlayerInteractByTeam(player.OwnerClientId))
                        continue;

                    float dist = Vector3.Distance(player.transform.position, table.transform.position);
                    if (dist <= 2.5f)
                    {
                        ScoreManager.Instance.AddScoreForClient(player.OwnerClientId, readyBonus);
                        var net = table.GetComponent<TableNetSync>();
                        if (net != null) net.BroadcastTablePointsPopup(readyBonus, 0);
                        ctx.ReadyToPayBonusGiven = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[READY TO PAY] +{readyBonus} granted to client={player.OwnerClientId}");
#endif
                        break;
                    }
                }
            }
        }

        if (ctx.ActiveOrder == null)
        {
            ctx.ResetWorkstation();
            return;
        }

        remaining -= dt;
        ctx.PushProgress(Mathf.Clamp01(remaining / ctx.ConsumeSeconds));

        if (remaining <= 0f)
            ctx.ChangeState(WorkstationPhaseId.AwaitingPayment);
    }

    public void Interact(Player player) { }
}