using UnityEngine;
using UnityEngine.Audio;

public class PlayerWalkingSoundHandler : MonoBehaviour
{
    [Header("Walking Clip")]
    [Tooltip("Looping footstep clip. Playback position is preserved across stop/start cycles.")]
    [SerializeField] private AudioClip walkingClip;

    [Header("Owner Local Feedback (2D)")]
    [SerializeField] private bool playOwnerLocally = true;
    [Range(0f, 1f)]
    [SerializeField] private float ownerWalkingVolume = 0.8f;

    [Header("Remote Audibility (3D)")]
    [SerializeField] private bool playRemote3D = true;
    [Range(0f, 1f)]
    [SerializeField] private float remoteWalkingVolume = 0.75f;
    [SerializeField] private float remoteMinDistance = 2f;
    [SerializeField] private float remoteMaxDistance = 18f;

    [Header("Mixer Routing")]
    [Tooltip("Route to the SFX group of NewAudioMixer for centralised volume control.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    private Player player;
    private PlayerMovement playerMovement;
    private Animator characterAnimator;
    private AudioSource ownerAudioSource2D;
    private AudioSource remoteAudioSource3D;
    private int isWalkingHash;
    private bool _isPaused;

    // Tracks the previous walking state so Update only fires on transitions.
    private bool _prevWalking;

    private void Awake()
    {
        player = GetComponent<Player>();
        playerMovement = GetComponent<PlayerMovement>();
        characterAnimator = GetComponentInChildren<Animator>(true);
        isWalkingHash = Animator.StringToHash("isWalking");

        ownerAudioSource2D = gameObject.AddComponent<AudioSource>();
        ownerAudioSource2D.clip = walkingClip;
        ownerAudioSource2D.loop = true;
        ownerAudioSource2D.playOnAwake = false;
        ownerAudioSource2D.spatialBlend = 0f;
        ownerAudioSource2D.volume = ownerWalkingVolume;

        if (outputMixerGroup != null)
            ownerAudioSource2D.outputAudioMixerGroup = outputMixerGroup;

        remoteAudioSource3D = gameObject.AddComponent<AudioSource>();
        remoteAudioSource3D.clip = walkingClip;
        remoteAudioSource3D.loop = true;
        remoteAudioSource3D.playOnAwake = false;
        remoteAudioSource3D.spatialBlend = 1f;
        remoteAudioSource3D.rolloffMode = AudioRolloffMode.Linear;
        remoteAudioSource3D.minDistance = remoteMinDistance;
        remoteAudioSource3D.maxDistance = remoteMaxDistance;
        remoteAudioSource3D.volume = remoteWalkingVolume;

        if (outputMixerGroup != null)
            remoteAudioSource3D.outputAudioMixerGroup = outputMixerGroup;

        if (player == null || playerMovement == null)
        {
            Debug.LogError($"{nameof(PlayerWalkingSoundHandler)}: missing Player or PlayerMovement " +
                           $"on '{gameObject.name}'. Component disabled.", this);
            enabled = false;
        }
    }

    private void Update()
    {
        bool isWalking = ResolveWalkingState();

        // Only act on state transitions — zero per-frame cost when state is stable.
        if (isWalking == _prevWalking) return;
        _prevWalking = isWalking;

        if (isWalking)
            StartWalkingSound();
        else
            PauseWalkingSound();
    }

    private void OnDisable()
    {
        // Pause (not stop) so the clip position is preserved across disable/enable cycles,
        // e.g. when the player object is temporarily deactivated mid-walk.
        var source = ResolveCurrentSource();
        if (source != null && source.isPlaying)
            PauseWalkingSound();

        _prevWalking = false; // force transition check on re-enable
    }

    // -----------------------------------------------------------------
    // State transitions
    // -----------------------------------------------------------------

    private void StartWalkingSound()
    {
        var source = ResolveCurrentSource();
        if (walkingClip == null || source == null) return;

        if (_isPaused)
        {
            // Resume from the exact position where Pause() was called.
            source.UnPause();
            _isPaused = false;
        }
        else
        {
            // First ever start, or restarting after an unexpected stop.
            source.Play();
        }
    }

    private void PauseWalkingSound()
    {
        var source = ResolveCurrentSource();
        if (source == null) return;
        source.Pause();
        _isPaused = true;
    }

    private AudioSource ResolveCurrentSource()
    {
        if (player != null && player.IsOwner)
            return playOwnerLocally ? ownerAudioSource2D : null;

        if (!playRemote3D) return null;

        if (remoteAudioSource3D != null)
        {
            remoteAudioSource3D.minDistance = remoteMinDistance;
            remoteAudioSource3D.maxDistance = remoteMaxDistance;
            remoteAudioSource3D.volume = remoteWalkingVolume;
        }
        return remoteAudioSource3D;
    }

    private bool ResolveWalkingState()
    {
        if (player != null && player.IsOwner)
            return playerMovement != null && playerMovement.IsWalking();

        // For remote players, rely on replicated animator parameter.
        if (characterAnimator == null) return false;
        return characterAnimator.GetBool(isWalkingHash);
    }
}
