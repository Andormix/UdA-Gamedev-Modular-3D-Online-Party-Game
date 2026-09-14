using UnityEngine;
using UnityEngine.AI;

public class PlayerClickMoveController : MonoBehaviour
{
    [Header("Click Move")]
    [SerializeField] private bool enableClickMove = true;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float pointerRayMaxDistance = 250f;
    [SerializeField] private float stoppingDistance = 0.15f;

    [Header("NavMesh Assist")]
    [SerializeField] private bool useNavMeshAssist = true;
    [Tooltip("Maximum distance used to snap a clicked point onto the NavMesh.")]
    [SerializeField] private float navMeshSampleDistance = 2f;
    [Tooltip("If enabled, only complete NavMesh paths are accepted for click movement.")]
    [SerializeField] private bool requireCompleteNavMeshPath = true;
    [Tooltip("Enable temporary debug logs for rejected destinations and repath failures.")]
    [SerializeField] private bool verboseNavMeshLogs;

    [Header("Dynamic Repath")]
    [Tooltip("When enabled, click-move can rebuild the path if progress stalls.")]
    [SerializeField] private bool enableDynamicRepath = true;
    [Tooltip("How often the controller checks progress toward the destination.")]
    [SerializeField] private float stuckCheckInterval = 0.25f;
    [Tooltip("Minimum forward progress required per check before movement is considered stalled.")]
    [SerializeField] private float stuckProgressEpsilon = 0.05f;
    [Tooltip("How long movement can stall before requesting a local repath.")]
    [SerializeField] private float stuckTimeBeforeRepath = 0.75f;
    [Tooltip("Minimum cooldown between dynamic repath attempts.")]
    [SerializeField] private float repathCooldown = 0.4f;

    private NavMeshPath navMeshPath;
    private bool hasDestination;
    private bool hasPathCorners;
    private bool hasValidNavPath;
    private int pathCornerIndex;
    private Vector3 destination;
    private float nextStuckCheckTime;
    private float nextAllowedRepathTime;
    private float lastDistanceToDestination = float.PositiveInfinity;
    private float stalledTime;

    private void Awake()
    {
        navMeshPath = new NavMeshPath();
    }

    public bool TrySetDestinationFromPointer(Ray pointerRay)
    {
        if (!enableClickMove) return false;

        if (!Physics.Raycast(pointerRay, out RaycastHit groundHit, pointerRayMaxDistance, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 target = groundHit.point;

        if (useNavMeshAssist)
        {
            if (!NavMesh.SamplePosition(target, out NavMeshHit navHit, navMeshSampleDistance, NavMesh.AllAreas))
            {
                LogVerbose("Click destination rejected: no nearby NavMesh position.");
                CancelMove();
                return false;
            }

            target = navHit.position;

            if (!TryBuildPath(target, out bool pathHasCorners))
            {
                LogVerbose("Click destination rejected: NavMesh path was invalid or incomplete.");
                CancelMove();
                return false;
            }

            hasDestination = true;
            destination = target;
            hasValidNavPath = true;
            hasPathCorners = pathHasCorners;
            pathCornerIndex = hasPathCorners ? 1 : 0;
            ResetStuckTracking();
            return true;
        }

        hasDestination = true;
        destination = target;
        hasPathCorners = false;
        hasValidNavPath = false;
        pathCornerIndex = 0;
        ResetStuckTracking();

        return true;
    }

    public Vector2 GetMoveVector()
    {
        if (!hasDestination) return Vector2.zero;
        if (useNavMeshAssist && !hasValidNavPath) return Vector2.zero;

        if (useNavMeshAssist && enableDynamicRepath)
            TryRepathIfStalled();

        Vector3 currentTarget = destination;
        if (hasPathCorners)
        {
            float cornerReachDistance = Mathf.Max(stoppingDistance, 0.2f);
            float cornerReachDistanceSq = cornerReachDistance * cornerReachDistance;

            while (pathCornerIndex < navMeshPath.corners.Length)
            {
                Vector3 corner = navMeshPath.corners[pathCornerIndex];
                corner.y = transform.position.y;

                if ((corner - transform.position).sqrMagnitude <= cornerReachDistanceSq)
                {
                    pathCornerIndex++;
                    continue;
                }

                currentTarget = corner;
                break;
            }

            if (pathCornerIndex >= navMeshPath.corners.Length)
                currentTarget = destination;
        }

        Vector3 toTarget = currentTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= stoppingDistance * stoppingDistance)
        {
            if (!hasPathCorners || pathCornerIndex >= navMeshPath.corners.Length - 1)
            {
                CancelMove();
                return Vector2.zero;
            }
        }

        Vector3 dir = toTarget.normalized;
        return new Vector2(dir.x, dir.z);
    }

    public void CancelMove()
    {
        hasDestination = false;
        hasPathCorners = false;
        hasValidNavPath = false;
        pathCornerIndex = 0;
        ResetStuckTracking();
    }

    private bool TryBuildPath(Vector3 targetPosition, out bool pathHasCorners)
    {
        pathHasCorners = false;
        if (navMeshPath == null)
            navMeshPath = new NavMeshPath();

        if (!NavMesh.CalculatePath(transform.position, targetPosition, NavMesh.AllAreas, navMeshPath))
            return false;

        if (navMeshPath.status == NavMeshPathStatus.PathInvalid)
            return false;

        if (requireCompleteNavMeshPath && navMeshPath.status != NavMeshPathStatus.PathComplete)
            return false;

        if (navMeshPath.corners == null || navMeshPath.corners.Length == 0)
            return false;

        pathHasCorners = navMeshPath.corners.Length > 1;
        return true;
    }

    private void TryRepathIfStalled()
    {
        if (!hasDestination || Time.time < nextStuckCheckTime)
            return;

        nextStuckCheckTime = Time.time + Mathf.Max(0.05f, stuckCheckInterval);

        Vector3 flatCurrent = transform.position;
        flatCurrent.y = 0f;
        Vector3 flatDestination = destination;
        flatDestination.y = 0f;
        float distance = Vector3.Distance(flatCurrent, flatDestination);

        if (float.IsPositiveInfinity(lastDistanceToDestination))
        {
            lastDistanceToDestination = distance;
            return;
        }

        float progress = lastDistanceToDestination - distance;
        if (progress > stuckProgressEpsilon || distance <= stoppingDistance * 2f)
        {
            stalledTime = 0f;
        }
        else
        {
            stalledTime += Mathf.Max(0.05f, stuckCheckInterval);
        }

        lastDistanceToDestination = distance;

        if (stalledTime < stuckTimeBeforeRepath || Time.time < nextAllowedRepathTime)
            return;

        nextAllowedRepathTime = Time.time + Mathf.Max(0.1f, repathCooldown);
        stalledTime = 0f;

        if (!TryBuildPath(destination, out bool pathHasCorners))
        {
            hasValidNavPath = false;
            hasPathCorners = false;
            pathCornerIndex = 0;
            LogVerbose("Dynamic repath failed. Click-move paused until a new valid destination is set.");
            return;
        }

        hasValidNavPath = true;
        hasPathCorners = pathHasCorners;
        pathCornerIndex = pathHasCorners ? 1 : 0;
    }

    private void ResetStuckTracking()
    {
        nextStuckCheckTime = 0f;
        nextAllowedRepathTime = 0f;
        lastDistanceToDestination = float.PositiveInfinity;
        stalledTime = 0f;
    }

    private void LogVerbose(string message)
    {
        if (!verboseNavMeshLogs) return;
        Debug.Log($"[{nameof(PlayerClickMoveController)}] {message}", this);
    }
}
