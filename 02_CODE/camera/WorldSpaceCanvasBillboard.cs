using UnityEngine;

public class WorldSpaceCanvasBillboard : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool keepUpright = true;

    [Header("Optional constant on-screen size")]
    [SerializeField] private bool keepScreenSize = true;
    [SerializeField] private float referenceDistance = 6f;
    [SerializeField] private Vector3 baseScale = Vector3.one;

    private void LateUpdate()
    {
        Camera cam = targetCamera != null ? targetCamera : PlayerEvents.LocalGameplayCamera;
        if (cam == null)
            cam = Camera.main;
        if (cam == null) return;

        Vector3 toCam = transform.position - cam.transform.position;

        if (keepUpright)
        {
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        if (keepScreenSize)
        {
            float d = Vector3.Distance(transform.position, cam.transform.position);
            float factor = Mathf.Max(0.0001f, d / Mathf.Max(0.0001f, referenceDistance));
            transform.localScale = baseScale * factor;
        }
    }
}