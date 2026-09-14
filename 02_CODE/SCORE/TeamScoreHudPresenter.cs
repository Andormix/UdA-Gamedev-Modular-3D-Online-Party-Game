using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using Unity.Netcode;
using UnityEngine.Audio;

public class TeamScoreHudPresenter : MonoBehaviour
{
    [Header("Panel Roots")]
    [SerializeField] private GameObject bluePanelRoot;
    [SerializeField] private GameObject redPanelRoot;

    [Header("Blue UI")]
    [SerializeField] private TMP_Text blueScoreText;
    [SerializeField] private TMP_Text blueComboText;
    [SerializeField] private Slider blueComboSlider;

    [Header("Red UI")]
    [SerializeField] private TMP_Text redScoreText;
    [SerializeField] private TMP_Text redComboText;
    [SerializeField] private Slider redComboSlider;

    [Header("Formatting")]
    [SerializeField] private string comboPrefix = "x";
    [SerializeField] private bool oneDecimalCombo = true;

    [Header("Refresh")]
    [SerializeField] private float refreshInterval = 0.1f; // 10 Hz

    [Header("Score Count-Up FX")]
    [SerializeField] private float scoreAnimDuration = 0.25f;
    [SerializeField] private int minStepPerFrame = 1;

    [Header("Score Gain Popup")]
    [SerializeField] private ScoreGainPopupUI popupPrefab;
    [SerializeField] private RectTransform bluePopupAnchor;
    [SerializeField] private RectTransform redPopupAnchor;
    [SerializeField] private Color bluePopupColor = new Color(0.35f, 0.75f, 1f, 1f);
    [SerializeField] private Color redPopupColor = new Color(1f, 0.4f, 0.4f, 1f);

