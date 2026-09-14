using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class TutorialNpcDirector : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform npcRoot;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private TutorialEventChannelSO events;

    [Header("Animator Params")]
    [SerializeField] private string isWalkingParam = "isWalking";
    [SerializeField] private string isJoggingParam = "isJogging";
    [SerializeField] private string isRunningParam = "isRunning";
    [SerializeField] private string isSittingParam = "isSitting";
    [SerializeField] private string jumpTriggerParam = "Jump";

    [Header("Events")]
    [SerializeField] private bool raiseEvents = true;
    [SerializeField] private string npcPayloadId = "CatNPC";

    private Coroutine running;

    private void Awake()
    {
        if (npcRoot == null) npcRoot = transform;
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    public void Execute(TutorialNpcActionData action)
    {
        if (action == null || action.actionType == TutorialNpcActionType.None) return;

        if (running != null) StopCoroutine(running);
        running = StartCoroutine(ExecuteRoutine(action));
    }

    public void CancelCurrentAction()
    {
        if (running != null) StopCoroutine(running);
        running = null;

        if (agent != null) agent.isStopped = true;
        ClearMoveBools();
    }

    private IEnumerator ExecuteRoutine(TutorialNpcActionData a)
    {
        switch (a.actionType)
        {
            case TutorialNpcActionType.MoveToTransform:
            {
                Transform target = a.targetTransform;
                if (target == null && !string.IsNullOrWhiteSpace(a.targetId))
                    target = TutorialSceneTargetRegistry.Resolve(a.targetId);

                if (target == null)
                {
                    Debug.LogWarning($"[TutorialNpcDirector] MoveToTransform target missing. targetId='{a.targetId}'");
                    yield break;
                }

                Vector3 p = target.position;
                if (a.snapYToNpc) p.y = npcRoot.position.y;

                ApplyMoveStyle(a);
                yield return MoveTo(p, a.moveStoppingDistance, a.moveTimeoutSeconds);
                RaiseNpcEvent("MoveToTransformDone");
                break;
            }

            case TutorialNpcActionType.MoveToPoint:
            {
                Vector3 p = a.worldPoint;
                if (a.snapYToNpc) p.y = npcRoot.position.y;

                ApplyMoveStyle(a);
                yield return MoveTo(p, a.moveStoppingDistance, a.moveTimeoutSeconds);
                RaiseNpcEvent("MoveToPointDone");
                break;
            }

            case TutorialNpcActionType.FollowPlayer:
            {
                ApplyMoveStyle(a);
                yield return FollowPlayer(a);
                RaiseNpcEvent("FollowDone");
                break;
            }

            case TutorialNpcActionType.Stop:
            {
                if (agent != null) agent.isStopped = true;
                ClearMoveBools();
                RaiseNpcEvent("StopDone");
                break;
            }

            case TutorialNpcActionType.Sit:
            {
                if (animator != null && !string.IsNullOrWhiteSpace(isSittingParam))
                    animator.SetBool(isSittingParam, true);

                ClearMoveBools();
                RaiseNpcEvent("SitDone");
                break;
            }

            case TutorialNpcActionType.Jump:
            {
                if (animator != null)
                {
                    if (!string.IsNullOrWhiteSpace(jumpTriggerParam))
                        animator.SetTrigger(jumpTriggerParam);
                    else if (!string.IsNullOrWhiteSpace(a.animatorParamName))
                        animator.SetTrigger(a.animatorParamName);
                }

                RaiseNpcEvent("JumpDone");
                break;
            }

            case TutorialNpcActionType.FaceTransform:
            {
                Transform target = a.targetTransform;
                if (target == null && !string.IsNullOrWhiteSpace(a.targetId))
                    target = TutorialSceneTargetRegistry.Resolve(a.targetId);

                if (target != null)
                    yield return FaceToward(target.position, a.turnSpeedDegPerSec);
                else
                    Debug.LogWarning($"[TutorialNpcDirector] FaceTransform target missing. targetId='{a.targetId}'");

                RaiseNpcEvent("FaceTransformDone");
                break;
            }

            case TutorialNpcActionType.FacePlayer:
            {
                var p = FindLocalPlayer();
                if (p != null) yield return FaceToward(p.position, a.turnSpeedDegPerSec);

                RaiseNpcEvent("FacePlayerDone");
                break;
            }

            case TutorialNpcActionType.PlayAnimatorTrigger:
            {
                if (animator != null && !string.IsNullOrWhiteSpace(a.animatorParamName))
                    animator.SetTrigger(a.animatorParamName);

                RaiseNpcEvent("PlayTriggerDone");
                break;
            }

            case TutorialNpcActionType.PlayAnimatorBool:
            {
                if (animator != null && !string.IsNullOrWhiteSpace(a.animatorParamName))
                    animator.SetBool(a.animatorParamName, a.animatorBoolValue);

                RaiseNpcEvent("PlayBoolDone");
                break;
            }
        }

        running = null;
    }

    private IEnumerator MoveTo(Vector3 pos, float stopDist, float timeout)
    {
        if (agent == null)
        {
            npcRoot.position = pos;
            yield break;
        }

        agent.isStopped = false;
        agent.stoppingDistance = Mathf.Max(0.05f, stopDist);
        agent.SetDestination(pos);

        float end = Time.time + Mathf.Max(0.1f, timeout);

        while (Time.time < end)
        {
            if (!agent.pathPending)
            {
                if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
                    break;
            }

            yield return null;
        }

        agent.isStopped = true;
        ClearMoveBools();
    }

    private IEnumerator FollowPlayer(TutorialNpcActionData a)
    {
        if (agent == null) yield break;

        float stopDistance = Mathf.Max(0.1f, a.followStopDistance);
        float maxDuration = Mathf.Max(0.1f, a.followMaxDuration);
        float repathInterval = Mathf.Max(0.05f, a.followRepathInterval);
        float standUpDistance = Mathf.Max(stopDistance + 0.1f, a.standUpDistance);

        float end = Time.time + maxDuration;
        float nextRepath = 0f;
        bool isNearSitting = false;

        agent.isStopped = false;
        agent.stoppingDistance = stopDistance;

        while (true)
        {
            // non-continuous mode respects timeout
            if (!a.followContinuously && Time.time > end)
                break;

            var p = FindLocalPlayer();
            if (p == null)
            {
                yield return null;
                continue;
            }

            float d = Vector3.Distance(npcRoot.position, p.position);

            // Near behavior
            if (d <= stopDistance)
            {
                if (a.sitWhenNearPlayer)
                {
                    if (!isNearSitting)
                    {
                        agent.isStopped = true;
                        ClearMoveBools();

                        if (animator != null && !string.IsNullOrWhiteSpace(isSittingParam))
                            animator.SetBool(isSittingParam, true);

                        isNearSitting = true;
                    }
                }
                else
                {
                    agent.isStopped = true;
                    ClearMoveBools();
                }
            }
            else
            {
                // Far again => stand up and resume follow
                bool shouldResume = !isNearSitting || d >= standUpDistance;

                if (shouldResume)
                {
                    if (animator != null && !string.IsNullOrWhiteSpace(isSittingParam))
                        animator.SetBool(isSittingParam, false);

                    isNearSitting = false;
                    agent.isStopped = false;

                    // keep move style active while moving
                    ApplyMoveStyle(a);

                    if (Time.time >= nextRepath)
                    {
                        agent.SetDestination(p.position);
                        nextRepath = Time.time + repathInterval;
                    }
                }
            }

            yield return null;
        }

        agent.isStopped = true;
        ClearMoveBools();
    }

    private IEnumerator FaceToward(Vector3 worldPos, float speedDegPerSec)
    {
        Vector3 dir = worldPos - npcRoot.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;

        Quaternion target = Quaternion.LookRotation(dir.normalized, Vector3.up);
        float speed = Mathf.Max(1f, speedDegPerSec);

        while (Quaternion.Angle(npcRoot.rotation, target) > 1f)
        {
            npcRoot.rotation = Quaternion.RotateTowards(npcRoot.rotation, target, speed * Time.deltaTime);
            yield return null;
        }

        npcRoot.rotation = target;
    }

    private void ApplyMoveStyle(TutorialNpcActionData a)
    {
        if (agent != null)
        {
            agent.acceleration = Mathf.Max(0.1f, a.acceleration);
            agent.angularSpeed = Mathf.Max(1f, a.angularSpeed);

            switch (a.moveStyle)
            {
                case TutorialNpcMoveStyle.Run:
                    agent.speed = Mathf.Max(0.1f, a.runSpeed);
                    break;
                case TutorialNpcMoveStyle.Jog:
                    agent.speed = Mathf.Max(0.1f, a.jogSpeed);
                    break;
                default:
                    agent.speed = Mathf.Max(0.1f, a.walkSpeed);
                    break;
            }
        }

        if (animator == null) return;

        if (!string.IsNullOrWhiteSpace(isWalkingParam))
            animator.SetBool(isWalkingParam, a.moveStyle == TutorialNpcMoveStyle.Walk);

        if (!string.IsNullOrWhiteSpace(isJoggingParam))
            animator.SetBool(isJoggingParam, a.moveStyle == TutorialNpcMoveStyle.Jog);

        if (!string.IsNullOrWhiteSpace(isRunningParam))
            animator.SetBool(isRunningParam, a.moveStyle == TutorialNpcMoveStyle.Run);

        if (!string.IsNullOrWhiteSpace(isSittingParam))
            animator.SetBool(isSittingParam, false);
    }

    private void ClearMoveBools()
    {
        if (animator == null) return;

        if (!string.IsNullOrWhiteSpace(isWalkingParam))
            animator.SetBool(isWalkingParam, false);

        if (!string.IsNullOrWhiteSpace(isJoggingParam))
            animator.SetBool(isJoggingParam, false);

        if (!string.IsNullOrWhiteSpace(isRunningParam))
            animator.SetBool(isRunningParam, false);
    }

    private Transform FindLocalPlayer()
    {
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p != null && p.IsOwner) return p.transform;
        }

        return null;
    }

    private void RaiseNpcEvent(string suffix)
    {
        if (!raiseEvents || events == null) return;
        events.Raise(TutorialEventType.WorkStarted, $"{npcPayloadId}:{suffix}");
    }
}