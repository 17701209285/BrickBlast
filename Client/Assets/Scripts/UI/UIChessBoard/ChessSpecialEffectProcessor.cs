using UnityEngine;

internal readonly struct ChessSpecialEffectResult
{
    public bool IsTriggered { get; }
    public bool PassThrough { get; }
    public bool SplitIntoThreeWay { get; }
    public Vector2 SplitOrigin { get; }
    public Vector2 SplitDirection { get; }
    public bool RedirectCurrentProjectile { get; }
    public Vector2 RedirectOrigin { get; }
    public Vector2 RedirectDirection { get; }
    public int AddedBallCount { get; }

    public ChessSpecialEffectResult(
        bool isTriggered,
        bool passThrough,
        bool splitIntoThreeWay,
        Vector2 splitOrigin,
        Vector2 splitDirection,
        bool redirectCurrentProjectile,
        Vector2 redirectOrigin,
        Vector2 redirectDirection,
        int addedBallCount)
    {
        IsTriggered = isTriggered;
        PassThrough = passThrough;
        SplitIntoThreeWay = splitIntoThreeWay;
        SplitOrigin = splitOrigin;
        SplitDirection = splitDirection;
        RedirectCurrentProjectile = redirectCurrentProjectile;
        RedirectOrigin = redirectOrigin;
        RedirectDirection = redirectDirection;
        AddedBallCount = Mathf.Max(0, addedBallCount);
    }

    public ProjectileHitEffectResult ToProjectileHitEffectResult()
    {
        return new ProjectileHitEffectResult(
            SplitIntoThreeWay,
            PassThrough,
            SplitOrigin,
            SplitDirection,
            RedirectCurrentProjectile,
            RedirectOrigin,
            RedirectDirection,
            AddedBallCount);
    }
}

internal static class ChessSpecialEffectProcessor
{
    public static ChessSpecialEffectResult TryTrigger(
        UIChessBoard board,
        ChessElement target,
        Vector2 incomingDirection,
        ChessDamageSource source,
        ChessBoardImpactAccumulator impactAccumulator,
        bool allowSplitSpecial = true)
    {
        if (board == null || target == null || !target.IsSpecialItem || source != ChessDamageSource.Projectile)
        {
            return default;
        }

        switch (target.Type)
        {
            case LevelCellType.HorizontalBlast:
                impactAccumulator?.RegisterSpecialTrigger(target.Type);
                ChessLineBlastProcessor.TriggerHorizontal(board, target, impactAccumulator);
                return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
            case LevelCellType.VerticalBlast:
                impactAccumulator?.RegisterSpecialTrigger(target.Type);
                ChessLineBlastProcessor.TriggerVertical(board, target, impactAccumulator);
                return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
            case LevelCellType.CrossBlast:
                impactAccumulator?.RegisterSpecialTrigger(target.Type);
                ChessLineBlastProcessor.TriggerCross(board, target, impactAccumulator);
                return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
            case LevelCellType.SplitThreeWay:
                if (!allowSplitSpecial)
                {
                    return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
                }

                impactAccumulator?.RegisterSpecialTrigger(target.Type);
                if (!target.TryConsumeSpecialTriggerBudget(LevelCellTypeConstants.SplitSpecialMaxTriggerCountPerVolley))
                {
                    return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
                }

                return new ChessSpecialEffectResult(
                    true,
                    true,
                    true,
                    board.GetSplitLaunchOrigin(target),
                    BallDirectionUtility.NormalizeOrFallback(incomingDirection),
                    false,
                    Vector2.zero,
                    Vector2.zero,
                    0);
            case LevelCellType.Redirect:
                impactAccumulator?.RegisterSpecialTrigger(target.Type);
                var redirectIndex = target.SpecialTriggerCountThisVolley % LevelCellTypeConstants.RedirectDirectionCount;
                if (!target.TryConsumeSpecialTriggerBudget(LevelCellTypeConstants.SplitSpecialMaxTriggerCountPerVolley))
                {
                    return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
                }

                return new ChessSpecialEffectResult(
                    true,
                    false,
                    false,
                    Vector2.zero,
                    Vector2.zero,
                    true,
                    board.GetSplitLaunchOrigin(target),
                    ChessRedirectDirectionResolver.GetRedirectDirection(incomingDirection, redirectIndex),
                    0);
            case LevelCellType.ExtraBalls:
                impactAccumulator?.RegisterSpecialTrigger(target.Type);
                if (!target.TryConsumeSpecialTriggerBudget(1))
                {
                    return new ChessSpecialEffectResult(true, true, false, Vector2.zero, Vector2.zero, false, Vector2.zero, Vector2.zero, 0);
                }

                return new ChessSpecialEffectResult(
                    true,
                    true,
                    false,
                    Vector2.zero,
                    Vector2.zero,
                    false,
                    Vector2.zero,
                    Vector2.zero,
                    target.SpecialValue);
            default:
                return default;
        }
    }
}
