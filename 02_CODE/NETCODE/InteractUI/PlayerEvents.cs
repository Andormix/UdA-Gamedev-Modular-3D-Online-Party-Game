using System;
using UnityEngine;

public static class PlayerEvents 
{
    public static Action<PlayerInteractions> OnLocalPlayerSpawned;
    public static Camera LocalGameplayCamera { get; set; }
}