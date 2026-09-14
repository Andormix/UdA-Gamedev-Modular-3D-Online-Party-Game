using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameMultiplayerManager : NetworkBehaviour
{
    public const int MAX_PLAYER_AMOUNT = 8;
    private const string PLAYER_PREFS_PLAYER_MULTIPLAYER_NAME = "PlayerNameMultiplayer";
    private const string PLAYER_PREFS_PLAYER_MULTIPLAYER_CUSTOMS = "PlayerCustomizationOptions";

    // CRITICO ERIC:  initialize at declaration so NGO always sees non-null
    private NetworkList<PlayerData> playerDataNetworkList = new NetworkList<PlayerData>();

    private string playerName;
    public static bool playMultiplayer;

    // Suppresses expected callbacks while we intentionally close a session
    private bool _isShuttingDown;

    // Server-side stable team assignment: set once at join time, never recomputed.
    private readonly Dictionary<ulong, MatchTeam> _clientTeamMap = new Dictionary<ulong, MatchTeam>();

    private Dictionary<ulong, CustomizationData> _pendingCustomization = new();

    public event EventHandler OnTryingToJoinGame;
    public event EventHandler OnFailToJoinGame;
    public event EventHandler OnPlayerDataNetworkListChanged;

    public static GameMultiplayerManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // ensure exactly one Twitch sub
        playerDataNetworkList.OnListChanged -= PlayerDataNetworkList_OnListChanged;
        playerDataNetworkList.OnListChanged += PlayerDataNetworkList_OnListChanged;

        playerName = PlayerPrefs.GetString(
            PLAYER_PREFS_PLAYER_MULTIPLAYER_NAME,
            "PlayerName" + UnityEngine.Random.Range(100000, 1000000));
    }

    private void OnApplicationQuit()
    {
        _isShuttingDown = true;
    }

    public void StartHost()
    {
        _isShuttingDown = false;
        _pendingCustomization.Clear();
        _clientTeamMap.Clear();

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[GameMultiplayerManager] StartHost failed: NetworkManager.Singleton is null.");
            return;
        }

        nm.ConnectionApprovalCallback -= NetworkManager_ConnectionApprovalCallback;
        nm.ConnectionApprovalCallback += NetworkManager_ConnectionApprovalCallback;

        nm.OnClientDisconnectCallback -= NetworkManager_Server_OnClientDisconnectCallback;
        nm.OnClientDisconnectCallback += NetworkManager_Server_OnClientDisconnectCallback;

        nm.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;
        nm.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;

        DisableMenuInSceneNetworkObjectsBeforeStartup();

        string json = PlayerPrefs.GetString(PLAYER_PREFS_PLAYER_MULTIPLAYER_CUSTOMS, "");
        _pendingCustomization[NetworkManager.ServerClientId] = JsonToCustomizationData(json);

        bool started = nm.StartHost();
        if (!started)
        {
            Debug.LogError("[GameMultiplayerManager] StartHost failed.");
            return;
        }

        // SESSION RESET (server authority, now safe)
        if (playerDataNetworkList != null)
            playerDataNetworkList.Clear();

        // W e Add host explicitly once so host always appears exactly once
        _pendingCustomization.TryGetValue(NetworkManager.ServerClientId, out var hostCustomization);

        playerDataNetworkList.Add(new PlayerData
        {
            clientId = NetworkManager.ServerClientId,
            customization = hostCustomization
        });

        // Host always gets team Blue (join index 0)
        _clientTeamMap[NetworkManager.ServerClientId] = MatchTeam.Blue;

        _pendingCustomization.Remove(NetworkManager.ServerClientId);

        // we set host metadata on the newly created host row
        SetPlayerNameServerRpc(GetPlayerName());
        SetPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);

        OnPlayerDataNetworkListChanged?.Invoke(this, EventArgs.Empty);
        GraphicsQualityBootstrap.RefreshNetworkTickFromCurrentQuality();
    }
    public bool StartClient()
    {
        _isShuttingDown = false;

        OnTryingToJoinGame?.Invoke(this, EventArgs.Empty);

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[GameMultiplayerManager] StartClient failed: NetworkManager.Singleton is null.");
            OnFailToJoinGame?.Invoke(this, EventArgs.Empty);
            return false;
        }

        nm.OnClientDisconnectCallback -= NetworkManager_Client_OnClientDisconnectCallback;
        nm.OnClientDisconnectCallback += NetworkManager_Client_OnClientDisconnectCallback;

        nm.OnClientConnectedCallback -= NetworkManager_Client_OnClientConnectedCallback;
        nm.OnClientConnectedCallback += NetworkManager_Client_OnClientConnectedCallback;

        DisableMenuInSceneNetworkObjectsBeforeStartup();

        string json = PlayerPrefs.GetString(PLAYER_PREFS_PLAYER_MULTIPLAYER_CUSTOMS, "");
        if (nm.NetworkConfig == null)
        {
            Debug.LogError("[GameMultiplayerManager] StartClient failed: NetworkConfig is null.");
            OnFailToJoinGame?.Invoke(this, EventArgs.Empty);
            return false;
        }

        nm.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(json);

        bool started = nm.StartClient();
        if (!started)
        {
            Debug.LogError("[GameMultiplayerManager] StartClient failed.");
            OnFailToJoinGame?.Invoke(this, EventArgs.Empty);
            return false;
        }

        GraphicsQualityBootstrap.RefreshNetworkTickFromCurrentQuality();
        return true;
    }

    public void MarkSessionShuttingDown()
    {
        _isShuttingDown = true;
    }

    private void NetworkManager_ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        if (SceneManager.GetActiveScene().name != Loader.Scene.CharacterSelectScene.ToString())
        {
            response.Approved = false;
            response.Reason = "The game started and you are not allowed to join";
            return;
        }

        if (NetworkManager.Singleton.ConnectedClientsIds.Count >= MAX_PLAYER_AMOUNT)
        {
            response.Approved = false;
            response.Reason = "The game is already full";
            return;
        }

        response.Approved = true;

        string clientJson = System.Text.Encoding.UTF8.GetString(request.Payload);
        _pendingCustomization[request.ClientNetworkId] = JsonToCustomizationData(clientJson);
        response.CreatePlayerObject = false;
    }

    private void NetworkManager_Server_OnClientDisconnectCallback(ulong clientId)
    {
        if (!IsServer) return;
        if (_isShuttingDown) return;

        // Remove ALL stale entries for this clientId
        for (int i = playerDataNetworkList.Count - 1; i >= 0; i--)
        {
            if (playerDataNetworkList[i].clientId == clientId)
                playerDataNetworkList.RemoveAt(i);
        }

        Debug.Log($"[CharacterSelect] Removed disconnected player clientId={clientId}");
    }

    private void PlayerDataNetworkList_OnListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        if (_isShuttingDown) return;
        OnPlayerDataNetworkListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        if (_isShuttingDown) return;
        if (!IsServer) return;

        // Host already inserted in StartHost()
        if (clientId == NetworkManager.ServerClientId)
            return;

        int existingIndex = GetPlayerDataIndexFromClientId(clientId);
        if (existingIndex >= 0 && existingIndex < playerDataNetworkList.Count)
            playerDataNetworkList.RemoveAt(existingIndex);

        _pendingCustomization.TryGetValue(clientId, out CustomizationData customization);

        // Assign team based on join order at the time of connection.
        // Using the current list count (before add) as the join index so that
        // even if earlier players disconnect later, this client's team never changes.
        if (!_clientTeamMap.ContainsKey(clientId))
        {
            int joinIndex = playerDataNetworkList.Count; // 0 = Blue, 1 = Red, 2 = Blue …
            _clientTeamMap[clientId] = MatchTeamRules.TeamFromJoinIndex(joinIndex);
        }

        playerDataNetworkList.Add(new PlayerData
        {
            clientId = clientId,
            customization = customization,
        });

        _pendingCustomization.Remove(clientId);

        SetPlayerNameServerRpc(GetPlayerName());
        SetPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);
    }
    
    private void NetworkManager_Client_OnClientConnectedCallback(ulong clientId)
    {
        if (_isShuttingDown) return;

        SetPlayerNameServerRpc(GetPlayerName());
        SetPlayerIdServerRpc(AuthenticationService.Instance.PlayerId);
    }

    private void NetworkManager_Client_OnClientDisconnectCallback(ulong clientId)
    {
        if (_isShuttingDown) return;
        OnFailToJoinGame?.Invoke(this, EventArgs.Empty);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerNameServerRpc(string playerName, ServerRpcParams serverRpcParams = default)
    {
        if (_isShuttingDown) return;

        int playerDataIndex = GetPlayerDataIndexFromClientId(serverRpcParams.Receive.SenderClientId);
        if (playerDataIndex == -1) return;

        PlayerData playerData = playerDataNetworkList[playerDataIndex];
        playerData.playerName = playerName;
        playerDataNetworkList[playerDataIndex] = playerData;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerIdServerRpc(string playerId, ServerRpcParams serverRpcParams = default)
    {
        if (_isShuttingDown) return;

        int playerDataIndex = GetPlayerDataIndexFromClientId(serverRpcParams.Receive.SenderClientId);
        if (playerDataIndex == -1) return;

        PlayerData playerData = playerDataNetworkList[playerDataIndex];
        playerData.playerId = playerId;
        playerDataNetworkList[playerDataIndex] = playerData;
    }

    public bool IsPlayerIndexConnected(int playerIndex) => playerIndex < playerDataNetworkList.Count;
    public PlayerData GetPlayerDataFromPlayerIndex(int playerIndex) => playerDataNetworkList[playerIndex];

    public void kickPlayer(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || !nm.IsListening) return;
        nm.DisconnectClient(clientId);
    }

    public string GetPlayerName() => playerName;

    public void SetPlayerName(string name)
    {
        playerName = name;
        PlayerPrefs.SetString(PLAYER_PREFS_PLAYER_MULTIPLAYER_NAME, playerName);
    }

    public PlayerData GetPlayerDataFromClientId(ulong clientId)
    {
        foreach (PlayerData playerData in playerDataNetworkList)
        {
            if (playerData.clientId == clientId) return playerData;
        }
        return default;
    }

    public PlayerData GetPlayerData()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return default;
        return GetPlayerDataFromClientId(nm.LocalClientId);
    }

    public int GetPlayerDataIndexFromClientId(ulong clientId)
    {
        for (int i = 0; i < playerDataNetworkList.Count; i++)
        {
            if (playerDataNetworkList[i].clientId == clientId) return i;
        }
        return -1;
    }

    public MatchTeam GetClientTeam(ulong clientId)
    {
        // Server-authoritative path: use stable map
        if (IsServer && _clientTeamMap.TryGetValue(clientId, out MatchTeam team))
            return team;

        // Client fallback: derive from list position (display only, may drift on disconnect)
        int idx = GetPlayerDataIndexFromClientId(clientId);
        return idx >= 0 ? MatchTeamRules.TeamFromJoinIndex(idx) : MatchTeam.Blue;
    }

    private static CustomizationData JsonToCustomizationData(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;

        var saveObject = JsonUtility.FromJson<PlayerCharacterCustomized.SaveObject>(json);
        if (saveObject?.bodyPartTypeIndexList == null) return default;

        var data = new CustomizationData();
        foreach (var entry in saveObject.bodyPartTypeIndexList)
        {
            byte mesh = (byte)entry.index;
            byte color = (byte)entry.colorIndex;
            switch (entry.bodyPartType)
            {
                case PlayerCharacterCustomized.BodyPartType.Hair: data.hairMesh = mesh; data.hairColor = color; break;
                case PlayerCharacterCustomized.BodyPartType.HairAcc: data.hairAccMesh = mesh; data.hairAccColor = color; break;
                case PlayerCharacterCustomized.BodyPartType.Beard: data.beardMesh = mesh; data.beardColor = color; break;
                case PlayerCharacterCustomized.BodyPartType.Top: data.topMesh = mesh; data.topColor = color; break;
                case PlayerCharacterCustomized.BodyPartType.Legs: data.legsMesh = mesh; data.legsColor = color; break;
            }
        }
        return data;
    }

    public override void OnDestroy()
    {
        _isShuttingDown = true;

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.ConnectionApprovalCallback -= NetworkManager_ConnectionApprovalCallback;
            NetworkManager.Singleton.OnClientDisconnectCallback -= NetworkManager_Server_OnClientDisconnectCallback;
            NetworkManager.Singleton.OnClientConnectedCallback -= NetworkManager_OnClientConnectedCallback;

            NetworkManager.Singleton.OnClientDisconnectCallback -= NetworkManager_Client_OnClientDisconnectCallback;
            NetworkManager.Singleton.OnClientConnectedCallback -= NetworkManager_Client_OnClientConnectedCallback;
        }

        playerDataNetworkList.OnListChanged -= PlayerDataNetworkList_OnListChanged;

        // Do NOT Dispose/Null NetworkList here to avoid NGO destroy/init edge exceptions
        // playerDataNetworkList.Dispose();

        base.OnDestroy();
    }

    public void ResetSessionState()
    {
        _isShuttingDown = false;
        _pendingCustomization.Clear();
        _clientTeamMap.Clear();

        // NetworkList is server-authoritative. Only server can mutate it while network is active.
        bool canWriteNetworkList =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer;

        if (canWriteNetworkList && playerDataNetworkList != null)
        {
            playerDataNetworkList.Clear();
        }

        // Always notify local UI to refresh/clear slots regardless of authority.
        OnPlayerDataNetworkListChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DisableMenuInSceneNetworkObjectsBeforeStartup()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Loader.Scene.MenuScene.ToString())
            return;

        var all = FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all == null || all.Length == 0) return;

        foreach (var no in all)
        {
            if (no == null) continue;
            //if (no.InScenePlacedSourceGlobalObjectIdHash == 0) continue;
            if (no.gameObject.scene.name != Loader.Scene.MenuScene.ToString()) continue;
            // Avoid syncing menu preview objects that are scene instances of network prefabs.
            no.gameObject.SetActive(false);
        }
    }




}
