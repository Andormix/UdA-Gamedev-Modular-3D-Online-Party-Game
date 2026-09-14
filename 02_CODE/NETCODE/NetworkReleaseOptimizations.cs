using Unity.Netcode;
using UnityEngine;

// Disables NGO dev metrics/logging in non-development builds.

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class NetworkReleaseOptimizations : MonoBehaviour
{
    private void Awake()
    {
#if !DEVELOPMENT_BUILD
        var nm = GetComponent<NetworkManager>();
        if (nm == null)
            return;

        nm.NetworkConfig.EnableNetworkLogs = false;
        nm.NetworkConfig.NetworkMessageMetrics = false;
        nm.NetworkConfig.NetworkProfilingMetrics = false;
#endif
    }
}
