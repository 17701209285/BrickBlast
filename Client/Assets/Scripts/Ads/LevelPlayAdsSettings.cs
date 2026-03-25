using UnityEngine;

[CreateAssetMenu(fileName = "LevelPlayAdsSettings", menuName = "BrickBlast/Ads/LevelPlay Ads Settings")]
public sealed class LevelPlayAdsSettings : ScriptableObject
{
    [Header("Runtime")]
    [SerializeField] private bool autoCreateRuntimeObject = true;
    [SerializeField] private string bootstrapSceneName = "Launch";
    [SerializeField] private string runtimeObjectName = "Ads Runtime";

    [Header("Startup")]
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool validateIntegrationBeforeInit = true;
    [SerializeField] private bool enableAdaptersDebug = true;
    [SerializeField] private bool verboseLifecycleLogs = true;
    [SerializeField] private bool launchTestSuiteAfterInit;

    [Header("SDK")]
    [SerializeField] private string androidAppKey;
    [SerializeField] private string iosAppKey;
    [SerializeField] private string userId;

    [Header("Rewarded")]
    [SerializeField] private string rewardedAdUnitId;
    [SerializeField] private string rewardedPlacementName;
    [SerializeField] private bool autoLoadRewarded = true;

    [Header("Interstitial")]
    [SerializeField] private string interstitialAdUnitId;
    [SerializeField] private string interstitialPlacementName;
    [SerializeField] private bool autoLoadInterstitial = true;

    [Header("Banner")]
    [SerializeField] private string bannerAdUnitId;
    [SerializeField] private string bannerPlacementName;
    [SerializeField] private bool autoLoadBanner;
    [SerializeField] private bool autoShowBanner;
    [SerializeField] private bool bannerRespectSafeArea = true;
    [SerializeField] private bool bannerUseAdaptiveSize = true;
    [SerializeField] private bool bannerAtBottom = true;

    public bool AutoCreateRuntimeObject => autoCreateRuntimeObject;
    public string BootstrapSceneName => bootstrapSceneName;
    public string RuntimeObjectName => string.IsNullOrWhiteSpace(runtimeObjectName) ? "Ads Runtime" : runtimeObjectName;
    public bool InitializeOnAwake => initializeOnAwake;
    public bool PersistAcrossScenes => persistAcrossScenes;
    public bool ValidateIntegrationBeforeInit => validateIntegrationBeforeInit;
    public bool EnableAdaptersDebug => enableAdaptersDebug;
    public bool VerboseLifecycleLogs => verboseLifecycleLogs;
    public bool LaunchTestSuiteAfterInit => launchTestSuiteAfterInit;
    public string AndroidAppKey => androidAppKey;
    public string IOSAppKey => iosAppKey;
    public string UserId => userId;
    public string RewardedAdUnitId => rewardedAdUnitId;
    public string RewardedPlacementName => rewardedPlacementName;
    public bool AutoLoadRewarded => autoLoadRewarded;
    public string InterstitialAdUnitId => interstitialAdUnitId;
    public string InterstitialPlacementName => interstitialPlacementName;
    public bool AutoLoadInterstitial => autoLoadInterstitial;
    public string BannerAdUnitId => bannerAdUnitId;
    public string BannerPlacementName => bannerPlacementName;
    public bool AutoLoadBanner => autoLoadBanner;
    public bool AutoShowBanner => autoShowBanner;
    public bool BannerRespectSafeArea => bannerRespectSafeArea;
    public bool BannerUseAdaptiveSize => bannerUseAdaptiveSize;
    public bool BannerAtBottom => bannerAtBottom;

    public bool ShouldBootstrapScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(bootstrapSceneName))
        {
            return true;
        }

        return string.Equals(bootstrapSceneName, sceneName, System.StringComparison.Ordinal);
    }
}
