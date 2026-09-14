using System;

[Serializable]
public class LevelProgressData
{
    public string levelId;   // exemple: "Map_01"
    public bool unlocked;
    public bool completed;

    public int bestScore;    // keep max
    public float bestStars;  // keep max (0..3, step 0.5)
}