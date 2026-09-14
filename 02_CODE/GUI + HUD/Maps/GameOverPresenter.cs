using System;
using TMPro;
using UnityEngine;

public sealed class GameOverPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    [SerializeField] private GameManager gameManager;
    [SerializeField] private RecipeManager recipeManager;
    [SerializeField] private UserInterfaceManager ui;

    [Header("View")]
    [SerializeField] private TextMeshProUGUI craftedTxt;

    private void Awake()
    {
        if (gameManager == null || recipeManager == null || ui == null || craftedTxt == null)
        {
            Debug.LogError($"{nameof(GameOverPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }
    }

    private void Start()
    {
        // Start is safer than Awake (UI manager finishes its Awake map setup) TODO 109: Doc RNF a la memo
        ui.Hide(UserInterfaceManager.UIPage.GameOver);
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
        if (gameManager.IsGameOver())
        {
            craftedTxt.text = recipeManager.GetSuccessfulCraftsQTY().ToString();
            ui.Show(UserInterfaceManager.UIPage.GameOver);
        }
        else
        {
            ui.Hide(UserInterfaceManager.UIPage.GameOver);
        }
    }
}