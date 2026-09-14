using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class NpcGuestOutfitRandomizer : NetworkBehaviour
{
    public enum OutfitArchetype : byte
    {
        Feminine = 0,
        Masculine = 1,
        Neutral = 2
    }

    public enum OptionStyle
    {
        Neutral = 0,
        Feminine = 1,
        Masculine = 2
    }

    [Serializable]
    public class OutfitCategory
    {
        public string categoryId = "Category";
        public List<string> includeNamePrefixes = new();
        public List<string> excludeNamePrefixes = new();
        public bool includeNoneOption;
        [Min(1)] public int noneWeight = 3;
        public List<OutfitOption> options = new();
    }

    [Serializable]
    public class OutfitOption
    {
        public GameObject optionObject;

        [Header("Style Inference")]
        public OptionStyle inferredStyle = OptionStyle.Neutral;
        public bool overrideInferredStyle;
        public OptionStyle styleOverride = OptionStyle.Neutral;

        [Header("Allowed Archetypes")]
        public bool allowFeminine = true;
        public bool allowMasculine = true;
        public bool allowNeutral = true;

        public OptionStyle EffectiveStyle => overrideInferredStyle ? styleOverride : inferredStyle;
    }

    [Header("Discovery")]
    [SerializeField] private bool autoDiscoverFromChildRenderers = true;

    [Header("Archetype Weights")]
    [SerializeField, Min(1)] private int feminineWeight = 3;
    [SerializeField, Min(1)] private int masculineWeight = 3;
    [SerializeField, Min(1)] private int neutralWeight = 2;

    [Header("Accessory Bias")]
    [SerializeField, Range(0f, 1f)] private float accessoryNoneProbability = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool logOutfitSelection;

    [Header("Bodypart Categories")]
    [SerializeField] private List<OutfitCategory> categories = new();

    private readonly NetworkVariable<FixedString128Bytes> replicatedOutfitIndices = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        EnsureDefaultCategoriesIfEmpty();
        RefreshDiscoveredOptions();
    }

    public override void OnNetworkSpawn()
    {
        replicatedOutfitIndices.OnValueChanged += ReplicatedOutfitIndices_OnValueChanged;
        if (!replicatedOutfitIndices.Value.IsEmpty)
            ApplyFromPayload(replicatedOutfitIndices.Value.ToString());
    }

    public override void OnNetworkDespawn()
    {
        replicatedOutfitIndices.OnValueChanged -= ReplicatedOutfitIndices_OnValueChanged;
    }

    public void ServerInitializeRandomOutfit()
    {
        if (!IsServer) return;

        EnsureDefaultCategoriesIfEmpty();
        RefreshDiscoveredOptions();

        string payload = BuildRandomPayload();
        replicatedOutfitIndices.Value = payload;
        ApplyFromPayload(payload);
    }

    private void ReplicatedOutfitIndices_OnValueChanged(FixedString128Bytes previousValue, FixedString128Bytes newValue)
    {
        if (newValue.IsEmpty) return;
        ApplyFromPayload(newValue.ToString());
    }

    private string BuildRandomPayload()
    {
        if (categories == null || categories.Count == 0) return string.Empty;

        OutfitArchetype archetype = RollArchetype();
        var parts = new string[categories.Count];
        for (int i = 0; i < categories.Count; i++)
        {
            OutfitCategory category = categories[i];
            int selectedIndex = RollIndex(category, archetype);
            parts[i] = selectedIndex.ToString();
        }

        if (logOutfitSelection)
            LogSelection(archetype, parts);

        return $"{(int)archetype};{string.Join(",", parts)}";
    }

    private OutfitArchetype RollArchetype()
    {
        int total = Mathf.Max(1, feminineWeight) + Mathf.Max(1, masculineWeight) + Mathf.Max(1, neutralWeight);
        int roll = UnityEngine.Random.Range(0, total);

        int f = Mathf.Max(1, feminineWeight);
        int m = Mathf.Max(1, masculineWeight);
        if (roll < f) return OutfitArchetype.Feminine;
        if (roll < f + m) return OutfitArchetype.Masculine;
        return OutfitArchetype.Neutral;
    }

    private int RollIndex(OutfitCategory category, OutfitArchetype archetype)
    {
        if (category == null || category.options == null || category.options.Count == 0)
            return -1;

        string categoryId = category.categoryId ?? string.Empty;
        bool isBeard = categoryId.Equals("Beard", StringComparison.OrdinalIgnoreCase);
        bool isAccessory = categoryId.Equals("Accessory", StringComparison.OrdinalIgnoreCase);

        // Strict archetype guards
        if (isBeard && archetype == OutfitArchetype.Feminine)
            return -1;

        List<int> candidates = new();
        for (int i = 0; i < category.options.Count; i++)
        {
            OutfitOption option = category.options[i];
            if (option == null || option.optionObject == null) continue;

            if (IsOptionAllowedForArchetype(option, archetype))
                candidates.Add(i);
        }

        // Guard: if category has no archetype-compatible candidates, keep null-safe for optional slots
        // before allowing fallback to non-compatible options.
        if (candidates.Count == 0)
        {
            if (isBeard || (isAccessory && category.includeNoneOption))
                return -1;

            for (int i = 0; i < category.options.Count; i++)
                if (category.options[i] != null && category.options[i].optionObject != null)
                    candidates.Add(i);

            if (candidates.Count == 0)
                return -1;
        }

        if (isAccessory && category.includeNoneOption && UnityEngine.Random.value < Mathf.Clamp01(accessoryNoneProbability))
            return -1;

        if (!category.includeNoneOption)
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];

        int safeNoneWeight = Mathf.Max(1, category.noneWeight);
        int total = safeNoneWeight + candidates.Count;
        int roll = UnityEngine.Random.Range(0, total);
        if (roll < safeNoneWeight)
            return -1;

        int picked = roll - safeNoneWeight;
        picked = Mathf.Clamp(picked, 0, candidates.Count - 1);
        return candidates[picked];
    }

    private static bool IsOptionAllowedForArchetype(OutfitOption option, OutfitArchetype archetype)
    {
        if (option == null) return false;
        return archetype switch
        {
            OutfitArchetype.Feminine => option.allowFeminine,
            OutfitArchetype.Masculine => option.allowMasculine,
            _ => option.allowNeutral
        };
    }

    private static OptionStyle InferStyle(string categoryId, string optionName)
    {
        if (string.IsNullOrWhiteSpace(optionName))
            return OptionStyle.Neutral;

        string n = optionName;
        bool has(string s) => n.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;

        if (categoryId.Equals("Beard", StringComparison.OrdinalIgnoreCase))
            return OptionStyle.Masculine;

        if (categoryId.Equals("Legs", StringComparison.OrdinalIgnoreCase))
        {
            if (has("Skirt")) return OptionStyle.Feminine;
            return OptionStyle.Neutral;
        }

        if (categoryId.Equals("Hair", StringComparison.OrdinalIgnoreCase))
        {
            if (has("Shave") || has("Buzz") || has("Spiky")) return OptionStyle.Masculine;
            if (has("Long") || has("Pigtails") || has("Ponytail") || has("Bun") || has("ShortBob") || has("Hijab"))
                return OptionStyle.Feminine;
            return OptionStyle.Neutral;
        }

        if (categoryId.Equals("Top", StringComparison.OrdinalIgnoreCase))
            return OptionStyle.Neutral;

        if (categoryId.Equals("Accessory", StringComparison.OrdinalIgnoreCase))
            return OptionStyle.Neutral;

        return OptionStyle.Neutral;
    }

    private void ApplyFromPayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || categories == null || categories.Count == 0)
            return;

        string[] archetypeAndValues = payload.Split(';');
        if (archetypeAndValues.Length < 2) return;

        string[] values = archetypeAndValues[1].Split(',');
        for (int i = 0; i < categories.Count; i++)
        {
            int selected = -1;
            if (i < values.Length)
                int.TryParse(values[i], out selected);

            ApplyCategorySelection(categories[i], selected);
        }
    }

    private static void ApplyCategorySelection(OutfitCategory category, int selectedIndex)
    {
        if (category == null || category.options == null) return;

        // Always disable every object currently tracked by this category first.
        for (int i = 0; i < category.options.Count; i++)
        {
            OutfitOption tracked = category.options[i];
            if (tracked == null || tracked.optionObject == null) continue;
            tracked.optionObject.SetActive(false);
        }

        int clampedSelected = selectedIndex;
        if (category.options.Count == 0)
            clampedSelected = -1;
        else if (selectedIndex >= category.options.Count)
            clampedSelected = category.options.Count - 1;

        for (int i = 0; i < category.options.Count; i++)
        {
            OutfitOption option = category.options[i];
            if (option == null || option.optionObject == null) continue;
            option.optionObject.SetActive(i == clampedSelected);
        }
    }

    private void RefreshDiscoveredOptions()
    {
        if (!autoDiscoverFromChildRenderers) return;
        if (categories == null || categories.Count == 0) return;

        var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < categories.Count; i++)
        {
            OutfitCategory category = categories[i];
            if (category == null) continue;

            // Respect manual inspector configuration: if the category already has
            bool hasManualOptions = false;
            for (int m = 0; m < category.options.Count; m++)
            {
                OutfitOption existing = category.options[m];
                if (existing != null && existing.optionObject != null)
                {
                    hasManualOptions = true;
                    break;
                }
            }
            if (hasManualOptions)
            {
                // Keep inferred style fresh for convenience, but do not change...
                for (int m = 0; m < category.options.Count; m++)
                {
                    OutfitOption manual = category.options[m];
                    if (manual == null || manual.optionObject == null) continue;
                    manual.inferredStyle = InferStyle(category.categoryId ?? string.Empty, manual.optionObject.name);
                }
                continue;
            }

            var previousByObject = new Dictionary<GameObject, OutfitOption>();
            for (int p = 0; p < category.options.Count; p++)
            {
                OutfitOption existing = category.options[p];
                if (existing == null || existing.optionObject == null) continue;
                if (!previousByObject.ContainsKey(existing.optionObject))
                    previousByObject.Add(existing.optionObject, existing);
            }

            category.options.Clear();
            var unique = new HashSet<GameObject>();

            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (renderer == null) continue;

                GameObject candidate = renderer.gameObject;
                string name = candidate.name;
                if (!MatchesPrefixes(name, category.includeNamePrefixes, category.excludeNamePrefixes))
                    continue;

                if (!unique.Add(candidate)) continue;

                if (previousByObject.TryGetValue(candidate, out OutfitOption kept))
                {
                    kept.inferredStyle = InferStyle(category.categoryId ?? string.Empty, candidate.name);
                    category.options.Add(kept);
                    continue;
                }

                OptionStyle inferred = InferStyle(category.categoryId ?? string.Empty, candidate.name);
                category.options.Add(new OutfitOption
                {
                    optionObject = candidate,
                    inferredStyle = inferred,
                    styleOverride = inferred,
                    overrideInferredStyle = false,
                    allowFeminine = inferred == OptionStyle.Neutral || inferred == OptionStyle.Feminine,
                    allowMasculine = inferred == OptionStyle.Neutral || inferred == OptionStyle.Masculine,
                    allowNeutral = inferred == OptionStyle.Neutral
                });
            }

            category.options.Sort((a, b) =>
            {
                string an = a != null && a.optionObject != null ? a.optionObject.name : string.Empty;
                string bn = b != null && b.optionObject != null ? b.optionObject.name : string.Empty;
                return string.CompareOrdinal(an, bn);
            });
        }
    }

    private static bool MatchesPrefixes(string value, List<string> includePrefixes, List<string> excludePrefixes)
    {
        bool includeMatch = includePrefixes != null && includePrefixes.Count > 0;
        if (includeMatch)
        {
            includeMatch = false;
            for (int i = 0; i < includePrefixes.Count; i++)
            {
                string prefix = includePrefixes[i];
                if (string.IsNullOrWhiteSpace(prefix)) continue;
                if (value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    includeMatch = true;
                    break;
                }
            }
        }

        if (!includeMatch) return false;

        if (excludePrefixes == null || excludePrefixes.Count == 0)
            return true;

        for (int i = 0; i < excludePrefixes.Count; i++)
        {
            string prefix = excludePrefixes[i];
            if (string.IsNullOrWhiteSpace(prefix)) continue;
            if (value.StartsWith(prefix, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private void EnsureDefaultCategoriesIfEmpty()
    {
        if (categories != null && categories.Count > 0) return;

        categories = new List<OutfitCategory>
        {
            new OutfitCategory
            {
                categoryId = "Hair",
                includeNamePrefixes = new List<string> { "Hair_" },
                excludeNamePrefixes = new List<string> { "Hair_Acc_" },
                includeNoneOption = false,
                noneWeight = 1
            },
            new OutfitCategory
            {
                categoryId = "HairAcc",
                includeNamePrefixes = new List<string> { "Hair_Acc_" },
                includeNoneOption = true,
                noneWeight = 3
            },
            new OutfitCategory
            {
                categoryId = "Beard",
                includeNamePrefixes = new List<string> { "Beard_" },
                includeNoneOption = true,
                noneWeight = 4
            },
            new OutfitCategory
            {
                categoryId = "Top",
                includeNamePrefixes = new List<string> { "Clothes_Top_" },
                includeNoneOption = false,
                noneWeight = 1
            },
            new OutfitCategory
            {
                categoryId = "Legs",
                includeNamePrefixes = new List<string> { "Clothes_Legs_" },
                includeNoneOption = false,
                noneWeight = 1
            },
            new OutfitCategory
            {
                categoryId = "Accessory",
                includeNamePrefixes = new List<string> { "Accessory_" },
                includeNoneOption = true,
                noneWeight = 2
            }
        };
    }

    private void LogSelection(OutfitArchetype archetype, string[] selectedIndices)
    {
        if (!IsServer) return;
        if (selectedIndices == null) return;

        var pieces = new List<string>(categories.Count);
        for (int i = 0; i < categories.Count; i++)
        {
            OutfitCategory category = categories[i];
            string categoryId = category != null ? category.categoryId : $"Category{i}";

            int idx = -1;
            if (i < selectedIndices.Length)
                int.TryParse(selectedIndices[i], out idx);

            string pickedName = "None";
            if (category != null && category.options != null && idx >= 0 && idx < category.options.Count)
            {
                OutfitOption opt = category.options[idx];
                if (opt != null && opt.optionObject != null)
                    pickedName = opt.optionObject.name;
            }

            pieces.Add($"{categoryId}={pickedName}");
        }

        Debug.Log($"[NpcGuestOutfitRandomizer] NPC='{name}' Archetype={archetype} -> {string.Join(" | ", pieces)}", this);
    }
}
