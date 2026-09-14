using System.Collections.Generic;
using UnityEngine;

public static class TutorialSceneTargetRegistry
{
    private static readonly Dictionary<string, Transform> map = new();

    public static void Rebuild()
    {
        map.Clear();
        var all = Object.FindObjectsByType<TutorialSceneTarget>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t == null) continue;
            if (string.IsNullOrWhiteSpace(t.TargetId)) continue;
            map[t.TargetId] = t.transform;
        }
    }

    public static Transform Resolve(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        if (map.Count == 0) Rebuild();

        if (map.TryGetValue(id, out var tr) && tr != null)
            return tr;

        // retry once in case scene reloaded/spawned late
        Rebuild();
        map.TryGetValue(id, out tr);
        return tr;
    }
}