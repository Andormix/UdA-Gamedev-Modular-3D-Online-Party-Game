using UnityEngine;

[CreateAssetMenu(menuName = "Game/Scoring/Scoring Config")]
public class ScoringConfigSO : ScriptableObject
{
    [Header("Tips")]
    public int fastAcceptPoints = 50;
    public int readyToPayBonus = 30;
    public int deliverPerfectBase = 80;
    public int deliverStandardBase = 60;
    public int deliverClutchFlat = 120;

    [Header("Delivery Windows")]
    [Range(0f, 1f)] public float deliverPerfectThreshold = 0.30f;
    [Range(0f, 1f)] public float deliverStandardThreshold = 0.80f;

    [Header("Payment")]
    public float perfectPaymentMultiplier = 1.5f;
}