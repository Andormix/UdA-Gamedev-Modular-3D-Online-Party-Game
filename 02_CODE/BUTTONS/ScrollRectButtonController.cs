using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class ScrollRectButtonController : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private PressHandler leftButtonHandler;
    [SerializeField] private PressHandler rightButtonHandler;

    [Header("Settings")]
    [SerializeField] private float scrollSpeed = 2f; // Higher = faster movement
    [SerializeField] private bool smoothScroll = true;
    [SerializeField] private float smoothTime = 0.1f;

    private ScrollRect _scrollRect;
    private float _targetPosition;

    private void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
        _targetPosition = _scrollRect.horizontalNormalizedPosition; // Initialize at current pos
    }

    private void Update()
    {
        if (_scrollRect == null) return;

        bool isMoving = false;

        // Decrease position for Left
        if (leftButtonHandler != null && leftButtonHandler.IsPressed)
        {
            _targetPosition -= scrollSpeed * Time.deltaTime;
            isMoving = true;
        }

        // Increase position for Right
        if (rightButtonHandler != null && rightButtonHandler.IsPressed)
        {
            _targetPosition += scrollSpeed * Time.deltaTime;
            isMoving = true;
        }

        if (isMoving)
        {
            // Keep the value between 0 and 1
            _targetPosition = Mathf.Clamp01(_targetPosition);
            
            if (smoothScroll)
            {
                // Smoothly interpolate to the target position
                _scrollRect.horizontalNormalizedPosition = Mathf.Lerp(
                    _scrollRect.horizontalNormalizedPosition, 
                    _targetPosition, 
                    Time.deltaTime / smoothTime
                );
            }
            else
            {
                // Instant movement
                _scrollRect.horizontalNormalizedPosition = _targetPosition;
            }
        }
        else
        {
            // Sync target with manual mouse/touch scrolling so it doesn't "jump" back
            _targetPosition = _scrollRect.horizontalNormalizedPosition;
        }
    }

    // Public method to reset position 
    public void ResetScroll()
    {
        if (_scrollRect != null)
        {
            _scrollRect.horizontalNormalizedPosition = 0f;
            _targetPosition = 0f;
        }
    }
}