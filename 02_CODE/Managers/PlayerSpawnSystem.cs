using System.Collections.Generic;
using UnityEngine;


// Server-authoritative player spawn resolver.
public class PlayerSpawnSystem : MonoBehaviour
{
    [Header("Team Spawn Points")]
    [SerializeField] private Transform[] blueTeamSpawnPoints;
    [SerializeField] private Transform[] redTeamSpawnPoints;

    [Header("Fallback")]
    [SerializeField] private Transform fallbackSpawnPoint;

    private readonly Dictionary<MatchTeam, int> _nextIndexByTeam = new();

    public void ResetSpawnUsage()
    {
        _nextIndexByTeam.Clear();
    }

    public bool TryGetNextSpawn(MatchTeam team, out Vector3 position, out Quaternion rotation)
    {
        Transform[] teamSpawns = team == MatchTeam.Red ? redTeamSpawnPoints : blueTeamSpawnPoints;
        Transform point = GetNextValidPoint(teamSpawns, team);

        if (point != null)
        {
            position = point.position;
            rotation = point.rotation;
            return true;
        }

        if (fallbackSpawnPoint != null)
        {
            position = fallbackSpawnPoint.position;
            rotation = fallbackSpawnPoint.rotation;
            return true;
        }

        position = transform.position;
        rotation = transform.rotation;
        return true;
    }

    private Transform GetNextValidPoint(Transform[] points, MatchTeam team)
    {
        if (points == null || points.Length == 0)
            return null;

        int start = _nextIndexByTeam.TryGetValue(team, out int value) ? value : 0;
        int count = points.Length;

        for (int offset = 0; offset < count; offset++)
        {
            int idx = (start + offset) % count;
            Transform candidate = points[idx];
            if (candidate == null) continue;

            _nextIndexByTeam[team] = (idx + 1) % count;
            return candidate;
        }

        return null;
    }
}
