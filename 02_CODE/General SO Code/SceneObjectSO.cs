using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu()]
public class SceneObjectSO : ScriptableObject
{
    public Transform prefab;
    public string objectName;
    [Tooltip("Prompt shown when this SceneObject is loose in the world and can be picked up. Leave empty to use fallback.")]
    public string worldInteractPromptText;
    public Sprite sprite;
    public bool finalComponent;
    public bool deliverable;
    public string tutorialId;

    [Header("Consumption / Cleaning")]
    [Tooltip("If this SO is consumed at table, this points to its dirty variant SO.")]
    public SceneObjectSO dirtyVariantSO;

    [Tooltip("Optional marker for dirty items.")]
    public bool isDirtyItem;

    [Header("Cleaning Return")]
    [Tooltip("If this is a dirty SO, this points to the clean SO it becomes after washing.")]
    public SceneObjectSO cleanedResultSO;

    [Header("SFX (optional)")]
    [Tooltip("Played locally on the owning player's machine when this object is picked up. Leave empty for no sound.")]
    public AudioClip pickupClip;
    [Tooltip("Played locally on the owning player's machine when this object is dropped or delivered. Leave empty for no sound.")]
    public AudioClip dropClip;

    [Header("Coin Value (base + random range)")]
    [FormerlySerializedAs("basePriceCents")]
    public int baseCoinValue = 100;

    [FormerlySerializedAs("minRandomDeltaCents")]
    public int minRandomDeltaCoins = -20;

    [FormerlySerializedAs("maxRandomDeltaCents")]
    public int maxRandomDeltaCoins = 20;

    public string GetWorldInteractPromptText()
    {
        if (!string.IsNullOrWhiteSpace(worldInteractPromptText))
            return worldInteractPromptText;

        if (!string.IsNullOrWhiteSpace(objectName))
            return $"Pick up {objectName}";

        return "Pick up";
    }
}