using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    public event EventHandler OnStateChanged;
    public event EventHandler OnGamePaused;
    public event EventHandler OnGameUnpaused;
    public event EventHandler OnLocalPlayerReadyChanged;

    [Header("Initial State - Debug")]
    [SerializeField] private State initialStateOnLoad = State.Waiting;

    [Header("Durations")]
    [SerializeField] private float waitingDuration = 5f;
    [SerializeField] private float gameOverDuration = 10f;

    [Header("Network Sync Tuning")]
    [Tooltip("Quantize countdown replication to reduce NetworkVariable churn.")]
    [SerializeField] private bool quantizeCountdownTimer = true;
    [Tooltip("Quantization step for countdown timer replication.")]
    [SerializeField] private float countdownTimerReplicationStep = 0.05f;
    [Tooltip("Quantize playing replication to reduce NetworkVariable churn.")]
    [SerializeField] private bool quantizePlayingTimer = true;
    [Tooltip("Quantization step for playing timer replication.")]
    [SerializeField] private float playingTimerReplicationStep = 0.05f;

    [SerializeField] private Transform playerPrefab;
    [SerializeField] private PlayerSpawnSystem playerSpawnSystem;

    [Header("Match Audio Events")]
    [SerializeField] private AudioSource matchAudioSource;
    [SerializeField] private AudioMixerGroup matchSfxMixerGroup;
    [SerializeField] private AudioClip[] countdownFinishedClips;
    [SerializeField] private AudioClip[] matchTimerFinishedClips;
    [SerializeField, Range(0f, 1f)] private float countdownFinishedVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float matchTimerFinishedVolume = 1f;

    public float GetPlayingDuration() => playingDuration;
    public float GetRemainingTimeSeconds() => Mathf.Max(0f, playingTimer.Value);

    public enum State
    {
        MainMenu,
        Waiting,
        Countdown,
        Playing,
        Paused,
        GameOver,
        Returning,
    }

    private NetworkVariable<State> state = new(State.MainMenu);
    private NetworkVariable<float> countdownTimer = new(5f);
    private NetworkVariable<float> playingTimer = new(180f);
    private float countdownTimerRaw;
    private float playingTimerRaw;

    private float playingDuration = 180f;
    private float gameOverTimer;
    private bool isLocalPlayerReady = false;
    private bool isGamePaused;
    public bool allClientsReady = true;
    private Dictionary<ulong, bool> playerReadyDictionary;

    private bool resultBroadcastedForThisMatch;
    private int cachedFinalScore;

    // sequence for unique match result ids (host/server runtime)
    private static int s_serverMatchSequence = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        gameOverTimer = gameOverDuration;
        playerReadyDictionary = new Dictionary<ulong, bool>();
        countdownTimerRaw = countdownTimer.Value;
        playingTimerRaw = playingTimer.Value;
        ResolveMatchAudioSource();
    }

    private void Update()
    {
        if (!IsServer) return;

        switch (state.Value)
        {
            case State.Countdown:
                countdownTimerRaw = Mathf.Max(0f, countdownTimerRaw - Time.deltaTime);
                if (countdownTimerRaw <= 0f)
                {
                    countdownTimer.Value = 0f;
                    state.Value = State.Playing;
                }
                else
                {
                    SetReplicatedTimerValue(
                        countdownTimer,
                        countdownTimerRaw,
                        quantizeCountdownTimer,
                        countdownTimerReplicationStep);
                }
                break;

            case State.Playing:
                if (!TutorialGameplayPolicy.DisableGlobalMatchTimeout)
                {
                    playingTimerRaw = Mathf.Max(0f, playingTimerRaw - Time.deltaTime);
                    if (playingTimerRaw <= 0f)
                    {
                        playingTimer.Value = 0f;
                        if (ScoreManager.Instance != null)
                            cachedFinalScore = ScoreManager.Instance.Score;

                        state.Value = State.GameOver;
                        gameOverTimer = gameOverDuration;
                    }
                    else
                    {
                        SetReplicatedTimerValue(
                            playingTimer,
                            playingTimerRaw,
                            quantizePlayingTimer,
                            playingTimerReplicationStep);
                    }
                }
                else
                {
                    // Tutorial mode: no global timeout end.
                    // BUGFIX 25: WE keep timer stable/full for any UI depending on it.
                    playingTimerRaw = Mathf.Max(playingTimerRaw, playingDuration);
                    float tutorialTimer = Mathf.Max(playingTimer.Value, playingDuration);
                    if (!Mathf.Approximately(playingTimer.Value, tutorialTimer))
                        playingTimer.Value = tutorialTimer;
                }
                break;

            case State.GameOver:
                gameOverTimer -= Time.deltaTime;
                if (gameOverTimer <= 0f)
                {
                    state.Value = State.MainMenu;
                    ReturnToMenuClientRpc();
                }
                break;
        }
    }

    private void SetReplicatedTimerValue(
        NetworkVariable<float> timerVariable,
        float rawValue,
        bool quantized,
        float quantizationStep)
    {
        float clamped = Mathf.Max(0f, rawValue);
        float targetValue = clamped;

        if (quantized)
        {
            float step = Mathf.Max(0.01f, quantizationStep);
            targetValue = Mathf.Floor(clamped / step) * step;
        }

        if (Mathf.Abs(timerVariable.Value - targetValue) >= 0.0001f)
            timerVariable.Value = targetValue;
    }

    public override void OnNetworkSpawn()
    {
        state.OnValueChanged += State_OnValueChanged;

        resultBroadcastedForThisMatch = false;
        cachedFinalScore = 0;

        if (IsServer)
        {
            countdownTimerRaw = countdownTimer.Value;
            playingTimerRaw = playingTimer.Value;
            NetworkManager.Singleton.OnClientDisconnectCallback += NetworkManager_Server_OnClientDisconnectCallback;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= SceneManager_OnLoadEventCompleted;
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += SceneManager_OnLoadEventCompleted;
        }

        if (IsClient)
        {
            SetLocalPlayerReady(true);

            if (CoopCampaignSessionContext.IsCoopCampaignRun)
            {
                bool wasUnlocked = false;
                string levelId = CoopCampaignSessionContext.SelectedLevelId;

                if (!string.IsNullOrWhiteSpace(levelId) && CampaignManager.Instance != null)
                    wasUnlocked = CampaignManager.Instance.IsLevelUnlocked(levelId);

                CoopCampaignSessionContext.LocalWasLevelUnlockedAtMatchStart = wasUnlocked;
                Debug.Log($"[CoopCampaign] Local unlock snapshot for '{levelId}': {wasUnlocked}");
            }
        }
    }

    private void NetworkManager_Server_OnClientDisconnectCallback(ulong clientId)
    {
        if (playerReadyDictionary.ContainsKey(clientId))
            playerReadyDictionary.Remove(clientId);

        CheckAllClientsReady();
    }

    private void SceneManager_OnLoadEventCompleted(string sceneName, LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        bool isGameplayMap = IsGameplayScene(sceneName);

        if (IsServer && isGameplayMap && state.Value == State.MainMenu)
            state.Value = State.Waiting;

        if (IsServer && isGameplayMap && ScoreManager.Instance != null)
            ScoreManager.Instance.ResetPerMatchServer();

        PlayerSpawnSystem spawnSystem = ResolvePlayerSpawnSystem();
        if (IsServer && isGameplayMap)
            spawnSystem?.ResetSpawnUsage();

        CheckAllClientsReady();

        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client) &&
                client.PlayerObject != null)
            {
                Debug.Log($"[Spawn] Skip clientId={clientId}, already has PlayerObject");
                continue;
            }

            Transform playerTransform = Instantiate(playerPrefab);

            MatchTeam spawnTeam = ResolveSpawnTeam(clientId);
            if (spawnSystem != null && spawnSystem.TryGetNextSpawn(spawnTeam, out Vector3 spawnPos, out Quaternion spawnRot))
            {
                playerTransform.SetPositionAndRotation(spawnPos, spawnRot);
            }
            else
            {
                Debug.LogWarning($"[Spawn] No configured spawn point resolved for clientId={clientId} team={spawnTeam}. Using prefab default transform.");
            }

            var no = playerTransform.GetComponent<NetworkObject>();
            no.SpawnAsPlayerObject(clientId, true);

            if (IsServer && ScoreManager.Instance != null)
                ScoreManager.Instance.EnsurePlayerRowServer(clientId);

            var customized = playerTransform.GetComponentInChildren<PlayerCharacterCustomized>(true);

            if (customized != null)
            {
                CustomizationData data = default;
                bool hasData = false;

                if (GameMultiplayerManager.Instance != null)
                {
                    PlayerData pd = GameMultiplayerManager.Instance.GetPlayerDataFromClientId(clientId);
                    if (pd.clientId == clientId)
                    {
                        data = pd.customization;
                        hasData = true;
                    }
                }

                if (!hasData)
                {
                    string json = PlayerPrefs.GetString("PlayerCustomizationOptions", "");
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var saveObject = JsonUtility.FromJson<PlayerCharacterCustomized.SaveObject>(json);
                        if (saveObject?.bodyPartTypeIndexList != null)
                        {
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

                            hasData = true;
                        }
                    }
                }

                customized.ServerSetCustomizationData(data);
            }

            Debug.Log($"[Spawn] Spawned PlayerObject clientId={clientId} netId={no.NetworkObjectId}");
        }
    }

    private static bool IsGameplayScene(string sceneName)
    {
        return sceneName == Loader.Scene.Map_01.ToString()
            || sceneName == Loader.Scene.Map_02.ToString()
            || sceneName == Loader.Scene.Map_03_M.ToString();
    }

    private MatchTeam ResolveSpawnTeam(ulong clientId)
    {
        if (!GameMultiplayerManager.playMultiplayer || CoopCampaignSessionContext.IsCoopCampaignRun)
            return MatchTeam.Blue;

        return TeamInteractionRules.ResolveTeam(clientId);
    }

    private PlayerSpawnSystem ResolvePlayerSpawnSystem()
    {
        if (playerSpawnSystem != null) return playerSpawnSystem;

        playerSpawnSystem = GetComponent<PlayerSpawnSystem>();
        if (playerSpawnSystem != null) return playerSpawnSystem;

        playerSpawnSystem = FindFirstObjectByType<PlayerSpawnSystem>(FindObjectsInactive.Exclude);
        return playerSpawnSystem;
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetPlayerReadyServerRpc(ServerRpcParams serverRpcParams = default)
    {
        ulong clientId = serverRpcParams.Receive.SenderClientId;
        playerReadyDictionary[clientId] = true;
        CheckAllClientsReady();
        Debug.Log($"Client {clientId} is ready. All clients ready: {allClientsReady}");
    }

    private void State_OnValueChanged(State previousValue, State newValue)
    {
        if (IsServer)
        {
            if (newValue == State.Countdown)
            {
                countdownTimerRaw = waitingDuration;
                countdownTimer.Value = waitingDuration;
            }
            else if (newValue == State.Playing)
            {
                playingTimerRaw = playingDuration;
                playingTimer.Value = playingDuration;
            }
        }

        PlayMatchTransitionAudio(previousValue, newValue);
        OnStateChanged?.Invoke(this, EventArgs.Empty);

        if (IsServer && newValue == State.GameOver && !resultBroadcastedForThisMatch)
        {
            resultBroadcastedForThisMatch = true;

            bool isSingle = CampaignRuntimeContext.IsCampaignRun;
            bool isCoop = CoopCampaignSessionContext.IsCoopCampaignRun;

            if (isSingle || isCoop)
            {
                string levelId = isSingle
                    ? CampaignRuntimeContext.SelectedLevelId
                    : CoopCampaignSessionContext.SelectedLevelId;

                ApplyCampaignResultClientRpc(levelId, cachedFinalScore, isSingle, isCoop);
                Debug.Log($"[GameManager] Broadcast campaign result. level={levelId}, score={cachedFinalScore}, single={isSingle}, coop={isCoop}");
            }

            PublishMatchResultsServer();
        }
    }

    [ClientRpc]
    private void ApplyCampaignResultClientRpc(string levelId, int finalScore, bool isSingle, bool isCoop)
    {
        CampaignResultApplier.ApplyLocalResult(levelId, finalScore, isSingle, isCoop);
    }

    private void GameInput_OnPauseAction(object sender, EventArgs e) => PauseToggle();

    public void PauseToggle()
    {
        if (!IsMainMenu())
        {
            isGamePaused = !isGamePaused;
            if (isGamePaused) OnGamePaused?.Invoke(this, EventArgs.Empty);
            else OnGameUnpaused?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsGamePlaying() => state.Value == State.Playing;
    public bool IsCountdownActive() => state.Value == State.Countdown;
    public float GetCountdownTimer() => countdownTimer.Value;
    public bool IsGameOver() => state.Value == State.GameOver;
    public bool IsMainMenu() => state.Value == State.MainMenu;
    public bool IsGameWaiting() => state.Value == State.Waiting;
    public float GetClockTimerNormalized() => 1f - (playingTimer.Value / playingDuration);
    public bool IsLocalPlayerReady() => isLocalPlayerReady;
    public State GetGameState() => state.Value;

    public void SetGameState(State newState) => state.Value = newState;

    public void SetLocalPlayerReady(bool value)
    {
        isLocalPlayerReady = value;
        SetPlayerReadyServerRpc();
        OnLocalPlayerReadyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CheckAllClientsReady()
    {
        if (state.Value != State.Waiting) return;

        bool allReady = true;
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (!playerReadyDictionary.TryGetValue(clientId, out bool isReady) || !isReady)
            {
                allReady = false;
                break;
            }
        }

        allClientsReady = allReady;
        if (allReady) state.Value = State.Countdown;
    }

    public override void OnNetworkDespawn()
    {
        state.OnValueChanged -= State_OnValueChanged;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= SceneManager_OnLoadEventCompleted;
            NetworkManager.Singleton.OnClientDisconnectCallback -= NetworkManager_Server_OnClientDisconnectCallback;
        }
    }

    [ClientRpc]
    private void ReturnToMenuClientRpc()
    {
        if (SessionCoordinator.Instance != null) SessionCoordinator.Instance.LeaveToMainMenu();
        else Loader.Load(Loader.Scene.MenuScene);
    }

    private EconomyGameMode ResolveEconomyMode()
    {
        if (CoopCampaignSessionContext.IsCoopCampaignRun) return EconomyGameMode.CoopCampaign;
        if (GameMultiplayerManager.playMultiplayer) return EconomyGameMode.Multiplayer;
        return EconomyGameMode.OfflineSingleplayer;
    }

    private int CalculateStarsFromScoreForCurrentLevel(int score)
    {
        string levelId = CampaignRuntimeContext.IsCampaignRun
            ? CampaignRuntimeContext.SelectedLevelId
            : CoopCampaignSessionContext.SelectedLevelId;

        if (!string.IsNullOrWhiteSpace(levelId) && CampaignManager.Instance != null)
        {
            var def = CampaignManager.Instance.GetLevelDefinition(levelId);
            if (def != null)
                return Mathf.RoundToInt(CampaignManager.Instance.CalculateStars(score, def));
        }

        if (score >= 1000) return 3;
        if (score >= 650) return 2;
        if (score >= 300) return 1;
        return 0;
    }

    private void PublishMatchResultsServer()
    {
        if (!IsServer) return;
        if (MatchResultNetSync.Instance == null) return;

        int matchId = ++s_serverMatchSequence;

        var ranking = new List<PlayerMatchResultData>();
        var connected = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);

        int blueTeamScore = 0;
        int redTeamScore = 0;

        EconomyGameMode mode = ResolveEconomyMode();
        bool isCoop = mode == EconomyGameMode.CoopCampaign;

        for (int i = 0; i < connected.Count; i++)
        {
            ulong clientId = connected[i];

            string pName = $"Player {clientId}";
            if (GameMultiplayerManager.Instance != null)
            {
                var pd = GameMultiplayerManager.Instance.GetPlayerDataFromClientId(clientId);
                if (!pd.playerName.IsEmpty) pName = pd.playerName.ToString();
            }

            int playerScore = ScoreManager.Instance != null
                ? ScoreManager.Instance.GetScoreForClient(clientId)
                : 0;

            MatchTeam team = isCoop ? MatchTeam.Blue : TeamInteractionRules.ResolveTeam(clientId);

            if (team == MatchTeam.Blue) blueTeamScore += playerScore;
            else redTeamScore += playerScore;

            ranking.Add(new PlayerMatchResultData
            {
                clientId = clientId,
                playerName = pName,
                score = playerScore,
                team = (byte)team
            });
        }

        ranking.Sort((a, b) => b.score.CompareTo(a.score));

        string topName = ranking.Count > 0 ? ranking[0].playerName.ToString() : "-";
        int topScore = ranking.Count > 0 ? ranking[0].score : 0;

        MatchSummaryData shared = new MatchSummaryData
        {
            matchId = matchId,
            matchDurationSeconds = GetPlayingDuration(),
            topPlayerName = topName,
            topScore = topScore,
            mode = (byte)mode,
            blueTeamScore = blueTeamScore,
            redTeamScore = redTeamScore,

            localTeam = (byte)MatchTeam.Blue,
            localScore = 0,
            localStars = 0,
            localCoinsEarned = 0,
            localDiamondsEarned = 0,
            localDiamondReason = "",
            localOutcome = (byte)MatchOutcome.None,
            applyRewardLocally = 0
        };

        MatchResultNetSync.Instance.ServerPublishResults(ranking, shared);

        bool blueWins = blueTeamScore > redTeamScore;
        bool redWins = redTeamScore > blueTeamScore;
        bool isDraw = blueTeamScore == redTeamScore;

        for (int i = 0; i < connected.Count; i++)
        {
            ulong clientId = connected[i];
            MatchTeam localTeam = isCoop ? MatchTeam.Blue : TeamInteractionRules.ResolveTeam(clientId);

            int localTeamScore = localTeam == MatchTeam.Blue ? blueTeamScore : redTeamScore;
            int stars = CalculateStarsFromScoreForCurrentLevel(localTeamScore);

            MatchOutcome outcome;
            if (isDraw) outcome = MatchOutcome.Draw;
            else if ((localTeam == MatchTeam.Blue && blueWins) || (localTeam == MatchTeam.Red && redWins))
                outcome = MatchOutcome.Victory;
            else
                outcome = MatchOutcome.Defeat;

            MatchSummaryData local = new MatchSummaryData
            {
                matchId = matchId,
                matchDurationSeconds = GetPlayingDuration(),
                topPlayerName = topName,
                topScore = topScore,
                mode = (byte)mode,
                blueTeamScore = blueTeamScore,
                redTeamScore = redTeamScore,

                localTeam = (byte)localTeam,
                localScore = localTeamScore,
                localStars = stars,
                localCoinsEarned = 0,
                localDiamondsEarned = 0,
                localDiamondReason = "",
                localOutcome = (byte)outcome,
                applyRewardLocally = 1
            };

            var toClient = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { clientId }
                }
            };

            MatchResultNetSync.Instance.ApplyLocalSummaryClientRpc(local, toClient);
        }

        Debug.Log($"[MatchResult] Published. matchId={matchId}, rows={ranking.Count}, top={topName}, topScore={topScore}, blue={blueTeamScore}, red={redTeamScore}");
    }

    public float GetGameOverDuration() => gameOverDuration;

    public float GetGameOverTimerRemaining()
    {
        if (state.Value != State.GameOver) return 0f;
        return Mathf.Max(0f, gameOverTimer);
    }

    public float GetGameOverReturnNormalized()
    {
        if (gameOverDuration <= 0f) return 1f;
        return Mathf.Clamp01(1f - (GetGameOverTimerRemaining() / gameOverDuration));
    }

    private void ResolveMatchAudioSource()
    {
        if (matchAudioSource == null)
            matchAudioSource = GetComponent<AudioSource>();
        if (matchAudioSource == null)
            matchAudioSource = gameObject.AddComponent<AudioSource>();

        matchAudioSource.playOnAwake = false;
        matchAudioSource.loop = false;
        matchAudioSource.spatialBlend = 0f;
        if (matchSfxMixerGroup != null)
            matchAudioSource.outputAudioMixerGroup = matchSfxMixerGroup;
    }

    private void PlayMatchTransitionAudio(State previousValue, State newValue)
    {
        if (matchAudioSource == null) return;

        if (previousValue == State.Countdown && newValue == State.Playing)
            PlayMatchClipFromPool(countdownFinishedClips, countdownFinishedVolume);
        else if (previousValue == State.Playing && newValue == State.GameOver)
            PlayMatchClipFromPool(matchTimerFinishedClips, matchTimerFinishedVolume);
    }

    private void PlayMatchClipFromPool(AudioClip[] clipPool, float volume)
    {
        AudioClip clip = PickRandomClip(clipPool);
        if (clip == null) return;
        matchAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip PickRandomClip(AudioClip[] clipPool)
    {
        if (clipPool == null || clipPool.Length == 0) return null;
        int start = UnityEngine.Random.Range(0, clipPool.Length);
        for (int i = 0; i < clipPool.Length; i++)
        {
            AudioClip clip = clipPool[(start + i) % clipPool.Length];
            if (clip != null) return clip;
        }
        return null;
    }

    private void OnValidate()
    {
        countdownTimerReplicationStep = Mathf.Clamp(countdownTimerReplicationStep, 0.01f, 0.5f);
        playingTimerReplicationStep = Mathf.Clamp(playingTimerReplicationStep, 0.01f, 0.5f);
        countdownFinishedVolume = Mathf.Clamp01(countdownFinishedVolume);
        matchTimerFinishedVolume = Mathf.Clamp01(matchTimerFinishedVolume);
    }
}
