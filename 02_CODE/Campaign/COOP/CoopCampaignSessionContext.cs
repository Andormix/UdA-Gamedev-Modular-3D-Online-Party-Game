public static class CoopCampaignSessionContext
{
    public static bool IsCoopCampaignRun;
    public static string SelectedLevelId;
    public static bool LocalWasLevelUnlockedAtMatchStart;

    public static void Clear()
    {
        IsCoopCampaignRun = false;
        SelectedLevelId = null;
        LocalWasLevelUnlockedAtMatchStart = false;
    }
}