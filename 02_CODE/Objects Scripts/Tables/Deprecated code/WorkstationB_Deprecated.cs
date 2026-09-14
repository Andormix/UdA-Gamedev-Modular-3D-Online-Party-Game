using System;
using System.Collections.Generic;
using UnityEngine;

public class WorkstationB_Deprecated : InteractableAsset, InterfaceProgressBar
{
    public event EventHandler<InterfaceProgressBar.OnProgressChangedEventArgs> OnProgressChanged;
    public event EventHandler OnThrownElement;
    private enum Phase
    {
        Idle,
        ReadyToRequest,
        DeliverItems,
        Consuming,
        AwaitingPayment,
    }

    [Header("Scheduler / Ready Window")]
    [SerializeField] private bool isOrderRequestReady;              // Set by scheduler
    [SerializeField] private float readyWindowSeconds = 8f;         // X seconds to request a new order

    [Header("Order Phase Timers")]
    [SerializeField] private float deliverItemsSeconds = 10f;       // W seconds to deliver ingredients (fail if expires)
    [SerializeField] private float consumeSeconds = 1.5f;           // Z seconds consuming/processing after all delivered
    [SerializeField] private float paymentSeconds = 2f;             // Y seconds to take payment (fail if expires)

    [Header("Debug Indicators (Optional)")]
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject deliverIndicator;
    [SerializeField] private GameObject consumingIndicator;
    [SerializeField] private GameObject paymentIndicator;

    // Current phase
    private Phase phase = Phase.Idle;

    // Timers for each phase
    private float readyTimeRemaining;
    private float deliverTimeRemaining;
    private float consumeTimeRemaining;
    private float paymentTimeRemaining;

    // Active order and remaining requirements
    private RecipeOrder activeOrder;
    private readonly List<SceneObjectSO> remainingRequired = new();

    // Thrown items tracking (as you already had)
    private readonly List<SceneObjectSO> thrownSceneObjects = new();

    // You used this as "has order been requested / active flow"
    private bool hasActiveCommand;


    // Initialize lists and apply indicator state.
    private void Awake()
    {
        ApplyPhaseVisuals();
    }

    // Called by the scheduler: toggles the "ready to request order" window.
    // If an order is already active, it ignores readiness.
    public void SetOrderRequestReady(bool ready)
    {
        if (ready && (hasActiveCommand || activeOrder != null))
            return;

        isOrderRequestReady = ready;

        if (ready)
        {
            // Enter ready-to-request phase
            phase = Phase.ReadyToRequest;
            readyTimeRemaining = readyWindowSeconds;
            PushProgress(1f);
        }
        else
        {
            // Leave ready-to-request phase (only if we are currently in it)
            if (phase == Phase.ReadyToRequest)
                phase = Phase.Idle;

            readyTimeRemaining = 0f;
            PushProgress(0f);
        }

        ApplyPhaseVisuals();
    }

    // Exposed helper for other systems.
    public bool IsOrderRequestReady() => isOrderRequestReady;


    // Updates visuals based on the current phase.
    private void ApplyPhaseVisuals()
    {
        if (readyIndicator != null)
            readyIndicator.SetActive(phase == Phase.ReadyToRequest && isOrderRequestReady);

        if (deliverIndicator != null)
            deliverIndicator.SetActive(phase == Phase.DeliverItems);

        if (consumingIndicator != null)
            consumingIndicator.SetActive(phase == Phase.Consuming);

        if (paymentIndicator != null)
            paymentIndicator.SetActive(phase == Phase.AwaitingPayment);
    }


    // Drives the currently active phase timer.
    private void Update()
    {
        switch (phase)
        {
            case Phase.ReadyToRequest:
                TickReadyWindow();
                break;

            case Phase.DeliverItems:
                TickDeliverPhase();
                break;

            case Phase.Consuming:
                TickConsumePhase();
                break;

            case Phase.AwaitingPayment:
                TickPaymentPhase();
                break;
        }
    }

