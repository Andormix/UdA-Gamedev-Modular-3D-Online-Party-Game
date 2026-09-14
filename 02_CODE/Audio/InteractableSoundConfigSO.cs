using UnityEngine;
using UnityEngine.Audio;

// Ruta: Create via: Assets > Create > Audio > Interactable Sound Config

[CreateAssetMenu(fileName = "InteractableSoundConfig", menuName = "Audio/Interactable Sound Config")]
public class InteractableSoundConfigSO : ScriptableObject
{
    [Header("Hover")]
    [Tooltip("Plays once when the local player's crosshair enters this interactable.")]
    public AudioClip onHoverClip;
    [Range(0f, 1f)] public float hoverVolume = 0.5f;

    [Header("Interact (one-shot)")]
    [Tooltip("Plays once when the local player presses Interact while looking at this object.")]
    public AudioClip onInteractClip;
    [Range(0f, 1f)] public float interactVolume = 0.8f;

    [Header("Work Hold")]
    [Tooltip("Plays when the local player starts holding the Interact button on a workstation.")]
    public AudioClip onWorkStartClip;
    [Tooltip("Plays when the local player releases the Interact button or the workstation finishes.")]
    public AudioClip onWorkStopClip;
    [Range(0f, 1f)] public float workVolume = 0.7f;

    [Header("3D Spatial Audio")]
    [Tooltip("0 = fully 2D (UI-like), 1 = fully 3D positional. Use 1 for world objects.")]
    [Range(0f, 1f)] public float spatialBlend = 1f;
    [Tooltip("Distance at which the sound reaches full volume.")]
    public float minDistance = 1f;
    [Tooltip("Distance at which the sound is fully attenuated.")]
    public float maxDistance = 10f;

    [Header("Mixer Routing")]
    [Tooltip("Route to the SFX group of NewAudioMixer for centralised volume control.")]
    public AudioMixerGroup outputMixerGroup;
}
