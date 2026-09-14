using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyListSingleUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameTxT;

    [Header("Optional extra labels")]
    [SerializeField] private TextMeshProUGUI modeTxt;
    [SerializeField] private TextMeshProUGUI mapTxt;
    [SerializeField] private TextMeshProUGUI playersTxt;

    [Header("Players Visuals")]
    [SerializeField] private Slider playersFillSlider; // 0..1
    [SerializeField] private Color notFullPlayersColor = new Color32(0x55, 0x71, 0x90, 0xFF); // #557190
    [SerializeField] private Color fullPlayersColor = new Color32(0x27, 0xDC, 0x95, 0xFF);    // #27DC95

    private const string KEY_SELECTED_MAP = "selectedMap";
    private const string KEY_GAME_MODE = "gameMode";

    private Lobby lobby;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
        {
            if (lobby == null) return;
            _ = GameLobby.Instance.JoinWithId(lobby.Id);
        });

        if (playersFillSlider != null)
        {
            playersFillSlider.minValue = 0f;
            playersFillSlider.maxValue = 1f;
            playersFillSlider.wholeNumbers = false;
        }
    }

    public void SetLobby(Lobby lobby)
    {
        this.lobby = lobby;

        if (lobbyNameTxT != null)
            lobbyNameTxT.text = lobby.Name;

        string mode = "multiplayer";
        string map = "Map_01";

        if (lobby.Data != null)
        {
            if (lobby.Data.TryGetValue(KEY_GAME_MODE, out var modeData) && !string.IsNullOrWhiteSpace(modeData?.Value))
                mode = modeData.Value;

            if (lobby.Data.TryGetValue(KEY_SELECTED_MAP, out var mapData) && !string.IsNullOrWhiteSpace(mapData?.Value))
                map = mapData.Value;
        }

        bool isCoop = string.Equals(mode, "coop_campaign", System.StringComparison.OrdinalIgnoreCase);
        int maxPlayersForMode = isCoop ? 2 : 4;

        int currentPlayers = lobby.Players != null ? lobby.Players.Count : 0;
        if (currentPlayers > maxPlayersForMode)
            currentPlayers = maxPlayersForMode;

        bool isFull = currentPlayers >= maxPlayersForMode;
        Color suffixColor = isFull ? fullPlayersColor : notFullPlayersColor;

        if (modeTxt != null)
            modeTxt.text = isCoop ? "Mode: Co-op Campaign" : "Mode: Multiplayer";

        if (mapTxt != null)
            mapTxt.text = $"Map: {map}";

        if (playersTxt != null)
        {
            // "Players 2 <color=#557190>/4</color>" when not full
            // "Players 4 <color=#27DC95>/4</color>" when full
            string hex = ColorUtility.ToHtmlStringRGB(suffixColor);
            playersTxt.text = $"Players {currentPlayers} <color=#{hex}>/{maxPlayersForMode}</color>";
        }

        if (playersFillSlider != null)
        {
            float normalized = maxPlayersForMode > 0 ? (float)currentPlayers / maxPlayersForMode : 0f;
            playersFillSlider.value = Mathf.Clamp01(normalized);
        }
    }
}