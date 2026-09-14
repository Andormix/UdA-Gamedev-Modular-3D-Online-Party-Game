using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class SinkQueueVisualNetSync : NetworkBehaviour
{
    [SerializeField] private SceneObjectCatalogSO catalog;
    [SerializeField] private Transform[] queueSlots;

    [Header("Visual Scale")]
    [SerializeField] private Vector3 defaultVisualScale = Vector3.one;
    [SerializeField] private Vector3[] slotVisualScales; // optional per-slot override

    private NetworkList<int> queueIndices;
    private GameObject[] slotVisuals;

    // Tracks the catalog index currently shown per slot to avoid unnecessary
    // Destroy + Instantiate when the content hasn't changed.
    private int[] _displayedIndices;

    private void Awake()
    {
        queueIndices = new NetworkList<int>();

        int slotCount = queueSlots != null ? queueSlots.Length : 0;
        slotVisuals = new GameObject[slotCount];
        _displayedIndices = new int[slotCount];
        for (int i = 0; i < slotCount; i++)
            _displayedIndices[i] = -1;
    }

    public override void OnNetworkSpawn()
    {
        queueIndices.OnListChanged += OnQueueChanged;
        RebuildVisuals();
    }

    public override void OnNetworkDespawn()
    {
        queueIndices.OnListChanged -= OnQueueChanged;
        ClearAll();
    }

    private void OnQueueChanged(NetworkListEvent<int> _)
    {
        RebuildVisuals();
    }

    public void ServerSetQueue(List<SceneObjectSO> queued)
    {
        if (!IsServer) return;
        if (catalog == null) return;

        queueIndices.Clear();

        if (queued == null || queueSlots == null) return;
        int count = Mathf.Min(queued.Count, queueSlots.Length);

        for (int i = 0; i < count; i++)
        {
            SceneObjectSO so = queued[i];
            int idx = catalog.IndexOf(so);
            if (idx >= 0) queueIndices.Add(idx);
        }
    }

    private void RebuildVisuals()
    {
        if (catalog == null || queueSlots == null) return;

        int newCount = Mathf.Min(queueIndices.Count, queueSlots.Length);

        // First pass: destroy slots whose content changed or are no longer needed.
        for (int i = 0; i < slotVisuals.Length; i++)
        {
            int newIdx = i < newCount ? queueIndices[i] : -1;

            if (_displayedIndices[i] == newIdx) continue; // unchanged

            if (slotVisuals[i] != null)
            {
                Destroy(slotVisuals[i]);
                slotVisuals[i] = null;
            }
            _displayedIndices[i] = -1;
        }

        // Second pass: instantiate only slots that need a new visual.
        for (int i = 0; i < newCount; i++)
        {
            if (slotVisuals[i] != null) continue; // already correct

            Transform slot = queueSlots[i];
            if (slot == null) continue;

            int catalogIdx = queueIndices[i];
            SceneObjectSO so = catalog.Get(catalogIdx);
            if (so == null || so.prefab == null) continue;

            GameObject go = Instantiate(so.prefab.gameObject, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = GetScaleForSlot(i);

            DisableRuntimeComponents(go);
            slotVisuals[i] = go;
            _displayedIndices[i] = catalogIdx;
        }
    }

    private Vector3 GetScaleForSlot(int slotIndex)
    {
        if (slotVisualScales != null &&
            slotIndex >= 0 &&
            slotIndex < slotVisualScales.Length &&
            slotVisualScales[slotIndex] != Vector3.zero)
        {
            return slotVisualScales[slotIndex];
        }

        return defaultVisualScale;
    }

    private void ClearAll()
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
        foreach (var no in root.GetComponentsInChildren<NetworkObject>(true)) no.enabled = false;
        foreach (var nt in root.GetComponentsInChildren<Unity.Netcode.Components.NetworkTransform>(true)) nt.enabled = false;
        foreach (var soNet in root.GetComponentsInChildren<SceneObjectNet>(true)) soNet.enabled = false;
        foreach (var so in root.GetComponentsInChildren<SceneObject>(true)) so.enabled = false;

        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        foreach (var c in root.GetComponentsInChildren<Collider>(true)) c.enabled = false;
    }
}