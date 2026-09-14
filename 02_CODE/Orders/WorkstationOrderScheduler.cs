using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class WorkstationOrderScheduler : NetworkBehaviour
{
    [SerializeField] private float intervalSeconds = 30f;
    [SerializeField] private List<Table_StatePattern> workstations = new();
    [SerializeField] private bool autoFindInScene = false;

    [Header("Tutorial Control")]
    [SerializeField] private bool blockDuringTutorial = true;

    private Coroutine routine;

    private void Awake()
    {
        if (autoFindInScene)
            RebuildWorkstationListFromScene();
    }

    public override void OnNetworkSpawn()
    {
        if (autoFindInScene && (workstations == null || workstations.Count == 0))
            RebuildWorkstationListFromScene();

        if (IsServer)
            routine = StartCoroutine(Run());
    }

    public override void OnNetworkDespawn()
    {
        if (routine != null) StopCoroutine(routine);
    }

    private IEnumerator Run()
    {
        SetAllReady(false);

        while (true)
        {
            yield return new WaitForSeconds(intervalSeconds);

            if (blockDuringTutorial && TutorialRuntimeContext.IsTutorialRun)
                continue;

            TryRecoverStaleTables();
            TryScheduleRandomTable();
        }
    }

    private void RebuildWorkstationListFromScene()
    {
        workstations ??= new List<Table_StatePattern>();
        workstations.Clear();
        workstations.AddRange(Object.FindObjectsByType<Table_StatePattern>(FindObjectsSortMode.None));
    }

    private void TryScheduleRandomTable()
    {
        if (workstations == null || workstations.Count == 0) return;

        var candidates = new List<Table_StatePattern>();
        foreach (var ws in workstations)
        {
            if (ws == null) continue;
            if (!ws.CanStartSeatingNow()) continue;
            candidates.Add(ws);
        }

        if (candidates.Count == 0) return;

        Table_StatePattern chosen = candidates[Random.Range(0, candidates.Count)];
        chosen.BeginPreServiceSeating();
    }

    public bool TryStartTableNow(Table_StatePattern table)
    {
        if (!IsServer) return false;
        if (table == null) return false;

        // allow if table is valid/can start even if list wasn't curated
        if (!table.CanStartSeatingNow()) return false;

        table.BeginPreServiceSeating();
        return true;
    }

    public bool TryStartTableNowByDisplayName(string tableDisplayName)
    {
        if (!IsServer) return false;
        if (string.IsNullOrWhiteSpace(tableDisplayName)) return false;

        // First pass: exact display name
        if (TryStartByNameInternal(tableDisplayName)) return true;

        // Second pass: trimmed/case-insensitive for user-proofing
        string wanted = tableDisplayName.Trim();
        if (TryStartByNameInternal(wanted, ignoreCase: true)) return true;

        Debug.LogWarning($"[WorkstationOrderScheduler] No table found for DisplayName='{tableDisplayName}'.");
        return false;
    }

    public bool TryStartAnyAvailableTableNow()
    {
        if (!IsServer) return false;
        if (workstations == null || workstations.Count == 0) RebuildWorkstationListFromScene();

        foreach (var ws in workstations)
        {
            if (ws == null) continue;
            if (!ws.CanStartSeatingNow()) continue;
            ws.BeginPreServiceSeating();
            Debug.Log($"[WorkstationOrderScheduler] Fallback started table '{ws.name}'.");
            return true;
        }

        Debug.LogWarning("[WorkstationOrderScheduler] No available table for fallback trigger.");
        return false;
    }

    private bool TryStartByNameInternal(string name, bool ignoreCase = false)
    {
        if (workstations == null || workstations.Count == 0) RebuildWorkstationListFromScene();

        var comparison = ignoreCase ? System.StringComparison.OrdinalIgnoreCase : System.StringComparison.Ordinal;

        for (int i = 0; i < workstations.Count; i++)
        {
            var ws = workstations[i];
            if (ws == null) continue;

            var id = ws.GetComponent<TableIdentity>();
            if (id == null) continue;

            if (!string.Equals(id.DisplayName, name, comparison))
                continue;

            return TryStartTableNow(ws);
        }

        return false;
    }

    private void SetAllReady(bool ready)
    {
        if (workstations == null) return;

        foreach (var ws in workstations)
        {
            if (ws == null) continue;

            var net = ws.GetComponent<TableNetSync>();
            if (net != null)
                net.ServerSetReady(ready);
            else
                ws.SetOrderRequestReady(ready);
        }
    }

    private void TryRecoverStaleTables()
    {
        if (workstations == null) return;

        foreach (var ws in workstations)
        {
            if (ws == null) continue;

            // If table is in cleanup with no dirty items, reset it.
            if (ws.CurrentPhaseForNetSync == WorkstationPhaseId.NeedsCleanup && !ws.HasDirtyItems())
            {
                ws.ResetWorkstation();
                Debug.Log($"[WorkstationOrderScheduler] Recovered stale cleanup table '{ws.name}'.");
            }
        }
    }
}