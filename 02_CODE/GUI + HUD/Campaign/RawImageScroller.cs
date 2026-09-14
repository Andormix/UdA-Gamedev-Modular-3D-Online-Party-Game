using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class RawImageScroller : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Adjust X and Y for direction and speed")]
    [SerializeField] private Vector2 velocity = new Vector2(0.1f, 0.05f);

    private RawImage _img;

    private void Awake()
    {
        _img = GetComponent<RawImage>();
    }

    void Update()
    {
        // Calculate the new position based on velocity and frame time
        Vector2 nextPosition = _img.uvRect.position + (velocity * Time.deltaTime);

        // Update the UV Rect while maintaining the original scale (size)
        _img.uvRect = new Rect(nextPosition, _img.uvRect.size);
    }

    // Reset is called when the script is added or 'Reset' is clicked in Inspector
    private void Reset()
    {
        _img = GetComponent<RawImage>();
    }
}