using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DefaultChessHitEffect : MonoBehaviour, IChessHitEffectPlayer
{
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

    private Image image;
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
        Debug.Log(
            $"[ChessHitEffect] Target={context.Target?.name ?? "null"} Source={context.DamageSource} Damage={context.Damage} PrevLife={context.PreviousLife} CurrentLife={context.CurrentLife} Destroyed={context.IsDestroyed}",
            this);
        CacheComponents();
        StopTweens();

        var totalDuration = context.IsDestroyed ? DestroyedDuration : HitDuration;
        var isBlastDamage =
            context.DamageSource == ChessDamageSource.HorizontalBlast
            || context.DamageSource == ChessDamageSource.VerticalBlast
            || context.DamageSource == ChessDamageSource.CrossBlast;

        if (image == null)
        {
            return;
        }

        var targetColor = image.color;
        var baseColor = context.PreHitColor.a > 0f ? context.PreHitColor : targetColor;
        var flashStrength = isBlastDamage
            ? Mathf.Clamp01(FlashStrength * LevelCellTypeConstants.BlastHitEffectFlashMultiplier)
            : FlashStrength;
        var flashColor = Color.Lerp(baseColor, Color.white, flashStrength);
        image.color = flashColor;
        colorTween = image
            .DOColor(targetColor, totalDuration)
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => colorTween = null);
    }

    private void CacheComponents()
    {
        if (image == null)
        {
            image = GetComponent<Image>();
        }
    }

    private void StopTweens()
    {
        if (colorTween != null)
        {
            colorTween.Kill(false);
            colorTween = null;
        }
    }
}
