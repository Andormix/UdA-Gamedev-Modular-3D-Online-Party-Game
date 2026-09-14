using UnityEngine;

public static class TutorialRunStarter
{
    // Singleplayer tutorial entry
    public static void StartSingleplayerTutorial(SessionCoordinator session, Loader.Scene tutorialScene, string scenarioId = "CoreLoop_v1")
    {
        if (session == null) return;

        GameMultiplayerManager.playMultiplayer = false;
        CampaignRuntimeContext.Clear();
        CoopCampaignSessionContext.Clear();

        TutorialRuntimeContext.StartTutorial(scenarioId, sharedProgression: false);

        session.StartSingleplayerHostLocal(tutorialScene);
    }

    // Product decision i made: tutorial is NOT available in coop
    public static void MarkCoopTutorialContext(string scenarioId = "CoreLoop_v1")
    {
        TutorialRuntimeContext.Clear();
    }
}