    // Ready window countdown. If time expires, table is no longer ready.
    private void TickReadyWindow()
    {
        if (!isOrderRequestReady)
        {
            phase = Phase.Idle;
            PushProgress(0f);
            ApplyPhaseVisuals();
            return;
        }

        readyTimeRemaining -= Time.deltaTime;

        float normalized = Mathf.Clamp01(readyTimeRemaining / readyWindowSeconds);
        PushProgress(normalized);

        if (readyTimeRemaining <= 0f)
        {
            isOrderRequestReady = false;
            readyTimeRemaining = 0f;

            phase = Phase.Idle;
            PushProgress(0f);
            ApplyPhaseVisuals();
        }
    }

    // Deliver phase countdown. If time expires before all required items are delivered -> fail.
    // Also syncs RecipeOrder.remainingTime/duration so the global order UI stays correct.
    private void TickDeliverPhase()
    {
        deliverTimeRemaining -= Time.deltaTime;

        if (activeOrder != null)
            RecipeManager.Instance.SetOrderTimer(activeOrder.id, deliverTimeRemaining, deliverItemsSeconds);

        float normalized = Mathf.Clamp01(deliverTimeRemaining / deliverItemsSeconds);
        PushProgress(normalized);

        if (deliverTimeRemaining <= 0f)
            FailActiveOrder();
    }

    // Consume/processing phase countdown. When finished, transitions into payment phase.
    // Also syncs RecipeOrder timer values.
    private void TickConsumePhase()
    {
        consumeTimeRemaining -= Time.deltaTime;

        if (activeOrder != null)
            RecipeManager.Instance.SetOrderTimer(activeOrder.id, consumeTimeRemaining, consumeSeconds);

        float normalized = Mathf.Clamp01(consumeTimeRemaining / consumeSeconds);
        PushProgress(normalized);

        if (consumeTimeRemaining <= 0f)
            BeginPaymentPhase();
    }

    // Payment phase countdown. If player does NOT interact before it ends fail.
    // Also syncs RecipeOrder timer values.
    private void TickPaymentPhase()
    {
        paymentTimeRemaining -= Time.deltaTime;

        if (activeOrder != null)
            RecipeManager.Instance.SetOrderTimer(activeOrder.id, paymentTimeRemaining, paymentSeconds);

        float normalized = Mathf.Clamp01(paymentTimeRemaining / paymentSeconds);
        PushProgress(normalized);

        if (paymentTimeRemaining <= 0f)
            FailActiveOrder();
    }

    /// Sends 0..1 progress to any UI ProgressBarUI listening to this workstation.
    private void PushProgress(float normalized)
    {
        OnProgressChanged?.Invoke(this, new InterfaceProgressBar.OnProgressChangedEventArgs
        {
            progressNormalized = normalized
        });
    }

