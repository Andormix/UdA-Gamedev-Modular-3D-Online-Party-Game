using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class LobbyResponseMessageUI : MonoBehaviour
{

    [SerializeField] private TextMeshProUGUI messageTxT;
    [SerializeField] private Button closeButton;
    private GameLobby _gameLobby;
    private GameMultiplayerManager _gameMultiplayerManager;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Hide);
        }
        else
        {
            Debug.LogWarning("[LobbyResponseMessageUI] Close button is not assigned.");
        }
    }

    private void Start()
    {
        _gameMultiplayerManager = GameMultiplayerManager.Instance;
        _gameLobby = GameLobby.Instance;

        if (_gameMultiplayerManager != null)
        {
            _gameMultiplayerManager.OnFailToJoinGame += GameMultiplayerManager_OnFailToJoinGame;
        }
        else
        {
            Debug.LogWarning("[LobbyResponseMessageUI] GameMultiplayerManager.Instance is null at Start.");
        }

        if (_gameLobby != null)
        {
            _gameLobby.OnCreateLobbyStarted += GameLobby_OnCreateLobbyStarted;
            _gameLobby.OnCreateLobbyFailed += GameLobby_OnCreateLobbyFailed;
            _gameLobby.OnJoinSatarted += GameLobby_OnJoinSatarted;
            _gameLobby.OnQuickJoinFailed += GameLobby_OnQuickJoinFailed;
            _gameLobby.OnJoinFailed += GameLobby_OnJoinFailed;
        }
        else
        {
            Debug.LogWarning("[LobbyResponseMessageUI] GameLobby.Instance is null at Start.");
        }

        Hide();
    }

    private void GameLobby_OnJoinFailed(object sender, EventArgs e)
    {
        ShowMessage("Failed to join Lobby");
    }

    private void GameLobby_OnQuickJoinFailed(object sender, EventArgs e)
    {
        ShowMessage("Could not found a lobby to Quickjoin");
    }

    private void GameLobby_OnJoinSatarted(object sender, EventArgs e)
    {
        ShowMessage("Joining Lobby...");
    }

    private void GameLobby_OnCreateLobbyStarted(object sender, EventArgs e)
    {
        ShowMessage("Creating Lobby...");
    }

    private void GameLobby_OnCreateLobbyFailed(object sender, EventArgs e)
    {
        ShowMessage("Failed to create lobby!");
    }

    private void GameMultiplayerManager_OnFailToJoinGame(object sender, System.EventArgs e)
    {
        Show();

        var nm = NetworkManager.Singleton;
        if (nm == null || string.IsNullOrWhiteSpace(nm.DisconnectReason))
        {
            ShowMessage("Failed to connect");
        }
        else
        {
            ShowMessage(nm.DisconnectReason);
        }
    }

    private  void Show()
    {
        gameObject.SetActive(true);
    }

    private  void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);

        if (_gameMultiplayerManager != null)
            _gameMultiplayerManager.OnFailToJoinGame -= GameMultiplayerManager_OnFailToJoinGame;

        if (_gameLobby != null)
        {
            _gameLobby.OnCreateLobbyStarted -= GameLobby_OnCreateLobbyStarted;
            _gameLobby.OnCreateLobbyFailed -= GameLobby_OnCreateLobbyFailed;
            _gameLobby.OnJoinSatarted -= GameLobby_OnJoinSatarted;
            _gameLobby.OnQuickJoinFailed -= GameLobby_OnQuickJoinFailed;
            _gameLobby.OnJoinFailed -= GameLobby_OnJoinFailed;
        }
    }

    private void ShowMessage(string message)
    {
        Show();
        messageTxT.text = message;
    }

}
