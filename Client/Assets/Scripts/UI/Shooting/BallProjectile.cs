using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BallProjectile : MonoBehaviour
{
    private const float MinimumSweepCollisionEpsilon = 0.01f;
    private const float MaximumTickDeltaTime = 0.05f;
    private const int MinimumSegmentTransitionBudget = 8;

    private BallVolleyController owner;
    private UIChessBoard chessBoard;
    private RectTransform simulationSpace;
    private RectTransform selfRectTransform;
    private Graphic graphic;
    private Rect collisionBounds;
    private Vector2 localPosition;
    private Vector2 direction;
    private float speed;
    private float radius;
    private float collectorY;
    private float collisionSkin;
    private int maxCollisionsPerStep;
    private bool isFlying;
    private bool canTriggerSplitSpecial;
    private BallPathSegment activeSegment;
    private float activeSegmentTravelledDistance;
    private bool hasActiveSegment;
    private ChessElement ignoredPassThroughBlock;
    private ChessElement ignoredPassThroughAdditionalBlock;
    private float ignoredPassThroughDistanceRemaining;

    public bool IsFlying => isFlying;
    public bool CanTriggerSplitSpecial => canTriggerSplitSpecial;

    private void Awake()
    {
        CacheComponents();
        ConfigureVisuals();
    }

    public void Tick(float deltaTime)
    {
        if (!isFlying)
        {
            return;
        }

        Simulate(deltaTime);
    }

    public void Launch(in BallProjectileLaunchData launchData)
    {
        CacheComponents();
        ConfigureVisuals();

        owner = launchData.Owner;
        chessBoard = launchData.ChessBoard;
        simulationSpace = launchData.SimulationSpace;
        collisionBounds = launchData.CollisionBounds;
        localPosition = launchData.StartLocalPosition;
        direction = launchData.Direction;
        speed = Mathf.Max(0f, launchData.Speed);
        radius = Mathf.Max(0f, launchData.Radius);
        collectorY = launchData.CollectorY;
        collisionSkin = Mathf.Max(0.01f, launchData.CollisionSkin);
        maxCollisionsPerStep = Mathf.Max(1, launchData.MaxCollisionsPerStep);
        canTriggerSplitSpecial = launchData.CanTriggerSplitSpecial;
        ClearActiveSegment();
        ClearIgnoredPassThroughBlocks();
        isFlying = true;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ApplyPosition();
    }

    public void ReturnToPool()
    {
        isFlying = false;
        ClearActiveSegment();
        ClearIgnoredPassThroughBlocks();
        gameObject.SetActive(false);
    }

    // Chinese note: the projectile now moves along a cached path segment.
    // We only ask physics for a new collision when the current segment is exhausted,
    // which is much closer to the reference project's "segment driven" battle loop.
    private void Simulate(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        var remainingDistance = speed * Mathf.Min(deltaTime, MaximumTickDeltaTime);
        var transitionBudget = Mathf.Max(MinimumSegmentTransitionBudget, maxCollisionsPerStep * 8);

        while (remainingDistance > 0.001f && isFlying && transitionBudget > 0)
        {
            if (!TryEnsureActiveSegment())
            {
                if (!TryRecoverFromUnexpectedOverlap(ref remainingDistance))
                {
                    MoveWithoutResolvedSegment(ref remainingDistance);
                }

                transitionBudget--;
                continue;
            }

            var remainingOnSegment = Mathf.Max(0f, activeSegment.Distance - activeSegmentTravelledDistance);
            if (remainingOnSegment <= GetSweepCollisionEpsilon())
            {
                transitionBudget--;
                if (ResolveActiveSegmentEnd(ref remainingDistance))
                {
                    return;
                }

                continue;
            }

            var travelledDistance = Mathf.Min(remainingDistance, remainingOnSegment);
            activeSegmentTravelledDistance += travelledDistance;
            localPosition = activeSegment.GetPoint(activeSegmentTravelledDistance);
            remainingDistance = Mathf.Max(0f, remainingDistance - travelledDistance);
            ConsumeIgnoredPassThroughDistance(travelledDistance);

            if (activeSegmentTravelledDistance + GetSweepCollisionEpsilon() < activeSegment.Distance)
            {
                continue;
            }

            transitionBudget--;
            if (ResolveActiveSegmentEnd(ref remainingDistance))
            {
                return;
            }
        }

        ApplyPosition();
    }

    private bool TryEnsureActiveSegment()
    {
        if (hasActiveSegment)
        {
            return true;
        }

        if (BallPhysicsUtility.TryCalculatePathSegment(
                chessBoard,
                simulationSpace,
                collisionBounds,
                collectorY,
                localPosition,
                direction,
                radius,
                GetSweepCollisionEpsilon(),
                GetIgnoredPassThroughBlock(),
                GetIgnoredPassThroughAdditionalBlock(),
                out activeSegment))
        {
            activeSegmentTravelledDistance = 0f;
            hasActiveSegment = true;
            return true;
        }

        return false;
    }

    private bool ResolveActiveSegmentEnd(ref float remainingDistance)
    {
        var hit = activeSegment.Hit;
        ClearActiveSegment();
        return ResolveHit(hit, ref remainingDistance);
    }

    private bool ResolveHit(in BallCollisionHit hit, ref float remainingDistance)
    {
        localPosition = hit.Point;
        ConsumeIgnoredPassThroughDistance(hit.Distance);

        if (hit.Type == BallCollisionType.Collector)
        {
            ApplyPosition();
            owner?.NotifyProjectileReturned(this, hit.Point);
            return true;
        }

        if (hit.Type == BallCollisionType.Block && hit.Block != null)
        {
            var effectResult = chessBoard != null
                ? chessBoard.ResolveProjectileBlockHit(hit, canTriggerSplitSpecial)
                : default;
            if (TryHandleProjectileHitEffect(effectResult))
            {
                return true;
            }

            if (effectResult.PassThrough)
            {
                AdvancePassThrough(ref remainingDistance, hit);
                return false;
            }
        }

        ClearIgnoredPassThroughBlocks();
        var reflectedDirection = BallPhysicsUtility.Reflect(direction, hit.Normal);
        direction = reflectedDirection;

        if (hit.Type == BallCollisionType.Block && hit.Block != null)
        {
            AdvanceBounce(ref remainingDistance, hit, reflectedDirection);
        }
        else
        {
            localPosition += BallPhysicsUtility.GetSeparationOffset(hit.Normal, collisionSkin);
            remainingDistance = Mathf.Max(0f, remainingDistance - collisionSkin);
        }

        return false;
    }

    private bool TryRecoverFromUnexpectedOverlap(ref float remainingDistance)
    {
        if (!BallPhysicsUtility.TryGetOverlapBlockHit(
                chessBoard,
                simulationSpace,
                localPosition,
                radius,
                collisionSkin,
                GetIgnoredPassThroughBlock(),
                GetIgnoredPassThroughAdditionalBlock(),
                out var overlapHit,
                out var resolvedPosition))
        {
            return false;
        }

        // Chinese note: overlap recovery is now a last-resort safety net.
        // We convert the overlap into a corrected boundary hit instead of stepping
        // through the field in many tiny slices every frame.
        var correctedHit = new BallCollisionHit(
            BallCollisionType.Block,
            0f,
            resolvedPosition,
            overlapHit.Normal,
            overlapHit.ImpactPoint,
            direction,
            overlapHit.Block,
            overlapHit.AdditionalImpactPoint,
            overlapHit.AdditionalBlock);

        return ResolveHit(correctedHit, ref remainingDistance);
    }

    private void MoveWithoutResolvedSegment(ref float remainingDistance)
    {
        var nextPosition = localPosition + (direction * remainingDistance);
        var nextDirection = direction;
        if (TryResolveBoundsFallback(ref nextPosition, ref nextDirection, out var collectedPoint))
        {
            localPosition = collectedPoint ?? nextPosition;
            direction = nextDirection;
            remainingDistance = 0f;

            if (collectedPoint.HasValue)
            {
                ApplyPosition();
                owner?.NotifyProjectileReturned(this, collectedPoint.Value);
                return;
            }
        }
        else
        {
            localPosition = nextPosition;
            remainingDistance = 0f;
        }
    }

    private bool TryHandleProjectileHitEffect(ProjectileHitEffectResult effectResult)
    {
        if (effectResult.AddedBallCount > 0)
        {
            owner?.AddBallCount(effectResult.AddedBallCount);
        }

        if (effectResult.RedirectCurrentProjectile)
        {
            if (owner != null)
            {
                owner.HandleRedirectTrigger(this, effectResult.RedirectOrigin, effectResult.RedirectDirection);
            }
            else
            {
                ReturnToPool();
            }

            return true;
        }

        if (effectResult.SplitIntoThreeWay)
        {
            if (!canTriggerSplitSpecial)
            {
                return false;
            }

            if (owner != null)
            {
                owner.HandleSplitTrigger(this, effectResult.SplitOrigin, effectResult.SplitDirection);
            }
            else
            {
                ReturnToPool();
            }

            return true;
        }

        return false;
    }

    private void AdvancePassThrough(ref float remainingDistance, in BallCollisionHit hit)
    {
        ignoredPassThroughBlock = hit.Block;
        ignoredPassThroughAdditionalBlock = hit.AdditionalBlock;
        ignoredPassThroughDistanceRemaining = Mathf.Max(0f, GetPassThroughOffset(hit));

        var advanceDistance = Mathf.Min(
            remainingDistance,
            Mathf.Max(collisionSkin, GetSweepCollisionEpsilon()));
        localPosition += direction * advanceDistance;
        remainingDistance = Mathf.Max(0f, remainingDistance - advanceDistance);
        ConsumeIgnoredPassThroughDistance(advanceDistance);
    }

    private void AdvanceBounce(ref float remainingDistance, in BallCollisionHit hit, Vector2 reflectedDirection)
    {
        var bounceOffset = GetBounceOffset(hit, reflectedDirection);
        localPosition += reflectedDirection * bounceOffset;
        remainingDistance = Mathf.Max(0f, remainingDistance - bounceOffset);
    }

    private float GetPassThroughOffset(in BallCollisionHit hit)
    {
        var baseOffset = Mathf.Max(collisionSkin, radius * BallShootingConstants.PassThroughOffsetRadiusRatio);
        var primaryExitOffset = GetExitOffsetForBlock(hit.Block, direction);
        var secondaryExitOffset = GetExitOffsetForBlock(hit.AdditionalBlock, direction);
        return Mathf.Max(baseOffset, Mathf.Max(primaryExitOffset, secondaryExitOffset));
    }

    private float GetExitOffsetForBlock(ChessElement block, Vector2 travelDirection)
    {
        if (block == null || simulationSpace == null)
        {
            return 0f;
        }

        var blockRect = block.GetRectInSpace(simulationSpace);
        if (blockRect.width <= 0f || blockRect.height <= 0f)
        {
            return 0f;
        }

        blockRect.xMin -= radius;
        blockRect.xMax += radius;
        blockRect.yMin -= radius;
        blockRect.yMax += radius;

        var exitDistance = float.MaxValue;
        if (travelDirection.x > 0.0001f)
        {
            exitDistance = Mathf.Min(exitDistance, (blockRect.xMax - localPosition.x) / travelDirection.x);
        }
        else if (travelDirection.x < -0.0001f)
        {
            exitDistance = Mathf.Min(exitDistance, (blockRect.xMin - localPosition.x) / travelDirection.x);
        }

        if (travelDirection.y > 0.0001f)
        {
            exitDistance = Mathf.Min(exitDistance, (blockRect.yMax - localPosition.y) / travelDirection.y);
        }
        else if (travelDirection.y < -0.0001f)
        {
            exitDistance = Mathf.Min(exitDistance, (blockRect.yMin - localPosition.y) / travelDirection.y);
        }

        if (exitDistance == float.MaxValue)
        {
            return 0f;
        }

        return Mathf.Max(0f, exitDistance) + collisionSkin;
    }

    private float GetSweepCollisionEpsilon()
    {
        return Mathf.Min(collisionSkin * 0.25f, Mathf.Max(0.001f, MinimumSweepCollisionEpsilon));
    }

    private ChessElement GetIgnoredPassThroughBlock()
    {
        return ignoredPassThroughDistanceRemaining > 0f ? ignoredPassThroughBlock : null;
    }

    private ChessElement GetIgnoredPassThroughAdditionalBlock()
    {
        return ignoredPassThroughDistanceRemaining > 0f ? ignoredPassThroughAdditionalBlock : null;
    }

    private float GetBounceOffset(in BallCollisionHit hit, Vector2 reflectedDirection)
    {
        var primaryExitOffset = GetExitOffsetForBlock(hit.Block, reflectedDirection);
        var secondaryExitOffset = GetExitOffsetForBlock(hit.AdditionalBlock, reflectedDirection);
        return Mathf.Max(collisionSkin, Mathf.Max(primaryExitOffset, secondaryExitOffset));
    }

    private void ConsumeIgnoredPassThroughDistance(float distance)
    {
        if (ignoredPassThroughDistanceRemaining <= 0f || distance <= 0f)
        {
            return;
        }

        ignoredPassThroughDistanceRemaining = Mathf.Max(0f, ignoredPassThroughDistanceRemaining - distance);
        if (ignoredPassThroughDistanceRemaining <= 0f)
        {
            ClearIgnoredPassThroughBlocks();
        }
    }

    private void ClearIgnoredPassThroughBlocks()
    {
        ignoredPassThroughBlock = null;
        ignoredPassThroughAdditionalBlock = null;
        ignoredPassThroughDistanceRemaining = 0f;
    }

    private void ClearActiveSegment()
    {
        activeSegment = default;
        activeSegmentTravelledDistance = 0f;
        hasActiveSegment = false;
    }

    private void ApplyPosition()
    {
        if (selfRectTransform != null)
        {
            selfRectTransform.anchoredPosition = localPosition;
        }
    }

    private void CacheComponents()
    {
        if (selfRectTransform == null)
        {
            selfRectTransform = GetComponent<RectTransform>();
        }

        if (graphic == null)
        {
            graphic = GetComponent<Graphic>();
        }
    }

    private void ConfigureVisuals()
    {
        if (graphic != null)
        {
            graphic.enabled = true;
            graphic.raycastTarget = false;
        }
    }

    private bool TryResolveBoundsFallback(ref Vector2 nextPosition, ref Vector2 nextDirection, out Vector2? collectedPoint)
    {
        collectedPoint = null;

        var left = collisionBounds.xMin + radius;
        var right = collisionBounds.xMax - radius;
        var top = collisionBounds.yMax - radius;
        var reflected = false;

        if (nextPosition.x < left)
        {
            nextPosition.x = left + (left - nextPosition.x);
            nextDirection.x = Mathf.Abs(nextDirection.x);
            reflected = true;
        }
        else if (nextPosition.x > right)
        {
            nextPosition.x = right - (nextPosition.x - right);
            nextDirection.x = -Mathf.Abs(nextDirection.x);
            reflected = true;
        }

        if (nextPosition.y > top)
        {
            nextPosition.y = top - (nextPosition.y - top);
            nextDirection.y = -Mathf.Abs(nextDirection.y);
            reflected = true;
        }

        if (nextDirection.y < 0f && nextPosition.y <= collectorY && nextPosition.x >= left && nextPosition.x <= right)
        {
            collectedPoint = new Vector2(Mathf.Clamp(nextPosition.x, left, right), collectorY);
            return true;
        }

        if (reflected)
        {
            nextDirection = nextDirection.normalized;
        }

        return reflected;
    }
}
