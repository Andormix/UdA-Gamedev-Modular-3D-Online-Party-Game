using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Netcode/Scene Object Catalog")]
public class SceneObjectCatalogSO : ScriptableObject
{
    public List<SceneObjectSO> all = new();

    // O(1) reverse-lookup cache; rebuilt lazily whenever the list changes at runtime.
    private Dictionary<SceneObjectSO, int> _indexCache;

    private void BuildCache()
    {
        _indexCache = new Dictionary<SceneObjectSO, int>(all.Count);
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null)
                _indexCache[all[i]] = i;
        }
    }

    // Returns the catalog index for <paramref name="so"/>, or -1 if not found.
    public int IndexOf(SceneObjectSO so)
    {
        if (so == null) return -1;
        if (_indexCache == null) BuildCache();
        return _indexCache.TryGetValue(so, out int idx) ? idx : -1;
    }

    public SceneObjectSO Get(int index)
    {
        if (index < 0 || index >= all.Count) return null;
        return all[index];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Invalidate cache whenever the asset is edited in the Inspector.
        _indexCache = null;
    }
#endif
}