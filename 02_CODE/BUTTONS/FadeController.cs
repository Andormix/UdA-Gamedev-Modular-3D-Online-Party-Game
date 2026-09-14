using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class FadeController : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Image targetImage;
    [SerializeField] private float fadeDuration = 2.0f;
    [SerializeField] private bool activateOnStart = false;

    private void Start()
    {
        if (activateOnStart)
        {
            StartFadeOut();
        }
    }

    // Remember E. Called method from scene manager 
    public void StartFadeOut()
    {
        if (targetImage != null)
        {
            StartCoroutine(FadeRoutine());
        }
        else
        {
            Debug.LogWarning("FadeController: No target image assigned!");
        }
    }

    private IEnumerator FadeRoutine()
    {
        float elapsedTime = 0f;
        Color color = targetImage.color;

        // Ensure we start from the current alpha
        float startAlpha = color.a;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Calculate new alpha 
            color.a = Mathf.Lerp(startAlpha, 1f, elapsedTime / fadeDuration);
            targetImage.color = color;
            
            yield return null; // Wait for the next frame
        }

        // Ensure it's fully opaque at the end
        color.a = 1f;
        targetImage.color = color;
    }
}