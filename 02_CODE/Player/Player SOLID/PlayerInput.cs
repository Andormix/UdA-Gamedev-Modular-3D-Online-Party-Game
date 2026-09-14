using System;
using Unity.Netcode;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    private PlayerInputActions playerInputActions;

    public event EventHandler OnInteractAction;
    public event EventHandler OnPauseAction;
    public event EventHandler OnDropAction;
    public event EventHandler OnInteractStarted;
    public event EventHandler OnInteractCanceled;
    public event EventHandler OnDropStarted;
    public event EventHandler OnDropCanceled;

    private void Awake()
    {
        playerInputActions = new PlayerInputActions();

        // Hook callbacks once
        playerInputActions.Player.Interact.performed += Interact_performed;
        playerInputActions.Player.Drop.performed += Drop_performed;
        playerInputActions.Player.Pause.performed += Pause_performed;
        playerInputActions.Player.Interact.started += Interact_started;
        playerInputActions.Player.Interact.canceled += Interact_canceled;
        playerInputActions.Player.Drop.started += Drop_started;
        playerInputActions.Player.Drop.canceled += Drop_canceled;
    }

    private void Interact_started(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    => OnInteractStarted?.Invoke(this, EventArgs.Empty);

    private void Interact_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    => OnInteractCanceled?.Invoke(this, EventArgs.Empty);

    private void Drop_started(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    => OnDropStarted?.Invoke(this, EventArgs.Empty);

    private void Drop_canceled(UnityEngine.InputSystem.InputAction.CallbackContext obj)
    => OnDropCanceled?.Invoke(this, EventArgs.Empty);

    private void OnEnable()
    {
        playerInputActions.Player.Enable();
    }

    private void OnDisable()
    {
        playerInputActions.Player.Disable();
    }

    private void OnDestroy()
    {
        // Unhook
        playerInputActions.Player.Interact.performed -= Interact_performed;
        playerInputActions.Player.Drop.performed -= Drop_performed;
        playerInputActions.Player.Pause.performed -= Pause_performed;
        playerInputActions.Player.Interact.started -= Interact_started;
        playerInputActions.Player.Interact.canceled -= Interact_canceled;
        playerInputActions.Player.Drop.started -= Drop_started;
        playerInputActions.Player.Drop.canceled -= Drop_canceled;

        playerInputActions.Dispose();
    }

    private void Pause_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
        => OnPauseAction?.Invoke(this, EventArgs.Empty);

    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
        => OnInteractAction?.Invoke(this, EventArgs.Empty);

    private void Drop_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj)
        => OnDropAction?.Invoke(this, EventArgs.Empty);

    public Vector2 GetMovementVectorNorm()
    {
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();
        return inputVector.normalized;
    }

    public void EnableControls() => playerInputActions.Player.Enable();
    public void DisableControls() => playerInputActions.Player.Disable();
}