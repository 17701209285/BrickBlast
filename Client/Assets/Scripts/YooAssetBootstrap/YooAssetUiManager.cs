using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using YooAsset;

public sealed class YooAssetUiManager : MonoBehaviour
{
    private readonly List<LoadedScreen> loadedScreens = new List<LoadedScreen>();

    private YooAssetUiSettings settings;
    private AssetHandle settingsHandle;
    private ResourcePackage activePackage;
    private YooAssetUiSceneBinding activeBinding;
    private AssetHandle rootHandle;
    private GameObject rootInstance;
    private Camera rootUiCamera;
    private RectTransform screenLayer;
    private string activeSceneName = string.Empty;
    private string activeSettingsAddress = string.Empty;

    public Transform UiRootTransform
    {
        get { return rootInstance == null ? null : rootInstance.transform; }
    }

    public bool RefreshCameraStack(Camera baseCamera = null)
    {
        if (rootInstance == null)
        {
            return false;
        }

        return UiCameraStackUtility.BindUiRootToSceneCamera(rootInstance, rootInstance.scene, baseCamera);
    }

    public Coroutine OpenScreen(string screenId)
    {
        if (activePackage == null)
        {
            Debug.LogError("[YooAsset][UI] Package is not ready for opening screens.");
            return null;
        }

        if (activeBinding == null)
        {
            Debug.LogError("[YooAsset][UI] No active UI scene binding is loaded.");
            return null;
        }

        return StartCoroutine(LoadScreenRoutine(activePackage, screenId));
    }

    public Coroutine LoadPrefab(string instanceId, string address, Action<GameObject> onLoaded = null)
    {
        if (activePackage == null)
        {
            Debug.LogError("[YooAsset][UI] Package is not ready for loading UI prefabs.");
            return null;
        }

        return StartCoroutine(LoadUiPrefabRoutine(activePackage, instanceId, address, onLoaded));
    }

    public bool CloseScreen(string screenId)
    {
        return ReleaseLoadedScreen(screenId);
    }

    public void UnloadManagedUi()
    {
        ReleaseLoadedUi();
    }

    public IEnumerator LoadSceneUiRoutine(ResourcePackage package, string uiSettingsAddress, Scene scene)
    {
        if (package == null)
        {
            Debug.LogError("[YooAsset][UI] Can not load scene UI without a resource package.");
            yield break;
        }

        yield return EnsureSettingsRoutine(package, uiSettingsAddress);
        if (settings == null)
        {
            ReleaseLoadedUi();
            yield break;
        }

        YooAssetUiSceneBinding binding;
        if (settings.TryGetSceneBinding(scene, out binding) == false || binding == null)
        {
            ReleaseLoadedUi(false);
            yield break;
        }

        bool sameSceneBinding = rootInstance != null
            && string.Equals(activeSceneName, scene.name, System.StringComparison.OrdinalIgnoreCase)
            && activeBinding == binding;
        if (sameSceneBinding)
        {
            if (rootInstance != null && scene.IsValid() && scene.isLoaded && rootInstance.scene != scene)
            {
                SceneManager.MoveGameObjectToScene(rootInstance, scene);
            }

            yield break;
        }

        ReleaseLoadedUi(false);
        activePackage = package;
        activeBinding = binding;
        activeSceneName = scene.name;

        yield return LoadRootRoutine(package, scene, binding);
        if (rootInstance == null)
        {
            ReleaseLoadedUi();
            yield break;
        }

        for (int i = 0; i < binding.StartupScreenCount; i++)
        {
            string screenId = binding.GetStartupScreenId(i);
            if (string.IsNullOrWhiteSpace(screenId))
            {
                continue;
            }

            yield return LoadScreenRoutine(package, screenId);
        }
    }

    private void OnDestroy()
    {
        ReleaseLoadedUi();
    }

