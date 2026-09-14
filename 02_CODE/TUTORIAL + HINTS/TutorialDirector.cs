using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public sealed class TutorialDirector : MonoBehaviour
{
    [SerializeField] private TutorialEventChannelSO events;
    [SerializeField] private TutorialSettingsSO settings;
    [SerializeField] private TutorialSequenceSO sequence;
    [SerializeField] private NarratorPresenter narratorUI;
    [SerializeField] private VoiceNarrator voice;

    [Header("Pointers")]
    [SerializeField] private TutorialPointerRegistry pointerRegistry;

    [Header("Tutorial Runtime Hooks")]
    [SerializeField] private WorkstationOrderScheduler scheduler;

    [Header("NPC Runtime Hooks")]
    [SerializeField] private TutorialNpcDirector npcDirector;

    private const int MaxImmediateAutoTransitions = 12;

    private int immediateTransitionDepth;
    private NarratorBlackboard bb;
    private NarratorBrain brain;
    private TutorialStepSO current;
    private float stepStartTime;

    private float nextReminderAt;
    private int reminderLoopIndex;
    private bool endingTutorial;
    private bool transitioningToNextStep;

    private Dictionary<string, TutorialStepSO> stepMap;
    private string forcedNextStepId;

    // per-step local state
    private bool stepMoved;
    private bool stepPickedUpAny;
    private bool stepLookedAtAny;
    private bool stepNarrationFinished;
    private bool stepPaymentPhaseStarted;
    private bool stepOrderAccepted;
    private bool stepDeliveredRequiredItem;
    private bool stepAllItemsDelivered;
    private bool stepPaymentAttemptFailed;
    private bool stepPaymentSucceeded;
    private bool stepOrderFailedTimeout;
    private bool stepDeliverItemsTimeout;

    private bool stepPriceBoardOpened;
    private bool stepPriceBoardOpenedRequiredPayload;

    // per-step payload matches
    private bool stepPickedRequiredPayload;
    private bool stepLookedRequiredPayload;
    private bool stepReachedRequiredTarget;
    private bool stepOrderAcceptedRequiredPayload;
    private bool stepPaymentPhaseStartedRequiredPayload;
    private bool stepDeliveredRequiredItemRequiredPayload;
    private bool stepAllItemsDeliveredRequiredPayload;
    private bool stepPaymentAttemptFailedRequiredPayload;
    private bool stepPaymentSucceededRequiredPayload;
    private bool stepOrderFailedTimeoutRequiredPayload;
    private bool stepDeliverItemsTimeoutRequiredPayload;

    private bool stepSinkItemDeposited;
    private bool stepSinkItemDepositedRequiredPayload;
    private bool stepSinkItemCleaned;
    private bool stepSinkItemCleanedRequiredPayload;

    private Player _cachedLocalPlayer;
    private PlayerInput _cachedLocalPlayerInput;

    private void Awake()
    {
        bb = new NarratorBlackboard
        {
            LevelId = sequence != null ? sequence.sequenceId : "Map_01"
        };
        brain = new NarratorBrain();

        stepMap = sequence != null ? sequence.BuildStepMap() : new Dictionary<string, TutorialStepSO>();
        sequence?.ValidateGraph();
    }

    private void OnEnable()
    {
        if (events != null) events.OnEvent += OnEvent;
        if (voice != null) voice.OnVoiceFinished += Voice_OnVoiceFinished;
    }

    private void OnDisable()
    {
        if (events != null) events.OnEvent -= OnEvent;
        if (voice != null) voice.OnVoiceFinished -= Voice_OnVoiceFinished;
    }

    private void TryCacheLocalPlayerInput()
    {
        if (_cachedLocalPlayer != null && _cachedLocalPlayer && _cachedLocalPlayer.IsOwner)
            return;

        _cachedLocalPlayer = FindFirstObjectByType<Player>();
        if (_cachedLocalPlayer == null || !_cachedLocalPlayer.IsOwner)
        {
            _cachedLocalPlayer = null;
            _cachedLocalPlayerInput = null;
            return;
        }

        _cachedLocalPlayerInput = _cachedLocalPlayer.GetComponent<PlayerInput>();
    }

    private void Start()
    {
        bb.IsTutorialActive = ShouldRun();
        if (!bb.IsTutorialActive)
        {
            pointerRegistry?.HideAll();
            return;
        }

        ActivateNextStep();
        events?.Raise(TutorialEventType.PlayerSpawned);
    }

    private void Update()
    {
        if (!bb.IsTutorialActive || current == null) return;

        if (!stepMoved)
        {
            TryCacheLocalPlayerInput();
            if (_cachedLocalPlayerInput != null
                && _cachedLocalPlayerInput.GetMovementVectorNorm().sqrMagnitude > 0.0001f)
                stepMoved = true;
        }

        float elapsed = Time.time - stepStartTime;
        brain.Tick(bb, current, settings, narratorUI, voice, elapsed);
        HandleReminderLoop(elapsed);

        TryCompleteCurrentStep();
    }

    private void Voice_OnVoiceFinished(AudioClip clip)
    {
        if (!bb.IsTutorialActive || current == null) return;

        stepNarrationFinished = true;
        events?.Raise(TutorialEventType.NarrationFinished, current.stepId);
        TryCompleteCurrentStep();
    }

    private void OnEvent(TutorialEventType type, object payload)
    {
        if (!bb.IsTutorialActive || current == null) return;

        brain.ApplyEventToBlackboard(bb, type);
        bb.Remember(type, payload);

        bool hasReq = !string.IsNullOrWhiteSpace(current.requiredPayloadId);
        bool reqMatch = payload is string s && s == current.requiredPayloadId;

        switch (type)
        {
            case TutorialEventType.MovedFirstTime:
                stepMoved = true;
                break;

            case TutorialEventType.PickedUpItem:
                stepPickedUpAny = true;
                if (hasReq && reqMatch) stepPickedRequiredPayload = true;
                break;

            case TutorialEventType.LookedAtInteractable:
                stepLookedAtAny = true;
                if (hasReq && reqMatch) stepLookedRequiredPayload = true;
                break;

            case TutorialEventType.ReachedTutorialTarget:
                if (hasReq && reqMatch) stepReachedRequiredTarget = true;
                break;

            case TutorialEventType.OrderAccepted:
                stepOrderAccepted = true;
                if (hasReq && reqMatch) stepOrderAcceptedRequiredPayload = true;
                break;

            case TutorialEventType.PaymentPhaseStarted:
                stepPaymentPhaseStarted = true;
                if (hasReq && reqMatch) stepPaymentPhaseStartedRequiredPayload = true;
                break;

            case TutorialEventType.DeliveredRequiredItem:
                stepDeliveredRequiredItem = true;
                if (hasReq && reqMatch) stepDeliveredRequiredItemRequiredPayload = true;
                break;

            case TutorialEventType.AllItemsDelivered:
                stepAllItemsDelivered = true;
                if (hasReq && reqMatch) stepAllItemsDeliveredRequiredPayload = true;
                break;

            case TutorialEventType.PaymentAttemptFailed:
                stepPaymentAttemptFailed = true;
                if (hasReq && reqMatch) stepPaymentAttemptFailedRequiredPayload = true;
                break;

            case TutorialEventType.PaymentSucceeded:
                stepPaymentSucceeded = true;
                if (hasReq && reqMatch) stepPaymentSucceededRequiredPayload = true;
                break;

            case TutorialEventType.OrderFailedTimeout:
                stepOrderFailedTimeout = true;
                if (hasReq && reqMatch) stepOrderFailedTimeoutRequiredPayload = true;
                break;

            case TutorialEventType.DeliverItemsTimeout:
                stepDeliverItemsTimeout = true;
                if (hasReq && reqMatch) stepDeliverItemsTimeoutRequiredPayload = true;
                break;

            case TutorialEventType.NarrationFinished:
                if (payload is string stepId && stepId == current.stepId)
                    stepNarrationFinished = true;
                break;

            case TutorialEventType.PriceBoardOpened:
                stepPriceBoardOpened = true;
                if (hasReq && reqMatch) stepPriceBoardOpenedRequiredPayload = true;
                break;

            case TutorialEventType.SinkItemDeposited:
                stepSinkItemDeposited = true;
                if (hasReq && reqMatch) stepSinkItemDepositedRequiredPayload = true;
                break;

            case TutorialEventType.SinkItemCleaned:
                stepSinkItemCleaned = true;
                if (hasReq && reqMatch) stepSinkItemCleanedRequiredPayload = true;
                break;
        }

        if (TryBranchOnIncomingEvent(type, payload))
        return;
        TryCompleteCurrentStep();
    }

    private void TryCompleteCurrentStep()
    {
        if (current == null) return;
        if (endingTutorial) return;
        if (transitioningToNextStep) return;

        bool mainSatisfiedNow = IsMainTriggerSatisfied(current);
        if (!mainSatisfiedNow && current.allowPersistentMemoryForMainTrigger)
            mainSatisfiedNow = IsMainTriggerSatisfiedByPersistentMemory(current);

        if (!mainSatisfiedNow) return;

        bool extraSatisfiedNow = AreExtraConditionsSatisfied(current);
        if (!extraSatisfiedNow && current.allowPersistentMemoryForExtraConditions)
            extraSatisfiedNow = AreExtraConditionsSatisfiedByPersistentMemory(current);

        if (!extraSatisfiedNow) return;

        if (!bb.CompletedSteps.Contains(current.stepId))
            bb.CompletedSteps.Add(current.stepId);

        forcedNextStepId = ResolveNextStepIdAfterCompletion(current);

        if (current.onStepCompletedAction != TutorialStepAction.None)
        {
            ExecuteStepAction(current);
            return;
        }

        float delay = Mathf.Max(0f, current.actionDelaySeconds);
        if (delay > 0f)
            StartCoroutine(ActivateNextStepDelayed(delay));
        else
            ActivateNextStep();
    }

    private string ResolveNextStepIdAfterCompletion(TutorialStepSO step)
    {
        if (sequence == null || step == null) return null;
        if (!sequence.useGraph) return null;

        var links = step.nextLinks;
        if (links == null || links.Length == 0) return null;

        TutorialStepLink best = null;

        for (int i = 0; i < links.Length; i++)
        {
            var l = links[i];
            if (l == null || string.IsNullOrWhiteSpace(l.targetStepId)) continue;

            bool match = EvaluateLink(l);
            if (!match) continue;

            if (best == null || l.priority > best.priority)
                best = l;
        }

        return best != null ? best.targetStepId : null;
    }

    private bool EvaluateLink(TutorialStepLink link)
    {
        if (link == null) return false;

        if (link.conditionMode == TutorialLinkConditionMode.Always)
            return true;

        bool hasPayload = !string.IsNullOrWhiteSpace(link.requiredPayloadId);

        bool localMatch = link.whenEvent switch
        {
            TutorialEventType.MovedFirstTime => stepMoved,
            TutorialEventType.PickedUpItem => hasPayload ? stepPickedRequiredPayload : stepPickedUpAny,
            TutorialEventType.LookedAtInteractable => hasPayload ? stepLookedRequiredPayload : stepLookedAtAny,
            TutorialEventType.ReachedTutorialTarget => hasPayload ? stepReachedRequiredTarget : false,
            TutorialEventType.OrderAccepted => hasPayload ? stepOrderAcceptedRequiredPayload : stepOrderAccepted,
            TutorialEventType.PaymentPhaseStarted => hasPayload ? stepPaymentPhaseStartedRequiredPayload : stepPaymentPhaseStarted,
            TutorialEventType.DeliveredRequiredItem => hasPayload ? stepDeliveredRequiredItemRequiredPayload : stepDeliveredRequiredItem,
            TutorialEventType.AllItemsDelivered => hasPayload ? stepAllItemsDeliveredRequiredPayload : stepAllItemsDelivered,
            TutorialEventType.PaymentAttemptFailed => hasPayload ? stepPaymentAttemptFailedRequiredPayload : stepPaymentAttemptFailed,
            TutorialEventType.PaymentSucceeded => hasPayload ? stepPaymentSucceededRequiredPayload : stepPaymentSucceeded,
            TutorialEventType.OrderFailedTimeout => hasPayload ? stepOrderFailedTimeoutRequiredPayload : stepOrderFailedTimeout,
            TutorialEventType.DeliverItemsTimeout => hasPayload ? stepDeliverItemsTimeoutRequiredPayload : stepDeliverItemsTimeout,
            TutorialEventType.PriceBoardOpened => hasPayload ? stepPriceBoardOpenedRequiredPayload : stepPriceBoardOpened,
            TutorialEventType.SinkItemDeposited => hasPayload ? stepSinkItemDepositedRequiredPayload : stepSinkItemDeposited,
            TutorialEventType.SinkItemCleaned => hasPayload ? stepSinkItemCleanedRequiredPayload : stepSinkItemCleaned,
            TutorialEventType.NarrationFinished => stepNarrationFinished,
            _ => false
        };

        if (localMatch) return true;
        if (!link.allowPersistentMatch) return false;

        if (hasPayload)
            return bb.HasFact(link.whenEvent, link.requiredPayloadId);

        return bb.HasFact(link.whenEvent);
    }

    private bool IsMainTriggerSatisfied(TutorialStepSO step)
    {
        bool req = !string.IsNullOrWhiteSpace(step.requiredPayloadId);

        switch (step.completesOn)
        {
            case TutorialEventType.MovedFirstTime:
                return stepMoved;

            case TutorialEventType.PickedUpItem:
                return req ? stepPickedRequiredPayload : stepPickedUpAny;

            case TutorialEventType.LookedAtInteractable:
                return req ? stepLookedRequiredPayload : stepLookedAtAny;

            case TutorialEventType.ReachedTutorialTarget:
                return req && stepReachedRequiredTarget;

            case TutorialEventType.NarrationFinished:
                return stepNarrationFinished;

            case TutorialEventType.OrderAccepted:
                return req ? stepOrderAcceptedRequiredPayload : stepOrderAccepted;

            case TutorialEventType.PaymentPhaseStarted:
                return req ? stepPaymentPhaseStartedRequiredPayload : stepPaymentPhaseStarted;

            case TutorialEventType.DeliveredRequiredItem:
                return req ? stepDeliveredRequiredItemRequiredPayload : stepDeliveredRequiredItem;

            case TutorialEventType.AllItemsDelivered:
                return req ? stepAllItemsDeliveredRequiredPayload : stepAllItemsDelivered;

            case TutorialEventType.PaymentAttemptFailed:
                return req ? stepPaymentAttemptFailedRequiredPayload : stepPaymentAttemptFailed;

            case TutorialEventType.PaymentSucceeded:
                return req ? stepPaymentSucceededRequiredPayload : stepPaymentSucceeded;

            case TutorialEventType.OrderFailedTimeout:
                return req ? stepOrderFailedTimeoutRequiredPayload : stepOrderFailedTimeout;

            case TutorialEventType.DeliverItemsTimeout:
                return req ? stepDeliverItemsTimeoutRequiredPayload : stepDeliverItemsTimeout;

            case TutorialEventType.PriceBoardOpened:
                return req ? stepPriceBoardOpenedRequiredPayload : stepPriceBoardOpened;

            case TutorialEventType.SinkItemDeposited:
                return req ? stepSinkItemDepositedRequiredPayload : stepSinkItemDeposited;

            case TutorialEventType.SinkItemCleaned:
                return req ? stepSinkItemCleanedRequiredPayload : stepSinkItemCleaned;

            default:
                return false;
        }
    }

    private bool AreExtraConditionsSatisfied(TutorialStepSO step)
    {
        if (step.requireMovement && !stepMoved) return false;
        if (step.requirePickup && !stepPickedUpAny) return false;
        if (step.requireLookAtInteractable && !stepLookedAtAny) return false;
        if (step.requirePaymentPhase && !stepPaymentPhaseStarted) return false;
        if (step.requireNarrationFinishedForThisStep && !stepNarrationFinished) return false;
        return true;
    }

    private void ActivateNextStep()
    {
        transitioningToNextStep = false;

        if (sequence == null || sequence.steps == null)
        {
            current = null;
            pointerRegistry?.HideAll();
            narratorUI?.Hide();
            return;
        }

        current = SelectNextStep();
        forcedNextStepId = null;
        immediateTransitionDepth = 0;

        if (current == null)
        {
            TutorialRuntimeContext.MarkCompleted();
            pointerRegistry?.HideAll();
            narratorUI?.Hide();
            return;
        }

        stepMoved = false;
        stepPickedUpAny = false;
        stepLookedAtAny = false;
        stepNarrationFinished = false;
        stepPaymentPhaseStarted = false;
        stepOrderAccepted = false;
        stepDeliveredRequiredItem = false;
        stepAllItemsDelivered = false;
        stepPaymentAttemptFailed = false;
        stepPaymentSucceeded = false;
        stepOrderFailedTimeout = false;
        stepDeliverItemsTimeout = false;

        stepPriceBoardOpened = false;
        stepPriceBoardOpenedRequiredPayload = false;

        stepPickedRequiredPayload = false;
        stepLookedRequiredPayload = false;
        stepReachedRequiredTarget = false;
        stepOrderAcceptedRequiredPayload = false;
        stepPaymentPhaseStartedRequiredPayload = false;
        stepDeliveredRequiredItemRequiredPayload = false;
        stepAllItemsDeliveredRequiredPayload = false;
        stepPaymentAttemptFailedRequiredPayload = false;
        stepPaymentSucceededRequiredPayload = false;
        stepOrderFailedTimeoutRequiredPayload = false;
        stepDeliverItemsTimeoutRequiredPayload = false;

        stepSinkItemDeposited = false;
        stepSinkItemDepositedRequiredPayload = false;
        stepSinkItemCleaned = false;
        stepSinkItemCleanedRequiredPayload = false;

        if (current.completesOn == TutorialEventType.ReachedTutorialTarget &&
            !string.IsNullOrWhiteSpace(current.requiredPayloadId) &&
            TutorialTargetProximityEmitter.IsTargetReached(current.requiredPayloadId))
        {
            stepReachedRequiredTarget = true;
            bb.Remember(TutorialEventType.ReachedTutorialTarget, current.requiredPayloadId);
        }

        if (current.allowCurrentStateForPickup &&
            current.completesOn == TutorialEventType.PickedUpItem &&
            !string.IsNullOrWhiteSpace(current.requiredPayloadId))
        {
            if (IsLocalPlayerHoldingTutorialItem(current.requiredPayloadId))
            {
                stepPickedUpAny = true;
                stepPickedRequiredPayload = true;
                bb.Remember(TutorialEventType.PickedUpItem, current.requiredPayloadId);
                Debug.Log($"[TutorialDirector] Current-state pickup satisfied for payload '{current.requiredPayloadId}'");
            }
        }

        stepStartTime = Time.time;
        reminderLoopIndex = 0;
        nextReminderAt = Mathf.Max(1f, current.reminderIntervalSeconds);

        if (pointerRegistry != null)
        {
            if (string.IsNullOrWhiteSpace(current.pointerTargetId)) pointerRegistry.HideAll();
            else pointerRegistry.ShowOnly(current.pointerTargetId);
        }

        TryTriggerStepSideEffects(current);
        TryTriggerNpcStepAction(current);

        narratorUI?.Hide();
        brain.OnStepActivated(bb, current, settings, narratorUI, voice);

        if (immediateTransitionDepth < MaxImmediateAutoTransitions)
        {
            immediateTransitionDepth++;
            TryCompleteCurrentStep();
        }
        else
        {
            Debug.LogWarning("[TutorialDirector] Max immediate auto-transition depth reached. Check graph for loops.");
        }
    }

    private TutorialStepSO SelectNextStep()
    {
        if (sequence == null || sequence.steps == null) return null;

        if (!sequence.useGraph)
            return brain.SelectNextStep(bb, sequence.steps); // legacy

        if (!string.IsNullOrWhiteSpace(forcedNextStepId))
        {
            if (stepMap.TryGetValue(forcedNextStepId, out var forced) && forced != null)
                return forced;

            Debug.LogWarning($"[TutorialDirector] Graph forcedNextStepId '{forcedNextStepId}' not found.");
        }

        if (current == null)
        {
            if (!string.IsNullOrWhiteSpace(sequence.entryStepId) &&
                stepMap.TryGetValue(sequence.entryStepId, out var entry) && entry != null)
                return entry;

            return brain.SelectNextStep(bb, sequence.steps); // fallback
        }

        return brain.SelectNextStep(bb, sequence.steps); // fallback
    }

    private System.Collections.IEnumerator ActivateNextStepDelayed(float delay)
    {
        if (transitioningToNextStep) yield break;
        transitioningToNextStep = true;

        yield return new WaitForSecondsRealtime(delay);

        transitioningToNextStep = false;

        if (endingTutorial) yield break;
        ActivateNextStep();
    }

    private void HandleReminderLoop(float elapsed)
    {
        if (current == null) return;
        if (current.reminderLoopLines == null || current.reminderLoopLines.Length == 0) return;
        if (elapsed < nextReminderAt) return;

        float interval = Mathf.Max(1f, current.reminderIntervalSeconds);
        nextReminderAt = elapsed + interval;

        var line = current.reminderLoopLines[reminderLoopIndex % current.reminderLoopLines.Length];
        reminderLoopIndex++;
        if (line == null) return;

        if (settings.voiceEnabled && voice != null && line.voiceClip != null)
            voice.PlayVoice(line.voiceClip);

        if (narratorUI != null)
        {
            string text = line.text != null ? line.text.Get(settings.language) : "";
            bool persistent = line.kind == NarratorLineKind.Instruction;
            if (persistent) narratorUI.ForceShow(text, line.minShowSeconds, line.canInterrupt, true);
            else narratorUI.Show(text, line.minShowSeconds, line.canInterrupt, false);
        }
    }

    private bool ShouldRun()
    {
        if (!TutorialRuntimeContext.IsTutorialRun) return false;
        if (settings == null || !settings.tutorialEnabled) return false;

        bool isMultiplayer = NetworkManager.Singleton != null &&
                             NetworkManager.Singleton.IsListening &&
                             NetworkManager.Singleton.ConnectedClientsIds.Count > 1;

        if (isMultiplayer) return false;
        return settings.enableFullTutorialInSingleplayer;
    }

    private void TryTriggerStepSideEffects(TutorialStepSO step)
    {
        if (step == null) return;
        if (!step.triggerTableOrderOnActivate) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            Debug.Log("[TutorialDirector] Side effect skipped: not server.");
            return;
        }

        if (scheduler == null)
            scheduler = FindFirstObjectByType<WorkstationOrderScheduler>();

        if (scheduler == null)
        {
            Debug.LogWarning("[TutorialDirector] Scheduler not found; cannot trigger table order.");
            return;
        }

        bool ok = false;
        string target = null;

        if (!string.IsNullOrWhiteSpace(step.targetTableDisplayName))
            target = step.targetTableDisplayName;
        else if (!string.IsNullOrWhiteSpace(step.requiredPayloadId))
            target = step.requiredPayloadId;

        if (!string.IsNullOrWhiteSpace(target))
        {
            ok = scheduler.TryStartTableNowByDisplayName(target);
        }
        else
        {
            ok = scheduler.TryStartAnyAvailableTableNow();
            Debug.LogWarning($"[TutorialDirector] Step '{step.stepId}' has no target table name. Fallback used.");
        }

        Debug.Log($"[TutorialDirector] triggerTableOrderOnActivate step='{step.stepId}' target='{target}' => {ok}");
    }

    private void ExecuteStepAction(TutorialStepSO step)
    {
        if (step == null) return;

        switch (step.onStepCompletedAction)
        {
            case TutorialStepAction.FinishAndReturnToMenu:
                StartCoroutine(FinishAndReturnRoutine(Mathf.Max(0f, step.actionDelaySeconds)));
                break;

            default:
                if (step.actionDelaySeconds > 0f)
                    StartCoroutine(ActivateNextStepDelayed(step.actionDelaySeconds));
                else
                    ActivateNextStep();
                break;
        }
    }

    private System.Collections.IEnumerator FinishAndReturnRoutine(float delay)
    {
        if (endingTutorial) yield break;
        endingTutorial = true;

        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        TutorialRuntimeContext.MarkCompleted();
        pointerRegistry?.HideAll();
        narratorUI?.Hide();

        Time.timeScale = 1f;

        if (SessionCoordinator.Instance != null)
            SessionCoordinator.Instance.LeaveToMainMenu();
        else
            Loader.Load(Loader.Scene.MenuScene);
    }

    private bool IsMainTriggerSatisfiedByPersistentMemory(TutorialStepSO step)
    {
        if (step == null) return false;

        bool req = !string.IsNullOrWhiteSpace(step.requiredPayloadId);

        switch (step.completesOn)
        {
            case TutorialEventType.MovedFirstTime:
                return bb.EverMoved || bb.HasFact(TutorialEventType.MovedFirstTime);

            case TutorialEventType.PickedUpItem:
                return req ? bb.HasFact(TutorialEventType.PickedUpItem, step.requiredPayloadId)
                           : bb.EverPickedUpItem || bb.HasFact(TutorialEventType.PickedUpItem);

            case TutorialEventType.LookedAtInteractable:
                return req ? bb.HasFact(TutorialEventType.LookedAtInteractable, step.requiredPayloadId)
                           : bb.EverLookedAtInteractable || bb.HasFact(TutorialEventType.LookedAtInteractable);

            case TutorialEventType.ReachedTutorialTarget:
                if (!req) return bb.HasFact(TutorialEventType.ReachedTutorialTarget);
                return bb.HasFact(TutorialEventType.ReachedTutorialTarget, step.requiredPayloadId) ||
                       TutorialTargetProximityEmitter.IsTargetReached(step.requiredPayloadId);

            case TutorialEventType.OrderAccepted:
                return req ? bb.HasFact(TutorialEventType.OrderAccepted, step.requiredPayloadId)
                           : bb.EverOrderAccepted || bb.HasFact(TutorialEventType.OrderAccepted);

            case TutorialEventType.PaymentPhaseStarted:
                return req ? bb.HasFact(TutorialEventType.PaymentPhaseStarted, step.requiredPayloadId)
                           : bb.EverPaymentPhaseStarted || bb.HasFact(TutorialEventType.PaymentPhaseStarted);

            case TutorialEventType.DeliveredRequiredItem:
                return req ? bb.HasFact(TutorialEventType.DeliveredRequiredItem, step.requiredPayloadId)
                           : bb.EverDeliveredRequiredItem || bb.HasFact(TutorialEventType.DeliveredRequiredItem);

            case TutorialEventType.AllItemsDelivered:
                return req ? bb.HasFact(TutorialEventType.AllItemsDelivered, step.requiredPayloadId)
                           : bb.EverAllItemsDelivered || bb.HasFact(TutorialEventType.AllItemsDelivered);

            case TutorialEventType.PaymentAttemptFailed:
                return req ? bb.HasFact(TutorialEventType.PaymentAttemptFailed, step.requiredPayloadId)
                           : bb.EverPaymentAttemptFailed || bb.HasFact(TutorialEventType.PaymentAttemptFailed);

            case TutorialEventType.PaymentSucceeded:
                return req ? bb.HasFact(TutorialEventType.PaymentSucceeded, step.requiredPayloadId)
                           : bb.EverPaymentSucceeded || bb.HasFact(TutorialEventType.PaymentSucceeded);

            case TutorialEventType.OrderFailedTimeout:
                return req ? bb.HasFact(TutorialEventType.OrderFailedTimeout, step.requiredPayloadId)
                           : bb.EverOrderFailedTimeout || bb.HasFact(TutorialEventType.OrderFailedTimeout);

            case TutorialEventType.DeliverItemsTimeout:
                return req ? bb.HasFact(TutorialEventType.DeliverItemsTimeout, step.requiredPayloadId)
                           : bb.EverDeliverItemsTimeout || bb.HasFact(TutorialEventType.DeliverItemsTimeout);

            case TutorialEventType.PriceBoardOpened:
                return req ? bb.HasFact(TutorialEventType.PriceBoardOpened, step.requiredPayloadId)
                           : bb.EverPriceBoardOpened || bb.HasFact(TutorialEventType.PriceBoardOpened);

            case TutorialEventType.SinkItemDeposited:
                return req ? bb.HasFact(TutorialEventType.SinkItemDeposited, step.requiredPayloadId)
                           : bb.EverSinkItemDeposited || bb.HasFact(TutorialEventType.SinkItemDeposited);

            case TutorialEventType.SinkItemCleaned:
                return req ? bb.HasFact(TutorialEventType.SinkItemCleaned, step.requiredPayloadId)
                           : bb.EverSinkItemCleaned || bb.HasFact(TutorialEventType.SinkItemCleaned);

            case TutorialEventType.NarrationFinished:
                return false;

            default:
                return false;
        }
    }

    private bool AreExtraConditionsSatisfiedByPersistentMemory(TutorialStepSO step)
    {
        if (step == null) return false;

        if (step.requireMovement &&
            !(stepMoved || bb.EverMoved || bb.HasFact(TutorialEventType.MovedFirstTime)))
            return false;

        if (step.requirePickup &&
            !(stepPickedUpAny || bb.EverPickedUpItem || bb.HasFact(TutorialEventType.PickedUpItem)))
            return false;

        if (step.requireLookAtInteractable &&
            !(stepLookedAtAny || bb.EverLookedAtInteractable || bb.HasFact(TutorialEventType.LookedAtInteractable)))
            return false;

        if (step.requirePaymentPhase &&
            !(stepPaymentPhaseStarted || bb.EverPaymentPhaseStarted || bb.HasFact(TutorialEventType.PaymentPhaseStarted)))
            return false;

        if (step.requireNarrationFinishedForThisStep && !stepNarrationFinished)
            return false;

        return true;
    }

    private bool IsLocalPlayerHoldingTutorialItem(string requiredPayloadId)
    {
        if (string.IsNullOrWhiteSpace(requiredPayloadId)) return false;

        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p == null || !p.IsOwner) continue;

            var carry = p.GetComponent<PlayerCarryNet>();
            if (carry == null || !carry.HasAny) return false;

            for (int k = 0; k < carry.Count; k++)
            {
                if (!carry.TryGetHeldAt(k, out var obj) || obj == null) continue;
                var so = obj.GetSceneObjectSO();
                if (so == null) continue;

                string id = !string.IsNullOrWhiteSpace(so.tutorialId) ? so.tutorialId : so.objectName;
                if (string.Equals(id, requiredPayloadId, System.StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        return false;
    }

    private void TryTriggerNpcStepAction(TutorialStepSO step)
    {
        if (step == null) return;
        if (!step.executeNpcActionOnActivate) return;

        if (npcDirector == null)
            npcDirector = FindFirstObjectByType<TutorialNpcDirector>();

        if (npcDirector == null)
        {
            Debug.LogWarning($"[TutorialDirector] NPC director not found for step '{step.stepId}'.");
            return;
        }

        npcDirector.Execute(step.npcAction);
    }

    private bool TryBranchOnIncomingEvent(TutorialEventType type, object payload)
    {
        
        if (transitioningToNextStep) return false;
        if (sequence == null || !sequence.useGraph) return false;
        if (current == null || current.nextLinks == null || current.nextLinks.Length == 0) return false;

        TutorialStepLink best = null;
        for (int i = 0; i < current.nextLinks.Length; i++)
        {
            var l = current.nextLinks[i];
            if (l == null) continue;
            if (l.conditionMode != TutorialLinkConditionMode.OnEvent) continue;
            if (l.whenEvent != type) continue;
            if (string.IsNullOrWhiteSpace(l.targetStepId)) continue;

            bool payloadOk = string.IsNullOrWhiteSpace(l.requiredPayloadId) ||
                            (payload is string s && s == l.requiredPayloadId) ||
                            (l.allowPersistentMatch && bb.HasFact(l.whenEvent, l.requiredPayloadId));

            if (!payloadOk) continue;

            if (best == null || l.priority > best.priority) best = l;
        }

        if (best == null) return false;
        Debug.Log($"[TutorialDirector] Branch event {type} payload={payload} -> {best.targetStepId}");

        forcedNextStepId = best.targetStepId;
        if (!bb.CompletedSteps.Contains(current.stepId))
            bb.CompletedSteps.Add(current.stepId);

        ActivateNextStep();
        return true;
    }
}