using UnityEngine;

public readonly struct ChessBoardImpactSummary
{
    public ChessDamageSource Source { get; }
    public Vector2 HitPointInBoardSpace { get; }
    public int DamagedBrickCount { get; }
    public int DestroyedBrickCount { get; }
    public LevelCellType TriggeredSpecialType { get; }
    public bool HasTriggeredSpecial => LevelCellTypeUtility.IsSpecial(TriggeredSpecialType);
    public bool HasAnyImpact => DamagedBrickCount > 0 || HasTriggeredSpecial;

    public ChessBoardImpactSummary(
        ChessDamageSource source,
        Vector2 hitPointInBoardSpace,
        int damagedBrickCount,
        int destroyedBrickCount,
        LevelCellType triggeredSpecialType)
    {
        Source = source;
        HitPointInBoardSpace = hitPointInBoardSpace;
        DamagedBrickCount = Mathf.Max(0, damagedBrickCount);
        DestroyedBrickCount = Mathf.Max(0, destroyedBrickCount);
        TriggeredSpecialType = triggeredSpecialType;
    }
}
