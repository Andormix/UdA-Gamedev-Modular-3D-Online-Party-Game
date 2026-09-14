using UnityEngine;

public class CampaignManager : MonoBehaviour
{
    public static CampaignManager Instance { get; private set; }

    [SerializeField] private CampaignDefinitionSO campaignDefinition;

    public CampaignDefinitionSO Definition => campaignDefinition;

    private CampaignSaveData saveData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (campaignDefinition == null)
        {
            Debug.LogError("[CampaignManager] CampaignDefinitionSO missing.");
            return;
        }

        saveData = CampaignSaveSystem.LoadOrCreate(campaignDefinition);
    }

    public CampaignSaveData GetSaveData() => saveData;

    public LevelProgressData GetLevelProgress(string levelId)
    {
        return saveData.levels.Find(x => x.levelId == levelId);
    }

    public CampaignLevelDefinitionSO GetLevelDefinition(string levelId)
    {
        return campaignDefinition.levels.Find(x => x != null && x.levelId == levelId);
    }

    public bool IsLevelUnlocked(string levelId)
    {
        var p = GetLevelProgress(levelId);
        return p != null && p.unlocked;
    }

    public void RecordLevelResult(string levelId, int score)
    {
        var def = GetLevelDefinition(levelId);
        var progress = GetLevelProgress(levelId);

        if (def == null || progress == null)
        {
            Debug.LogWarning($"[Campaign] Missing level definition/progress for {levelId}");
            return;
        }

        float stars = CalculateStars(score, def);

        if (score > progress.bestScore) progress.bestScore = score;
        if (stars > progress.bestStars) progress.bestStars = stars;

        progress.completed = true;
        progress.unlocked = true;

        // Linear unlock
        if (!string.IsNullOrWhiteSpace(def.nextLevelId))
        {
            var next = GetLevelProgress(def.nextLevelId);
            if (next != null) next.unlocked = true;
        }

        CampaignSaveSystem.Save(saveData);
        Debug.Log($"[Campaign] Saved result {levelId}: score={score}, stars={stars}");
    }

    public float CalculateStars(int score, CampaignLevelDefinitionSO def)
    {
        float raw;

        if (score < def.scoreFor1Star) raw = 0f;
        else if (score < def.scoreFor2Stars)
            raw = Mathf.Lerp(1f, 2f, Mathf.InverseLerp(def.scoreFor1Star, def.scoreFor2Stars, score));
        else if (score < def.scoreFor3Stars)
            raw = Mathf.Lerp(2f, 3f, Mathf.InverseLerp(def.scoreFor2Stars, def.scoreFor3Stars, score));
        else
            raw = 3f;

        // half-star steps
        return Mathf.Clamp(Mathf.Round(raw * 2f) / 2f, 0f, 3f);
    }

    [ContextMenu("DEBUG Reset Campaign Save")]
    public void DebugResetSave()
    {
        saveData = CampaignSaveSystem.LoadOrCreate(campaignDefinition);
        foreach (var l in saveData.levels)
        {
            l.unlocked = false;
            l.completed = false;
            l.bestScore = 0;
            l.bestStars = 0f;
        }

        if (campaignDefinition.levels.Count > 0 && campaignDefinition.levels[0] != null)
        {
            var first = GetLevelProgress(campaignDefinition.levels[0].levelId);
            if (first != null) first.unlocked = true;
        }

        CampaignSaveSystem.Save(saveData);
        Debug.Log("[Campaign] Save reset.");
    }
}