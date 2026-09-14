using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RecipeMangerUniqueUI : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private TextMeshProUGUI recipeName;
    [SerializeField] private TextMeshProUGUI tableNameLabel;
    [SerializeField] private Transform imageFolder;
    [SerializeField] private Transform elementTemplate;
    [SerializeField] private Slider timerSlider;

    [Header("Template wiring")]
    [SerializeField] private Image templateIconImage;

    [Header("State colors")]
    [SerializeField] private Image timerFillImage;
    [SerializeField] private Color stateBlue = new(0.20f, 0.55f, 1f, 1f);
    [SerializeField] private Color stateGreen = new(0.20f, 0.90f, 0.35f, 1f);
    [SerializeField] private Color stateOrange = new(1.00f, 0.60f, 0.15f, 1f);
    [SerializeField] private Color stateRed = new(0.95f, 0.20f, 0.20f, 1f);

    private float duration;
    private double endTime;

    private void Awake()
    {
        if (elementTemplate != null)
            elementTemplate.gameObject.SetActive(false);

        if (timerSlider == null)
            timerSlider = GetComponentInChildren<Slider>(true);

        if (timerFillImage == null && timerSlider != null && timerSlider.fillRect != null)
            timerFillImage = timerSlider.fillRect.GetComponent<Image>();
    }

    public void SetOrderFromNet(RecipeSO recipe, int orderId, float duration, double endTime, ulong tableNetId, string tableDisplayName)
    {
        this.duration = Mathf.Max(0.0001f, duration);
        this.endTime = endTime;

        if (recipeName != null)
            recipeName.text = recipe != null ? recipe.recipeName : "Unknown";

        if (tableNameLabel != null)
            tableNameLabel.text = string.IsNullOrWhiteSpace(tableDisplayName) ? "Table ?" : tableDisplayName;

        RebuildIcons(recipe);

        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = this.duration;
        }

        SetStateColorIndex(0);
    }

    public void ApplyTimer(float duration, double endTime)
    {
        this.duration = Mathf.Max(0.0001f, duration);
        this.endTime = endTime;

        if (timerSlider != null)
            timerSlider.maxValue = this.duration;
    }

    public void SetStateColorIndex(int idx)
    {
        if (timerFillImage == null) return;

        timerFillImage.color = idx switch
        {
            0 => stateBlue,
            1 => stateGreen,
            2 => stateOrange,
            _ => stateRed
        };
    }

    private void Update()
    {
        if (timerSlider == null || NetworkManager.Singleton == null) return;

        double now = NetworkManager.Singleton.LocalTime.Time;
        float remaining = Mathf.Clamp((float)(endTime - now), 0f, duration);
        timerSlider.value = remaining;
    }

    private void RebuildIcons(RecipeSO recipe)
    {
        if (imageFolder == null || elementTemplate == null) return;

        foreach (Transform ch in imageFolder)
        {
            if (ch == elementTemplate) continue;
            Destroy(ch.gameObject);
        }

        if (recipe == null || recipe.sceneObjectSOList == null) return;

        foreach (SceneObjectSO sceneObjectSO in recipe.sceneObjectSOList)
        {
            Transform elementTransform = Instantiate(elementTemplate, imageFolder);
            elementTransform.gameObject.SetActive(true);

            Image iconImage = ResolveIconImage(elementTransform);
            if (iconImage == null) continue;

            iconImage.sprite = sceneObjectSO != null ? sceneObjectSO.sprite : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.preserveAspect = true;
        }
    }

    private Image ResolveIconImage(Transform elementInstance)
    {
        if (templateIconImage != null && elementTemplate != null)
        {
            string relPath = GetRelativePath(templateIconImage.transform, elementTemplate);
            if (!string.IsNullOrEmpty(relPath))
            {
                Transform target = elementInstance.Find(relPath);
                if (target != null)
                {
                    Image img = target.GetComponent<Image>();
                    if (img != null) return img;
                }
            }
        }

        Transform iconTf = elementInstance.Find("Icon");
        if (iconTf != null)
        {
            Image img = iconTf.GetComponent<Image>();
            if (img != null) return img;
        }

        Image[] images = elementInstance.GetComponentsInChildren<Image>(true);
        foreach (var img in images)
        {
            if (img.transform == elementInstance) continue;
            return img;
        }

        return null;
    }

    private string GetRelativePath(Transform target, Transform root)
    {
        if (target == null || root == null) return null;
        if (target == root) return string.Empty;

        System.Collections.Generic.Stack<string> parts = new();
        Transform current = target;

        while (current != null && current != root)
        {
            parts.Push(current.name);
            current = current.parent;
        }

        if (current != root) return null;
        return string.Join("/", parts);
    }
}