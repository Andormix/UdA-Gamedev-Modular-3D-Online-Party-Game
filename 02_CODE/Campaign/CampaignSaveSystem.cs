using System.IO;
using UnityEngine;

public static class CampaignSaveSystem
{
#if UNITY_EDITOR
    private const string FileName = "campaign_save_EDITOR.json";
#else
    private const string FileName = "campaign_save_BUILD.json";
#endif

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    public static CampaignSaveData LoadOrCreate(CampaignDefinitionSO definition)
    {
        CampaignSaveData data = null;

        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<CampaignSaveData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CampaignSave] Failed reading save, creating new. {e.Message}");
            }
        }

        if (data == null)
            data = new CampaignSaveData();

        EnsureSchema(data, definition);
        Save(data);

        Debug.Log($"[CampaignSave] Using file: {SavePath}");
        return data;
    }

    public static void Save(CampaignSaveData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CampaignSave] Save failed: {e.Message}");
        }
    }

    private static void EnsureSchema(CampaignSaveData data, CampaignDefinitionSO definition)
    {
        if (data.levels == null)
            data.levels = new System.Collections.Generic.List<LevelProgressData>();

        foreach (var def in definition.levels)
        {
            if (def == null || string.IsNullOrWhiteSpace(def.levelId)) continue;

            var row = data.levels.Find(x => x.levelId == def.levelId);
            if (row == null)
            {
                row = new LevelProgressData
                {
                    levelId = def.levelId,
                    unlocked = false,
                    completed = false,
                    bestScore = 0,
                    bestStars = 0f
                };
                data.levels.Add(row);
            }
        }

        if (definition.levels.Count > 0 && definition.levels[0] != null)
        {
            string firstId = definition.levels[0].levelId;
            var first = data.levels.Find(x => x.levelId == firstId);
            if (first != null) first.unlocked = true;
        }
    }
}