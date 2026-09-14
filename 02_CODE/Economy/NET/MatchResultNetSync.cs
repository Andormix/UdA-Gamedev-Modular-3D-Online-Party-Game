using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class MatchResultNetSync : NetworkBehaviour
{
    public static MatchResultNetSync Instance { get; private set; }

    private NetworkList<PlayerMatchResultData> rankingList;
    private NetworkVariable<MatchSummaryData> summary =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private MatchSummaryData localSummaryOverride;
    private bool hasLocalSummaryOverride;

    // anti-double-grant guard on client
    private int lastGrantedMatchId = -1;

    public event Action OnResultsUpdated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rankingList = new NetworkList<PlayerMatchResultData>();
    }

    public override void OnNetworkSpawn()
    {
        rankingList.OnListChanged += RankingList_OnListChanged;
        summary.OnValueChanged += Summary_OnValueChanged;
    }

    public override void OnNetworkDespawn()
    {
        if (rankingList != null) rankingList.OnListChanged -= RankingList_OnListChanged;
        summary.OnValueChanged -= Summary_OnValueChanged;
    }

    private void RankingList_OnListChanged(NetworkListEvent<PlayerMatchResultData> changeEvent)
    {
        OnResultsUpdated?.Invoke();
    }

    private void Summary_OnValueChanged(MatchSummaryData previousValue, MatchSummaryData newValue)
    {
        OnResultsUpdated?.Invoke();
    }

    public IReadOnlyList<PlayerMatchResultData> GetRanking()
    {
        var copy = new List<PlayerMatchResultData>(rankingList.Count);
        for (int i = 0; i < rankingList.Count; i++) copy.Add(rankingList[i]);
        return copy;
    }

    public MatchSummaryData GetSummary()
    {
        return hasLocalSummaryOverride ? localSummaryOverride : summary.Value;
    }

    public void ServerPublishResults(List<PlayerMatchResultData> ranking, MatchSummaryData summaryData)
    {
        if (!IsServer) return;

        rankingList.Clear();
        if (ranking != null)
        {
            foreach (var row in ranking) rankingList.Add(row);
        }

        summary.Value = summaryData;
        OnResultsUpdated?.Invoke();
    }

    [ClientRpc]
    public void ApplyLocalSummaryClientRpc(MatchSummaryData data, ClientRpcParams rpcParams = default)
    {
        hasLocalSummaryOverride = true;
        localSummaryOverride = data;

        bool shouldApplyReward =
            data.applyRewardLocally == 1 &&
            EconomyManager.Instance != null &&
            data.matchId != lastGrantedMatchId; // guard

        if (shouldApplyReward)
        {
            EconomyGameMode mode = (EconomyGameMode)data.mode;

            bool isMultiplayerWinForLocalPlayer = false;
            float coinsMultiplier = 1f;

            if (mode == EconomyGameMode.Multiplayer)
            {
                bool blueWins = data.blueTeamScore > data.redTeamScore;
                bool redWins = data.redTeamScore > data.blueTeamScore;
                bool tie = data.blueTeamScore == data.redTeamScore;

                MatchTeam localTeam = (MatchTeam)data.localTeam;
                isMultiplayerWinForLocalPlayer =
                    (localTeam == MatchTeam.Blue && blueWins) ||
                    (localTeam == MatchTeam.Red && redWins);

                if (tie) coinsMultiplier = 0.80f;
                else if (isMultiplayerWinForLocalPlayer) coinsMultiplier = 1.00f;
                else coinsMultiplier = 0.50f;
            }

            var reward = EconomyManager.Instance.GrantMatchRewards(
                stars: data.localStars,
                mode: mode,
                campaignLevelJustReachedThreeStarsFirstTime: false,
                isMultiplayerWinForLocalPlayer: isMultiplayerWinForLocalPlayer,
                coinsMultiplier: coinsMultiplier
            );

            localSummaryOverride.localCoinsEarned = reward.coinsEarned;
            localSummaryOverride.localDiamondsEarned = reward.diamondsEarned;
            localSummaryOverride.localDiamondReason = reward.diamondReason ?? "";
            localSummaryOverride.applyRewardLocally = 0;

            lastGrantedMatchId = data.matchId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MatchResultNetSync] Reward granted for matchId={data.matchId}");
#endif
        }
        else
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (data.applyRewardLocally == 1 && data.matchId == lastGrantedMatchId)
                Debug.Log($"[MatchResultNetSync] Duplicate reward blocked for matchId={data.matchId}");
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[MatchResultNetSync] Local summary override applied. matchId={localSummaryOverride.matchId}, localTeam={(MatchTeam)localSummaryOverride.localTeam}, localScore={localSummaryOverride.localScore}, coins={localSummaryOverride.localCoinsEarned}");
#endif
        OnResultsUpdated?.Invoke();
    }
}