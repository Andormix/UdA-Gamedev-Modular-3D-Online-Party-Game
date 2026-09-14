using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Cleaning/Cleaning Return Registry")]
public class CleaningReturnRegistrySO : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("Clean SO that should be replenished after washing (e.g. EmptyCup_Clean).")]
        public SceneObjectSO cleanSo;

        [Tooltip("Spawner that owns finite stock for this clean SO.")]
        public ObjectSpawner targetSpawner;
    }

    public List<Entry> entries = new();

    public ObjectSpawner FindSpawnerForCleanSo(SceneObjectSO cleanSo)
    {
        if (cleanSo == null || entries == null) return null;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.cleanSo == null || e.targetSpawner == null) continue;
            if (e.cleanSo == cleanSo) return e.targetSpawner;
        }

        return null;
    }
}