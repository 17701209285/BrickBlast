using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;
using YooAsset;

public sealed class YooAssetImageManager : MonoBehaviour
{
    private readonly Dictionary<string, LoadedImageAsset> loadedAssets = new Dictionary<string, LoadedImageAsset>(StringComparer.OrdinalIgnoreCase);

    private ResourcePackage activePackage;

    public void SetPackage(ResourcePackage package)
    {
        activePackage = package;
    }

    public Coroutine LoadTexture(string assetId, string address, Action<Texture2D> onLoaded = null)
    {
        if (activePackage == null)
        {
            Debug.LogError("[YooAsset][Image] Package is not ready for loading textures.");
            return null;
        }

        return StartCoroutine(LoadTextureRoutine(assetId, address, onLoaded));
    }

    public Coroutine LoadSprite(string assetId, string address, Action<Sprite> onLoaded = null)
    {
        if (activePackage == null)
        {
            Debug.LogError("[YooAsset][Image] Package is not ready for loading sprites.");
            return null;
        }

        return StartCoroutine(LoadSpriteRoutine(assetId, address, onLoaded));
    }

    public Coroutine LoadAtlasSprite(string atlasId, string atlasAddress, string spriteName, Action<Sprite> onLoaded = null)
    {
        if (activePackage == null)
        {
            Debug.LogError("[YooAsset][Image] Package is not ready for loading atlas sprites.");
            return null;
        }

        return StartCoroutine(LoadAtlasSpriteRoutine(atlasId, atlasAddress, spriteName, onLoaded));
    }

    public Coroutine LoadTextureToRawImage(string assetId, string address, RawImage target, bool setNativeSize = false, Action<Texture2D> onLoaded = null)
    {
        if (target == null)
        {
            Debug.LogError("[YooAsset][Image] RawImage target is null.");
            return null;
        }

        return LoadTexture(assetId, address, texture =>
        {
            if (target == null)
            {
                return;
            }

            target.texture = texture;
            if (setNativeSize)
            {
                target.SetNativeSize();
            }

            onLoaded?.Invoke(texture);
        });
    }

    public Coroutine LoadSpriteToImage(string assetId, string address, Image target, bool setNativeSize = false, Action<Sprite> onLoaded = null)
    {
        if (target == null)
        {
            Debug.LogError("[YooAsset][Image] Image target is null.");
            return null;
        }

        return LoadSprite(assetId, address, sprite =>
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
            if (setNativeSize)
            {
                target.SetNativeSize();
            }

            onLoaded?.Invoke(sprite);
        });
    }

    public Coroutine LoadAtlasSpriteToImage(string atlasId, string atlasAddress, string spriteName, Image target, bool setNativeSize = false, Action<Sprite> onLoaded = null)
    {
        if (target == null)
        {
            Debug.LogError("[YooAsset][Image] Image target is null.");
            return null;
        }

        return LoadAtlasSprite(atlasId, atlasAddress, spriteName, sprite =>
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
            if (setNativeSize)
            {
                target.SetNativeSize();
            }

            onLoaded?.Invoke(sprite);
        });
    }

    public bool ReleaseAsset(string assetId)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return false;
        }

        return ReleaseLoadedAsset(assetId);
    }

    public void ReleaseAllAssets()
    {
        List<string> keys = new List<string>(loadedAssets.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            ReleaseLoadedAsset(keys[i]);
        }
    }

    private void OnDestroy()
    {
        ReleaseAllAssets();
    }

    private IEnumerator LoadTextureRoutine(string assetId, string address, Action<Texture2D> onLoaded)
    {
        yield return LoadAssetRoutine<Texture2D>(assetId, address, LoadedImageAssetKind.Texture, onLoaded);
    }

    private IEnumerator LoadSpriteRoutine(string assetId, string address, Action<Sprite> onLoaded)
    {
        yield return LoadAssetRoutine<Sprite>(assetId, address, LoadedImageAssetKind.Sprite, onLoaded);
    }

    private IEnumerator LoadAtlasSpriteRoutine(string atlasId, string atlasAddress, string spriteName, Action<Sprite> onLoaded)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            Debug.LogError("[YooAsset][Image] Atlas sprite name is empty.");
            yield break;
        }

        SpriteAtlas atlas = null;
        yield return LoadAssetRoutine<SpriteAtlas>(atlasId, atlasAddress, LoadedImageAssetKind.Atlas, loadedAtlas => atlas = loadedAtlas);
        if (atlas == null)
        {
            yield break;
        }

        Sprite sprite = atlas.GetSprite(spriteName);
        if (sprite == null)
        {
            Debug.LogErrorFormat("[YooAsset][Image] Sprite '{0}' was not found in atlas '{1}'.", spriteName, atlasAddress);
            yield break;
        }

        onLoaded?.Invoke(sprite);
    }

    private IEnumerator LoadAssetRoutine<T>(string assetId, string address, LoadedImageAssetKind kind, Action<T> onLoaded) where T : UnityEngine.Object
    {
        string loadId = string.IsNullOrWhiteSpace(assetId) ? address : assetId;
        if (string.IsNullOrWhiteSpace(loadId))
        {
            Debug.LogError("[YooAsset][Image] Asset load id is empty.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            Debug.LogError("[YooAsset][Image] Asset address is empty: " + loadId);
            yield break;
        }

        LoadedImageAsset cachedAsset;
        if (loadedAssets.TryGetValue(loadId, out cachedAsset))
        {
            if (cachedAsset.Kind == kind && string.Equals(cachedAsset.Address, address, StringComparison.OrdinalIgnoreCase))
            {
                T cachedObject = cachedAsset.Asset as T;
                if (cachedObject != null)
                {
                    onLoaded?.Invoke(cachedObject);
                    yield break;
                }
            }

            ReleaseLoadedAsset(loadId);
        }

        AssetHandle handle = activePackage.LoadAssetAsync<T>(address);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeed)
        {
            handle.Release();
            Debug.LogError("[YooAsset][Image] Load asset failed: " + handle.LastError);
            yield break;
        }

        T asset = handle.GetAssetObject<T>();
        if (asset == null)
        {
            handle.Release();
            Debug.LogError("[YooAsset][Image] Loaded asset type mismatch: " + address);
            yield break;
        }

        loadedAssets[loadId] = new LoadedImageAsset(loadId, address, kind, handle, asset);
        onLoaded?.Invoke(asset);
        GameLog.InfoFormat("[YooAsset][Image] Loaded {0} '{1}' from '{2}'.", kind, loadId, address);
    }

    private bool ReleaseLoadedAsset(string assetId)
    {
        LoadedImageAsset loadedAsset;
        if (loadedAssets.TryGetValue(assetId, out loadedAsset) == false)
        {
            return false;
        }

        if (loadedAsset.Handle != null && loadedAsset.Handle.IsValid)
        {
            loadedAsset.Handle.Release();
        }

        loadedAssets.Remove(assetId);
        return true;
    }

    private readonly struct LoadedImageAsset
    {
        public readonly string Id;
        public readonly string Address;
        public readonly LoadedImageAssetKind Kind;
        public readonly AssetHandle Handle;
        public readonly UnityEngine.Object Asset;

        public LoadedImageAsset(string id, string address, LoadedImageAssetKind kind, AssetHandle handle, UnityEngine.Object asset)
        {
            Id = id;
            Address = address;
            Kind = kind;
            Handle = handle;
            Asset = asset;
        }
    }

    private enum LoadedImageAssetKind
    {
        Texture,
        Sprite,
        Atlas
    }
}
