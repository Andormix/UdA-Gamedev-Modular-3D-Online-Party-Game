using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    //VARS
    private Animator animator;
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private bool _lastWalkingState;
    private bool _hasWalkingState;

    private void Initialize()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        Initialize();
    }

    public void UpdateAnimations(bool isWalking)
    {
        if (animator == null) return;
        if (_hasWalkingState && _lastWalkingState == isWalking) return;

        _lastWalkingState = isWalking;
        _hasWalkingState = true;
        animator.SetBool(IsWalkingHash, isWalking);
    }
}
