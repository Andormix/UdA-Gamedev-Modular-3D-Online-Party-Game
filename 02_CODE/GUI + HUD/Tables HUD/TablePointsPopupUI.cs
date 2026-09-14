using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TablePointsPopupUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text valueText;

    [Header("Anim")]
    [SerializeField] private float life = 0.75f;
    [SerializeField] private float rise = 35f;
    [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    private float t;
    private Vector3 startLocalPos;
    private Color iconBaseColor;
    private Color textBaseColor;

    public void Setup(int points, Sprite iconSprite, Color tint, string prefix = "+")
    {
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.color = tint;
            iconBaseColor = iconImage.color;
        }

        if (valueText != null)
        {
            valueText.text = $"{prefix} {points}";
            valueText.color = tint;
            textBaseColor = valueText.color;
        }

        startLocalPos = transform.localPosition;
        t = 0f;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float n = Mathf.Clamp01(t / life);

        transform.localPosition = startLocalPos + Vector3.up * (rise * n);

        float a = alphaCurve.Evaluate(n);

        if (iconImage != null)
        {
            var c = iconBaseColor;
            c.a = a;
            iconImage.color = c;
        }

        if (valueText != null)
        {
            var c = textBaseColor;
            c.a = a;
            valueText.color = c;
        }

        if (n >= 1f)
            Destroy(gameObject);
    }
}