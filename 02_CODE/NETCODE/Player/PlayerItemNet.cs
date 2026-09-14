using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Player))]
public class PlayerItemNet : NetworkBehaviour
{
    private Player player;
    private void Awake() => player = GetComponent<Player>();

    private const int PickupMaxHits = 32;
    private readonly Collider[] pickupHits = new Collider[PickupMaxHits];
    private const int DropAssistMaxHits = 32;
    private readonly Collider[] dropAssistHits = new Collider[DropAssistMaxHits];
    private readonly HashSet<int> _acceptTriedIds = new HashSet<int>();

    [Header("Anti-spam")]
    [SerializeField] private float pickupRequestMinInterval = 0.10f; // 10/s max
    [SerializeField] private float dropRequestMinInterval = 0.08f;   // 12.5/s max
    [SerializeField] private float serverPickupMaxDistance = 2.25f;  // safety cap
    [SerializeField] private float dropNearAssistRadius = 1.25f;

    [Header("Drop Tuning")]
    [SerializeField] private float clientDropForwardDistance = 0.35f;
    [SerializeField] private float clientDropVerticalOffset = 0.5f;
    [SerializeField] private float serverMaxDropDistance = 1.25f;
    [SerializeField] private bool enableDropImpulse = false;
    [SerializeField] private float dropImpulseForward = 0f;
    [SerializeField] private float dropImpulseUpward = 0f;
    [SerializeField] private ForceMode dropImpulseForceMode = ForceMode.Impulse;

    private float nextPickupAllowedTime;
    private float nextDropAllowedTime;

    public void RequestPickupNearest(float radius, LayerMask mask)
    {
        if (!IsOwner) return;
        if (Time.time < nextPickupAllowedTime) return;

        nextPickupAllowedTime = Time.time + pickupRequestMinInterval;
        RequestPickupNearestServerRpc(radius, mask.value);
    }

    public bool RequestPickupSpecific(SceneObject target, float maxDistance)
    {
        if (!IsOwner || target == null) return false;
        if (Time.time < nextPickupAllowedTime) return false;

        NetworkObject netObj = target.GetComponent<NetworkObject>();
        if (netObj == null || !netObj.IsSpawned) return false;

        nextPickupAllowedTime = Time.time + pickupRequestMinInterval;
        RequestPickupSpecificServerRpc(netObj.NetworkObjectId, maxDistance);
        return true;
    }

    public void RequestDrop(Vector3 dropPosition)
    {
        if (!IsOwner) return;
        if (Time.time < nextDropAllowedTime) return;

        nextDropAllowedTime = Time.time + dropRequestMinInterval;
        RequestDropServerRpc(dropPosition);
    }

