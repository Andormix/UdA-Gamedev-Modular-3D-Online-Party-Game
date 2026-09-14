using UnityEngine;

public class ParallaxBackground : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Maximum pixels the image will move from center.")]
    public Vector2 moveAmount = new Vector2(50f, 50f);
    
    [Tooltip("How smooth the movement is. Higher = Slower/Smoother.")]
    public float smoothing = 5f;

    [Tooltip("Invert to move away from cursor instead of toward it.")]
    public bool invert = false;

    private RectTransform rectTransform;
    private Vector3 initialPosition;
    private Vector2 targetOffset;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        initialPosition = rectTransform.localPosition;
    }

    void Update()
    {
        // 1. Get Mouse Position in normalized coordinates (-1 to 1)
        // Screen.width/height is used to find the percentage of movement
        float mouseX = (Input.mousePosition.x / Screen.width) * 2 - 1;
        float mouseY = (Input.mousePosition.y / Screen.height) * 2 - 1;

        // 2. Calculate the target offset
        int direction = invert ? -1 : 1;
        targetOffset.x = mouseX * moveAmount.x * direction;
        targetOffset.y = mouseY * moveAmount.y * direction;

        // 3. Smoothly interpolate to the target position
        Vector3 targetPos = initialPosition + new Vector3(targetOffset.x, targetOffset.y, 0);
        rectTransform.localPosition = Vector3.Lerp(rectTransform.localPosition, targetPos, Time.deltaTime * smoothing);
    }
}