using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class PauseUiPresenter : MonoBehaviour
{
    [Header("Scene dependencies (drag from scene)")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UserInterfaceManager ui;

    [Header("View (drag from scene UI)")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button optionsButton;

    private void Awake()
    {
        // "Fail fast"
        if (gameManager == null || ui == null || resumeButton == null || menuButton == null || optionsButton == null)
        {
            Debug.LogError($"{nameof(PauseUiPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }

        // Commands (View -> System)
        resumeButton.onClick.AddListener(gameManager.PauseToggle);


        menuButton.onClick.AddListener(() =>
        {
            if (SessionCoordinator.Instance == null)
            {
                Debug.LogError("SessionCoordinator missing.");
                Loader.Load(Loader.Scene.MenuScene); // fallback
                return;
            }

            SessionCoordinator.Instance.LeaveToMainMenu();
        });


        optionsButton.onClick.AddListener(OpenOptions);
    }

    private void OnEnable()
    {
        // Notifications: System a Presenter a View)
        gameManager.OnGamePaused += OnPaused;
        gameManager.OnGameUnpaused += OnUnpaused;
    }

    private void OnDisable()
    {
        if (gameManager == null) return;
        gameManager.OnGamePaused -= OnPaused;
        gameManager.OnGameUnpaused -= OnUnpaused;
    }

    private void OnPaused(object sender, EventArgs e)
    {
        ui.Show(UserInterfaceManager.UIPage.Pause);
    }

    private void OnUnpaused(object sender, EventArgs e)
    {
        //ui.Hide(UserInterfaceManager.UIPage.Pause);
        ui.HideAll();
    }

    private void OpenOptions()
    {
        ui.Show(UserInterfaceManager.UIPage.Settings);
        ui.Hide(UserInterfaceManager.UIPage.Pause);
    }
}