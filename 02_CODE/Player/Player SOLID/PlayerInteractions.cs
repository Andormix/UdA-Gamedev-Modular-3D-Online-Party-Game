using System;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine;

public class PlayerInteractions : MonoBehaviour
{
    [Header("Scene dependencies")]
    private GameManager gameManager;

    [Header("Input / Detection")]
    [SerializeField] private PlayerInput gameInput;
    [SerializeField] private LayerMask interactableLayerMask;
    [SerializeField] private LayerMask interactableObjectsLayerMask;
    [SerializeField] private float interactionDistance = 2f;
    [SerializeField] private float pickupDistance = 1.5f;
    [SerializeField] private float pointerRayMaxDistance = 250f;
    [SerializeField] private PlayerClickMoveController clickMoveController;


    private Vector3 lastInteractDirection;
    private InteractableAsset selectedAsset;
    private Player player;
    // Client-side ref to the workstation the local player is currently holding.
    // Used to reliably send a stop even when the player has looked away.
    private WorkstationNetSync _activeWorkstationHold;

    [SerializeField] private TutorialEventChannelSO tutorialEvents;
    private bool _raisedMovedFirstTime;
    private bool _raisedLookedAtInteractable;
    private string _lastLookTargetId;
    private Camera _cachedCamera;
    private int _combinedPointerMask;

    public event EventHandler<OnSelectedAssetChangedEventArgs> OnSelectedAssetChanged;
    public class OnSelectedAssetChangedEventArgs : EventArgs
    {
        public InteractableAsset selectedAsset;
    }
    public event EventHandler<OnPromptTargetChangedEventArgs> OnPromptTargetChanged;
    public class OnPromptTargetChangedEventArgs : EventArgs
    {
        public string promptText;
        public InteractableAsset selectedAsset;
        public SceneObject selectedSceneObject;
    }

    private InteractableAsset promptAsset;
    private SceneObject promptSceneObject;
    private string promptText;

    private void Awake()
    {
        player = GetComponent<Player>();

        if ( gameInput == null)
        {
            Debug.LogError($"{nameof(PlayerInteractions)} missing references in Inspector.", this);
            enabled = false;
            return;
        }

        if (clickMoveController == null)
            clickMoveController = GetComponent<PlayerClickMoveController>();

        gameManager = GameManager.Instance;
        _combinedPointerMask = interactableLayerMask.value | interactableObjectsLayerMask.value;
    }

    private void Start()
    {
        // Only the local player should register as "local"
        var playerComponent = GetComponent<Player>();
        if (playerComponent != null && playerComponent.IsOwner)
        {
            PlayerEvents.OnLocalPlayerSpawned?.Invoke(this);
        }
    }
    private void OnEnable()
    {
        // Subscribe when enabled
        gameInput.OnInteractAction += GameInput_OnInteractAction;
        gameInput.OnDropAction += GameInput_OnDropAction;

        gameInput.OnInteractStarted += GameInput_OnInteractStarted;
        gameInput.OnInteractCanceled += GameInput_OnInteractCanceled;
        gameInput.OnDropStarted += GameInput_OnDropStarted;
        gameInput.OnDropCanceled += GameInput_OnDropCanceled;
    }

    private void OnDisable()
    {
        // Unsubscribe when disabled (prevents leaks / double subscriptions)
        if (gameInput != null)
        {
            gameInput.OnInteractAction -= GameInput_OnInteractAction;
            gameInput.OnDropAction -= GameInput_OnDropAction;

            gameInput.OnInteractStarted -= GameInput_OnInteractStarted;
            gameInput.OnInteractCanceled -= GameInput_OnInteractCanceled;
            gameInput.OnDropStarted -= GameInput_OnDropStarted;
            gameInput.OnDropCanceled -= GameInput_OnDropCanceled;
        }

        // Release any active workstation hold so the server lock is freed
        // even if this player object is disabled / destroyed mid-hold.
        if (_activeWorkstationHold != null)
        {
            _activeWorkstationHold.RequestStopWork();
            _activeWorkstationHold = null;
        }
    }

    private void GameInput_OnInteractStarted(object sender, EventArgs e)
    {
        HandleInteractStarted();
    }

    private void GameInput_OnInteractCanceled(object sender, EventArgs e)
    {
        HandleInteractCanceled();
    }

