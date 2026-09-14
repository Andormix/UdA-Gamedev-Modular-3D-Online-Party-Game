using UnityEngine;

public class EconomyDebugTester : MonoBehaviour
{
    [ContextMenu("Economy Test A: 0 stars Offline")]
    private void TestA()
    {
        if (EconomyManager.Instance == null) { Debug.LogError("[EconomyDebugTester] EconomyManager.Instance is null"); return; }
        var reward = EconomyManager.Instance.GrantMatchRewards(0, EconomyGameMode.OfflineSingleplayer, false, false);
        Debug.Log($"[TEST A] coins={reward.coinsEarned}, diamonds={reward.diamondsEarned}, reason={reward.diamondReason}");
    }

    [ContextMenu("Economy Test B: 2 stars Coop")]
    private void TestB()
    {
        if (EconomyManager.Instance == null) { Debug.LogError("[EconomyDebugTester] EconomyManager.Instance is null"); return; }
        var reward = EconomyManager.Instance.GrantMatchRewards(2, EconomyGameMode.CoopCampaign, false, false);
        Debug.Log($"[TEST B] coins={reward.coinsEarned}, diamonds={reward.diamondsEarned}, reason={reward.diamondReason}");
    }

    [ContextMenu("Economy Test C: 3 stars Multiplayer + campaignMaster + dailyWin")]
    private void TestC()
    {
        if (EconomyManager.Instance == null) { Debug.LogError("[EconomyDebugTester] EconomyManager.Instance is null"); return; }
        var reward = EconomyManager.Instance.GrantMatchRewards(3, EconomyGameMode.Multiplayer, true, true);
        Debug.Log($"[TEST C] coins={reward.coinsEarned}, diamonds={reward.diamondsEarned}, reason={reward.diamondReason}");
    }

    [ContextMenu("Economy Test D: Print Wallet")]
    private void TestD_PrintWallet()
    {
        if (EconomyManager.Instance == null) { Debug.LogError("[EconomyDebugTester] EconomyManager.Instance is null"); return; }
        EconomyManager.Instance.DebugPrintWallet();
    }
}