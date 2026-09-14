using System.Collections.Generic;
using UnityEngine;

public sealed class TargetOutlineController : MonoBehaviour
{
    private PlayerInteractions playerInteractions;
    private readonly List<Renderer> collectedRenderers = new List<Renderer>(16);
    private readonly List<Renderer> drawRenderers = new List<Renderer>(16);
    private readonly List<Collider> targetColliders = new List<Collider>(16);
    private Material immediateOutlineMaterial;

    [Header("Outline Target Collection")]
    [SerializeField] private float fallbackMaxRendererDistance = 4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureBootstrap()
    {
        if (Object.FindFirstObjectByType<TargetOutlineController>() != null)
            return;

        var bootstrap = new GameObject(nameof(TargetOutlineController));
        Object.DontDestroyOnLoad(bootstrap);
        bootstrap.AddComponent<TargetOutlineController>();
    }

    private void OnEnable()
    {
        PlayerEvents.OnLocalPlayerSpawned += HandleLocalPlayerSpawned;

        if (immediateOutlineMaterial == null)
        {
            Shader shellShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shellShader == null)
                shellShader = Shader.Find("Unlit/Color");

            if (shellShader != null)
            {
                immediateOutlineMaterial = new Material(shellShader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                // Build-safe fallback shell settings:
                // render front faces culled and with transparency, so only outer shell is visible.
                SetIfExists(immediateOutlineMaterial, "_Cull", 1f);          // Front
                SetIfExists(immediateOutlineMaterial, "_ZWrite", 0f);
                SetIfExists(immediateOutlineMaterial, "_Surface", 1f);       // Transparent
                SetIfExists(immediateOutlineMaterial, "_Blend", 0f);         // Alpha
            }
        }
    }

    private void OnDisable()
    {
        PlayerEvents.OnLocalPlayerSpawned -= HandleLocalPlayerSpawned;
        UnsubscribeFromPlayerInteractions();
        TargetOutlineRuntime.ClearCurrentTarget();

        if (immediateOutlineMaterial != null)
        {
            Destroy(immediateOutlineMaterial);
            immediateOutlineMaterial = null;
        }
    }

    private void HandleLocalPlayerSpawned(PlayerInteractions interactions)
    {
        if (interactions == null)
            return;

        UnsubscribeFromPlayerInteractions();
        playerInteractions = interactions;
        playerInteractions.OnPromptTargetChanged += HandlePromptTargetChanged;
        SyncFromCurrentSelection();
    }

    private void HandlePromptTargetChanged(object sender, PlayerInteractions.OnPromptTargetChangedEventArgs e)
    {
        Transform targetRoot = null;
        if (e.selectedSceneObject != null)
            targetRoot = e.selectedSceneObject.transform;
        else if (e.selectedAsset != null)
            targetRoot = e.selectedAsset.transform;

        SyncTargetRenderersFromRoot(targetRoot);
    }

    private void UnsubscribeFromPlayerInteractions()
    {
        if (playerInteractions == null)
            return;

        playerInteractions.OnPromptTargetChanged -= HandlePromptTargetChanged;
        playerInteractions = null;
    }


    private void SyncFromCurrentSelection()
    {
        if (playerInteractions == null)
        {
            TargetOutlineRuntime.ClearCurrentTarget();
            return;
        }

        InteractableAsset selectedAsset = playerInteractions.GetSelected();
        if (selectedAsset == null)
        {
            TargetOutlineRuntime.ClearCurrentTarget();
            return;
        }

        SyncTargetRenderersFromRoot(selectedAsset.transform);
    }

    private void OnRenderObject()
    {
        if (GraphicsQualityBootstrap.IsLowQuality)
            return;

        if (immediateOutlineMaterial == null)
            return;

        LayerMask layerMask = ~0;
        Color outlineColor = new Color(1f, 0.85f, 0.1f, 1f);
        float outlineThickness = 1.75f;
        if (TargetOutlineRendererFeature.TryGetRuntimeSettings(out Color runtimeColor, out float runtimeThickness, out LayerMask runtimeMask))
        {
            outlineColor = runtimeColor;
            outlineThickness = runtimeThickness;
            layerMask = runtimeMask;
        }

        if (!TargetOutlineRuntime.TryCopyCurrentRenderers(drawRenderers, layerMask))
            return;

        immediateOutlineMaterial.SetColor("_OutlineColor", outlineColor);
        float shellScale = 1f + Mathf.Max(0.25f, outlineThickness) * 0.01f;
        SetOutlineColor(immediateOutlineMaterial, outlineColor);

        for (int i = 0; i < drawRenderers.Count; i++)
        {
            Renderer renderer = drawRenderers[i];
            if (renderer == null)
                continue;

            if (renderer is MeshRenderer meshRenderer)
            {
                if (!meshRenderer.TryGetComponent(out MeshFilter meshFilter) || meshFilter == null || meshFilter.sharedMesh == null)
                    continue;

                int subMeshCount = meshFilter.sharedMesh.subMeshCount;
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    immediateOutlineMaterial.SetPass(0);
                    Graphics.DrawMeshNow(meshFilter.sharedMesh, meshRenderer.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * shellScale), subMesh);
                }
                continue;
            }

            if (renderer is SkinnedMeshRenderer skinnedRenderer && skinnedRenderer.sharedMesh != null)
            {
                int subMeshCount = skinnedRenderer.sharedMesh.subMeshCount;
                for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                {
                    immediateOutlineMaterial.SetPass(0);
                    Graphics.DrawMeshNow(skinnedRenderer.sharedMesh, skinnedRenderer.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * shellScale), subMesh);
                }
            }
        }
    }

    private void SyncTargetRenderersFromRoot(Transform targetRoot)
    {
        if (targetRoot == null)
        {
            TargetOutlineRuntime.ClearCurrentTarget();
            return;
        }

        collectedRenderers.Clear();
        targetColliders.Clear();

        targetRoot.GetComponentsInChildren(includeInactive: false, result: targetColliders);
        for (int i = 0; i < targetColliders.Count; i++)
        {
            Collider collider = targetColliders[i];
            if (collider == null)
                continue;

            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null)
                renderer = collider.GetComponentInChildren<Renderer>();
            if (renderer == null)
                renderer = collider.GetComponentInParent<Renderer>();

            if (renderer != null && !collectedRenderers.Contains(renderer))
                collectedRenderers.Add(renderer);
        }

        if (collectedRenderers.Count == 0)
        {
            targetRoot.GetComponentsInChildren(includeInactive: false, result: collectedRenderers);
            if (collectedRenderers.Count > 0)
            {
                Vector3 rootPos = targetRoot.position;
                float maxDistSq = fallbackMaxRendererDistance * fallbackMaxRendererDistance;
                for (int i = collectedRenderers.Count - 1; i >= 0; i--)
                {
                    Renderer renderer = collectedRenderers[i];
                    if (renderer == null)
                    {
                        collectedRenderers.RemoveAt(i);
                        continue;
                    }

                    float distSq = (renderer.bounds.center - rootPos).sqrMagnitude;
                    if (distSq > maxDistSq)
                        collectedRenderers.RemoveAt(i);
                }
            }
        }

        if (collectedRenderers.Count == 0)
        {
            TargetOutlineRuntime.ClearCurrentTarget();
            return;
        }

        TargetOutlineRuntime.SetCurrentTargetRenderers(collectedRenderers);
    }

    private static void SetOutlineColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_OutlineColor"))
            material.SetColor("_OutlineColor", color);
    }

    private static void SetIfExists(Material material, string property, float value)
    {
        if (material != null && material.HasProperty(property))
            material.SetFloat(property, value);
    }
}
