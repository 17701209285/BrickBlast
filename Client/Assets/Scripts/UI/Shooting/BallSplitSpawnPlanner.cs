using UnityEngine;

public static class BallSplitSpawnPlanner
{
    public static BallSplitSpawnPlan CreatePlan(int activeProjectileCount, int maxRuntimeProjectileCount, float splitFanHalfAngle)
    {
        var leftDirection = Rotate(Vector2.up, -splitFanHalfAngle);
        var centerDirection = Vector2.up;
        var rightDirection = Rotate(Vector2.up, splitFanHalfAngle);

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

    private static Vector2 Rotate(Vector2 vector, float angleDegrees)
    {
        var radians = angleDegrees * Mathf.Deg2Rad;
        var sin = Mathf.Sin(radians);
        var cos = Mathf.Cos(radians);
        return new Vector2(
            (vector.x * cos) - (vector.y * sin),
            (vector.x * sin) + (vector.y * cos));
    }
}
