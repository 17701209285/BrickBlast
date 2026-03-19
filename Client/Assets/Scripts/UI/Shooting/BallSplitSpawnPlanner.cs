using UnityEngine;

public static class BallSplitSpawnPlanner
{
    public static BallSplitSpawnPlan CreatePlan(int activeProjectileCount, int maxRuntimeProjectileCount, float splitFanHalfAngle, Vector2 baseDirection)
    {
        var normalizedBaseDirection = BallDirectionUtility.NormalizeOrFallback(baseDirection);
        var leftDirection = BallDirectionUtility.Rotate(normalizedBaseDirection, -splitFanHalfAngle);
        var centerDirection = normalizedBaseDirection;
        var rightDirection = BallDirectionUtility.Rotate(normalizedBaseDirection, splitFanHalfAngle);

        var projectedProjectileCount = activeProjectileCount + BallShootingConstants.SplitProjectileAdditionalCount;
        if (projectedProjectileCount > Mathf.Max(1, maxRuntimeProjectileCount))
        {
            var directionIndex = Random.Range(0, BallShootingConstants.SplitProjectileCount);
            var selectedDirection = GetDirectionByIndex(directionIndex, leftDirection, centerDirection, rightDirection);
            return new BallSplitSpawnPlan(true, 1, selectedDirection, Vector2.zero, Vector2.zero);
        }

        return new BallSplitSpawnPlan(
            false,
            BallShootingConstants.SplitProjectileCount,
            leftDirection,
            centerDirection,
            rightDirection);
    }

    private static Vector2 GetDirectionByIndex(int index, Vector2 leftDirection, Vector2 centerDirection, Vector2 rightDirection)
    {
        switch (index)
        {
            case 0:
                return leftDirection;
            case 1:
                return centerDirection;
            default:
                return rightDirection;
        }
    }

}
