using UnityEngine;

public readonly struct BallProjectileLaunchData
{
    // 中文：VisualTint 用来描述“一整轮球”的统一视觉状态，例如续关后的紫球。
    // English: VisualTint describes the shared visual state for a whole volley, such as the rescue purple balls.
    public BallVolleyController Owner { get; }
    public UIChessBoard ChessBoard { get; }
    public RectTransform SimulationSpace { get; }
    public Vector2 StartLocalPosition { get; }
    public Vector2 Direction { get; }
    public float Speed { get; }
    public float Radius { get; }
    public Rect CollisionBounds { get; }
    public float CollectorY { get; }
    public float CollisionSkin { get; }
    public float SimulationStep { get; }
    public int MaxCollisionsPerStep { get; }
    public float FallbackSubstepDistance { get; }
    public bool CanTriggerSplitSpecial { get; }
    public Color VisualTint { get; }

    public BallProjectileLaunchData(
        BallVolleyController owner,
        UIChessBoard chessBoard,
        RectTransform simulationSpace,
        Vector2 startLocalPosition,
        Vector2 direction,
        float speed,
        float radius,
        Rect collisionBounds,
        float collectorY,
        float collisionSkin,
        float simulationStep,
        int maxCollisionsPerStep,
        float fallbackSubstepDistance,
        bool canTriggerSplitSpecial,
        Color visualTint)
    {
        Owner = owner;
        ChessBoard = chessBoard;
        SimulationSpace = simulationSpace;
        StartLocalPosition = startLocalPosition;
        Direction = direction.normalized;
        Speed = speed;
        Radius = radius;
        CollisionBounds = collisionBounds;
        CollectorY = collectorY;
        CollisionSkin = collisionSkin;
        SimulationStep = simulationStep;
        MaxCollisionsPerStep = maxCollisionsPerStep;
        FallbackSubstepDistance = fallbackSubstepDistance;
        CanTriggerSplitSpecial = canTriggerSplitSpecial;
        VisualTint = visualTint;
    }
}