    private void GameInput_OnDropStarted(object sender, EventArgs e)
    {
        if (!CanUseQAsContextInteract()) return;
        HandleInteractStarted();
    }

    private void GameInput_OnDropCanceled(object sender, EventArgs e)
    {
        if (_activeWorkstationHold == null && !CanUseQAsContextInteract()) return;
        HandleInteractCanceled();
    }

    private void HandleInteractStarted()
    {
        if (!gameManager.IsGamePlaying()) return;
        if (selectedAsset == null) return;

        if (selectedAsset.TryGetComponent<WorkstationNetSync>(out var wsNet))
        {
            // Start hold only if currently selected target is a workstation
            wsNet.RequestStartWork();
            _activeWorkstationHold = wsNet;
            return;
        }
    }

    private void HandleInteractCanceled()
    {
        if (!gameManager.IsGamePlaying()) return;

        // Always stop via the tracked hold reference – this is reliable even when
        // the player has looked away from the workstation before releasing the key.
        if (_activeWorkstationHold != null)
        {
            _activeWorkstationHold.RequestStopWork();
            _activeWorkstationHold = null;
            return;
        }

        // Fallback: current selection happens to be a workstation (e.g. player never moved away). Manté el mateix behaviour for the non-tracking path.
        if (selectedAsset != null && selectedAsset.TryGetComponent<WorkstationNetSync>(out var wsNet))
        {
            wsNet.RequestStopWork();
        }
    }

    private void GameInput_OnDropAction(object sender, EventArgs e)
    {
        if (CanUseQAsContextInteract())
        {
            HandleInteractTap(includeReadyToggle: false);
            return;
        }

        RequestDrop();
    }

