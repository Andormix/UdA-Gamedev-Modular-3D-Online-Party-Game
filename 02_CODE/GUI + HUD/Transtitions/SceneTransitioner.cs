using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public class SceneTransitioner : MonoBehaviour
{
    public static SceneTransitioner Instance;

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Image transitionImage; // Drag the 'TransitionOverlay' CHILD here
    [SerializeField] private float transitionTime = 0.5f;

    private void Awake()
    {
        Instance = this;
        
        // Ensure we start invisible and NOT blocking clicks
        if (transitionImage != null)
        {
            transitionImage.raycastTarget = false; 
            Color c = transitionImage.color;
            transitionImage.color = new Color(c.r, c.g, c.b, 0);
        }
    }

    public void PlayTransition(Action onMidPoint)
    {
        StopAllCoroutines(); // Prevent overlapping transitions
        StartCoroutine(TransitionRoutine(onMidPoint));
    }

    private IEnumerator TransitionRoutine(Action onMidPoint)
    {
        // 1. BLOCK CLICKS: Turn on raycast so player can't click while screen moves
        if (transitionImage != null) transitionImage.raycastTarget = true;

        // 2. START ANIMATION
        if (animator != null) animator.SetTrigger("Start");
        
        yield return new WaitForSeconds(transitionTime);

        // 3. THE SWAP: Screen is now fully covered
        onMidPoint?.Invoke();

        // 4. END ANIMATION
        if (animator != null) animator.SetTrigger("End");

        yield return new WaitForSeconds(transitionTime);

        // 5. UNBLOCK CLICS: Let the player use the menu again
        if (transitionImage != null) transitionImage.raycastTarget = false;
    }
}