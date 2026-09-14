using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Tutorial/Event Channel")]
public class TutorialEventChannelSO : ScriptableObject
{
    public event Action<TutorialEventType, object> OnEvent;
    public void Raise(TutorialEventType type, object payload = null) => OnEvent?.Invoke(type, payload);
}