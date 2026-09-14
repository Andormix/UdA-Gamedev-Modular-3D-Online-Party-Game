using UnityEngine;
using UnityEngine.UI;

public class QuadTouchFeedback : MonoBehaviour
{
    [Header("Raycast Target")]
    public Camera cam;
    public LayerMask quadMask;
    public float rayDistance = 100f;

    [Header("UI Ripple")]
    public Canvas overlayCanvas; // Screen Space - Overlay
    public Sprite rippleSprite;  // Assignació de PNG sprite here
    public float life = 0.25f;
    public float startSize = 30f;
    public float endSize = 140f;
    public Color rippleColor = new Color(1f, 1f, 1f, 0.9f);

    void Awake()
    {
        if (cam == null) cam = Camera.main;
        if (overlayCanvas == null) overlayCanvas = EnsureOverlayCanvas();

        if (rippleSprite == null)
            Debug.LogWarning("Ripple sprite is null. Assign PNG sprite in inspector.");
    }

    void Update()
    {
        if (!TryGetPointerDown(out Vector2 screenPos)) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, quadMask))
            SpawnUiRipple(screenPos);
    }

    bool TryGetPointerDown(out Vector2 pos)
    {
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                pos = t.position;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            pos = Input.mousePosition;
            return true;
        }

        pos = default;
        return false;
    }

    void SpawnUiRipple(Vector2 screenPos)
    {
        GameObject go = new GameObject("TouchRippleUI");
        go.transform.SetParent(overlayCanvas.transform, false);

        Image img = go.AddComponent<Image>();
        img.sprite = rippleSprite;
        img.color = rippleColor;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        RectTransform rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = screenPos;
        rt.sizeDelta = new Vector2(startSize, startSize);

        var anim = go.AddComponent<UiRippleAnim>();
        anim.Setup(img, rt, life, startSize, endSize, rippleColor);
    }

    Canvas EnsureOverlayCanvas()
    {
        Canvas found = FindObjectOfType<Canvas>();
        if (found != null && found.renderMode == RenderMode.ScreenSpaceOverlay)
            return found;

        GameObject canvasGo = new GameObject("TouchFeedbackCanvas");
        Canvas c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        return c;
    }
}

public class UiRippleAnim : MonoBehaviour
{
    Image img;
    RectTransform rt;
    float life, s0, s1, t;
    Color c0;

    public void Setup(Image image, RectTransform rect, float lifeTime, float start, float end, Color color)
    {
        img = image;
        rt = rect;
        life = Mathf.Max(0.01f, lifeTime);
        s0 = Mathf.Max(1f, start);
        s1 = Mathf.Max(s0, end);
        c0 = color;
        if (c0.a <= 0f) c0.a = 0.9f;
    }

    void Update()
    {
        t += Time.deltaTime / life;
        float k = Mathf.Clamp01(t);

        float s = Mathf.Lerp(s0, s1, k);
        rt.sizeDelta = new Vector2(s, s);

        Color c = c0;
        c.a = Mathf.Lerp(c0.a, 0f, k);
        img.color = c;

        if (k >= 1f) Destroy(gameObject);
    }
}