    private void GameInput_OnInteractAction(object sender, EventArgs e)
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleMousePrimaryAction();
            return;
        }

        HandleInteractTap(includeReadyToggle: true);
    }

    private void HandleMousePrimaryAction()
    {
        if (!gameManager.IsGamePlaying()) return;
        if (IsPointerOverUi()) return;

        if (TryPickupPointedObject())
            return;

        if (TryGetPointerTarget(out InteractableAsset pointedInteractable, out _, out Vector3 pointerDir)
            && pointedInteractable != null)
        {
            if (pointerDir != Vector3.zero)
                lastInteractDirection = pointerDir;

            SetSelectedAsset(pointedInteractable);
            HandleInteractTap(includeReadyToggle: false);
            return;
        }

        if (clickMoveController != null && TryGetPointerRay(out Ray pointerRay))
            clickMoveController.TrySetDestinationFromPointer(pointerRay);
    }

    private bool CanUseQAsContextInteract()
    {
        if (!gameManager.IsGamePlaying()) return false;
        if (selectedAsset == null) return false;
        return IsQInteractTarget(selectedAsset);
    }

    private static bool IsQInteractTarget(InteractableAsset target)
    {
        if (target == null) return false;
        return target.TryGetComponent<TableNetSync>(out _)
            || target.TryGetComponent<WorkstationNetSync>(out _)
            || target.TryGetComponent<SinkStationNetSync>(out _);
    }

    private void HandleInteractTap(bool includeReadyToggle)
    {
        if(gameManager.IsGameWaiting())
        {
            if (includeReadyToggle)
                gameManager.SetLocalPlayerReady(true);
        }

        if (!gameManager.IsGamePlaying()) return;

        // 1) Looking at an interactable
        if (selectedAsset != null)
        {

            // Tables: client -> server RPC
            if (selectedAsset.TryGetComponent<TableNetSync>(out var tableNet))
            {
                RequestPickup(); 

                // If table is AwaitingPayment, only allow interaction if holding TPV
                if (tableNet.CurrentPhase == WorkstationPhaseId.AwaitingPayment)
                {
                    var carry = player.GetComponent<PlayerCarryNet>();
                    if (carry == null || !carry.IsHoldingTPV())
                        return; // block E (client-side)
                }

                tableNet.RequestInteract();
                return;
            }

            // Spawners: client -> server RPC
            if (selectedAsset.TryGetComponent<ObjectSpawnerNetSync>(out var spawnerNet))
            {
                spawnerNet.RequestInteract();
                return;
            }

            // Sink: client -> server RPC for deposit interaction
            if (selectedAsset.TryGetComponent<SinkStationNetSync>(out var sinkNet))
            {
                sinkNet.RequestDepositInteract();
                return;
            }

            //Generic
            selectedAsset.Interact(player);
            return;
        }

        // 2) Not looking at an interactable: handle pickup
        RequestPickup(); 
    }

    private void SetSelectedAsset(InteractableAsset newSelected)
    {
        if (selectedAsset == newSelected) return;

        if (newSelected != null)
        {
            string targetId = null;
            var tag = newSelected.GetComponent<TutorialTargetTag>();
            if (tag != null && !string.IsNullOrWhiteSpace(tag.tutorialTargetId))
                targetId = tag.tutorialTargetId;

            // Raise only if changed target (avoid spam)
            if (_lastLookTargetId != targetId)
            {
                _lastLookTargetId = targetId;
                tutorialEvents?.Raise(TutorialEventType.LookedAtInteractable, targetId);
            }
        }

        selectedAsset = newSelected;
        OnSelectedAssetChanged?.Invoke(this, new OnSelectedAssetChangedEventArgs
        {
            selectedAsset = selectedAsset
        });
    }

    public void HandleInteractions()
    {
        if (player != null && !player.IsOwner)
            return;

        Vector2 inputVector = gameInput.GetMovementVectorNorm();
        Vector3 movementDirection = new Vector3(inputVector.x, 0f, inputVector.y);
        Vector2 mouseMoveVector = clickMoveController != null ? clickMoveController.GetMoveVector() : Vector2.zero;
        Vector3 mouseMoveDirection = new Vector3(mouseMoveVector.x, 0f, mouseMoveVector.y);

        if (!_raisedMovedFirstTime && (movementDirection != Vector3.zero || mouseMoveDirection != Vector3.zero))
        {
            _raisedMovedFirstTime = true;
            tutorialEvents?.Raise(TutorialEventType.MovedFirstTime);
        }

        if (movementDirection != Vector3.zero)
            lastInteractDirection = movementDirection;
        else if (mouseMoveDirection != Vector3.zero)
            lastInteractDirection = mouseMoveDirection;

        if (!IsPointerOverUi() && TryGetPointerTarget(out InteractableAsset pointerInteractable, out SceneObject pointerSceneObject, out Vector3 pointerDir))
        {
            if (pointerDir != Vector3.zero)
                lastInteractDirection = pointerDir;

            if (pointerInteractable != null)
            {
                SetSelectedAsset(pointerInteractable);
                SetPromptTarget(pointerInteractable, null, pointerInteractable.GetInteractTxT());
                return;
            }

            if (pointerSceneObject != null)
            {
                SetSelectedAsset(null);
                SetPromptTarget(null, pointerSceneObject, ResolveSceneObjectPromptText(pointerSceneObject));
                return;
            }
        }

        if (Physics.Raycast(transform.position, lastInteractDirection, out RaycastHit hit, interactionDistance, interactableLayerMask)
            && hit.transform.TryGetComponent(out InteractableAsset interactableAsset))
        {
            SetSelectedAsset(interactableAsset);
            SetPromptTarget(interactableAsset, null, interactableAsset.GetInteractTxT());
            return;
        }

        if (TryGetDirectionalPickupCandidateInRange(out SceneObject directionalSceneObject) && directionalSceneObject != null)
        {
            SetSelectedAsset(null);
            SetPromptTarget(null, directionalSceneObject, ResolveSceneObjectPromptText(directionalSceneObject));
        }
        else
        {
            SetSelectedAsset(null);
            SetPromptTarget(null, null, null);
        }
    }

    public InteractableAsset GetSelected() => selectedAsset;
    public InteractableAsset GetPromptAsset() => promptAsset;
    public SceneObject GetPromptSceneObject() => promptSceneObject;


    private void RequestPickup()
    {
        var itemNet = player.GetComponent<PlayerItemNet>();
        if (itemNet == null)
        {
            Debug.LogError("PlayerItemNet missing on Player.");
            return;
        }

        itemNet.RequestPickupNearest(pickupDistance, interactableObjectsLayerMask);
        //tutorialEvents?.Raise(TutorialEventType.PickedUpItem);
    }

    private bool TryPickupPointedObject()
    {
        if (!TryGetPointerPickupCandidateInRange(out SceneObject pointedObject) || pointedObject == null)
            return false;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.HasFreeSlot)
            return false;

        var itemNet = player.GetComponent<PlayerItemNet>();
        if (itemNet == null)
        {
            Debug.LogError("PlayerItemNet missing on Player.");
            return false;
        }

        return itemNet.RequestPickupSpecific(pointedObject, pickupDistance);
    }

    private bool TryGetPointerTarget(
        out InteractableAsset pointedInteractable,
        out SceneObject pointedObject,
        out Vector3 flatDirection)
    {
        pointedInteractable = null;
        pointedObject = null;
        flatDirection = Vector3.zero;

        if (!TryGetPointerRay(out Ray pointerRay))
            return false;

        if (!Physics.Raycast(pointerRay, out RaycastHit hit, pointerRayMaxDistance, _combinedPointerMask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.transform.TryGetComponent(out InteractableAsset interactable))
        {
            Vector3 toTarget = hit.point - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude <= interactionDistance * interactionDistance)
            {
                pointedInteractable = interactable;
                flatDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.zero;
                return true;
            }
        }

        SceneObject sceneObject = hit.collider.GetComponentInParent<SceneObject>();
        if (sceneObject == null || !sceneObject.CanBePickedUp())
            return false;

        Vector3 center = player.transform.position + Vector3.up * 0.5f;
        float maxDistSq = pickupDistance * pickupDistance;
        if ((sceneObject.transform.position - center).sqrMagnitude > maxDistSq)
            return false;

        pointedObject = sceneObject;
        Vector3 toPickup = sceneObject.transform.position - transform.position;
        toPickup.y = 0f;
        flatDirection = toPickup.sqrMagnitude > 0.0001f ? toPickup.normalized : Vector3.zero;
        return true;
    }

    private bool TryGetPointerPickupCandidateInRange(out SceneObject pointedObject)
    {
        pointedObject = null;
        if (!TryGetPointerTarget(out _, out SceneObject candidate, out _))
            return false;
        pointedObject = candidate;
        return pointedObject != null;
    }

    private bool TryGetDirectionalPickupCandidateInRange(out SceneObject candidate)
    {
        candidate = null;
        if (lastInteractDirection.sqrMagnitude <= 0.0001f)
            return false;

        if (!Physics.Raycast(transform.position, lastInteractDirection, out RaycastHit hit, pickupDistance, interactableObjectsLayerMask, QueryTriggerInteraction.Ignore))
            return false;

        candidate = hit.collider.GetComponentInParent<SceneObject>();
        if (candidate == null || !candidate.CanBePickedUp())
            return false;

        Vector3 center = player.transform.position + Vector3.up * 0.5f;
        float maxDistSq = pickupDistance * pickupDistance;
        return (candidate.transform.position - center).sqrMagnitude <= maxDistSq;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private bool TryGetPointerRay(out Ray pointerRay)
    {
        pointerRay = default;
        if (Mouse.current == null) return false;

        Camera cam = _cachedCamera != null ? _cachedCamera : PlayerEvents.LocalGameplayCamera;
        if (cam == null)
            cam = Camera.main;
        if (cam == null)
            return false;

        _cachedCamera = cam;
        pointerRay = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        return true;
    }

    private void RequestDrop()
    {
        var itemNet = player.GetComponent<PlayerItemNet>();
        if (itemNet == null)
        {
            Debug.LogError("PlayerItemNet missing on Player.");
            return;
        }

        Vector3 dropPos = itemNet.GetLocalSuggestedDropPosition(player.transform);
        itemNet.RequestDrop(dropPos);
        tutorialEvents?.Raise(TutorialEventType.DroppedItem);
    }

    private static string ResolveSceneObjectPromptText(SceneObject sceneObject)
    {
        if (sceneObject == null)
            return null;

        SceneObjectSO sceneObjectSO = sceneObject.GetSceneObjectSO();
        if (sceneObjectSO == null)
            return "Pick up";

        return sceneObjectSO.GetWorldInteractPromptText();
    }

    private void SetPromptTarget(InteractableAsset asset, SceneObject sceneObject, string nextPromptText)
    {
        if (promptAsset == asset
            && promptSceneObject == sceneObject
            && string.Equals(promptText, nextPromptText, StringComparison.Ordinal))
        {
            return;
        }

        promptAsset = asset;
        promptSceneObject = sceneObject;
        promptText = nextPromptText;

        OnPromptTargetChanged?.Invoke(this, new OnPromptTargetChangedEventArgs
        {
            promptText = promptText,
            selectedAsset = promptAsset,
            selectedSceneObject = promptSceneObject
        });
    }
}