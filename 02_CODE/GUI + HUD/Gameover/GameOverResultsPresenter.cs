using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameOverResultsPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private UserInterfaceManager ui;
    [SerializeField] private GameManager gameManager;

    [Header("Panels")]
    [SerializeField] private GameObject singleplayerPanel;
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject coopPanel;

    [Header("Multiplayer - Ranking (max 4)")]
    [SerializeField] private TMP_Text[] mpPlayerNameTexts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] mpPlayerScoreTexts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] mpPlayerTeamTexts = new TMP_Text[4];
    [SerializeField] private Image[] mpPlayerTeamDots = new Image[4];

    [Header("Multiplayer - Team Dot Colors")]
    [SerializeField] private Color blueTeamColor = new Color32(79, 195, 247, 255); 
    [SerializeField] private Color redTeamColor = new Color32(239, 83, 80, 255);   
    [SerializeField] private Color neutralTeamColor = Color.white;

    [Header("Multiplayer - Summary")]
    [SerializeField] private TMP_Text mpTopPlayerText;
    [SerializeField] private TMP_Text mpMatchTimeText;
    [SerializeField] private TMP_Text mpLocalCoinsText;
    [SerializeField] private TMP_Text mpLocalScoreText;
    [SerializeField] private TMP_Text mpBlueTeamScoreText;
    [SerializeField] private TMP_Text mpRedTeamScoreText;
    [SerializeField] private TMP_Text mpLocalTeamText;

    [Header("Coop - Ranking (max 4)")]
    [SerializeField] private TMP_Text[] coopPlayerNameTexts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] coopPlayerScoreTexts = new TMP_Text[4];
    [SerializeField] private TMP_Text[] coopPlayerTeamTexts = new TMP_Text[4];

    [Header("Coop - Summary")]
    [SerializeField] private TMP_Text coopTopPlayerText;
    [SerializeField] private TMP_Text coopMatchTimeText;
    [SerializeField] private TMP_Text coopLocalCoinsText;
    [SerializeField] private TMP_Text coopLocalScoreText;
    [SerializeField] private TMP_Text coopBlueTeamScoreText;
    [SerializeField] private TMP_Text coopRedTeamScoreText;
    [SerializeField] private TMP_Text coopLocalTeamText;

    [Header("Outcome Banners (shared)")]
    [SerializeField] private GameObject victoryBanner;
    [SerializeField] private GameObject defeatBanner;
    [SerializeField] private GameObject tieBanner;

    [Header("Single - Summary")]
    [SerializeField] private TMP_Text spPlayerNameText;
    [SerializeField] private TMP_Text spScoreText;
    [SerializeField] private TMP_Text spMatchTimeText;
    [SerializeField] private TMP_Text spCoinsText;
    [SerializeField] private TMP_Text spStarsText;

    [Header("Single - Stars Visuals")]
    [SerializeField] private GameObject spZeroStar;
    [SerializeField] private GameObject spOneStar;
    [SerializeField] private GameObject spTwoStar;
    [SerializeField] private GameObject spThreeStar;

    [Header("Coop - Stars Visuals")]
    [SerializeField] private GameObject coopZeroStar;
    [SerializeField] private GameObject coopOneStar;
    [SerializeField] private GameObject coopTwoStar;
    [SerializeField] private GameObject coopThreeStar;

    [Header("Diamond Popup (shared)")]
    [SerializeField] private GameObject diamondPopupRoot;
    [SerializeField] private TMP_Text diamondPopupText;

    [Header("Shared Return Timer UI")]
    [SerializeField] private Slider returnTimerSlider;

    [Header("Shared Return Timer FX")]
    [SerializeField] private RectTransform returnTimerHourglass;
    [SerializeField] private float hourglassFlipDuration = 0.20f;          // duration of a 180º flip
    [SerializeField] private float hourglassWaitBetweenFlips = 0.90f;      // wait after each flip

    private float hourglassAngleZ;
    private float hourglassFlipT;
    private float hourglassWaitT;
    private bool hourglassIsFlipping;
    private float hourglassFromAngle;
    private float hourglassToAngle;

    private bool lastGameOverState;

    private void Awake()
    {
        if (diamondPopupRoot != null) diamondPopupRoot.SetActive(false);
        if (singleplayerPanel != null) singleplayerPanel.SetActive(false);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(false);
        if (coopPanel != null) coopPanel.SetActive(false);

        SetOutcomeBanners(false, false, false);
        SetSingleStarsVisual(-1);
        SetCoopStarsVisual(-1);
        ResetMpTeamDots();

        if (returnTimerSlider != null)
        {
            returnTimerSlider.minValue = 0f;
            returnTimerSlider.maxValue = 1f;
            returnTimerSlider.value = 0f;
        }

        ResetHourglass();
    }

    private void OnEnable()
    {
        if (MatchResultNetSync.Instance != null)
            MatchResultNetSync.Instance.OnResultsUpdated += RefreshFromNet;
    }

    private void OnDisable()
    {
        if (MatchResultNetSync.Instance != null)
            MatchResultNetSync.Instance.OnResultsUpdated -= RefreshFromNet;
    }

    private void Update()
    {
        if (gameManager == null) gameManager = GameManager.Instance;
        if (ui == null) ui = UserInterfaceManager.Instance;

        bool isGameOver = gameManager != null && gameManager.IsGameOver();

        if (!lastGameOverState && isGameOver)
        {
            ShowGameOverPage();
            RefreshFromNet();
        }

        lastGameOverState = isGameOver;

        if (returnTimerSlider != null && gameManager != null)
        {
            returnTimerSlider.value = gameManager.IsGameOver()
                ? gameManager.GetGameOverReturnNormalized()
                : 0f;
        }

        if (gameManager != null && gameManager.IsGameOver())
            UpdateHourglassStepFlip();
        else
            ResetHourglass();
    }

    private void ShowGameOverPage()
    {
        if (ui != null)
            ui.Show(UserInterfaceManager.UIPage.GameOver);
    }

    [ContextMenu("DEBUG Refresh Results From Net")]
    public void RefreshFromNet()
    {
        if (MatchResultNetSync.Instance == null) return;

        var ranking = MatchResultNetSync.Instance.GetRanking();
        var summary = MatchResultNetSync.Instance.GetSummary();

        EconomyGameMode mode = (EconomyGameMode)summary.mode;
        bool isSingle = mode == EconomyGameMode.OfflineSingleplayer;
        bool isCoop = mode == EconomyGameMode.CoopCampaign;
        bool isMulti = mode == EconomyGameMode.Multiplayer;

        if (singleplayerPanel != null) singleplayerPanel.SetActive(isSingle);
        if (multiplayerPanel != null) multiplayerPanel.SetActive(isMulti);
        if (coopPanel != null) coopPanel.SetActive(isCoop);

        if (isSingle)
        {
            FillSingle(summary);
            SetSingleStarsVisual(summary.localStars);
            SetCoopStarsVisual(-1);
            ResetMpTeamDots();
            ApplyPvEOutcomeBanner(summary.localStars);
        }
        else if (isCoop)
        {
            FillCoop(ranking, summary);
            SetCoopStarsVisual(summary.localStars);
            SetSingleStarsVisual(-1);
            ResetMpTeamDots();
            ApplyPvEOutcomeBanner(summary.localStars);
        }
        else // multiplayer
        {
            FillMultiplayer(ranking, summary);
            SetSingleStarsVisual(-1);
            SetCoopStarsVisual(-1);
            ApplyOutcomeBanner((MatchOutcome)summary.localOutcome);
        }

        ShowDiamondPopupIfNeeded(summary.localDiamondsEarned, summary.localDiamondReason.ToString());
    }

    private void FillMultiplayer(System.Collections.Generic.IReadOnlyList<PlayerMatchResultData> ranking, MatchSummaryData summary)
    {
        FillRanking(ranking, mpPlayerNameTexts, mpPlayerScoreTexts, mpPlayerTeamTexts, false);
        FillMultiplayerTeamDots(ranking);

        if (mpTopPlayerText != null) mpTopPlayerText.text = $"{summary.topPlayerName} ({summary.topScore})";
        if (mpMatchTimeText != null) mpMatchTimeText.text = $"{FormatSeconds(summary.matchDurationSeconds)}";
        if (mpLocalCoinsText != null) mpLocalCoinsText.text = $"{summary.localCoinsEarned}";
        if (mpLocalScoreText != null) mpLocalScoreText.text = $"{summary.localScore}";
        if (mpBlueTeamScoreText != null) mpBlueTeamScoreText.text = $"{summary.blueTeamScore}";
        if (mpRedTeamScoreText != null) mpRedTeamScoreText.text = $"{summary.redTeamScore}";

        if (mpLocalTeamText != null)
        {
            MatchTeam localTeam = (MatchTeam)summary.localTeam;
            mpLocalTeamText.text = $"Your Team: {MatchTeamRules.TeamLabel(localTeam)}";
        }
    }

    private void FillMultiplayerTeamDots(System.Collections.Generic.IReadOnlyList<PlayerMatchResultData> ranking)
    {
        ResetMpTeamDots();

        int count = ranking != null ? ranking.Count : 0;
        for (int i = 0; i < count && i < 4; i++)
        {
            if (i >= mpPlayerTeamDots.Length || mpPlayerTeamDots[i] == null) continue;

            MatchTeam team = (MatchTeam)ranking[i].team;
            mpPlayerTeamDots[i].gameObject.SetActive(true);
            mpPlayerTeamDots[i].color = team == MatchTeam.Blue ? blueTeamColor : redTeamColor;
        }
    }

    private void ResetMpTeamDots()
    {
        if (mpPlayerTeamDots == null) return;

        for (int i = 0; i < mpPlayerTeamDots.Length; i++)
        {
            if (mpPlayerTeamDots[i] == null) continue;
            mpPlayerTeamDots[i].gameObject.SetActive(false);
            mpPlayerTeamDots[i].color = neutralTeamColor;
        }
    }

    private void FillCoop(System.Collections.Generic.IReadOnlyList<PlayerMatchResultData> ranking, MatchSummaryData summary)
    {
        FillRanking(ranking, coopPlayerNameTexts, coopPlayerScoreTexts, coopPlayerTeamTexts, true);

        if (coopTopPlayerText != null) coopTopPlayerText.text = $"{summary.topPlayerName} ({summary.topScore})";
        if (coopMatchTimeText != null) coopMatchTimeText.text = $"{FormatSeconds(summary.matchDurationSeconds)}";
        if (coopLocalCoinsText != null) coopLocalCoinsText.text = $"{summary.localCoinsEarned}";
        if (coopLocalScoreText != null) coopLocalScoreText.text = $"{summary.localScore}";
        if (coopBlueTeamScoreText != null) coopBlueTeamScoreText.text = $"{summary.blueTeamScore}";
        if (coopRedTeamScoreText != null) coopRedTeamScoreText.text = $"{summary.redTeamScore}";
        if (coopLocalTeamText != null) coopLocalTeamText.text = "Your Team: Blue Team";
    }

    private void FillRanking(
        System.Collections.Generic.IReadOnlyList<PlayerMatchResultData> ranking,
        TMP_Text[] nameTexts,
        TMP_Text[] scoreTexts,
        TMP_Text[] teamTexts,
        bool forceBlueTeamLabel)
    {
        for (int i = 0; i < 4; i++)
        {
            if (i < nameTexts.Length && nameTexts[i] != null) nameTexts[i].text = "-";
            if (i < scoreTexts.Length && scoreTexts[i] != null) scoreTexts[i].text = "-";
            if (i < teamTexts.Length && teamTexts[i] != null) teamTexts[i].text = "-";
        }

        int count = ranking != null ? ranking.Count : 0;
        for (int i = 0; i < count && i < 4; i++)
        {
            var row = ranking[i];
            MatchTeam team = (MatchTeam)row.team;

            if (i < nameTexts.Length && nameTexts[i] != null) nameTexts[i].text = row.playerName.ToString();
            if (i < scoreTexts.Length && scoreTexts[i] != null) scoreTexts[i].text = row.score.ToString();

            if (i < teamTexts.Length && teamTexts[i] != null)
                teamTexts[i].text = forceBlueTeamLabel ? "Blue Team" : MatchTeamRules.TeamLabel(team);
        }
    }

    private void ApplyOutcomeBanner(MatchOutcome outcome)
    {
        switch (outcome)
        {
            case MatchOutcome.Victory: SetOutcomeBanners(true, false, false); break;
            case MatchOutcome.Defeat: SetOutcomeBanners(false, true, false); break;
            case MatchOutcome.Draw: SetOutcomeBanners(false, false, true); break;
            default: SetOutcomeBanners(false, false, false); break;
        }
    }

    private void ApplyPvEOutcomeBanner(int stars)
    {
        if (stars >= 1) SetOutcomeBanners(true, false, false);
        else SetOutcomeBanners(false, true, false);
    }

    private void SetOutcomeBanners(bool victory, bool defeat, bool tie)
    {
        if (victoryBanner != null) victoryBanner.SetActive(victory);
        if (defeatBanner != null) defeatBanner.SetActive(defeat);
        if (tieBanner != null) tieBanner.SetActive(tie);
    }

    private void SetSingleStarsVisual(int stars)
    {
        bool valid = stars >= 0;
        if (spZeroStar != null) spZeroStar.SetActive(valid && stars == 0);
        if (spOneStar != null) spOneStar.SetActive(valid && stars == 1);
        if (spTwoStar != null) spTwoStar.SetActive(valid && stars == 2);
        if (spThreeStar != null) spThreeStar.SetActive(valid && stars == 3);
    }

    private void SetCoopStarsVisual(int stars)
    {
        bool valid = stars >= 0;
        if (coopZeroStar != null) coopZeroStar.SetActive(valid && stars == 0);
        if (coopOneStar != null) coopOneStar.SetActive(valid && stars == 1);
        if (coopTwoStar != null) coopTwoStar.SetActive(valid && stars == 2);
        if (coopThreeStar != null) coopThreeStar.SetActive(valid && stars == 3);
    }

    private void FillSingle(MatchSummaryData summary)
    {
        string localName = ResolveLocalPlayerName();

        if (spPlayerNameText != null) spPlayerNameText.text = localName;
        if (spScoreText != null) spScoreText.text = $"{summary.localScore}";
        if (spMatchTimeText != null) spMatchTimeText.text = $"{FormatSeconds(summary.matchDurationSeconds)}";
        if (spCoinsText != null) spCoinsText.text = $"{summary.localCoinsEarned}";
        if (spStarsText != null) spStarsText.text = $"{summary.localStars}/3";
    }

    private void ShowDiamondPopupIfNeeded(int diamonds, string reason)
    {
        if (diamondPopupRoot == null) return;

        if (diamonds <= 0)
        {
            diamondPopupRoot.SetActive(false);
            return;
        }

        diamondPopupRoot.SetActive(true);

        if (diamondPopupText != null)
        {
            var sb = new StringBuilder();
            sb.Append($"Diamonds Earned: +{diamonds}");
            if (!string.IsNullOrWhiteSpace(reason))
                sb.Append($"\nReason: {reason}");
            diamondPopupText.text = sb.ToString();
        }
    }

    private string ResolveLocalPlayerName()
    {
        if (GameMultiplayerManager.Instance != null)
        {
            var pd = GameMultiplayerManager.Instance.GetPlayerData();
            if (!pd.playerName.IsEmpty) return pd.playerName.ToString();
        }
        return "Player";
    }

    private string FormatSeconds(float totalSeconds)
    {
        int sec = Mathf.Max(0, Mathf.RoundToInt(totalSeconds));
        int mm = sec / 60;
        int ss = sec % 60;
        return $"{mm:00}:{ss:00}";
    }

    private void UpdateHourglassStepFlip()
    {
        if (returnTimerHourglass == null) return;

        float flipDur = Mathf.Max(0.01f, hourglassFlipDuration);
        float waitDur = Mathf.Max(0f, hourglassWaitBetweenFlips);

        if (!hourglassIsFlipping)
        {
            // waiting phase
            hourglassWaitT += Time.unscaledDeltaTime;
            if (hourglassWaitT >= waitDur)
            {
                hourglassWaitT = 0f;
                hourglassIsFlipping = true;
                hourglassFlipT = 0f;

                hourglassFromAngle = hourglassAngleZ;
                hourglassToAngle = hourglassFromAngle + 180f;
            }

            return;
        }

        // flipping phase
        hourglassFlipT += Time.unscaledDeltaTime;
        float p = Mathf.Clamp01(hourglassFlipT / flipDur);

        float eased = Mathf.SmoothStep(0f, 1f, p);
        hourglassAngleZ = Mathf.LerpUnclamped(hourglassFromAngle, hourglassToAngle, eased);
        returnTimerHourglass.localRotation = Quaternion.Euler(0f, 0f, hourglassAngleZ);

        if (p >= 1f)
        {
            hourglassAngleZ = hourglassToAngle;
            returnTimerHourglass.localRotation = Quaternion.Euler(0f, 0f, hourglassAngleZ);
            hourglassIsFlipping = false;
            hourglassFlipT = 0f;
        }
    }

    private void ResetHourglass()
    {
        if (returnTimerHourglass == null) return;

        hourglassAngleZ = 0f;
        hourglassFlipT = 0f;
        hourglassWaitT = 0f;
        hourglassIsFlipping = false;
        hourglassFromAngle = 0f;
        hourglassToAngle = 0f;

        returnTimerHourglass.localRotation = Quaternion.identity;
    }
}