using UnityEngine;

public static class BallDirectionUtility
{
    public static Vector2 NormalizeOrFallback(Vector2 direction)
    {
        return direction.sqrMagnitude <= Mathf.Epsilon ? Vector2.up : direction.normalized;
    }

    public static Vector2 Rotate(Vector2 vector, float angleDegrees)
    {
        var normalized = NormalizeOrFallback(vector);
        var radians = angleDegrees * Mathf.Deg2Rad;
        var sin = Mathf.Sin(radians);
        var cos = Mathf.Cos(radians);
        return new Vector2(
            (normalized.x * cos) - (normalized.y * sin),
            (normalized.x * sin) + (normalized.y * cos)).normalized;
    }
}
