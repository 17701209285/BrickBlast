using UnityEngine;

internal readonly struct ChessSpecialEffectResult
{
    public bool IsTriggered { get; }
    public bool PassThrough { get; }
    public bool SplitIntoThreeWay { get; }
    public Vector2 SplitOrigin { get; }

    public ChessSpecialEffectResult(bool isTriggered, bool passThrough, bool splitIntoThreeWay, Vector2 splitOrigin)
    {
        IsTriggered = isTriggered;
        PassThrough = passThrough;
        SplitIntoThreeWay = splitIntoThreeWay;
        SplitOrigin = splitOrigin;
    }

    public ProjectileHitEffectResult ToProjectileHitEffectResult()
    {
        return new ProjectileHitEffectResult(SplitIntoThreeWay, PassThrough, SplitOrigin);
    }
}

internal static class ChessSpecialEffectProcessor
{
    public static ChessSpecialEffectResult TryTrigger(UIChessBoard board, ChessElement target, ChessDamageSource source)
    {
        if (board == null || target == null || !target.IsSpecialItem || source != ChessDamageSource.Projectile)
        {
            return default;
        }

        switch (target.Type)
        {
            case LevelCellType.HorizontalBlast:
                ChessLineBlastProcessor.TriggerHorizontal(board, target);
                return new ChessSpecialEffectResult(true, true, false, Vector2.zero);
            case LevelCellType.VerticalBlast:
                ChessLineBlastProcessor.TriggerVertical(board, target);
                return new ChessSpecialEffectResult(true, true, false, Vector2.zero);
            case LevelCellType.SplitThreeWay:
                if (!target.TryConsumeSpecialTriggerBudget(LevelCellTypeConstants.SplitSpecialMaxTriggerCountPerVolley))
                {
                    return new ChessSpecialEffectResult(true, true, false, Vector2.zero);
                }

                return new ChessSpecialEffectResult(true, true, true, board.GetSplitLaunchOrigin(target));
            default:
                return default;
        }
    }
}
