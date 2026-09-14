using UnityEngine;
using UnityEngine.UI;

public class EdgeScroll : MonoBehaviour
{
    public ScrollRect scrollRect;
    public float scrollSpeed = 0.5f; // How fast it scrolls
    public float edgeThreshold = 100f; // Distance from edge in pixels

    void Update()
    {
        if (scrollRect == null) return;

        Vector2 mousePos = Input.mousePosition;
        float screenWidth = Screen.width;

        // Check Right Edge
        if (mousePos.x > screenWidth - edgeThreshold)
        {
            MoveScroll(scrollSpeed * Time.deltaTime);
        }
        // Check Left Edge
        else if (mousePos.x < edgeThreshold)
        {
            MoveScroll(-scrollSpeed * Time.deltaTime);
        }
    }

    void MoveScroll(float amount)
    {
        // horizontalNormalizedPosition is a value between 0 and 1 (Do not touch)
        scrollRect.horizontalNormalizedPosition = Mathf.Clamp01(scrollRect.horizontalNormalizedPosition + amount);
    }
}