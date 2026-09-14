using UnityEngine;

public class TutorialQuickStartDebug : MonoBehaviour
{
    [SerializeField] private string scenarioId = "CoreLoop_v1";

    [ContextMenu("DEBUG Start Tutorial on Map_01")]
    public void DebugStartTutorialMap01()
    {
        var session = SessionCoordinator.Instance;
        if (session == null)
        {
            Debug.LogError("[TutorialQuickStartDebug] SessionCoordinator.Instance is null");
            return;
        }

        TutorialRunStarter.StartSingleplayerTutorial(session, Loader.Scene.Map_01, scenarioId);
    }
}