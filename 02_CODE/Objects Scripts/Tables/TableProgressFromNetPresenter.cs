using UnityEngine;
using UnityEngine.UI;

public sealed class TableProgressFromNetPresenter : MonoBehaviour
{
    [SerializeField] private TableNetSync tableNetSync;

    [Header("UI")]
    [SerializeField] private Slider slider;        
    [SerializeField] private Image barImage;       // optional fallback old fill image
    [SerializeField] private GameObject rootToShowHide;

    private void Awake()
    {
        if (rootToShowHide == null) rootToShowHide = gameObject;

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.interactable = false;
            slider.value = 0f;
        }

        if (barImage != null)
            barImage.fillAmount = 0f;
    }

    private void Update()
    {
        if (tableNetSync == null || (slider == null && barImage == null))
        {
            SetUIValue(0f);
            SetVisible(false);
            return;
        }

        if (!tableNetSync.HasReplicatedTimerForCurrentPhase)
        {
            SetUIValue(0f);
            SetVisible(false);
            return;
        }

        float normalized = Mathf.Clamp01(tableNetSync.GetReplicatedTimerNormalized());
        SetUIValue(normalized);

        bool visible = normalized > 0f && normalized < 1f;
        SetVisible(visible);
    }

    private void SetUIValue(float normalized)
    {
        if (slider != null) slider.value = normalized;
        if (barImage != null) barImage.fillAmount = normalized;
    }

    private void SetVisible(bool visible)
    {
        if (rootToShowHide != null && rootToShowHide.activeSelf != visible)
            rootToShowHide.SetActive(visible);
    }
}