using System;
using TMPro;
using UnityEngine;

public class CharacterSelectPlayersCountUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playersCountTxt;
    [SerializeField] private string slashColorHex = "#6b87a3";

    private void Start()
    {
        if (GameMultiplayerManager.Instance != null)
            GameMultiplayerManager.Instance.OnPlayerDataNetworkListChanged += OnPlayerDataChanged;

        Refresh();
    }

    private void OnDestroy()
    {
        if (GameMultiplayerManager.Instance != null)
            GameMultiplayerManager.Instance.OnPlayerDataNetworkListChanged -= OnPlayerDataChanged;
    }

    private void OnPlayerDataChanged(object sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (playersCountTxt == null || GameMultiplayerManager.Instance == null)
            return;

        int current = 0;
        for (int i = 0; i < GameMultiplayerManager.MAX_PLAYER_AMOUNT; i++)
        {
            if (GameMultiplayerManager.Instance.IsPlayerIndexConnected(i))
                current++;
        }

        bool isCoop = GameLobby.Instance != null && GameLobby.Instance.IsCoopCampaignLobby();
        int max = isCoop ? 2 : 4;

        if (current > max) current = max;

        // style: "0 <#6b87a3>/ 4" hardcoded TODO 65
        playersCountTxt.text = $"{current} <color={slashColorHex}>/ {max}</color>";
    }
}