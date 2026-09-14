using UnityEngine;

[CreateAssetMenu(menuName = "Game/Tutorial/Settings")]
public class TutorialSettingsSO : ScriptableObject
{
    public bool tutorialEnabled = true;
    public bool enableFullTutorialInSingleplayer = true;
    public bool enableHintsInMultiplayer = true;

    public bool voiceEnabled = true;
    public Language language = Language.English;

    public float globalMinSecondsBetweenLines = 1.0f;
}