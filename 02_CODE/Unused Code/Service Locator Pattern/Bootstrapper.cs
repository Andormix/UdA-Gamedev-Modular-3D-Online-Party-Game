using UnityEngine;


[DefaultExecutionOrder(-1)]
public class Bootstrapper : MonoBehaviour
{
    private void Awake()
    {
        ServiceLocator.Register<IAudioSystem>(new AudioSystem());
    }
}
