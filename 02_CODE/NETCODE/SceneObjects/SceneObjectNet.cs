using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(SceneObject))]
public class SceneObjectNet : NetworkBehaviour
{
    [SerializeField] private SceneObjectCatalogSO catalog;

    // Index into catalog (replicated)
    private readonly NetworkVariable<int> sceneObjectIndex = new(-1);

    private SceneObject sceneObject;

    private void Awake()
    {
        sceneObject = GetComponent<SceneObject>();
    }

    public override void OnNetworkSpawn()
    {
        ApplyFromNet();
        sceneObjectIndex.OnValueChanged += OnIndexChanged;
    }

    public override void OnNetworkDespawn()
    {
        sceneObjectIndex.OnValueChanged -= OnIndexChanged;
    }

    private void OnIndexChanged(int previous, int next) => ApplyFromNet();

    private void ApplyFromNet()
    {
        if (sceneObject == null || catalog == null) return;
        if (sceneObjectIndex.Value < 0) return;

        SceneObjectSO so = catalog.Get(sceneObjectIndex.Value);
        if (so != null)
            sceneObject.SetSOFromNet(so);
    }

    // Server-only init right after spawn
    public void ServerInitialize(SceneObjectSO so)
    {
        if (!IsServer || catalog == null) return;

        int idx = catalog.IndexOf(so);
        sceneObjectIndex.Value = idx;
        // Apply immediately on server too:
        if (sceneObject != null && so != null)
            sceneObject.SetSOFromNet(so);
    }

    public SceneObjectSO GetSO()
    {
        if (catalog == null) return null;
        return catalog.Get(sceneObjectIndex.Value);
    }
}