using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using YooAsset;

public sealed class YooAssetUiManager : MonoBehaviour
{
    private const string SettingsResourcePath = "YooAsset/YooAssetUiSettings";

    private readonly List<LoadedScreen> loadedScreens = new List<LoadedScreen>();

    private YooAssetUiSettings settings;
    private ResourcePackage activePackage;
    private YooAssetUiSceneBinding activeBinding;
    private AssetHandle rootHandle;
    private GameObject rootInstance;
    private Camera rootUiCamera;
    private RectTransform screenLayer;
    private string activeSceneName = string.Empty;

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

    public IEnumerator LoadSceneUiRoutine(ResourcePackage package, Scene scene)
    {
        if (package == null)
        {
            Debug.LogError("[YooAsset][UI] Can not load scene UI without a resource package.");
            yield break;
        }

        EnsureSettings();
        if (settings == null)
        {
            ReleaseLoadedUi();
            yield break;
        }

        YooAssetUiSceneBinding binding;
        if (settings.TryGetSceneBinding(scene, out binding) == false || binding == null)
        {
            ReleaseLoadedUi();
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

        ReleaseLoadedUi();
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
        NormalizeRootTransform(rootInstance.transform);

        if (scene.IsValid() && scene.isLoaded && rootInstance.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(rootInstance, scene);
        }

        rootUiCamera = UiCameraStackUtility.FindPreferredUiCamera(rootInstance);
        UiCameraStackUtility.AssignWorldCamera(rootInstance, rootUiCamera);
        UiCameraStackUtility.BindUiRootToSceneCamera(rootInstance, scene);
        screenLayer = EnsureScreenLayer(binding);
        Debug.LogFormat("[YooAsset][UI] Loaded root '{0}' for scene '{1}'.", binding.RootPrefabAddress, scene.name);
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
        NormalizeRootTransform(instance.transform);
        UiCameraStackUtility.AssignWorldCamera(instance, rootUiCamera);
        loadedScreens.Add(new LoadedScreen(loadId, address, handle, instance));
        if (onLoaded != null)
        {
            onLoaded(instance);
        }

        Debug.LogFormat("[YooAsset][UI] Loaded UI '{0}' from '{1}'.", loadId, address);
    }

    private void EnsureSettings()
    {
        if (settings == null)
        {
            settings = Resources.Load<YooAssetUiSettings>(SettingsResourcePath);
        }
    }

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

    private void ReleaseLoadedUi()
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
            Debug.LogWarning("[YooAsset][UI] Base camera was not found for UI root: " + uiRoot.name);
            return false;
        }

        UniversalAdditionalCameraData baseData = baseCamera.GetUniversalAdditionalCameraData();
        UniversalAdditionalCameraData uiData = uiCamera.GetUniversalAdditionalCameraData();
        if (baseData == null || uiData == null)
        {
            Debug.LogWarning("[YooAsset][UI] URP camera data is missing while binding UI camera stack.");
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
