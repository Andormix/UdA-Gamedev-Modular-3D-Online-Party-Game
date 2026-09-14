using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class DisconnectUI : MonoBehaviour
{
    [SerializeField] private Button returnMainMenuButton;

    private void Awake()
    {
        returnMainMenuButton.onClick.AddListener(() => {
            if (SessionCoordinator.Instance != null)
            {
                SessionCoordinator.Instance.LeaveToMainMenu();
                return;
            }

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
            Loader.Load(Loader.Scene.MenuScene);
        });
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectCallback;
        }
        Hide();
    }

    private void OnDestroy()
    {
        // SIEMPRE desuscribirse de eventos estáticos o persistentes
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= NetworkManager_OnClientDisconnectCallback;
        }

    }

    private void NetworkManager_OnClientDisconnectCallback(ulong clientId)
    {
        // Si el clientId es el del Servidor (0), significa que perdimos la conexión
        // O si somos nosotros mismos los que nos desconectamos
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (clientId == NetworkManager.ServerClientId || clientId == nm.LocalClientId)
        {
            Show();
        }
    }

    private void Show() => gameObject.SetActive(true);
    private void Hide() => gameObject.SetActive(false);
}
