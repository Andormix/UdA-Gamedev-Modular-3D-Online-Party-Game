using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingProgressBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI loadingText;
    
    [Header("Speed Settings")]
    [Tooltip("How much the bar fills per second (0.5 = 2 seconds to fill 100%)")]
    [SerializeField] private float fillSpeed = 0.8f; 
    
    [Tooltip("Keep this low (e.g., 0.2) so the bar doesn't wait for nothing")]
    [SerializeField] private float minLoadTime = 0.2f;

    private AsyncOperation loadingOperation;
    private float timer;

    private void Start()
    {
        timer = 0f;
        if (progressBar != null) progressBar.value = 0;
        
        loadingOperation = Loader.GetLoadingAsyncOperation();
        
        if (loadingOperation != null)
            loadingOperation.allowSceneActivation = false;
    }

    private void Update()
    {
        if (loadingOperation == null) return;

        timer += Time.deltaTime;

        // 1. Get the actual Unity loading progress (0 to 1)
        float realProgress = Mathf.Clamp01(loadingOperation.progress / 0.9f);

        // 2. Move the bar at a CONSTANT speed towards the real progress
        // This is much faster and more predictable than Lerp i think
        progressBar.value = Mathf.MoveTowards(progressBar.value, realProgress, Time.deltaTime * fillSpeed);

        // 3. Update Text
        if (loadingText != null)
        {
            int percentage = Mathf.RoundToInt(progressBar.value * 100f);
            loadingText.text = $"Loading... {percentage}%";
        }

        // 4. THE TRIGGER
        // We wait until the bar is EXACTLY 1.0 and the timer is done
        if (progressBar.value >= 1f && loadingOperation.progress >= 0.9f && timer >= minLoadTime)
        {
            loadingOperation.allowSceneActivation = true;
        }
    }
}