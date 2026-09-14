using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float movementsSpeed = 5f;

    // Eric Im your pas self: Do NOT include the Player layer here.
    // Player-vs-player separation is handled by PlayerSoftPushNet.
    [SerializeField] private LayerMask collisionMask = ~0;

    private bool isWalking;
    private Transform _cachedTransform;

    // ── External push (D4.2) ──────────────────────────────────────────────────
    // Written by PlayerSoftPushNet (same frame, before Move() is called).
    // Consumed atomically inside Move() — never stale, never doubled.
    private Vector3 _pendingPush = Vector3.zero;
    private PlayerSoftPushNet _softPush;

    // ── Constants (match PlayerSoftPushNet) ───────────────────────────────────
    private const float PlayerRadius = 0.2f;
    private const float PlayerHeight = 1.5f;

    // ── Unity ─────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _softPush = GetComponent<PlayerSoftPushNet>();
        _cachedTransform = transform;
    }

    // ── Public API ────────────────────────────────────────────────────────────


    // Called by PlayerSoftPushNet once per frame BEFORE Move().
    // Accumulates push — never applied directly to position.
    public void AddExternalPush(Vector3 push) => _pendingPush += push;

    /// Called by Player.Update() each frame (owner-only).
    public void Move(Vector2 inputVector)
    {
        float movementDistance = movementsSpeed * Time.deltaTime;
        bool hasInput = inputVector.sqrMagnitude > 0.0001f;

        Vector3 p1 = _cachedTransform.position;
        Vector3 p2 = _cachedTransform.position + Vector3.up * PlayerHeight;

        // ── 1. Resolve input direction through wall collision ─────────────────
        Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 inputStep = ResolveStep(moveDir, movementDistance, p1, p2);

        // ── 2. D4.2 soft resistance ───────────────────────────────────────────
        // Partially cancel input when pressing directly INTO another player.
        // This gives the "pushing against mass" feel without a hard wall.
        if (_softPush != null && inputStep != Vector3.zero && _pendingPush != Vector3.zero)
        {
            float dot = Vector3.Dot(moveDir, _pendingPush.normalized);
            if (dot < 0f)
            {
                // dot is negative = moving into push direction
                // Scale back input proportionally to how head-on the collision is
                float reduction = -dot * _softPush.GetResistanceFactor() * movementDistance;
                inputStep -= moveDir * reduction;
            }
        }

        // ── 3. Consume push — wall-checked, then zeroed atomically ───────────
        Vector3 pushStep = Vector3.zero;
        if (_pendingPush != Vector3.zero)
        {
            pushStep   = WallSafeStep(_pendingPush, p1, p2);
            _pendingPush = Vector3.zero; // ← atomic consume, same frame
        }

        // ── 4. Apply combined step ────────────────────────────────────────────
        _cachedTransform.position += inputStep + pushStep;

        // ── 5. Rotate toward movement ─────────────────────────────────────────
        if (hasInput)
            _cachedTransform.forward = Vector3.Slerp(
                _cachedTransform.forward, moveDir,
                2f * movementsSpeed * Time.deltaTime);

        isWalking = hasInput;
    }

    public bool IsWalking() => isWalking;

    // ── Private helpers ───────────────────────────────────────────────────────

    // Resolves a movement direction + distance against walls.
    // Tries full → X-axis → Z-axis sliding. Returns safe displacement.
    private Vector3 ResolveStep(Vector3 dir, float distance, Vector3 p1, Vector3 p2)
    {
        if (dir == Vector3.zero) return Vector3.zero;

        if (!Cast(p1, p2, dir, distance))
            return dir * distance;

        Vector3 dirX = new Vector3(dir.x, 0f, 0f).normalized;
        if (dir.x != 0f && !Cast(p1, p2, dirX, distance))
            return dirX * distance;

        Vector3 dirZ = new Vector3(0f, 0f, dir.z).normalized;
        if (dir.z != 0f && !Cast(p1, p2, dirZ, distance))
            return dirZ * distance;

        return Vector3.zero;
    }


    // Wall-checks an arbitrary displacement (from a push, not a unit dir + dist).
    // Tries full → X-only → Z-only. Returns safe displacement or zero.
    private Vector3 WallSafeStep(Vector3 desired, Vector3 p1, Vector3 p2)
    {
        if (desired == Vector3.zero) return Vector3.zero;

        float mag = desired.magnitude;
        Vector3 dir = desired / mag;

        if (!Cast(p1, p2, dir, mag))
            return desired;

        Vector3 stepX = new Vector3(desired.x, 0f, 0f);
        if (stepX != Vector3.zero && !Cast(p1, p2, stepX.normalized, stepX.magnitude))
            return stepX;

        Vector3 stepZ = new Vector3(0f, 0f, desired.z);
        if (stepZ != Vector3.zero && !Cast(p1, p2, stepZ.normalized, stepZ.magnitude))
            return stepZ;

        return Vector3.zero; // fully blocked — player stays put, does NOT clip through
    }

    private bool Cast(Vector3 p1, Vector3 p2, Vector3 dir, float dist)
        => Physics.CapsuleCast(p1, p2, PlayerRadius, dir, dist,
                                collisionMask, QueryTriggerInteraction.Ignore);
}