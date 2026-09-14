using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class TableDirtyVisualNetSync : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private SceneObjectCatalogSO catalog;

    [Header("Dirty slots")]
    [SerializeField] private Transform[] dirtySlots;

    // Replicated dirty item SO indices (slot-ordered)
    private NetworkList<int> dirtyItemIndices;

    // Local visuals per slot
    private GameObject[] slotVisuals;

    // Tracks the catalog index currently shown per slot so we can skipp unnecessary Destroy+Instantiate when the content hasn't changed.
    private int[] _displayedIndices;

    private void Awake()
    {
        dirtyItemIndices = new NetworkList<int>();
        if (dirtySlots != null && dirtySlots.Length > 0)
        {
            slotVisuals = new GameObject[dirtySlots.Length];
            _displayedIndices = new int[dirtySlots.Length];
            for (int i = 0; i < _displayedIndices.Length; i++)
                _displayedIndices[i] = -1;
        }
    }

    public override void OnNetworkSpawn()
    {
        dirtyItemIndices.OnListChanged += OnDirtyListChanged;
        RebuildVisuals();
    }

    public override void OnNetworkDespawn()
    {
        dirtyItemIndices.OnListChanged -= OnDirtyListChanged;
        ClearAllVisuals();
    }

    private void OnDirtyListChanged(NetworkListEvent<int> _)
    {
        RebuildVisuals();
    }

    // SERVER API
    public void ServerSetDirtyItems(System.Collections.Generic.List<SceneObjectSO> dirtyItems)
    {
        if (!IsServer) return;
        if (catalog == null) return;

        dirtyItemIndices.Clear();

        if (dirtyItems == null || dirtySlots == null) return;

        int count = Mathf.Min(dirtyItems.Count, dirtySlots.Length);
        for (int i = 0; i < count; i++)
        {
            SceneObjectSO so = dirtyItems[i];
            int idx = catalog.IndexOf(so);
            if (idx >= 0)
                dirtyItemIndices.Add(idx);
        }
    }

    public void ServerClearDirtyItems()
    {
        if (!IsServer) return;
        dirtyItemIndices.Clear();
    }

    private void RebuildVisuals()
    {
        if (catalog == null || dirtySlots == null || slotVisuals == null) return;

        int newCount = Mathf.Min(dirtyItemIndices.Count, dirtySlots.Length);

        // First pass: clear slots whose content changed or that are no longer needed.
        for (int i = 0; i < slotVisuals.Length; i++)
        {
            int newIdx = i < newCount ? dirtyItemIndices[i] : -1;

            if (_displayedIndices[i] == newIdx) continue; // nothing changed for this slot

            if (slotVisuals[i] != null)
            {
                Destroy(slotVisuals[i]);
                slotVisuals[i] = null;
            }
            _displayedIndices[i] = -1;
        }

        // Second pass: instantiate only slots that now need a new visual.
        for (int i = 0; i < newCount; i++)
        {
            if (slotVisuals[i] != null) continue; // already correct, skip

            Transform slot = dirtySlots[i];
            if (slot == null) continue;

            int catalogIdx = dirtyItemIndices[i];
            SceneObjectSO so = catalog.Get(catalogIdx);
            if (so == null || so.prefab == null) continue;

            GameObject go = Instantiate(so.prefab.gameObject, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            DisableRuntimeComponents(go);
            slotVisuals[i] = go;
            _displayedIndices[i] = catalogIdx;
        }
    }

    private void ClearAllVisuals()
    {
        if (slotVisuals == null) return;

        for (int i = 0; i < slotVisuals.Length; i++)
        {
            if (slotVisuals[i] != null)
            {
                Destroy(slotVisuals[i]);
                slotVisuals[i] = null;
            }
            _displayedIndices[i] = -1;
        }
    }

    private void DisableRuntimeComponents(GameObject root)
    {
        foreach (var no in root.GetComponentsInChildren<NetworkObject>(true))
            no.enabled = false;

        foreach (var nt in root.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true))
            nt.enabled = false;

        foreach (var soNet in root.GetComponentsInChildren<SceneObjectNet>(true))
            soNet.enabled = false;

        foreach (var so in root.GetComponentsInChildren<SceneObject>(true))
            so.enabled = false;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            c.enabled = false;
    }
}