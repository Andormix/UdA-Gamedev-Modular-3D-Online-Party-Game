using UnityEngine;

public class PlayerTransformAnchor : MonoBehaviour
{
    [Header("Menu Settings")]
    [SerializeField] private Vector3 menuPosition;
    [SerializeField] private Vector3 menuRotation;

    [Header("Customization Settings")]
    [SerializeField] private Vector3 customPosition;
    [SerializeField] private Vector3 customRotation;

    public void SnapToMenu()
    {
        transform.position = menuPosition;
        transform.rotation = Quaternion.Euler(menuRotation);
    }

    public void SnapToCustomization()
    {
        transform.position = customPosition;
        transform.rotation = Quaternion.Euler(customRotation);
    }

    public void Rotate(float amount)
    {
        // Simple rotation on the Y axis
        transform.Rotate(Vector3.up, amount);
    }
}