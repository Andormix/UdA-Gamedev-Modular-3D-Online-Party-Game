using TMPro;
using UnityEngine;
using UnityEngine.UI;

// TODO DIADA UNIVERSITARIA 205: Integrate it before it happens.
// Options: 0 = Low (Performance), 1 = PC (High).
public sealed class GraphicsQualityMenuToggle : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private Button lowQualityButton;
    [SerializeField] private Button pcQualityButton;

    private void OnEnable()
    {
        int current = GraphicsQualityBootstrap.ResolveInitialQuality();
        GraphicsQualityBootstrap.ApplyQuality(current, savePreference: false);

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new System.Collections.Generic.List<string> { "Low (Performance)", "PC (High)" });
            qualityDropdown.SetValueWithoutNotify(current);
            qualityDropdown.onValueChanged.AddListener(OnDropdownChanged);
        }

        if (lowQualityButton != null)
            lowQualityButton.onClick.AddListener(() => SetQuality(GraphicsQualityBootstrap.QualityLow));
        if (pcQualityButton != null)
            pcQualityButton.onClick.AddListener(() => SetQuality(GraphicsQualityBootstrap.QualityPc));
    }

    private void OnDisable()
    {
        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        if (lowQualityButton != null)
            lowQualityButton.onClick.RemoveAllListeners();
        if (pcQualityButton != null)
            pcQualityButton.onClick.RemoveAllListeners();
    }

    private void OnDropdownChanged(int index) => SetQuality(index);

    private static void SetQuality(int index)
    {
        GraphicsQualityBootstrap.ApplyQuality(index, savePreference: true);
    }
}
