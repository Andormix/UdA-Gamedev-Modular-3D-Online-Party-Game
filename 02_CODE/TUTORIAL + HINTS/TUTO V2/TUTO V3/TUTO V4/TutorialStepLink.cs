using System;
using UnityEngine;

public enum TutorialLinkConditionMode
{
    Always = 0,     // unconditional fallback
    OnEvent = 1     // condition by event (+ optional payload)
}

[Serializable]
public class TutorialStepLink
{
    [Tooltip("Target step ID to jump to when this edge matches.")]
    public string targetStepId;

    [Header("Condition")]
    public TutorialLinkConditionMode conditionMode = TutorialLinkConditionMode.Always;

    [Tooltip("Used only when conditionMode=OnEvent.")]
    public TutorialEventType whenEvent = TutorialEventType.MovedFirstTime;

    [Tooltip("Optional payload match (exact). Leave empty for any payload.")]
    public string requiredPayloadId;

    [Tooltip("If true, condition checks persistent memory too (not only step-local signals).")]
    public bool allowPersistentMatch = true;

    [Tooltip("Higher priority wins when multiple edges match.")]
    public int priority = 0;
}