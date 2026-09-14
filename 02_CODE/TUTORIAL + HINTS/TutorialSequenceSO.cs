using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Tutorial/Sequence")]
public class TutorialSequenceSO : ScriptableObject
{
    public string sequenceId = "Map_01_Tutorial";
    public TutorialStepSO[] steps;

    [Header("Graph Mode")]
    [Tooltip("If true, TutorialDirector uses graph transitions. If false, legacy linear ordering is used.")]
    public bool useGraph = false;

    [Tooltip("First step id in graph mode. If empty, falls back to first array element.")]
    public string entryStepId;

    public TutorialStepSO FindById(string stepId)
    {
        if (steps == null || string.IsNullOrWhiteSpace(stepId)) return null;

        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            if (s == null) continue;
            if (s.stepId == stepId) return s;
        }

        return null;
    }

    public Dictionary<string, TutorialStepSO> BuildStepMap()
    {
        var map = new Dictionary<string, TutorialStepSO>();
        if (steps == null) return map;

        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            if (s == null || string.IsNullOrWhiteSpace(s.stepId)) continue;

            if (map.ContainsKey(s.stepId))
                Debug.LogWarning($"[TutorialSequenceSO] Duplicate stepId '{s.stepId}' in sequence '{sequenceId}'. Last one wins.");

            map[s.stepId] = s;
        }

        return map;
    }

    public void ValidateGraph()
    {
        if (!useGraph || steps == null) return;

        var map = BuildStepMap();

        if (!string.IsNullOrWhiteSpace(entryStepId) && !map.ContainsKey(entryStepId))
            Debug.LogWarning($"[TutorialSequenceSO] Entry step '{entryStepId}' not found in sequence '{sequenceId}'.");

        for (int i = 0; i < steps.Length; i++)
        {
            var s = steps[i];
            if (s == null) continue;

            if (s.nextLinks == null) continue;
            for (int j = 0; j < s.nextLinks.Length; j++)
            {
                var l = s.nextLinks[j];
                if (l == null || string.IsNullOrWhiteSpace(l.targetStepId)) continue;

                if (!map.ContainsKey(l.targetStepId))
                    Debug.LogWarning($"[TutorialSequenceSO] Step '{s.stepId}' link target '{l.targetStepId}' not found.");
            }
        }
    }
}