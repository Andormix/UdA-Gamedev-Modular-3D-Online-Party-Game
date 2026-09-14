using UnityEngine;
using UnityEngine.UI;

public class VoiceProximityIcon3D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private VoiceNarrator narrator;
    [SerializeField] private AudioSource source;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform iconRoot;
    [SerializeField] private Image iconImage;

    [Header("World Follow (optional)")]
    [Tooltip("Optional. If this object is already parented to jaw/head bone, you can leave this null.")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Audio Reactivity")]
    [SerializeField] private int sampleSize = 128;
    [SerializeField] private float sensitivity = 16f;
    [SerializeField] private float floor = 0.008f;
    [SerializeField] private float attack = 18f;
    [SerializeField] private float release = 8f;

    [Header("Scale + Alpha")]
    [SerializeField] private Vector2 voiceScaleRange = new Vector2(0.9f, 1.25f);
    [SerializeField] private float showAlpha = 1f;
    [SerializeField] private float hideAlpha = 0f;
    [SerializeField] private float alphaLerp = 12f;

    [Header("Distance Influence")]
    [SerializeField] private bool useDistanceInfluence = true;
    [SerializeField] private Vector2 distanceScaleRange = new Vector2(1f, 0.75f); // near -> far
    [SerializeField] private Vector2 distanceAlphaRange = new Vector2(1f, 0.55f); // near -> far

    [Header("Safety Clamps")]
    [SerializeField] private float minFinalScale = 0.65f;
    [SerializeField] private float maxFinalScale = 1.6f;
    [SerializeField] private float minFinalAlpha = 0.2f;
    [SerializeField] private float maxFinalAlpha = 1f;

    private float[] samples;
    private float env;

    private void Awake()
    {
        if (narrator == null) narrator = FindFirstObjectByType<VoiceNarrator>();
        if (source == null && narrator != null) source = narrator.GetComponent<AudioSource>();
        if (canvasGroup == null) canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        if (iconRoot == null) iconRoot = GetComponentInChildren<RectTransform>(true);

        sampleSize = Mathf.Clamp(sampleSize, 64, 1024);
        samples = new float[sampleSize];

        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (narrator == null || source == null || iconRoot == null || canvasGroup == null) return;

        if (followTarget != null)
            transform.position = followTarget.position + localOffset;

        bool speaking = narrator.IsPlayingVoice;
        float raw = speaking ? GetAudioLevel() : 0f;

        float speed = raw > env ? attack : release;
        env = Mathf.Lerp(env, raw, Time.unscaledDeltaTime * speed);

        float d01 = narrator.CurrentDistance01;
        float voiceScale = Mathf.Lerp(voiceScaleRange.x, voiceScaleRange.y, env);
        float distScale = useDistanceInfluence ? Mathf.Lerp(distanceScaleRange.x, distanceScaleRange.y, d01) : 1f;
        float finalScale = Mathf.Clamp(voiceScale * distScale, minFinalScale, maxFinalScale);

        iconRoot.localScale = new Vector3(finalScale, finalScale, 1f);

        float targetBaseAlpha = speaking ? showAlpha : hideAlpha;
        float distAlphaMul = useDistanceInfluence ? Mathf.Lerp(distanceAlphaRange.x, distanceAlphaRange.y, d01) : 1f;
        float alphaTarget = speaking
            ? Mathf.Clamp(targetBaseAlpha * distAlphaMul, minFinalAlpha, maxFinalAlpha)
            : hideAlpha;

        canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, alphaTarget, Time.unscaledDeltaTime * alphaLerp);

        if (iconImage != null)
        {
            float c = Mathf.Lerp(0.85f, 1f, env);
            iconImage.color = new Color(c, c, c, 1f);
        }
    }

    private float GetAudioLevel()
    {
        if (!source.isPlaying) return 0f;

        source.GetOutputData(samples, 0);
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += samples[i] * samples[i];

        float rms = Mathf.Sqrt(sum / samples.Length);
        return Mathf.Clamp01(Mathf.Max(0f, (rms - floor) * sensitivity));
    }
}