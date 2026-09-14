using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TabMenuExtendedController : MonoBehaviour
{
    [Header("Color Settings")]
    [SerializeField] private Color selectedColor = new Color(0.33f, 0.53f, 0.93f);
    [SerializeField] private Color unselectedColor = new Color(0.42f, 0.53f, 0.64f);

    private List<TabData> _tabs = new List<TabData>();

    private struct TabData
    {
        public Button button;
        public Image icon;
        public TMP_Text text;
        public GameObject focusElement;
    }

    private void Awake()
    {
        foreach (Transform child in transform)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                TabData data = new TabData
                {
                    button = btn,
                    icon = child.Find("Icon")?.GetComponent<Image>(),
                    text = child.Find("Text")?.GetComponent<TMP_Text>(),
                    focusElement = child.Find("TabFocus")?.gameObject
                };

                _tabs.Add(data);
                btn.onClick.AddListener(() => OnTabClicked(btn));
            }
        }

        // Initialize first tab as active
        if (_tabs.Count > 0) OnTabClicked(_tabs[0].button);
    }

    // Changed to PUBLIC so MainMenuPresenter can call it
    public void OnTabClicked(Button clickedButton)
    {
        foreach (var tab in _tabs)
        {
            bool isActive = (tab.button == clickedButton);
            if (tab.focusElement != null) tab.focusElement.SetActive(isActive);

            Color targetColor = isActive ? selectedColor : unselectedColor;
            if (tab.icon != null) tab.icon.color = targetColor;
            if (tab.text != null) tab.text.color = targetColor;
        }
    }

    // Helper to reset to default tab (Per Hair)
    public void ResetToDefault()
    {
        if (_tabs.Count > 0) OnTabClicked(_tabs[0].button);
    }

    public void ResetToMulty()
    {
        if (_tabs.Count > 0) OnTabClicked(_tabs[2].button);
    }
}