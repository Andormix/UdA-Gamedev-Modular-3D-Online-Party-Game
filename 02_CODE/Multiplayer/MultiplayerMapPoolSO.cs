using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Multiplayer/Map Pool")]
public class MultiplayerMapPoolSO : ScriptableObject
{
    [Tooltip("Maps used only for standard multiplayer quick match.")]
    public List<Loader.Scene> multiplayerMaps = new();

    public Loader.Scene GetRandomMap()
    {
        if (multiplayerMaps == null || multiplayerMaps.Count == 0)
            return Loader.Scene.Map_03_M;

        int idx = Random.Range(0, multiplayerMaps.Count);
        return multiplayerMaps[idx];
    }
}