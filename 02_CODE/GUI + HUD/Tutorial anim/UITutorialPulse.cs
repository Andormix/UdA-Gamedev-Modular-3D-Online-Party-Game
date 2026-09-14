using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class UITutorialPulse : MonoBehaviour
{
    [Header("Visual Pulse Settings")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private float amplitude = 0.1f;

    private RectTransform rectTransform;
    private Vector3 initialScale;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        initialScale = rectTransform.localScale;
    }

    void OnEnable()
    {
        if (rectTransform != null)
            rectTransform.localScale = initialScale;
    }

    void Update()
    {
        float pulse = Mathf.Sin(Time.time * speed) * amplitude;
        rectTransform.localScale = initialScale + new Vector3(pulse, pulse, 0);
    }

    void OnDisable()
    {
        if (rectTransform != null)
            rectTransform.localScale = initialScale;
    }
}