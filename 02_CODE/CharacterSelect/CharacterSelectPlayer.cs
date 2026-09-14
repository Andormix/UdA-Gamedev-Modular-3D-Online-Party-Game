using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectPlayer : MonoBehaviour
{
    [SerializeField] private int playerIndex;
    [SerializeField] private GameObject readyGameobject;
    [SerializeField] private Button kickButton;
    [SerializeField] private TextMeshPro playerNameTxt;
    [SerializeField] private PlayerCharacterCustomized previewCharacter;

    private void Awake()
    {
        if (kickButton != null)
        {
            kickButton.onClick.AddListener(async () =>
            {
                if (GameMultiplayerManager.Instance == null || GameLobby.Instance == null) return;
                if (!GameMultiplayerManager.Instance.IsPlayerIndexConnected(playerIndex)) return;
                if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

                PlayerData playerData = GameMultiplayerManager.Instance.GetPlayerDataFromPlayerIndex(playerIndex);

                // Safety: host can't kick self
                if (playerData.clientId == NetworkManager.Singleton.LocalClientId) return;

                await GameLobby.Instance.KickPlayer(playerData.playerId.ToString());
                GameMultiplayerManager.Instance.kickPlayer(playerData.clientId);
            });
        }
    }

    private void Start()
    {
        if (GameMultiplayerManager.Instance != null)
            GameMultiplayerManager.Instance.OnPlayerDataNetworkListChanged += GameMultiplayerManager_OnPlayerDataNetworkListChanged;

        if (CharacterSelectReady.Instance != null)
            CharacterSelectReady.Instance.OnReadyChanged += CharacterSelectReady_OnReadyChanged;

        UpdatePlayer();
    }

    private void CharacterSelectReady_OnReadyChanged(object sender, EventArgs e) => UpdatePlayer();

    private void GameMultiplayerManager_OnPlayerDataNetworkListChanged(object sender, EventArgs e)
    {
        UpdatePlayer();
        Debug.Log("Player Data NetworkList Changed");
    }

    private void UpdatePlayer()
    {
        if (GameMultiplayerManager.Instance == null || NetworkManager.Singleton == null)
        {
            Hide();
            return;
        }

        if (GameMultiplayerManager.Instance.IsPlayerIndexConnected(playerIndex))
        {
            Show();

            PlayerData playerData = GameMultiplayerManager.Instance.GetPlayerDataFromPlayerIndex(playerIndex);

            if (readyGameobject != null && CharacterSelectReady.Instance != null)
                readyGameobject.SetActive(CharacterSelectReady.Instance.IsPlayerReady(playerData.clientId));

            if (playerNameTxt != null)
                playerNameTxt.text = playerData.playerName.ToString();

            if (previewCharacter != null)
                previewCharacter.ApplyCustomizationData(playerData.customization);

            if (kickButton != null)
            {
                bool localIsHost = NetworkManager.Singleton.IsServer;
                bool isSelfRow = playerData.clientId == NetworkManager.Singleton.LocalClientId;

                bool showKick = localIsHost && !isSelfRow;
                kickButton.gameObject.SetActive(showKick);
                kickButton.interactable = showKick;
            }
        }
        else
        {
            if (readyGameobject != null) readyGameobject.SetActive(false);
            if (playerNameTxt != null) playerNameTxt.text = "";
            if (kickButton != null) kickButton.gameObject.SetActive(false);
            Hide();
        }
    }

    private void Show() => gameObject.SetActive(true);

    private void Hide()
    {
        if (this == null || gameObject == null) return;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (GameMultiplayerManager.Instance != null)
            GameMultiplayerManager.Instance.OnPlayerDataNetworkListChanged -= GameMultiplayerManager_OnPlayerDataNetworkListChanged;

        if (CharacterSelectReady.Instance != null)
            CharacterSelectReady.Instance.OnReadyChanged -= CharacterSelectReady_OnReadyChanged;
    }
}