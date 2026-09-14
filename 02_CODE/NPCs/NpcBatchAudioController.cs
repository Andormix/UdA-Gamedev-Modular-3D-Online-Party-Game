using UnityEngine;
using UnityEngine.Audio;

[DisallowMultipleComponent]
public class NpcBatchAudioController : MonoBehaviour
{
    public static NpcBatchAudioController Instance { get; private set; }

    [Header("Spatial Anchors (Entry/Exit Doors)")]
    [SerializeField] private Transform spawnAudioAnchor;
    [SerializeField] private Transform despawnAudioAnchor;

    [Header("Audio Source Defaults")]
    [SerializeField] private AudioMixerGroup outputMixerGroup;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 28f;

    [Header("Batch Clip Pools (Random)")]
    [SerializeField] private AudioClip[] npcSpawnBatchClips;
    [SerializeField] private AudioClip[] npcDespawnBatchClips;
    [SerializeField, Range(0f, 1f)] private float npcSpawnBatchVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float npcDespawnBatchVolume = 0.95f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveAnchors();
    }

    public void PlayNpcSpawnBatch()
    {
        PlayOneShotFromPool(spawnAudioAnchor, npcSpawnBatchClips, npcSpawnBatchVolume);
    }

    public void PlayNpcDespawnBatch()
    {
        PlayOneShotFromPool(despawnAudioAnchor, npcDespawnBatchClips, npcDespawnBatchVolume);
    }

    private void PlayOneShotFromPool(Transform anchor, AudioClip[] clipPool, float volume)
    {
        AudioClip clip = PickRandomClip(clipPool);
        if (clip == null) return;

        Vector3 position = anchor != null ? anchor.position : transform.position;
        var oneShotObject = new GameObject("NpcBatchAudioOneShot");
        oneShotObject.transform.SetPositionAndRotation(position, Quaternion.identity);

        AudioSource source = oneShotObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Linear;
        if (outputMixerGroup != null)
            source.outputAudioMixerGroup = outputMixerGroup;

        source.Play();
        Destroy(oneShotObject, clip.length + 0.1f);
    }

    private AudioClip PickRandomClip(AudioClip[] clipPool)
    {
        if (clipPool == null || clipPool.Length == 0) return null;
        int start = Random.Range(0, clipPool.Length);
        for (int i = 0; i < clipPool.Length; i++)
        {
            AudioClip clip = clipPool[(start + i) % clipPool.Length];
            if (clip != null) return clip;
        }
        return null;
    }

    private void ResolveAnchors()
    {
        if (spawnAudioAnchor == null)
            spawnAudioAnchor = transform;
        if (despawnAudioAnchor == null)
            despawnAudioAnchor = transform;
    }

    private void OnValidate()
    {
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        npcSpawnBatchVolume = Mathf.Clamp01(npcSpawnBatchVolume);
        npcDespawnBatchVolume = Mathf.Clamp01(npcDespawnBatchVolume);
        ResolveAnchors();
    }
}
