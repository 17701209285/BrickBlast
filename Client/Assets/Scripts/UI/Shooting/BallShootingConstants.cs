public static class BallShootingConstants
{
    public const float DefaultBallSpeed = 2000f;
    public const float DefaultLaunchInterval = 0.03f;
    public const float DefaultSplitFanHalfAngle = 32f;
    public const float DefaultCollisionRadiusScale = 1f;
    public const float DefaultCollisionSkin = 1f;
    public const float DefaultSimulationStep = 1f / 120f;
    public const int DefaultMaxCollisionsPerStep = 5;
    public const float DefaultFallbackSubstepDistance = 6f;
    public const float DefaultFallbackWidth = 1080f;
    public const float DefaultFallbackHeight = 1920f;
    public const float DefaultBallRadius = 25f;
    public const int MinimumWarmProjectileCount = 96;
    public const int ProjectileWarmupMultiplier = 2;
    public const int DefaultMaxRuntimeProjectileCount = 180;
    public const int SplitProjectileCount = 3;
    public const int SplitProjectileAdditionalCount = SplitProjectileCount - 1;
    public const float PassThroughOffsetRadiusRatio = 0.25f;
}
