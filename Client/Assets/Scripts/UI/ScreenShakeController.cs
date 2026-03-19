using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ScreenShakeController : MonoBehaviour
{
    [SerializeField]
    private UIChessBoard ChessBoard;

    [SerializeField]
    private RectTransform Target;

    [SerializeField]
    private bool ShakeOnMultiDestroy = true;

    [SerializeField]
    [Min(1)]
    private int MinimumDestroyedBricksForShake = ScreenShakeConstants.MinimumDestroyedBricksForShake;

    private Tween shakeTween;
    private bool isSubscribed;
    private Vector2 baseAnchoredPosition;

    private void Reset()
    {
        ChessBoard = GetComponent<UIChessBoard>();
        Target = transform as RectTransform;
        MinimumDestroyedBricksForShake = ScreenShakeConstants.MinimumDestroyedBricksForShake;
    }

    private void Awake()
    {
        EnsureReferences();
        CaptureBaseAnchoredPosition();
    }

    private void OnEnable()
    {
        EnsureReferences();
        Subscribe();
        CaptureBaseAnchoredPosition();
    }

    private void OnDisable()
    {
        Unsubscribe();
        StopShake(restoreBasePosition: true);
    }

    private void LateUpdate()
    {
        if (shakeTween == null || !shakeTween.IsActive())
        {
            CaptureBaseAnchoredPosition();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MinimumDestroyedBricksForShake = Mathf.Max(1, MinimumDestroyedBricksForShake);

        if (!Application.isPlaying)
        {
            EnsureReferences();
            CaptureBaseAnchoredPosition();
        }
    }
#endif

    public void PlayShakeForDestroyedBricks(int destroyedBrickCount)
    {
        if (destroyedBrickCount < MinimumDestroyedBricksForShake)
        {
            return;
        }

        var clampedDestroyedCount = Mathf.Clamp(
            destroyedBrickCount,
            MinimumDestroyedBricksForShake,
            ScreenShakeConstants.DestroyedBricksForMaxShake);
        var normalizedStrength = Mathf.InverseLerp(
            MinimumDestroyedBricksForShake,
            ScreenShakeConstants.DestroyedBricksForMaxShake,
            clampedDestroyedCount);
        var strength = Mathf.Lerp(
            ScreenShakeConstants.BaseShakeStrength,
            ScreenShakeConstants.MaxShakeStrength,
            normalizedStrength);
        var duration = Mathf.Lerp(
            ScreenShakeConstants.BaseShakeDuration,
            ScreenShakeConstants.MaxShakeDuration,
            normalizedStrength);

        PlayShake(strength, duration);
    }

    private void HandleBoardImpactResolved(ChessBoardImpactSummary impactSummary)
    {
        if (!ShakeOnMultiDestroy)
        {
            return;
        }

        PlayShakeForDestroyedBricks(impactSummary.DestroyedBrickCount);
    }

    private void PlayShake(float strength, float duration)
    {
        EnsureReferences();
        if (Target == null)
        {
            return;
        }

        StopShake(restoreBasePosition: true);
        CaptureBaseAnchoredPosition();

        var shakeStrength = new Vector2(strength, strength * ScreenShakeConstants.VerticalStrengthRatio);
        shakeTween = Target
            .DOShakeAnchorPos(
                duration,
                shakeStrength,
                ScreenShakeConstants.ShakeVibrato,
                ScreenShakeConstants.ShakeRandomness,
                false,
                true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                shakeTween = null;
                RestoreBaseAnchoredPosition();
            });
    }

    private void StopShake(bool restoreBasePosition)
    {
        if (shakeTween != null)
        {
            shakeTween.Kill(false);
            shakeTween = null;
        }

        if (restoreBasePosition)
        {
            RestoreBaseAnchoredPosition();
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || ChessBoard == null)
        {
            return;
        }

        ChessBoard.ImpactResolved += HandleBoardImpactResolved;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || ChessBoard == null)
        {
            return;
        }

        ChessBoard.ImpactResolved -= HandleBoardImpactResolved;
        isSubscribed = false;
    }

    private void EnsureReferences()
    {
        if (ChessBoard == null)
        {
            ChessBoard = GetComponent<UIChessBoard>();
        }

        if (Target == null)
        {
            Target = ChessBoard != null ? ChessBoard.PrimaryShakeTarget : transform as RectTransform;
        }
    }

    private void CaptureBaseAnchoredPosition()
    {
        if (Target == null)
        {
            return;
        }

        baseAnchoredPosition = Target.anchoredPosition;
    }

    private void RestoreBaseAnchoredPosition()
    {
        if (Target == null)
        {
            return;
        }

        Target.anchoredPosition = baseAnchoredPosition;
    }
}
