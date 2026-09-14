using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NpcGuestOutfitRandomizer))]
public class TableNpcAgent : NetworkBehaviour
{
    private static readonly Dictionary<Material, Material> MatteMaterialCache = new();
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SpecularHighlightsId = Shader.PropertyToID("_SpecularHighlights");
    private static readonly int EnvironmentReflectionsId = Shader.PropertyToID("_EnvironmentReflections");
    private static readonly int ClearCoatMaskId = Shader.PropertyToID("_ClearCoatMask");
    private static readonly int CoatMaskId = Shader.PropertyToID("_CoatMask");

    public event Action<TableNpcAgent> OnSeatedServer;
    public event Action<TableNpcAgent> OnWalkedAwayServer;

    [Header("Refs")]
    [SerializeField] private Animator animator;
    [SerializeField] private string walkBoolName = "isWalking";
    [SerializeField] private string sitTriggerName = "Sit";
    [SerializeField] private string standTriggerName = "Stand";
    [SerializeField] private string consumingBoolName = "isConsuming";

    [Header("Tuning")]
    [SerializeField] private float arriveDistance = 0.2f;
    [SerializeField] private float standUpDelay = 1f;
    [SerializeField] private float sitBlendDelay = 0.08f;     // wait after Sit trigger
    [SerializeField] private float sitBlendDuration = 0.35f;  // smooth move to final anchor
    [SerializeField] private TableNpcNetState netState;

    [Header("Visual Overrides")]
    [SerializeField] private bool forceMatteMaterials = true;

    private NavMeshAgent agent;
    private Transform targetSeat;       // nav target
    private Transform finalSeatTarget;  // final seated reference

    private bool movingToSeat;
    private bool seated;
    private bool seatEventSent;

    private bool movingAway;
    private bool walkAwayEventSent;

