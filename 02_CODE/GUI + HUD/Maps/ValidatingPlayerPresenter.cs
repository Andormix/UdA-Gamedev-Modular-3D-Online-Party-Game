using System;
using UnityEngine;

public class ValidatingPlayerPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private UserInterfaceManager ui;

    private void Awake()
    {
        if (gameManager == null  || ui == null)
        {
            Debug.LogError($"{nameof(ValidatingPlayerPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Start is safer than Awake RNF Sec
        ui.Show(UserInterfaceManager.UIPage.Validating);
    }

    private void OnEnable()
    {
        gameManager.OnLocalPlayerReadyChanged += GameManager_OnLocalPlayerReadyChanged;
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnLocalPlayerReadyChanged -= GameManager_OnLocalPlayerReadyChanged;
    }

    private void GameManager_OnLocalPlayerReadyChanged(object sender, EventArgs e)
    {

        if (gameManager.IsLocalPlayerReady())
        {
            ui.Hide(UserInterfaceManager.UIPage.Validating);
        }
        else
        {
            ui.Show(UserInterfaceManager.UIPage.Validating);
        }
    }
}
