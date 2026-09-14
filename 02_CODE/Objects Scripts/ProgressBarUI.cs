using System;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBarUI : MonoBehaviour
{
    [SerializeField] private Image barImage;

    //[SerializeField] private InterfaceProgressBar progressBar;  (WORKAROUND)
    private InterfaceProgressBar progressBar;
    [SerializeField] private GameObject hasProgressBarGameObject;

    [Header("Smoothing")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothingUnitsPerSecond = 6f;
    [SerializeField] private float snapEpsilon = 0.0025f;

    private float _targetProgress;
    private float _displayProgress;
    private bool _isVisible;

    private void Start()
    {
        if (hasProgressBarGameObject == null)
        {
            Debug.LogError("[ProgressBarUI] hasProgressBarGameObject is null.", this);
            enabled = false;
            return;
        }

        progressBar = hasProgressBarGameObject.GetComponent<InterfaceProgressBar>();

        if(progressBar == null)
        {
            Debug.LogError("Game Object" + hasProgressBarGameObject + " does not implement a progress bar");
            enabled = false;
            return;
        }

        progressBar.OnProgressChanged += progressBar_OnProgressChanged;
        _targetProgress = 0f;
        _displayProgress = 0f;
        barImage.fillAmount = 0f;
        Hide();
    }

    private void OnDestroy()
    {
        if (progressBar != null)
            progressBar.OnProgressChanged -= progressBar_OnProgressChanged;
    }

    private void Update()
    {
        if (!enableSmoothing) return;

        if (Mathf.Abs(_displayProgress - _targetProgress) <= snapEpsilon)
        {
            if (!Mathf.Approximately(_displayProgress, _targetProgress))
            {
                _displayProgress = _targetProgress;
                ApplyVisual(_displayProgress);
            }
            return;
        }

        float speed = Mathf.Max(0.1f, smoothingUnitsPerSecond);
        _displayProgress = Mathf.MoveTowards(_displayProgress, _targetProgress, speed * Time.deltaTime);
        ApplyVisual(_displayProgress);
    }

    private void progressBar_OnProgressChanged(object sender, InterfaceProgressBar.OnProgressChangedEventArgs e)
    {
        _targetProgress = Mathf.Clamp01(e.progressNormalized);
        if (!enableSmoothing)
        {
            _displayProgress = _targetProgress;
            ApplyVisual(_displayProgress);
            return;
        }

        if (_targetProgress <= 0f || _targetProgress >= 1f)
        {
            _displayProgress = _targetProgress;
            ApplyVisual(_displayProgress);
        }
    }

    private void ApplyVisual(float progress)
    {
        barImage.fillAmount = progress;
        if (progress <= 0f || progress >= 1f) Hide();
        else Show();
    }

    private void Show()
    {
        if (_isVisible) return;
        _isVisible = true;
        if (barImage != null) barImage.enabled = true;
    }

    private void Hide()
    {
        if (!_isVisible && barImage != null && !barImage.enabled) return;
        _isVisible = false;
        if (barImage != null) barImage.enabled = false;
    }

    private void OnValidate()
    {
        smoothingUnitsPerSecond = Mathf.Clamp(smoothingUnitsPerSecond, 0.1f, 40f);
        snapEpsilon = Mathf.Clamp(snapEpsilon, 0.0001f, 0.1f);
    }
}
