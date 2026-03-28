using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UniFramework.Event;
using YooAsset;
using YooSceneHandle = YooAsset.SceneHandle;

public sealed class YooAssetGameRuntime : MonoBehaviour
{
    private const string SettingsResourcePath = "YooAsset/YooAssetBootstrapSettings";

    private static bool bootstrapCreated;
    private static YooAssetGameRuntime instance;

    private YooAssetBootstrapSettings settings;
    private YooAssetUiManager uiManager;
    private YooAssetImageManager imageManager;
    private YooAssetSpriteAtlasBridge spriteAtlasBridge;
    private ResourcePackage package;
    private YooSceneHandle activeSceneHandle;
    private AssetHandle activePrefabHandle;
    private GameObject activePrefabInstance;
    private GameObject patchWindowInstance;
    private readonly List<AssetHandle> shaderVariantCollectionHandles = new List<AssetHandle>();
    private string activeEntryId = string.Empty;
    private bool packageInitializing;
    private bool packageInitialized;
    private bool shaderVariantsWarmed;
    private bool entryLoading;

    public static YooAssetGameRuntime Instance
    {
        get { return instance; }
    }

    public static bool IsReady
    {
        get { return instance != null && instance.packageInitialized; }
    }

    public static string ActiveEntryId
    {
        get { return instance == null ? string.Empty : instance.activeEntryId; }
    }

    public YooAssetBootstrapSettings Settings
    {
        get { return settings; }
    }

    public YooAssetUiManager UiManager
    {
        get { return uiManager; }
    }

