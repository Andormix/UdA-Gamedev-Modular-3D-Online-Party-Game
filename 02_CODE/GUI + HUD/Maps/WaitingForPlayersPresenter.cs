using System;
using UnityEngine;

public class WaitingForPlayersPresenter : MonoBehaviour
{

    [Header("Scene dependencies")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UserInterfaceManager ui;

    private void Awake()
    {
        if (gameManager == null  || ui == null)
        {
            Debug.LogError($"{nameof(WaitingForPlayersPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Start is safer than Awake RNF Sec
        ui.Show(UserInterfaceManager.UIPage.Waiting);
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

    private void GameManager_OnStateChanged(object sender, EventArgs e)
    {
        if (gameManager.IsGameWaiting())
        {
            ui.Show(UserInterfaceManager.UIPage.Waiting);
        }
        else
        {
            ui.Hide(UserInterfaceManager.UIPage.Waiting);
        }
    }
}


