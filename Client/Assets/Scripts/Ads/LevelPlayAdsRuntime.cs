using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelPlayAdsRuntime
{
    private const string SettingsResourcePath = "LevelPlay/LevelPlayAdsSettings";

    private static bool runtimeCreated;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        runtimeCreated = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TryCreateRuntime()
    {
        if (runtimeCreated || LevelPlayAdsManager.Instance != null)
        {
            return;
        }

        LevelPlayAdsSettings settings = Resources.Load<LevelPlayAdsSettings>(SettingsResourcePath);
        if (settings == null || settings.AutoCreateRuntimeObject == false)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!settings.ShouldBootstrapScene(activeScene.name))
        {
            return;
        }

        GameObject runtimeObject = new GameObject(settings.RuntimeObjectName);
        Object.DontDestroyOnLoad(runtimeObject);
        runtimeObject.AddComponent<LevelPlayAdsManager>();
        runtimeCreated = true;
    }
}
