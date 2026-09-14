using System;
using UnityEngine;

public class PlayerInteractionSoundHandler : MonoBehaviour
{
    // Resolved automatically via GetComponent in Awake; no Inspector wiring needed.
    private Player player;
    private PlayerInteractions playerInteractions;
    private PlayerInput playerInput;

    // Tracks the last interactable the player looked at.
    private InteractableAsset currentSelectedAsset;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerInteractions = GetComponent<PlayerInteractions>();
        playerInput = GetComponent<PlayerInput>();

        if (playerInteractions == null || playerInput == null || player == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionSoundHandler)}: missing required components on '{gameObject.name}'. " +
                           "Ensure Player, PlayerInteractions, and PlayerInput are on the same GameObject.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        playerInteractions.OnSelectedAssetChanged += HandleSelectedAssetChanged;
        playerInput.OnInteractAction += HandleInteractAction;
    }

    private void OnDisable()
    {
        if (playerInteractions != null)
            playerInteractions.OnSelectedAssetChanged -= HandleSelectedAssetChanged;

        if (playerInput != null)
        {
            playerInput.OnInteractAction -= HandleInteractAction;
        }
    }

    // -----------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------

    private void HandleSelectedAssetChanged(object sender, PlayerInteractions.OnSelectedAssetChangedEventArgs e)
    {
        if (!player.IsOwner) return;

        currentSelectedAsset = e.selectedAsset;

        if (currentSelectedAsset != null &&
            currentSelectedAsset.TryGetComponent<InteractableSoundEmitter>(out var emitter))
        {
            emitter.PlayHover();
        }
    }
    
    private void HandleInteractAction(object sender, EventArgs e)
    {
        if (!player.IsOwner) return;
        if (currentSelectedAsset == null) return;

        if (currentSelectedAsset.TryGetComponent<InteractableSoundEmitter>(out var emitter))
            emitter.PlayInteract();
    }

    // Work hold sound is network/state-driven in WorkstationNetSync to keep
    // owner + remote clients consistent and avoid client-only divergence.
}
