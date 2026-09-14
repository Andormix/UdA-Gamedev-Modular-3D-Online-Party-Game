using UnityEngine;
using System.Collections;

public class MouseTutorialSwitcher : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject clickImage;
    [SerializeField] private GameObject noClickImage;

    [Header("Settings")]
    [SerializeField] private float interval = 0.5f;

    private void OnEnable()
    {
        // Start the loop whenever this parent object (TPV) is activated
        StartCoroutine(ToggleImages());
    }

    private IEnumerator ToggleImages()
    {
        while (true)
        {
            // Show Click, Hide No Click
            clickImage.SetActive(true);
            noClickImage.SetActive(false);
            yield return new WaitForSeconds(interval);

            // Hide Click, Show No Click
            clickImage.SetActive(false);
            noClickImage.SetActive(true);
            yield return new WaitForSeconds(interval);
        }
    }

    private void OnDisable()
    {
        // Clean up when the UI is hidden
        StopAllCoroutines();
    }
}