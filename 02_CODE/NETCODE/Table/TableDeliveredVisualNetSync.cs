using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public class TableDeliveredVisualNetSync : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private SceneObjectCatalogSO catalog;
    [SerializeField] private Table_StatePattern table;

    [Header("Slots where delivered items are shown")]
    [SerializeField] private Transform[] deliveredSlots;

    // Replicated item indices currently shown on table (order = slot order)
    private NetworkList<int> deliveredItemIndices;

    // Client/server local spawned visuals (one entry per slot, null = empty)
    private GameObject[] slotVisuals;

    // Tracks the catalog index currently shown per slot to avoid needless
    // Destroy + Instantiate when the same item is already displayed.
    private int[] _displayedIndices;

    private void Awake()
    {
        deliveredItemIndices = new NetworkList<int>();
        if (table == null) table = GetComponent<Table_StatePattern>();

        if (deliveredSlots != null && deliveredSlots.Length > 0)
        {
            slotVisuals = new GameObject[deliveredSlots.Length];
            _displayedIndices = new int[deliveredSlots.Length];
            for (int i = 0; i < _displayedIndices.Length; i++)
                _displayedIndices[i] = -1;
        }
    }

    public override void OnNetworkSpawn()
    {
        deliveredItemIndices.OnListChanged += OnDeliveredListChanged;
        RebuildVisualsFromNet();
    }

    public override void OnNetworkDespawn()
    {
        deliveredItemIndices.OnListChanged -= OnDeliveredListChanged;
        ClearSpawnedVisuals();
    }

    private void OnDeliveredListChanged(NetworkListEvent<int> _)
    {
        RebuildVisualsFromNet();
    }

    // SERVER API
    public void ServerSetDeliveredVisuals(List<SceneObjectSO> delivered)
    {
        if (!IsServer) return;
        if (catalog == null) return;

        deliveredItemIndices.Clear();
        if (delivered == null) return;

        int max = deliveredSlots != null ? deliveredSlots.Length : 0;
        int count = Mathf.Min(delivered.Count, max);

        for (int i = 0; i < count; i++)
        {
            var so = delivered[i];
            int idx = catalog.IndexOf(so);
            if (idx >= 0) deliveredItemIndices.Add(idx);
        }
    }

    public void ServerClearDeliveredVisuals()
    {
        if (!IsServer) return;
        deliveredItemIndices.Clear();
    }

    private void RebuildVisualsFromNet()
    {
        if (catalog == null || deliveredSlots == null || slotVisuals == null) return;

        int newCount = Mathf.Min(deliveredItemIndices.Count, deliveredSlots.Length);

        // First pass: destroy any slot whose content changed or is no longer needed.
        for (int i = 0; i < slotVisuals.Length; i++)
        {
            int newIdx = i < newCount ? deliveredItemIndices[i] : -1;

            if (_displayedIndices[i] == newIdx) continue; // unchanged, skip

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

            Transform slot = deliveredSlots[i];
            if (slot == null) continue;

            int catalogIdx = deliveredItemIndices[i];
            SceneObjectSO so = catalog.Get(catalogIdx);
            if (so == null || so.prefab == null) continue;

            GameObject go = Instantiate(so.prefab.gameObject, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // visual only: strip network/physics behavior
            DisableRuntimeComponents(go);

            slotVisuals[i] = go;
            _displayedIndices[i] = catalogIdx;
        }
    }

    private void ClearSpawnedVisuals()
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

        foreach (var col in root.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
    }
}