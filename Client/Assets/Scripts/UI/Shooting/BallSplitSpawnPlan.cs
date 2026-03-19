using UnityEngine;

public readonly struct BallSplitSpawnPlan
{
    private readonly Vector2 firstDirection;
    private readonly Vector2 secondDirection;
    private readonly Vector2 thirdDirection;

    public bool ReuseSourceProjectile { get; }
    public int DirectionCount { get; }

    public BallSplitSpawnPlan(
        bool reuseSourceProjectile,
        int directionCount,
        Vector2 firstDirection,
        Vector2 secondDirection,
        Vector2 thirdDirection)
    {
        ReuseSourceProjectile = reuseSourceProjectile;
        DirectionCount = Mathf.Clamp(directionCount, 0, BallShootingConstants.SplitProjectileCount);
        this.firstDirection = firstDirection;
        this.secondDirection = secondDirection;
        this.thirdDirection = thirdDirection;
    }

    public Vector2 GetDirection(int index)
    {
        switch (index)
        {
            case 0:
                return firstDirection;
            case 1:
                return secondDirection;
            case 2:
                return thirdDirection;
            default:
                return Vector2.up;
        }
    }
}
