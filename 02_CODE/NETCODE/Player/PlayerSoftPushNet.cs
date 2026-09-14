using Unity.Netcode;
using UnityEngine;



/* 
RESUM ARCH
Owner runs local separation every frame (zero latency, always responsive).
Server also runs separation and sends validated corrections at a fixed rate.
Pushes are NEVER applied to transform directly — always queued into
PlayerMovement.AddExternalPush() so every push goes through the same
wall-collision check as normal input. Players physically CANNOT be
pushed through walls, pillars or any collider in collisionMask.
PC rate is throttled (default: 20/s max) — not every frame.
Server clamps push magnitude to prevent any client from sending
exaggerated values (basic anti-cheat).
*/

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerMovement))]
public class PlayerSoftPushNet : NetworkBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Collider (auto-found if blank)")]
    [SerializeField] private CapsuleCollider capsule;

    [Header("Layers")]
    [Tooltip("Must contain ONLY the Player layer.")]
    [SerializeField] private LayerMask playerLayerMask;

    [Header("Spring feel")]
    [Tooltip("Separation spring: higher = snappier response.")]
    [SerializeField] private float springStrength = 9f;

    [Tooltip("Max metres the push can move a player per second — prevents teleporting.")]
    [SerializeField] private float maxSeparationPerSecond = 3.5f;

    [Tooltip("How much input is resisted when walking into another player. 0 = ghost, 1 = full stop.")]
    [Range(0f, 1f)]
    [SerializeField] private float pushbackResistance = 0.55f;

    [Header("Network")]
    [Tooltip("How often (seconds) the owner sends push corrections to remote players. 0.05 = 20/s.")]
    [SerializeField] private float rpcInterval = 0.08f;

    [Tooltip("Max overlapping colliders processed each frame (NonAlloc).")]
    [SerializeField] private int maxOverlapColliders = 16;

    [Tooltip("Ignore tiny overlap depths to skip unnecessary push work.")]
    [SerializeField] private float minPenetrationDepth = 0.005f;

    // ── Private ───────────────────────────────────────────────────────────────

    private PlayerMovement _movement;
    private float _rpcTimer;
    private int _physicsFrameSkip;
    private Collider[] _overlapHits;
    private Transform _rootTransform;

    private const int PhysicsCheckIntervalFrames = 2;

    // Server-side: maximum push magnitude we accept from any client per RPC
    // (= maxSeparationPerSecond × rpcInterval × small headroom factor)
    private float MaxAllowedPushMagnitude => maxSeparationPerSecond * rpcInterval * 2f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        if (capsule == null) capsule = GetComponentInChildren<CapsuleCollider>();
        _rootTransform = transform.root;
        EnsureOverlapBuffer();

        if (GraphicsQualityBootstrap.IsLowQuality)
            rpcInterval = 0.12f;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (capsule == null) return;

        _physicsFrameSkip++;
        if (_physicsFrameSkip < PhysicsCheckIntervalFrames)
            return;
        _physicsFrameSkip = 0;

        float frameDelta = Time.deltaTime * PhysicsCheckIntervalFrames;

        GetCapsuleWorld(out Vector3 cp1, out Vector3 cp2, out float cr);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            cp1,
            cp2,
            cr,
            _overlapHits,
            playerLayerMask,
            QueryTriggerInteraction.Ignore);

        _rpcTimer -= Time.deltaTime;

        bool hasBest = false;
        ulong bestTargetId = 0;
        Vector3 bestDir = Vector3.zero;
        float bestDepth = 0f;

        Transform capsuleTransform = capsule.transform;
        Vector3 capsulePosition = capsuleTransform.position;
        Quaternion capsuleRotation = capsuleTransform.rotation;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _overlapHits[i];
            if (hit == null) continue;
            if (hit.transform.root == _rootTransform) continue;

            if (!Physics.ComputePenetration(
                    capsule, capsulePosition, capsuleRotation,
                    hit, hit.transform.position, hit.transform.rotation,
                    out Vector3 dir, out float depth)) continue;
            if (depth < minPenetrationDepth) continue;

            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            dir.Normalize();

            float magnitude = Mathf.Min(depth * springStrength * frameDelta, maxSeparationPerSecond * frameDelta);
            _movement.AddExternalPush(dir * magnitude);

            if (_rpcTimer <= 0f)
            {
                NetworkObject otherNo = hit.transform.root.GetComponent<NetworkObject>();
                if (otherNo == null) continue;

                if (!hasBest || depth > bestDepth)
                {
                    hasBest = true;
                    bestTargetId = otherNo.NetworkObjectId;
                    bestDir = -dir;
                    bestDepth = depth;
                }
            }
        }

        if (_rpcTimer <= 0f)
        {
            if (hasBest)
            {
                NetRpcStats.Hit(nameof(NotifyServerOfPushServerRpc));
                NotifyServerOfPushServerRpc(bestTargetId, bestDir, bestDepth);
            }
            _rpcTimer = rpcInterval;
        }

        for (int i = 0; i < hitCount; i++)
            _overlapHits[i] = null;
    }
    // ── Public API ────────────────────────────────────────────────────────────

    public float GetResistanceFactor() => pushbackResistance;

    // ── Server RPC (called by pusher's client) ────────────────────────────────

    [ServerRpc(RequireOwnership = false)]
    private void NotifyServerOfPushServerRpc(
        ulong targetId,
        Vector3 rawDir,
        float rawDepth,
        ServerRpcParams rpcParams = default)
    {
        NetRpcStats.Hit(nameof(NotifyServerOfPushServerRpc));
        // ── Server-side validation ────────────────────────────────────────────
        // 1. Direction must be horizontal (client should not send vertical pushes)
        rawDir.y = 0f;
        if (rawDir.sqrMagnitude < 0.0001f) return;
        rawDir.Normalize();

        // 2. Clamp depth so no client can send exaggerated push magnitudes
        float safeDt    = rpcInterval; // worst-case delta time for the interval
        float maxDepth  = MaxAllowedPushMagnitude / (springStrength * safeDt);
        float clampedDepth = Mathf.Clamp(rawDepth, 0f, maxDepth);

        // 3. Verify target still exists
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                .TryGetValue(targetId, out NetworkObject targetNo)) return;

        PlayerSoftPushNet targetPush = targetNo.GetComponent<PlayerSoftPushNet>();
        if (targetPush == null) return;

        // 4. Recompute safe magnitude on the server (not trusting client value)
        float magnitude = Mathf.Min(
            clampedDepth * springStrength * safeDt,
            maxSeparationPerSecond * safeDt);

        // Send validated push to the target's owner only
        targetPush.ReceivePushClientRpc(
            rawDir * magnitude,
            new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { targetNo.OwnerClientId }
                }
            });
    }

    // ── Client RPC (received by pushed player's owner) ────────────────────────

    [ClientRpc]
    private void ReceivePushClientRpc(Vector3 validatedStep, ClientRpcParams _ = default)
    {
        NetRpcStats.Hit(nameof(ReceivePushClientRpc));
        // Queue into PlayerMovement — will be wall-checked before being applied
        _movement.AddExternalPush(validatedStep);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void GetCapsuleWorld(out Vector3 p1, out Vector3 p2, out float r)
    {
        float height = Mathf.Max(capsule.height, capsule.radius * 2f);
        float half   = height * 0.5f - capsule.radius;
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 up   = capsule.transform.up;

        p1 = center + up * half;
        p2 = center - up * half;
        r  = capsule.radius * Mathf.Max(
                capsule.transform.lossyScale.x,
                capsule.transform.lossyScale.z);
    }

    private void EnsureOverlapBuffer()
    {
        int bufferSize = Mathf.Max(4, maxOverlapColliders);
        if (_overlapHits == null || _overlapHits.Length != bufferSize)
            _overlapHits = new Collider[bufferSize];
    }

    private void OnValidate()
    {
        rpcInterval = Mathf.Max(0.02f, rpcInterval);
        maxOverlapColliders = Mathf.Max(4, maxOverlapColliders);
        minPenetrationDepth = Mathf.Clamp(minPenetrationDepth, 0.001f, 0.1f);
        EnsureOverlapBuffer();
    }
}