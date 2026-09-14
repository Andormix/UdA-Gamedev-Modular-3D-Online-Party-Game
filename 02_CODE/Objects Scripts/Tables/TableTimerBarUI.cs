using UnityEngine;
using UnityEngine.UI;

public class TableTimerBarUI : Slider
{
    [Header("Timer Settings")]
    public int currentTime = 50;
    public int maxTime = 100;

/*#if UNITY_EDITOR
    // Editor-only: lets you preview changes in Inspector
    protected void OnValidate()
    {
        UpdateBar();
        UpdateVisibility();
    }
#endif*/

    protected override void Awake()
    {
        base.Awake();
        UpdateBar();
        UpdateVisibility();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        UpdateBar();
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (fillRect == null) return;

        var fillRenderer = fillRect.GetComponent<CanvasRenderer>();
        if (fillRenderer != null)
        {
            float alpha = (value <= 0) ? 0f : 1f;
            fillRenderer.SetAlpha(alpha);
        }
    }

    private void UpdateBar()
    {
        maxTime = Mathf.Max(1, maxTime);
        maxValue = maxTime;
        value = Mathf.Clamp(currentTime, 0, (int)maxValue);
    }

    public void TimeUp()
    {
        currentTime++;
        UpdateBar();
        UpdateVisibility();
    }

    public void TimeDown()
    {
        currentTime--;
        UpdateBar();
        UpdateVisibility();
    }
}