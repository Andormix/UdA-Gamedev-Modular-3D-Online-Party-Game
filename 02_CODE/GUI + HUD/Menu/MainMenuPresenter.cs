using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class MainMenuPresenter : MonoBehaviour
{
    [Header("View - Navigation")]
    [SerializeField] private Button multiplayerPlayButton;
    [SerializeField] private Button singleplayerPlayButton;
    [SerializeField] private Button exitButton;
    
    [Header("View - Customization")]
    [SerializeField] private Button skinsButton;
    [SerializeField] private Button returnMainMenuButton1; 
    [SerializeField] private Button returnMainMenuButton2; 

    [Header("View - Wallet (TMP)")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text diamondsText;

    [Header("Scene dependencies")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private UserInterfaceManager ui;
    [SerializeField] private TabMenuExtendedController tabController;

    [Header("Customization Preview")]
    [SerializeField] private Transform previewTarget;

    [Header("Player Animator")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string skinsTabBoolName = "isSkinsTab";

    [Header("Credits")]
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button returnMainMenuButtonCredits;

    [Header("Graphics quality (optional)")]
    [SerializeField] private Button lowGraphicsButton;
    [SerializeField] private Button highGraphicsButton;
    [SerializeField] private TMP_Dropdown graphicsQualityDropdown;

    private int _skinsTabBoolHash;

    private void Awake()
    {
        Time.timeScale = 1f;
        _skinsTabBoolHash = Animator.StringToHash(skinsTabBoolName);

        if (multiplayerPlayButton == null || skinsButton == null || ui == null)
        {
            Debug.LogError($"{nameof(MainMenuPresenter)} missing references.", this);
            enabled = false;
            return;
        }

        SetSkinsTabAnim(false);
    }

    private void OnEnable()
    {
        multiplayerPlayButton.onClick.AddListener(OnMultiplayerPlayClicked);
        singleplayerPlayButton.onClick.AddListener(OnSingleplayerPlayClicked);
        skinsButton.onClick.AddListener(OnSkinsClicked);
        if (exitButton != null) exitButton.onClick.AddListener(OnExitClicked);
        if (returnMainMenuButton1 != null) returnMainMenuButton1.onClick.AddListener(OnReturnClicked);
        if (returnMainMenuButton2 != null) returnMainMenuButton2.onClick.AddListener(OnReturnClicked);
        if (creditsButton != null) creditsButton.onClick.AddListener(OnCreditsClicked);
        if (returnMainMenuButtonCredits != null) returnMainMenuButtonCredits.onClick.AddListener(OnReturnClicked);

        if (lowGraphicsButton != null)
            lowGraphicsButton.onClick.AddListener(() => GraphicsQualityBootstrap.ApplyQuality(GraphicsQualityBootstrap.QualityLow));
        if (highGraphicsButton != null)
            highGraphicsButton.onClick.AddListener(() => GraphicsQualityBootstrap.ApplyQuality(GraphicsQualityBootstrap.QualityPc));

        if (graphicsQualityDropdown != null)
        {
            graphicsQualityDropdown.ClearOptions();
            graphicsQualityDropdown.AddOptions(new System.Collections.Generic.List<string> { "Low (Performance)", "PC (High)" });
            graphicsQualityDropdown.SetValueWithoutNotify(GraphicsQualityBootstrap.ResolveInitialQuality());
            graphicsQualityDropdown.onValueChanged.AddListener(index => GraphicsQualityBootstrap.ApplyQuality(index));
        }

        // Wallet events
        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
            EconomyManager.Instance.OnDiamondsChanged += HandleDiamondsChanged;
        }

        RefreshWalletUI();
    }

    private void OnDisable()
    {
        multiplayerPlayButton.onClick.RemoveListener(OnMultiplayerPlayClicked);
        singleplayerPlayButton.onClick.RemoveListener(OnSingleplayerPlayClicked);
        skinsButton.onClick.RemoveListener(OnSkinsClicked);
        if (exitButton != null) exitButton.onClick.RemoveListener(OnExitClicked);
        if (returnMainMenuButton1 != null) returnMainMenuButton1.onClick.RemoveListener(OnReturnClicked);
        if (returnMainMenuButton2 != null) returnMainMenuButton2.onClick.RemoveListener(OnReturnClicked);
        if (creditsButton != null) creditsButton.onClick.RemoveListener(OnCreditsClicked);
        if (returnMainMenuButtonCredits != null) returnMainMenuButtonCredits.onClick.RemoveListener(OnReturnClicked);
        if (lowGraphicsButton != null) lowGraphicsButton.onClick.RemoveAllListeners();
        if (highGraphicsButton != null) highGraphicsButton.onClick.RemoveAllListeners();
        if (graphicsQualityDropdown != null) graphicsQualityDropdown.onValueChanged.RemoveAllListeners();

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
            EconomyManager.Instance.OnDiamondsChanged -= HandleDiamondsChanged;
        }
    }

    private void Start()
    {
        RefreshWalletUI();
    }

    private void HandleCoinsChanged(int _) => RefreshWalletUI();
    private void HandleDiamondsChanged(int _) => RefreshWalletUI();

    private void RefreshWalletUI()
    {
        if (EconomyManager.Instance == null)
        {
            if (coinsText != null) coinsText.text = "0";
            if (diamondsText != null) diamondsText.text = "0";
            return;
        }

        if (coinsText != null)
            coinsText.text = EconomyManager.Instance.Coins.ToString("N0");      // 9,999,999

        if (diamondsText != null)
            diamondsText.text = EconomyManager.Instance.Diamonds.ToString("N0"); // 9,999,999
    }

    private void OnSingleplayerPlayClicked()
    {
        SetSkinsTabAnim(false);
        commonResetOperations();

        GameMultiplayerManager.playMultiplayer = false;
        CampaignRuntimeContext.Clear();
        CoopCampaignSessionContext.Clear();
        TutorialRuntimeContext.Clear();
        ui.ShowOnly(UserInterfaceManager.UIPage.Campaign);
    }

    private void OnMultiplayerPlayClicked()
    {
        SetSkinsTabAnim(false);
        commonResetOperations();
        CampaignRuntimeContext.Clear();
        CoopCampaignSessionContext.Clear();
        TutorialRuntimeContext.Clear();
        GameMultiplayerManager.playMultiplayer = true;
        ui.ShowOnly(UserInterfaceManager.UIPage.LobbyOnline);
    }

    private void OnExitClicked()
    {
        SetSkinsTabAnim(false);
        Loader.Load(Loader.Scene.StartScene);
    }

    private void OnSkinsClicked()
    {
        SetSkinsTabAnim(true);

        if (cameraManager != null) cameraManager.SwitchToCam2();

        if (previewTarget != null)
        {
            var anchor = previewTarget.GetComponent<PlayerTransformAnchor>();
            if (anchor != null) anchor.SnapToCustomization();
        }

        ui.ShowOnly(UserInterfaceManager.UIPage.CustomizationMenu);
    }

    private void OnReturnClicked()
    {
        SetSkinsTabAnim(false);
        commonResetOperations();

        if (ui != null) ui.ShowOnly(UserInterfaceManager.UIPage.MainMenu);
    }

    private void commonResetOperations()
    {
        if (cameraManager != null) cameraManager.SwitchToCam1();

        if (previewTarget != null)
        {
            var anchor = previewTarget.GetComponent<PlayerTransformAnchor>();
            if (anchor != null) anchor.SnapToMenu();
        }

        if (tabController != null) tabController.ResetToDefault();
    }

    private void SetSkinsTabAnim(bool value)
    {
        if (playerAnimator != null)
            playerAnimator.SetBool(_skinsTabBoolHash, value);
    }

    private void OnCreditsClicked()
    {
        SetSkinsTabAnim(false);
        commonResetOperations();
        if (ui != null) ui.ShowOnly(UserInterfaceManager.UIPage.Credits);
    }

    public void OnCoopCampaignButtonClicked()
    {
        SetSkinsTabAnim(false);
        commonResetOperations();
        if (tabController != null) tabController.ResetToMulty();
        CampaignRuntimeContext.Clear();
        CoopCampaignSessionContext.Clear();
        TutorialRuntimeContext.Clear();
        GameMultiplayerManager.playMultiplayer = true;
        ui.ShowOnly(UserInterfaceManager.UIPage.LobbyOnline);
    }

    
}