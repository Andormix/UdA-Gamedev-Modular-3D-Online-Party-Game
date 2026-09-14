using UnityEngine;

public class SinkWashFxController : MonoBehaviour
{
    [SerializeField] private ParticleSystem washLoop;
    [SerializeField] private AudioSource washAudio; // optional

    public void SetWashingVisual(bool on)
    {
        if (washLoop != null)
        {
            if (on)
            {
                if (!washLoop.isPlaying) washLoop.Play(true);
            }
            else
            {
                if (washLoop.isPlaying) washLoop.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (washAudio != null)
        {
            if (on)
            {
                if (!washAudio.isPlaying) washAudio.Play();
            }
            else
            {
                washAudio.Stop();
            }
        }
    }
}