using System;
using System.Collections.Generic;
using UnityEngine;

public class TutorialPointerRegistry : MonoBehaviour
{
    [Serializable]
    public class PointerEntry
    {
        [Tooltip("Must match TutorialStepSO.pointerTargetId")]
        public string targetId;

        [Tooltip("GO with shader arrow / highlight to toggle")]
        public GameObject pointerRoot;
    }

    [SerializeField] private List<PointerEntry> pointers = new();

    private readonly Dictionary<string, GameObject> map = new();

    private void Awake()
    {
        map.Clear();

        foreach (var p in pointers)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.targetId) || p.pointerRoot == null)
                continue;

            map[p.targetId] = p.pointerRoot;
            p.pointerRoot.SetActive(false);
        }
    }

    public void ShowOnly(string targetId)
    {
        HideAll();

        if (string.IsNullOrWhiteSpace(targetId)) return;
        if (map.TryGetValue(targetId, out var go) && go != null)
            go.SetActive(true);
    }

    public void HideAll()
    {
        foreach (var kv in map)
        {
            if (kv.Value != null)
                kv.Value.SetActive(false);
        }
    }
}