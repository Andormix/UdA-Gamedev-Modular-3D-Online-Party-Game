using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;

public class CampaignLevelButtonUI : MonoBehaviour, IPointerClickHandler
{
    [Header("Visual State")]
    [SerializeField] private GameObject lockedRoot;     // image/group shown when locked
    [SerializeField] private GameObject unlockedRoot;   // image/group shown when unlocked

    [Header("Texts")]
    [SerializeField] private TMP_Text titleAndScoreText; // "Cloudy Beach\n<#6b83a2><size=70%>Score: 1234"
    [SerializeField] private TMP_Text starsCounterText;  // "3/3"

    [Header("Progress")]
    [SerializeField] private Slider starsSlider;         // 0..3

    [Header("NEW Badge")]
    [SerializeField] private GameObject newBadgeRoot;    // parent object for "NEEW" image+text

    [Header("Artwork")]
    [SerializeField] private Image lockedPreviewImage;     // Image inside lockedRoot to display BlockedImage sprite
    [SerializeField] private Image unlockedPreviewImage;   // Image inside unlockedRoot to display UnlockedImage sprite

    [Header("Optional legacy button (if still present)")]
    [SerializeField] private Button playButton;

    [Header("Fallback (if SessionCoordinator is missing)")]
    [SerializeField] private SessionCoordinator sessionCoordinatorPrefab;

    private CampaignLevelDefinitionSO def;
    private bool isUnlocked;

    // Tutorial-card mode
    private bool isTutorialCard;
    private Action tutorialClickAction;