    [ServerRpc]
    private void RequestPickupNearestServerRpc(float radius, int maskValue, ServerRpcParams rpcParams = default)
    {
        NetRpcStats.Hit(nameof(RequestPickupNearestServerRpc));
        if (player == null) return;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return;
        if (!carry.HasFreeSlot) return;

        // Clamp radius server-side (trust NOO client)
        float safeRadius = Mathf.Clamp(radius, 0.1f, serverPickupMaxDistance);

        Vector3 center = player.transform.position + Vector3.up * 0.5f;

        int hitCount = Physics.OverlapSphereNonAlloc(center, safeRadius, pickupHits, maskValue, QueryTriggerInteraction.Ignore);
        if (hitCount <= 0) return;

        SceneObject bestObj = null;
        float bestDistSq = float.MaxValue;
        float maxDistSq = safeRadius * safeRadius;

        for (int i = 0; i < hitCount; i++)
        {
            var h = pickupHits[i];
            if (h == null) continue;

            var obj = h.GetComponentInParent<SceneObject>();
            if (obj == null) continue;

            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj == null || !netObj.IsSpawned) continue;

            if (!obj.CanBePickedUp()) continue;

            float distSq = (obj.transform.position - center).sqrMagnitude;
            if (distSq > maxDistSq) continue;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestObj = obj;
            }
        }

        if (bestObj != null)
            carry.ServerTryPush(bestObj);
    }

    [ServerRpc]
    private void RequestPickupSpecificServerRpc(ulong targetNetworkObjectId, float maxDistance, ServerRpcParams rpcParams = default)
    {
        NetRpcStats.Hit(nameof(RequestPickupSpecificServerRpc));
        if (player == null) return;
        if (NetworkManager.Singleton == null || NetworkManager.Singleton.SpawnManager == null) return;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null || !carry.HasFreeSlot) return;

        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetworkObjectId, out NetworkObject no) || no == null)
            return;

        SceneObject target = no.GetComponent<SceneObject>();
        if (target == null || !target.CanBePickedUp()) return;

        float safeDistance = Mathf.Clamp(maxDistance, 0.1f, serverPickupMaxDistance);
        Vector3 center = player.transform.position + Vector3.up * 0.5f;
        if ((target.transform.position - center).sqrMagnitude > safeDistance * safeDistance)
            return;

        carry.ServerTryPush(target);
    }

    [ServerRpc]
    private void RequestDropServerRpc(Vector3 dropPosition, ServerRpcParams rpcParams = default)
    {
        NetRpcStats.Hit(nameof(RequestDropServerRpc));
        if (player == null) return;

        var carry = player.GetComponent<PlayerCarryNet>();
        if (carry == null) return;
        if (!carry.HasAny) return;

        // Safety clamp: don't allow far tele-drop
        Vector3 from = player.transform.position + Vector3.up * 0.5f;
        Vector3 delta = dropPosition - from;
        float maxDropDist = Mathf.Max(0f, serverMaxDropDistance);
        if (maxDropDist <= 0f)
        {
            dropPosition = from;
        }
        else if (delta.sqrMagnitude > maxDropDist * maxDropDist)
        {
            dropPosition = from + delta.normalized * maxDropDist;
        }

        if (TryServerAcceptDropNear(dropPosition, carry))
            return;

        carry.ServerDropTop(dropPosition, BuildServerDropImpulse(player.transform.forward), dropImpulseForceMode);
    }

    private bool TryServerAcceptDropNear(Vector3 dropPosition, PlayerCarryNet carry)
    {
        if (carry == null || !carry.HasAny) return false;

        float radius = Mathf.Clamp(dropNearAssistRadius, 0.1f, 3f);
        int hitCount = Physics.OverlapSphereNonAlloc(
            dropPosition,
            radius,
            dropAssistHits,
            ~0,
            QueryTriggerInteraction.Collide
        );
        if (hitCount <= 0) return false;

        // Try table first so request-phase item delivery keeps priority when multiple
        // station colliders overlap around the drop point.
        if (TryAcceptByType<TableNetSync>(hitCount, dropPosition, (target, playerRef) => target.ServerTryAcceptDroppedFromPlayer(playerRef)))
            return true;
        if (TryAcceptByType<Workstation>(hitCount, dropPosition, (target, playerRef) => target.ServerTryAcceptDroppedFromPlayer(playerRef)))
            return true;
        if (TryAcceptByType<SinkStation>(hitCount, dropPosition, (target, playerRef) => target.ServerTryAcceptDroppedFromPlayer(playerRef)))
            return true;

        return false;
    }

    private bool TryAcceptByType<T>(int hitCount, Vector3 dropPosition, System.Func<T, Player, bool> acceptor)
        where T : Component
    {
        _acceptTriedIds.Clear();
        while (true)
        {
            T bestTarget = null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var hit = dropAssistHits[i];
                if (hit == null) continue;

                T candidate = hit.GetComponentInParent<T>();
                if (candidate == null) continue;

                int id = candidate.GetInstanceID();
                if (_acceptTriedIds.Contains(id)) continue;

                float distSq = (candidate.transform.position - dropPosition).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestTarget = candidate;
                }
            }

            if (bestTarget == null) return false;

            int bestId = bestTarget.GetInstanceID();
            _acceptTriedIds.Add(bestId);

            if (acceptor(bestTarget, player))
                return true;
        }
    }

    public Vector3 GetLocalSuggestedDropPosition(Transform originTransform)
    {
        if (originTransform == null)
            return Vector3.zero;

        float forwardDistance = Mathf.Max(0f, clientDropForwardDistance);
        float verticalOffset = Mathf.Max(0f, clientDropVerticalOffset);
        return originTransform.position + originTransform.forward * forwardDistance + Vector3.up * verticalOffset;
    }

    private Vector3 BuildServerDropImpulse(Vector3 forward)
    {
        if (!enableDropImpulse)
            return Vector3.zero;

        Vector3 flatForward = forward;
        flatForward.y = 0f;
        flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.zero;

        float forwardStrength = Mathf.Max(0f, dropImpulseForward);
        float upwardStrength = Mathf.Max(0f, dropImpulseUpward);
        return flatForward * forwardStrength + Vector3.up * upwardStrength;
    }
}