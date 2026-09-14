using Unity.Netcode;
using UnityEngine;

public class MainMenuCleanup : MonoBehaviour
{
    private void Awake()
    {
        // Stop active session transport if still alive.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        // IMPORTANT:
        // Ara NOT destroy GameLobby / GameMultiplayerManager anymore.
        // MenuScene now hosts integrated lobby browsing + create/join.
        // Persistent core should remain alive:
        // - SessionCoordinator
        // - CampaignManager
        // - GameLobby
        // - GameMultiplayerManager
    }
}