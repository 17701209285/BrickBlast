using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BallProjectile : MonoBehaviour
{
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
    private float simulationStep;
    private int maxCollisionsPerStep;
    private float fallbackSubstepDistance;
    private float simulationAccumulator;
    private bool isFlying;

    public bool IsFlying => isFlying;

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
        simulationStep = Mathf.Max(0.001f, launchData.SimulationStep);
        maxCollisionsPerStep = Mathf.Max(1, launchData.MaxCollisionsPerStep);
        fallbackSubstepDistance = Mathf.Max(0.5f, launchData.FallbackSubstepDistance);
        simulationAccumulator = 0f;
        isFlying = true;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        ApplyPosition();
    }

    public void ReturnToPool()
    {
        isFlying = false;
        simulationAccumulator = 0f;
        gameObject.SetActive(false);
    }

    private void Simulate(float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return;
        }

        simulationAccumulator += Mathf.Min(deltaTime, 0.05f);
        while (simulationAccumulator >= simulationStep && isFlying)
        {
            SimulateStep(simulationStep);
            simulationAccumulator -= simulationStep;
        }
    }

    private void SimulateStep(float stepDuration)
    {
        var remainingDistance = speed * Mathf.Max(0f, stepDuration);
        var collisionCounter = 0;

        // 中文备注：这里用固定步长来推进球，再在每个步长里处理多次连续反弹。
        // 这种写法会比直接按帧推进稳定很多，更接近 Brick Blast 这类游戏的手感。
        while (remainingDistance > 0.001f && collisionCounter++ < maxCollisionsPerStep)
        {
            if (!BallPhysicsUtility.TryGetNextHit(
                    chessBoard,
                    simulationSpace,
                    collisionBounds,
                    collectorY,
                    localPosition,
                    direction,
                    radius,
                    remainingDistance,
                    collisionSkin,
                    out var hit))
            {
                AdvanceWithFallbackSteps(remainingDistance);
                return;
            }

            localPosition = hit.Point;
            remainingDistance = Mathf.Max(0f, remainingDistance - hit.Distance);

            if (hit.Type == BallCollisionType.Collector)
            {
                ApplyPosition();
                owner?.NotifyProjectileReturned(this, hit.Point);
                return;
            }

            if (hit.Type == BallCollisionType.Block && hit.Block != null)
            {
                var effectResult = chessBoard != null
                    ? chessBoard.ResolveProjectileBlockHit(hit)
                    : default;
                if (TryHandleProjectileHitEffect(effectResult))
                {
                    return;
                }

                if (effectResult.PassThrough)
                {
                    AdvancePassThrough(ref remainingDistance);
                    continue;
                }
            }

            direction = BallPhysicsUtility.Reflect(direction, hit.Normal);
            localPosition += BallPhysicsUtility.GetSeparationOffset(hit.Normal, collisionSkin);
            remainingDistance = Mathf.Max(0f, remainingDistance - collisionSkin);
        }

        ApplyPosition();
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

    private void AdvanceWithFallbackSteps(float remainingDistance)
    {
        var substepLength = Mathf.Max(fallbackSubstepDistance, radius * 0.5f);
        var stepCount = Mathf.Max(1, Mathf.CeilToInt(remainingDistance / substepLength));
        var stepDistance = remainingDistance / stepCount;

        // 中文备注：这是连续碰撞的第二层兜底。
        // 如果某个极端角度没有被提前扫到，只要球真的跨进砖块或越过边界，这里也会修正回来。
        for (int i = 0; i < stepCount && isFlying; i++)
        {
            var nextPosition = localPosition + (direction * stepDistance);
            if (TryResolveBoundsFallback(ref nextPosition, ref direction, out var collectedPoint))
            {
                if (collectedPoint.HasValue)
                {
                    localPosition = collectedPoint.Value;
                    ApplyPosition();
                    owner?.NotifyProjectileReturned(this, collectedPoint.Value);
                    return;
                }

                localPosition = nextPosition;
                continue;
            }

            if (BallPhysicsUtility.TryGetOverlapBlockHit(
                    chessBoard,
                    simulationSpace,
                    nextPosition,
                    radius,
                    collisionSkin,
                    out var overlapHit,
                    out var resolvedPosition))
            {
                localPosition = resolvedPosition + BallPhysicsUtility.GetSeparationOffset(overlapHit.Normal, collisionSkin);

                if (overlapHit.Block != null)
                {
                    LogOverlapFallbackHit(overlapHit, nextPosition, resolvedPosition);
                }

                direction = BallPhysicsUtility.Reflect(direction, overlapHit.Normal);
                continue;
            }

            localPosition = nextPosition;
        }

        ApplyPosition();
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

    private void AdvancePassThrough(ref float remainingDistance)
    {
        var passThroughOffset = GetPassThroughOffset();
        localPosition += direction * passThroughOffset;
        remainingDistance = Mathf.Max(0f, remainingDistance - passThroughOffset);
    }

    private float GetPassThroughOffset()
    {
        return Mathf.Max(collisionSkin, radius * BallShootingConstants.PassThroughOffsetRadiusRatio);
    }

    private bool TryResolveBoundsFallback(ref Vector2 nextPosition, ref Vector2 nextDirection, out Vector2? collectedPoint)
    {
        collectedPoint = null;

        var left = collisionBounds.xMin + radius;
        var right = collisionBounds.xMax - radius;
        var top = collisionBounds.yMax - radius;
        var reflected = false;

        // 中文备注：这是边界兜底。
        // 主碰撞应该优先算出反弹；如果某一小步漏判了，这里至少保证球不会直接飞出棋盘。
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

    private void LogOverlapFallbackHit(in BallCollisionHit overlapHit, Vector2 nextPosition, Vector2 resolvedPosition)
    {
        var primary = DescribeBlock(overlapHit.Block);
        var additional = overlapHit.AdditionalBlock != null
            ? $", Additional={DescribeBlock(overlapHit.AdditionalBlock)}"
            : string.Empty;
        Debug.LogError(
            $"[BallProjectile] Overlap fallback detected. Damage was skipped because the projectile entered block interior without a swept boundary hit. " +
            $"Projectile={name}, Primary={primary}{additional}, Next={nextPosition}, Resolved={resolvedPosition}, Current={localPosition}, Direction={direction}, Radius={radius}, Skin={collisionSkin}",
            this);
    }

    private static string DescribeBlock(ChessElement block)
    {
        if (block == null)
        {
            return "null";
        }

        return $"[{block.name}] X={block.X} Y={block.Y} Type={block.Type} Life={block.Life}";
    }
}
