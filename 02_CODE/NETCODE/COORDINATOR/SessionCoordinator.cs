using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using Unity.Netcode.Transports.UTP;

public sealed class SessionCoordinator : MonoBehaviour
{
    public static SessionCoordinator Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private NetworkManager networkManagerPrefab;

    [Header("Config")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    private bool _leaving;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public NetworkManager EnsureNetworkManager()
    {
        if (NetworkManager.Singleton != null)
            return NetworkManager.Singleton;

        if (networkManagerPrefab == null)
        {
            Debug.LogError($"{nameof(SessionCoordinator)}: NetworkManager prefab not assigned.", this);
            return null;
        }

        NetworkManager nm = Instantiate(networkManagerPrefab);
        if (dontDestroyOnLoad)
            DontDestroyOnLoad(nm.gameObject);

        return nm;
    }

    public async Task LeaveToMainMenuAsync()
    {
        if (_leaving) return;
        _leaving = true;

        if (GameMultiplayerManager.Instance != null)
            GameMultiplayerManager.Instance.MarkSessionShuttingDown();

        if (GameLobby.Instance != null)
        {
            try
            {
                await GameLobby.Instance.LeaveLobby();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"LeaveLobby failed (ignored): {e.Message}");
            }
        }

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            await Task.Yield();
            await Task.Yield();
        }
        NetTickBudget.ResetAll();
        CampaignRuntimeContext.Clear();
        CoopCampaignSessionContext.Clear();
        TutorialRuntimeContext.Clear();

        if (GameMultiplayerManager.Instance != null)
        {
            GameMultiplayerManager.Instance.ResetSessionState();
        }

        Loader.Load(Loader.Scene.MenuScene);
        _leaving = false;
    }

    public void LeaveToMainMenu()
    {
        _ = LeaveToMainMenuAsync();
    }

    public void StartSingleplayerHostLocal()
    {
        StartSingleplayerHostLocal(Loader.Scene.Map_01);
    }

    public void StartSingleplayerHostLocal(Loader.Scene targetScene)
    {
        var nm = EnsureNetworkManager();
        if (nm == null) return;

        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null)
        {
            Debug.LogError("UnityTransport missing on NetworkManager prefab.");
            return;
        }

        utp.SetConnectionData("127.0.0.1", 7777);

        if (nm.IsListening)
        {
            Debug.LogWarning("NetworkManager already listening; loading gameplay scene anyway.");
            Loader.LoadNetwork(targetScene);
            return;
        }

        nm.StartHost();
        Loader.LoadNetwork(targetScene);
    }
}
