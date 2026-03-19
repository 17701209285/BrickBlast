using UnityEngine;

internal static class ChessRedirectDirectionResolver
{
    public static Vector2 GetRedirectDirection(Vector2 incomingDirection, int redirectIndex)
    {
        var baseDirection = BallDirectionUtility.NormalizeOrFallback(incomingDirection);
        if (baseDirection.y <= 0f)
        {
            baseDirection = new Vector2(baseDirection.x, Mathf.Abs(baseDirection.y));
            baseDirection = BallDirectionUtility.NormalizeOrFallback(baseDirection);
        }

        switch (Mathf.Abs(redirectIndex) % LevelCellTypeConstants.RedirectDirectionCount)
        {
            case 0:
                return BallDirectionUtility.Rotate(baseDirection, -LevelCellTypeConstants.RedirectAngleStep);
            case 1:
                return baseDirection;
            default:
                return BallDirectionUtility.Rotate(baseDirection, LevelCellTypeConstants.RedirectAngleStep);
        }
    }
}
