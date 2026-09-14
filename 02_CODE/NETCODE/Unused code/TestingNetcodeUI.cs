using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class TestingNetcodeUI : MonoBehaviour
{
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;

    private void Awake()
    {
        startHostButton.onClick.AddListener(() =>
        {
            GameMultiplayerManager.Instance.StartHost();
            //NetworkManager.Singleton.StartHost();
            Debug.Log("We started as host");
            Hide();
        });

        startClientButton.onClick.AddListener(() =>
        {
            GameMultiplayerManager.Instance.StartClient();
            Debug.Log("We started as client");
            Hide();
        });
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }
}
