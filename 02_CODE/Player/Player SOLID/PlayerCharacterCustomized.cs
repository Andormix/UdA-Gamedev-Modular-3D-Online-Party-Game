using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerCharacterCustomized : NetworkBehaviour
{
    private string PLAYER_PREFS_SAVE = "PlayerCustomizationOptions";

    // 10 bytes per player per sync — replaces LEGACY NetworkVariable<FixedString4096Bytes> Doc at memo
    private NetworkVariable<CustomizationData> _netCustomization =
        new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [SerializeField] private BodyPartData[] bodyPartDataArray;

    public enum BodyPartType
    {
        Hair,
        HairAcc,
        Beard,
        Top,
        Legs,
    }

    [System.Serializable]
    public class BodyPartData
    {
        public BodyPartType bodyPartType;
        public Mesh[] meshArray;
        public Sprite[] iconArray;
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Material[] colorArray;
        [HideInInspector] public int currentColorIndex;
        [HideInInspector] public int currentMeshIndex;
    }

    private void Awake()
    {
        // In menu/offline, ensures indices match visuals before first Save.
        SyncIndicesFromRenderer();
    }

    public void ChangeBodypart(BodyPartType bodyPartType)
    {
        BodyPartData data = GetBodyPartData(bodyPartType);
        if (data == null || data.meshArray == null || data.meshArray.Length == 0) return;

        data.currentMeshIndex = (data.currentMeshIndex + 1) % data.meshArray.Length;
        Apply(data);
    }

    public void ChangeHairColor(BodyPartType bodyPartType)
    {
        BodyPartData bodyPartData = GetBodyPartData(bodyPartType);
        if (bodyPartData == null || bodyPartData.colorArray == null || bodyPartData.colorArray.Length == 0) return;

        bodyPartData.currentColorIndex = (bodyPartData.currentColorIndex + 1) % bodyPartData.colorArray.Length;
        Apply(bodyPartData);
    }

    private BodyPartData GetBodyPartData(BodyPartType bodyPartType)
    {
        foreach (BodyPartData bodyPartData in bodyPartDataArray)
        {
            if (bodyPartData.bodyPartType == bodyPartType)
                return bodyPartData;
        }
        Debug.LogError("Clang 03" + bodyPartType);
        return null;
    }

    [Serializable]
    public class BodyPartTypeIndex
    {
        public BodyPartType bodyPartType;
        public int index;
        public int colorIndex;
    }

    public class SaveObject
    {
        public List<BodyPartTypeIndex> bodyPartTypeIndexList;
    }

    public void Save()
    {
        List<BodyPartTypeIndex> bodyPartTypeIndexList = new List<BodyPartTypeIndex>();

        foreach (BodyPartType bodyPartType in System.Enum.GetValues(typeof(BodyPartType)))
        {
            BodyPartData bodyPartData = GetBodyPartData(bodyPartType);
            if (bodyPartData == null || bodyPartData.skinnedMeshRenderer == null)
                continue;

            int maxMesh = (bodyPartData.meshArray != null && bodyPartData.meshArray.Length > 0)
                ? bodyPartData.meshArray.Length - 1 : 0;

            int maxColor = (bodyPartData.colorArray != null && bodyPartData.colorArray.Length > 0)
                ? bodyPartData.colorArray.Length - 1 : 0;

            int meshIndex = Mathf.Clamp(bodyPartData.currentMeshIndex, 0, maxMesh);
            int colorIndex = Mathf.Clamp(bodyPartData.currentColorIndex, 0, maxColor);

            bodyPartTypeIndexList.Add(new BodyPartTypeIndex
            {
                bodyPartType = bodyPartType,
                index = meshIndex,
                colorIndex = colorIndex,
            });
        }

        SaveObject saveObject = new SaveObject { bodyPartTypeIndexList = bodyPartTypeIndexList };

        string json = JsonUtility.ToJson(saveObject);
        Debug.Log(json);

        PlayerPrefs.SetString(PLAYER_PREFS_SAVE, json);
        PlayerPrefs.Save();
    }

    // Load() for offline/menu preview only — reads from PlayerPrefs
    public void Load()
    {
        if (!PlayerPrefs.HasKey(PLAYER_PREFS_SAVE)) return;
        string json = PlayerPrefs.GetString(PLAYER_PREFS_SAVE, "");
        LoadFromJson(json);
    }

    public void LoadFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        SaveObject saveObject = JsonUtility.FromJson<SaveObject>(json);
        if (saveObject == null || saveObject.bodyPartTypeIndexList == null) return;

        foreach (BodyPartTypeIndex bodyPartTypeIndex in saveObject.bodyPartTypeIndexList)
        {
            BodyPartData bodyPartData = GetBodyPartData(bodyPartTypeIndex.bodyPartType);
            if (bodyPartData == null || bodyPartData.skinnedMeshRenderer == null) continue;

            bodyPartData.currentMeshIndex = bodyPartTypeIndex.index;
            bodyPartData.currentColorIndex = bodyPartTypeIndex.colorIndex;
            Apply(bodyPartData);
        }
    }

    // ── Networking ────────────────────────────────────────────────────────────

    public override void OnNetworkSpawn()
    {
        _netCustomization.OnValueChanged += OnNetCustomizationChanged;

        // Late-joiner safety: if the value is already set, apply immediately.
        // CustomizationData is a value type — check a field to detect non-default.
        CustomizationData current = _netCustomization.Value;
        if (current.hairMesh != 0 || current.hairColor != 0 ||
            current.topMesh  != 0 || current.legsMesh  != 0)
        {
            ApplyCustomizationData(current);
        }
    }

    public override void OnNetworkDespawn()
    {
        _netCustomization.OnValueChanged -= OnNetCustomizationChanged;
    }

    private void OnNetCustomizationChanged(CustomizationData prev, CustomizationData next)
    {
        ApplyCustomizationData(next);
    }

    // Called by GameManager on the server after spawning the player object.
    // Applies locally on server and replicates 10 bytes to all clients.
    public void ServerSetCustomizationData(CustomizationData data)
    {
        if (!IsServer) return;
        ApplyCustomizationData(data);
        _netCustomization.Value = data;
    }

    // ── Struct helpers ────────────────────────────────────────────────────────

    public CustomizationData ToCustomizationData()
    {
        return new CustomizationData
        {
            hairMesh     = (byte)GetBodyPartData(BodyPartType.Hair)?.currentMeshIndex,
            hairAccMesh  = (byte)GetBodyPartData(BodyPartType.HairAcc)?.currentMeshIndex,
            beardMesh    = (byte)GetBodyPartData(BodyPartType.Beard)?.currentMeshIndex,
            topMesh      = (byte)GetBodyPartData(BodyPartType.Top)?.currentMeshIndex,
            legsMesh     = (byte)GetBodyPartData(BodyPartType.Legs)?.currentMeshIndex,

            hairColor    = (byte)GetBodyPartData(BodyPartType.Hair)?.currentColorIndex,
            hairAccColor = (byte)GetBodyPartData(BodyPartType.HairAcc)?.currentColorIndex,
            beardColor   = (byte)GetBodyPartData(BodyPartType.Beard)?.currentColorIndex,
            topColor     = (byte)GetBodyPartData(BodyPartType.Top)?.currentColorIndex,
            legsColor    = (byte)GetBodyPartData(BodyPartType.Legs)?.currentColorIndex,
        };
    }

    public void ApplyCustomizationData(CustomizationData data)
    {
        ApplyPart(BodyPartType.Hair,    data.hairMesh,    data.hairColor);
        ApplyPart(BodyPartType.HairAcc, data.hairAccMesh, data.hairAccColor);
        ApplyPart(BodyPartType.Beard,   data.beardMesh,   data.beardColor);
        ApplyPart(BodyPartType.Top,     data.topMesh,     data.topColor);
        ApplyPart(BodyPartType.Legs,    data.legsMesh,    data.legsColor);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void ApplyPart(BodyPartType type, byte meshIdx, byte colorIdx)
    {
        BodyPartData data = GetBodyPartData(type);
        if (data == null) return;
        data.currentMeshIndex  = meshIdx;
        data.currentColorIndex = colorIdx;
        Apply(data);
    }

    private void Apply(BodyPartData data)
    {
        if (data == null || data.skinnedMeshRenderer == null) return;

        if (data.meshArray != null && data.meshArray.Length > 0)
        {
            data.currentMeshIndex = Mathf.Clamp(data.currentMeshIndex, 0, data.meshArray.Length - 1);
            data.skinnedMeshRenderer.sharedMesh = data.meshArray[data.currentMeshIndex];
        }

        if (data.colorArray != null && data.colorArray.Length > 0)
        {
            data.currentColorIndex = Mathf.Clamp(data.currentColorIndex, 0, data.colorArray.Length - 1);
            data.skinnedMeshRenderer.sharedMaterial = data.colorArray[data.currentColorIndex];
        }
    }

    private void SyncIndicesFromRenderer()
    {
        if (bodyPartDataArray == null) return;

        foreach (var data in bodyPartDataArray)
        {
            if (data == null || data.skinnedMeshRenderer == null) continue;

            if (data.meshArray != null && data.meshArray.Length > 0 && data.skinnedMeshRenderer.sharedMesh != null)
            {
                int idx = System.Array.IndexOf(data.meshArray, data.skinnedMeshRenderer.sharedMesh);
                if (idx >= 0) data.currentMeshIndex = idx;
            }

            if (data.colorArray != null && data.colorArray.Length > 0 && data.skinnedMeshRenderer.sharedMaterial != null)
            {
                int cidx = System.Array.IndexOf(data.colorArray, data.skinnedMeshRenderer.sharedMaterial);
                if (cidx >= 0) data.currentColorIndex = cidx;
            }
        }
    }

    public int GetPartCount(BodyPartType type)
    {
        BodyPartData data = GetBodyPartData(type);
        return (data != null && data.meshArray != null) ? data.meshArray.Length : 0;
    }

    public void SetBodyPart(BodyPartType type, int index)
    {
        Debug.Log($"Player: Receiving change request for {type} to index {index}"); // Add this!
        BodyPartData data = GetBodyPartData(type);
        if (data == null || data.meshArray == null) return;

        data.currentMeshIndex = Mathf.Clamp(index, 0, data.meshArray.Length - 1);
        Apply(data);
    }

    public Sprite[] GetSpritesForPart(BodyPartType type)
    {
        BodyPartData data = GetBodyPartData(type);
        return (data != null) ? data.iconArray : null;
    }

    public Color GetColorFromMaterial(BodyPartType type, int index)
    {
        BodyPartData data = GetBodyPartData(type);
        if (data == null || data.colorArray == null || index >= data.colorArray.Length) 
            return Color.white;

        // This pulls the "_Color" property from your material
        return data.colorArray[index].color; 
    }

    public int GetColorCount(BodyPartType type)
    {
        BodyPartData data = GetBodyPartData(type);
        return (data != null && data.colorArray != null) ? data.colorArray.Length : 0;
    }

    public Color GetColorValue(BodyPartType type, int index)
    {
        BodyPartData data = GetBodyPartData(type);
        if (data == null || data.colorArray == null || index >= data.colorArray.Length) 
            return Color.white;

        Material mat = data.colorArray[index];
        if (mat == null) return Color.white;

        // 1. Try URP Standard property
        if (mat.HasProperty("_BaseColor")) 
            return mat.GetColor("_BaseColor");

        // 2. Try Legacy Standard property
        if (mat.HasProperty("_Color")) 
            return mat.GetColor("_Color");

        // 3. Fallback: If it's a "Hair" material, maybe try to look for a specific color?
        // Otherwise, return white and let the icon show
        return Color.white; 
    }

    public void SetBodyPartColor(BodyPartType type, int index)
    {
        BodyPartData data = GetBodyPartData(type);
        if (data == null || data.colorArray == null) return;
        data.currentColorIndex = Mathf.Clamp(index, 0, data.colorArray.Length - 1);
        Apply(data);
    }

    public BodyPartData GetBodyPartData_Public(BodyPartType type)
    {
        foreach (BodyPartData data in bodyPartDataArray)
        {
            if (data.bodyPartType == type) return data;
        }
        return null;
    }


}

