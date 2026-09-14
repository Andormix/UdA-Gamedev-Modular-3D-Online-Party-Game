using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class TableNpcSeatingController : NetworkBehaviour
{
    [Header("Setup")]
    [SerializeField] private Table_StatePattern table;
    [SerializeField] private TableNetSync tableNetSync;
    [SerializeField] private TableNpcAgent npcPrefab;
    [SerializeField] private Transform npcSpawnOrigin;
    [SerializeField] private Transform npcExitPoint; // where NPCs walk away before despawn

    [Header("Spawn Count")]
    [SerializeField] private int minNpc = 1;
    [SerializeField] private int maxNpc = 4;

    [Header("Cleanup")]
    [SerializeField] private float forceDespawnDelay = 4f; // fail-safe

    private readonly List<TableNpcAgent> activeNpcs = new();
    private readonly Dictionary<TableNpcAgent, TableSeatPoint> seatByNpc = new();

    private int targetSeated;
    private int seatedCount;
    private Action onAllSeated;
    private bool allSeatedNotified;

    private int pendingWalkAway;
    private bool cleanupInProgress;

    private Coroutine forceDespawnRoutine;
    private int cleanupGeneration = 0;

    private Action onCleanupFinished;
    private bool batchDespawnNotified;

    private void Awake()
    {
        if (table == null) table = GetComponent<Table_StatePattern>();
        if (tableNetSync == null) tableNetSync = GetComponent<TableNetSync>();
        if (npcSpawnOrigin == null) npcSpawnOrigin = transform;
        if (npcExitPoint == null) npcExitPoint = transform;
    }

    public bool BeginSeating(Action onCompleted)
    {
        if (!IsServer) return false;

        if (table == null || npcPrefab == null) return false;
        if (cleanupInProgress || activeNpcs.Count > 0) return false;

        // CRITICAL ERIC: cancel old force-despawn coroutine from previous cleanup cycle
        if (forceDespawnRoutine != null)
        {
            StopCoroutine(forceDespawnRoutine);
            forceDespawnRoutine = null;
        }

        cleanupGeneration++; // invalidate old cleanup callbacks

        onAllSeated = onCompleted;
        targetSeated = 0;
        seatedCount = 0;
        allSeatedNotified = false;

        table?.SetNpcSeatingCycleActive(true);

        int seatCount = table.GetSeatCount();
        if (seatCount <= 0)
        {
            table?.SetNpcSeatingCycleActive(false);
            return false;
        }

        int desired = UnityEngine.Random.Range(minNpc, maxNpc + 1);
        int toSpawn = Mathf.Clamp(desired, 1, seatCount);

        var freeSeats = new List<TableSeatPoint>(table.GetFreeSeats());
        Shuffle(freeSeats);

        for (int i = 0; i < toSpawn && i < freeSeats.Count; i++)
        {
            var seat = freeSeats[i];
            if (seat == null || seat.IsOccupied) continue;

            seat.SetOccupied(true);

            var npc = Instantiate(npcPrefab, npcSpawnOrigin.position, npcSpawnOrigin.rotation);
            var no = npc.GetComponent<NetworkObject>();
            no.Spawn(true);

            var outfit = npc.GetComponent<NpcGuestOutfitRandomizer>();
            if (outfit != null)
            {
                outfit.ServerInitializeRandomOutfit();
            }
            else
            {
                Debug.LogWarning("[TableNpcSeatingController] NPC guest is missing NpcGuestOutfitRandomizer. Outfit randomization skipped.", npc);
            }

            npc.OnSeatedServer += HandleNpcSeatedServer;
            npc.OnWalkedAwayServer += HandleNpcWalkedAwayServer;

            activeNpcs.Add(npc);
            seatByNpc[npc] = seat;
            targetSeated++;

            npc.ServerGoToSeat(seat.SitAnchor, seat.SitFinalAnchor);
        }

        if (targetSeated == 0)
        {
            table?.SetNpcSeatingCycleActive(false);
            return false;
        }

        return true;
    }

    private void HandleNpcSeatedServer(TableNpcAgent npc)
    {
        if (!IsServer) return;
        if (!activeNpcs.Contains(npc)) return; // guard stale events

        seatedCount++;
        if (seatedCount >= targetSeated)
            NotifyAllSeatedOnce();
    }

    private void NotifyAllSeatedOnce()
    {
        if (allSeatedNotified) return;
        allSeatedNotified = true;
        onAllSeated?.Invoke();
    }

    public void CleanupAllServer()
    {
        if (!IsServer) return;
        if (cleanupInProgress) return;

        cleanupInProgress = true;
        int myGeneration = ++cleanupGeneration;
        batchDespawnNotified = false;

        if (activeNpcs.Count == 0)
        {
            table?.ReleaseAllSeats();
            cleanupInProgress = false;
            table?.SetNpcSeatingCycleActive(false);
            NotifyCleanupFinished();
            return;
        }

        pendingWalkAway = 0;
        Vector3 exit = npcExitPoint != null ? npcExitPoint.position : transform.position;

        for (int i = 0; i < activeNpcs.Count; i++)
        {
            var npc = activeNpcs[i];
            if (npc == null) continue;
            pendingWalkAway++;
            npc.ServerStandAndWalkAway(exit);
        }

        if (forceDespawnRoutine != null) StopCoroutine(forceDespawnRoutine);
        forceDespawnRoutine = StartCoroutine(ForceDespawnAfterDelay(forceDespawnDelay, myGeneration));
    }

    private IEnumerator ForceDespawnAfterDelay(float delay, int generation)
    {
        yield return new WaitForSeconds(delay);

        // ignore stale coroutine from previous cycle
        if (generation != cleanupGeneration) yield break;

        ForceDespawnAllImmediate();
        NotifyBatchDespawnFinishedOnce();
        cleanupInProgress = false;
        table?.SetNpcSeatingCycleActive(false);
        forceDespawnRoutine = null;
        NotifyCleanupFinished();
    }

    private void DespawnOne(TableNpcAgent npc)
    {
        if (npc == null) return;

        npc.OnSeatedServer -= HandleNpcSeatedServer;
        npc.OnWalkedAwayServer -= HandleNpcWalkedAwayServer;

        if (seatByNpc.TryGetValue(npc, out var seat) && seat != null)
            seat.SetOccupied(false);

        seatByNpc.Remove(npc);
        activeNpcs.Remove(npc);

        var no = npc.GetComponent<NetworkObject>();
        if (no != null && no.IsSpawned) no.Despawn(true);
        else Destroy(npc.gameObject);
    }

    private void ForceDespawnAllImmediate()
    {
        for (int i = activeNpcs.Count - 1; i >= 0; i--)
        {
            var npc = activeNpcs[i];
            if (npc == null) continue;
            DespawnOne(npc);
        }

        activeNpcs.Clear();
        seatByNpc.Clear();
        table?.ReleaseAllSeats();

        pendingWalkAway = 0;
        cleanupInProgress = false;

        if (table != null)
            table.SetNpcSeatingCycleActive(false);
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    public bool HasActiveNpcsOrCleanup()
    {
        return activeNpcs.Count > 0 || cleanupInProgress;
    }

    private void HandleNpcWalkedAwayServer(TableNpcAgent npc)
    {
        if (!IsServer) return;

        DespawnOne(npc);

        pendingWalkAway = Mathf.Max(0, pendingWalkAway - 1);
        if (pendingWalkAway == 0)
        {
            table?.ReleaseAllSeats();
            cleanupInProgress = false;
            if (table != null)
                table.SetNpcSeatingCycleActive(false);

            NotifyBatchDespawnFinishedOnce();
            NotifyCleanupFinished();
        }
    }

    private void NotifyCleanupFinished()
    {
        var cb = onCleanupFinished;
        onCleanupFinished = null;
        cb?.Invoke();
    }

    public void CleanupAllServer(Action onFinished)
    {
        if (!IsServer) return;

        onCleanupFinished = onFinished;
        CleanupAllServer();
    }

    public void SetAllNpcsConsumingServer(bool value)
    {
        if (!IsServer) return;

        for (int i = 0; i < activeNpcs.Count; i++)
        {
            var npc = activeNpcs[i];
            if (npc == null) continue;
            npc.ServerSetConsuming(value);
        }
    }

    private void NotifyBatchDespawnFinishedOnce()
    {
        if (batchDespawnNotified) return;
        batchDespawnNotified = true;
        tableNetSync?.ServerNotifyNpcBatchDespawned();
    }
}