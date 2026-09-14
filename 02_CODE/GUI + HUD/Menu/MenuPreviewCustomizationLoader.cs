using UnityEngine;

public class MenuPreviewCustomizationLoader : MonoBehaviour
{
    [SerializeField] private PlayerCharacterCustomized previewCustomized;

    private void Awake()
    {
        if (previewCustomized == null)
            previewCustomized = GetComponentInChildren<PlayerCharacterCustomized>(true);
    }

    private void Start()
    {
        if (previewCustomized == null)
        {
            Debug.LogWarning("[MenuPreviewCustomizationLoader] PlayerCharacterCustomized not found.");
            return;
        }

        // Always reload local saved appearance for menu preview
        previewCustomized.Load();
    }
}