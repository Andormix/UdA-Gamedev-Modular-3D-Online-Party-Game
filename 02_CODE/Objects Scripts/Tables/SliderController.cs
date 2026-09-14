using UnityEngine;
using UnityEngine.UI;

public class SliderTimerCountdown : MonoBehaviour
{
    private Slider slider;
    [SerializeField] private float duration = 10f;

    private float remaining;
    private bool running;

    private void Awake()
    {
        // If not assigned, find the Slider on THIS instance.
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);

        slider.minValue = 0f;
        slider.maxValue = duration;

        StartTimer();
    }

    public void StartTimer()
    {
        remaining = duration;
        running = true;
        slider.value = remaining;
    }

    private void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            remaining = 0f;
            running = false;
        }

        slider.value = remaining;
    }
}