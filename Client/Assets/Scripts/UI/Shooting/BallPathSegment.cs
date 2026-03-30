using UnityEngine;

/// <summary>
/// A single ballistic segment between the current ball position and the next resolved hit.
/// The projectile only recalculates when it reaches the end of this segment.
/// </summary>
public readonly struct BallPathSegment
{
    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public float Distance { get; }
    public Vector2 NextDirection { get; }
    public BallCollisionHit Hit { get; }

    public Vector2 EndPoint => Origin + (Direction * Distance);

    public BallPathSegment(
        Vector2 origin,
        Vector2 direction,
        float distance,
        Vector2 nextDirection,
        in BallCollisionHit hit)
    {
        Origin = origin;
        Direction = direction.normalized;
        Distance = Mathf.Max(0f, distance);
        NextDirection = nextDirection.normalized;
        Hit = hit;
    }

    public Vector2 GetPoint(float travelledDistance)
    {
        return Origin + (Direction * Mathf.Clamp(travelledDistance, 0f, Distance));
    }
}
