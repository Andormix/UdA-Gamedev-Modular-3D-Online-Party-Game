using System.Collections.Generic;
using UnityEngine;

public sealed class NarratorBrain
{
    private readonly Dictionary<string, float> cooldownUntil = new();
    private float globalCooldownUntil;

    public TutorialStepSO SelectNextStep(NarratorBlackboard bb, TutorialStepSO[] steps)
    {
        if (steps == null) return null;

        foreach (var s in steps)
        {
            if (s == null) continue;
            if (bb.CompletedSteps.Contains(s.stepId)) continue;

            bool ok = true;
            if (s.prerequisites != null)
            {
                foreach (var p in s.prerequisites)
                {
                    if (p != null && !bb.CompletedSteps.Contains(p.stepId))
                    {
                        ok = false;
                        break;
                    }
                }
            }

            if (ok) return s;
        }

        return null;
    }

    public void ApplyEventToBlackboard(NarratorBlackboard bb, TutorialEventType type)
    {
        switch (type)
        {
            case TutorialEventType.MovedFirstTime: bb.HasMoved = true; break;
            case TutorialEventType.LookedAtInteractable: bb.HasLookedAtInteractable = true; break;
            case TutorialEventType.PickedUpItem: bb.HasPickedUp = true; break;

            case TutorialEventType.PaymentPhaseStarted:
                bb.IsInPaymentPhase = true;
                bb.PaymentFailCount = 0;
                break;

            case TutorialEventType.PaymentAttemptFailed:
                bb.PaymentFailCount++;
                break;

            case TutorialEventType.PaymentSucceeded:
                bb.IsInPaymentPhase = false;
                bb.PaymentFailCount = 0;
                break;
        }
    }

    public void OnStepActivated(
        NarratorBlackboard bb,
        TutorialStepSO step,
        TutorialSettingsSO settings,
        NarratorPresenter ui,
        VoiceNarrator voice)
    {
        if (step == null) return;
        TryShowBestLine(bb, step.startLines, settings, ui, voice, true);
    }

    public void Tick(
        NarratorBlackboard bb,
        TutorialStepSO step,
        TutorialSettingsSO settings,
        NarratorPresenter ui,
        VoiceNarrator voice,
        float elapsed)
    {
        if (step == null) return;
        if (elapsed < step.secondsBeforeHint) return;

        bool force = bb.IsInPaymentPhase && bb.PaymentFailCount >= 2;
        TryShowBestLine(bb, step.hintLines, settings, ui, voice, force);
    }

    private void TryShowBestLine(
        NarratorBlackboard bb,
        NarratorLineSO[] lines,
        TutorialSettingsSO settings,
        NarratorPresenter ui,
        VoiceNarrator voice,
        bool force)
    {
        if (lines == null || lines.Length == 0) return;
        if (!force && Time.time < globalCooldownUntil) return;

        NarratorLineSO best = null;

        foreach (var l in lines)
        {
            if (l == null) continue;
            if (l.oneShot && bb.ShownLines.Contains(l.id)) continue;
            if (!force && cooldownUntil.TryGetValue(l.id, out float until) && Time.time < until) continue;

            if (best == null || l.priority > best.priority) best = l;
        }

        if (best == null) return;

        string text = best.text != null ? best.text.Get(settings.language) : "(missing text)";
        bool persistent = best.kind == NarratorLineKind.Instruction;

        if (persistent)
        {
            // Instruction replaces previous instruction always
            ui.ForceShow(text, best.minShowSeconds, best.canInterrupt, persistent: true);
        }
        else
        {
            // Hint respects current line interruption rules
            ui.Show(text, best.minShowSeconds, best.canInterrupt, persistent: false);
        }

        if (settings.voiceEnabled && voice != null && best.voiceClip != null)
            voice.PlayVoice(best.voiceClip);

        bb.ShownLines.Add(best.id);
        cooldownUntil[best.id] = Time.time + best.cooldownSeconds;
        globalCooldownUntil = Time.time + settings.globalMinSecondsBetweenLines;
    }
}