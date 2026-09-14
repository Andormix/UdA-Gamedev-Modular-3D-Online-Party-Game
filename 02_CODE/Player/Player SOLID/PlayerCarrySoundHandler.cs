using UnityEngine;
using UnityEngine.Audio;

public class PlayerCarrySoundHandler : MonoBehaviour
{
    [Header("Owner Local Feedback (2D)")]
    [SerializeField] private bool playOwnerLocally = true;
    [Range(0f, 1f)]
    [SerializeField] private float ownerPickupVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float ownerDropVolume = 0.7f;

    [Header("Remote Audibility (3D)")]
    [SerializeField] private bool playRemote3D = true;
    [Range(0f, 1f)]
    [SerializeField] private float remotePickupVolume = 0.8f;
    [Range(0f, 1f)]
    [SerializeField] private float remoteDropVolume = 0.7f;
    [SerializeField] private float remoteMinDistance = 1.5f;
    [SerializeField] private float remoteMaxDistance = 12f;

    [Header("Mixer Routing")]
    [Tooltip("Route to the SFX group of NewAudioMixer for centralised volume control.")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;

    private Player player;
    private PlayerCarryNet carryNet;
    private AudioSource ownerAudioSource2D;
    private AudioSource worldAudioSource3D;

    private void Awake()
    {
        player   = GetComponent<Player>();
        carryNet = GetComponent<PlayerCarryNet>();

        ownerAudioSource2D = gameObject.AddComponent<AudioSource>();
        ownerAudioSource2D.playOnAwake = false;
        ownerAudioSource2D.loop = false;
        ownerAudioSource2D.spatialBlend = 0f;
        ownerAudioSource2D.volume = 1f;

        if (outputMixerGroup != null)
            ownerAudioSource2D.outputAudioMixerGroup = outputMixerGroup;

        worldAudioSource3D = gameObject.AddComponent<AudioSource>();
        worldAudioSource3D.playOnAwake = false;
        worldAudioSource3D.loop = false;
        worldAudioSource3D.spatialBlend = 1f;
        worldAudioSource3D.rolloffMode = AudioRolloffMode.Linear;
        worldAudioSource3D.minDistance = remoteMinDistance;
        worldAudioSource3D.maxDistance = remoteMaxDistance;
        worldAudioSource3D.volume = 1f;

        if (outputMixerGroup != null)
            worldAudioSource3D.outputAudioMixerGroup = outputMixerGroup;

        if (player == null || carryNet == null)
        {
            Debug.LogError($"{nameof(PlayerCarrySoundHandler)}: missing Player or PlayerCarryNet " +
                           $"on '{gameObject.name}'. Component disabled.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (carryNet == null) return;
        carryNet.OnReplicatedItemPickedUp += HandleItemPickedUp;
        carryNet.OnReplicatedItemDropped += HandleItemDropped;
    }

    private void OnDisable()
    {
        if (carryNet == null) return;
        carryNet.OnReplicatedItemPickedUp -= HandleItemPickedUp;
        carryNet.OnReplicatedItemDropped -= HandleItemDropped;
    }

    // -----------------------------------------------------------------
    // Event handlers
    // -----------------------------------------------------------------

    private void HandleItemPickedUp(SceneObject obj, bool isOwnerEvent)
    {
        if (obj == null) return;

        AudioClip clip = obj.GetSceneObjectSO()?.pickupClip;
        PlayOneShotByAudience(clip, isOwnerEvent, ownerPickupVolume, remotePickupVolume);
    }

    private void HandleItemDropped(SceneObject obj, bool isOwnerEvent)
    {
        if (obj == null) return;

        AudioClip clip = obj.GetSceneObjectSO()?.dropClip;
        PlayOneShotByAudience(clip, isOwnerEvent, ownerDropVolume, remoteDropVolume);
    }

    // -----------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------

    private void PlayOneShotByAudience(AudioClip clip, bool isOwnerEvent, float ownerVolume, float remoteVolume)
    {
        if (clip == null) return;

        if (isOwnerEvent)
        {
            if (!playOwnerLocally) return;
            if (ownerAudioSource2D == null) return;
            ownerAudioSource2D.PlayOneShot(clip, ownerVolume);
            return;
        }

        if (!playRemote3D) return;
        if (worldAudioSource3D == null) return;

        worldAudioSource3D.minDistance = remoteMinDistance;
        worldAudioSource3D.maxDistance = remoteMaxDistance;
        worldAudioSource3D.PlayOneShot(clip, remoteVolume);
    }
}
