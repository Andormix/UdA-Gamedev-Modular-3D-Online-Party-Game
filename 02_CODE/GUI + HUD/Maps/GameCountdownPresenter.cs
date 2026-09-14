using System;
using TMPro;
using UnityEngine;

public sealed class GameCountdownPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UserInterfaceManager ui;

    [Header("View")]
    [SerializeField] private TextMeshProUGUI countdownTxT;

    private void Awake()
    {
        if (gameManager == null || ui == null || countdownTxT == null)
        {
            Debug.LogError($"{nameof(GameCountdownPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }

        ui.Hide(UserInterfaceManager.UIPage.Countdown);
    }

    private void OnEnable()
    {
        gameManager.OnStateChanged += GameManager_OnStateChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnStateChanged -= GameManager_OnStateChanged;
    }

    private void Update()
    {
        if (TutorialRuntimeContext.IsTutorialRun)
        {
            ui.Hide(UserInterfaceManager.UIPage.Countdown);
            return;
        }

        if (!ui.IsVisible(UserInterfaceManager.UIPage.Countdown))
            return;

        float t = gameManager.GetCountdownTimer();

        // Shows: 5 4 3 2 1 GO! 
        // TODO 77: Keep "1" visible before switching to "GO!"
        if (t <= 0f)
            countdownTxT.text = "GO!";
        else
            countdownTxT.text = Mathf.CeilToInt(t).ToString();
    }

    private void GameManager_OnStateChanged(object sender, EventArgs e)
    {
        if (TutorialRuntimeContext.IsTutorialRun)
        {
            ui.Hide(UserInterfaceManager.UIPage.Countdown);
            return;
        }

        if (gameManager.IsCountdownActive())
            ui.Show(UserInterfaceManager.UIPage.Countdown);
        else
            ui.Hide(UserInterfaceManager.UIPage.Countdown);
    }
}
