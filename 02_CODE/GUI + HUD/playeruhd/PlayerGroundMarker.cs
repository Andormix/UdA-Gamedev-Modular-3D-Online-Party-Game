using Unity.Netcode;
using UnityEngine;

public class PlayerGroundMarker : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform ringRoot;
    [SerializeField] private Renderer ringRenderer;

    [Header("Ring Colors")]
    [SerializeField] private Color blueRing = new Color32(79, 195, 247, 255); // Blue team
    [SerializeField] private Color redRing = new Color32(239, 83, 80, 255);   // Red team

    [Header("Animation")]
    [SerializeField] private float pulseSpeed = 2.2f;
    [SerializeField] private float pulseAmount = 0.04f;
    [SerializeField] private float localScaleBoost = 1.10f;

    [Header("Refresh (Multiplayer only)")]
    [SerializeField] private float mpColorRefreshInterval = 0.20f; // no need per-frame team polling

    private static readonly int RingColorId = Shader.PropertyToID("_RingColor");

    private Vector3 _baseScale = Vector3.one;
    private MaterialPropertyBlock _mpb;
    private Color _lastAppliedColor = new Color(-1f, -1f, -1f, -1f);
    private float _nextMpRefreshTime;

    private enum MarkerMode
    {
        Single,
        Coop,
        Multiplayer
    }

    private MarkerMode _mode;
    private bool _modeInitialized;

    private void Awake()
    {
        if (ringRoot == null) ringRoot = transform;
        if (ringRenderer == null) ringRenderer = GetComponentInChildren<Renderer>(true);
    }

    public override void OnNetworkSpawn()
    {
        _baseScale = ringRoot != null ? ringRoot.localScale : Vector3.one;
        _mpb = new MaterialPropertyBlock();

        ResolveModeOnce();
        ForceApplyResolvedColor();
        _nextMpRefreshTime = Time.unscaledTime + mpColorRefreshInterval;
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;

        if (!_modeInitialized) ResolveModeOnce();

        // Only multiplayer needs periodic re-check (team/join list can settle after spawn - updated memo with related changes)
        if (_mode == MarkerMode.Multiplayer && Time.unscaledTime >= _nextMpRefreshTime)
        {
            _nextMpRefreshTime = Time.unscaledTime + mpColorRefreshInterval;
            ApplyResolvedColorIfChanged();
        }

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float boost = IsLocalPlayerObject() ? localScaleBoost : 1f;

        if (ringRoot != null)
            ringRoot.localScale = _baseScale * boost * pulse;
    }

    private void ResolveModeOnce()
    {
        // single = not multiplayer + not coop session
        if (!GameMultiplayerManager.playMultiplayer && !CoopCampaignSessionContext.IsCoopCampaignRun)
            _mode = MarkerMode.Single;
        else if (CoopCampaignSessionContext.IsCoopCampaignRun)
            _mode = MarkerMode.Coop;
        else
            _mode = MarkerMode.Multiplayer;

        _modeInitialized = true;
    }

    private void ForceApplyResolvedColor()
    {
        ApplyColor(ResolveRingColorByMode());
    }

    private void ApplyResolvedColorIfChanged()
    {
        Color c = ResolveRingColorByMode();
        if (!ApproximatelyEqualColor(c, _lastAppliedColor))
            ApplyColor(c);
    }

    private Color ResolveRingColorByMode()
    {
        switch (_mode)
        {
            case MarkerMode.Single:
                return blueRing; // single always blue

            case MarkerMode.Coop:
                return blueRing; // coop all blue

            case MarkerMode.Multiplayer:
            default:
                if (TryGetJoinIndexForOwner(out int joinIndex))
                {
                    MatchTeam team = MatchTeamRules.TeamFromJoinIndex(joinIndex);
                    return team == MatchTeam.Red ? redRing : blueRing;
                }

                // safe fallback
                return blueRing;
        }
    }

    private void ApplyColor(Color c)
    {
        if (ringRenderer == null) return;

        ringRenderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(RingColorId, c);
        ringRenderer.SetPropertyBlock(_mpb);

        _lastAppliedColor = c;
    }

    private bool TryGetJoinIndexForOwner(out int joinIndex)
    {
        joinIndex = -1;

        if (NetworkManager.Singleton == null) return false;
        var ids = NetworkManager.Singleton.ConnectedClientsIds;

        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == OwnerClientId)
            {
                joinIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool IsLocalPlayerObject()
    {
        if (NetworkManager.Singleton == null) return false;
        return OwnerClientId == NetworkManager.Singleton.LocalClientId;
    }

    private static bool ApproximatelyEqualColor(Color a, Color b)
    {
        const float eps = 0.001f;
        return Mathf.Abs(a.r - b.r) < eps &&
               Mathf.Abs(a.g - b.g) < eps &&
               Mathf.Abs(a.b - b.b) < eps &&
               Mathf.Abs(a.a - b.a) < eps;
    }
}