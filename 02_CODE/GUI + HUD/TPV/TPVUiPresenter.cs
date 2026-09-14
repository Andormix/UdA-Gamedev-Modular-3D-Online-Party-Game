using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Audio;

public sealed class TPVUiPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    [SerializeField] private UserInterfaceManager ui;

    [Header("Optional world object toggle")]
    [SerializeField] private GameObject proximityObject; // object to enable/disable near payment table
    [SerializeField] private bool hideObjectOnStart = true;

    [Header("View")]
    [SerializeField] private TMP_Text display;
    [SerializeField] private Button[] digitButtons; // size 10
    [SerializeField] private Button dotButton;
    [SerializeField] private Button eraseButton; // "X"
    [SerializeField] private Button clearButton;
    [SerializeField] private Button enterButton;

    [Header("TPV Audio Feedback (local)")]
    [SerializeField] private bool enableTouchFeedback = true;
    [SerializeField] private bool enableErrorFeedback = true;
    [SerializeField] private bool enableSuccessFeedback = true;
    [SerializeField] private AudioClip[] touchFeedbackClips;
    [SerializeField] private AudioClip[] errorFeedbackClips;
    [SerializeField] private AudioClip[] successFeedbackClips;
    [SerializeField, Range(0f, 1f)] private float touchFeedbackVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float errorFeedbackVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float successFeedbackVolume = 1f;
    [SerializeField] private AudioSource uiAudioSource;
    [SerializeField] private AudioMixerGroup uiSfxMixerGroup;

    private string current = "";
    private Player localPlayer;
    private PlayerInteractions localInteractions;

    private const int MAXCHARACTERS = 5;
    private const float MinTableCacheRefreshSeconds = 0.1f;

    [SerializeField] private float paymentTableProximityDistance = 2.5f;
    [SerializeField] private float tableCacheRefreshSeconds = 1f;

    private TableNetSync[] tableCache = new TableNetSync[0];
    private float nextTableCacheRefreshTime;

    // RNF 60FPS: Caches to avoid repeated show/hide or SetActive calls every frame
    private bool lastShowState = false;
    private bool initializedShowState = false;

    private void Awake()
    {
        if (ui == null || display == null)
        {
            Debug.LogError($"{nameof(TPVUiPresenter)} missing references.", this);
            enabled = false;
            return;
        }

        // Optional: start hiddden
        if (proximityObject != null && hideObjectOnStart)
            proximityObject.SetActive(false);

        for (int i = 0; i < digitButtons.Length; i++)
        {
            int n = i;
            digitButtons[i].onClick.AddListener(() => AppendDigit(n));
        }

        dotButton.onClick.AddListener(AppendDot);
        eraseButton.onClick.AddListener(EraseLast);
        clearButton.onClick.AddListener(ClearAll);
        enterButton.onClick.AddListener(TryEnterPayment);

        ResolveAudioSource();
    }

    private void OnEnable()
    {
        PlayerEvents.OnLocalPlayerSpawned += OnLocalPlayerSpawned;
        TableNetSync.OnLocalPaymentFeedback += HandleLocalPaymentFeedback;
    }

    private void OnDisable()
    {
        PlayerEvents.OnLocalPlayerSpawned -= OnLocalPlayerSpawned;
        TableNetSync.OnLocalPaymentFeedback -= HandleLocalPaymentFeedback;
    }

    private void OnLocalPlayerSpawned(PlayerInteractions interactions)
    {
        localInteractions = interactions;
        localPlayer = interactions.GetComponent<Player>();
        Debug.Log("TPV Getting player interactions");
    }

    private void Update()
    {
        bool show = ShouldShowTPV();

        // only apply if state changed (or first frame)
        if (!initializedShowState || show != lastShowState)
        {
            initializedShowState = true;
            lastShowState = show;

            if (show) ui.Show(UserInterfaceManager.UIPage.TPV);
            else ui.Hide(UserInterfaceManager.UIPage.TPV);

            if (proximityObject != null)
                proximityObject.SetActive(show);
        }
    }

    private bool ShouldShowTPV()
    {
        if (localPlayer == null) return false;
        if (!localPlayer.IsOwner) return false; // local UI only

        var carry = localPlayer.GetComponent<PlayerCarryNet>();
        if (carry == null) return false;
        if (!carry.IsHoldingTPV()) return false;

        return IsNearAwaitingPaymentTable();
    }

    private bool IsNearAwaitingPaymentTable()
    {
        if (localPlayer == null) return false;

        if (Time.time >= nextTableCacheRefreshTime)
        {
            tableCache = FindObjectsByType<TableNetSync>(FindObjectsSortMode.None);
            nextTableCacheRefreshTime = Time.time + Mathf.Max(MinTableCacheRefreshSeconds, tableCacheRefreshSeconds);
        }

        float maxDistSq = Mathf.Max(0f, paymentTableProximityDistance * paymentTableProximityDistance);
        Vector3 playerPos = localPlayer.transform.position;

        for (int i = 0; i < tableCache.Length; i++)
        {
            var table = tableCache[i];
            if (table == null) continue;
            if (table.CurrentPhase != WorkstationPhaseId.AwaitingPayment) continue;

            if ((table.transform.position - playerPos).sqrMagnitude <= maxDistSq)
                return true;
        }

        return false;
    }

    private void AppendDigit(int digit)
    {
        PlayTouchFeedback();
        if (current.Length >= MAXCHARACTERS) return;
        current += digit.ToString();
        display.text = current;
    }

    private void AppendDot()
    {
        PlayTouchFeedback();
        if (current.Length >= MAXCHARACTERS) return;
        if (!current.Contains(".")) current += ".";
        display.text = current;
    }

    private void EraseLast()
    {
        PlayTouchFeedback();
        if (current.Length <= 0) return;
        current = current.Substring(0, current.Length - 1);
        display.text = current;
    }

    private void ClearAll()
    {
        PlayTouchFeedback();
        current = "";
        display.text = current;
    }

    private void TryEnterPayment()
    {
        PlayTouchFeedback();
        if (localInteractions == null) return;

        var selected = localInteractions.GetSelected();
        if (selected == null) return;

        if (!selected.TryGetComponent<TableNetSync>(out var tableNet))
            return;

        if (tableNet.CurrentPhase != WorkstationPhaseId.AwaitingPayment)
            return;

        Debug.Log("Sending to server current of: " + current);
        tableNet.RequestPayment(current);
    }

    private void HandleLocalPaymentFeedback(TableNetSync.PaymentFeedbackResult result)
    {
        switch (result)
        {
            case TableNetSync.PaymentFeedbackResult.Failed:
                PlayErrorFeedback();
                break;
            case TableNetSync.PaymentFeedbackResult.Succeeded:
                PlaySuccessFeedback();
                break;
        }
    }

    private void PlayTouchFeedback()
    {
        if (!enableTouchFeedback) return;
        PlayFromPool(touchFeedbackClips, touchFeedbackVolume);
    }

    private void PlayErrorFeedback()
    {
        if (!enableErrorFeedback) return;
        PlayFromPool(errorFeedbackClips, errorFeedbackVolume);
    }

    private void PlaySuccessFeedback()
    {
        if (!enableSuccessFeedback) return;
        PlayFromPool(successFeedbackClips, successFeedbackVolume);
    }

    private void PlayFromPool(AudioClip[] pool, float volume)
    {
        if (uiAudioSource == null) return;
        if (pool == null || pool.Length == 0) return;

        AudioClip clip = PickRandomClip(pool);
        if (clip == null) return;

        uiAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private void ResolveAudioSource()
    {
        if (uiAudioSource == null)
            uiAudioSource = GetComponent<AudioSource>();
        if (uiAudioSource == null)
            uiAudioSource = gameObject.AddComponent<AudioSource>();

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;
        if (uiSfxMixerGroup != null)
            uiAudioSource.outputAudioMixerGroup = uiSfxMixerGroup;
    }

    private static AudioClip PickRandomClip(AudioClip[] pool)
    {
        if (pool == null || pool.Length == 0) return null;
        int start = Random.Range(0, pool.Length);
        for (int i = 0; i < pool.Length; i++)
        {
            AudioClip candidate = pool[(start + i) % pool.Length];
            if (candidate != null) return candidate;
        }
        return null;
    }
}