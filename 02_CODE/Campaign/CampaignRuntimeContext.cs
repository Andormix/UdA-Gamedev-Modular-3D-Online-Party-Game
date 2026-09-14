public static class CampaignRuntimeContext
{
    // Set when player chooses a level in CampaignSelectScene (NECESSARI)
    public static bool IsCampaignRun;
    public static string SelectedLevelId;

    public static void Clear()
    {
        IsCampaignRun = false;
        SelectedLevelId = null;
    }
}