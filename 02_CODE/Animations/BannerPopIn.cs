using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BannerRevealWithDim : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Image screenDimImage;       // ScreenDim (Imagte de fons)
    [SerializeField] private RectTransform popupRoot;    // Popup

    [Header("Show Animation")]
    [SerializeField] private float showDuration = 0.24f;
    [SerializeField] [Range(0f, 1f)] private float dimTargetAlpha = 0.65f;
    [SerializeField] private float startScale = 0.05f;
    [SerializeField] private float overshootScale = 1.08f;
    [SerializeField] private AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Hide Animation")]
    [SerializeField] private bool hideOnClick = true;
    [SerializeField] private float hideDuration = 0.14f;
    [SerializeField] private float hideScale = 0.02f;

    [Header("Auto")]
    [SerializeField] private bool playOnEnable = true;

    private Vector3 _popupBaseScale = Vector3.one;
    private Coroutine _co;

    private void Awake()
    {
        if (popupRoot != null) _popupBaseScale = popupRoot.localScale;
        SetDimAlpha(0f);
    }

    private void OnEnable()
    {
        if (!playOnEnable) return;
        PlayShow();
    }

    public void PlayShow()
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(ShowRoutine());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!hideOnClick) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(HideRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        SetDimAlpha(0f);
        if (popupRoot != null) popupRoot.localScale = _popupBaseScale * startScale;

        float t = 0f;
        float upDuration = showDuration * 0.75f;
        float settleDuration = showDuration * 0.25f;

        while (t < upDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / upDuration);
            float k = ease.Evaluate(p);

            SetDimAlpha(Mathf.LerpUnclamped(0f, dimTargetAlpha, k));

            if (popupRoot != null)
                popupRoot.localScale = Vector3.LerpUnclamped(_popupBaseScale * startScale, _popupBaseScale * overshootScale, k);

            yield return null;
        }

        t = 0f;
        Vector3 from = popupRoot != null ? popupRoot.localScale : Vector3.one;
        while (t < settleDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / settleDuration);
            float k = ease.Evaluate(p);

            SetDimAlpha(Mathf.LerpUnclamped(dimTargetAlpha * 0.98f, dimTargetAlpha, k));

            if (popupRoot != null)
                popupRoot.localScale = Vector3.LerpUnclamped(from, _popupBaseScale, k);

            yield return null;
        }

        if (popupRoot != null) popupRoot.localScale = _popupBaseScale;
        SetDimAlpha(dimTargetAlpha);
        _co = null;
    }

    private IEnumerator HideRoutine()
    {
        float t = 0f;
        float startAlpha = GetDimAlpha();
        Vector3 from = popupRoot != null ? popupRoot.localScale : Vector3.one;
        Vector3 to = _popupBaseScale * hideScale;

        while (t < hideDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / hideDuration);
            float k = ease.Evaluate(p);

            SetDimAlpha(Mathf.LerpUnclamped(startAlpha, 0f, k));

            if (popupRoot != null)
                popupRoot.localScale = Vector3.LerpUnclamped(from, to, k);

            yield return null;
        }

        SetDimAlpha(0f);
        if (popupRoot != null) popupRoot.localScale = _popupBaseScale;

        gameObject.SetActive(false);
        _co = null;
    }

    private void SetDimAlpha(float a)
    {
        if (screenDimImage == null) return;
        var c = screenDimImage.color;
        c.a = Mathf.Clamp01(a);
        screenDimImage.color = c;
    }

    private float GetDimAlpha()
    {
        if (screenDimImage == null) return 0f;
        return screenDimImage.color.a;
    }
}