public enum TutorialEventType
{
    PlayerSpawned,
    NarrationFinished,

    MovedFirstTime,
    LookedAtInteractable,
    ReachedTutorialTarget,
    PickedUpItem,
    DroppedItem,

    WorkStarted,
    WorkStopped,

    TableReadyToRequest,
    PriceBoardOpened,

    OrderAccepted,
    DeliveredRequiredItem,
    AllItemsDelivered,

    PaymentPhaseStarted,
    PaymentAttemptFailed,
    PaymentSucceeded,

    OrderFailedTimeout,
    DeliverItemsTimeout,
    
    SinkItemDeposited,
    SinkItemCleaned
}