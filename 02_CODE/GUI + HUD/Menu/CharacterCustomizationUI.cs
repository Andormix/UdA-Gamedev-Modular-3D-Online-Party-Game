using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CharacterCustomizationUI : MonoBehaviour
{
    [Header("Category Selection Buttons")]
    [SerializeField] private Button hairButton;
    [SerializeField] private Button hairAccButton;
    [SerializeField] private Button beardButton;
    [SerializeField] private Button topButton;
    [SerializeField] private Button legsButton;

    [Header("Action Buttons")]
    [SerializeField] private Button saveButton;
    //[SerializeField] private Button returnButton;

    [Header("Grids & Templates")]
    [SerializeField] private Transform itemGrid;      
    [SerializeField] private GameObject itemTemplate; 
    [SerializeField] private Transform colorGrid;     
    [SerializeField] private GameObject colorTemplate;
    [SerializeField] private Sprite whiteCircleSprite; 

    [Header("Player References")]
    [SerializeField] private PlayerCharacterCustomized playerCharacterCustomized;
    [SerializeField] private PlayerTransformAnchor playerAnchor;

    [Header("Rotation Settings")]
    [SerializeField] private PressHandler rotateLeftHandler;
    [SerializeField] private PressHandler rotateRightHandler;
    [SerializeField] private float rotationSpeed = 150f;

    private void Awake()
    {
        // Category Listeners
        hairButton.onClick.AddListener(() => Refresh(PlayerCharacterCustomized.BodyPartType.Hair));
        hairAccButton.onClick.AddListener(() => Refresh(PlayerCharacterCustomized.BodyPartType.HairAcc));
        beardButton.onClick.AddListener(() => Refresh(PlayerCharacterCustomized.BodyPartType.Beard));
        topButton.onClick.AddListener(() => Refresh(PlayerCharacterCustomized.BodyPartType.Top));
        legsButton.onClick.AddListener(() => Refresh(PlayerCharacterCustomized.BodyPartType.Legs));

        saveButton.onClick.AddListener(onSaveButtonClick);
        //returnButton.onClick.AddListener(onReturnButtonClick);

        // Deactivate templates so they don't show in the grid
        if (itemTemplate != null) itemTemplate.SetActive(false);
        if (colorTemplate != null) colorTemplate.SetActive(false);
    }

    private void Start()
    {
        // 1. Load saved data so the UI reflects the current character state
        if (playerCharacterCustomized != null) playerCharacterCustomized.Load();

        // 2. Populate the Hair category immediately on start
        Refresh(PlayerCharacterCustomized.BodyPartType.Hair);

    }

    private void Update() => HandleRotation();

    private void HandleRotation()
    {
        if (rotateLeftHandler != null && rotateLeftHandler.IsPressed && playerAnchor != null)
            playerAnchor.Rotate(rotationSpeed * Time.deltaTime);

        if (rotateRightHandler != null && rotateRightHandler.IsPressed && playerAnchor != null)
            playerAnchor.Rotate(-rotationSpeed * Time.deltaTime);
    }

    private void Refresh(PlayerCharacterCustomized.BodyPartType type)
    {
        UpdateGrid(itemGrid, itemTemplate, type, false);
        UpdateGrid(colorGrid, colorTemplate, type, true);

        // Reset scroll position to the left whenever a category changes
        itemGrid.GetComponentInParent<ScrollRectButtonController>()?.ResetScroll();
        colorGrid.GetComponentInParent<ScrollRectButtonController>()?.ResetScroll();
    }

    private void UpdateGrid(Transform grid, GameObject template, PlayerCharacterCustomized.BodyPartType type, bool isColor)
    {
        if (grid == null || template == null) return;

        // Clear existing slots (exceptuant el template)
        foreach (Transform child in grid) 
        { 
            if (child.gameObject != template) Destroy(child.gameObject); 
        }

        var data = playerCharacterCustomized.GetBodyPartData_Public(type);
        int count = isColor ? playerCharacterCustomized.GetColorCount(type) : playerCharacterCustomized.GetPartCount(type);
        Sprite[] icons = isColor ? null : playerCharacterCustomized.GetSpritesForPart(type);

        for (int i = 0; i < count; i++)
        {
            GameObject slot = Instantiate(template, grid);
            slot.SetActive(true);
            
            // TOD 76: Fix UI positioning
            RectTransform rect = slot.GetComponent<RectTransform>();
            rect.anchoredPosition3D = new Vector3(rect.anchoredPosition.x, rect.anchoredPosition.y, 0);
            rect.localScale = Vector3.one;

            Color? tint = isColor ? (Color?)playerCharacterCustomized.GetColorValue(type, i) : null;
            Sprite s = isColor ? whiteCircleSprite : (icons != null && i < icons.Length ? icons[i] : null);
            Material mat = (isColor && data != null && data.colorArray != null) ? data.colorArray[i] : null;

            slot.GetComponent<CustomizationSlot>().Setup(i, s, type, this, isColor, tint, mat);
        }

        // Force the UI to rebuild layout per que the ScrollRect calculates the content size instantly
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid.GetComponent<RectTransform>());
    }

    public void SelectPartFromGallery(PlayerCharacterCustomized.BodyPartType type, int index) => playerCharacterCustomized.SetBodyPart(type, index);
    public void SelectColorFromGallery(PlayerCharacterCustomized.BodyPartType type, int index) => playerCharacterCustomized.SetBodyPartColor(type, index);

    private void onSaveButtonClick() => playerCharacterCustomized.Save();
}