    private Coroutine walkAwayRoutine;
    private Coroutine settleSeatRoutine;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (netState == null) netState = GetComponent<TableNpcNetState>();
        if (forceMatteMaterials) ApplyMatteMaterialVariants();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer && agent != null)
            agent.enabled = false; // server-authoritative movement
    }

    private void Update()
    {
        if (!IsServer) return;

        if (movingToSeat && !seated && targetSeat != null && agent != null && agent.enabled)
        {
            if (!agent.pathPending &&
                agent.remainingDistance <= arriveDistance &&
                (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f))
            {
                ServerSitNow();
            }
        }

        if (movingAway && agent != null && agent.enabled)
        {
            if (!agent.pathPending &&
                agent.remainingDistance <= arriveDistance &&
                (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f))
            {
                movingAway = false;
                if (!walkAwayEventSent)
                {
                    walkAwayEventSent = true;
                    OnWalkedAwayServer?.Invoke(this);
                }
            }
        }

        if (animator != null)
        {
            bool walking = (movingToSeat && !seated) || movingAway;
            if (agent != null && agent.enabled)
                walking = walking && agent.velocity.sqrMagnitude > 0.01f;

            animator.SetBool(walkBoolName, walking);
        }
    }

    // Backward compatibility
    public void ServerGoToSeat(Transform seatAnchor)
    {
        ServerGoToSeat(seatAnchor, seatAnchor);
    }

    // New preferred path
    public void ServerGoToSeat(Transform seatAnchor, Transform seatFinalAnchor)
    {
        if (!IsServer) return;
        if (seatAnchor == null) return;
        if (agent == null || !agent.enabled) return;
        ServerSetConsuming(false);

        targetSeat = seatAnchor;
        finalSeatTarget = seatFinalAnchor != null ? seatFinalAnchor : seatAnchor;

        seated = false;
        seatEventSent = false;
        movingToSeat = true;
        movingAway = false;
        walkAwayEventSent = false;

        if (settleSeatRoutine != null)
            StopCoroutine(settleSeatRoutine);

        agent.isStopped = false;
        agent.updateRotation = true;
        agent.SetDestination(targetSeat.position);

        netState?.ServerSetState(1); // WalkingToSeat
    }

    private void ServerSitNow()
    {
        seated = true;
        movingToSeat = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.updateRotation = false;
        }

        netState?.ServerSetState(2); // Seated

        if (animator != null)
            animator.SetTrigger(sitTriggerName);

        if (settleSeatRoutine != null)
            StopCoroutine(settleSeatRoutine);

        settleSeatRoutine = StartCoroutine(SettleToSeatRoutine());
    }

    private IEnumerator SettleToSeatRoutine()
    {
        if (sitBlendDelay > 0f)
            yield return new WaitForSeconds(sitBlendDelay);

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 endPos = finalSeatTarget != null ? finalSeatTarget.position : startPos;
        Quaternion endRot = finalSeatTarget != null ? finalSeatTarget.rotation : startRot;

        float t = 0f;
        float dur = Mathf.Max(0.01f, sitBlendDuration);

        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            // smoothstep easing
            k = k * k * (3f - 2f * k);

            transform.position = Vector3.Lerp(startPos, endPos, k);
            transform.rotation = Quaternion.Slerp(startRot, endRot, k);
            yield return null;
        }

        transform.SetPositionAndRotation(endPos, endRot);

        netState?.ServerSnap(transform);

        if (!seatEventSent)
        {
            seatEventSent = true;
            OnSeatedServer?.Invoke(this);
        }
    }

    public void ServerStandAndWalkAway(Vector3 worldDestination)
    {
        if (!IsServer) return;
        if (agent == null || !agent.enabled) return;
        ServerSetConsuming(false);

        if (walkAwayRoutine != null)
            StopCoroutine(walkAwayRoutine);

        if (settleSeatRoutine != null)
            StopCoroutine(settleSeatRoutine);

        netState?.ServerSetState(3); // WalkingAway
        walkAwayRoutine = StartCoroutine(StandAndWalkAwayRoutine(worldDestination));
    }

    private IEnumerator StandAndWalkAwayRoutine(Vector3 destination)
    {
        movingToSeat = false;
        movingAway = false;
        seated = false;
        walkAwayEventSent = false;

        if (animator != null)
            animator.SetTrigger(standTriggerName);

        yield return new WaitForSeconds(standUpDelay);

        if (agent != null && agent.enabled)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
            agent.SetDestination(destination);
            movingAway = true;
        }
    }

    public void ServerSetConsuming(bool value)
    {
        if (!IsServer) return;
        SetConsumingClientRpc(value);
    }

    [ClientRpc]
    private void SetConsumingClientRpc(bool value)
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) return;
        if (string.IsNullOrWhiteSpace(consumingBoolName)) return;

        animator.SetBool(consumingBoolName, value);
    }

    private void ApplyMatteMaterialVariants()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Renderer renderer = renderers[r];
            if (renderer == null) continue;

            Material[] sharedMaterials = renderer.sharedMaterials;
            bool changed = false;

            for (int i = 0; i < sharedMaterials.Length; i++)
            {
                Material source = sharedMaterials[i];
                if (source == null) continue;

                Material matte = GetOrCreateMatteVariant(source);
                if (matte == null || matte == source) continue;

                sharedMaterials[i] = matte;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = sharedMaterials;
        }
    }

    private static Material GetOrCreateMatteVariant(Material source)
    {
        if (source == null) return null;
        if (MatteMaterialCache.TryGetValue(source, out Material cached) && cached != null)
            return cached;

        Material matte = new Material(source)
        {
            name = source.name + "_NPC_Matte"
        };

        if (matte.HasProperty(SmoothnessId)) matte.SetFloat(SmoothnessId, 0f);
        if (matte.HasProperty(GlossinessId)) matte.SetFloat(GlossinessId, 0f);
        if (matte.HasProperty(MetallicId)) matte.SetFloat(MetallicId, 0f);
        if (matte.HasProperty(SpecularHighlightsId)) matte.SetFloat(SpecularHighlightsId, 0f);
        if (matte.HasProperty(EnvironmentReflectionsId)) matte.SetFloat(EnvironmentReflectionsId, 0f);
        if (matte.HasProperty(ClearCoatMaskId)) matte.SetFloat(ClearCoatMaskId, 0f);
        if (matte.HasProperty(CoatMaskId)) matte.SetFloat(CoatMaskId, 0f);

        MatteMaterialCache[source] = matte;
        return matte;
    }
}