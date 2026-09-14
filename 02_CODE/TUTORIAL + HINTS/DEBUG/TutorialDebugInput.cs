using UnityEngine;

public class TutorialDebugInput : MonoBehaviour
{
    [SerializeField] private TutorialEventChannelSO events;

    private void Update()
    {
        if (!TutorialRuntimeContext.IsTutorialRun) return;
        if (events == null) return;
        
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("[TutorialDebug] MovedFirstTime");
            events.Raise(TutorialEventType.MovedFirstTime);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("[TutorialDebug] LookedAtInteractable");
            events.Raise(TutorialEventType.LookedAtInteractable);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("[TutorialDebug] PickedUpItem");
            events.Raise(TutorialEventType.PickedUpItem);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Debug.Log("[TutorialDebug] WorkStarted");
            events.Raise(TutorialEventType.WorkStarted);
        }

        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            Debug.Log("[TutorialDebug] PaymentPhaseStarted");
            events.Raise(TutorialEventType.PaymentPhaseStarted);
        }

        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            Debug.Log("[TutorialDebug] PaymentAttemptFailed");
            events.Raise(TutorialEventType.PaymentAttemptFailed);
        }

        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            Debug.Log("[TutorialDebug] PaymentSucceeded");
            events.Raise(TutorialEventType.PaymentSucceeded);
        }
    }
}