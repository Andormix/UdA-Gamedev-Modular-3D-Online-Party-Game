using Unity.Netcode;
using UnityEngine;

public static class SceneObjectSpawn
{
    // Existing API stays

    public static SceneObject SpawnServer(SceneObjectSO so, InterfaceSceneObjectParent parent)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return null;
        if (so == null || so.prefab == null || parent == null) return null;

        SceneObject obj = SpawnServerUnparented(so);
        if (obj == null) return null;

        obj.SetSceneObjectParent(parent);
        return obj;
    }

    // NEW: spawn as a root object (world), no parent assignment
    public static SceneObject SpawnServerUnparented(SceneObjectSO so)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return null;
        if (so == null || so.prefab == null) return null;

        Transform t = Object.Instantiate(so.prefab);
        var netObj = t.GetComponent<NetworkObject>();
        var obj = t.GetComponent<SceneObject>();
        var net = t.GetComponent<SceneObjectNet>();

        if (netObj == null || obj == null || net == null)
        {
            Debug.LogError("SceneObject prefab must have NetworkObject + SceneObject + SceneObjectNet.");
            Object.Destroy(t.gameObject);
            return null;
        }

        netObj.Spawn(true);
        net.ServerInitialize(so);

        return obj;
    }
}