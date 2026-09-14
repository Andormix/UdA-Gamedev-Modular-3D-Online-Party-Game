using UnityEngine;

public class WorkstationBVisuals : MonoBehaviour
{
    [SerializeField] private GameObject readyIndicator;
    [SerializeField] private GameObject deliverIndicator;
    [SerializeField] private GameObject consumingIndicator;
    [SerializeField] private GameObject paymentIndicator;
    [SerializeField] private GameObject cleanupIndicator;

    public void Apply(WorkstationPhaseId phase, bool isReadyFlag)
    {
        if (readyIndicator != null)
            readyIndicator.SetActive(phase == WorkstationPhaseId.ReadyToRequest && isReadyFlag);

        if (deliverIndicator != null)
            deliverIndicator.SetActive(phase == WorkstationPhaseId.DeliverItems);

        if (consumingIndicator != null)
            consumingIndicator.SetActive(phase == WorkstationPhaseId.Consuming);

        if (paymentIndicator != null)
            paymentIndicator.SetActive(phase == WorkstationPhaseId.AwaitingPayment);

        if (cleanupIndicator != null)
            cleanupIndicator.SetActive(phase == WorkstationPhaseId.NeedsCleanup);
    }
}