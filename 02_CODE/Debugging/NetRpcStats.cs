using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public static class NetRpcStats
{
    public static bool Enabled = true; // TOGGLE PARA RUNTIME

    private class Counter { public int Count; public float T0; }
    private static readonly Dictionary<string, Counter> C = new();
    private const float Window = 1f;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Hit(string name)
    {
        if (!Enabled) return;

        string role = "LOCAL";
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            if (nm.IsHost) role = "HOST";
            else if (nm.IsServer) role = "SERVER";
            else if (nm.IsClient) role = "CLIENT";
        }

        string key = $"{role}:{name}";
        if (!C.TryGetValue(key, out var v))
        {
            v = new Counter { T0 = Time.time };
            C[key] = v;
        }

        v.Count++;
        if (Time.time - v.T0 >= Window)
        {
            Debug.Log($"[RPC/s] {key} = {v.Count}");
            v.Count = 0;
            v.T0 = Time.time;
        }
    }
}