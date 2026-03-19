internal sealed class ChessBoardImpactAccumulator
{
    private readonly ChessDamageSource source;
    private readonly UnityEngine.Vector2 hitPointInBoardSpace;

    public int DamagedBrickCount { get; private set; }
    public int DestroyedBrickCount { get; private set; }
    public LevelCellType TriggeredSpecialType { get; private set; } = LevelCellType.Empty;
    public bool HasAnyImpact => DamagedBrickCount > 0 || LevelCellTypeUtility.IsSpecial(TriggeredSpecialType);

    public ChessBoardImpactAccumulator(ChessDamageSource source, UnityEngine.Vector2 hitPointInBoardSpace)
    {
        this.source = source;
        this.hitPointInBoardSpace = hitPointInBoardSpace;
    }

    public void RegisterDamage(in ChessHitEffectContext hitContext)
    {
        if (hitContext.PreviousLife <= hitContext.CurrentLife)
        {
            return;
        }

        DamagedBrickCount++;
        if (hitContext.IsDestroyed)
        {
            DestroyedBrickCount++;
        }
    }

    public void RegisterSpecialTrigger(LevelCellType specialType)
    {
        if (!LevelCellTypeUtility.IsSpecial(specialType))
        {
            return;
        }

        TriggeredSpecialType = specialType;
    }

    public ChessBoardImpactSummary BuildSummary()
    {
        return new ChessBoardImpactSummary(
            source,
            hitPointInBoardSpace,
            DamagedBrickCount,
            DestroyedBrickCount,
            TriggeredSpecialType);
    }
}
