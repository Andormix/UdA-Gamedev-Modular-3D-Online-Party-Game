using System;
using System.Collections.Generic;

[Serializable]
public class EconomySaveData
{
    public int version = 1;

    // Wallet
    public int coins;
    public int diamonds;

    // Progress
    public int totalAccumulatedStars; // lifetime sum for Star Vault milestones

    // Milestones
    public int lastStarVaultClaimedAtStars; // e.g. 0,50,100...

    // Daily multiplayer win diamond
    public string lastDailyCompetitiveWinDateUtc; // yyyy-MM-dd

    // Campaign first 3-star rewards registry
    public List<string> campaignLevelsGrantedDiamond = new();
}