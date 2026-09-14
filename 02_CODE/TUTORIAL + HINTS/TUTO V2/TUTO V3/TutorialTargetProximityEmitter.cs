using UnityEngine;

public class TutorialTargetProximityEmitter : MonoBehaviour
{
    [SerializeField] private TutorialEventChannelSO events;
    [SerializeField] private string targetId;
    [SerializeField] private bool oneShot = true;

    [Header("Detection")]
    [SerializeField] private float radius = 1.2f;
    [SerializeField] private LayerMask playerMask = ~0;
    [SerializeField] private bool use2DOnXZ = true;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = true;
    [SerializeField] private bool drawGizmo = true;

    private bool raised;
    private Player _cachedLocalPlayer;

    // global runtime registry to know if target is already reached
    private static readonly System.Collections.Generic.HashSet<string> reachedTargets = new();

    public static bool IsTargetReached(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        return reachedTargets.Contains(id);
    }

    public static void MarkTargetReached(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        reachedTargets.Add(id);
    }

    public static void ClearReachedTargets()
    {
        reachedTargets.Clear();
    }

    private void Update()
    {
        if (oneShot && raised) return;
        if (!TutorialRuntimeContext.IsTutorialRun) return;

        var player = FindLocalPlayer();
        if (player == null) return;

        Vector3 a = transform.position;
        Vector3 b = player.transform.position;

        float dist = use2DOnXZ
            ? Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z))
            : Vector3.Distance(a, b);

        if (dist <= radius)
        {
            raised = true;
            MarkTargetReached(targetId);

            if (debugLogs) Debug.Log($"[TutorialProximity] Raised target='{targetId}' dist={dist:0.00}");
            events?.Raise(TutorialEventType.ReachedTutorialTarget, targetId);
        }
    }

    private Player FindLocalPlayer()
    {
        if (_cachedLocalPlayer != null && _cachedLocalPlayer.IsOwner)
            return _cachedLocalPlayer;

        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p != null && p.IsOwner)
            {
                _cachedLocalPlayer = p;
                return p;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGizmo) return;
        Gizmos.color = raised ? new Color(0.2f, 1f, 0.2f, 0.35f) : new Color(1f, 0.9f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
#endif
}