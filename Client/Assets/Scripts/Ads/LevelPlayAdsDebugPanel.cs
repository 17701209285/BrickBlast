using UnityEngine;
using UnityEngine.InputSystem;

public sealed class LevelPlayAdsDebugPanel : MonoBehaviour
{
    [SerializeField] private Key ToggleKey = Key.F8;
    [SerializeField] private bool StartHidden;

    private const float WindowWidth = 320f;
    private const float WindowHeight = 300f;

    private Rect windowRect = new Rect(20f, 20f, WindowWidth, WindowHeight);
    private bool isVisible = true;
    private bool bannerVisible;

    private void Awake()
    {
        isVisible = !StartHidden;
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && ToggleKey != Key.None && keyboard[ToggleKey].wasPressedThisFrame)
        {
            isVisible = !isVisible;
        }
    }

    private void OnGUI()
    {
        if (!isVisible)
        {
            return;
        }

        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Ads Debug");
    }

    private void DrawWindow(int windowId)
    {
        LevelPlayAdsManager ads = LevelPlayAdsManager.Instance;

        GUILayout.BeginVertical();
        GUILayout.Label($"SDK: {GetStateText(ads != null && ads.IsInitialized, ads == null ? "Missing" : "Ready", "Waiting")}");
        GUILayout.Label($"Rewarded: {GetStateText(ads != null && ads.IsRewardedReady, "Ready", "Not Ready")}");
        GUILayout.Label($"Interstitial: {GetStateText(ads != null && ads.IsInterstitialReady, "Ready", "Not Ready")}");
        GUILayout.Space(8f);

        if (GUILayout.Button("Init SDK"))
        {
            ads?.InitializeSdk();
        }

        if (GUILayout.Button("Load Rewarded"))
        {
            ads?.LoadRewardedAd();
        }

        GUI.enabled = ads != null && ads.IsRewardedReady;
        if (GUILayout.Button("Show Rewarded"))
        {
            ads.ShowRewardedAd();
        }

        GUI.enabled = ads != null;
        if (GUILayout.Button("Load Interstitial"))
        {
            ads.LoadInterstitialAd();
        }

        GUI.enabled = ads != null && ads.IsInterstitialReady;
        if (GUILayout.Button("Show Interstitial"))
        {
            ads.ShowInterstitialAd();
        }

        GUI.enabled = ads != null;
        if (GUILayout.Button("Load Banner"))
        {
            ads.LoadBannerAd();
        }

        if (GUILayout.Button(bannerVisible ? "Hide Banner" : "Show Banner"))
        {
            bannerVisible = !bannerVisible;
            if (bannerVisible)
            {
                ads.ShowBannerAd();
            }
            else
            {
                ads.HideBannerAd();
            }
        }

        if (GUILayout.Button("Launch Test Suite"))
        {
            ads.LaunchTestSuite();
        }

        GUI.enabled = true;
        GUILayout.Space(6f);
        GUILayout.Label($"Toggle: {ToggleKey}");

        if (GUILayout.Button("Hide Panel"))
        {
            isVisible = false;
        }

        GUILayout.EndVertical();
        GUI.DragWindow(new Rect(0f, 0f, WindowWidth, 24f));
    }

    private static string GetStateText(bool state, string onText, string offText)
    {
        return state ? onText : offText;
    }
}
