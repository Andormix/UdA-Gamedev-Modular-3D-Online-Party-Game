using System;
using System.Collections.Generic;

public interface IWorkstationContext
{
    // Events / UI
    void PushProgress(float normalized);
    void NotifyRequiredChanged();

    // Order + requirements
    RecipeOrder ActiveOrder { get; set; }
    List<SceneObjectSO> RemainingRequired { get; }
    List<SceneObjectSO> ThrownObjects { get; }
    bool HasActiveCommand { get; set; }

    // Readiness flag
    bool IsOrderRequestReady { get; set; }

    // Services
    IOrderService Orders { get; }

    // Config values
    float ReadyWindowSeconds { get; }
    float DeliverItemsSeconds { get; }
    float ConsumeSeconds { get; }
    float PaymentSeconds { get; }

    // State transitions
    void ChangeState(WorkstationPhaseId next);
    void ResetWorkstation();
    void ApplyVisuals(WorkstationPhaseId phase);

    // Pricing
    int CurrentOrderCoinTotal { get; set; }
    public bool ReadyToPayBonusGiven { get; set; }
    double PaymentPhaseStartTime { get; set; }
    bool PaymentAttempted { get; set; }
}