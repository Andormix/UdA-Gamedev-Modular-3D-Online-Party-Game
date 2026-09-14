using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using System.Threading.Tasks;
using Unity.Services.Lobbies.Models;
using System;
using System.Collections.Generic;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class GameLobby : MonoBehaviour
{
    private const string KEY_RELAY_JOIN_CODE = "relayJoinCode";

    private const string KEY_SELECTED_MAP = "selectedMap";
    private const string KEY_GAME_MODE = "gameMode";

    [SerializeField] private MultiplayerMapPoolSO multiplayerMapPool;

    public static GameLobby Instance { get; private set; }

    private Lobby joinedLobby;
    private float listLobbiesTimer = 5f;
    private float listLobbiesTimerMax = 5f;
    private float heartbeatTimer;
    private float heartbeatTimerMax = 15f;
    private bool lobbyBrowserActive;
    private bool _listLobbiesInFlight;

    // RNF: Lazy online init guards
    private bool _servicesReady;
    private bool _servicesInitAttempted;

    // RNF Single-flight guard: prevents concurrent quick-match attempts
    private bool _quickMatchInFlight;

    public event EventHandler OnCreateLobbyStarted;
    public event EventHandler OnCreateLobbyFailed;

    // legacy TODO: 222: REVIW AND DELETE ALL NOT USED CODE
    public event EventHandler OnJoinSatarted;
    // preferred alias
    public event EventHandler OnJoinStarted
    {
        add { OnJoinSatarted += value; }
        remove { OnJoinSatarted -= value; }
    }

    public event EventHandler OnQuickJoinFailed;
    public event EventHandler OnJoinFailed;
    public event EventHandler<OnLobbyListChangedEventArgs> OnLobbyListChanged;

    private List<Lobby> _lastQueriedLobbies = new List<Lobby>();

    public class OnLobbyListChangedEventArgs : EventArgs
    {
        public List<Lobby> lobbyList;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // IMPORTANT: do NOT auto-init online services in Awake (Així offline/singleplayer arrenca clean)
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        _ = HandleLobbyList();
    }

    public void SetLobbyBrowserActive(bool active)
    {
        lobbyBrowserActive = active;
        if (active) listLobbiesTimer = 0.1f;
    }

    private async Task<bool> EnsureOnlineServicesReady()
    {
        if (_servicesReady &&
            AuthenticationService.Instance != null &&
            AuthenticationService.Instance.IsSignedIn)
        {
            return true;
        }

        if (!_servicesInitAttempted)
        {
            _servicesInitAttempted = true;
            await InitializeUnityAuthentication();
        }

        _servicesReady =
            UnityServices.State == ServicesInitializationState.Initialized &&
            AuthenticationService.Instance != null &&
            AuthenticationService.Instance.IsSignedIn;

        return _servicesReady;
    }

    private async Task HandleLobbyList()
    {
        if (!lobbyBrowserActive || joinedLobby != null) return;
        if (_listLobbiesInFlight) return;

        _listLobbiesInFlight = true;

        try
        {
            if (!await EnsureOnlineServicesReady()) return;

            listLobbiesTimer -= Time.deltaTime;
            if (listLobbiesTimer <= 0)
            {
                listLobbiesTimer = listLobbiesTimerMax;
                await ListAvailableLobbies();
            }
        }
        finally
        {
            _listLobbiesInFlight = false;
        }
    }

    private async void HandleLobbyHeartbeat()
    {
        if (!IsLobbyHost()) return;
        if (!TryGetLobbyService("HandleLobbyHeartbeat", out var lobbyService)) return;

        heartbeatTimer -= Time.deltaTime;
        if (heartbeatTimer > 0) return;

        heartbeatTimer = heartbeatTimerMax;

        try
        {
            await lobbyService.SendHeartbeatPingAsync(joinedLobby.Id);
            Debug.Log("Lobby Heartbeat Sent");
        }
        catch (LobbyServiceException e)
        {
            Debug.Log($"Heartbeat failed: {e.Message}");
        }
    }

    private bool IsLobbyHost()
    {
        return joinedLobby != null &&
               AuthenticationService.Instance != null &&
               joinedLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private async Task InitializeUnityAuthentication()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions();
                options.SetProfile(UnityEngine.Random.Range(0, 10000).ToString());
                await UnityServices.InitializeAsync(options);
            }

            var auth = AuthenticationService.Instance;
            if (auth == null)
            {
                Debug.LogWarning("[GameLobby] Authentication service is unavailable after initialization.");
                return;
            }

            if (!auth.IsSignedIn)
                await auth.SignInAnonymouslyAsync();
        }
        catch (Exception e)
        {
            // Downgrade to warning so offline mode is graceful
            Debug.LogWarning($"[GameLobby] Unity Services init/auth unavailable (likely offline): {e.Message}");
        }
    }

    private bool TryGetLobbyService(string context, out ILobbyService lobbyService)
    {
        lobbyService = LobbyService.Instance;
        if (lobbyService != null) return true;

        Debug.LogWarning($"[GameLobby] {context}: LobbyService.Instance is null.");
        return false;
    }

    private bool TryGetRelayService(string context, out IRelayService relayService)
    {
        relayService = RelayService.Instance;
        if (relayService != null) return true;

        Debug.LogWarning($"[GameLobby] {context}: RelayService.Instance is null.");
        return false;
    }

    private UnityTransport TryGetTransportOrFail(string context)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError($"[GameLobby] {context}: NetworkManager.Singleton is null.");
            return null;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError($"[GameLobby] {context}: UnityTransport missing on NetworkManager.");
            return null;
        }

        return transport;
    }

    public async Task CreateLobby(string lobbyName, bool isPrivate)
    {
        await CreateLobbyInternal(lobbyName, isPrivate, Loader.Scene.Map_01, false);
    }

    public async Task CreateLobby(string lobbyName, bool isPrivate, Loader.Scene selectedScene, bool isCoopCampaign)
    {
        await CreateLobbyInternal(lobbyName, isPrivate, selectedScene, isCoopCampaign);
    }

    private async Task CreateLobbyInternal(string lobbyName, bool isPrivate, Loader.Scene selectedScene, bool isCoopCampaign)
    {
        OnCreateLobbyStarted?.Invoke(this, EventArgs.Empty);

        if (!await EnsureOnlineServicesReady())
        {
            OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            if (!TryGetLobbyService("CreateLobby", out var lobbyService))
            {
                OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            string mode = isCoopCampaign ? "coop_campaign" : "multiplayer";
            string selectedMap = selectedScene.ToString();

            joinedLobby = await lobbyService.CreateLobbyAsync(
                lobbyName,
                GameMultiplayerManager.MAX_PLAYER_AMOUNT,
                new CreateLobbyOptions
                {
                    IsPrivate = isPrivate,
                    Data = new Dictionary<string, DataObject>
                    {
                        { KEY_GAME_MODE, new DataObject(DataObject.VisibilityOptions.Public, mode, DataObject.IndexOptions.S1) },
                        { KEY_SELECTED_MAP, new DataObject(DataObject.VisibilityOptions.Public, selectedMap, DataObject.IndexOptions.S2) }
                    }
                });

            heartbeatTimer = heartbeatTimerMax;

            Allocation allocation = await AllocateRelay();
            if (allocation.AllocationIdBytes == null || allocation.AllocationIdBytes.Length == 0)
            {
                OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            string relayJoinCode = await GetRelayJoinCode(allocation);
            if (string.IsNullOrWhiteSpace(relayJoinCode))
            {
                OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            await lobbyService.UpdateLobbyAsync(joinedLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_JOIN_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) },
                    { KEY_GAME_MODE, new DataObject(DataObject.VisibilityOptions.Public, mode) },
                    { KEY_SELECTED_MAP, new DataObject(DataObject.VisibilityOptions.Public, selectedMap) }
                }
            });

            Debug.Log($"[LobbyCreated] name={joinedLobby.Name} id={joinedLobby.Id} mode={mode} map={selectedMap}");

            var transport = TryGetTransportOrFail("CreateLobby");
            if (transport == null)
            {
                OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
                return;
            }
            transport.SetRelayServerData(allocation.ToRelayServerData("dtls"));

            CoopCampaignSessionContext.IsCoopCampaignRun = isCoopCampaign;
            CoopCampaignSessionContext.SelectedLevelId = selectedMap;

            ApplyTutorialContextFromLobbySelection(isCoopCampaign, selectedMap);

            GameMultiplayerManager.Instance.StartHost();
            Loader.LoadNetwork(Loader.Scene.CharacterSelectScene);
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
            OnCreateLobbyFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    [Obsolete("Use QuickMatchMultiplayer instead.")]
    public async Task QuickJoin() => await QuickMatchMultiplayer();

    public Lobby GetLobby() => joinedLobby;

    public async Task JoinWithCode(string lobbyCode)
    {
        OnJoinSatarted?.Invoke(this, EventArgs.Empty);

        if (!await EnsureOnlineServicesReady())
        {
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (string.IsNullOrWhiteSpace(lobbyCode))
        {
            Debug.LogWarning("Lobby code cannot be empty.");
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            if (!TryGetLobbyService("JoinWithCode", out var lobbyService))
            {
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            joinedLobby = await lobbyService.JoinLobbyByCodeAsync(lobbyCode);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);
            if (joinAllocation.AllocationIdBytes == null || joinAllocation.AllocationIdBytes.Length == 0)
            {
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var transport = TryGetTransportOrFail("JoinWithCode");
            if (transport == null)
            {
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }
            transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            CoopCampaignSessionContext.IsCoopCampaignRun = IsCoopCampaignLobby();
            CoopCampaignSessionContext.SelectedLevelId = GetSelectedMapLevelId();

            ApplyTutorialContextFromLobbySelection(
                CoopCampaignSessionContext.IsCoopCampaignRun,
                CoopCampaignSessionContext.SelectedLevelId
            );

            if (GameMultiplayerManager.Instance == null || !GameMultiplayerManager.Instance.StartClient())
            {
                Debug.LogWarning("[GameLobby] JoinWithCode: unable to start multiplayer client, leaving joined lobby.");
                await LeaveLobby();
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JoinWithCode failed: {e.Message}");
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task JoinWithId(string lobbyId)
    {
        OnJoinSatarted?.Invoke(this, EventArgs.Empty);

        if (!await EnsureOnlineServicesReady())
        {
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (string.IsNullOrWhiteSpace(lobbyId))
        {
            Debug.LogWarning("Lobby id cannot be empty.");
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            if (!TryGetLobbyService("JoinWithId", out var lobbyService))
            {
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            joinedLobby = await lobbyService.JoinLobbyByIdAsync(lobbyId);

            string relayJoinCode = joinedLobby.Data[KEY_RELAY_JOIN_CODE].Value;
            JoinAllocation joinAllocation = await JoinRelay(relayJoinCode);
            if (joinAllocation.AllocationIdBytes == null || joinAllocation.AllocationIdBytes.Length == 0)
            {
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            var transport = TryGetTransportOrFail("JoinWithId");
            if (transport == null)
            {
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }
            transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            CoopCampaignSessionContext.IsCoopCampaignRun = IsCoopCampaignLobby();
            CoopCampaignSessionContext.SelectedLevelId = GetSelectedMapLevelId();

            ApplyTutorialContextFromLobbySelection(
                CoopCampaignSessionContext.IsCoopCampaignRun,
                CoopCampaignSessionContext.SelectedLevelId
            );

            if (GameMultiplayerManager.Instance == null || !GameMultiplayerManager.Instance.StartClient())
            {
                Debug.LogWarning("[GameLobby] JoinWithId: unable to start multiplayer client, leaving joined lobby.");
                await LeaveLobby();
                OnJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"JoinWithId failed: {e.Message}");
            OnJoinFailed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task LeaveLobby()
    {
        if (joinedLobby == null)
        {
            CoopCampaignSessionContext.Clear();
            return;
        }

        string leavingLobbyId = joinedLobby.Id;

        // Always clear local state first so we don't retry on repeated calls.
        joinedLobby = null;
        CoopCampaignSessionContext.Clear();

        try
        {
            if (AuthenticationService.Instance == null || !AuthenticationService.Instance.IsSignedIn)
                return; // Already signed out – nothing to remove remotely.

            if (!TryGetLobbyService("LeaveLobby", out var lobbyService))
                return;

            await lobbyService.RemovePlayerAsync(leavingLobbyId, AuthenticationService.Instance.PlayerId);
        }
        catch (LobbyServiceException e)
        {
            // These are all expected during teardown (lobby already gone, player already
            // removed by host migration, race with host deletion, etc.).  Downgrade to
            // warning so the console is not flooded with red errors on every match end.
            Debug.LogWarning($"[Lobby] Leave lobby warning (non-critical, lobby may already be gone): {e.Reason} – {e.Message}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Lobby] Leave lobby warning: {e.Message}");
        }
    }

    public async Task DeleteLobby()
    {
        if (joinedLobby == null) return;

        try
        {
            if (!TryGetLobbyService("DeleteLobby", out var lobbyService))
                return;

            await lobbyService.DeleteLobbyAsync(joinedLobby.Id);
            joinedLobby = null;
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public async Task KickPlayer(string playerId)
    {
        if (!IsLobbyHost()) return;

        try
        {
            if (!TryGetLobbyService("KickPlayer", out var lobbyService))
                return;

            await lobbyService.RemovePlayerAsync(joinedLobby.Id, playerId);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError(e);
        }
    }

    private async Task ListAvailableLobbies()
    {
        try
        {
            if (!TryGetLobbyService("ListAvailableLobbies", out var lobbyService))
            {
                _lastQueriedLobbies = new List<Lobby>();
                OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs { lobbyList = _lastQueriedLobbies });
                return;
            }

            QueryLobbiesOptions opts = new QueryLobbiesOptions
            {
                Count = 50,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };

            QueryResponse queryResponse = await lobbyService.QueryLobbiesAsync(opts);

            _lastQueriedLobbies = queryResponse.Results ?? new List<Lobby>();

            OnLobbyListChanged?.Invoke(this, new OnLobbyListChangedEventArgs
            {
                lobbyList = _lastQueriedLobbies
            });
        }
        catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    public string GetSelectedMapLevelId()
    {
        if (joinedLobby?.Data == null) return Loader.Scene.Map_01.ToString();

        if (joinedLobby.Data.TryGetValue(KEY_SELECTED_MAP, out DataObject mapData))
        {
            string value = mapData?.Value;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }

        return Loader.Scene.Map_01.ToString();
    }

    public Loader.Scene GetSelectedMapSceneOrDefault()
    {
        string levelId = GetSelectedMapLevelId();
        if (Enum.TryParse(levelId, out Loader.Scene scene)) return scene;
        return Loader.Scene.Map_01;
    }

    public bool IsCoopCampaignLobby()
    {
        if (joinedLobby?.Data == null) return false;

        if (joinedLobby.Data.TryGetValue(KEY_GAME_MODE, out DataObject modeData))
            return string.Equals(modeData?.Value, "coop_campaign", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private Loader.Scene GetRandomMultiplayerMapOrDefault()
    {
        if (multiplayerMapPool != null) return multiplayerMapPool.GetRandomMap();
        return Loader.Scene.Map_01;
    }

    public async Task QuickMatchMultiplayer()
    {
        // Single-flight guard: reject concurrent calls (e.g. double-tapping the button)
        if (_quickMatchInFlight)
        {
            Debug.LogWarning("[QuickMatch] Already in flight, ignoring duplicate call.");
            return;
        }

        _quickMatchInFlight = true;
        OnJoinSatarted?.Invoke(this, EventArgs.Empty);

        try
        {
            if (joinedLobby != null)
            {
                Debug.LogWarning("[QuickMatch] Already in a lobby. Leave current lobby before quick matching.");
                return;
            }

            if (!await EnsureOnlineServicesReady())
            {
                OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
                return;
            }

            // ── Step 1: Try cached lobby-list entries first (same source as lobby browser UI) ──
            List<Lobby> candidates = BuildQuickMatchCandidates(_lastQueriedLobbies);
            foreach (Lobby lobby in candidates)
            {
                bool joined = await TryJoinLobbyByIdAsync(lobby.Id);
                if (joined) return;
            }

            // ── Step 2: If cache has no joinable lobby, refresh query and retry ──
            try
            {
                if (!TryGetLobbyService("QuickMatchMultiplayer.QueryLobbies", out var lobbyService))
                {
                    OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                var opts = new QueryLobbiesOptions
                {
                    Count = 25,
                    Filters = new List<QueryFilter>
                    {
                        // Must have at least one free slot
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                        // Prefer indexed multiplayer lobbies when index metadata is available
                        new QueryFilter(QueryFilter.FieldOptions.S1, "multiplayer", QueryFilter.OpOptions.EQ),
                    }
                };

                QueryResponse queryResponse = await lobbyService.QueryLobbiesAsync(opts);
                _lastQueriedLobbies = queryResponse?.Results ?? new List<Lobby>();

                if (_lastQueriedLobbies.Count == 0)
                {
                    opts.Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
                    };
                    queryResponse = await lobbyService.QueryLobbiesAsync(opts);
                    _lastQueriedLobbies = queryResponse?.Results ?? new List<Lobby>();
                }

                candidates = BuildQuickMatchCandidates(_lastQueriedLobbies);
                foreach (Lobby lobby in candidates)
                {
                    bool joined = await TryJoinLobbyByIdAsync(lobby.Id);
                    if (joined) return;
                }
            }
            catch (LobbyServiceException qe)
            {
                Debug.LogWarning($"[QuickMatch] Query failed, will create lobby. Reason: {qe.Message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuickMatch] Query failed with unexpected error, will create lobby. Reason: {e.Message}");
            }

            // ── Step 3: No valid lobby found – create a new one ──
            Debug.LogWarning("[QuickMatch] No suitable lobby found, creating a new one.");
            Loader.Scene randomMap = GetRandomMultiplayerMapOrDefault();
            string autoLobbyName = $"QuickMatch_{UnityEngine.Random.Range(1000, 9999)}";
            await CreateLobby(autoLobbyName, false, randomMap, false);
        }
        finally
        {
            _quickMatchInFlight = false;
        }
    }

    private List<Lobby> BuildQuickMatchCandidates(List<Lobby> source)
    {
        List<Lobby> candidates = new List<Lobby>();
        if (source == null) return candidates;

        foreach (Lobby lobby in source)
        {
            if (lobby == null || string.IsNullOrWhiteSpace(lobby.Id)) continue;
            if (lobby.AvailableSlots <= 0) continue;

            if (lobby.Data == null ||
                !lobby.Data.TryGetValue(KEY_GAME_MODE, out DataObject modeData) ||
                string.IsNullOrWhiteSpace(modeData?.Value))
                continue;
            string mode = modeData.Value;
            if (!string.Equals(mode, "multiplayer", StringComparison.OrdinalIgnoreCase)) continue;
            candidates.Add(lobby);
        }

        return candidates;
    }

    // Attempt to join a specific lobby by ID and connect to relay.
    // Returns true on success (client start initiated).
    // On failure cleans up (leaves the lobby) so the caller can try another.
    private async Task<bool> TryJoinLobbyByIdAsync(string lobbyId)
    {
        Lobby lobby = null;
        try
        {
            if (!TryGetLobbyService("TryJoinLobbyByIdAsync", out var lobbyService))
            {
                OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
                return false;
            }

            lobby = await lobbyService.JoinLobbyByIdAsync(lobbyId);

            if (lobby?.Data == null ||
                !lobby.Data.TryGetValue(KEY_RELAY_JOIN_CODE, out DataObject relayCodeData) ||
                string.IsNullOrWhiteSpace(relayCodeData?.Value))
            {
                Debug.LogWarning($"[QuickMatch] Lobby {lobbyId} has no relay code after join – leaving.");
                await SafeLeaveQuickMatchLobbyAsync(lobby?.Id);
                return false;
            }

            JoinAllocation joinAllocation = await JoinRelay(relayCodeData.Value);
            if (joinAllocation.AllocationIdBytes == null || joinAllocation.AllocationIdBytes.Length == 0)
            {
                Debug.LogWarning($"[QuickMatch] Relay join failed for lobby {lobbyId}.");
                await SafeLeaveQuickMatchLobbyAsync(lobby.Id);
                return false;
            }

            var transport = TryGetTransportOrFail("QuickMatchMultiplayer");
            if (transport == null)
            {
                await SafeLeaveQuickMatchLobbyAsync(lobby.Id);
                OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
                return false;
            }

            transport.SetRelayServerData(joinAllocation.ToRelayServerData("dtls"));

            joinedLobby = lobby;
            CoopCampaignSessionContext.IsCoopCampaignRun = false;
            CoopCampaignSessionContext.SelectedLevelId = GetSelectedMapLevelId();
            TutorialRuntimeContext.Clear();
            if (GameMultiplayerManager.Instance == null || !GameMultiplayerManager.Instance.StartClient())
            {
                Debug.LogWarning($"[QuickMatch] Failed to start client after joining lobby {lobbyId}. Leaving lobby.");
                await SafeLeaveQuickMatchLobbyAsync(lobby.Id);
                joinedLobby = null;
                CoopCampaignSessionContext.Clear();
                OnQuickJoinFailed?.Invoke(this, EventArgs.Empty);
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[QuickMatch] Join lobby {lobbyId} failed: {e.Message}");
            if (lobby != null)
                await SafeLeaveQuickMatchLobbyAsync(lobby.Id);
            return false;
        }
    }

    // Leave a quick-match lobby silently on failure – best-effort, no error spam.
    private async Task SafeLeaveQuickMatchLobbyAsync(string lobbyId)
    {
        if (string.IsNullOrWhiteSpace(lobbyId)) return;
        try
        {
            if (AuthenticationService.Instance != null &&
                AuthenticationService.Instance.IsSignedIn &&
                TryGetLobbyService("SafeLeaveQuickMatchLobbyAsync", out var lobbyService))
            {
                await lobbyService.RemovePlayerAsync(lobbyId, AuthenticationService.Instance.PlayerId);
            }
        }
        catch { /* best effort */ }
    }

    private async Task<JoinAllocation> JoinRelay(string joinCode)
    {
        if (!TryGetRelayService("JoinRelay", out var relayService)) return default;
        try { return await relayService.JoinAllocationAsync(joinCode); }
        catch (RelayServiceException e) { Debug.Log(e); return default; }
    }

    private async Task<string> GetRelayJoinCode(Allocation allocation)
    {
        if (!TryGetRelayService("GetRelayJoinCode", out var relayService)) return default;
        try { return await relayService.GetJoinCodeAsync(allocation.AllocationId); }
        catch (RelayServiceException e) { Debug.Log(e); return default; }
    }

    private async Task<Allocation> AllocateRelay()
    {
        if (!TryGetRelayService("AllocateRelay", out var relayService)) return default;
        try { return await relayService.CreateAllocationAsync(GameMultiplayerManager.MAX_PLAYER_AMOUNT - 1); }
        catch (RelayServiceException e) { Debug.Log(e); return default; }
    }

    private void ApplyTutorialContextFromLobbySelection(bool isCoopCampaign, string selectedMap)
    {
        // New product decision XD: no tutorial in lobby modes (coop)
        TutorialRuntimeContext.Clear();
    }
}