    public YooAssetImageManager ImageManager
    {
        get { return imageManager; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        bootstrapCreated = false;
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void TryCreateBootstrap()
    {
        if (bootstrapCreated)
        {
            return;
        }

        YooAssetBootstrapSettings runtimeSettings = Resources.Load<YooAssetBootstrapSettings>(SettingsResourcePath);
        if (runtimeSettings == null)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (runtimeSettings.ShouldBootstrap(activeScene) == false)
        {
            return;
        }

        GameObject go = new GameObject(nameof(YooAssetGameRuntime));
        DontDestroyOnLoad(go);
        go.AddComponent<YooAssetUiManager>();
        go.AddComponent<YooAssetImageManager>();
        go.AddComponent<YooAssetSpriteAtlasBridge>();
        go.AddComponent<YooAssetGameRuntime>();
        bootstrapCreated = true;
    }

    public static Coroutine LoadEntry(string entryId)
    {
        if (instance == null)
        {
            Debug.LogError("[YooAsset] Runtime bootstrap is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadEntryRoutine(entryId));
    }

    public static Coroutine LoadUiScreen(string screenId)
    {
        if (instance == null || instance.uiManager == null)
        {
            Debug.LogError("[YooAsset][UI] Runtime UI manager is not available.");
            return null;
        }

        return instance.uiManager.OpenScreen(screenId);
    }

    public static Coroutine LoadUiPrefab(string instanceId, string address, System.Action<GameObject> onLoaded = null)
    {
        if (instance == null || instance.uiManager == null)
        {
            Debug.LogError("[YooAsset][UI] Runtime UI manager is not available.");
            return null;
        }

        return instance.uiManager.LoadPrefab(instanceId, address, onLoaded);
    }

    public static bool CloseUiScreen(string screenId)
    {
        if (instance == null || instance.uiManager == null)
        {
            return false;
        }

        return instance.uiManager.CloseScreen(screenId);
    }

    public static Coroutine LoadTextureAsset(string assetId, string address, System.Action<Texture2D> onLoaded = null)
    {
        if (instance == null || instance.imageManager == null)
        {
            Debug.LogError("[YooAsset][Image] Runtime image manager is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadTextureAssetRoutine(assetId, address, onLoaded));
    }

    public static Coroutine LoadSpriteAsset(string assetId, string address, System.Action<Sprite> onLoaded = null)
    {
        if (instance == null || instance.imageManager == null)
        {
            Debug.LogError("[YooAsset][Image] Runtime image manager is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadSpriteAssetRoutine(assetId, address, onLoaded));
    }

    public static Coroutine LoadAtlasSpriteAsset(string atlasId, string atlasAddress, string spriteName, System.Action<Sprite> onLoaded = null)
    {
        if (instance == null || instance.imageManager == null)
        {
            Debug.LogError("[YooAsset][Image] Runtime image manager is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadAtlasSpriteAssetRoutine(atlasId, atlasAddress, spriteName, onLoaded));
    }

    public static Coroutine LoadTextureToRawImage(string assetId, string address, RawImage target, bool setNativeSize = false, System.Action<Texture2D> onLoaded = null)
    {
        if (instance == null || instance.imageManager == null)
        {
            Debug.LogError("[YooAsset][Image] Runtime image manager is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadTextureToRawImageRoutine(assetId, address, target, setNativeSize, onLoaded));
    }

    public static Coroutine LoadSpriteToImage(string assetId, string address, Image target, bool setNativeSize = false, System.Action<Sprite> onLoaded = null)
    {
        if (instance == null || instance.imageManager == null)
        {
            Debug.LogError("[YooAsset][Image] Runtime image manager is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadSpriteToImageRoutine(assetId, address, target, setNativeSize, onLoaded));
    }

    public static Coroutine LoadAtlasSpriteToImage(string atlasId, string atlasAddress, string spriteName, Image target, bool setNativeSize = false, System.Action<Sprite> onLoaded = null)
    {
        if (instance == null || instance.imageManager == null)
        {
            Debug.LogError("[YooAsset][Image] Runtime image manager is not available.");
            return null;
        }

        return instance.StartCoroutine(instance.LoadAtlasSpriteToImageRoutine(atlasId, atlasAddress, spriteName, target, setNativeSize, onLoaded));
    }

    public static bool ReleaseLoadedImageAsset(string assetId)
    {
        if (instance == null || instance.imageManager == null)
        {
            return false;
        }

        return instance.imageManager.ReleaseAsset(assetId);
    }

    public static bool RefreshUiCameraStack(Camera baseCamera = null)
    {
        if (instance == null || instance.uiManager == null)
        {
            return false;
        }

        return instance.uiManager.RefreshCameraStack(baseCamera);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        uiManager = GetComponent<YooAssetUiManager>();
        if (uiManager == null)
        {
            uiManager = gameObject.AddComponent<YooAssetUiManager>();
        }

        imageManager = GetComponent<YooAssetImageManager>();
        if (imageManager == null)
        {
            imageManager = gameObject.AddComponent<YooAssetImageManager>();
        }

        spriteAtlasBridge = GetComponent<YooAssetSpriteAtlasBridge>();
        if (spriteAtlasBridge == null)
        {
            spriteAtlasBridge = gameObject.AddComponent<YooAssetSpriteAtlasBridge>();
        }

        instance = this;
    }

    private IEnumerator Start()
    {
        settings = Resources.Load<YooAssetBootstrapSettings>(SettingsResourcePath);
        if (settings == null)
        {
            Debug.LogError("[YooAsset] Missing bootstrap settings at Resources/YooAsset/YooAssetBootstrapSettings.");
            yield break;
        }

        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false)
        {
            yield break;
        }

        yield return LoadEntryRoutine(settings.StartupEntryId);
    }

    private void OnDestroy()
    {
        ReleaseShaderVariantCollections();
    }

    public Coroutine RunHostedCoroutine(IEnumerator routine)
    {
        return StartCoroutine(routine);
    }

    public InitializeParameters CreateInitializeParameters(EPlayMode playMode)
    {
        if (playMode == EPlayMode.EditorSimulateMode)
        {
            PackageInvokeBuildResult buildResult = EditorSimulateModeHelper.SimulateBuild(settings.PackageName);
            EditorSimulateModeParameters editorParameters = new EditorSimulateModeParameters();
            editorParameters.EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
            return editorParameters;
        }

        if (playMode == EPlayMode.OfflinePlayMode)
        {
            OfflinePlayModeParameters offlineParameters = new OfflinePlayModeParameters();
            offlineParameters.BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters();
            return offlineParameters;
        }

        if (playMode == EPlayMode.HostPlayMode)
        {
            string primaryUrl = settings.GetPrimaryHostServerUrl();
            string fallbackUrl = settings.GetFallbackHostServerUrl();
            BootstrapRemoteServices remoteServices = new BootstrapRemoteServices(primaryUrl, fallbackUrl);

            HostPlayModeParameters hostParameters = new HostPlayModeParameters();
            hostParameters.BuildinFileSystemParameters = null;
            hostParameters.CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices);
            return hostParameters;
        }

        if (playMode == EPlayMode.WebPlayMode)
        {
            string primaryUrl = settings.GetPrimaryHostServerUrl();
            string fallbackUrl = settings.GetFallbackHostServerUrl();
            BootstrapRemoteServices remoteServices = new BootstrapRemoteServices(primaryUrl, fallbackUrl);

            WebPlayModeParameters webParameters = new WebPlayModeParameters();
            webParameters.WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters();
            webParameters.WebRemoteFileSystemParameters = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServices);
            return webParameters;
        }

        throw new System.NotSupportedException("Unsupported YooAsset play mode: " + playMode);
    }

    private IEnumerator EnsurePackageReadyRoutine()
    {
        if (packageInitialized)
        {
            yield break;
        }

        while (packageInitializing)
        {
            yield return null;
        }

        if (packageInitialized)
        {
            yield break;
        }

        packageInitializing = true;
        EnsureCoreSystems();
        EnsurePatchWindow();

        PatchOperation operation = new PatchOperation(settings.PackageName, settings.ResolvePlayMode(), this);
        YooAssets.StartOperation(operation);
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            packageInitializing = false;
            Debug.LogError("[YooAsset] Patch operation failed: " + operation.Error);
            yield break;
        }

        package = YooAssets.GetPackage(settings.PackageName);
        if (package == null)
        {
            packageInitializing = false;
            Debug.LogError("[YooAsset] Patched package was not found: " + settings.PackageName);
            yield break;
        }

        YooAssets.SetDefaultPackage(package);
        if (imageManager != null)
        {
            imageManager.SetPackage(package);
        }

        if (spriteAtlasBridge != null)
        {
            spriteAtlasBridge.SetPackage(package);
        }

        yield return WarmUpShaderVariantsRoutine();
        packageInitialized = true;
        packageInitializing = false;
        GameLog.InfoFormat("[YooAsset] Package '{0}' is ready.", settings.PackageName);
    }

    private IEnumerator WarmUpShaderVariantsRoutine()
    {
        if (shaderVariantsWarmed || settings == null || package == null)
        {
            yield break;
        }

        shaderVariantsWarmed = true;
        IReadOnlyList<string> configuredAddresses = settings.ShaderVariantCollectionAddresses;
        if (configuredAddresses == null || configuredAddresses.Count == 0)
        {
            yield break;
        }

        int warmedCount = 0;
        for (int i = 0; i < configuredAddresses.Count; i++)
        {
            string configuredAddress = configuredAddresses[i];
            if (string.IsNullOrWhiteSpace(configuredAddress))
            {
                continue;
            }

            AssetHandle handle = null;
            ShaderVariantCollection collection = null;
            string resolvedAddress = string.Empty;

            yield return TryLoadShaderVariantCollectionRoutine(package, configuredAddress, result =>
            {
                handle = result.Handle;
                collection = result.Collection;
                resolvedAddress = result.Address;
            });

            if (collection == null)
            {
                Debug.LogWarning("[YooAsset][Shader] Failed to load ShaderVariantCollection: " + configuredAddress);
                continue;
            }

            collection.WarmUp();
            warmedCount++;
            if (handle != null && handle.IsValid)
            {
                shaderVariantCollectionHandles.Add(handle);
            }

            Debug.Log("[YooAsset][Shader] Warmed ShaderVariantCollection: " + resolvedAddress);
        }

        Debug.Log("[YooAsset][Shader] Warmed collections: " + warmedCount);
    }

    private IEnumerator TryLoadShaderVariantCollectionRoutine(ResourcePackage resourcePackage, string configuredAddress, System.Action<ShaderVariantCollectionLoadResult> onCompleted)
    {
        string[] candidates = BuildAddressCandidates(configuredAddress);
        for (int i = 0; i < candidates.Length; i++)
        {
            string candidateAddress = candidates[i];
            if (string.IsNullOrWhiteSpace(candidateAddress))
            {
                continue;
            }

            if (resourcePackage.CheckLocationValid(candidateAddress) == false)
            {
                continue;
            }

            AssetHandle handle = resourcePackage.LoadAssetAsync<ShaderVariantCollection>(candidateAddress);
            yield return handle;

            if (handle.Status != EOperationStatus.Succeed)
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }

                continue;
            }

            ShaderVariantCollection collection = handle.GetAssetObject<ShaderVariantCollection>();
            if (collection == null)
            {
                handle.Release();
                continue;
            }

            onCompleted?.Invoke(new ShaderVariantCollectionLoadResult(candidateAddress, handle, collection));
            yield break;
        }
    }

    private static string[] BuildAddressCandidates(string configuredAddress)
    {
        List<string> candidates = new List<string>(4);
        AddAddressCandidate(candidates, configuredAddress);
        AddAddressCandidate(candidates, Path.ChangeExtension(configuredAddress, null));
        AddAddressCandidate(candidates, Path.GetFileName(configuredAddress));
        AddAddressCandidate(candidates, Path.GetFileNameWithoutExtension(configuredAddress));
        return candidates.ToArray();
    }

    private static void AddAddressCandidate(List<string> candidates, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], candidate, System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        candidates.Add(candidate);
    }

    private void ReleaseShaderVariantCollections()
    {
        for (int i = shaderVariantCollectionHandles.Count - 1; i >= 0; i--)
        {
            AssetHandle handle = shaderVariantCollectionHandles[i];
            if (handle != null && handle.IsValid)
            {
                handle.Release();
            }
        }

        shaderVariantCollectionHandles.Clear();
        shaderVariantsWarmed = false;
    }

    private IEnumerator LoadTextureAssetRoutine(string assetId, string address, System.Action<Texture2D> onLoaded)
    {
        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false || imageManager == null)
        {
            yield break;
        }

        imageManager.SetPackage(package);
        yield return imageManager.LoadTexture(assetId, address, onLoaded);
    }

    private IEnumerator LoadSpriteAssetRoutine(string assetId, string address, System.Action<Sprite> onLoaded)
    {
        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false || imageManager == null)
        {
            yield break;
        }

        imageManager.SetPackage(package);
        yield return imageManager.LoadSprite(assetId, address, onLoaded);
    }

    private IEnumerator LoadAtlasSpriteAssetRoutine(string atlasId, string atlasAddress, string spriteName, System.Action<Sprite> onLoaded)
    {
        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false || imageManager == null)
        {
            yield break;
        }

        imageManager.SetPackage(package);
        yield return imageManager.LoadAtlasSprite(atlasId, atlasAddress, spriteName, onLoaded);
    }

    private IEnumerator LoadTextureToRawImageRoutine(string assetId, string address, RawImage target, bool setNativeSize, System.Action<Texture2D> onLoaded)
    {
        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false || imageManager == null)
        {
            yield break;
        }

        imageManager.SetPackage(package);
        yield return imageManager.LoadTextureToRawImage(assetId, address, target, setNativeSize, onLoaded);
    }

    private IEnumerator LoadSpriteToImageRoutine(string assetId, string address, Image target, bool setNativeSize, System.Action<Sprite> onLoaded)
    {
        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false || imageManager == null)
        {
            yield break;
        }

        imageManager.SetPackage(package);
        yield return imageManager.LoadSpriteToImage(assetId, address, target, setNativeSize, onLoaded);
    }

    private IEnumerator LoadAtlasSpriteToImageRoutine(string atlasId, string atlasAddress, string spriteName, Image target, bool setNativeSize, System.Action<Sprite> onLoaded)
    {
        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false || imageManager == null)
        {
            yield break;
        }

        imageManager.SetPackage(package);
        yield return imageManager.LoadAtlasSpriteToImage(atlasId, atlasAddress, spriteName, target, setNativeSize, onLoaded);
    }

    private IEnumerator LoadEntryRoutine(string entryId)
    {
        if (entryLoading)
        {
            GameLog.Warning("[YooAsset] Entry loading is already in progress.");
            yield break;
        }

        yield return EnsurePackageReadyRoutine();
        if (packageInitialized == false)
        {
            yield break;
        }

        YooAssetMiniGameEntry entry;
        if (settings.TryGetEntry(entryId, out entry) == false || entry == null)
        {
            Debug.LogError("[YooAsset] Can not find mini-game entry: " + entryId);
            yield break;
        }

        if (string.IsNullOrWhiteSpace(entry.Address))
        {
            Debug.LogError("[YooAsset] Entry address is empty: " + entry.DisplayName);
            yield break;
        }

        entryLoading = true;
        GameLog.InfoFormat("[YooAsset] Load entry '{0}' ({1}).", entry.DisplayName, entry.Address);
        PreparePatchWindowForEntryTransition();

        if (entry.LoadMode == YooAssetMiniGameEntryLoadMode.Scene)
        {
            ReleaseActivePrefab();

            YooSceneHandle previousSceneHandle = activeSceneHandle;
            YooSceneHandle sceneHandle = package.LoadSceneAsync(entry.Address, entry.SceneMode);
            yield return sceneHandle;

            if (sceneHandle.Status != EOperationStatus.Succeed)
            {
                entryLoading = false;
                Debug.LogError("[YooAsset] Load scene failed: " + sceneHandle.LastError);
                yield break;
            }

            if (entry.SceneMode == LoadSceneMode.Additive)
            {
                sceneHandle.ActivateScene();
            }

            activeSceneHandle = sceneHandle;
            activeEntryId = entry.EntryId;

            Scene activeScene = SceneManager.GetActiveScene();
            if (uiManager != null)
            {
                yield return uiManager.LoadSceneUiRoutine(package, settings.UiSettingsAddress, activeScene);
            }

            DestroyPatchWindow();

            if (previousSceneHandle != null && previousSceneHandle.IsValid && previousSceneHandle != sceneHandle && entry.SceneMode == LoadSceneMode.Single)
            {
                previousSceneHandle.Release();
            }
        }
        else
        {
            if (uiManager != null)
            {
                uiManager.UnloadManagedUi();
            }

            if (activeSceneHandle != null && activeSceneHandle.IsValid)
            {
                UnloadSceneOperation unloadSceneOperation = activeSceneHandle.UnloadAsync();
                yield return unloadSceneOperation;
                activeSceneHandle = null;
            }

            ReleaseActivePrefab();

            AssetHandle assetHandle = package.LoadAssetAsync<GameObject>(entry.Address);
            yield return assetHandle;

            if (assetHandle.Status != EOperationStatus.Succeed)
            {
                entryLoading = false;
                Debug.LogError("[YooAsset] Load prefab failed: " + assetHandle.LastError);
                yield break;
            }

            GameObject prefab = assetHandle.GetAssetObject<GameObject>();
            if (prefab == null)
            {
                assetHandle.Release();
                entryLoading = false;
                Debug.LogError("[YooAsset] Loaded asset is not a GameObject: " + entry.Address);
                yield break;
            }

            activePrefabHandle = assetHandle;
            activePrefabInstance = Instantiate(prefab);
            activeEntryId = entry.EntryId;
            DestroyPatchWindow();
        }

        entryLoading = false;
    }

    private void EnsureCoreSystems()
    {
        if (YooAssets.Initialized == false)
        {
            GameLog.Info("[YooAsset] Initialize runtime.");
            YooAssets.Initialize();
        }

        if (UniEvent.Initialized == false)
        {
            UniEvent.Initalize();
        }
    }

    private void EnsurePatchWindow()
    {
        if (patchWindowInstance != null)
        {
            return;
        }

        GameObject patchWindowPrefab = Resources.Load<GameObject>("PatchWindow");
        if (patchWindowPrefab == null)
        {
            GameLog.Warning("[YooAsset] PatchWindow prefab was not found in Resources.");
            return;
        }

        patchWindowInstance = Instantiate(patchWindowPrefab);
        DontDestroyOnLoad(patchWindowInstance);
        UiInputModuleCompatibilityUtility.EnsureInputSystemModules(patchWindowInstance);
    }

    private void DestroyPatchWindow()
    {
        if (patchWindowInstance != null)
        {
            Destroy(patchWindowInstance);
            patchWindowInstance = null;
        }
    }

    private void PreparePatchWindowForEntryTransition()
    {
        if (patchWindowInstance == null)
        {
            return;
        }

        EventSystem[] eventSystems = patchWindowInstance.GetComponentsInChildren<EventSystem>(true);
        for (int i = 0; i < eventSystems.Length; i++)
        {
            EventSystem eventSystem = eventSystems[i];
            if (eventSystem != null)
            {
                eventSystem.gameObject.SetActive(false);
            }
        }
    }

    private void ReleaseActivePrefab()
    {
        if (activePrefabInstance != null)
        {
            Destroy(activePrefabInstance);
            activePrefabInstance = null;
        }

        if (activePrefabHandle != null && activePrefabHandle.IsValid)
        {
            activePrefabHandle.Release();
        }

        activePrefabHandle = null;
    }

    private sealed class BootstrapRemoteServices : IRemoteServices
    {
        private readonly string primaryUrl;
        private readonly string fallbackUrl;

        public BootstrapRemoteServices(string primaryUrl, string fallbackUrl)
        {
            this.primaryUrl = primaryUrl;
            this.fallbackUrl = string.IsNullOrWhiteSpace(fallbackUrl) ? primaryUrl : fallbackUrl;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return primaryUrl.TrimEnd('/') + "/" + fileName;
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return fallbackUrl.TrimEnd('/') + "/" + fileName;
        }
    }

    private readonly struct ShaderVariantCollectionLoadResult
    {
        public readonly string Address;
        public readonly AssetHandle Handle;
        public readonly ShaderVariantCollection Collection;

        public ShaderVariantCollectionLoadResult(string address, AssetHandle handle, ShaderVariantCollection collection)
        {
            Address = address;
            Handle = handle;
            Collection = collection;
        }
    }
}

internal static class UiInputModuleCompatibilityUtility
{
    public static void EnsureInputSystemModules(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        EventSystem[] eventSystems = root.GetComponentsInChildren<EventSystem>(true);
        for (int i = 0; i < eventSystems.Length; i++)
        {
            EnsureInputSystemModule(eventSystems[i]);
        }
    }

    public static void EnsureInputSystemModule(EventSystem eventSystem)
    {
        if (eventSystem == null)
        {
            return;
        }

        StandaloneInputModule[] legacyModules = eventSystem.GetComponents<StandaloneInputModule>();
        for (int i = 0; i < legacyModules.Length; i++)
        {
            StandaloneInputModule legacyModule = legacyModules[i];
            if (legacyModule == null)
            {
                continue;
            }

            legacyModule.enabled = false;
            Object.Destroy(legacyModule);
        }

        InputSystemUIInputModule inputSystemModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemModule == null)
        {
            inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputSystemModule.AssignDefaultActions();
        }
    }
}
