using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Lobbies.Models;

public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] private Button readyButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TextMeshProUGUI lobbyNameTxt;
    [SerializeField] private TextMeshProUGUI lobbyCodeTxt;

    private void Awake()
    {
        readyButton.onClick.AddListener(()=> {
            
            CharacterSelectReady.Instance.SetPlayerReady();

        });

        mainMenuButton.onClick.AddListener(async () => {
            if (SessionCoordinator.Instance != null)
            {
                await SessionCoordinator.Instance.LeaveToMainMenuAsync();
                return;
            }
    
            // Fallback 
            await GameLobby.Instance.LeaveLobby();
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
            Loader.Load(Loader.Scene.MenuScene);
        });
    }

    private void Start()
    {
        var lobby = GameLobby.Instance != null ? GameLobby.Instance.GetLobby() : null;

        if (lobby != null)
        {
            lobbyNameTxt.text = "Lobby Name: " + lobby.Name;
            lobbyCodeTxt.text = "Lobby Code: " + lobby.LobbyCode;
        }
        else
        {
            lobbyNameTxt.text = "Lobby Name: -";
            lobbyCodeTxt.text = "Lobby Code: -";
        }
    }
}
