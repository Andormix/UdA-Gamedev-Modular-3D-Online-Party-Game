using TMPro;
using UnityEngine;

public class ScoreGainPopupUI : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float life = 0.6f;
    [SerializeField] private float rise = 35f;

    private float t;
    private Vector3 startPos;
    private Color startColor;

    public void Setup(int delta, Color color)
    {
        if (text != null)
        {
            text.text = $"+{delta}";
            text.color = color;
            startColor = color;
        }

        startPos = transform.localPosition;
        t = 0f;
    }

    private void Update()
    {
        t += Time.unscaledDeltaTime;
        float a = Mathf.Clamp01(t / life);

        transform.localPosition = startPos + Vector3.up * (rise * a);

        if (text != null)
        {
            Color c = startColor;
            c.a = 1f - a;
            text.color = c;
        }

        if (a >= 1f)
            Destroy(gameObject);
    }
}