    [Header("Team UI Audio (Local Team Only)")]
    [SerializeField] private bool enableTeamUiAudio = true;
    [SerializeField] private AudioSource teamUiAudioSource;
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField] private AudioClip[] coinsEarnedClips;
    [SerializeField] private AudioClip[] comboIncreasedClips;
    [SerializeField] private AudioClip[] comboResetClips;
    [SerializeField, Range(0f, 1f)] private float coinsEarnedVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float comboIncreasedVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] private float comboResetVolume = 0.8f;

    private const float MinCombo = 1f;
    private const float MaxCombo = 3f;

    private float nextRefreshTime;

    private int lastBlueScore = int.MinValue;
    private int lastRedScore = int.MinValue;
    private float lastBlueCombo = -1f;
    private float lastRedCombo = -1f;
    private bool lastIsMultiplayer;

    private int displayedBlueScore;
    private int displayedRedScore;

    private Coroutine blueScoreAnim;
    private Coroutine redScoreAnim;
    private Coroutine blueComboPop;
    private Coroutine redComboPop;
    private MatchTeam _localTeam = MatchTeam.Blue;
    private bool _localTeamResolved;

    private void Awake()
    {
        SetupSlider(blueComboSlider);
        SetupSlider(redComboSlider);
        ResolveAudioSource();
    }

    private void OnEnable()
    {
        ResolveLocalTeam();
        ForceRefresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime) return;
        nextRefreshTime = Time.unscaledTime + refreshInterval;
        RefreshIfChanged();
    }

    private void ForceRefresh()
    {
        lastBlueScore = int.MinValue;
        lastRedScore = int.MinValue;
        lastBlueCombo = -1f;
        lastRedCombo = -1f;
        lastIsMultiplayer = !lastIsMultiplayer;

        displayedBlueScore = 0;
        displayedRedScore = 0;

        if (blueScoreText != null) blueScoreText.text = "0";
        if (redScoreText != null) redScoreText.text = "0";

        RefreshIfChanged();
    }

    private void RefreshIfChanged()
    {
        bool isMultiplayer = GameMultiplayerManager.playMultiplayer && !CoopCampaignSessionContext.IsCoopCampaignRun;

        if (bluePanelRoot != null) bluePanelRoot.SetActive(true);
        if (redPanelRoot != null) redPanelRoot.SetActive(isMultiplayer);

        if (ScoreManager.Instance == null || NetworkManagerOrIdsMissing())
        {
            ApplyBlue(0, 1f);
            ApplyRed(0, 1f);
            return;
        }

        int blueScore = ScoreManager.Instance.GetTeamScore(MatchTeam.Blue);
        int redScore = isMultiplayer ? ScoreManager.Instance.GetTeamScore(MatchTeam.Red) : 0;

        float blueCombo = GetTeamMaxCombo(MatchTeam.Blue);
        float redCombo = isMultiplayer ? GetTeamMaxCombo(MatchTeam.Red) : 1f;

        bool changed =
            isMultiplayer != lastIsMultiplayer ||
            blueScore != lastBlueScore ||
            redScore != lastRedScore ||
            !Mathf.Approximately(blueCombo, lastBlueCombo) ||
            !Mathf.Approximately(redCombo, lastRedCombo);

        if (!changed) return;

        ApplyBlue(blueScore, blueCombo);
        ApplyRed(redScore, redCombo);

        lastIsMultiplayer = isMultiplayer;
        lastBlueScore = blueScore;
        lastRedScore = redScore;
        lastBlueCombo = blueCombo;
        lastRedCombo = redCombo;
    }

    private bool NetworkManagerOrIdsMissing()
    {
        return Unity.Netcode.NetworkManager.Singleton == null ||
               Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds == null;
    }

    private float GetTeamMaxCombo(MatchTeam team)
    {
        var ids = Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds;
        bool isCoop = CoopCampaignSessionContext.IsCoopCampaignRun;
        float max = 1f;

        for (int i = 0; i < ids.Count; i++)
        {
            ulong clientId = ids[i];
            MatchTeam t = isCoop ? MatchTeam.Blue : TeamInteractionRules.ResolveTeam(clientId);
            if (t != team) continue;

            float c = ScoreManager.Instance.GetComboForClient(clientId);
            if (c > max) max = c;
        }

        return Mathf.Clamp(max, MinCombo, MaxCombo);
    }

    private void ApplyBlue(int targetScore, float combo)
    {
        bool hadPrevCombo = lastBlueCombo >= MinCombo - 0.001f;
        bool comboIncreased = hadPrevCombo && combo > lastBlueCombo + 0.001f;
        bool comboReset = hadPrevCombo && combo < lastBlueCombo - 0.001f;

        int delta = (lastBlueScore == int.MinValue) ? 0 : (targetScore - lastBlueScore);
        if (delta > 0) SpawnPopup(delta, true);
        AnimateScoreBlue(targetScore);

        if (IsLocalTeam(MatchTeam.Blue))
        {
            if (delta > 0) PlayTeamUiClipFromPool(coinsEarnedClips, coinsEarnedVolume);
            if (comboIncreased) PlayTeamUiClipFromPool(comboIncreasedClips, comboIncreasedVolume);
            else if (comboReset) PlayTeamUiClipFromPool(comboResetClips, comboResetVolume);
        }

        if (blueComboText != null) blueComboText.text = comboPrefix + FormatCombo(combo);
        if (blueComboSlider != null) blueComboSlider.value = ComboTo01(combo);

        if (comboIncreased && blueComboText != null)
        {
            if (blueComboPop != null) StopCoroutine(blueComboPop);
            blueComboPop = StartCoroutine(PopTextFx(blueComboText.transform));
        }
    }

    private void ApplyRed(int targetScore, float combo)
    {
        bool hadPrevCombo = lastRedCombo >= MinCombo - 0.001f;
        bool comboIncreased = hadPrevCombo && combo > lastRedCombo + 0.001f;
        bool comboReset = hadPrevCombo && combo < lastRedCombo - 0.001f;

        int delta = (lastRedScore == int.MinValue) ? 0 : (targetScore - lastRedScore);
        if (delta > 0) SpawnPopup(delta, false);
        AnimateScoreRed(targetScore);

        if (IsLocalTeam(MatchTeam.Red))
        {
            if (delta > 0) PlayTeamUiClipFromPool(coinsEarnedClips, coinsEarnedVolume);
            if (comboIncreased) PlayTeamUiClipFromPool(comboIncreasedClips, comboIncreasedVolume);
            else if (comboReset) PlayTeamUiClipFromPool(comboResetClips, comboResetVolume);
        }

        if (redComboText != null) redComboText.text = comboPrefix + FormatCombo(combo);
        if (redComboSlider != null) redComboSlider.value = ComboTo01(combo);

        if (comboIncreased && redComboText != null)
        {
            if (redComboPop != null) StopCoroutine(redComboPop);
            redComboPop = StartCoroutine(PopTextFx(redComboText.transform));
        }
    }

    private void AnimateScoreBlue(int target)
    {
        if (blueScoreText == null) return;

        if (blueScoreAnim != null) StopCoroutine(blueScoreAnim);
        blueScoreAnim = StartCoroutine(AnimateScoreRoutine(
            () => displayedBlueScore,
            v => { displayedBlueScore = v; blueScoreText.text = v.ToString(); },
            target
        ));
    }

    private void AnimateScoreRed(int target)
    {
        if (redScoreText == null) return;

        if (redScoreAnim != null) StopCoroutine(redScoreAnim);
        redScoreAnim = StartCoroutine(AnimateScoreRoutine(
            () => displayedRedScore,
            v => { displayedRedScore = v; redScoreText.text = v.ToString(); },
            target
        ));
    }

    private IEnumerator AnimateScoreRoutine(System.Func<int> getCurrent, System.Action<int> setCurrent, int target)
    {
        int start = getCurrent();
        if (start == target)
        {
            setCurrent(target);
            yield break;
        }

        float duration = Mathf.Max(0.01f, scoreAnimDuration);
        float t = 0f;
        int lastValue = start;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(t / duration);

            int value = Mathf.RoundToInt(Mathf.Lerp(start, target, a));

            // ensure visible progress
            if (Mathf.Abs(value - lastValue) < minStepPerFrame && value != target)
            {
                value = lastValue + (target > start ? minStepPerFrame : -minStepPerFrame);
                value = target > start ? Mathf.Min(value, target) : Mathf.Max(value, target);
            }

            setCurrent(value);
            lastValue = value;
            yield return null;
        }

        setCurrent(target);
    }

    private void SetupSlider(Slider s)
    {
        if (s == null) return;
        s.minValue = 0f;
        s.maxValue = 1f;
        s.wholeNumbers = false;
        s.interactable = false;
        s.value = 0f;
    }

    private float ComboTo01(float combo)
    {
        combo = Mathf.Clamp(combo, MinCombo, MaxCombo);
        return Mathf.InverseLerp(MinCombo, MaxCombo, combo);
    }

    private string FormatCombo(float combo)
    {
        return oneDecimalCombo ? combo.ToString("0.0") : combo.ToString("0");
    }

    private IEnumerator PopTextFx(Transform t)
    {
        if (t == null) yield break;

        Vector3 baseScale = Vector3.one;
        t.localScale = baseScale;

        float upTime = 0.08f;
        float downTime = 0.10f;
        Vector3 peak = baseScale * 1.18f;

        float k = 0f;
        while (k < upTime)
        {
            k += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(k / upTime);
            t.localScale = Vector3.Lerp(baseScale, peak, a);
            yield return null;
        }

        k = 0f;
        while (k < downTime)
        {
            k += Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(k / downTime);
            t.localScale = Vector3.Lerp(peak, baseScale, a);
            yield return null;
        }

        t.localScale = baseScale;
    }

    private void SpawnPopup(int delta, bool blue)
    {
        if (popupPrefab == null) return;

        RectTransform anchor = blue ? bluePopupAnchor : redPopupAnchor;
        if (anchor == null) return;

        var popup = Instantiate(popupPrefab, anchor);
        popup.transform.localPosition = Vector3.zero;
        popup.Setup(delta, blue ? bluePopupColor : redPopupColor);
    }

    private bool IsLocalTeam(MatchTeam team)
    {
        if (CoopCampaignSessionContext.IsCoopCampaignRun || !GameMultiplayerManager.playMultiplayer)
            return team == MatchTeam.Blue;

        ResolveLocalTeam();
        return _localTeamResolved && _localTeam == team;
    }

    private void ResolveLocalTeam()
    {
        if (CoopCampaignSessionContext.IsCoopCampaignRun || !GameMultiplayerManager.playMultiplayer)
        {
            _localTeam = MatchTeam.Blue;
            _localTeamResolved = true;
            return;
        }

        NetworkManager nm = NetworkManager.Singleton;
        if (nm == null)
        {
            _localTeamResolved = false;
            return;
        }

        _localTeam = TeamInteractionRules.ResolveTeam(nm.LocalClientId);
        _localTeamResolved = true;
    }

    private void ResolveAudioSource()
    {
        if (teamUiAudioSource == null)
            teamUiAudioSource = GetComponent<AudioSource>();
        if (teamUiAudioSource == null)
            teamUiAudioSource = gameObject.AddComponent<AudioSource>();

        teamUiAudioSource.playOnAwake = false;
        teamUiAudioSource.loop = false;
        teamUiAudioSource.spatialBlend = 0f;
        if (outputMixerGroup != null)
            teamUiAudioSource.outputAudioMixerGroup = outputMixerGroup;
    }

    private void PlayTeamUiClipFromPool(AudioClip[] clipPool, float volume)
    {
        if (!enableTeamUiAudio) return;
        if (teamUiAudioSource == null) return;
        AudioClip clip = PickRandomClip(clipPool);
        if (clip == null) return;
        teamUiAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private static AudioClip PickRandomClip(AudioClip[] clipPool)
    {
        if (clipPool == null || clipPool.Length == 0) return null;
        int start = Random.Range(0, clipPool.Length);
        for (int i = 0; i < clipPool.Length; i++)
        {
            AudioClip clip = clipPool[(start + i) % clipPool.Length];
            if (clip != null) return clip;
        }
        return null;
    }
}