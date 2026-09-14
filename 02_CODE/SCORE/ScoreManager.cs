using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct PlayerScoreEntry : INetworkSerializable, IEquatable<PlayerScoreEntry>
{
    public ulong clientId;
    public int score;
    public float combo; // per-player combo multiplier (1.0..3.0)

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref score);
        serializer.SerializeValue(ref combo);
    }

    public bool Equals(PlayerScoreEntry other) =>
        clientId == other.clientId && score == other.score && Mathf.Abs(combo - other.combo) < 0.0001f;
}

public class ScoreManager : NetworkBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public event Action<int> OnScoreChanged;
    public event Action<float> OnComboChanged;

    private readonly NetworkVariable<int> score =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Legacy/global combo kept for compatibility with older systems
    private readonly NetworkVariable<float> comboMultiplier =
        new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> clientsLost =
        new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkList<PlayerScoreEntry> playerScores;

    private int perfectPayments;
    private int totalPayments;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        playerScores = new NetworkList<PlayerScoreEntry>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            ResetPerMatchServer();
    }

    public int Score => score.Value;
    public float Combo => comboMultiplier.Value; // legacy
    public int ClientsLost => clientsLost.Value;

    public void AddScore(int points)
    {
        if (!IsServer) return;
        score.Value += points;
        OnScoreChanged?.Invoke(score.Value);
    }

    public void AddScoreForClient(ulong clientId, int points)
    {
        if (!IsServer) return;
        if (points == 0) return;

        score.Value += points;
        OnScoreChanged?.Invoke(score.Value);

        int index = GetPlayerScoreIndex(clientId);
        if (index < 0)
        {
            playerScores.Add(new PlayerScoreEntry { clientId = clientId, score = points, combo = 1f });
        }
        else
        {
            var row = playerScores[index];
            row.score += points;
            playerScores[index] = row;
        }
    }

    public int GetScoreForClient(ulong clientId)
    {
        int index = GetPlayerScoreIndex(clientId);
        return index < 0 ? 0 : playerScores[index].score;
    }

    public float GetComboForClient(ulong clientId)
    {
        int index = GetPlayerScoreIndex(clientId);
        return index < 0 ? 1f : Mathf.Max(1f, playerScores[index].combo);
    }

    public float GetMaxComboAcrossPlayers()
    {
        float max = 1f;
        for (int i = 0; i < playerScores.Count; i++)
            max = Mathf.Max(max, playerScores[i].combo);
        return max;
    }

    public int GetTeamScore(MatchTeam team)
    {
        if (NetworkManager.Singleton == null) return 0;

        int total = 0;
        var ids = NetworkManager.Singleton.ConnectedClientsIds;

        bool isCoop = CoopCampaignSessionContext.IsCoopCampaignRun;
        for (int i = 0; i < ids.Count; i++)
        {
            ulong id = ids[i];
            MatchTeam t = isCoop ? MatchTeam.Blue : TeamInteractionRules.ResolveTeam(id);
            if (t == team)
                total += GetScoreForClient(id);
        }

        return total;
    }

    public int GetTotalPlayersWithScoreRows() => playerScores.Count;
    public PlayerScoreEntry GetPlayerScoreEntryAt(int index) => playerScores[index];

    public void EnsurePlayerRowServer(ulong clientId)
    {
        if (!IsServer) return;
        if (GetPlayerScoreIndex(clientId) >= 0) return;
        playerScores.Add(new PlayerScoreEntry { clientId = clientId, score = 0, combo = 1f });
    }

    public void ResetPerMatchServer()
    {
        if (!IsServer) return;

        score.Value = 0;
        comboMultiplier.Value = 1f;
        clientsLost.Value = 0;

        perfectPayments = 0;
        totalPayments = 0;

        playerScores.Clear();

        if (NetworkManager.Singleton != null)
        {
            foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
                EnsurePlayerRowServer(clientId);
        }

        OnScoreChanged?.Invoke(score.Value);
        OnComboChanged?.Invoke(comboMultiplier.Value);
    }

    private int GetPlayerScoreIndex(ulong clientId)
    {
        for (int i = 0; i < playerScores.Count; i++)
        {
            if (playerScores[i].clientId == clientId) return i;
        }
        return -1;
    }

    // Legacy/global combo (compat)
    public void IncreaseCombo()
    {
        if (!IsServer) return;
        comboMultiplier.Value = ScoreUtils.NextCombo(comboMultiplier.Value);
        OnComboChanged?.Invoke(comboMultiplier.Value);
    }

    public void ResetCombo()
    {
        if (!IsServer) return;
        comboMultiplier.Value = 1f;
        OnComboChanged?.Invoke(comboMultiplier.Value);
    }

    // per-player combo API
    public void IncreaseComboForClient(ulong clientId)
    {
        if (!IsServer) return;

        int idx = GetPlayerScoreIndex(clientId);
        if (idx < 0)
        {
            playerScores.Add(new PlayerScoreEntry { clientId = clientId, score = 0, combo = 1.2f });
            OnComboChanged?.Invoke(1.2f);
            return;
        }

        var row = playerScores[idx];
        row.combo = ScoreUtils.NextCombo(Mathf.Max(1f, row.combo));
        playerScores[idx] = row;
        Debug.Log($"[COMBO UP] client={clientId} combo={row.combo}");

        OnComboChanged?.Invoke(row.combo);
    }

    public void ResetComboForClient(ulong clientId)
    {
        if (!IsServer) return;

        int idx = GetPlayerScoreIndex(clientId);
        if (idx < 0) return;

        var row = playerScores[idx];
        row.combo = 1f;
        playerScores[idx] = row;
        Debug.Log($"[COMBO RESET] client={clientId}");

        OnComboChanged?.Invoke(1f);
    }

    public void RegisterPayment(bool perfect)
    {
        if (!IsServer) return;
        totalPayments++;
        if (perfect) perfectPayments++;
    }

    public float GetAccuracy() =>
        totalPayments == 0 ? 1f : (float)perfectPayments / totalPayments;

    // Legacy API kept (no longer tracked globally)
    public float GetMaxComboDuration() => 0f;

    public void RegisterClientLost()
    {
        if (!IsServer) return;
        clientsLost.Value++;
        //ResetCombo();
    }
}