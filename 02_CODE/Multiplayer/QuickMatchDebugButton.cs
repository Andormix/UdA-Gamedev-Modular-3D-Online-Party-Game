using UnityEngine;
using UnityEngine.UI;

public class QuickMatchDebugButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();

        button.onClick.AddListener(async () =>
        {
            if (GameLobby.Instance == null)
            {
                Debug.LogError("[QuickMatchDebug] GameLobby.Instance is null");
                return;
            }

            await GameLobby.Instance.QuickMatchMultiplayer();
        });
    }
}