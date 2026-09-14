using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TabMenuController : MonoBehaviour
{
    [Header("Color Settings")]
    [SerializeField] private Color selectedColor = new Color(0.33f, 0.53f, 0.93f); // 5486ED "COLOR PICKER"
    [SerializeField] private Color unselectedColor = new Color(0.42f, 0.53f, 0.64f); // 6B87A3

    private List<TabButtonData> _tabs = new List<TabButtonData>();

    private struct TabButtonData
    {
        public Button button;
        public Image iconImage;
        public GameObject focusElement;
    }

    private void Awake()
    {
        // Automatically find all children buttons in the Horizontal Layout Group
        foreach (Transform child in transform)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                TabButtonData data = new TabButtonData
                {
                    button = btn,
                    iconImage = btn.GetComponent<Image>(), 
                    focusElement = btn.transform.Find("TabFocus")?.gameObject 
                };

                _tabs.Add(data);
                btn.onClick.AddListener(() => OnTabClicked(btn));
            }
        }
        if (_tabs.Count > 0) OnTabClicked(_tabs[0].button);
    }

    private void OnTabClicked(Button clickedButton)
    {
        foreach (var tab in _tabs)
        {
            bool isActive = (tab.button == clickedButton);

            // Toggle the TabFocus element
            if (tab.focusElement != null)
            {
                tab.focusElement.SetActive(isActive);
            }

            // Update Icon Color
            if (tab.iconImage != null)
            {
                tab.iconImage.color = isActive ? selectedColor : unselectedColor;
            }
        }
    }
}