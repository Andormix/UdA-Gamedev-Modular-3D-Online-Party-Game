using UnityEngine;

public class CatAnimationStarter : MonoBehaviour
{
    [Header("Boolean States")]
    public bool isWalking;
    public bool isJogging;
    public bool isRunning;
    public bool isAttacking;
    public bool isSitting;
    public bool isEating;
    public bool isCleaning;
    public bool isItching;
    public bool isLickingButt;
    public bool isSleeping;

    [Header("Triggers")]
    public bool triggerJump;
    public bool triggerScare;
    public bool triggerHit;
    public bool triggerDie;

    [SerializeField] private Animator anim;

    void Start()
    {
        anim = GetComponent<Animator>();

        // Apply Boolean Parameters
        anim.SetBool("isWalking", isWalking);
        anim.SetBool("isJogging", isJogging);
        anim.SetBool("isRunning", isRunning);
        anim.SetBool("isAttacking", isAttacking);
        anim.SetBool("isSitting", isSitting);
        anim.SetBool("isEating", isEating);
        anim.SetBool("isCleaning", isCleaning);
        anim.SetBool("isItching", isItching);
        anim.SetBool("isLickingButt", isLickingButt);
        anim.SetBool("isSleeping", isSleeping);

        // Apply Triggers (If checked in inspector, fire once on start)
        if (triggerJump) anim.SetTrigger("Jump");
        if (triggerScare) anim.SetTrigger("Scare");
        if (triggerHit) anim.SetTrigger("hit");
        if (triggerDie) anim.SetTrigger("die");
    }
}