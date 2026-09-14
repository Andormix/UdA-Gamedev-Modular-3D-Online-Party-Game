using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Campaign/Campaign Definition")]
public class CampaignDefinitionSO : ScriptableObject
{
    public List<CampaignLevelDefinitionSO> levels = new();
}