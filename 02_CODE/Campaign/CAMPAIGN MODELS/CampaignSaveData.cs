using System;
using System.Collections.Generic;

[Serializable]
public class CampaignSaveData
{
    public int version = 1;
    public List<LevelProgressData> levels = new();
}