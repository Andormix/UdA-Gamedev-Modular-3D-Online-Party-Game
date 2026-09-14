using UnityEngine;
using UnityEngine.Rendering.Universal;

public sealed class TargetOutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public sealed class OutlineSettings
    {
        [Header("Visuals")]
        public Color outlineColor = new Color(1f, 0.85f, 0.1f, 1f);
        [Range(0.5f, 6f)] public float outlineThickness = 1.75f;

        [Header("Filtering")]
        public LayerMask targetLayerMask = ~0;
    }

    [SerializeField] private OutlineSettings settings = new OutlineSettings();

    private static Color runtimeOutlineColor = new Color(1f, 0.85f, 0.1f, 1f);
    private static float runtimeOutlineThickness = 1.75f;
    private static LayerMask runtimeLayerMask = ~0;
    private static bool runtimeSettingsAvailable;

    public static bool TryGetRuntimeSettings(out Color color, out float thickness, out LayerMask layerMask)
    {
        color = runtimeOutlineColor;
        thickness = runtimeOutlineThickness;
        layerMask = runtimeLayerMask;
        return runtimeSettingsAvailable;
    }

    public override void Create()
    {
        ApplyRuntimeSettings();
    }

    private void OnValidate()
    {
        ApplyRuntimeSettings();
    }

    private void ApplyRuntimeSettings()
    {
        runtimeOutlineColor = settings.outlineColor;
        runtimeOutlineThickness = Mathf.Max(0.25f, settings.outlineThickness);
        runtimeLayerMask = settings.targetLayerMask;
        runtimeSettingsAvailable = true;
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData) { }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) { }

    protected override void Dispose(bool disposing)
    {
        // TODO 98: No dispose encara.
    }
}
