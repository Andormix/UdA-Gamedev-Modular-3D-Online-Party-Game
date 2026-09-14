using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreditsScroller : MonoBehaviour
{
    public enum CreditItemType { Header, RoleName, PersonName, Spacer, Logo }

    [System.Serializable]
    public class CreditItem
    {
        public CreditItemType type = CreditItemType.PersonName;

        [TextArea] public string text;
        public Sprite logo;
        public float spacerHeight = 40f;

        [Header("Per-Logo Overrides")]
        public bool useCustomLogoHeight = false;
        public float customLogoHeight = 140f;   
        public bool useCustomLogoScale = false;
        public float customLogoScale = 1f;      
        public float extraTopBottomGap = 0f;    
    }

    private struct SpawnedVisual
    {
        public RectTransform rt;
        public Graphic graphic;
        public Color baseColor;
    }

    [Header("UI Refs")]
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private TMP_Text textPrefab;
    [SerializeField] private Image logoPrefab;

    [Header("Background Dim (0 -> Max, editable)")]
    [SerializeField] private Image screenDimImage;
    [Range(0f, 1f)] [SerializeField] private float screenDimMaxAlpha = 0.65f;
    [SerializeField] private float screenDimFadeInSeconds = 0.25f;
    [SerializeField] private float screenDimFadeOutSeconds = 0.20f;

    [Header("Style")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Color headerColor = Color.white;
    [SerializeField] private Color roleColor = new Color(0.8f, 0.9f, 1f);
    [SerializeField] private Color nameColor = new Color(0.95f, 0.95f, 0.95f);
    [SerializeField] private int headerSize = 54;
    [SerializeField] private int roleSize = 38;
    [SerializeField] private int nameSize = 32;
    [SerializeField] private float logoHeight = 140f; // default logo height
    [SerializeField] private float verticalGap = 20f;
    [SerializeField] private TextAlignmentOptions alignment = TextAlignmentOptions.Center;

    [Header("Logo Tint")]
    [SerializeField] private bool tintLogos = true;
    [SerializeField] private Color logoTintColor = Color.white;

    [Header("Motion")]
    [SerializeField] private float speed = 90f;
    [SerializeField] private float startOffsetFromBottom = 80f;
    [SerializeField] private float endOffsetAboveTop = 120f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool loop = true;

    [Header("Code Fade (Top/Bottom)")]
    [SerializeField] private bool useCodeFade = true;
    [SerializeField] private float topFadeHeight = 120f;
    [SerializeField] private float bottomFadeHeight = 120f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Data (fully editable)")]
    [SerializeField] private List<CreditItem> items = new();

    private readonly List<GameObject> spawned = new();
    private readonly List<SpawnedVisual> visuals = new();

    private float contentHeight;
    private float yStart;
    private float yEnd;
    private bool playing;

    private float dimCurrentAlpha;
    private bool dimFadingIn;
    private bool dimFadingOut;

    private void OnEnable()
    {
        StartDimFadeIn();
        if (playOnEnable) RebuildAndPlay();
    }

    private void OnDisable()
    {
        SetDimAlpha(0f);
        dimFadingIn = false;
        dimFadingOut = false;
    }

    public void RebuildAndPlay()
    {
        BuildContent();
        ResetPosition();
        playing = true;
        if (useCodeFade) UpdateFadePerVisual();
    }

    public void StopScroll() => playing = false;

    public void BeginCloseAndFadeOut()
    {
        dimFadingIn = false;
        dimFadingOut = true;
    }

    private void Update()
    {
        UpdateDim();

        if (!playing || contentRoot == null) return;

        Vector2 p = contentRoot.anchoredPosition;
        p.y += speed * Time.unscaledDeltaTime;
        contentRoot.anchoredPosition = p;

        if (useCodeFade) UpdateFadePerVisual();

        if (p.y >= yEnd)
        {
            if (loop)
            {
                ResetPosition();
                if (useCodeFade) UpdateFadePerVisual();
            }
            else
            {
                playing = false;
            }
        }
    }

    private void StartDimFadeIn()
    {
        SetDimAlpha(0f);
        dimFadingOut = false;
        dimFadingIn = true;
    }

    private void UpdateDim()
    {
        if (screenDimImage == null) return;

        if (dimFadingIn)
        {
            float dur = Mathf.Max(0.0001f, screenDimFadeInSeconds);
            dimCurrentAlpha += (screenDimMaxAlpha / dur) * Time.unscaledDeltaTime;
            if (dimCurrentAlpha >= screenDimMaxAlpha)
            {
                dimCurrentAlpha = screenDimMaxAlpha;
                dimFadingIn = false;
            }
            SetDimAlpha(dimCurrentAlpha);
        }
        else if (dimFadingOut)
        {
            float dur = Mathf.Max(0.0001f, screenDimFadeOutSeconds);
            dimCurrentAlpha -= (screenDimMaxAlpha / dur) * Time.unscaledDeltaTime;
            if (dimCurrentAlpha <= 0f)
            {
                dimCurrentAlpha = 0f;
                dimFadingOut = false;
            }
            SetDimAlpha(dimCurrentAlpha);
        }
    }

    private void SetDimAlpha(float a)
    {
        if (screenDimImage == null) return;
        Color c = screenDimImage.color;
        c.a = Mathf.Clamp(a, 0f, 1f);
        screenDimImage.color = c;
    }

    private void BuildContent()
    {
        ClearSpawned();
        if (contentRoot == null || textPrefab == null || logoPrefab == null) return;

        float y = 0f;
        float width = contentRoot.rect.width;

        foreach (var it in items)
        {
            switch (it.type)
            {
                case CreditItemType.Spacer:
                    y += Mathf.Max(0f, it.spacerHeight);
                    break;

                case CreditItemType.Logo:
                {
                    if (it.logo == null) break;

                    Image img = Instantiate(logoPrefab, contentRoot);
                    img.sprite = it.logo;
                    img.preserveAspect = true;
                    img.color = tintLogos ? logoTintColor : Color.white;

                    RectTransform rt = img.rectTransform;

                    float ratio = it.logo.rect.width / Mathf.Max(1f, it.logo.rect.height);
                    float h = it.useCustomLogoHeight ? Mathf.Max(1f, it.customLogoHeight) : Mathf.Max(1f, logoHeight);
                    float w = h * ratio;

                    if (it.useCustomLogoScale)
                    {
                        float s = Mathf.Max(0.01f, it.customLogoScale);
                        w *= s;
                        h *= s;
                    }

                    float extraGap = Mathf.Max(0f, it.extraTopBottomGap);

                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -(y + extraGap));
                    rt.sizeDelta = new Vector2(w, h);

                    spawned.Add(img.gameObject);
                    visuals.Add(new SpawnedVisual { rt = rt, graphic = img, baseColor = img.color });

                    y += extraGap + h + verticalGap + extraGap;
                    break;
                }

                default:
                {
                    TMP_Text t = Instantiate(textPrefab, contentRoot);
                    t.text = it.text;
                    if (font != null) t.font = font;
                    t.alignment = alignment;

                    switch (it.type)
                    {
                        case CreditItemType.Header:
                            t.fontSize = headerSize; t.color = headerColor; t.fontStyle = FontStyles.Bold; break;
                        case CreditItemType.RoleName:
                            t.fontSize = roleSize; t.color = roleColor; t.fontStyle = FontStyles.Bold; break;
                        default:
                            t.fontSize = nameSize; t.color = nameColor; t.fontStyle = FontStyles.Normal; break;
                    }

                    RectTransform rt = t.rectTransform;
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(width, 120f);

                    t.ForceMeshUpdate();
                    float h = Mathf.Max(40f, t.preferredHeight);
                    rt.sizeDelta = new Vector2(width, h);
                    rt.anchoredPosition = new Vector2(0f, -y);

                    spawned.Add(t.gameObject);
                    visuals.Add(new SpawnedVisual { rt = rt, graphic = t, baseColor = t.color });

                    y += h + verticalGap;
                    break;
                }
            }
        }

        contentHeight = y;
        contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        ComputeBounds();
    }

    private void ComputeBounds()
    {
        float viewportH = viewport != null ? viewport.rect.height : 800f;
        yStart = -(viewportH * 0.5f) - startOffsetFromBottom;
        yEnd = contentHeight + (viewportH * 0.5f) + endOffsetAboveTop;
    }

    private void ResetPosition()
    {
        if (contentRoot == null) return;
        contentRoot.anchoredPosition = new Vector2(0f, yStart);
    }

    private void UpdateFadePerVisual()
    {
        if (!useCodeFade || viewport == null) return;

        float viewportHalfH = viewport.rect.height * 0.5f;
        float yBottom = -viewportHalfH;
        float yTop = viewportHalfH;

        float topZone = Mathf.Max(0.0001f, topFadeHeight);
        float bottomZone = Mathf.Max(0.0001f, bottomFadeHeight);

        for (int i = 0; i < visuals.Count; i++)
        {
            var v = visuals[i];
            if (v.rt == null || v.graphic == null) continue;

            Vector3 worldCenter = v.rt.TransformPoint(v.rt.rect.center);
            Vector3 localToViewport = viewport.InverseTransformPoint(worldCenter);
            float yy = localToViewport.y;

            float alpha = 1f;

            if (yy < yBottom + bottomZone)
            {
                float t = Mathf.InverseLerp(yBottom, yBottom + bottomZone, yy);
                alpha = Mathf.Min(alpha, fadeCurve.Evaluate(t));
            }

            if (yy > yTop - topZone)
            {
                float t = Mathf.InverseLerp(yTop - topZone, yTop, yy);
                alpha = Mathf.Min(alpha, 1f - fadeCurve.Evaluate(t));
            }

            Color c = v.baseColor;
            c.a = c.a * Mathf.Clamp01(alpha);
            v.graphic.color = c;
        }
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i] != null) Destroy(spawned[i]);

        spawned.Clear();
        visuals.Clear();
    }
}