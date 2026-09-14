public static class TutorialRuntimeContext
{
    public static bool IsTutorialRun;
    public static string ScenarioId;
    public static bool IsSharedProgression;
    public static bool IsTutorialCompleted;

    public static void StartTutorial(string scenarioId, bool sharedProgression)
    {
        IsTutorialRun = true;
        ScenarioId = scenarioId;
        IsSharedProgression = sharedProgression;
        IsTutorialCompleted = false;

        // reset proximity reached cache for fresh run
        TutorialTargetProximityEmitter.ClearReachedTargets();
    }

    public static void MarkCompleted()
    {
        IsTutorialCompleted = true;
    }

    public static void Clear()
    {
        IsTutorialRun = false;
        ScenarioId = null;
        IsSharedProgression = false;
        IsTutorialCompleted = false;

        TutorialTargetProximityEmitter.ClearReachedTargets();
    }
}