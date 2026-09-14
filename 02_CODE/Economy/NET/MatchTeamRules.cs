using UnityEngine;

public static class MatchTeamRules
{
    // Alternating team assignment - Funciona amb join index:
    // 0 Blue, 1 Red, 2 Blue, 3 Red...
    public static MatchTeam TeamFromJoinIndex(int index)
    {
        return (index % 2 == 0) ? MatchTeam.Blue : MatchTeam.Red;
    }

    public static string TeamLabel(MatchTeam team)
    {
        return team == MatchTeam.Blue ? "Blue Team" : "Red Team";
    }
}