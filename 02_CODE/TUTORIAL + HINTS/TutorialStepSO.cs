using UnityEngine;

[CreateAssetMenu(menuName = "Game/Tutorial/Step")]
public class TutorialStepSO : ScriptableObject
{
    public string stepId;
    public string requiredPayloadId;

    public TutorialStepSO[] prerequisites;
    public NarratorLineSO[] startLines;
    public NarratorLineSO[] hintLines;

    public TutorialEventType completesOn;
    public float secondsBeforeHint = 8f;

    [Header("Extra completion conditions (AND)")]
    public bool requireMovement;
    public bool requireNarrationFinishedForThisStep;
    public bool requirePickup;
    public bool requireLookAtInteractable;
    public bool requirePaymentPhase;

    [Header("Pointer (optional)")]
    public string pointerTargetId;

    [Header("Reminder Loop (optional)")]
    public NarratorLineSO[] reminderLoopLines;
    public float reminderIntervalSeconds = 10f;

    [Header("Activation Side Effects")]
    [Tooltip("If true, when this step activates, TutorialDirector will request one table cycle start.")]
    public bool triggerTableOrderOnActivate = false;

    [Tooltip("Optional explicit table display name (TableIdentity.DisplayName). If empty, requiredPayloadId is used.")]
    public string targetTableDisplayName;

    [Header("Step Action")]
    [Tooltip("Optional action executed when this step is completed.")]
    public TutorialStepAction onStepCompletedAction = TutorialStepAction.None;

    [Tooltip("Optional delay before executing action (seconds).")]
    public float actionDelaySeconds = 0f;

    [Header("Memory Policy")]
    [Tooltip("If true, this step can be satisfied by previously completed events/facts.")]
    public bool allowPersistentMemoryForMainTrigger = true;

    [Tooltip("If true, extra AND conditions can be satisfied by previously completed events/facts.")]
    public bool allowPersistentMemoryForExtraConditions = true;

    [Header("Current-State Checks")]
    [Tooltip("If true and this step completes on PickedUpItem with requiredPayloadId, step can auto-satisfy if local player is already holding that item.")]
    public bool allowCurrentStateForPickup = true;

    [Header("Graph Transitions (optional)")]
    [Tooltip("If set, these edges define next step selection (non-linear). If empty, legacy sequence order is used.")]
    public TutorialStepLink[] nextLinks;

    [Header("NPC Action (optional)")]
    public bool executeNpcActionOnActivate = false;
    public TutorialNpcActionData npcAction;
}