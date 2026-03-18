using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DefaultChessHitEffect : MonoBehaviour, IChessHitEffectPlayer
{
    [SerializeField]
    [Min(1f)]
    private float HitScale = 1.12f;

    [SerializeField]
    [Min(1f)]
    private float DestroyedScale = 1.18f;

    [SerializeField]
    private bool EnableScaleEffect = false;

    [SerializeField]
    [Min(0.01f)]
    private float HitDuration = 0.12f;

    [SerializeField]
    [Min(0.01f)]
    private float DestroyedDuration = 0.16f;

    [SerializeField]
    [Range(0f, 1f)]
    private float FlashStrength = 0.35f;

    [SerializeField]
    [Min(0f)]
    private float MinHitEffectInterval = 0.04f;

    private RectTransform selfRectTransform;
    private Image image;
    private Tween scaleTween;
    private Tween colorTween;
    private float lastHitEffectTime = -999f;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnDisable()
    {
        StopTweens();
    }

    private void OnDestroy()
    {
        StopTweens();
    }

    public void PlayHitEffect(in ChessHitEffectContext context)
    {
        var now = Time.unscaledTime;
        if (!context.IsDestroyed && now - lastHitEffectTime < MinHitEffectInterval)
        {
            return;
        }

        lastHitEffectTime = now;
        CacheComponents();
        StopTweens();

        // 中文备注：这里故意只放一个轻量默认效果，
        // 后面你可以直接删掉这个组件，换成粒子、Shader 或 Spine。
        var totalDuration = context.IsDestroyed ? DestroyedDuration : HitDuration;
        var targetScale = context.IsDestroyed ? DestroyedScale : HitScale;

        if (EnableScaleEffect && selfRectTransform != null && !Mathf.Approximately(targetScale, 1f))
        {
            selfRectTransform.localScale = Vector3.one;
            scaleTween = selfRectTransform
                .DOScale(targetScale, totalDuration * 0.5f)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    scaleTween = selfRectTransform
                        .DOScale(1f, totalDuration * 0.5f)
                        .SetEase(Ease.InQuad)
                        .SetLink(gameObject)
                        .OnComplete(() => scaleTween = null);
                });
        }

        if (image == null || context.IsDestroyed)
        {
            return;
        }

        var baseColor = image.color;
        var flashColor = Color.Lerp(baseColor, Color.white, FlashStrength);
        image.color = flashColor;
        colorTween = image
            .DOColor(baseColor, totalDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => colorTween = null);
    }

    private void CacheComponents()
    {
        if (selfRectTransform == null)
        {
            selfRectTransform = GetComponent<RectTransform>();
        }

        if (image == null)
        {
            image = GetComponent<Image>();
        }
    }

    private void StopTweens()
    {
        if (scaleTween != null)
        {
            scaleTween.Kill(false);
            scaleTween = null;
        }

        if (colorTween != null)
        {
            colorTween.Kill(false);
            colorTween = null;
        }

        if (selfRectTransform != null)
        {
            selfRectTransform.localScale = Vector3.one;
        }
    }
}
