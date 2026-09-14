using System;
using UnityEngine;
using UnityEngine.UI;

public class WorkstationProgressBarUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider slider;                
    [SerializeField] private Image fallbackFillImage;  // old image support (for regression if i need it)

    [Header("Source")]
    [SerializeField] private GameObject hasProgressBarGameObject;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenZero = true;
    [SerializeField] private bool hideWhenFull = true;

    [Header("Smoothing")]
    [Tooltip("How fast the UI interpolates to the replicated progress target.")]
    [SerializeField] private bool enableSmoothing = true;
    [SerializeField] private float smoothingUnitsPerSecond = 6f;
    [SerializeField] private float snapEpsilon = 0.0025f;

    private InterfaceProgressBar progressBar;
    private float _targetProgress;
    private float _displayProgress;
    private Graphic[] _graphics;
    private bool _isVisible;

    private void Start()
    {
        if (hasProgressBarGameObject == null)
        {
            Debug.LogError("[WorkstationProgressBarUI] hasProgressBarGameObject is null.", this);
            enabled = false;
            return;
        }

        progressBar = hasProgressBarGameObject.GetComponent<InterfaceProgressBar>();
        if (progressBar == null)
        {
            Debug.LogError($"[WorkstationProgressBarUI] {hasProgressBarGameObject.name} does not implement InterfaceProgressBar.", this);
            enabled = false;
            return;
        }

        progressBar.OnProgressChanged += OnProgressChanged;
        _graphics = GetComponentsInChildren<Graphic>(true);
        _isVisible = true;

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 0f;
            slider.interactable = false; // pure display
        }

        if (fallbackFillImage != null)
            fallbackFillImage.fillAmount = 0f;

        _targetProgress = 0f;
        _displayProgress = 0f;
        ApplyVisual(0f);
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

    private void OnDestroy()
    {
        if (progressBar != null)
            progressBar.OnProgressChanged -= OnProgressChanged;
    }

    private void OnProgressChanged(object sender, InterfaceProgressBar.OnProgressChangedEventArgs e)
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
        if (slider != null) slider.value = progress;
        if (fallbackFillImage != null) fallbackFillImage.fillAmount = progress;
        ApplyVisibility(progress);
    }

    private void ApplyVisibility(float p)
    {
        bool visible = true;
        if (hideWhenZero && p <= 0f) visible = false;
        if (hideWhenFull && p >= 1f) visible = false;

        if (_isVisible == visible) return;
        _isVisible = visible;
        SetGraphicsVisible(visible);
    }

    private void SetGraphicsVisible(bool visible)
    {
        if (_graphics == null || _graphics.Length == 0)
            _graphics = GetComponentsInChildren<Graphic>(true);

        for (int i = 0; i < _graphics.Length; i++)
        {
            if (_graphics[i] != null)
                _graphics[i].enabled = visible;
        }
    }

    private void OnValidate()
    {
        smoothingUnitsPerSecond = Mathf.Clamp(smoothingUnitsPerSecond, 0.1f, 40f);
        snapEpsilon = Mathf.Clamp(snapEpsilon, 0.0001f, 0.1f);
    }
}