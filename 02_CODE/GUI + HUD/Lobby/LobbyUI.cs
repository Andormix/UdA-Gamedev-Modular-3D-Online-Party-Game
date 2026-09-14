using System;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [Header("Main Buttons")]
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button createLobbyButton;
    [SerializeField] private Button quickMatchButton;
    [SerializeField] private Button joinWithCodeButton;

    [Header("Tabs")]
    [SerializeField] private Button tabMultiplayerButton;
    [SerializeField] private Button tabCoopButton;
    [SerializeField] private TMP_Text tabMultiplayerLabel;
    [SerializeField] private TMP_Text tabCoopLabel;
    [SerializeField] private Image tabMultiplayerImage;
    [SerializeField] private Image tabCoopImage;
    [SerializeField] private Color selectedTabColor = Color.white; // FFFFFF
    [SerializeField] private Color unselectedTabColor = new Color32(0x96, 0x96, 0x96, 0xFF);

    [Header("Panels / Inputs")]
    [SerializeField] private LobbyCreateUI lobbyCreateUI;
    [SerializeField] private TMP_InputField joinCodeInputField;
    [SerializeField] private TMP_InputField playerNameInputField;

    [Header("Lobby List")]
    [SerializeField] private Transform lobbyContainer;
    [SerializeField] private Transform lobbyTemplate;
    [SerializeField] private TMP_Text emptyStateText;

    private const string KEY_GAME_MODE = "gameMode";

    private enum LobbyTab
    {
        Multiplayer,
        CoopCampaign
    }

    private LobbyTab currentTab = LobbyTab.Multiplayer;
    private List<Lobby> cachedLobbies = new();

    private bool _uiWired;

    private void Awake()
    {
        WireUiOnce();
        if (lobbyTemplate != null) lobbyTemplate.gameObject.SetActive(false);
    }

    private void Start()
    {
        RefreshLocalPlayerNameUi();
        UpdateLobbyList(new List<Lobby>());
        RefreshTabVisuals();
        UpdateQuickJoinAvailability(); 
        UpdateEmptyState(0);
    }

    private void OnEnable()
    {
        if (GameLobby.Instance != null)
        {
            GameLobby.Instance.SetLobbyBrowserActive(true);
            GameLobby.Instance.OnLobbyListChanged -= GameLobby_OnLobbyListChanged; // safety
            GameLobby.Instance.OnLobbyListChanged += GameLobby_OnLobbyListChanged;
        }

        RefreshLocalPlayerNameUi();
        UpdateQuickJoinAvailability(); 
        ApplyFilteredList(); // apply cached immediately on reopen
    }

    private void OnDisable()
    {
        if (GameLobby.Instance != null)
        {
            GameLobby.Instance.SetLobbyBrowserActive(false);
            GameLobby.Instance.OnLobbyListChanged -= GameLobby_OnLobbyListChanged;
        }
    }

    private void OnDestroy()
    {
        if (GameLobby.Instance != null)
            GameLobby.Instance.OnLobbyListChanged -= GameLobby_OnLobbyListChanged;
    }

    private void WireUiOnce()
    {
        if (_uiWired) return;
        _uiWired = true;

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(async () =>
            {
                if (GameLobby.Instance != null)
                    await GameLobby.Instance.LeaveLobby();

                if (UserInterfaceManager.Instance != null)
                    UserInterfaceManager.Instance.ShowOnly(UserInterfaceManager.UIPage.MainMenu);
            });
        }
        else
        {
            Debug.LogWarning("[LobbyUI] Main Menu button reference is missing.");
        }

        if (createLobbyButton != null)
        {
            createLobbyButton.onClick.AddListener(() =>
            {
                if (lobbyCreateUI != null) lobbyCreateUI.Show();
            });
        }
        else
        {
            Debug.LogWarning("[LobbyUI] Create Lobby button reference is missing.");
        }

        if (quickMatchButton != null)
        {
            quickMatchButton.onClick.AddListener(async () =>
            {
                // NEW GUARD: disabled in Co-op tab
                if (currentTab != LobbyTab.Multiplayer) return;
                if (GameLobby.Instance == null) return;
                await GameLobby.Instance.QuickMatchMultiplayer();
            });
        }
        else
        {
            Debug.LogWarning("[LobbyUI] Quick Match button reference is missing.");
        }

        if (joinWithCodeButton != null)
        {
            joinWithCodeButton.onClick.AddListener(async () =>
            {
                if (GameLobby.Instance == null) return;
                await GameLobby.Instance.JoinWithCode(joinCodeInputField != null ? joinCodeInputField.text : "");
            });
        }
        else
        {
            Debug.LogWarning("[LobbyUI] Join With Code button reference is missing.");
        }

        if (tabMultiplayerButton != null)
        {
            tabMultiplayerButton.onClick.AddListener(() =>
            {
                currentTab = LobbyTab.Multiplayer;
                RefreshTabVisuals();
                UpdateQuickJoinAvailability(); 
                ApplyFilteredList();
            });
        }

        if (tabCoopButton != null)
        {
            tabCoopButton.onClick.AddListener(() =>
            {
                currentTab = LobbyTab.CoopCampaign;
                RefreshTabVisuals();
                UpdateQuickJoinAvailability(); 
                ApplyFilteredList();
            });
        }

        if (playerNameInputField != null)
        {
            playerNameInputField.onValueChanged.AddListener((string text) =>
            {
                if (GameMultiplayerManager.Instance != null)
                    GameMultiplayerManager.Instance.SetPlayerName(text);
            });
        }
    }

    private void RefreshLocalPlayerNameUi()
    {
        if (playerNameInputField == null) return;
        if (GameMultiplayerManager.Instance == null) return;

        playerNameInputField.SetTextWithoutNotify(GameMultiplayerManager.Instance.GetPlayerName());
    }

    private void GameLobby_OnLobbyListChanged(object sender, GameLobby.OnLobbyListChangedEventArgs e)
    {
        cachedLobbies = e.lobbyList ?? new List<Lobby>();
        ApplyFilteredList();
    }

    private void ApplyFilteredList()
    {
        List<Lobby> filtered = new();

        foreach (var lobby in cachedLobbies)
        {
            string mode = GetLobbyMode(lobby);
            bool isCoop = string.Equals(mode, "coop_campaign", StringComparison.OrdinalIgnoreCase);

            if (currentTab == LobbyTab.Multiplayer && !isCoop)
                filtered.Add(lobby);

            if (currentTab == LobbyTab.CoopCampaign && isCoop)
                filtered.Add(lobby);
        }

        UpdateLobbyList(filtered);
        UpdateEmptyState(filtered.Count);
    }

    private string GetLobbyMode(Lobby lobby)
    {
        if (lobby?.Data == null) return "multiplayer";
        if (lobby.Data.TryGetValue(KEY_GAME_MODE, out var modeData) && !string.IsNullOrWhiteSpace(modeData?.Value))
            return modeData.Value;
        return "multiplayer";
    }

    private void RefreshTabVisuals()
    {
        bool multiplayerSelected = currentTab == LobbyTab.Multiplayer;
        bool coopSelected = currentTab == LobbyTab.CoopCampaign;

        if (tabMultiplayerImage != null)
            tabMultiplayerImage.color = multiplayerSelected ? selectedTabColor : unselectedTabColor;

        if (tabCoopImage != null)
            tabCoopImage.color = coopSelected ? selectedTabColor : unselectedTabColor;

        if (tabMultiplayerLabel != null)
            tabMultiplayerLabel.text = "Multiplayer";

        if (tabCoopLabel != null)
            tabCoopLabel.text = "Co-op";
    }

    // NEW
    private void UpdateQuickJoinAvailability()
    {
        if (quickMatchButton == null) return;

        bool allow = currentTab == LobbyTab.Multiplayer;
        quickMatchButton.interactable = allow;

        // optional visual dim 
        var cg = quickMatchButton.GetComponent<CanvasGroup>();
        if (cg != null)
            cg.alpha = allow ? 1f : 0.5f;
    }

    private void UpdateEmptyState(int count)
    {
        if (emptyStateText == null) return;

        if (count == 0)
        {
            emptyStateText.gameObject.SetActive(true);
            emptyStateText.text = currentTab == LobbyTab.Multiplayer
                ? "No Multiplayer lobbies found."
                : "No Co-op Campaign lobbies found.";
        }
        else
        {
            emptyStateText.gameObject.SetActive(false);
        }
    }

    private void UpdateLobbyList(List<Lobby> lobbyList)
    {
        if (lobbyContainer == null || lobbyTemplate == null) return;

        foreach (Transform child in lobbyContainer)
        {
            if (child == lobbyTemplate) continue;
            Destroy(child.gameObject);
        }

        foreach (Lobby lobby in lobbyList)
        {
            Transform lobbyTransform = Instantiate(lobbyTemplate, lobbyContainer);
            lobbyTransform.gameObject.SetActive(true);
            var row = lobbyTransform.GetComponent<LobbyListSingleUI>();
            if (row != null) row.SetLobby(lobby);
        }
    }
}