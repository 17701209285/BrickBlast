using ImportedLevels;
using UnityEngine;

public enum BallCollisionType
{
    None = 0,
    Wall = 1,
    Block = 2,
    Collector = 3
}

public readonly struct BallCollisionHit
{
    public BallCollisionType Type { get; }
    public float Distance { get; }
    public Vector2 Point { get; }
    public Vector2 Normal { get; }
    public Vector2 ImpactPoint { get; }
    public Vector2 ImpactDirection { get; }
    public ChessElement Block { get; }
    public Vector2 AdditionalImpactPoint { get; }
    public ChessElement AdditionalBlock { get; }

    public BallCollisionHit(
        BallCollisionType type,
        float distance,
        Vector2 point,
        Vector2 normal,
        Vector2 impactPoint,
        Vector2 impactDirection,
        ChessElement block,
        Vector2 additionalImpactPoint = default,
        ChessElement additionalBlock = null)
    {
        Type = type;
        Distance = distance;
        Point = point;
        Normal = normal;
        ImpactPoint = impactPoint;
        ImpactDirection = impactDirection;
        Block = block;
        AdditionalImpactPoint = additionalImpactPoint;
        AdditionalBlock = additionalBlock;
    }
}

public static class BallPhysicsUtility
{
    private readonly struct ShapePolygon
    {
        public int Count { get; }
        private readonly Vector2 vertex0;
        private readonly Vector2 vertex1;
        private readonly Vector2 vertex2;
        private readonly Vector2 vertex3;

        public ShapePolygon(Vector2 v0, Vector2 v1, Vector2 v2)
        {
            Count = 3;
            vertex0 = v0;
            vertex1 = v1;
            vertex2 = v2;
            vertex3 = default;
        }

        public ShapePolygon(Vector2 v0, Vector2 v1, Vector2 v2, Vector2 v3)
        {
            Count = 4;
            vertex0 = v0;
            vertex1 = v1;
            vertex2 = v2;
            vertex3 = v3;
        }

        public Vector2 GetVertex(int index)
        {
            switch (index)
            {
                case 0:
                    return vertex0;
                case 1:
                    return vertex1;
                case 2:
                    return vertex2;
                case 3:
                    return vertex3;
                default:
                    return default;
            }
        }
    }

    private const float DirectionThresholdEpsilon = 0.0001f;

    public static bool TryGetNextHit(
        UIChessBoard board,
        RectTransform simulationSpace,
        Rect bounds,
        float collectorY,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float maxDistance,
        float epsilon,
        ChessElement ignoredBlock,
        ChessElement ignoredAdditionalBlock,
        out BallCollisionHit hit)
    {
        hit = new BallCollisionHit(BallCollisionType.None, float.MaxValue, origin, Vector2.zero, origin, direction, null);
        var foundHit = false;

        if (TryGetCollectorHit(bounds, origin, direction, collectorY, radius, maxDistance, epsilon, out var collectorHit))
        {
            foundHit = TrySelectCloserHit(collectorHit, ref hit, epsilon);
        }

        if (TryGetWallHit(bounds, collectorY, origin, direction, radius, maxDistance, epsilon, out var wallHit))
        {
            foundHit = TrySelectCloserHit(wallHit, ref hit, epsilon) || foundHit;
        }

        if (TryGetBlockHit(board, simulationSpace, origin, direction, radius, maxDistance, epsilon, ignoredBlock, ignoredAdditionalBlock, out var blockHit))
        {
            foundHit = TrySelectCloserHit(blockHit, ref hit, epsilon) || foundHit;
        }

        return foundHit && hit.Type != BallCollisionType.None;
    }

    public static bool TryCalculatePathSegment(
        UIChessBoard board,
        RectTransform simulationSpace,
        Rect bounds,
        float collectorY,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float epsilon,
        ChessElement ignoredBlock,
        ChessElement ignoredAdditionalBlock,
        out BallPathSegment segment)
    {
        segment = default;
        if (!TryGetNextHit(
                board,
                simulationSpace,
                bounds,
                collectorY,
                origin,
                direction,
                radius,
                float.PositiveInfinity,
                epsilon,
                ignoredBlock,
                ignoredAdditionalBlock,
                out var hit))
        {
            return false;
        }

        var normalizedDirection = direction.normalized;
        var nextDirection = normalizedDirection;
        if (hit.Type == BallCollisionType.Block || hit.Type == BallCollisionType.Wall)
        {
            nextDirection = Reflect(normalizedDirection, hit.Normal);
        }

        segment = new BallPathSegment(
            origin,
            normalizedDirection,
            hit.Distance,
            nextDirection,
            hit);
        return true;
    }

