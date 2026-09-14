using UnityEngine;

public static class TutorialGameplayPolicy
{
    public static bool IsActive => TutorialRuntimeContext.IsTutorialRun;

    // Global match timer/game-over by time
    public static bool DisableGlobalMatchTimeout => IsActive;

    // Table state fail-on-expiry behavior
    public static bool DisableReadyWindowFailure => IsActive;
    public static bool DisableDeliverFailure => IsActive;
    public static bool DisablePaymentFailure => IsActive;

    // In tutorial, we want guidance loop instead of hard fail
    public static bool UseGuidanceInsteadOfFailure => IsActive;
}