    private void Awake()
    {
        EnsurePreviewImageBindings(logWarnings: false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsurePreviewImageBindings(logWarnings: true);
    }
#endif

    public void Setup(CampaignLevelDefinitionSO levelDef, LevelProgressData progress, bool showNewBadge = false)
    {
        EnsurePreviewImageBindings(logWarnings: false);
        isTutorialCard = false;
        tutorialClickAction = null;

        def = levelDef;

        string title = string.IsNullOrWhiteSpace(def.displayName) ? def.levelId : def.displayName;
        int bestScore = progress != null ? progress.bestScore : 0;
        float bestStars = progress != null ? progress.bestStars : 0f;
        isUnlocked = progress != null && progress.unlocked;

        if (lockedRoot != null) lockedRoot.SetActive(!isUnlocked);
        if (unlockedRoot != null) unlockedRoot.SetActive(isUnlocked);

        if (lockedPreviewImage != null) lockedPreviewImage.sprite = def.lockedImage;
        if (unlockedPreviewImage != null) unlockedPreviewImage.sprite = def.unlockedImage;

        if (newBadgeRoot != null) newBadgeRoot.SetActive(isUnlocked && showNewBadge);

        if (titleAndScoreText != null)
        {
            titleAndScoreText.text =
                $"{title}\n<#6b83a2><size=70%>Score: {bestScore}";
        }

        if (starsSlider != null)
        {
            starsSlider.minValue = 0f;
            starsSlider.maxValue = 3f;
            starsSlider.wholeNumbers = false;
            starsSlider.value = Mathf.Clamp(bestStars, 0f, 3f);
            starsSlider.interactable = false;
        }

        if (starsCounterText != null)
        {
            string starsText = Mathf.Approximately(bestStars % 1f, 0f) ? $"{bestStars:0}" : $"{bestStars:0.0}";
            starsCounterText.text = $"{starsText}/3";
        }

        if (playButton != null)
        {
            playButton.interactable = isUnlocked;
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
    }

    // tutorial card setup
    public void SetupAsTutorialCard(string title, Action onClick, Sprite artwork = null)
    {
        EnsurePreviewImageBindings(logWarnings: false);
        isTutorialCard = true;
        tutorialClickAction = onClick;
        def = null;
        isUnlocked = true;

        if (lockedRoot != null) lockedRoot.SetActive(false);
        if (unlockedRoot != null) unlockedRoot.SetActive(true);

        if (lockedPreviewImage != null) lockedPreviewImage.sprite = null;
        if (unlockedPreviewImage != null)
        {
            // Fallback chain avoids a white/default card if tutorial artwork is missing on the active presenter instance.
            Sprite resolvedArtwork = artwork;
            if (resolvedArtwork == null && CampaignManager.Instance != null && CampaignManager.Instance.Definition != null)
            {
                var levels = CampaignManager.Instance.Definition.levels;
                if (levels != null && levels.Count > 0 && levels[0] != null)
                    resolvedArtwork = levels[0].unlockedImage;
            }

            unlockedPreviewImage.sprite = resolvedArtwork;
            unlockedPreviewImage.color = Color.white;

            if (resolvedArtwork == null)
                Debug.LogWarning("[CampaignTutorialCard] No tutorial artwork assigned and no fallback level artwork found. Card may appear white.", this);
        }

        // can choose if tutorial shows NEW badge (IMETGE VERMELLA):
        if (newBadgeRoot != null) newBadgeRoot.SetActive(false);

        if (titleAndScoreText != null)
        {
            titleAndScoreText.text = $"{title}\n<size=70%>Practice the full loop";
        }

        if (starsSlider != null)
        {
            starsSlider.minValue = 0f;
            starsSlider.maxValue = 3f;
            starsSlider.wholeNumbers = false;
            starsSlider.value = 0f;
            starsSlider.interactable = false;
        }

        if (starsCounterText != null)
            starsCounterText.text = "—";

        if (playButton != null)
        {
            playButton.interactable = true;
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(OnPlayClicked);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isUnlocked) return;
        OnPlayClicked();
    }

    private void OnPlayClicked()
    {
        // Tutorial path
        if (isTutorialCard)
        {
            tutorialClickAction?.Invoke();
            return;
        }

        // Normal campaign path
        if (def == null) return;
        if (!isUnlocked) return;

        GameMultiplayerManager.playMultiplayer = false;
        CampaignRuntimeContext.IsCampaignRun = true;
        CampaignRuntimeContext.SelectedLevelId = def.levelId;
        TutorialRuntimeContext.Clear();

        var session = EnsureSessionCoordinator();
        if (session == null)
        {
            Debug.LogError("[Campaign] SessionCoordinator missing and no prefab assigned.");
            return;
        }

        session.StartSingleplayerHostLocal(def.scene);
    }

    private SessionCoordinator EnsureSessionCoordinator()
    {
        if (SessionCoordinator.Instance != null)
            return SessionCoordinator.Instance;

        if (sessionCoordinatorPrefab == null)
            return null;

        return Instantiate(sessionCoordinatorPrefab);
    }

    private void EnsurePreviewImageBindings(bool logWarnings)
    {
        lockedPreviewImage = ResolvePreviewImage(lockedRoot, lockedPreviewImage, "lockedPreviewImage", logWarnings);
        unlockedPreviewImage = ResolvePreviewImage(unlockedRoot, unlockedPreviewImage, "unlockedPreviewImage", logWarnings);
    }

    private Image ResolvePreviewImage(GameObject expectedRoot, Image assignedImage, string fieldName, bool logWarnings)
    {
        if (expectedRoot == null)
            return assignedImage;

        if (assignedImage != null && assignedImage.transform.IsChildOf(expectedRoot.transform))
            return assignedImage;

        Image resolved = expectedRoot.GetComponent<Image>();
        if (resolved == null)
            resolved = expectedRoot.GetComponentInChildren<Image>(true);

        if (resolved != null)
        {
            if (logWarnings && assignedImage != null)
            {
                Debug.LogWarning(
                    $"[CampaignLevelButtonUI] '{fieldName}' was wired to '{assignedImage.name}', but it must be under '{expectedRoot.name}'. Auto-fixed to '{resolved.name}'.",
                    this);
            }

            return resolved;
        }

        if (logWarnings)
        {
            Debug.LogWarning(
                $"[CampaignLevelButtonUI] Could not resolve '{fieldName}' under '{expectedRoot.name}'. Assign an Image in the inspector.",
                this);
        }

        return assignedImage;
    }
}