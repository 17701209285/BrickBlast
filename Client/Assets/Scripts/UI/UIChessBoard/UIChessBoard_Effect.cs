using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static UnityEngine.UI.Image;

public partial class UIChessBoard : MonoBehaviour
{
    private const string EffectClipShaderName = "BrickBlast/Particles/Rect Clip";
    private sealed class CachedEffectInstance
    {
        public string CacheKey;
        public GameObject Prefab;
        public GameObject Instance;
        public ParticleSystem[] ParticleSystems;
        public ParticleSystemRenderer[] ParticleRenderers;
        public Material[] RuntimeMaterials;
        public MaterialPropertyBlock PropertyBlock;
        public Graphic Graphic;
        public float ReleaseAt;
    }

    private const float DefaultEffectLifetime = 2f;
    private static readonly Vector3 DefaultEffectScale = new Vector3(0.5f, 0.5f, 0.5f);
    private static readonly int UseClipRectShaderId = Shader.PropertyToID("_UseClipRect");
    private static readonly int ClipRectLocalShaderId = Shader.PropertyToID("_ClipRectLocal");
    private static readonly int ClipWorldToLocalShaderId = Shader.PropertyToID("_ClipWorldToLocal");
    private static readonly Vector4 DisabledClipRect = new Vector4(-99999f, -99999f, 99999f, 99999f);
    private static readonly Matrix4x4 IdentityClipMatrix = Matrix4x4.identity;

    private readonly Dictionary<string, CachedEffectInstance> cachedEffects = new Dictionary<string, CachedEffectInstance>();
    private readonly List<CachedEffectInstance> activeEffects = new List<CachedEffectInstance>(8);

    public GameEffectsScriptable Effects;

    [SerializeField]
    private RectTransform GameOverEffectParent;

    [SerializeField]
    private RectTransform EffectClipRect;

    [SerializeField]
    private bool UseCustomGameOverEffectPosition;

    [SerializeField]
    private Vector2 GameOverEffectPosition;

    public void PlayHorizontalEffect(ChessElement origin)
    {
        PlayEffect(GlobleValue.EFFECT_HORIZONTAL, origin, 0f);
    }

    public void PlayVerticalEffect(ChessElement origin)
    {
        if (PlayEffect(GlobleValue.EFFECT_VERTICAL, origin, 0f))
        {
            return;
        }

        PlayEffect(GlobleValue.EFFECT_HORIZONTAL, origin, 90f);
    }

    public void PlayCrossEffect(ChessElement origin)
    {
        PlayEffect(GlobleValue.EFFECT_CROSS, origin, 0f);
    }

    public void PlayGameOverEffect()
    {
        PlayEffect(
            GlobleValue.EFFECT_COMPLETE,
            null,
            0f,
            GameOverEffectParent,
            UseCustomGameOverEffectPosition ? (Vector2?)GameOverEffectPosition : null);
    }

    public void PlayGameOverEffect(RectTransform parent, Vector2 anchoredPosition)
    {
        PlayEffect(GlobleValue.EFFECT_COMPLETE, null, 0f, parent, anchoredPosition);
    }

