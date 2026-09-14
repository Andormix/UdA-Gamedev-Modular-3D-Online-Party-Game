using UnityEngine;

[CreateAssetMenu(menuName = "Game/Campaign/Level Definition")]
public class CampaignLevelDefinitionSO : ScriptableObject
{
    [Header("Identity")]
    public string levelId;       // MUST (match scene name) Per exemple: "Map_01"
    public string displayName;   

    [Header("Scene")]
    public Loader.Scene scene;   // El enum que hem creat 

    [Header("Linear progression")]
    public string nextLevelId;   //  "Map_02" or empty if last

    [Header("Star thresholds (score-based)")]
    public int scoreFor1Star = 300;
    public int scoreFor2Stars = 650;
    public int scoreFor3Stars = 1000;

    [Header("Campaign Artwork")]
    public Sprite lockedImage;
    public Sprite unlockedImage;
}