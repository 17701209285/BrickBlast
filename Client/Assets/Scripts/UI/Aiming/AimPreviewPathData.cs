using UnityEngine;

public enum AimBoundaryType
{
    None = 0,
    Left = 1,
    Right = 2,
    Top = 3,
    Bottom = 4
}

public struct AimPreviewSegment
{
    public Vector2 StartPoint;
    public Vector2 EndPoint;
    public bool HitBoundary;
    public Vector2 BoundaryHitPoint;
    public AimBoundaryType BoundaryType;

    public AimPreviewSegment(
        Vector2 startPoint,
        Vector2 endPoint,
        bool hitBoundary,
        Vector2 boundaryHitPoint,
        AimBoundaryType boundaryType)
    {
        StartPoint = startPoint;
        EndPoint = endPoint;
        HitBoundary = hitBoundary;
        BoundaryHitPoint = boundaryHitPoint;
        BoundaryType = boundaryType;
    }
}

public struct AimPreviewPath
{
    public AimPreviewSegment PrimarySegment;
    public AimPreviewSegment ReflectionSegment;
    public bool HasReflectionSegment;
}

public struct AimPreviewImpactData
{
    public bool HasBlockImpact;
    public bool IsReflectionImpact;
    public Vector2 BlockImpactCenterPoint;

    public AimPreviewImpactData(bool hasBlockImpact, bool isReflectionImpact, Vector2 blockImpactCenterPoint)
    {
        HasBlockImpact = hasBlockImpact;
        IsReflectionImpact = isReflectionImpact;
        BlockImpactCenterPoint = blockImpactCenterPoint;
    }
}
