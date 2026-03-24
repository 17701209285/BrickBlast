using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "YooAssetUiSettings", menuName = "BrickBlast/YooAsset UI Settings")]
public sealed class YooAssetUiSettings : ScriptableObject
{
    [SerializeField] private List<YooAssetUiSceneBinding> sceneBindings = new List<YooAssetUiSceneBinding>();

    public bool TryGetSceneBinding(Scene scene, out YooAssetUiSceneBinding binding)
    {
        return TryGetSceneBinding(scene.name, out binding);
    }

    public bool TryGetSceneBinding(string sceneName, out YooAssetUiSceneBinding binding)
    {
        if (string.IsNullOrWhiteSpace(sceneName) == false)
        {
            for (int i = 0; i < sceneBindings.Count; i++)
            {
                YooAssetUiSceneBinding current = sceneBindings[i];
                if (current != null && current.MatchesScene(sceneName))
                {
                    binding = current;
                    return true;
                }
            }
        }

        binding = null;
        return false;
    }
}

[Serializable]
public sealed class YooAssetUiSceneBinding
{
    [SerializeField] private string sceneName = "Main";
    [SerializeField] private string rootPrefabAddress = "Assets/AssetBundle/Prefabs/UIRoot";
    [SerializeField] private string screenLayerName = "ScreenLayer";
    [SerializeField] private List<string> startupScreenIds = new List<string> { "ui-chess-board" };
    [SerializeField] private List<YooAssetUiScreenDefinition> screens = new List<YooAssetUiScreenDefinition>();

    public string SceneName
    {
        get { return sceneName; }
    }

    public string RootPrefabAddress
    {
        get { return rootPrefabAddress; }
    }

    public string ScreenLayerName
    {
        get { return string.IsNullOrWhiteSpace(screenLayerName) ? "ScreenLayer" : screenLayerName; }
    }

    public int StartupScreenCount
    {
        get { return startupScreenIds.Count; }
    }

    public string GetStartupScreenId(int index)
    {
        if (index < 0 || index >= startupScreenIds.Count)
        {
            return string.Empty;
        }

        return startupScreenIds[index];
    }

    public bool MatchesScene(string value)
    {
        return string.Equals(sceneName, value, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetScreen(string screenId, out YooAssetUiScreenDefinition screen)
    {
        if (string.IsNullOrWhiteSpace(screenId) == false)
        {
            for (int i = 0; i < screens.Count; i++)
            {
                YooAssetUiScreenDefinition current = screens[i];
                if (current != null && current.Matches(screenId))
                {
                    screen = current;
                    return true;
                }
            }
        }

        screen = null;
        return false;
    }
}

[Serializable]
public sealed class YooAssetUiScreenDefinition
{
    [SerializeField] private string screenId = "ui-chess-board";
    [SerializeField] private string address = "Assets/AssetBundle/Prefabs/UIChessBoard";

    public string ScreenId
    {
        get { return screenId; }
    }

    public string Address
    {
        get { return address; }
    }

    public bool Matches(string value)
    {
        return string.Equals(screenId, value, StringComparison.OrdinalIgnoreCase);
    }
}