    // Player interaction:
    // - If awaiting payment: interacting completes immediately.
    // - If ready to request: interacting requests and assigns an order.
    // - If delivering items: interacting does nothing (throwing happens via holding an object and calling Interact).
    public override void Interact(Player player)
    {
        // During consume phase, ignore player input.
        if (phase == Phase.Consuming)
            return;

        // During payment phase, interaction completes immediately.
        if (phase == Phase.AwaitingPayment)
        {
            CompleteActiveOrder();
            return;
        }

        // Take a new order during ready window.
        if (!hasActiveCommand && isOrderRequestReady)
        {
            // Consume readiness immediately.
            isOrderRequestReady = false;
            readyTimeRemaining = 0f;
            phase = Phase.Idle;
            PushProgress(0f);
            ApplyPhaseVisuals();

            // Request order.
            if (RequestAndAssignOrder())
                hasActiveCommand = true;

            return;
        }

        // Deliver/throw item behavior (only while we have an active command/order and are delivering items).
        if (!hasActiveCommand)
            return;

        if (phase != Phase.DeliverItems)
            return;

        if (!player.HasSceneObject() || !player.GetSceneObject().IsFinalObject())
            return;

        SceneObjectSO thrownSO = player.GetSceneObject().GetSceneObjectSO();
        player.GetSceneObject().DeleteObject();

        thrownSceneObjects.Add(thrownSO);

        if (activeOrder == null)
            return;

        int idx = remainingRequired.FindIndex(x => x == thrownSO);
        if (idx < 0)
        {
            Debug.Log("This item is not required for the active order.");
            OnThrownElement?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Consume one required ingredient.
        remainingRequired.RemoveAt(idx);
        OnThrownElement?.Invoke(this, EventArgs.Empty);

        // If all delivered, transition to consume phase.
        if (remainingRequired.Count == 0)
            BeginConsumePhase();
    }


    // Returns the list of thrown objects.
    public List<SceneObjectSO> GetTrownSceneObjectsArraySO() => thrownSceneObjects;

    // Clears thrown items and refreshes required UI listeners.
    public void Clear()
    {
        thrownSceneObjects.Clear();
        OnThrownElement?.Invoke(this, EventArgs.Empty);
    }


    // Returns the currently remaining required ingredients for RequiredElementsUI.
    public IReadOnlyList<SceneObjectSO> GetRemainingRequiredSceneObjectsSO() => remainingRequired;

    private bool RequestAndAssignOrder()
    {
        RecipeOrder order = RecipeManager.Instance.RequestNewOrder();
        if (order == null)
        {
            Debug.Log("Order request denied (max active orders).");
            return false;
        }

        activeOrder = order;
        activeOrder.assignedToWorkstation = true;

        // Setup requirements
        remainingRequired.Clear();
        remainingRequired.AddRange(order.recipe.sceneObjectSOList);
        OnThrownElement?.Invoke(this, EventArgs.Empty);

        // Begin deliver phase
        deliverTimeRemaining = deliverItemsSeconds;
        phase = Phase.DeliverItems;
        PushProgress(1f);

        // Sync order timer for global UI
        RecipeManager.Instance.SetOrderTimer(activeOrder.id, deliverTimeRemaining, deliverItemsSeconds);

        ApplyPhaseVisuals();
        return true;
    }


    // Convenience: indicates if the table has any active work (order assigned or command started).
    public bool IsBusy() => hasActiveCommand || activeOrder != null;

    // Transition: all items delivered -> consume/processing delay.
    private void BeginConsumePhase()
    {
        if (activeOrder == null) return;

        consumeTimeRemaining = consumeSeconds;
        phase = Phase.Consuming;
        PushProgress(1f);

        RecipeManager.Instance.SetOrderTimer(activeOrder.id, consumeTimeRemaining, consumeSeconds);
        ApplyPhaseVisuals();
    }

    // Transition: consume done -> start payment window.
    private void BeginPaymentPhase()
    {
        if (activeOrder == null) return;

        paymentTimeRemaining = paymentSeconds;
        phase = Phase.AwaitingPayment;
        PushProgress(1f);

        RecipeManager.Instance.SetOrderTimer(activeOrder.id, paymentTimeRemaining, paymentSeconds);
        ApplyPhaseVisuals();
    }

    // Completes the active order successfully (increments success count via RecipeManager.CompleteOrder).
    // Resets the workstation.
    private void CompleteActiveOrder()
    {
        if (activeOrder != null)
            RecipeManager.Instance.CompleteOrder(activeOrder.id);

        ResetWorkstationState();
    }

    // Fails the active order (does NOT increment success count; uses RecipeManager.RemoveOrder).
    // Resets the workstation.
    private void FailActiveOrder()
    {
        if (activeOrder != null)
            RecipeManager.Instance.RemoveOrder(activeOrder.id);

        ResetWorkstationState();
    }

    // Resets all internal state back to idle and clears visuals/UI.
    private void ResetWorkstationState()
    {
        hasActiveCommand = false;
        activeOrder = null;

        remainingRequired.Clear();

        phase = Phase.Idle;
        PushProgress(0f);

        Clear(); // clears thrown list + triggers UI refresh
        ApplyPhaseVisuals();
    }
}