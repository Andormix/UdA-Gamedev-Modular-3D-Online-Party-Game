using TMPro;
using UnityEngine;

public sealed class InteractPromptPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    private PlayerInteractions playerInteractions;
    [SerializeField] private UserInterfaceManager ui;

    [Header("View")]
    [SerializeField] private TextMeshProUGUI interactTxt;

    private void Awake()
    {
        if (ui == null || interactTxt == null)
        {
            Debug.LogError($"{nameof(InteractPromptPresenter)} missing references in Inspector.", this);
            enabled = false;
            return;
        }
    }

    private void OnEnable() 
    {
        PlayerEvents.OnLocalPlayerSpawned += HandlePlayerSpawned;
    }   

    private void HandlePlayerSpawned(PlayerInteractions player) 
    {
        playerInteractions = player;
        playerInteractions.OnPromptTargetChanged += PlayerInteractions_OnPromptTargetChanged;
    }

    private void Start()
    {
        // Ensure hidden on load 
        ui.Hide(UserInterfaceManager.UIPage.Interact);

        // Sync initial state in case the player is already looking at something  TODO 43
        //Apply(playerInteractions.GetSelected());
    }

    private void OnDisable()
    {
        if (playerInteractions != null)
            playerInteractions.OnPromptTargetChanged -= PlayerInteractions_OnPromptTargetChanged;
    }

    private void PlayerInteractions_OnPromptTargetChanged(object sender, PlayerInteractions.OnPromptTargetChangedEventArgs e)
    {
        Apply(e.promptText);
    }

    private void Apply(string nextPromptText)
    {
        if (string.IsNullOrWhiteSpace(nextPromptText))
        {
            ui.Hide(UserInterfaceManager.UIPage.Interact);
            return;
        }

        interactTxt.text = nextPromptText;
        ui.Show(UserInterfaceManager.UIPage.Interact);
    }
}