using System;

[Serializable]
public struct EconomyMatchReward
{
    public int stars;            // 0..3
    public int coinsEarned;
    public int diamondsEarned;
    public string diamondReason; // "", "Star Vault", "Campaign Master", etc.
}