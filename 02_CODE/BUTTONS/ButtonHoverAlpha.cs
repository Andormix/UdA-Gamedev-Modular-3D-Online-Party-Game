using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoverAlpha : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target Settings")]
    [SerializeField] private Image targetImage;
    
    [Header("Alpha Values")]
    [Range(0f, 1f)] [SerializeField] private float normalAlpha = 0.5f;
    [Range(0f, 1f)] [SerializeField] private float hoverAlpha = 1f;
    
    [Header("Animation")]
    [SerializeField] private bool useSmoothing = true;
    [SerializeField] private float transitionSpeed = 10f;

    private float currentAlpha;

    void Start()
    {
        // Fallback
        if (targetImage == null) targetImage = GetComponent<Image>();
        
        currentAlpha = normalAlpha;
        SetAlpha(normalAlpha);
    }

    void Update()
    {
        if (useSmoothing)
        {
            // Smoothing.
            float target = isHovered ? hoverAlpha : normalAlpha;
            currentAlpha = Mathf.Lerp(currentAlpha, target, Time.deltaTime * transitionSpeed);
            SetAlpha(currentAlpha);
        }
    }

    private bool isHovered = false;

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (!useSmoothing) SetAlpha(hoverAlpha);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (!useSmoothing) SetAlpha(normalAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (targetImage != null)
        {
            Color c = targetImage.color;
            c.a = alpha;
            targetImage.color = c;
        }
    }
}