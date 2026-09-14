using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class OrderTimerSliderUI : MonoBehaviour
{
    [SerializeField] private Slider slider;

    private float duration;
    private double endTime;
    private bool bound;

    private void Awake()
    {
        if (slider == null)
            slider = GetComponentInChildren<Slider>(true);
    }

    public void Bind(float duration, double endTime)
    {
        this.duration = Mathf.Max(0.0001f, duration);
        this.endTime = endTime;
        bound = true;

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = this.duration;
        }
    }

    private void Update()
    {
        if (!bound || slider == null) return;
        if (NetworkManager.Singleton == null) return;

        double now = NetworkManager.Singleton.LocalTime.Time;
        float remaining = Mathf.Clamp((float)(endTime - now), 0f, duration);
        slider.value = remaining;
    }
}