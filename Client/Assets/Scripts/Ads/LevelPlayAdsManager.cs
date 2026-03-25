using System;
using Unity.Services.LevelPlay;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LevelPlayAdsManager : MonoBehaviour
{
    private const string SettingsResourcePath = "LevelPlay/LevelPlayAdsSettings";
    private const string EditorRewardedFallbackAdUnitId = "editor_rewarded";
    private const string EditorInterstitialFallbackAdUnitId = "editor_interstitial";
    private const string EditorBannerFallbackAdUnitId = "editor_banner";

    [Header("Startup")]
    [SerializeField] private bool InitializeOnAwake = true;
    [SerializeField] private bool PersistAcrossScenes = true;
    [SerializeField] private bool ValidateIntegrationBeforeInit = true;
    [SerializeField] private bool EnableAdaptersDebug = true;
    [SerializeField] private bool VerboseLifecycleLogs = true;
    [SerializeField] private bool LaunchTestSuiteAfterInit;

    [Header("SDK")]
    [SerializeField] private string AndroidAppKey;
    [SerializeField] private string IOSAppKey;
    [SerializeField] private string UserId;

    [Header("Rewarded")]
    [SerializeField] private string RewardedAdUnitId;
    [SerializeField] private string RewardedPlacementName;
    [SerializeField] private bool AutoLoadRewarded = true;

    [Header("Interstitial")]
    [SerializeField] private string InterstitialAdUnitId;
    [SerializeField] private string InterstitialPlacementName;
    [SerializeField] private bool AutoLoadInterstitial = true;

    [Header("Banner")]
    [SerializeField] private string BannerAdUnitId;
    [SerializeField] private string BannerPlacementName;
    [SerializeField] private bool AutoLoadBanner;
    [SerializeField] private bool AutoShowBanner;
    [SerializeField] private bool BannerRespectSafeArea = true;
    [SerializeField] private bool BannerUseAdaptiveSize = true;
    [SerializeField] private bool BannerAtBottom = true;

    private LevelPlayRewardedAd rewardedAd;
    private LevelPlayInterstitialAd interstitialAd;
    private LevelPlayBannerAd bannerAd;
    private bool initializationRequested;
    private bool callbacksRegistered;
    private bool settingsApplied;

    public static LevelPlayAdsManager Instance { get; private set; }

    public bool IsInitialized { get; private set; }
    public bool IsRewardedReady => rewardedAd != null && rewardedAd.IsAdReady();
    public bool IsInterstitialReady => interstitialAd != null && interstitialAd.IsAdReady();
    public bool HasBannerInstance => bannerAd != null;

    public event Action Initialized;
    public event Action<string> InitializationFailed;
    public event Action RewardedLoaded;
    public event Action RewardedClosed;
    public event Action RewardedCompleted;
    public event Action InterstitialLoaded;
    public event Action InterstitialClosed;
    public event Action BannerLoaded;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        TryApplySettingsFromResources();

        if (PersistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (InitializeOnAwake)
        {
            InitializeSdk();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnregisterSdkCallbacks();
        DisposeAds();
    }

    [ContextMenu("Initialize SDK")]
    public void InitializeSdk()
    {
        if (IsInitialized || initializationRequested)
        {
            return;
        }

        if (!SupportsCurrentBuildTarget())
        {
            Debug.LogWarning("[LevelPlayAdsManager] LevelPlay only runs on iOS/Android. In Editor, switch Build Target to iOS or Android to test mock ads.", this);
            return;
        }

        string appKey = GetResolvedAppKey();
        if (string.IsNullOrWhiteSpace(appKey))
        {
            Debug.LogError("[LevelPlayAdsManager] Missing LevelPlay App Key. Fill AndroidAppKey or IOSAppKey before initializing.", this);
            return;
        }

        RegisterSdkCallbacks();
        initializationRequested = true;

        if (ValidateIntegrationBeforeInit)
        {
            LevelPlay.ValidateIntegration();
        }

        LevelPlay.SetAdaptersDebug(EnableAdaptersDebug);

        if (LaunchTestSuiteAfterInit)
        {
            LevelPlay.SetMetaData("is_test_suite", "enable");
        }

        if (string.IsNullOrWhiteSpace(UserId))
        {
            LevelPlay.Init(appKey);
        }
        else
        {
            LevelPlay.Init(appKey, UserId);
        }

        Log($"Init requested. AppKey={Mask(appKey)}");
    }

    [ContextMenu("Load Rewarded")]
    public void LoadRewardedAd()
    {
        if (!EnsureRewardedAd())
        {
            return;
        }

        rewardedAd.LoadAd();
        Log("Rewarded load requested.");
    }

    [ContextMenu("Show Rewarded")]
    public bool ShowRewardedAd()
    {
        if (!EnsureRewardedAd())
        {
            return false;
        }

        if (!rewardedAd.IsAdReady())
        {
            Debug.LogWarning("[LevelPlayAdsManager] Rewarded is not ready yet. Call LoadRewardedAd and wait for OnAdLoaded.", this);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(RewardedPlacementName) &&
            LevelPlayRewardedAd.IsPlacementCapped(RewardedPlacementName))
        {
            Debug.LogWarning($"[LevelPlayAdsManager] Rewarded placement is capped: {RewardedPlacementName}", this);
            return false;
        }

        rewardedAd.ShowAd(EmptyToNull(RewardedPlacementName));
        Log($"Rewarded show requested. Placement={SafeValue(RewardedPlacementName)}");
        return true;
    }

    [ContextMenu("Load Interstitial")]
    public void LoadInterstitialAd()
    {
        if (!EnsureInterstitialAd())
        {
            return;
        }

        interstitialAd.LoadAd();
        Log("Interstitial load requested.");
    }

    [ContextMenu("Show Interstitial")]
    public bool ShowInterstitialAd()
    {
        if (!EnsureInterstitialAd())
        {
            return false;
        }

        if (!interstitialAd.IsAdReady())
        {
            Debug.LogWarning("[LevelPlayAdsManager] Interstitial is not ready yet. Call LoadInterstitialAd and wait for OnAdLoaded.", this);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(InterstitialPlacementName) &&
            LevelPlayInterstitialAd.IsPlacementCapped(InterstitialPlacementName))
        {
            Debug.LogWarning($"[LevelPlayAdsManager] Interstitial placement is capped: {InterstitialPlacementName}", this);
            return false;
        }

        interstitialAd.ShowAd(EmptyToNull(InterstitialPlacementName));
        Log($"Interstitial show requested. Placement={SafeValue(InterstitialPlacementName)}");
        return true;
    }

    [ContextMenu("Load Banner")]
    public void LoadBannerAd()
    {
        if (!EnsureBannerAd())
        {
            return;
        }

        bannerAd.LoadAd();
        Log("Banner load requested.");
    }

    [ContextMenu("Show Banner")]
    public void ShowBannerAd()
    {
        if (!EnsureBannerAd())
        {
            return;
        }

        bannerAd.ShowAd();
        Log("Banner show requested.");
    }

    [ContextMenu("Hide Banner")]
    public void HideBannerAd()
    {
        if (bannerAd == null)
        {
            return;
        }

        bannerAd.HideAd();
        Log("Banner hide requested.");
    }

    [ContextMenu("Launch Test Suite")]
    public void LaunchTestSuite()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[LevelPlayAdsManager] LaunchTestSuite requires LevelPlay to be initialized first.", this);
            return;
        }

        LevelPlay.LaunchTestSuite();
        Log("LevelPlay test suite launched.");
    }

    public void SetConsent(bool consent)
    {
        LevelPlay.SetConsent(consent);
    }

    private void HandleInitSuccess(LevelPlayConfiguration configuration)
    {
        initializationRequested = false;
        IsInitialized = true;

        Log($"Init success. Config={configuration}");

        EnsureRewardedAd();
        EnsureInterstitialAd();
        EnsureBannerAd();

        if (AutoLoadRewarded)
        {
            LoadRewardedAd();
        }

        if (AutoLoadInterstitial)
        {
            LoadInterstitialAd();
        }

        if (AutoLoadBanner)
        {
            LoadBannerAd();
        }

        if (LaunchTestSuiteAfterInit)
        {
            LevelPlay.LaunchTestSuite();
        }

        Initialized?.Invoke();
    }

    private void HandleInitFailed(LevelPlayInitError error)
    {
        initializationRequested = false;
        IsInitialized = false;

        string message = error == null ? "Unknown LevelPlay init error" : error.ToString();
        Debug.LogError($"[LevelPlayAdsManager] Init failed: {message}", this);
        InitializationFailed?.Invoke(message);
    }

    private bool EnsureRewardedAd()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[LevelPlayAdsManager] Rewarded ad requested before SDK initialization.", this);
            return false;
        }

        if (rewardedAd != null)
        {
            return true;
        }

        string adUnitId = GetResolvedRewardedAdUnitId();
        if (string.IsNullOrWhiteSpace(adUnitId))
        {
            Debug.LogWarning("[LevelPlayAdsManager] Missing RewardedAdUnitId.", this);
            return false;
        }

        rewardedAd = new LevelPlayRewardedAd(adUnitId);
        rewardedAd.OnAdLoaded += HandleRewardedLoaded;
        rewardedAd.OnAdLoadFailed += HandleRewardedLoadFailed;
        rewardedAd.OnAdDisplayed += HandleRewardedDisplayed;
        rewardedAd.OnAdDisplayFailed += HandleRewardedDisplayFailed;
        rewardedAd.OnAdRewarded += HandleRewardedRewarded;
        rewardedAd.OnAdClosed += HandleRewardedClosed;
        rewardedAd.OnAdClicked += HandleRewardedClicked;
        return true;
    }

    private bool EnsureInterstitialAd()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[LevelPlayAdsManager] Interstitial ad requested before SDK initialization.", this);
            return false;
        }

        if (interstitialAd != null)
        {
            return true;
        }

        string adUnitId = GetResolvedInterstitialAdUnitId();
        if (string.IsNullOrWhiteSpace(adUnitId))
        {
            Debug.LogWarning("[LevelPlayAdsManager] Missing InterstitialAdUnitId.", this);
            return false;
        }

        interstitialAd = new LevelPlayInterstitialAd(adUnitId);
        interstitialAd.OnAdLoaded += HandleInterstitialLoaded;
        interstitialAd.OnAdLoadFailed += HandleInterstitialLoadFailed;
        interstitialAd.OnAdDisplayed += HandleInterstitialDisplayed;
        interstitialAd.OnAdDisplayFailed += HandleInterstitialDisplayFailed;
        interstitialAd.OnAdClosed += HandleInterstitialClosed;
        interstitialAd.OnAdClicked += HandleInterstitialClicked;
        return true;
    }

    private bool EnsureBannerAd()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning("[LevelPlayAdsManager] Banner ad requested before SDK initialization.", this);
            return false;
        }

        if (bannerAd != null)
        {
            return true;
        }

        string adUnitId = GetResolvedBannerAdUnitId();
        if (string.IsNullOrWhiteSpace(adUnitId))
        {
            return false;
        }

        LevelPlayBannerAd.Config config = new LevelPlayBannerAd.Config.Builder()
            .SetSize(BannerUseAdaptiveSize ? LevelPlayAdSize.CreateAdaptiveAdSize() : LevelPlayAdSize.BANNER)
            .SetPosition(BannerAtBottom ? LevelPlayBannerPosition.BottomCenter : LevelPlayBannerPosition.TopCenter)
            .SetPlacementName(EmptyToNull(BannerPlacementName))
            .SetDisplayOnLoad(AutoShowBanner)
            .SetRespectSafeArea(BannerRespectSafeArea)
            .Build();

        bannerAd = new LevelPlayBannerAd(adUnitId, config);
        bannerAd.OnAdLoaded += HandleBannerLoaded;
        bannerAd.OnAdLoadFailed += HandleBannerLoadFailed;
        bannerAd.OnAdDisplayed += HandleBannerDisplayed;
        bannerAd.OnAdDisplayFailed += HandleBannerDisplayFailed;
        bannerAd.OnAdClicked += HandleBannerClicked;
        return true;
    }

    private void RegisterSdkCallbacks()
    {
        if (callbacksRegistered)
        {
            return;
        }

        LevelPlay.OnInitSuccess += HandleInitSuccess;
        LevelPlay.OnInitFailed += HandleInitFailed;
        callbacksRegistered = true;
    }

    private void UnregisterSdkCallbacks()
    {
        if (!callbacksRegistered)
        {
            return;
        }

        LevelPlay.OnInitSuccess -= HandleInitSuccess;
        LevelPlay.OnInitFailed -= HandleInitFailed;
        callbacksRegistered = false;
    }

    private void DisposeAds()
    {
        if (rewardedAd != null)
        {
            rewardedAd.OnAdLoaded -= HandleRewardedLoaded;
            rewardedAd.OnAdLoadFailed -= HandleRewardedLoadFailed;
            rewardedAd.OnAdDisplayed -= HandleRewardedDisplayed;
            rewardedAd.OnAdDisplayFailed -= HandleRewardedDisplayFailed;
            rewardedAd.OnAdRewarded -= HandleRewardedRewarded;
            rewardedAd.OnAdClosed -= HandleRewardedClosed;
            rewardedAd.OnAdClicked -= HandleRewardedClicked;
            rewardedAd.Dispose();
            rewardedAd = null;
        }

        if (interstitialAd != null)
        {
            interstitialAd.OnAdLoaded -= HandleInterstitialLoaded;
            interstitialAd.OnAdLoadFailed -= HandleInterstitialLoadFailed;
            interstitialAd.OnAdDisplayed -= HandleInterstitialDisplayed;
            interstitialAd.OnAdDisplayFailed -= HandleInterstitialDisplayFailed;
            interstitialAd.OnAdClosed -= HandleInterstitialClosed;
            interstitialAd.OnAdClicked -= HandleInterstitialClicked;
            interstitialAd.Dispose();
            interstitialAd = null;
        }

        if (bannerAd != null)
        {
            bannerAd.OnAdLoaded -= HandleBannerLoaded;
            bannerAd.OnAdLoadFailed -= HandleBannerLoadFailed;
            bannerAd.OnAdDisplayed -= HandleBannerDisplayed;
            bannerAd.OnAdDisplayFailed -= HandleBannerDisplayFailed;
            bannerAd.OnAdClicked -= HandleBannerClicked;
            bannerAd.Dispose();
            bannerAd = null;
        }
    }

    private void HandleRewardedLoaded(LevelPlayAdInfo adInfo)
    {
        Log($"Rewarded loaded. Info={adInfo}");
        RewardedLoaded?.Invoke();
    }

    private void HandleRewardedLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayAdsManager] Rewarded load failed: {error}", this);
    }

    private void HandleRewardedDisplayed(LevelPlayAdInfo adInfo)
    {
        Log($"Rewarded displayed. Info={adInfo}");
    }

    private void HandleRewardedDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayAdsManager] Rewarded display failed: {error}", this);
        if (AutoLoadRewarded && rewardedAd != null)
        {
            rewardedAd.LoadAd();
        }
    }

    private void HandleRewardedRewarded(LevelPlayAdInfo adInfo, LevelPlayReward reward)
    {
        Log($"Rewarded granted. Reward={reward}");
        RewardedCompleted?.Invoke();
    }

    private void HandleRewardedClosed(LevelPlayAdInfo adInfo)
    {
        Log("Rewarded closed.");
        RewardedClosed?.Invoke();
        if (AutoLoadRewarded && rewardedAd != null)
        {
            rewardedAd.LoadAd();
        }
    }

    private void HandleRewardedClicked(LevelPlayAdInfo adInfo)
    {
        Log("Rewarded clicked.");
    }

    private void HandleInterstitialLoaded(LevelPlayAdInfo adInfo)
    {
        Log($"Interstitial loaded. Info={adInfo}");
        InterstitialLoaded?.Invoke();
    }

    private void HandleInterstitialLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayAdsManager] Interstitial load failed: {error}", this);
    }

    private void HandleInterstitialDisplayed(LevelPlayAdInfo adInfo)
    {
        Log($"Interstitial displayed. Info={adInfo}");
    }

    private void HandleInterstitialDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayAdsManager] Interstitial display failed: {error}", this);
        if (AutoLoadInterstitial && interstitialAd != null)
        {
            interstitialAd.LoadAd();
        }
    }

    private void HandleInterstitialClosed(LevelPlayAdInfo adInfo)
    {
        Log("Interstitial closed.");
        InterstitialClosed?.Invoke();
        if (AutoLoadInterstitial && interstitialAd != null)
        {
            interstitialAd.LoadAd();
        }
    }

    private void HandleInterstitialClicked(LevelPlayAdInfo adInfo)
    {
        Log("Interstitial clicked.");
    }

    private void HandleBannerLoaded(LevelPlayAdInfo adInfo)
    {
        Log($"Banner loaded. Info={adInfo}");
        BannerLoaded?.Invoke();
    }

    private void HandleBannerLoadFailed(LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayAdsManager] Banner load failed: {error}", this);
    }

    private void HandleBannerDisplayed(LevelPlayAdInfo adInfo)
    {
        Log($"Banner displayed. Info={adInfo}");
    }

    private void HandleBannerDisplayFailed(LevelPlayAdInfo adInfo, LevelPlayAdError error)
    {
        Debug.LogWarning($"[LevelPlayAdsManager] Banner display failed: {error}", this);
    }

    private void HandleBannerClicked(LevelPlayAdInfo adInfo)
    {
        Log("Banner clicked.");
    }

    private void TryApplySettingsFromResources()
    {
        if (settingsApplied)
        {
            return;
        }

        LevelPlayAdsSettings settings = Resources.Load<LevelPlayAdsSettings>(SettingsResourcePath);
        if (settings == null)
        {
            return;
        }

        settingsApplied = true;
        InitializeOnAwake = settings.InitializeOnAwake;
        PersistAcrossScenes = settings.PersistAcrossScenes;
        ValidateIntegrationBeforeInit = settings.ValidateIntegrationBeforeInit;
        EnableAdaptersDebug = settings.EnableAdaptersDebug;
        VerboseLifecycleLogs = settings.VerboseLifecycleLogs;
        LaunchTestSuiteAfterInit = settings.LaunchTestSuiteAfterInit;
        AndroidAppKey = settings.AndroidAppKey;
        IOSAppKey = settings.IOSAppKey;
        UserId = settings.UserId;
        RewardedAdUnitId = settings.RewardedAdUnitId;
        RewardedPlacementName = settings.RewardedPlacementName;
        AutoLoadRewarded = settings.AutoLoadRewarded;
        InterstitialAdUnitId = settings.InterstitialAdUnitId;
        InterstitialPlacementName = settings.InterstitialPlacementName;
        AutoLoadInterstitial = settings.AutoLoadInterstitial;
        BannerAdUnitId = settings.BannerAdUnitId;
        BannerPlacementName = settings.BannerPlacementName;
        AutoLoadBanner = settings.AutoLoadBanner;
        AutoShowBanner = settings.AutoShowBanner;
        BannerRespectSafeArea = settings.BannerRespectSafeArea;
        BannerUseAdaptiveSize = settings.BannerUseAdaptiveSize;
        BannerAtBottom = settings.BannerAtBottom;
    }

    private string GetResolvedAppKey()
    {
#if UNITY_IOS
        return IOSAppKey;
#elif UNITY_ANDROID
        return AndroidAppKey;
#else
        return string.IsNullOrWhiteSpace(AndroidAppKey) ? IOSAppKey : AndroidAppKey;
#endif
    }

    private string GetResolvedRewardedAdUnitId()
    {
        if (!string.IsNullOrWhiteSpace(RewardedAdUnitId))
        {
            return RewardedAdUnitId;
        }

#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        return EditorRewardedFallbackAdUnitId;
#else
        return RewardedAdUnitId;
#endif
    }

    private string GetResolvedInterstitialAdUnitId()
    {
        if (!string.IsNullOrWhiteSpace(InterstitialAdUnitId))
        {
            return InterstitialAdUnitId;
        }

#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        return EditorInterstitialFallbackAdUnitId;
#else
        return InterstitialAdUnitId;
#endif
    }

    private string GetResolvedBannerAdUnitId()
    {
        if (!string.IsNullOrWhiteSpace(BannerAdUnitId))
        {
            return BannerAdUnitId;
        }

#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
        return AutoLoadBanner ? EditorBannerFallbackAdUnitId : string.Empty;
#else
        return BannerAdUnitId;
#endif
    }

    private bool SupportsCurrentBuildTarget()
    {
#if UNITY_ANDROID || UNITY_IOS
        return true;
#else
        return false;
#endif
    }

    private void Log(string message)
    {
        if (!VerboseLifecycleLogs)
        {
            return;
        }

        Debug.Log($"[LevelPlayAdsManager] {message}", this);
    }

    private static string EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<default>" : value;
    }

    private static string Mask(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= 6)
        {
            return value;
        }

        return $"{value.Substring(0, 3)}***{value.Substring(value.Length - 3)}";
    }
}
