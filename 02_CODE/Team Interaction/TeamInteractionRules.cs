using Unity.Netcode;
using UnityEngine;

public static class TeamInteractionRules
{
    public static bool CanPlayerInteract(ulong clientId, TeamInteractionAccess access)
    {
        if (access == TeamInteractionAccess.All) return true;
        if (access == TeamInteractionAccess.None) return false;

        // Singleplayer or coop => treat all as Blue
        if (!GameMultiplayerManager.playMultiplayer || CoopCampaignSessionContext.IsCoopCampaignRun)
        {
            return access == TeamInteractionAccess.BlueOnly;
        }

        if (NetworkManager.Singleton == null) return false;

        MatchTeam team = ResolveTeam(clientId);
        return access switch
        {
            TeamInteractionAccess.BlueOnly => team == MatchTeam.Blue,
            TeamInteractionAccess.RedOnly  => team == MatchTeam.Red,
            _ => false
        };
    }
    
    public static MatchTeam ResolveTeam(ulong clientId)
    {
        // Prefer the stable assignment stored in GameMultiplayerManager
        if (GameMultiplayerManager.Instance != null)
            return GameMultiplayerManager.Instance.GetClientTeam(clientId);

        // Legacy fallback if manager not yet available
        if (NetworkManager.Singleton == null) return MatchTeam.Blue;
        var ids = NetworkManager.Singleton.ConnectedClientsIds;
        for (int i = 0; i < ids.Count; i++)
            if (ids[i] == clientId) return MatchTeamRules.TeamFromJoinIndex(i);

        return MatchTeam.Blue;
    }
}