    public static Vector2 Reflect(Vector2 direction, Vector2 hitNormal)
    {
        var normalizedNormal = NormalizeCollisionNormal(hitNormal);
        if (normalizedNormal == Vector2.zero)
        {
            return direction.normalized;
        }

        if (Mathf.Abs(normalizedNormal.x) <= DirectionThresholdEpsilon || Mathf.Abs(normalizedNormal.y) <= DirectionThresholdEpsilon)
        {
            var snappedNormal = NormalizeNormal(normalizedNormal);
            if (Mathf.Abs(snappedNormal.x) > 0.5f)
            {
                direction.x = -direction.x;
            }

            if (Mathf.Abs(snappedNormal.y) > 0.5f)
            {
                direction.y = -direction.y;
            }

            return direction.normalized;
        }

        return Vector2.Reflect(direction.normalized, normalizedNormal).normalized;
    }

    public static Vector2 GetSeparationOffset(Vector2 hitNormal, float skin)
    {
        var normalizedNormal = NormalizeCollisionNormal(hitNormal);
        if (normalizedNormal == Vector2.zero || skin <= 0f)
        {
            return Vector2.zero;
        }

        return normalizedNormal * skin;
    }

    public static bool TryGetOverlapBlockHit(
        UIChessBoard board,
        RectTransform simulationSpace,
        Vector2 ballCenter,
        float radius,
        float epsilon,
        ChessElement ignoredBlock,
        ChessElement ignoredAdditionalBlock,
        out BallCollisionHit hit,
        out Vector2 resolvedPosition)
    {
        hit = new BallCollisionHit(BallCollisionType.None, 0f, ballCenter, Vector2.zero, ballCenter, Vector2.zero, null);
        resolvedPosition = ballCenter;
        if (board == null || simulationSpace == null)
        {
            return false;
        }

        board.RefreshCollisionCandidates(simulationSpace);

        var candidates = board.CollisionCandidates;
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        var foundHit = false;
        var simultaneousTolerance = GetSimultaneousBlockDistanceTolerance(radius, epsilon);
        var bestPushDistance = float.MaxValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var block = candidate.Element;
            if (block == null || !block.HasContent || ShouldIgnoreBlock(block, ignoredBlock, ignoredAdditionalBlock))
            {
                continue;
            }

            if (!TryResolveCandidateOverlap(
                    candidate,
                    ballCenter,
                    radius,
                    epsilon,
                    out var normal,
                    out var impactPoint,
                    out var candidateResolvedPosition))
            {
                continue;
            }

            var pushDistance = Vector2.Distance(candidateResolvedPosition, ballCenter);
            if (foundHit && pushDistance > bestPushDistance + simultaneousTolerance)
            {
                continue;
            }

            var candidateHit = new BallCollisionHit(BallCollisionType.Block, 0f, ballCenter, normal, impactPoint, Vector2.zero, block);
            if (!foundHit || pushDistance < bestPushDistance - simultaneousTolerance)
            {
                bestPushDistance = pushDistance;
                resolvedPosition = candidateResolvedPosition;
                hit = candidateHit;
                foundHit = true;
                continue;
            }

            hit = MergeSimultaneousBlockHit(hit, candidateHit);
        }

        if (foundHit)
        {
            hit = AugmentSharedBlockHit(hit, candidates, radius, simultaneousTolerance, epsilon, ignoredBlock, ignoredAdditionalBlock);
        }

