using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PriceRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private Image iconImage;

    public void Set(string itemName, int cents, Sprite icon)
    {
        if (nameText != null) nameText.text = itemName;
        if (priceText != null) priceText.text = cents.ToString();
        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }
}