    private void Update()
    {
        if (activeEffects.Count == 0)
        {
            return;
        }

        var now = Time.unscaledTime;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            var effect = activeEffects[i];
            if (effect == null || effect.Instance == null || now < effect.ReleaseAt)
            {
                continue;
            }

            ReleaseEffect(effect, i);
        }
    }

    private bool PlayEffect(
        string effectName,
        ChessElement origin,
        float rotationZ,
        RectTransform parentOverride = null,
        Vector2? anchoredPositionOverride = null)
    {
        if (Effects == null)
        {
            return false;
        }

        if (!Effects.GetEffect(effectName, out var effectPrefab) || effectPrefab == null)
        {
            return false;
        }

        var cachedEffect = AcquireEffect(effectName, rotationZ, effectPrefab, origin);
        var effectInstance = cachedEffect.Instance;
        effectInstance.name = effectPrefab.name;
        effectInstance.SetActive(true);

        var targetParent = ResolveEffectParent(origin, parentOverride);
        var anchoredPosition = ResolveEffectPosition(origin, targetParent, anchoredPositionOverride);
        var localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        if (effectInstance.TryGetComponent(out RectTransform effectRect) && targetParent != null)
        {
            effectRect.SetParent(targetParent, false);
            effectRect.anchoredPosition = anchoredPosition;
            effectRect.localRotation = localRotation;
            effectRect.localScale = DefaultEffectScale;
        }
        else
        {
            effectInstance.transform.SetParent(targetParent == null ? transform : targetParent, false);
            effectInstance.transform.localPosition = new Vector3(anchoredPosition.x, anchoredPosition.y, 0f);
            effectInstance.transform.localRotation = localRotation;
            effectInstance.transform.localScale = DefaultEffectScale;
        }

        ApplyClipRect(cachedEffect, ResolveEffectClipRect(targetParent));

        if (cachedEffect.Graphic != null)
        {
            cachedEffect.Graphic.raycastTarget = false;
        }

        var particleSystems = cachedEffect.ParticleSystems;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            particleSystems[i].Clear(true);
            particleSystems[i].Play(true);
        }

        cachedEffect.ReleaseAt = Time.unscaledTime + CalculateEffectLifetime(particleSystems);
        if (!activeEffects.Contains(cachedEffect))
        {
            activeEffects.Add(cachedEffect);
        }

        return true;
    }

    private RectTransform ResolveEffectParent(ChessElement origin, RectTransform parentOverride)
    {
        if (origin != null)
        {
            return transform as RectTransform;
        }

        if (parentOverride != null)
        {
            return parentOverride;
        }

        return transform as RectTransform;
    }

    private RectTransform ResolveEffectClipRect(RectTransform targetParent)
    {
        if (EffectClipRect != null)
        {
            return EffectClipRect;
        }

        return null;
    }

    private Vector2 ResolveEffectPosition(ChessElement origin, RectTransform targetParent, Vector2? anchoredPositionOverride)
    {
        if (anchoredPositionOverride.HasValue)
        {
            return anchoredPositionOverride.Value;
        }

        if (origin != null)
        {
            return GetElementCenterInBoardSpace(origin);
        }

        if (targetParent == null || targetParent == transform as RectTransform)
        {
            return GetBoardEffectCenterInBoardSpace();
        }

        return targetParent.rect.center;
    }

    private Vector2 GetBoardEffectCenterInBoardSpace()
    {
        if (transform is RectTransform boardRectTransform)
        {
            return boardRectTransform.rect.center;
        }

        return Vector2.zero;
    }

    private CachedEffectInstance AcquireEffect(string effectName, float rotationZ, GameObject effectPrefab, ChessElement origin)
    {
        var cacheKey = BuildEffectCacheKey(effectName, rotationZ, origin);
        if (cachedEffects.TryGetValue(cacheKey, out var cachedEffect) && cachedEffect != null && cachedEffect.Instance != null)
        {
            return cachedEffect;
        }

        var instance = Instantiate(effectPrefab, transform, false);
        instance.SetActive(false);
        cachedEffect = new CachedEffectInstance
        {
            CacheKey = cacheKey,
            Prefab = effectPrefab,
            Instance = instance,
            ParticleSystems = instance.GetComponentsInChildren<ParticleSystem>(true),
            ParticleRenderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true),
            Graphic = instance.GetComponent<Graphic>()
        };
        PrepareRuntimeClipMaterials(cachedEffect);
        cachedEffects[cacheKey] = cachedEffect;
        return cachedEffect;
    }

    private void ReleaseEffect(CachedEffectInstance effect, int activeIndex)
    {
        if (effect.Instance != null)
        {
            for (int i = 0; i < effect.ParticleSystems.Length; i++)
            {
                effect.ParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            effect.Instance.SetActive(false);
        }

        activeEffects.RemoveAt(activeIndex);
    }

    private static string BuildEffectCacheKey(string effectName, float rotationZ, ChessElement origin)
    {
        var ownerKey = origin == null ? "global" : origin.GetInstanceID().ToString();
        return string.Concat(effectName, "@", Mathf.RoundToInt(rotationZ), "@", ownerKey);
    }

    private void PrepareRuntimeClipMaterials(CachedEffectInstance effect)
    {
        if (effect == null || effect.ParticleRenderers == null || effect.ParticleRenderers.Length == 0)
        {
            return;
        }

        var clipShader = Shader.Find(EffectClipShaderName);
        if (clipShader == null)
        {
            return;
        }

        var runtimeMaterials = new List<Material>(effect.ParticleRenderers.Length * 2);
        for (int rendererIndex = 0; rendererIndex < effect.ParticleRenderers.Length; rendererIndex++)
        {
            var particleRenderer = effect.ParticleRenderers[rendererIndex];
            if (particleRenderer == null)
            {
                continue;
            }

            var sharedMaterials = particleRenderer.sharedMaterials;
            if (sharedMaterials == null || sharedMaterials.Length == 0)
            {
                continue;
            }

            var clippedMaterials = new Material[sharedMaterials.Length];
            bool hasValidMaterial = false;
            for (int materialIndex = 0; materialIndex < sharedMaterials.Length; materialIndex++)
            {
                var sharedMaterial = sharedMaterials[materialIndex];
                if (sharedMaterial == null)
                {
                    continue;
                }

                var clippedMaterial = new Material(clipShader)
                {
                    name = sharedMaterial.name + " (Rect Clip)"
                };
                clippedMaterial.CopyPropertiesFromMaterial(sharedMaterial);
                clippedMaterials[materialIndex] = clippedMaterial;
                runtimeMaterials.Add(clippedMaterial);
                hasValidMaterial = true;
            }

            if (hasValidMaterial)
            {
                particleRenderer.sharedMaterials = clippedMaterials;
            }
        }

        effect.RuntimeMaterials = runtimeMaterials.ToArray();
        effect.PropertyBlock = new MaterialPropertyBlock();
    }

    private void ApplyClipRect(CachedEffectInstance effect, RectTransform clipRectTransform)
    {
        if (effect == null || effect.ParticleRenderers == null || effect.ParticleRenderers.Length == 0)
        {
            return;
        }

        bool useClipRect = clipRectTransform != null;
        Vector4 clipRectLocal = DisabledClipRect;
        Matrix4x4 clipWorldToLocal = IdentityClipMatrix;
        if (useClipRect)
        {
            var rect = clipRectTransform.rect;
            clipRectLocal = new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax);
            clipWorldToLocal = clipRectTransform.worldToLocalMatrix;
        }

        var propertyBlock = effect.PropertyBlock ?? (effect.PropertyBlock = new MaterialPropertyBlock());
        for (int i = 0; i < effect.ParticleRenderers.Length; i++)
        {
            var particleRenderer = effect.ParticleRenderers[i];
            if (particleRenderer == null)
            {
                continue;
            }

            particleRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(UseClipRectShaderId, useClipRect ? 1f : 0f);
            propertyBlock.SetVector(ClipRectLocalShaderId, clipRectLocal);
            propertyBlock.SetMatrix(ClipWorldToLocalShaderId, clipWorldToLocal);
            particleRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDestroy()
    {
        foreach (var cachedEffect in cachedEffects.Values)
        {
            if (cachedEffect == null)
            {
                continue;
            }

            if (cachedEffect.RuntimeMaterials != null)
            {
                for (int i = 0; i < cachedEffect.RuntimeMaterials.Length; i++)
                {
                    if (cachedEffect.RuntimeMaterials[i] != null)
                    {
                        Destroy(cachedEffect.RuntimeMaterials[i]);
                    }
                }
            }
        }
    }

    private static float CalculateEffectLifetime(ParticleSystem[] particleSystems)
    {
        if (particleSystems == null || particleSystems.Length == 0)
        {
            return DefaultEffectLifetime;
        }

        float lifetime = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var main = particleSystems[i].main;
            if (main.loop)
            {
                lifetime = Mathf.Max(lifetime, DefaultEffectLifetime);
                continue;
            }

            lifetime = Mathf.Max(
                lifetime,
                main.duration + main.startDelay.constantMax + main.startLifetime.constantMax);
        }

        return Mathf.Max(DefaultEffectLifetime, lifetime + 0.2f);
    }
}
