using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class InteractableSoundEmitter : MonoBehaviour
{
    [SerializeField] private InteractableSoundConfigSO soundConfig;

    private AudioSource audioSource;
    private bool workLoopPlaying;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;

        ApplyConfigToSource();
    }

    // -----------------------------------------------------------------
    // Public API – called by PlayerInteractionSoundHandler
    // -----------------------------------------------------------------


    public void PlayHover() => PlayOneShot(soundConfig?.onHoverClip, soundConfig?.hoverVolume ?? 0.5f);
    public void PlayInteract() => PlayOneShot(soundConfig?.onInteractClip, soundConfig?.interactVolume ?? 0.8f);
    public void PlayWorkStart() => StartWorkLoop();
    public void PlayWorkStop() => PlayOneShot(soundConfig?.onWorkStopClip, soundConfig?.workVolume ?? 0.7f);


    // Starts a looping workstation clip while work is active.
    public void StartWorkLoop()
    {
        if (audioSource == null) return;
        if (workLoopPlaying) return;

        AudioClip loopClip = soundConfig != null ? soundConfig.onWorkStartClip : null;
        if (loopClip == null) return;

        audioSource.clip = loopClip;
        audioSource.volume = soundConfig != null ? soundConfig.workVolume : 0.7f;
        audioSource.loop = true;
        audioSource.Play();
        workLoopPlaying = true;
    }

    // Stops workstation loop playback.
    public void StopWorkLoop(bool playStopOneShot = true)
    {
        if (audioSource == null) return;
        if (!workLoopPlaying) return;

        audioSource.Stop();
        audioSource.loop = false;
        audioSource.clip = null;
        workLoopPlaying = false;

        if (playStopOneShot)
            PlayWorkStop();
    }

    // -----------------------------------------------------------------
    // Internal helperrs
    // -----------------------------------------------------------------

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }


    // Reads the SO and pushes 3D / mixer settings onto the AudioSource.
    private void ApplyConfigToSource()
    {
        if (soundConfig == null) return;

        audioSource.spatialBlend = soundConfig.spatialBlend;
        audioSource.minDistance = soundConfig.minDistance;
        audioSource.maxDistance = soundConfig.maxDistance;
        audioSource.rolloffMode = AudioRolloffMode.Linear;

        if (soundConfig.outputMixerGroup != null)
            audioSource.outputAudioMixerGroup = soundConfig.outputMixerGroup;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        // Auto-configure ...
        audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }
#endif
}
