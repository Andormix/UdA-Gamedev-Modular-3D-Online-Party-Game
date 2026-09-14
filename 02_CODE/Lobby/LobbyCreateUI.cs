using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyCreateUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button createPrivate;
    [SerializeField] private Button createPublic;
    [SerializeField] private Button closeButton;

    [Header("Lobby Fields")]
    [SerializeField] private TMP_InputField lobbyNameInputField;

    [Header("Game Mode")]
    [SerializeField] private Toggle multiplayerToggle;   
    [SerializeField] private Toggle coopCampaignToggle;
    [SerializeField] private GameObject mapSelectionRoot; // panel that contains scroll list

    [Header("Map Scroll List (Template-based)")]
    [SerializeField] private Transform mapListContainer;  // Content transform
    [SerializeField] private GameObject mapItemTemplate;  // Disabled template with Button + TMP_Text
    [SerializeField] private TMP_Text selectedMapLabel;   // Optional: shows selected map name

    [Header("Map Row Colors")]
    [SerializeField] private Color selectedRowColor = new Color32(0xB3, 0xB3, 0xB3, 0xFF);   // B3B3B3
    [SerializeField] private Color unselectedRowColor = new Color32(0x96, 0x96, 0x96, 0xFF);

    [Header("Random Lobby Name")]
    [SerializeField] private bool autoGenerateNameOnShow = true;
    [SerializeField] private bool onlyIfEmpty = true;

    private readonly List<CampaignLevelDefinitionSO> selectableLevels = new();
    private int selectedMapIndex = 0;
    private bool _isSwitchingModeToggles;

    private static readonly string[] NamePartA =
    {
        "Waffle","Sneaky","Turbo","Dizzy","Bouncy","Ninja","Chunky","Spicy","Cosmic","Derpy",
        "Fluffy","Sassy","Banana","Pixel","Giga","Mighty","Goofy","Jelly","Captain","Funky"
    };

    private static readonly string[] NamePartB =
    {
        "Pirate","Potato","Panda","Wizard","Goblin","Penguin","Raccoon","Noodle","Dragon","Muffin",
        "Pickle","Hamster","Rocket","Toaster","Unicorn","Bandit","Otter","Wombat","Meerkat","Burrito"
    };

    private void Awake()
    {
        createPublic.onClick.AddListener(async () => await CreateLobbyFromUi(isPrivate: false));
        createPrivate.onClick.AddListener(async () => await CreateLobbyFromUi(isPrivate: true));
        closeButton.onClick.AddListener(Hide);

        if (coopCampaignToggle != null)
            coopCampaignToggle.onValueChanged.AddListener(OnCoopToggleChanged);

        if (multiplayerToggle != null)
            multiplayerToggle.onValueChanged.AddListener(OnMultiplayerToggleChanged);

        if (mapItemTemplate != null)
            mapItemTemplate.SetActive(false);
    }

    private void Start()
    {
        // Default mode: Multiplayer (if both assigned)
        _isSwitchingModeToggles = false;
        if (multiplayerToggle != null) multiplayerToggle.isOn = false;
        if (coopCampaignToggle != null) coopCampaignToggle.isOn = true;
        _isSwitchingModeToggles = false;

        RebuildMapList();
        ApplyMapPickerVisibility();
        Hide();
    }

    private async System.Threading.Tasks.Task CreateLobbyFromUi(bool isPrivate)
    {
        string lobbyName = lobbyNameInputField != null ? lobbyNameInputField.text : "Lobby";

        bool isCoop = coopCampaignToggle != null && coopCampaignToggle.isOn;
        Loader.Scene selectedScene = Loader.Scene.Map_01;

        if (isCoop)
            selectedScene = GetSelectedSceneFromList();

        await GameLobby.Instance.CreateLobby(lobbyName, isPrivate, selectedScene, isCoop);
    }

    private Loader.Scene GetSelectedSceneFromList()
    {
        if (selectableLevels.Count == 0) return Loader.Scene.Map_01;

        int idx = Mathf.Clamp(selectedMapIndex, 0, selectableLevels.Count - 1);
        var level = selectableLevels[idx];
        return level != null ? level.scene : Loader.Scene.Map_01;
    }

    private void RebuildMapList()
    {
        selectableLevels.Clear();

        if (mapListContainer == null || mapItemTemplate == null)
            return;

        foreach (Transform child in mapListContainer)
        {
            if (child.gameObject == mapItemTemplate) continue;
            Destroy(child.gameObject);
        }

        if (CampaignManager.Instance == null || CampaignManager.Instance.Definition == null)
        {
            CreateMapRow("Map_01", Loader.Scene.Map_01.ToString(), 0);
            selectedMapIndex = 0;
            UpdateSelectedMapLabel("Map_01");
            RefreshRowSelectionVisuals();
            return;
        }

        CampaignDefinitionSO def = CampaignManager.Instance.Definition;
        CampaignSaveData save = CampaignManager.Instance.GetSaveData();

        int rowIndex = 0;
        foreach (var level in def.levels)
        {
            if (level == null) continue;

            var progress = save.levels.Find(x => x.levelId == level.levelId);
            bool unlocked = progress != null && progress.unlocked;
            if (!unlocked) continue;

            selectableLevels.Add(level);

            string label = string.IsNullOrWhiteSpace(level.displayName)
                ? level.levelId
                : $"{level.displayName} ({level.levelId})";

            CreateMapRow(label, level.levelId, rowIndex);
            rowIndex++;
        }

        if (selectableLevels.Count == 0)
        {
            CreateMapRow("Map_01", Loader.Scene.Map_01.ToString(), 0);
            selectedMapIndex = 0;
            UpdateSelectedMapLabel("Map_01");
            RefreshRowSelectionVisuals();
            return;
        }

        selectedMapIndex = Mathf.Clamp(selectedMapIndex, 0, selectableLevels.Count - 1);
        string selectedLabel = string.IsNullOrWhiteSpace(selectableLevels[selectedMapIndex].displayName)
            ? selectableLevels[selectedMapIndex].levelId
            : $"{selectableLevels[selectedMapIndex].displayName} ({selectableLevels[selectedMapIndex].levelId})";
        UpdateSelectedMapLabel(selectedLabel);
        RefreshRowSelectionVisuals();
    }

    private void CreateMapRow(string visibleLabel, string levelId, int index)
    {
        GameObject row = Instantiate(mapItemTemplate, mapListContainer);
        row.SetActive(true);

        TMP_Text txt = row.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.text = visibleLabel;

        Button btn = row.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                selectedMapIndex = index;
                UpdateSelectedMapLabel(visibleLabel);
                RefreshRowSelectionVisuals();
            });
        }

        var marker = row.GetComponent<MapRowMarker>();
        if (marker == null) marker = row.AddComponent<MapRowMarker>();
        marker.Index = index;
    }

    private void RefreshRowSelectionVisuals()
    {
        if (mapListContainer == null) return;

        foreach (Transform child in mapListContainer)
        {
            if (child.gameObject == mapItemTemplate) continue;

            var marker = child.GetComponent<MapRowMarker>();
            var image = child.GetComponent<Image>();
            bool isSelected = marker != null && marker.Index == selectedMapIndex;

            if (image != null)
                image.color = isSelected ? selectedRowColor : unselectedRowColor;
        }
    }

    private void UpdateSelectedMapLabel(string label)
    {
        if (selectedMapLabel != null)
            selectedMapLabel.text = label;
    }

    private void OnCoopToggleChanged(bool isOn)
    {
        if (_isSwitchingModeToggles) return;

        _isSwitchingModeToggles = true;

        if (isOn)
        {
            if (multiplayerToggle != null) multiplayerToggle.isOn = false;
        }
        else
        {
            if (multiplayerToggle != null && !multiplayerToggle.isOn)
                multiplayerToggle.isOn = true;
        }

        _isSwitchingModeToggles = false;
        ApplyMapPickerVisibility();
    }

    private void OnMultiplayerToggleChanged(bool isOn)
    {
        if (_isSwitchingModeToggles) return;

        _isSwitchingModeToggles = true;

        if (isOn)
        {
            if (coopCampaignToggle != null) coopCampaignToggle.isOn = false;
        }
        else
        {
            if (coopCampaignToggle != null && !coopCampaignToggle.isOn)
                coopCampaignToggle.isOn = true;
        }

        _isSwitchingModeToggles = false;
        ApplyMapPickerVisibility();
    }

    private void ApplyMapPickerVisibility()
    {
        bool show = coopCampaignToggle != null && coopCampaignToggle.isOn;

        if (mapSelectionRoot != null)
            mapSelectionRoot.SetActive(show);
    }

    public void Show()
    {
        RebuildMapList();
        ApplyMapPickerVisibility();

        if (lobbyNameInputField != null && autoGenerateNameOnShow)
        {
            bool shouldGenerate = !onlyIfEmpty || string.IsNullOrWhiteSpace(lobbyNameInputField.text);
            if (shouldGenerate)
                lobbyNameInputField.SetTextWithoutNotify(GenerateRandomLobbyName());
        }

        gameObject.SetActive(true);
    }

    private void Hide()
    {
        gameObject.SetActive(false);
    }

    private string GenerateRandomLobbyName()
    {
        string a = NamePartA[Random.Range(0, NamePartA.Length)];
        string b = NamePartB[Random.Range(0, NamePartB.Length)];
        return $"{a}{b}";
    }

    private class MapRowMarker : MonoBehaviour
    {
        public int Index;
    }
}