        return foundHit;
    }

    public static bool TryGetFirstBlockHit(
        UIChessBoard board,
        RectTransform simulationSpace,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float maxDistance,
        float epsilon,
        ChessElement ignoredBlock,
        ChessElement ignoredAdditionalBlock,
        out BallCollisionHit hit)
    {
        return TryGetBlockHit(board, simulationSpace, origin, direction, radius, maxDistance, epsilon, ignoredBlock, ignoredAdditionalBlock, out hit);
    }

    public static bool IsOverlappingBlock(
        UIChessBoard board,
        RectTransform simulationSpace,
        ChessElement targetBlock,
        Vector2 ballCenter,
        float radius,
        float epsilon)
    {
        if (board == null || simulationSpace == null || targetBlock == null || !targetBlock.HasContent)
        {
            return false;
        }

        board.RefreshCollisionCandidates(simulationSpace);

        var candidates = board.CollisionCandidates;
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Element != targetBlock)
            {
                continue;
            }

            return TryResolveCandidateOverlap(
                candidate,
                ballCenter,
                radius,
                epsilon,
                out _,
                out _,
                out _);
        }

        return false;
    }

    private static bool TryGetCollectorHit(
        Rect bounds,
        Vector2 origin,
        Vector2 direction,
        float collectorY,
        float radius,
        float maxDistance,
        float epsilon,
        out BallCollisionHit hit)
    {
        hit = default;
        if (direction.y >= -DirectionThresholdEpsilon)
        {
            return false;
        }

        var distance = (collectorY - origin.y) / direction.y;
        if (distance <= epsilon || distance > maxDistance)
        {
            return false;
        }

        var point = origin + (direction * distance);
        var minX = bounds.xMin + radius;
        var maxX = bounds.xMax - radius;

        // 中文备注：底线回收只在棋盘底部有效宽度内成立。
        // 如果交点已经跑到左右墙外面，应该先撞墙反弹，而不是直接判定回收。
        if (point.x < minX - epsilon || point.x > maxX + epsilon)
        {
            return false;
        }

        point.x = Mathf.Clamp(point.x, minX, maxX);
        hit = new BallCollisionHit(BallCollisionType.Collector, distance, point, Vector2.up, point, direction, null);
        return true;
    }

    private static bool TryGetWallHit(
        Rect bounds,
        float collectorY,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float maxDistance,
        float epsilon,
        out BallCollisionHit hit)
    {
        hit = default;
        var closestHit = new BallCollisionHit(BallCollisionType.None, float.MaxValue, origin, Vector2.zero, origin, direction, null);
        var foundHit = false;

        var left = bounds.xMin + radius;
        var right = bounds.xMax - radius;
        var top = bounds.yMax - radius;

        if (direction.x < -DirectionThresholdEpsilon)
        {
            var distance = (left - origin.x) / direction.x;
            if (TryCreateWallHit(distance, maxDistance, epsilon, origin, direction, Vector2.right, collectorY, top, true, ref closestHit))
            {
                foundHit = true;
            }
        }

        if (direction.x > DirectionThresholdEpsilon)
        {
            var distance = (right - origin.x) / direction.x;
            if (TryCreateWallHit(distance, maxDistance, epsilon, origin, direction, Vector2.left, collectorY, top, true, ref closestHit))
            {
                foundHit = true;
            }
        }

        if (direction.y > DirectionThresholdEpsilon)
        {
            var distance = (top - origin.y) / direction.y;
            if (TryCreateWallHit(distance, maxDistance, epsilon, origin, direction, Vector2.down, left, right, false, ref closestHit))
            {
                foundHit = true;
            }
        }

        hit = closestHit;
        return foundHit;
    }

    private static bool TryCreateWallHit(
        float distance,
        float maxDistance,
        float epsilon,
        Vector2 origin,
        Vector2 direction,
        Vector2 normal,
        float segmentMin,
        float segmentMax,
        bool validateY,
        ref BallCollisionHit currentHit)
    {
        if (distance <= epsilon || distance > maxDistance)
        {
            return false;
        }

        var point = origin + (direction * distance);
        var axisValue = validateY ? point.y : point.x;
        if (axisValue < segmentMin - epsilon || axisValue > segmentMax + epsilon)
        {
            return false;
        }

        if (validateY)
        {
            point.y = Mathf.Clamp(point.y, segmentMin, segmentMax);
        }
        else
        {
            point.x = Mathf.Clamp(point.x, segmentMin, segmentMax);
        }

        var candidate = new BallCollisionHit(BallCollisionType.Wall, distance, point, normal, point, direction, null);
        return TrySelectCloserHit(candidate, ref currentHit, epsilon);
    }

    private static bool TryGetBlockHit(
        UIChessBoard board,
        RectTransform simulationSpace,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float maxDistance,
        float epsilon,
        ChessElement ignoredBlock,
        ChessElement ignoredAdditionalBlock,
        out BallCollisionHit hit)
    {
        hit = default;
        if (board == null || simulationSpace == null)
        {
            return false;
        }

        board.RefreshCollisionCandidates(simulationSpace);

        var candidates = board.CollisionCandidates;
        if (candidates == null || candidates.Count == 0)
        {
            return false;
        }

        var closestHit = new BallCollisionHit(BallCollisionType.None, float.MaxValue, origin, Vector2.zero, origin, direction, null);
        var foundHit = false;
        var simultaneousTolerance = GetSimultaneousBlockDistanceTolerance(radius, epsilon);
        for (int i = 0; i < candidates.Count; i++)
        {
            var collisionCandidate = candidates[i];
            var block = collisionCandidate.Element;
            if (block == null || !block.HasContent || ShouldIgnoreBlock(block, ignoredBlock, ignoredAdditionalBlock))
            {
                continue;
            }

            if (!TryRaycastCollisionCandidate(
                    collisionCandidate,
                    origin,
                    direction,
                    radius,
                    maxDistance,
                    epsilon,
                    out var distance,
                    out var centerPoint,
                    out var normal,
                    out var impactPoint))
            {
                continue;
            }

            var hitCandidate = new BallCollisionHit(
                BallCollisionType.Block,
                distance,
                centerPoint,
                normal,
                impactPoint,
                direction,
                block);
            if (!foundHit || hitCandidate.Distance < closestHit.Distance - simultaneousTolerance)
            {
                closestHit = hitCandidate;
                foundHit = true;
                continue;
            }

            if (Mathf.Abs(hitCandidate.Distance - closestHit.Distance) <= simultaneousTolerance)
            {
                closestHit = MergeSimultaneousBlockHit(closestHit, hitCandidate);
                foundHit = true;
            }
        }

        if (foundHit)
        {
            closestHit = AugmentSharedBlockHit(closestHit, candidates, radius, simultaneousTolerance, epsilon, ignoredBlock, ignoredAdditionalBlock);
        }

        hit = closestHit;
        return foundHit;
    }

    private static bool TryRaycastExpandedRect(
        Vector2 origin,
        Vector2 direction,
        Rect rect,
        float radius,
        float maxDistance,
        float epsilon,
        out float distance,
        out Vector2 point,
        out Vector2 normal)
    {
        distance = 0f;
        point = origin;
        normal = Vector2.zero;

        var min = rect.min - Vector2.one * radius;
        var max = rect.max + Vector2.one * radius;
        var nearestDistance = float.NegativeInfinity;
        var farthestDistance = float.PositiveInfinity;
        var enterNormal = Vector2.zero;

        if (!TryUpdateAxis(origin.x, direction.x, min.x, max.x, Vector2.left, Vector2.right, epsilon, DirectionThresholdEpsilon, ref nearestDistance, ref farthestDistance, ref enterNormal))
        {
            return false;
        }

        if (!TryUpdateAxis(origin.y, direction.y, min.y, max.y, Vector2.down, Vector2.up, epsilon, DirectionThresholdEpsilon, ref nearestDistance, ref farthestDistance, ref enterNormal))
        {
            return false;
        }

        if (nearestDistance <= epsilon || nearestDistance > maxDistance)
        {
            return false;
        }

        distance = nearestDistance;
        point = origin + (direction * distance);
        normal = NormalizeNormal(enterNormal);
        return normal != Vector2.zero;
    }

    private static bool TryResolveExpandedRectOverlap(
        Vector2 ballCenter,
        Rect rect,
        float radius,
        float epsilon,
        out Vector2 normal,
        out Vector2 impactPoint,
        out Vector2 resolvedPosition)
    {
        normal = Vector2.zero;
        impactPoint = ballCenter;
        resolvedPosition = ballCenter;

        var expanded = Rect.MinMaxRect(rect.xMin - radius, rect.yMin - radius, rect.xMax + radius, rect.yMax + radius);
        if (!expanded.Contains(ballCenter))
        {
            return false;
        }

        impactPoint = new Vector2(
            Mathf.Clamp(ballCenter.x, rect.xMin, rect.xMax),
            Mathf.Clamp(ballCenter.y, rect.yMin, rect.yMax));

        var distanceToLeft = Mathf.Abs(ballCenter.x - expanded.xMin);
        var distanceToRight = Mathf.Abs(expanded.xMax - ballCenter.x);
        var distanceToBottom = Mathf.Abs(ballCenter.y - expanded.yMin);
        var distanceToTop = Mathf.Abs(expanded.yMax - ballCenter.y);

        var minDistance = distanceToLeft;
        normal = Vector2.left;
        resolvedPosition = new Vector2(expanded.xMin - epsilon, ballCenter.y);

        if (distanceToRight < minDistance)
        {
            minDistance = distanceToRight;
            normal = Vector2.right;
            resolvedPosition = new Vector2(expanded.xMax + epsilon, ballCenter.y);
        }

        if (distanceToBottom < minDistance)
        {
            minDistance = distanceToBottom;
            normal = Vector2.down;
            resolvedPosition = new Vector2(ballCenter.x, expanded.yMin - epsilon);
        }

        if (distanceToTop < minDistance)
        {
            normal = Vector2.up;
            resolvedPosition = new Vector2(ballCenter.x, expanded.yMax + epsilon);
        }

        return true;
    }

    private static bool TryResolveCandidateOverlap(
        in UIChessBoard.CollisionCandidate candidate,
        Vector2 ballCenter,
        float radius,
        float epsilon,
        out Vector2 normal,
        out Vector2 impactPoint,
        out Vector2 resolvedPosition)
    {
        if (TryBuildCollisionPolygon(candidate.RectInBoardSpace, candidate.ShapeType, out var polygon))
        {
            return TryResolveConvexPolygonOverlap(ballCenter, polygon, radius, epsilon, out normal, out impactPoint, out resolvedPosition);
        }

        return TryResolveExpandedRectOverlap(ballCenter, candidate.RectInBoardSpace, radius, epsilon, out normal, out impactPoint, out resolvedPosition);
    }

    private static bool TryUpdateAxis(
        float origin,
        float direction,
        float min,
        float max,
        Vector2 minNormal,
        Vector2 maxNormal,
        float epsilon,
        float directionThresholdEpsilon,
        ref float nearestDistance,
        ref float farthestDistance,
        ref Vector2 enterNormal)
    {
        if (Mathf.Abs(direction) <= directionThresholdEpsilon)
        {
            return origin >= min && origin <= max;
        }

        var inverseDirection = 1f / direction;
        var enterDistance = (min - origin) * inverseDirection;
        var exitDistance = (max - origin) * inverseDirection;
        var axisEnterNormal = minNormal;

        if (enterDistance > exitDistance)
        {
            (enterDistance, exitDistance) = (exitDistance, enterDistance);
            axisEnterNormal = maxNormal;
        }

        if (enterDistance > nearestDistance + epsilon)
        {
            nearestDistance = enterDistance;
            enterNormal = axisEnterNormal;
        }
        else if (Mathf.Abs(enterDistance - nearestDistance) <= epsilon)
        {
            enterNormal += axisEnterNormal;
        }

        farthestDistance = Mathf.Min(farthestDistance, exitDistance);
        return nearestDistance <= farthestDistance + epsilon;
    }

    private static bool TrySelectCloserHit(BallCollisionHit candidate, ref BallCollisionHit currentHit, float epsilon)
    {
        if (candidate.Type == BallCollisionType.None)
        {
            return false;
        }

        if (currentHit.Type == BallCollisionType.None || candidate.Distance < currentHit.Distance - epsilon)
        {
            currentHit = candidate;
            return true;
        }

        if (Mathf.Abs(candidate.Distance - currentHit.Distance) > epsilon)
        {
            return false;
        }

        if (currentHit.Type == BallCollisionType.Wall && candidate.Type == BallCollisionType.Wall)
        {
            currentHit = new BallCollisionHit(
                BallCollisionType.Wall,
                currentHit.Distance,
                currentHit.Point,
                NormalizeNormal(currentHit.Normal + candidate.Normal),
                currentHit.ImpactPoint,
                currentHit.ImpactDirection,
                null);
            return true;
        }

        if (currentHit.Type == BallCollisionType.Block && candidate.Type == BallCollisionType.Block)
        {
            currentHit = MergeSimultaneousBlockHit(currentHit, candidate);
            return true;
        }

        if (currentHit.Type != BallCollisionType.Block && candidate.Type == BallCollisionType.Block)
        {
            currentHit = candidate;
            return true;
        }

        return false;
    }

    private static Vector2 NormalizeNormal(Vector2 normal)
    {
        return new Vector2(Mathf.RoundToInt(Mathf.Sign(normal.x) * Mathf.Clamp01(Mathf.Abs(normal.x))), Mathf.RoundToInt(Mathf.Sign(normal.y) * Mathf.Clamp01(Mathf.Abs(normal.y))));
    }

    private static Vector2 NormalizeCollisionNormal(Vector2 normal)
    {
        return normal.sqrMagnitude <= DirectionThresholdEpsilon * DirectionThresholdEpsilon
            ? Vector2.zero
            : normal.normalized;
    }

    private static float GetSimultaneousBlockDistanceTolerance(float radius, float epsilon)
    {
        return Mathf.Max(epsilon, Mathf.Min(3f, Mathf.Max(1.25f, radius * 0.2f)));
    }

    private static BallCollisionHit MergeSimultaneousBlockHit(BallCollisionHit currentHit, BallCollisionHit candidate)
    {
        if (currentHit.Type != BallCollisionType.Block)
        {
            return candidate;
        }

        if (candidate.Type != BallCollisionType.Block)
        {
            return currentHit;
        }

        var mergedNormal = NormalizeNormal(currentHit.Normal + candidate.Normal);
        if (mergedNormal == Vector2.zero)
        {
            mergedNormal = BuildAxisFallbackNormal(currentHit.Normal, candidate.Normal);
        }

        var additionalImpactPoint = currentHit.AdditionalImpactPoint;
        var additionalBlock = currentHit.AdditionalBlock;
        if (candidate.Block != null && candidate.Block != currentHit.Block && candidate.Block != currentHit.AdditionalBlock)
        {
            additionalImpactPoint = candidate.ImpactPoint;
            additionalBlock = candidate.Block;
        }

        return new BallCollisionHit(
            BallCollisionType.Block,
            currentHit.Distance,
            currentHit.Point,
            mergedNormal,
            currentHit.ImpactPoint,
            currentHit.ImpactDirection,
            currentHit.Block,
            additionalImpactPoint,
            additionalBlock);
    }

    private static BallCollisionHit AugmentSharedBlockHit(
        BallCollisionHit hit,
        System.Collections.Generic.IReadOnlyList<UIChessBoard.CollisionCandidate> candidates,
        float radius,
        float simultaneousTolerance,
        float epsilon,
        ChessElement ignoredBlock,
        ChessElement ignoredAdditionalBlock)
    {
        if (hit.Type != BallCollisionType.Block || hit.Block == null || candidates == null || candidates.Count == 0)
        {
            return hit;
        }

        var augmentedHit = hit;
        var spatialTolerance = GetSharedBlockSpatialTolerance(radius, simultaneousTolerance, epsilon);
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var block = candidate.Element;
            if (block == null || !block.HasContent || block == augmentedHit.Block || block == augmentedHit.AdditionalBlock || ShouldIgnoreBlock(block, ignoredBlock, ignoredAdditionalBlock))
            {
                continue;
            }

            if (!TryGetSharedBlockContact(augmentedHit.Point, candidate, radius, spatialTolerance, epsilon, out var impactPoint, out var normal))
            {
                continue;
            }

            augmentedHit = MergeSimultaneousBlockHit(
                augmentedHit,
                new BallCollisionHit(
                    BallCollisionType.Block,
                    augmentedHit.Distance,
                    augmentedHit.Point,
                    normal,
                    impactPoint,
                    augmentedHit.ImpactDirection,
                    block));

            if (augmentedHit.AdditionalBlock != null)
            {
                break;
            }
        }

        return augmentedHit;
    }

    private static bool ShouldIgnoreBlock(ChessElement block, ChessElement ignoredBlock, ChessElement ignoredAdditionalBlock)
    {
        return block != null && (block == ignoredBlock || block == ignoredAdditionalBlock);
    }

    private static Vector2 BuildAxisFallbackNormal(Vector2 a, Vector2 b)
    {
        var x = Mathf.Abs(a.x) > 0.5f
            ? Mathf.Sign(a.x)
            : (Mathf.Abs(b.x) > 0.5f ? Mathf.Sign(b.x) : 0f);
        var y = Mathf.Abs(a.y) > 0.5f
            ? Mathf.Sign(a.y)
            : (Mathf.Abs(b.y) > 0.5f ? Mathf.Sign(b.y) : 0f);
        return new Vector2(x, y);
    }

    private static float GetSharedBlockSpatialTolerance(float radius, float simultaneousTolerance, float epsilon)
    {
        return Mathf.Max(epsilon, Mathf.Max(simultaneousTolerance, Mathf.Min(6f, Mathf.Max(1.5f, radius * 0.18f))));
    }

    private static bool TryGetSharedBlockContact(
        Vector2 ballCenter,
        in UIChessBoard.CollisionCandidate candidate,
        float radius,
        float spatialTolerance,
        float epsilon,
        out Vector2 impactPoint,
        out Vector2 normal)
    {
        if (TryBuildCollisionPolygon(candidate.RectInBoardSpace, candidate.ShapeType, out var polygon))
        {
            return TryGetSharedPolygonContact(ballCenter, polygon, radius, spatialTolerance, epsilon, out impactPoint, out normal);
        }

        var rect = candidate.RectInBoardSpace;
        impactPoint = new Vector2(
            Mathf.Clamp(ballCenter.x, rect.xMin, rect.xMax),
            Mathf.Clamp(ballCenter.y, rect.yMin, rect.yMax));

        var offset = ballCenter - impactPoint;
        var maxContactDistance = radius + spatialTolerance;
        if (offset.sqrMagnitude > maxContactDistance * maxContactDistance)
        {
            normal = Vector2.zero;
            return false;
        }

        normal = NormalizeNormal(offset);
        if (normal != Vector2.zero)
        {
            return true;
        }

        if (TryResolveExpandedRectOverlap(ballCenter, rect, radius, spatialTolerance + epsilon, out normal, out impactPoint, out _))
        {
            normal = NormalizeNormal(normal);
            return normal != Vector2.zero;
        }

        normal = Vector2.zero;
        return false;
    }

    private static bool TryRaycastCollisionCandidate(
        in UIChessBoard.CollisionCandidate candidate,
        Vector2 origin,
        Vector2 direction,
        float radius,
        float maxDistance,
        float epsilon,
        out float distance,
        out Vector2 point,
        out Vector2 normal,
        out Vector2 impactPoint)
    {
        if (TryBuildCollisionPolygon(candidate.RectInBoardSpace, candidate.ShapeType, out var polygon))
        {
            return TryRaycastConvexPolygon(origin, direction, polygon, radius, maxDistance, epsilon, out distance, out point, out normal, out impactPoint);
        }

        if (TryRaycastExpandedRect(origin, direction, candidate.RectInBoardSpace, radius, maxDistance, epsilon, out distance, out point, out normal))
        {
            impactPoint = point - (NormalizeCollisionNormal(normal) * radius);
            return true;
        }

        impactPoint = origin;
        return false;
    }

    private static bool TryBuildCollisionPolygon(Rect rect, LegacyBrickShapeType shapeType, out ShapePolygon polygon)
    {
        var bottomLeft = new Vector2(rect.xMin, rect.yMin);
        var topLeft = new Vector2(rect.xMin, rect.yMax);
        var topRight = new Vector2(rect.xMax, rect.yMax);
        var bottomRight = new Vector2(rect.xMax, rect.yMin);
        switch (shapeType)
        {
            case LegacyBrickShapeType.RightTriangleLeftDown:
                polygon = new ShapePolygon(bottomLeft, bottomRight, topLeft);
                return true;
            case LegacyBrickShapeType.RightTriangleLeftUp:
                polygon = new ShapePolygon(bottomLeft, topLeft, topRight);
                return true;
            case LegacyBrickShapeType.RightTriangleRightUp:
                polygon = new ShapePolygon(topLeft, topRight, bottomRight);
                return true;
            case LegacyBrickShapeType.RightTriangleRightDown:
                polygon = new ShapePolygon(bottomLeft, bottomRight, topRight);
                return true;
            case LegacyBrickShapeType.EquilateralTriangle:
                polygon = new ShapePolygon(bottomLeft, bottomRight, new Vector2(rect.center.x, rect.yMax));
                return true;
            default:
                polygon = default;
                return false;
        }
    }

    private static bool TryRaycastConvexPolygon(
        Vector2 origin,
        Vector2 direction,
        ShapePolygon polygon,
        float radius,
        float maxDistance,
        float epsilon,
        out float distance,
        out Vector2 point,
        out Vector2 normal,
        out Vector2 impactPoint)
    {
        distance = 0f;
        point = origin;
        normal = Vector2.zero;
        impactPoint = origin;

        var foundHit = false;
        var bestDistance = float.MaxValue;
        var bestNormal = Vector2.zero;
        var bestImpactPoint = origin;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon.GetVertex(i);
            var b = polygon.GetVertex((i + 1) % polygon.Count);
            var edge = b - a;
            if (edge.sqrMagnitude <= epsilon * epsilon)
            {
                continue;
            }

            var outwardNormal = NormalizeCollisionNormal(new Vector2(edge.y, -edge.x));
            var denominator = Vector2.Dot(outwardNormal, direction);
            if (denominator >= -DirectionThresholdEpsilon)
            {
                continue;
            }

            var edgeDistance = Vector2.Dot(outwardNormal, origin - a);
            var hitDistance = (radius - edgeDistance) / denominator;
            if (hitDistance <= epsilon || hitDistance > maxDistance)
            {
                continue;
            }

            var candidatePoint = origin + (direction * hitDistance);
            var candidateImpactPoint = candidatePoint - (outwardNormal * radius);
            if (!IsPointNearSegment(candidateImpactPoint, a, b, epsilon))
            {
                continue;
            }

            RegisterPolygonHitCandidate(hitDistance, outwardNormal, candidateImpactPoint, ref foundHit, ref bestDistance, ref bestNormal, ref bestImpactPoint, epsilon);
        }

        for (int i = 0; i < polygon.Count; i++)
        {
            var vertex = polygon.GetVertex(i);
            if (!TryRaycastCircle(origin, direction, vertex, radius, maxDistance, epsilon, out var hitDistance, out var candidatePoint, out var candidateNormal))
            {
                continue;
            }

            RegisterPolygonHitCandidate(hitDistance, candidateNormal, vertex, ref foundHit, ref bestDistance, ref bestNormal, ref bestImpactPoint, epsilon);
        }

        if (!foundHit)
        {
            return false;
        }

        distance = bestDistance;
        point = origin + (direction * bestDistance);
        normal = NormalizeCollisionNormal(bestNormal);
        impactPoint = bestImpactPoint;
        return normal != Vector2.zero;
    }

    private static void RegisterPolygonHitCandidate(
        float candidateDistance,
        Vector2 candidateNormal,
        Vector2 candidateImpactPoint,
        ref bool foundHit,
        ref float bestDistance,
        ref Vector2 bestNormal,
        ref Vector2 bestImpactPoint,
        float epsilon)
    {
        if (!foundHit || candidateDistance < bestDistance - epsilon)
        {
            foundHit = true;
            bestDistance = candidateDistance;
            bestNormal = candidateNormal;
            bestImpactPoint = candidateImpactPoint;
            return;
        }

        if (Mathf.Abs(candidateDistance - bestDistance) <= epsilon)
        {
            bestNormal += candidateNormal;
        }
    }

    private static bool TryRaycastCircle(
        Vector2 origin,
        Vector2 direction,
        Vector2 circleCenter,
        float radius,
        float maxDistance,
        float epsilon,
        out float distance,
        out Vector2 point,
        out Vector2 normal)
    {
        distance = 0f;
        point = origin;
        normal = Vector2.zero;

        var offset = origin - circleCenter;
        var projection = Vector2.Dot(offset, direction);
        var c = Vector2.Dot(offset, offset) - (radius * radius);
        var discriminant = (projection * projection) - c;
        if (discriminant < 0f)
        {
            return false;
        }

        var hitDistance = -projection - Mathf.Sqrt(discriminant);
        if (hitDistance <= epsilon || hitDistance > maxDistance)
        {
            return false;
        }

        point = origin + (direction * hitDistance);
        normal = NormalizeCollisionNormal(point - circleCenter);
        if (normal == Vector2.zero)
        {
            return false;
        }

        distance = hitDistance;
        return true;
    }

    private static bool TryResolveConvexPolygonOverlap(
        Vector2 ballCenter,
        ShapePolygon polygon,
        float radius,
        float epsilon,
        out Vector2 normal,
        out Vector2 impactPoint,
        out Vector2 resolvedPosition)
    {
        impactPoint = ballCenter;
        normal = Vector2.zero;
        resolvedPosition = ballCenter;

        var isInside = IsPointInsideConvexPolygon(ballCenter, polygon, epsilon);
        if (!TryGetClosestPointOnPolygonBoundary(ballCenter, polygon, out impactPoint, out var boundaryNormal))
        {
            return false;
        }

        var offset = ballCenter - impactPoint;
        var distance = offset.magnitude;
        if (!isInside && distance > radius + epsilon)
        {
            return false;
        }

        if (distance > epsilon)
        {
            normal = isInside ? -(offset / distance) : (offset / distance);
        }
        else
        {
            normal = boundaryNormal;
        }

        normal = NormalizeCollisionNormal(normal);
        if (normal == Vector2.zero)
        {
            return false;
        }

        resolvedPosition = impactPoint + (normal * (radius + epsilon));
        return true;
    }

    private static bool TryGetSharedPolygonContact(
        Vector2 ballCenter,
        ShapePolygon polygon,
        float radius,
        float spatialTolerance,
        float epsilon,
        out Vector2 impactPoint,
        out Vector2 normal)
    {
        impactPoint = ballCenter;
        normal = Vector2.zero;
        var isInside = IsPointInsideConvexPolygon(ballCenter, polygon, epsilon);
        if (!TryGetClosestPointOnPolygonBoundary(ballCenter, polygon, out impactPoint, out var boundaryNormal))
        {
            return false;
        }

        var offset = ballCenter - impactPoint;
        var maxContactDistance = radius + spatialTolerance;
        if (!isInside && offset.sqrMagnitude > maxContactDistance * maxContactDistance)
        {
            return false;
        }

        if (offset.sqrMagnitude > epsilon * epsilon)
        {
            normal = isInside ? -offset.normalized : offset.normalized;
        }
        else
        {
            normal = boundaryNormal;
        }

        normal = NormalizeCollisionNormal(normal);
        return normal != Vector2.zero;
    }

    private static bool TryGetClosestPointOnPolygonBoundary(
        Vector2 point,
        ShapePolygon polygon,
        out Vector2 closestPoint,
        out Vector2 outwardNormal)
    {
        closestPoint = point;
        outwardNormal = Vector2.zero;
        if (polygon.Count < 2)
        {
            return false;
        }

        var found = false;
        var bestDistanceSquared = float.MaxValue;
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon.GetVertex(i);
            var b = polygon.GetVertex((i + 1) % polygon.Count);
            var candidatePoint = ClosestPointOnSegment(point, a, b);
            var distanceSquared = (point - candidatePoint).sqrMagnitude;
            if (found && distanceSquared >= bestDistanceSquared)
            {
                continue;
            }

            found = true;
            bestDistanceSquared = distanceSquared;
            closestPoint = candidatePoint;
            outwardNormal = NormalizeCollisionNormal(new Vector2((b - a).y, -(b - a).x));
        }

        return found;
    }

    private static bool IsPointInsideConvexPolygon(Vector2 point, ShapePolygon polygon, float epsilon)
    {
        for (int i = 0; i < polygon.Count; i++)
        {
            var a = polygon.GetVertex(i);
            var b = polygon.GetVertex((i + 1) % polygon.Count);
            if (Cross(b - a, point - a) < -epsilon)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPointNearSegment(Vector2 point, Vector2 a, Vector2 b, float epsilon)
    {
        return (ClosestPointOnSegment(point, a, b) - point).sqrMagnitude <= epsilon * epsilon;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var edge = b - a;
        var edgeLengthSquared = edge.sqrMagnitude;
        if (edgeLengthSquared <= DirectionThresholdEpsilon * DirectionThresholdEpsilon)
        {
            return a;
        }

        var t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edgeLengthSquared);
        return a + (edge * t);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return (a.x * b.y) - (a.y * b.x);
    }
}
