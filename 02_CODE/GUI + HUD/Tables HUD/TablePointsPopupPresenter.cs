using UnityEngine;

public class TablePointsPopupPresenter : MonoBehaviour
{
    [SerializeField] private Transform popupAnchor;
    [SerializeField] private TablePointsPopupUI popupPrefab;
    [SerializeField] private Sprite coinSprite;

    [Header("Colors")]
    [SerializeField] private Color gainColor = new(1f, 0.95f, 0.35f, 1f);
    [SerializeField] private Color paymentColor = new(0.45f, 1f, 0.5f, 1f);
    [SerializeField] private Color penaltyColor = new(1f, 0.45f, 0.45f, 1f);

    public void ShowTip(int points) => Spawn(points, gainColor, "+");
    public void ShowPayment(int points) => Spawn(points, paymentColor, "+");
    public void ShowPenalty(int points) => Spawn(points, penaltyColor, "-");

    private void Spawn(int points, Color color, string prefix)
    {
        if (points <= 0 || popupPrefab == null || popupAnchor == null) return;

        var popup = Instantiate(popupPrefab, popupAnchor);
        popup.transform.localPosition = Vector3.zero;
        popup.Setup(points, coinSprite, color, prefix);
    }
}