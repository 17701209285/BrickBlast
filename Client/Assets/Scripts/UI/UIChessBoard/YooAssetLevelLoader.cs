using System;
using System.Collections;
using UnityEngine;
using YooAsset;

[DisallowMultipleComponent]
public class YooAssetLevelLoader : MonoBehaviour
{
    [SerializeField]
    private UIChessBoard ChessBoard;

    [SerializeField]
    private string LevelConfigAddressPattern = "Assets/AssetBundle/LevelConfig/LevelConfig1_{0}.asset";

    private AssetHandle currentLevelHandle;

    public bool IsLoading { get; private set; }

    private void Awake()
    {
        if (ChessBoard == null)
        {
            ChessBoard = GetComponent<UIChessBoard>();
        }
    }

    private void OnDestroy()
    {
        ReleaseHandle(ref currentLevelHandle);
    }

    public bool CanLoadNextLevel()
    {
        return TryGetNextLevelAddress(out _);
    }

    public void LoadNextLevel(Action<bool> onCompleted = null)
    {
        if (!TryGetNextLevelAddress(out var levelAddress))
        {
            onCompleted?.Invoke(false);
            return;
        }

        LoadLevelByAddress(levelAddress, onCompleted);
    }

    public void ReloadCurrentLevel(Action<bool> onCompleted = null)
    {
        var currentLevelAddress = ResolveCurrentLevelAddress();
        if (string.IsNullOrWhiteSpace(currentLevelAddress))
        {
            onCompleted?.Invoke(false);
            return;
        }

        LoadLevelByAddress(currentLevelAddress, onCompleted);
    }

    public void LoadLevelByAddress(string levelAddress, Action<bool> onCompleted = null)
    {
        if (IsLoading)
        {
            onCompleted?.Invoke(false);
            return;
        }

        StartCoroutine(LoadLevelRoutine(levelAddress, onCompleted));
    }

    private IEnumerator LoadLevelRoutine(string levelAddress, Action<bool> onCompleted)
    {
        if (string.IsNullOrWhiteSpace(levelAddress))
        {
            onCompleted?.Invoke(false);
            yield break;
        }

        var package = ResolveResourcePackage();
        if (package == null)
        {
            Debug.LogError("[YooAssetLevelLoader] YooAsset package is not ready.", this);
            onCompleted?.Invoke(false);
            yield break;
        }

        if (!TryResolveValidLocation(package, levelAddress, out var resolvedLevelAddress))
        {
            Debug.LogError("[YooAssetLevelLoader] Failed to resolve valid level config location: " + levelAddress, this);
            onCompleted?.Invoke(false);
            yield break;
        }

        IsLoading = true;
        var handle = package.LoadAssetAsync<LevelConfigScritable>(resolvedLevelAddress);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeed)
        {
            Debug.LogError("[YooAssetLevelLoader] Failed to load level config: " + handle.LastError, this);
            if (handle.IsValid)
            {
                handle.Release();
            }

            IsLoading = false;
            onCompleted?.Invoke(false);
            yield break;
        }

        var levelConfig = handle.GetAssetObject<LevelConfigScritable>();
        if (levelConfig == null)
        {
            Debug.LogError("[YooAssetLevelLoader] Loaded level asset is invalid: " + resolvedLevelAddress, this);
            handle.Release();
            IsLoading = false;
            onCompleted?.Invoke(false);
            yield break;
        }

        ReleaseHandle(ref currentLevelHandle);
        currentLevelHandle = handle;

        if (ChessBoard != null)
        {
            ChessBoard.SetLevelConfig(levelConfig, resolvedLevelAddress);
            ChessBoard.ReloadLevel();
        }

        IsLoading = false;
        onCompleted?.Invoke(true);
    }

    private bool TryGetNextLevelAddress(out string levelAddress)
    {
        levelAddress = string.Empty;
        if (ChessBoard == null || !ChessBoard.TryGetCurrentLevelNumber(out var currentLevelNumber))
        {
            return false;
        }

        var package = ResolveResourcePackage();
        if (package == null)
        {
            return false;
        }

        return TryResolveValidLocation(package, BuildLevelAddress(currentLevelNumber + 1), out levelAddress);
    }

    private string ResolveCurrentLevelAddress()
    {
        if (ChessBoard != null && string.IsNullOrWhiteSpace(ChessBoard.CurrentLevelAddress) == false)
        {
            return ChessBoard.CurrentLevelAddress;
        }

        if (ChessBoard != null && ChessBoard.TryGetCurrentLevelNumber(out var currentLevelNumber))
        {
            return BuildLevelAddress(currentLevelNumber);
        }

        return string.Empty;
    }

    private string BuildLevelAddress(int levelNumber)
    {
        if (levelNumber <= 0 || string.IsNullOrWhiteSpace(LevelConfigAddressPattern))
        {
            return string.Empty;
        }

        return string.Format(LevelConfigAddressPattern, levelNumber);
    }

    private bool TryResolveValidLocation(ResourcePackage package, string requestedLocation, out string resolvedLocation)
    {
        resolvedLocation = string.Empty;
        if (package == null || string.IsNullOrWhiteSpace(requestedLocation))
        {
            return false;
        }

        if (package.CheckLocationValid(requestedLocation))
        {
            resolvedLocation = requestedLocation;
            return true;
        }

        var extensionlessLocation = RemoveAssetExtension(requestedLocation);
        if (string.Equals(extensionlessLocation, requestedLocation, StringComparison.Ordinal) == false &&
            package.CheckLocationValid(extensionlessLocation))
        {
            resolvedLocation = extensionlessLocation;
            return true;
        }

        var assetLocation = EnsureAssetExtension(requestedLocation);
        if (string.Equals(assetLocation, requestedLocation, StringComparison.Ordinal) == false &&
            package.CheckLocationValid(assetLocation))
        {
            resolvedLocation = assetLocation;
            return true;
        }

        return false;
    }

    private static string RemoveAssetExtension(string location)
    {
        if (string.IsNullOrWhiteSpace(location) || !location.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            return location;
        }

        return location.Substring(0, location.Length - ".asset".Length);
    }

    private static string EnsureAssetExtension(string location)
    {
        if (string.IsNullOrWhiteSpace(location) || location.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
        {
            return location;
        }

        return location + ".asset";
    }

    private ResourcePackage ResolveResourcePackage()
    {
        var runtime = YooAssetGameRuntime.Instance;
        if (runtime != null && runtime.Settings != null)
        {
            var configuredPackage = YooAssets.TryGetPackage(runtime.Settings.PackageName);
            if (configuredPackage != null)
            {
                return configuredPackage;
            }
        }

        return YooAssets.TryGetPackage("DefaultPackage");
    }

    private static void ReleaseHandle(ref AssetHandle handle)
    {
        if (handle == null)
        {
            return;
        }

        if (handle.IsValid)
        {
            handle.Release();
        }

        handle = null;
    }
}
