using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

[CreateAssetMenu(fileName = "YooAssetBootstrapSettings", menuName = "BrickBlast/YooAsset Bootstrap Settings")]
public sealed class YooAssetBootstrapSettings : ScriptableObject
{
    [Header("Bootstrap")]
    [SerializeField] private string bootstrapSceneName = "Launch";
    [SerializeField] private string startupEntryId = "brick-blast";
    [Tooltip("Address of the hot-update UI config asset loaded through YooAsset.")]
    [SerializeField] private string uiSettingsAddress = "Assets/AssetBundle/YooAssetBootstrap/YooAssetUiSettings.asset";

    [Header("Package")]
    [SerializeField] private string packageName = "DefaultPackage";
    [SerializeField] private EPlayMode editorPlayMode = EPlayMode.EditorSimulateMode;
    [SerializeField] private EPlayMode playerPlayMode = EPlayMode.OfflinePlayMode;
    [SerializeField] private int requestTimeoutSeconds = 60;
    [SerializeField] private int downloaderMaxConcurrency = 10;
    [SerializeField] private int failedRetryCount = 3;
    [SerializeField] private bool autoDownloadRemoteUpdates = true;
    [SerializeField] private bool appendTimeTicksToVersionRequest = true;

    [Header("Remote")]
    [SerializeField] private string hostServerRoot = "http://127.0.0.1/CDN";
    [SerializeField] private string fallbackHostServerRoot = string.Empty;
    [SerializeField] private bool appendPlatformToHostServer = true;
    [SerializeField] private bool appendVersionToHostServer = true;
    [SerializeField] private string hostServerVersion = "v0.1.0";

    [Header("Entries")]
    [SerializeField] private List<YooAssetMiniGameEntry> gameEntries = new List<YooAssetMiniGameEntry>();

    public string BootstrapSceneName
    {
        get { return bootstrapSceneName; }
    }

    public string StartupEntryId
    {
        get { return startupEntryId; }
    }

    public string UiSettingsAddress
    {
        get { return uiSettingsAddress; }
    }

    public string PackageName
    {
        get { return packageName; }
    }

    public int RequestTimeoutSeconds
    {
        get { return Mathf.Max(1, requestTimeoutSeconds); }
    }

    public int DownloaderMaxConcurrency
    {
        get { return Mathf.Max(1, downloaderMaxConcurrency); }
    }

    public int FailedRetryCount
    {
        get { return Mathf.Max(0, failedRetryCount); }
    }

    public bool AutoDownloadRemoteUpdates
    {
        get { return autoDownloadRemoteUpdates; }
    }

    public bool AppendTimeTicksToVersionRequest
    {
        get { return appendTimeTicksToVersionRequest; }
    }

    public EPlayMode ResolvePlayMode()
    {
        return Application.isEditor ? editorPlayMode : playerPlayMode;
    }

    public bool ShouldBootstrap(Scene scene)
    {
        if (string.IsNullOrWhiteSpace(bootstrapSceneName))
        {
            return true;
        }

        return string.Equals(scene.name, bootstrapSceneName, StringComparison.Ordinal);
    }

    public bool TryGetEntry(string entryId, out YooAssetMiniGameEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entryId))
        {
            for (int i = 0; i < gameEntries.Count; i++)
            {
                YooAssetMiniGameEntry current = gameEntries[i];
                if (current != null && string.Equals(current.EntryId, entryId, StringComparison.OrdinalIgnoreCase))
                {
                    entry = current;
                    return true;
                }
            }
        }

        for (int i = 0; i < gameEntries.Count; i++)
        {
            if (gameEntries[i] != null)
            {
                entry = gameEntries[i];
                return true;
            }
        }

        entry = null;
        return false;
    }

    public string GetPrimaryHostServerUrl()
    {
        return BuildHostServerUrl(hostServerRoot);
    }

    public string GetFallbackHostServerUrl()
    {
        string root = string.IsNullOrWhiteSpace(fallbackHostServerRoot) ? hostServerRoot : fallbackHostServerRoot;
        return BuildHostServerUrl(root);
    }

    private string BuildHostServerUrl(string root)
    {
        string value = string.IsNullOrWhiteSpace(root) ? string.Empty : root.TrimEnd('/');
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (appendPlatformToHostServer)
        {
            value = value + "/" + GetPlatformFolder();
        }

        if (appendVersionToHostServer && string.IsNullOrWhiteSpace(hostServerVersion) == false)
        {
            value = value + "/" + hostServerVersion.Trim('/');
        }

        return value;
    }

    private static string GetPlatformFolder()
    {
#if UNITY_EDITOR
        switch (UnityEditor.EditorUserBuildSettings.activeBuildTarget)
        {
            case UnityEditor.BuildTarget.Android:
                return "Android";
            case UnityEditor.BuildTarget.iOS:
                return "IPhone";
            case UnityEditor.BuildTarget.WebGL:
                return "WebGL";
            default:
                return "PC";
        }
#else
        switch (Application.platform)
        {
            case RuntimePlatform.Android:
                return "Android";
            case RuntimePlatform.IPhonePlayer:
                return "IPhone";
            case RuntimePlatform.WebGLPlayer:
                return "WebGL";
            default:
                return "PC";
        }
#endif
    }
}

[Serializable]
public sealed class YooAssetMiniGameEntry
{
    [SerializeField] private string entryId = "brick-blast";
    [SerializeField] private string displayName = "Brick Blast";
    [SerializeField] private YooAssetMiniGameEntryLoadMode loadMode = YooAssetMiniGameEntryLoadMode.Scene;
    [Tooltip("When addressable is disabled, use the asset path or extensionless asset path.")]
    [SerializeField] private string address = "Assets/AssetBundle/Scenes/Main";
    [SerializeField] private LoadSceneMode sceneMode = LoadSceneMode.Single;

    public string EntryId
    {
        get { return entryId; }
    }

    public string DisplayName
    {
        get { return string.IsNullOrWhiteSpace(displayName) ? entryId : displayName; }
    }

    public YooAssetMiniGameEntryLoadMode LoadMode
    {
        get { return loadMode; }
    }

    public string Address
    {
        get { return address; }
    }

    public LoadSceneMode SceneMode
    {
        get { return sceneMode; }
    }
}

public enum YooAssetMiniGameEntryLoadMode
{
    Scene = 0,
    Prefab = 1,
}
