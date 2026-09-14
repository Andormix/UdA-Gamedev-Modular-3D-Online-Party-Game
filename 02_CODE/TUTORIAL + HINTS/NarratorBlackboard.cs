using System.Collections.Generic;

public sealed class NarratorBlackboard
{
    public string LevelId;
    public bool IsTutorialActive;

    public HashSet<string> CompletedSteps = new();
    public HashSet<string> ShownLines = new();

    // legacy booleans still useful for hints
    public bool HasMoved;
    public bool HasLookedAtInteractable;
    public bool HasPickedUp;

    public bool IsInPaymentPhase;
    public int PaymentFailCount;

    // persistent event memory (global across steps)
    public bool EverMoved;
    public bool EverLookedAtInteractable;
    public bool EverPickedUpItem;
    public bool EverDroppedItem;
    public bool EverWorkStarted;
    public bool EverWorkStopped;
    public bool EverPriceBoardOpened;
    public bool EverOrderAccepted;
    public bool EverDeliveredRequiredItem;
    public bool EverAllItemsDelivered;
    public bool EverPaymentPhaseStarted;
    public bool EverPaymentAttemptFailed;
    public bool EverPaymentSucceeded;
    public bool EverOrderFailedTimeout;
    public bool EverDeliverItemsTimeout;
    public bool EverSinkItemDeposited;
    public bool EverSinkItemCleaned;

    // payload-based persistent memory
    // e.g. "PickedUpItem|CoffeeCup", "ReachedTutorialTarget|SinkZoneA"
    public HashSet<string> PersistentFacts = new();

    public void Remember(TutorialEventType type, object payload)
    {
        // global booleans
        switch (type)
        {
            case TutorialEventType.MovedFirstTime: EverMoved = true; break;
            case TutorialEventType.LookedAtInteractable: EverLookedAtInteractable = true; break;
            case TutorialEventType.PickedUpItem: EverPickedUpItem = true; break;
            case TutorialEventType.DroppedItem: EverDroppedItem = true; break;
            case TutorialEventType.WorkStarted: EverWorkStarted = true; break;
            case TutorialEventType.WorkStopped: EverWorkStopped = true; break;
            case TutorialEventType.PriceBoardOpened: EverPriceBoardOpened = true; break;
            case TutorialEventType.OrderAccepted: EverOrderAccepted = true; break;
            case TutorialEventType.DeliveredRequiredItem: EverDeliveredRequiredItem = true; break;
            case TutorialEventType.AllItemsDelivered: EverAllItemsDelivered = true; break;
            case TutorialEventType.PaymentPhaseStarted: EverPaymentPhaseStarted = true; break;
            case TutorialEventType.PaymentAttemptFailed: EverPaymentAttemptFailed = true; break;
            case TutorialEventType.PaymentSucceeded: EverPaymentSucceeded = true; break;
            case TutorialEventType.OrderFailedTimeout: EverOrderFailedTimeout = true; break;
            case TutorialEventType.DeliverItemsTimeout: EverDeliverItemsTimeout = true; break;
            case TutorialEventType.SinkItemDeposited: EverSinkItemDeposited = true; break;
            case TutorialEventType.SinkItemCleaned: EverSinkItemCleaned = true; break;
        }

        // fact key with optional payload
        string payloadStr = payload as string;
        if (string.IsNullOrWhiteSpace(payloadStr))
            PersistentFacts.Add(type.ToString());
        else
            PersistentFacts.Add($"{type}|{payloadStr}");
    }

    public bool HasFact(TutorialEventType type) =>
        PersistentFacts.Contains(type.ToString());

    public bool HasFact(TutorialEventType type, string payloadId)
    {
        if (string.IsNullOrWhiteSpace(payloadId))
            return HasFact(type);

        return PersistentFacts.Contains($"{type}|{payloadId}");
    }
}