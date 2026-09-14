using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameClockPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    [SerializeField] private GameManager gameManager;

    [Header("View")]
    [SerializeField] private Slider timerSlider;          // optional
    [SerializeField] private Image timerImage;            // optional 
    [SerializeField] private TMP_Text timeText;           // format 00:00
    [SerializeField] private RectTransform sandWatchIcon; // optional - El flip

    [Header("Behavior")]
    [SerializeField] private bool decreasing = true;      // true = countdown
    [SerializeField] private bool resetWhenNotPlaying = true;
    [SerializeField] private float flipDuration = 0.25f;
    [SerializeField] private float flipDegrees = 180f;

    private bool ticking;
    private int lastDisplayedMinute = int.MinValue;
    private bool flipping;

    private void Awake()
    {
        if (gameManager == null)
        {
            Debug.LogError($"{nameof(GameClockPresenter)} missing GameManager.", this);
            enabled = false;
            return;
        }

        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = 1f;
            timerSlider.wholeNumbers = false;
            timerSlider.interactable = false;
            timerSlider.value = decreasing ? 1f : 0f;
        }

        if (timerImage != null)
            timerImage.fillAmount = decreasing ? 1f : 0f;

        if (timeText != null)
            timeText.text = decreasing ? "00:00" : "00:00";
    }

    private void OnEnable()
    {
        gameManager.OnStateChanged += GameManager_OnStateChanged;
        RefreshTickingState();
        RefreshVisual(force: true);
    }

    private void OnDisable()
    {
        if (gameManager != null)
            gameManager.OnStateChanged -= GameManager_OnStateChanged;
    }

    private void Update()
    {
        if (!ticking) return;
        RefreshVisual(force: false);
    }

    private void GameManager_OnStateChanged(object sender, EventArgs e)
    {
        RefreshTickingState();
        RefreshVisual(force: true);
    }

    private void RefreshTickingState()
    {
        ticking = gameManager.IsGamePlaying();
    }

    private void RefreshVisual(bool force)
    {
        float duration = Mathf.Max(0.0001f, gameManager.GetPlayingDuration());
        float remaining = Mathf.Clamp(gameManager.GetRemainingTimeSeconds(), 0f, duration);
        float elapsed01 = 1f - (remaining / duration); // 0..1 elapsed
        float shown01 = decreasing ? (1f - elapsed01) : elapsed01;

        if (timerSlider != null) timerSlider.value = shown01;
        if (timerImage != null) timerImage.fillAmount = shown01;

        int totalSec = Mathf.CeilToInt(decreasing ? remaining : (elapsed01 * duration));
        if (!ticking && resetWhenNotPlaying) totalSec = decreasing ? Mathf.CeilToInt(duration) : 0;

        int minutes = Mathf.Max(0, totalSec / 60);
        int seconds = Mathf.Max(0, totalSec % 60);

        if (timeText != null)
            timeText.text = $"{minutes:00}:{seconds:00}";

        // Flip only on minute change ...
        if (decreasing && ticking)
        {
            if (lastDisplayedMinute != int.MinValue && minutes < lastDisplayedMinute)
            {
                if (sandWatchIcon != null && !flipping)
                    StartCoroutine(FlipSandWatch());
            }

            lastDisplayedMinute = minutes;
        }
        else if (force)
        {
            lastDisplayedMinute = minutes;
        }
    }

    private IEnumerator FlipSandWatch()
    {
        flipping = true;

        Quaternion from = sandWatchIcon.localRotation;
        Quaternion to = from * Quaternion.Euler(0f, 0f, flipDegrees);

        float t = 0f;
        while (t < flipDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flipDuration);
            sandWatchIcon.localRotation = Quaternion.Slerp(from, to, k);
            yield return null;
        }

        sandWatchIcon.localRotation = to;
        flipping = false;
    }
}