using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using YooAsset;

public sealed class YooAssetSpriteAtlasBridge : MonoBehaviour
{
    private readonly Dictionary<string, LoadedAtlas> loadedAtlases = new Dictionary<string, LoadedAtlas>(StringComparer.OrdinalIgnoreCase);

    private ResourcePackage activePackage;

    private void Awake()
    {
        SpriteAtlasManager.atlasRequested += HandleAtlasRequested;
    }

    private void OnDestroy()
    {
        SpriteAtlasManager.atlasRequested -= HandleAtlasRequested;

        foreach (var pair in loadedAtlases)
        {
            if (pair.Value.Handle != null && pair.Value.Handle.IsValid)
            {
                pair.Value.Handle.Release();
            }
        }

        loadedAtlases.Clear();
    }

    public void SetPackage(ResourcePackage package)
    {
        activePackage = package;
    }

    private void HandleAtlasRequested(string atlasName, Action<SpriteAtlas> callback)
    {
        if (string.IsNullOrWhiteSpace(atlasName) || callback == null)
        {
            return;
        }

        if (loadedAtlases.TryGetValue(atlasName, out var loadedAtlas))
        {
            callback.Invoke(loadedAtlas.Atlas);
            return;
        }

        if (activePackage == null)
        {
            Debug.LogWarning("[YooAsset][Atlas] Package is not ready for atlas request: " + atlasName);
            return;
        }

        if (!TryResolveAtlasLocation(activePackage, atlasName, out var atlasAddress))
        {
            Debug.LogWarning("[YooAsset][Atlas] Failed to resolve atlas location: " + atlasName);
            return;
        }

        AssetHandle handle = activePackage.LoadAssetSync<SpriteAtlas>(atlasAddress);
        if (handle.Status != EOperationStatus.Succeed)
        {
            Debug.LogWarning("[YooAsset][Atlas] Failed to load atlas: " + atlasAddress + " Error: " + handle.LastError);
            if (handle.IsValid)
            {
                handle.Release();
            }

            return;
        }

        SpriteAtlas atlas = handle.GetAssetObject<SpriteAtlas>();
        if (atlas == null)
        {
            Debug.LogWarning("[YooAsset][Atlas] Loaded asset is not a SpriteAtlas: " + atlasAddress);
            if (handle.IsValid)
            {
                handle.Release();
            }

            return;
        }

        loadedAtlases[atlasName] = new LoadedAtlas(atlasAddress, handle, atlas);
        callback.Invoke(atlas);
    }

    private static bool TryResolveAtlasLocation(ResourcePackage package, string atlasName, out string atlasAddress)
    {
        atlasAddress = string.Empty;
        if (package == null || string.IsNullOrWhiteSpace(atlasName))
        {
            return false;
        }

        string atlasFileName = atlasName.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase)
            ? atlasName
            : atlasName + ".spriteatlas";

        string[] candidates =
        {
            atlasName,
            atlasFileName,
            "Assets/AssetBundle/UIAtlas/" + atlasName,
            "Assets/AssetBundle/UIAtlas/" + atlasFileName
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (package.CheckLocationValid(candidate))
            {
                atlasAddress = candidate;
                return true;
            }
        }

        return false;
    }

    private readonly struct LoadedAtlas
    {
        public readonly string Address;
        public readonly AssetHandle Handle;
        public readonly SpriteAtlas Atlas;

        public LoadedAtlas(string address, AssetHandle handle, SpriteAtlas atlas)
        {
            Address = address;
            Handle = handle;
            Atlas = atlas;
        }
    }
}
