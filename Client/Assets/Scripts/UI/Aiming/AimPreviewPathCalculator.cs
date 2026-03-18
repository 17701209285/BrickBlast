using UnityEngine;

public static class AimPreviewPathCalculator
{
    private const float HitEpsilon = 0.01f;

    public static bool TryBuildOneBouncePath(
        Rect bounds,
        Vector2 origin,
        Vector2 direction,
        float previewLength,
        out AimPreviewPath previewPath)
    {
        previewPath = default;

        if (direction.sqrMagnitude <= Mathf.Epsilon || previewLength <= 0f)
        {
            return false;
        }

        var normalizedDirection = direction.normalized;
        if (!TryGetBoundaryHit(bounds, origin, normalizedDirection, out var firstHit, out var firstHitDistance, out var firstBoundaryType))
        {
            previewPath.PrimarySegment = new AimPreviewSegment(
                origin,
                origin + (normalizedDirection * previewLength),
                false,
                origin,
                AimBoundaryType.None);
            return true;
        }

        previewPath.PrimarySegment = new AimPreviewSegment(
            origin,
            firstHit,
            true,
            firstHit,
            firstBoundaryType);

        var remainingLength = Mathf.Max(0f, previewLength - firstHitDistance);
        if (remainingLength <= HitEpsilon || !IsVerticalBoundary(firstBoundaryType))
        {
            return true;
        }

        var reflectionDirection = new Vector2(-normalizedDirection.x, normalizedDirection.y).normalized;
        var reflectionStart = firstHit + (reflectionDirection * HitEpsilon);
        if (TryGetBoundaryHit(bounds, reflectionStart, reflectionDirection, out var reflectionHit, out _, out var reflectionBoundaryType))
        {
            previewPath.ReflectionSegment = new AimPreviewSegment(
                firstHit,
                reflectionHit,
                true,
                reflectionHit,
                reflectionBoundaryType);
            previewPath.HasReflectionSegment = true;
            return true;
        }

        previewPath.ReflectionSegment = new AimPreviewSegment(
            firstHit,
            reflectionStart + (reflectionDirection * remainingLength),
            false,
            firstHit,
            AimBoundaryType.None);
        previewPath.HasReflectionSegment = true;
        return true;
    }

    private static bool TryGetBoundaryHit(
        Rect bounds,
        Vector2 origin,
        Vector2 direction,
        out Vector2 hitPoint,
        out float hitDistance,
        out AimBoundaryType boundaryType)
    {
        hitPoint = origin;
        hitDistance = 0f;
        boundaryType = AimBoundaryType.None;

        var nearestDistance = float.MaxValue;
        var foundHit = false;

        if (Mathf.Abs(direction.x) > HitEpsilon)
        {
            if (direction.x < 0f)
            {
                var distance = (bounds.xMin - origin.x) / direction.x;
                if (TrySelectHit(distance, ref nearestDistance))
                {
                    foundHit = true;
                    boundaryType = AimBoundaryType.Left;
                }
            }
            else
            {
                var distance = (bounds.xMax - origin.x) / direction.x;
                if (TrySelectHit(distance, ref nearestDistance))
                {
                    foundHit = true;
                    boundaryType = AimBoundaryType.Right;
                }
            }
        }

        if (Mathf.Abs(direction.y) > HitEpsilon)
        {
            if (direction.y < 0f)
            {
                var distance = (bounds.yMin - origin.y) / direction.y;
                if (TrySelectHit(distance, ref nearestDistance))
                {
                    foundHit = true;
                    boundaryType = AimBoundaryType.Bottom;
                }
            }
            else
            {
                var distance = (bounds.yMax - origin.y) / direction.y;
                if (TrySelectHit(distance, ref nearestDistance))
                {
                    foundHit = true;
                    boundaryType = AimBoundaryType.Top;
                }
            }
        }

        if (!foundHit)
        {
            return false;
        }

        hitDistance = nearestDistance;
        hitPoint = origin + (direction * nearestDistance);
        hitPoint.x = Mathf.Clamp(hitPoint.x, bounds.xMin, bounds.xMax);
        hitPoint.y = Mathf.Clamp(hitPoint.y, bounds.yMin, bounds.yMax);
        return true;
    }

    private static bool TrySelectHit(float candidateDistance, ref float nearestDistance)
    {
        if (candidateDistance <= HitEpsilon || candidateDistance >= nearestDistance)
        {
            return false;
        }

        nearestDistance = candidateDistance;
        return true;
    }

    private static bool IsVerticalBoundary(AimBoundaryType boundaryType)
    {
        return boundaryType == AimBoundaryType.Left || boundaryType == AimBoundaryType.Right;
    }
}
