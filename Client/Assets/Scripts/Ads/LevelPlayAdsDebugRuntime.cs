using UnityEngine;

public static class LevelPlayAdsDebugRuntime
{
#if UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TryCreateDebugPanel()
    {
        if (Object.FindFirstObjectByType<LevelPlayAdsDebugPanel>() != null)
        {
            return;
        }

        GameObject debugPanel = new GameObject("Ads Debug Panel");
        Object.DontDestroyOnLoad(debugPanel);
        debugPanel.AddComponent<LevelPlayAdsDebugPanel>();
    }
#endif
}
