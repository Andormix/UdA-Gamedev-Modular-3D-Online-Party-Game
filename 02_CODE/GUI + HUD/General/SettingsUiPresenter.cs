using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsUiPresenter : MonoBehaviour
{
    [Header("Scene dependencies (drag from scene)")]
    [SerializeField] private UserInterfaceManager ui;
    [SerializeField] private MusicManager musicManager; // if MusicManager is persistent latter, reference  via the persistent object in scene

    [Header("View")]
    [SerializeField] private Button musicButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private TextMeshProUGUI musicTxT;

    private void Awake()
    {
        if (ui == null || musicManager == null || musicButton == null || closeButton == null || musicTxT == null)
        {
            Debug.LogError($"{nameof(SettingsUiPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }

        // Commands
        musicButton.onClick.AddListener(OnMusicClicked);
        closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void Start()
    {
        Refresh();
    }

    private void OnMusicClicked()
    {
        musicManager.ChangeVolume();
        Refresh();
    }

    private void OnCloseClicked()
    {
        ui.Hide(UserInterfaceManager.UIPage.Settings);
        ui.Show(UserInterfaceManager.UIPage.Pause);
    }

    private void Refresh()
    {
        musicTxT.text = "Music volume: " + Mathf.Round(musicManager.GetVolume() * 10f);
    }
}