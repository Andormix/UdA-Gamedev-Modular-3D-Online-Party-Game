using UnityEngine;
using Unity.Netcode;

public class PlayerMarkerRing : NetworkBehaviour
{
    [SerializeField] private Renderer ringRenderer;
    [SerializeField] private Vector3 offset = new(0f, 0.02f, 0f);

    private static readonly Color[] Palette = new Color[]
    {
        new(0.20f, 0.75f, 1f),   // cyan
        new(1f, 0.35f, 0.35f),   // red
        new(0.35f, 1f, 0.45f),   // green
        new(1f, 0.85f, 0.25f),   // yellow
    };

    private Transform target;

    public override void OnNetworkSpawn()
    {
        target = transform;
        ApplyColorByOwner();
    }

    private void LateUpdate()
    {
        if (target == null) return;
        transform.position = target.position + offset;
    }

    private void ApplyColorByOwner()
    {
        if (ringRenderer == null) return;

        int idx = (int)(OwnerClientId % (ulong)Palette.Length);
        Color c = Palette[idx];

        // instance material safely
        var mat = ringRenderer.material;
        mat.color = c;
    }
}