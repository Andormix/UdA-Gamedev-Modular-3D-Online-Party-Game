#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FindMissingScriptsInPrefabs
{
    [MenuItem("Tools/Debug/Find Missing Scripts In Prefabs")]
    public static void FindInPrefabs()
    {
        var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        int missingCount = 0;

        foreach (var guid in prefabGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var components = prefab.GetComponentsInChildren<Component>(true);
            if (components.Any(c => c == null))
            {
                missingCount++;
                Debug.LogWarning($"Missing script in prefab: {path}", prefab);
            }
        }

        Debug.Log($"FindMissingScriptsInPrefabs: Found {missingCount} prefab(s) with missing scripts.");
    }
}
#endif