    private IEnumerator LoadRootRoutine(ResourcePackage package, Scene scene, YooAssetUiSceneBinding binding)
    {
        if (string.IsNullOrWhiteSpace(binding.RootPrefabAddress))
        {
            Debug.LogError("[YooAsset][UI] Root prefab address is empty for scene: " + binding.SceneName);
            yield break;
        }

        AssetHandle handle = package.LoadAssetAsync<GameObject>(binding.RootPrefabAddress);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeed)
        {
            handle.Release();
            Debug.LogError("[YooAsset][UI] Load root prefab failed: " + handle.LastError);
            yield break;
        }

        GameObject prefab = handle.GetAssetObject<GameObject>();
        if (prefab == null)
        {
            handle.Release();
            Debug.LogError("[YooAsset][UI] Root prefab is not a GameObject: " + binding.RootPrefabAddress);
            yield break;
        }

        rootHandle = handle;
        rootInstance = Instantiate(prefab);
        UiInputModuleCompatibilityUtility.EnsureInputSystemModules(rootInstance);
        NormalizeRootTransform(rootInstance.transform);

        if (scene.IsValid() && scene.isLoaded && rootInstance.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(rootInstance, scene);
        }

        rootUiCamera = UiCameraStackUtility.FindPreferredUiCamera(rootInstance);
        UiCameraStackUtility.AssignWorldCamera(rootInstance, rootUiCamera);
        UiCameraStackUtility.BindUiRootToSceneCamera(rootInstance, scene);
        screenLayer = EnsureScreenLayer(binding);
        GameLog.InfoFormat("[YooAsset][UI] Loaded root '{0}' for scene '{1}'.", binding.RootPrefabAddress, scene.name);
    }

    private IEnumerator LoadScreenRoutine(ResourcePackage package, string screenId)
    {
        YooAssetUiScreenDefinition screen;
        if (activeBinding.TryGetScreen(screenId, out screen) == false || screen == null)
        {
            Debug.LogError("[YooAsset][UI] Can not find screen definition: " + screenId);
            yield break;
        }

        yield return LoadUiPrefabRoutine(package, screen.ScreenId, screen.Address, null);
    }

    private IEnumerator LoadUiPrefabRoutine(ResourcePackage package, string instanceId, string address, Action<GameObject> onLoaded)
    {
        if (rootInstance == null)
        {
            Debug.LogError("[YooAsset][UI] Root UI is not loaded. UI prefab can not be opened: " + address);
            yield break;
        }

        string loadId = string.IsNullOrWhiteSpace(instanceId) ? address : instanceId;
        if (string.IsNullOrWhiteSpace(loadId))
        {
            Debug.LogError("[YooAsset][UI] UI prefab load id is empty.");
            yield break;
        }

        int loadedIndex = FindLoadedScreenIndex(loadId);
        if (loadedIndex >= 0)
        {
            if (onLoaded != null)
            {
                onLoaded(loadedScreens[loadedIndex].Instance);
            }

            yield break;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            Debug.LogError("[YooAsset][UI] UI prefab address is empty: " + loadId);
            yield break;
        }

        AssetHandle handle = package.LoadAssetAsync<GameObject>(address);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeed)
        {
            handle.Release();
            Debug.LogError("[YooAsset][UI] Load screen failed: " + handle.LastError);
            yield break;
        }

        GameObject prefab = handle.GetAssetObject<GameObject>();
        if (prefab == null)
        {
            handle.Release();
            Debug.LogError("[YooAsset][UI] UI prefab is not a GameObject: " + address);
            yield break;
        }

        RectTransform parent = EnsureScreenLayer(activeBinding);
        GameObject instance = Instantiate(prefab, parent, false);
        UiInputModuleCompatibilityUtility.EnsureInputSystemModules(instance);
        NormalizeRootTransform(instance.transform);
        UiCameraStackUtility.AssignWorldCamera(instance, rootUiCamera);
        loadedScreens.Add(new LoadedScreen(loadId, address, handle, instance));
        if (onLoaded != null)
        {
            onLoaded(instance);
        }

        GameLog.InfoFormat("[YooAsset][UI] Loaded UI '{0}' from '{1}'.", loadId, address);
    }

    private IEnumerator EnsureSettingsRoutine(ResourcePackage package, string uiSettingsAddress)
    {
        if (string.IsNullOrWhiteSpace(uiSettingsAddress))
        {
            Debug.LogError("[YooAsset][UI] UI settings address is empty in bootstrap settings.");
            yield break;
        }

        if (settings != null
            && settingsHandle != null
            && settingsHandle.IsValid
            && ReferenceEquals(activePackage, package)
            && string.Equals(activeSettingsAddress, uiSettingsAddress, StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        ReleaseSettings();
        AssetHandle handle = null;
        YooAssetUiSettings loadedSettings = null;
        string resolvedAddress = string.Empty;

        yield return TryLoadSettingsFromPackage(package, uiSettingsAddress, result =>
        {
            handle = result.Handle;
            loadedSettings = result.Settings;
            resolvedAddress = result.Address;
        });

        if (loadedSettings == null)
        {
#if UNITY_EDITOR
            loadedSettings = TryLoadSettingsFromEditor(uiSettingsAddress, out resolvedAddress);
#endif
        }

        if (loadedSettings == null)
        {
            if (handle != null && handle.IsValid)
            {
                handle.Release();
            }

            Debug.LogError("[YooAsset][UI] Load UI settings failed: " + uiSettingsAddress);
            yield break;
        }

        settingsHandle = handle;
        settings = loadedSettings;
        activeSettingsAddress = string.IsNullOrEmpty(resolvedAddress) ? uiSettingsAddress : resolvedAddress;
    }

    private IEnumerator TryLoadSettingsFromPackage(ResourcePackage package, string configuredAddress, Action<SettingsLoadResult> onCompleted)
    {
        string[] candidateAddresses = BuildSettingsAddressCandidates(configuredAddress);
        for (int i = 0; i < candidateAddresses.Length; i++)
        {
            string candidateAddress = candidateAddresses[i];
            if (string.IsNullOrWhiteSpace(candidateAddress))
            {
                continue;
            }

            AssetHandle handle = package.LoadAssetAsync<YooAssetUiSettings>(candidateAddress);
            yield return handle;

            if (handle.Status != EOperationStatus.Succeed)
            {
                if (handle.IsValid)
                {
                    handle.Release();
                }

                continue;
            }

            YooAssetUiSettings loadedSettings = handle.GetAssetObject<YooAssetUiSettings>();
            if (loadedSettings == null)
            {
                handle.Release();
                continue;
            }

            onCompleted?.Invoke(new SettingsLoadResult(candidateAddress, handle, loadedSettings));
            yield break;
        }
    }

    private static string[] BuildSettingsAddressCandidates(string configuredAddress)
    {
        List<string> candidates = new List<string>(4);
        AddCandidate(candidates, configuredAddress);
        AddCandidate(candidates, Path.ChangeExtension(configuredAddress, null));
        AddCandidate(candidates, Path.GetFileNameWithoutExtension(configuredAddress));
        return candidates.ToArray();
    }

    private static void AddCandidate(List<string> candidates, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], candidate, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        candidates.Add(candidate);
    }

#if UNITY_EDITOR
    private static YooAssetUiSettings TryLoadSettingsFromEditor(string configuredAddress, out string resolvedAddress)
    {
        string[] candidateAddresses = BuildSettingsAddressCandidates(configuredAddress);
        for (int i = 0; i < candidateAddresses.Length; i++)
        {
            string candidateAddress = candidateAddresses[i];
            if (string.IsNullOrWhiteSpace(candidateAddress) || candidateAddress.StartsWith("Assets/", StringComparison.Ordinal) == false)
            {
                continue;
            }

            YooAssetUiSettings asset = UnityEditor.AssetDatabase.LoadAssetAtPath<YooAssetUiSettings>(candidateAddress);
            if (asset != null)
            {
                GameLog.Warning("[YooAsset][UI] UI settings fell back to AssetDatabase loading: " + candidateAddress);
                resolvedAddress = candidateAddress;
                return asset;
            }
        }

        resolvedAddress = string.Empty;
        return null;
    }
#endif

    private RectTransform EnsureScreenLayer(YooAssetUiSceneBinding binding)
    {
        if (screenLayer != null)
        {
            return screenLayer;
        }

        Transform existing = rootInstance.transform.Find(binding.ScreenLayerName);
        screenLayer = existing as RectTransform;
        if (screenLayer != null)
        {
            return screenLayer;
        }

        GameObject layerObject = new GameObject(binding.ScreenLayerName, typeof(RectTransform));
        layerObject.layer = rootInstance.layer;
        screenLayer = layerObject.GetComponent<RectTransform>();
        screenLayer.SetParent(rootInstance.transform, false);
        screenLayer.anchorMin = Vector2.zero;
        screenLayer.anchorMax = Vector2.one;
        screenLayer.offsetMin = Vector2.zero;
        screenLayer.offsetMax = Vector2.zero;
        screenLayer.localScale = Vector3.one;
        screenLayer.localRotation = Quaternion.identity;
        screenLayer.localPosition = Vector3.zero;
        return screenLayer;
    }

    private void NormalizeRootTransform(Transform target)
    {
        RectTransform rectTransform = target as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.anchoredPosition3D = Vector3.zero;
            return;
        }

        target.localScale = Vector3.one;
        target.localRotation = Quaternion.identity;
        target.localPosition = Vector3.zero;
    }

    private void ReleaseLoadedUi(bool releaseSettings = true)
    {
        for (int i = loadedScreens.Count - 1; i >= 0; i--)
        {
            LoadedScreen loadedScreen = loadedScreens[i];
            if (loadedScreen.Instance != null)
            {
                Destroy(loadedScreen.Instance);
            }

            if (loadedScreen.Handle != null && loadedScreen.Handle.IsValid)
            {
                loadedScreen.Handle.Release();
            }
        }

        loadedScreens.Clear();
        screenLayer = null;

        if (rootInstance != null)
        {
            UiCameraStackUtility.UnbindUiRootFromSceneCamera(rootInstance, rootInstance.scene);
            Destroy(rootInstance);
            rootInstance = null;
        }

        if (rootHandle != null && rootHandle.IsValid)
        {
            rootHandle.Release();
        }

        rootHandle = null;
        rootUiCamera = null;
        activeBinding = null;
        activePackage = null;
        activeSceneName = string.Empty;
        if (releaseSettings)
        {
            ReleaseSettings();
        }
    }

    private bool ReleaseLoadedScreen(string screenId)
    {
        int index = FindLoadedScreenIndex(screenId);
        if (index < 0)
        {
            return false;
        }

        LoadedScreen loadedScreen = loadedScreens[index];
        if (loadedScreen.Instance != null)
        {
            Destroy(loadedScreen.Instance);
        }

        if (loadedScreen.Handle != null && loadedScreen.Handle.IsValid)
        {
            loadedScreen.Handle.Release();
        }

        loadedScreens.RemoveAt(index);
        return true;
    }

    private int FindLoadedScreenIndex(string screenId)
    {
        for (int i = 0; i < loadedScreens.Count; i++)
        {
            if (string.Equals(loadedScreens[i].Id, screenId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void ReleaseSettings()
    {
        if (settingsHandle != null && settingsHandle.IsValid)
        {
            settingsHandle.Release();
        }

        settingsHandle = null;
        settings = null;
        activeSettingsAddress = string.Empty;
    }

    private readonly struct LoadedScreen
    {
        public readonly string Id;
        public readonly string Address;
        public readonly AssetHandle Handle;
        public readonly GameObject Instance;

        public LoadedScreen(string id, string address, AssetHandle handle, GameObject instance)
        {
            Id = id;
            Address = address;
            Handle = handle;
            Instance = instance;
        }
    }

    private readonly struct SettingsLoadResult
    {
        public readonly string Address;
        public readonly AssetHandle Handle;
        public readonly YooAssetUiSettings Settings;

        public SettingsLoadResult(string address, AssetHandle handle, YooAssetUiSettings settings)
        {
            Address = address;
            Handle = handle;
            Settings = settings;
        }
    }
}

internal static class UiCameraStackUtility
{
    public static bool BindUiRootToSceneCamera(GameObject uiRoot, Scene scene, Camera explicitBaseCamera = null)
    {
        if (uiRoot == null)
        {
            return false;
        }

        Camera uiCamera = FindPreferredUiCamera(uiRoot);
        if (uiCamera == null)
        {
            return false;
        }

        Camera baseCamera = FindBaseCamera(scene, uiRoot.transform, explicitBaseCamera);
        AssignWorldCamera(uiRoot, uiCamera);
        if (baseCamera == null)
        {
            GameLog.Warning("[YooAsset][UI] Base camera was not found for UI root: " + uiRoot.name);
            return false;
        }

        UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData uiData = uiCamera.GetUniversalAdditionalCameraData();
        if (baseData == null || uiData == null)
        {
            GameLog.Warning("[YooAsset][UI] URP camera data is missing while binding UI camera stack.");
            return false;
        }

        if (uiData.renderType != CameraRenderType.Overlay)
        {
            uiData.renderType = CameraRenderType.Overlay;
        }

        if (baseData.cameraStack.Contains(uiCamera) == false)
        {
            baseData.cameraStack.Add(uiCamera);
        }

        return true;
    }

    public static void UnbindUiRootFromSceneCamera(GameObject uiRoot, Scene scene, Camera explicitBaseCamera = null)
    {
        if (uiRoot == null)
        {
            return;
        }

        Camera uiCamera = FindPreferredUiCamera(uiRoot);
        if (uiCamera == null)
        {
            return;
        }

        Camera baseCamera = FindBaseCamera(scene, uiRoot.transform, explicitBaseCamera);
        if (baseCamera == null)
        {
            return;
        }

        UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
        if (baseData == null)
        {
            return;
        }

        baseData.cameraStack.Remove(uiCamera);
    }

    public static Camera FindPreferredUiCamera(GameObject uiRoot)
    {
        if (uiRoot == null)
        {
            return null;
        }

        Camera[] cameras = uiRoot.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera current = cameras[i];
            if (current == null)
            {
                continue;
            }

            UniversalAdditionalCameraData data = current.GetUniversalAdditionalCameraData();
            if (data != null && data.renderType == CameraRenderType.Overlay)
            {
                return current;
            }
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    public static void AssignWorldCamera(GameObject uiNode, Camera uiCamera)
    {
        if (uiNode == null || uiCamera == null)
        {
            return;
        }

        Canvas[] canvases = uiNode.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas.renderMode != RenderMode.ScreenSpaceCamera)
            {
                continue;
            }

            canvas.worldCamera = uiCamera;
        }
    }

    private static Camera FindBaseCamera(Scene scene, Transform excludedRoot, Camera explicitBaseCamera)
    {
        if (explicitBaseCamera != null)
        {
            return explicitBaseCamera;
        }

        Camera taggedMainCamera = Camera.main;
        if (IsValidBaseCamera(taggedMainCamera, scene, excludedRoot))
        {
            return taggedMainCamera;
        }

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (IsValidBaseCamera(cameras[i], scene, excludedRoot))
            {
                return cameras[i];
            }
        }

        return null;
    }

    private static bool IsValidBaseCamera(Camera camera, Scene scene, Transform excludedRoot)
    {
        if (camera == null || camera.transform.IsChildOf(excludedRoot))
        {
            return false;
        }

        if (scene.IsValid() && camera.gameObject.scene != scene)
        {
            return false;
        }

        UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
        if (data == null)
        {
            return true;
        }

        return data.renderType != CameraRenderType.Overlay;
    }
}
