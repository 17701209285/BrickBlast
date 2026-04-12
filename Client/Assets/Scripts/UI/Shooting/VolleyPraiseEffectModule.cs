using System;
using UnityEngine;

public enum VolleyPraiseTier
{
    None = 0,
    Awesome = 1,
    Excellent = 2,
    Perfect = 3
}

[Serializable]
public sealed class VolleyPraiseSettings
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private bool allowDuringVictory;
    [SerializeField] private RectTransform effectParent;
    [SerializeField] private bool useCustomAnchoredPosition;
    [SerializeField] private Vector2 effectAnchoredPosition;

    [Header("Awesome Threshold")]
    [SerializeField] [Min(0)] private int awesomeDamagedBricks = 4;
    [SerializeField] [Min(0)] private int awesomeDestroyedBricks = 1;
    [SerializeField] [Min(0)] private int awesomeSpecialTriggers = 2;
    [SerializeField] [Range(1, 3)] private int awesomeRequiredMatches = 1;

    [Header("Excellent Threshold")]
    [SerializeField] [Min(0)] private int excellentDamagedBricks = 8;
    [SerializeField] [Min(0)] private int excellentDestroyedBricks = 3;
    [SerializeField] [Min(0)] private int excellentSpecialTriggers = 3;
    [SerializeField] [Range(1, 3)] private int excellentRequiredMatches = 2;

    [Header("Perfect Threshold")]
    [SerializeField] [Min(0)] private int perfectDamagedBricks = 12;
    [SerializeField] [Min(0)] private int perfectDestroyedBricks = 5;
    [SerializeField] [Min(0)] private int perfectSpecialTriggers = 4;
    [SerializeField] [Range(1, 3)] private int perfectRequiredMatches = 3;

    public bool Enabled => enabled;
    public bool AllowDuringVictory => allowDuringVictory;
    public RectTransform EffectParent => effectParent;
    public Vector2? EffectAnchoredPosition => useCustomAnchoredPosition ? effectAnchoredPosition : (Vector2?)null;

    public VolleyPraiseTier Evaluate(in VolleyPraiseMetrics metrics, LevelSettlementResult settlementResult)
    {
        if (!enabled || settlementResult == LevelSettlementResult.Defeat)
        {
            return VolleyPraiseTier.None;
        }

        if (settlementResult == LevelSettlementResult.Victory && !allowDuringVictory)
        {
            return VolleyPraiseTier.None;
        }

        if (metrics.Meets(perfectDamagedBricks, perfectDestroyedBricks, perfectSpecialTriggers, perfectRequiredMatches))
        {
            return VolleyPraiseTier.Perfect;
        }

        if (metrics.Meets(excellentDamagedBricks, excellentDestroyedBricks, excellentSpecialTriggers, excellentRequiredMatches))
        {
            return VolleyPraiseTier.Excellent;
        }

        if (metrics.Meets(awesomeDamagedBricks, awesomeDestroyedBricks, awesomeSpecialTriggers, awesomeRequiredMatches))
        {
            return VolleyPraiseTier.Awesome;
        }

        return VolleyPraiseTier.None;
    }
}

public readonly struct VolleyPraiseMetrics
{
    public int DamagedBrickCount { get; }
    public int DestroyedBrickCount { get; }
    public int TriggeredSpecialCount { get; }

    public VolleyPraiseMetrics(int damagedBrickCount, int destroyedBrickCount, int triggeredSpecialCount)
    {
        DamagedBrickCount = Mathf.Max(0, damagedBrickCount);
        DestroyedBrickCount = Mathf.Max(0, destroyedBrickCount);
        TriggeredSpecialCount = Mathf.Max(0, triggeredSpecialCount);
    }

    public VolleyPraiseMetrics AddImpact(in ChessBoardImpactSummary summary)
    {
        return new VolleyPraiseMetrics(
            DamagedBrickCount + summary.DamagedBrickCount,
            DestroyedBrickCount + summary.DestroyedBrickCount,
            TriggeredSpecialCount + (summary.HasTriggeredSpecial ? 1 : 0));
    }

    public bool Meets(int damagedBrickCount, int destroyedBrickCount, int triggeredSpecialCount, int requiredMatches)
    {
        var matchedCount = 0;
        if (DamagedBrickCount >= Mathf.Max(0, damagedBrickCount))
        {
            matchedCount++;
        }

        if (DestroyedBrickCount >= Mathf.Max(0, destroyedBrickCount))
        {
            matchedCount++;
        }

        if (TriggeredSpecialCount >= Mathf.Max(0, triggeredSpecialCount))
        {
            matchedCount++;
        }

        return matchedCount >= Mathf.Clamp(requiredMatches, 1, 3);
    }
}

internal sealed class VolleyPraiseEffectModule
{
    private readonly UIChessBoard chessBoard;
    private readonly VolleyPraiseSettings settings;
    private VolleyPraiseMetrics metrics;
    private bool volleyActive;

    public VolleyPraiseEffectModule(UIChessBoard chessBoard, VolleyPraiseSettings settings)
    {
        this.chessBoard = chessBoard;
        this.settings = settings;
    }

    public void BeginVolley()
    {
        volleyActive = true;
        metrics = default;
    }

    public void CancelVolley()
    {
        volleyActive = false;
        metrics = default;
    }

    public void RegisterImpact(in ChessBoardImpactSummary summary)
    {
        if (!volleyActive || !summary.HasAnyImpact)
        {
            return;
        }

        metrics = metrics.AddImpact(summary);
    }

    public void CompleteVolley(LevelSettlementResult settlementResult)
    {
        if (!volleyActive)
        {
            return;
        }

        volleyActive = false;
        var tier = settings == null ? VolleyPraiseTier.None : settings.Evaluate(metrics, settlementResult);
        metrics = default;
        if (tier == VolleyPraiseTier.None || chessBoard == null)
        {
            return;
        }

        chessBoard.PlayPraiseEffect(tier, settings.EffectParent, settings.EffectAnchoredPosition);
    }
}
