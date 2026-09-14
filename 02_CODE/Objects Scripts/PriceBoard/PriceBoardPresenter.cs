using UnityEngine;
using UnityEngine.UI;

public sealed class PriceBoardPresenter : MonoBehaviour
{
    [Header("Scene dependencies")]
    [SerializeField] private UserInterfaceManager ui;
    [SerializeField] private SceneObjectCatalogSO catalog;

    [Header("ScrollRect setup")]
    [SerializeField] private ScrollRect scrollRect;     
    [SerializeField] private Transform contentParent;   
    [SerializeField] private PriceRowUI rowTemplate;    

    [Header("View")]
    [SerializeField] private Button closeButton;

    [Header("Auto close")]
    [SerializeField] private float autoCloseDistance = 3.0f;

    private Transform openedBy;
    private Transform anchor;
    private bool isOpen;

    private void Awake()
    {
        if (ui == null || catalog == null || contentParent == null || rowTemplate == null || closeButton == null)
        {
            Debug.LogError($"{nameof(PriceBoardPresenter)} missing references.", this);
            enabled = false;
            return;
        }

        rowTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Hide);
    }

    private void Update()
    {
        if (!isOpen) return;

        if (openedBy == null || anchor == null)
        {
            Hide();
            return;
        }

        float dist = Vector3.Distance(openedBy.position, anchor.position);
        if (dist > autoCloseDistance)
            Hide();
    }

    public void Show(Player player, Transform boardAnchor)
    {
        if (player == null || boardAnchor == null) return;

        openedBy = player.transform;
        anchor = boardAnchor;
        isOpen = true;

        ui.Show(UserInterfaceManager.UIPage.PriceBoard);
        Refresh();

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f; // go top
    }

    public bool Toggle(Player player, Transform boardAnchor)
    {
        if (isOpen)
        {
            Hide();
            return false;
        }

        Show(player, boardAnchor);
        return isOpen;
    }

    public void Hide()
    {
        isOpen = false;
        openedBy = null;
        anchor = null;
        ui.Hide(UserInterfaceManager.UIPage.PriceBoard);
    }

    private void Refresh()
    {
        // clear old rows
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Transform child = contentParent.GetChild(i);
            if (child == rowTemplate.transform) continue;
            Destroy(child.gameObject);
        }

        var priceMgr = Object.FindFirstObjectByType<SceneObjectPriceManager>();
        if (priceMgr == null) return;

        foreach (var so in catalog.all)
        {
            if (so == null || !so.deliverable) continue;

            var row = Instantiate(rowTemplate, contentParent);
            row.gameObject.SetActive(true);
            row.Set(so.objectName, priceMgr.GetPrice(so), so.sprite);
        }

        // force layout rebuild after dynamic add
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentParent as RectTransform);
    }
}
