using System.Collections.Generic;
using UnityEngine;

public static class TargetOutlineRuntime
{
    private static readonly List<Renderer> CurrentRenderers = new List<Renderer>(16);

    public static void SetCurrentTargetRenderers(List<Renderer> renderers)
    {
        CurrentRenderers.Clear();
        if (renderers == null || renderers.Count == 0)
            return;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (!renderer.gameObject.activeInHierarchy || !renderer.enabled)
                continue;

            CurrentRenderers.Add(renderer);
        }
    }

    public static void ClearCurrentTarget()
    {
        CurrentRenderers.Clear();
    }

    public static bool TryCopyCurrentRenderers(List<Renderer> output, LayerMask layerMask)
    {
        output.Clear();
        if (CurrentRenderers.Count == 0)
            return false;

        int maskBits = layerMask.value;
        for (int i = 0; i < CurrentRenderers.Count; i++)
        {
            Renderer renderer = CurrentRenderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy || !renderer.enabled)
                continue;

            if ((maskBits & (1 << renderer.gameObject.layer)) == 0)
                continue;

            output.Add(renderer);
        }

        return output.Count > 0;
    }
}
