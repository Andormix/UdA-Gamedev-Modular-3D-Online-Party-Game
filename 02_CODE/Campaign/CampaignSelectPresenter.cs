using UnityEngine;
using UnityEngine.UI;

public class CampaignSelectPresenter : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private Transform container;
    [SerializeField] private CampaignLevelButtonUI rowTemplate;
    [SerializeField] private MainMenuPresenter mainMenuPresenter;
    [SerializeField] private Button coopButton;

    [Header("Optional fallback")]
    [SerializeField] private CampaignManager campaignManagerPrefab;

    [Header("Tutorial First Card")]
    [SerializeField] private bool showTutorialCardFirst = true;
    [SerializeField] private string tutorialScenarioId = "CoreLoop_v1";
    [SerializeField] private Sprite tutorialCardArtwork;

    private void Awake()
    {
        if (container == null || rowTemplate == null)
        {
            Debug.LogError("[CampaignSelectPresenter] Missing refs (container/rowTemplate).", this);
            enabled = false;
            return;
        }

        rowTemplate.gameObject.SetActive(false);

        if (coopButton != null)
            coopButton.onClick.AddListener(OnCoopButtonClicked);
    }

    private void Start()
    {
        EnsureCampaignManager();
        Build();
    }

    private void EnsureCampaignManager()
    {
        if (CampaignManager.Instance != null) return;

        if (campaignManagerPrefab != null)
        {
            Instantiate(campaignManagerPrefab);
            Debug.Log("[CampaignSelectPresenter] CampaignManager instantiated from prefab fallback.");
        }
        else
        {
            Debug.LogError("[CampaignSelectPresenter] CampaignManager missing and no prefab fallback assigned.");
        }
    }

    private void Build()
    {
        if (CampaignManager.Instance == null || CampaignManager.Instance.Definition == null)
        {
            Debug.LogError("[CampaignSelect] CampaignManager/Definition missing.");
            return;
        }

        var def = CampaignManager.Instance.Definition;
        var save = CampaignManager.Instance.GetSaveData();

        if (def == null || def.levels == null)
        {
            Debug.LogError("[CampaignSelect] Campaign definition missing or empty.");
            return;
        }

        foreach (Transform c in container)
        {
            if (c == rowTemplate.transform) continue;
            Destroy(c.gameObject);
        }

        // "NEW" = last unlocked real campaign level in campaign order
        int lastUnlockedIndex = -1;
        for (int i = 0; i < def.levels.Count; i++)
        {
            var level = def.levels[i];
            if (level == null) continue;

            var p = save.levels.Find(x => x.levelId == level.levelId);
            if (p != null && p.unlocked) lastUnlockedIndex = i;
        }

        int created = 0;

        // 1) Tutorial card FIRST (opcional)
        if (showTutorialCardFirst)
        {
            var row = Instantiate(rowTemplate, container);
            row.gameObject.SetActive(true);
            row.SetupAsTutorialCard("Tutorial", OnTutorialCardClicked, tutorialCardArtwork);
            created++;
        }

        // 2) Normal campaign levels - after tutorial
        for (int i = 0; i < def.levels.Count; i++)
        {
            var level = def.levels[i];
            if (level == null) continue;

            var progress = save.levels.Find(x => x.levelId == level.levelId);
            bool showNew = (i == lastUnlockedIndex);

            var row = Instantiate(rowTemplate, container);
            row.gameObject.SetActive(true);
            row.Setup(level, progress, showNew);
            created++;
        }

        Debug.Log($"[CampaignSelect] Built rows: {created}");
        if (created == 0)
            Debug.LogError("[CampaignSelect] 0 rows created. Check CampaignDefinition asset content.");
    }

    private void OnTutorialCardClicked()
    {
        var session = SessionCoordinator.Instance;
        if (session == null)
        {
            Debug.LogError("[CampaignSelect] SessionCoordinator missing.");
            return;
        }

        TutorialRunStarter.StartSingleplayerTutorial(
            session,
            Loader.Scene.Map_01,
            tutorialScenarioId
        );
    }

    private void OnCoopButtonClicked()
    {
        mainMenuPresenter.OnCoopCampaignButtonClicked();
    }
}