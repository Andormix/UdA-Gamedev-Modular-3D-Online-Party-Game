using System;
using System.Collections.Generic;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance { get; private set; }

    private static readonly Dictionary<int, int> BaseCoinsByStars = new()
    {
        { 0, 10 },
        { 1, 30 },
        { 2, 60 },
        { 3, 100 }
    };

    private const float MultiplierOffline = 1.0f;
    private const float MultiplierCoop = 1.2f;
    private const float MultiplierMultiplayer = 1.5f;

    [Header("Debug")]
    [SerializeField] private bool autoPrintWalletAfterGrant = true;

    private const int StarVaultStep = 50;
    private const int StarVaultDiamond = 2;

    private EconomySaveData save;

    public event Action<int> OnCoinsChanged;
    public event Action<int> OnDiamondsChanged;

    public int Coins => save != null ? save.coins : 0;
    public int Diamonds => save != null ? save.diamonds : 0;
    public int TotalAccumulatedStars => save != null ? save.totalAccumulatedStars : 0;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        save = EconomySaveSystem.LoadOrCreate();

        if (autoPrintWalletAfterGrant) DebugPrintWallet();
    }

    public EconomyMatchReward GrantMatchRewards(
        int stars,
        EconomyGameMode mode,
        bool campaignLevelJustReachedThreeStarsFirstTime,
        bool isMultiplayerWinForLocalPlayer,
        float coinsMultiplier = 1f) // NEW
    {
        stars = Mathf.Clamp(stars, 0, 3);
        coinsMultiplier = Mathf.Clamp01(coinsMultiplier);

        int baseCoins = CalculateCoins(stars, mode);
        int finalCoins = Mathf.RoundToInt(baseCoins * coinsMultiplier);

        EconomyMatchReward reward = new EconomyMatchReward
        {
            stars = stars,
            coinsEarned = finalCoins,
            diamondsEarned = 0,
            diamondReason = string.Empty
        };

        save.coins += reward.coinsEarned;
        save.totalAccumulatedStars += stars;

        List<string> reasons = new();

        int vaultBefore = save.lastStarVaultClaimedAtStars;
        int vaultNow = (save.totalAccumulatedStars / StarVaultStep) * StarVaultStep;
        if (vaultNow > vaultBefore)
        {
            int stepsGained = (vaultNow - vaultBefore) / StarVaultStep;
            int diamondsFromVault = stepsGained * StarVaultDiamond;
            save.diamonds += diamondsFromVault;
            reward.diamondsEarned += diamondsFromVault;
            save.lastStarVaultClaimedAtStars = vaultNow;
            reasons.Add($"Star Vault +{diamondsFromVault}");
        }

        if (campaignLevelJustReachedThreeStarsFirstTime && stars >= 3)
        {
            save.diamonds += 1;
            reward.diamondsEarned += 1;
            reasons.Add("Campaign Master +1");
        }

        if (isMultiplayerWinForLocalPlayer && mode == EconomyGameMode.Multiplayer)
        {
            string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (!string.Equals(save.lastDailyCompetitiveWinDateUtc, todayUtc, StringComparison.Ordinal))
            {
                save.lastDailyCompetitiveWinDateUtc = todayUtc;
                save.diamonds += 1;
                reward.diamondsEarned += 1;
                reasons.Add("Daily Competitive Win +1");
            }
        }

        reward.diamondReason = string.Join(" | ", reasons);

        EconomySaveSystem.Save(save);

        OnCoinsChanged?.Invoke(save.coins);
        OnDiamondsChanged?.Invoke(save.diamonds);

        Debug.Log($"[Economy] Reward => stars={reward.stars}, coins={reward.coinsEarned}, diamonds={reward.diamondsEarned}, reason={reward.diamondReason}");

        if (autoPrintWalletAfterGrant) DebugPrintWallet();
        return reward;
    }

    public int CalculateCoins(int stars, EconomyGameMode mode)
    {
        stars = Mathf.Clamp(stars, 0, 3);
        int baseCoins = BaseCoinsByStars[stars];

        float multiplier = mode switch
        {
            EconomyGameMode.CoopCampaign => MultiplierCoop,
            EconomyGameMode.Multiplayer => MultiplierMultiplayer,
            _ => MultiplierOffline
        };

        return Mathf.RoundToInt(baseCoins * multiplier);
    }

    [ContextMenu("DEBUG Reset Economy Save")]
    public void DebugResetEconomy()
    {
        save = new EconomySaveData();
        EconomySaveSystem.Save(save);

        OnCoinsChanged?.Invoke(save.coins);
        OnDiamondsChanged?.Invoke(save.diamonds);

        Debug.Log("[Economy] Save reset.");
    }

    public EconomySaveData GetSnapshot()
    {
        return new EconomySaveData
        {
            version = save.version,
            coins = save.coins,
            diamonds = save.diamonds,
            totalAccumulatedStars = save.totalAccumulatedStars,
            lastStarVaultClaimedAtStars = save.lastStarVaultClaimedAtStars,
            lastDailyCompetitiveWinDateUtc = save.lastDailyCompetitiveWinDateUtc,
            campaignLevelsGrantedDiamond = new List<string>(save.campaignLevelsGrantedDiamond)
        };
    }

    [ContextMenu("DEBUG Print Wallet")]
    public void DebugPrintWallet()
    {
        if (save == null) { Debug.LogWarning("[Economy] Save is null"); return; }

        Debug.Log($"[Economy Wallet] coins={save.coins}, diamonds={save.diamonds}, totalStars={save.totalAccumulatedStars}, lastVaultAt={save.lastStarVaultClaimedAtStars}, lastDailyWinUtc={save.lastDailyCompetitiveWinDateUtc}");
    }
}