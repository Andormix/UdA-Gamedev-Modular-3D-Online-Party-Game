using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies Low / PC quality at startup and exposes helpers for runtime switching.
/// Quality index 0 = Low, 1 = PC (see QualitySettings).
/// </summary>
public static class GraphicsQualityBootstrap
{
    public const string PlayerPrefsKey = "graphics_quality";
    public const int QualityLow = 0;
    public const int QualityPc = 1;

    public static int CurrentQualityIndex { get; private set; } = QualityLow;

    public static bool IsLowQuality => CurrentQualityIndex == QualityLow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyOnStartup()
    {
        int quality = ResolveInitialQuality();
        ApplyQuality(quality, savePreference: false);
    }

    public static int ResolveInitialQuality()
    {
        if (PlayerPrefs.HasKey(PlayerPrefsKey))
            return Mathf.Clamp(PlayerPrefs.GetInt(PlayerPrefsKey, QualityLow), QualityLow, QualityPc);

        return DetectWeakGpu() ? QualityLow : QualityPc;
    }

    public static bool DetectWeakGpu()
    {
        string gpu = SystemInfo.graphicsDeviceName ?? string.Empty;
        if (gpu.Length == 0)
            return true;

        string lower = gpu.ToLowerInvariant();
        if (lower.Contains("intel") || lower.Contains("uhd") || lower.Contains("iris") || lower.Contains("hd graphics"))
            return true;

        if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize < 2048)
            return true;

        return false;
    }

    public static void ApplyQuality(int qualityIndex, bool savePreference = true)
    {
        qualityIndex = Mathf.Clamp(qualityIndex, QualityLow, QualityPc);
        CurrentQualityIndex = qualityIndex;

        QualitySettings.SetQualityLevel(qualityIndex, applyExpensiveChanges: true);
        Application.targetFrameRate = PerformanceValidationSettings.TargetFrameRate;

        if (qualityIndex == QualityLow)
            SetLowRenderScale(PerformanceValidationSettings.RecommendedLowRenderScale);

        if (savePreference)
            PlayerPrefs.SetInt(PlayerPrefsKey, qualityIndex);

        ApplyNetworkTickForQuality(qualityIndex);
    }

    public static void RefreshNetworkTickFromCurrentQuality()
    {
        ApplyNetworkTickForQuality(CurrentQualityIndex);
    }

    private static void ApplyNetworkTickForQuality(int qualityIndex)
    {
        if (!Unity.Netcode.NetworkManager.Singleton)
            return;

        var config = Unity.Netcode.NetworkManager.Singleton.NetworkConfig;
        if (config == null)
            return;

        config.TickRate = qualityIndex == QualityLow ? (uint)20 : 30;
    }
    // Adjust internal render scale on the active URP asset (Només per low tier).
    public static void SetLowRenderScale(float scale)
    {
        scale = Mathf.Clamp(scale, 0.5f, 1f);
        var asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        if (asset != null && IsLowQuality)
            asset.renderScale = scale;
    }
}
