using UnityEngine;

public class MenuMultiplayerBootstrap : MonoBehaviour
{
    [Header("Prefabs (from your old LobbyScene setup)")]
    [SerializeField] private SessionCoordinator sessionCoordinatorPrefab;
    [SerializeField] private GameMultiplayerManager gameMultiplayerManagerPrefab;
    [SerializeField] private GameLobby gameLobbyPrefab;

    private void Awake()
    {
        EnsureSessionCoordinator();
        EnsureGameMultiplayerManager();
        EnsureGameLobby();
    }

    private void EnsureSessionCoordinator()
    {
        if (SessionCoordinator.Instance != null) return;

        if (sessionCoordinatorPrefab == null)
        {
            Debug.LogError("[MenuMultiplayerBootstrap] SessionCoordinator prefab missing.");
            return;
        }

        Instantiate(sessionCoordinatorPrefab);
    }

    private void EnsureGameMultiplayerManager()
    {
        if (GameMultiplayerManager.Instance != null) return;

        if (gameMultiplayerManagerPrefab == null)
        {
            Debug.LogError("[MenuMultiplayerBootstrap] GameMultiplayerManager prefab missing.");
            return;
        }

        Instantiate(gameMultiplayerManagerPrefab);
    }

    private void EnsureGameLobby()
    {
        if (GameLobby.Instance != null) return;

        if (gameLobbyPrefab == null)
        {
            Debug.LogError("[MenuMultiplayerBootstrap] GameLobby prefab missing.");
            return;
        }

        Instantiate(gameLobbyPrefab);
    }
}