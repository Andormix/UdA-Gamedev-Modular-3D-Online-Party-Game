using System;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VoiceNarrator : MonoBehaviour
{
    [Header("Spatial Source")]
    [SerializeField] private Transform voiceOrigin;

    [Header("Distance Volume")]
    [SerializeField] private float nearDistance = 2.5f;
    [SerializeField] private float farDistance = 20f;
    [Range(0f, 1f)] [SerializeField] private float minAudibleVolume = 0.20f;
    [Range(0f, 1f)] [SerializeField] private float maxAudibleVolume = 1.00f;
    [SerializeField] private float volumeSmoothing = 8f;

    [Header("3D Audio")]
    [SerializeField] private bool use3DAudio = true;
    [Range(0f, 1f)] [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Custom;
    [SerializeField] private float dopplerLevel = 0f;
    [SerializeField] private float spread = 0f;

    [Header("Base Mix")]
    [Range(0f, 1f)] [SerializeField] private float baseVolume = 1f;

    private AudioSource source;
    private AudioClip playingClip;
    private Transform localPlayerTransform;

    public event Action<AudioClip> OnVoiceFinished;

    public bool IsPlayingVoice => source != null && source.isPlaying;
    public float CurrentOutputVolume => source != null ? source.volume : 0f;

    public float CurrentDistance01
    {
        get
        {
            if (localPlayerTransform == null) return 0.35f;
            float d = Vector3.Distance(transform.position, localPlayerTransform.position);
            return Mathf.InverseLerp(nearDistance, farDistance, d);
        }
    }

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        ConfigureAudioSource();
    }

    private void OnValidate()
    {
        nearDistance = Mathf.Max(0.1f, nearDistance);
        farDistance = Mathf.Max(nearDistance + 0.1f, farDistance);
        minAudibleVolume = Mathf.Clamp01(minAudibleVolume);
        maxAudibleVolume = Mathf.Clamp01(maxAudibleVolume);
        baseVolume = Mathf.Clamp01(baseVolume);
        volumeSmoothing = Mathf.Max(0f, volumeSmoothing);

        if (source != null) ConfigureAudioSource();
    }

    private void Update()
    {
        UpdateVoiceOriginPosition();
        UpdateLocalPlayerRefIfNeeded();
        UpdateDistanceVolume();

        if (playingClip != null && !source.isPlaying)
        {
            var finished = playingClip;
            playingClip = null;
            OnVoiceFinished?.Invoke(finished);
        }
    }

    public void PlayVoice(AudioClip clip)
    {
        if (clip == null) return;

        UpdateVoiceOriginPosition();
        UpdateLocalPlayerRefIfNeeded();

        source.Stop();
        source.clip = clip;
        playingClip = clip;
        source.volume = ComputeTargetVolume();
        source.Play();
    }

    private void ConfigureAudioSource()
    {
        if (source == null) return;
        source.spatialBlend = use3DAudio ? spatialBlend : 0f;
        source.rolloffMode = rolloffMode;
        source.dopplerLevel = dopplerLevel;
        source.spread = spread;
    }

    private void UpdateVoiceOriginPosition()
    {
        if (voiceOrigin != null) transform.position = voiceOrigin.position;
    }

    private void UpdateLocalPlayerRefIfNeeded()
    {
        if (localPlayerTransform != null) return;

        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            var p = players[i];
            if (p != null && p.IsOwner)
            {
                localPlayerTransform = p.transform;
                return;
            }
        }
    }

    private void UpdateDistanceVolume()
    {
        if (source == null) return;
        if (playingClip == null && !source.isPlaying) return;

        float target = ComputeTargetVolume();
        if (volumeSmoothing <= 0f) source.volume = target;
        else source.volume = Mathf.Lerp(source.volume, target, Time.unscaledDeltaTime * volumeSmoothing);
    }

    private float ComputeTargetVolume()
    {
        float distFactor;
        if (localPlayerTransform != null)
        {
            float d = Vector3.Distance(transform.position, localPlayerTransform.position);
            float t = Mathf.InverseLerp(nearDistance, farDistance, d);
            distFactor = Mathf.Lerp(maxAudibleVolume, minAudibleVolume, t);
        }
        else
        {
            distFactor = Mathf.Lerp(maxAudibleVolume, minAudibleVolume, 0.35f);
        }

        float v = baseVolume * distFactor;
        return Mathf.Clamp(v, minAudibleVolume * baseVolume, maxAudibleVolume * baseVolume);
    }

    public void SetVoiceOrigin(Transform origin)
    {
        voiceOrigin = origin;
        UpdateVoiceOriginPosition();
    }
}