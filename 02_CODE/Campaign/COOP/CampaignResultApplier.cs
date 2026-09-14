using UnityEngine;

public static class CampaignResultApplier
{
    public static void ApplyLocalResult(string levelId, int finalScore, bool isSingle, bool isCoop)
    {
        if (CampaignManager.Instance == null) return;
        if (string.IsNullOrWhiteSpace(levelId)) return;

        if (isSingle)
        {
            CampaignManager.Instance.RecordLevelResult(levelId, finalScore);
            Debug.Log($"[CampaignResult] Single saved {levelId} score={finalScore}");
            return;
        }

        if (isCoop)
        {
            if (!CoopCampaignSessionContext.LocalWasLevelUnlockedAtMatchStart)
            {
                Debug.Log($"[CampaignResult] Coop blocked (locked at start) {levelId}");
                return;
            }

            CampaignManager.Instance.RecordLevelResult(levelId, finalScore);
            Debug.Log($"[CampaignResult] Coop saved {levelId} score={finalScore}");
        }
    }
}