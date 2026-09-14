using UnityEngine;
using UnityEngine.UI;

public class CustomizationSlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    
    private int _index;
    private PlayerCharacterCustomized.BodyPartType _partType;
    private CharacterCustomizationUI _uiManager;
    private bool _isColorSlot;

    public void Setup(int index, Sprite icon, PlayerCharacterCustomized.BodyPartType type, CharacterCustomizationUI manager, bool isColor, Color? tint = null, Material mat = null)
    {
        _index = index;
        _partType = type;
        _uiManager = manager;
        _isColorSlot = isColor;

        if (itemIcon == null) itemIcon = transform.Find("ItemIcon")?.GetComponent<Image>();

        if (itemIcon != null)
        {
            // TODO 55: Reset to default UI material to avoid 3D shader glitches
            itemIcon.material = null; 

            if (isColor)
            {
                // If a Material is provided, extract its texture
                if (mat != null && mat.HasProperty("_BaseMap"))
                {
                    Texture2D baseMap = mat.GetTexture("_BaseMap") as Texture2D;
                    if (baseMap != null)
                    {
                        // BUGFIX 23: Convert the Texture2D from the 3D material into a UI Sprite
                        itemIcon.sprite = Sprite.Create(baseMap, new Rect(0, 0, baseMap.width, baseMap.height), new Vector2(0.5f, 0.5f));
                        itemIcon.color = Color.white;
                    }
                }
                else if (tint.HasValue)
                {
                    itemIcon.sprite = icon; // Uses your whiteCircleSprite
                    itemIcon.color = tint.Value;
                }
            }
            else
            {
                // Logic for Mesh Part icons
                itemIcon.sprite = icon;
                itemIcon.color = Color.white;
            }

            itemIcon.enabled = (itemIcon.sprite != null);
        }

        // BUGFIX 24: Force Z to 0 to prevent the UI from "jumping out" of the scroll view
        RectTransform rect = GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchoredPosition3D = new Vector3(rect.anchoredPosition.x, rect.anchoredPosition.y, 0f);
            rect.localScale = Vector3.one;
        }

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => {
                if (_isColorSlot) _uiManager.SelectColorFromGallery(_partType, _index);
                else _uiManager.SelectPartFromGallery(_partType, _index);
            });
        }
    }
}