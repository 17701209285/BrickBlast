using System;
using UnityEngine;

[Serializable]
public sealed class VolleyScoreSettings
{
    [SerializeField] private bool enabled = true;
    [SerializeField] private bool awardPointsForSpecialTriggers = false;
    [SerializeField] [Min(0)] private int damageBrickPoints = 10;
    [SerializeField] [Min(0)] private int destroyedBrickBonus = 40;
    [SerializeField] [Min(0)] private int horizontalBlastBonus = 0;
    [SerializeField] [Min(0)] private int verticalBlastBonus = 0;
    [SerializeField] [Min(0)] private int crossBlastBonus = 0;
    [SerializeField] [Min(0)] private int splitThreeWayBonus = 0;
    [SerializeField] [Min(0)] private int redirectBonus = 0;
    [SerializeField] [Min(0)] private int extraBallsBonus = 0;
    [SerializeField] [Min(0)] private int victoryBonus = 500;
    [SerializeField] [Min(0)] private int defeatBonus = 0;

    public bool Enabled => enabled;
    public bool AwardPointsForSpecialTriggers => awardPointsForSpecialTriggers;
    public int DamageBrickPoints => Mathf.Max(0, damageBrickPoints);
    public int DestroyedBrickBonus => Mathf.Max(0, destroyedBrickBonus);
    public int VictoryBonus => Mathf.Max(0, victoryBonus);
    public int DefeatBonus => Mathf.Max(0, defeatBonus);

    public int GetSpecialTriggerBonus(LevelCellType specialType)
    {
        switch (specialType)
        {
            case LevelCellType.HorizontalBlast:
                return Mathf.Max(0, horizontalBlastBonus);
            case LevelCellType.VerticalBlast:
                return Mathf.Max(0, verticalBlastBonus);
            case LevelCellType.CrossBlast:
                return Mathf.Max(0, crossBlastBonus);
            case LevelCellType.SplitThreeWay:
                return Mathf.Max(0, splitThreeWayBonus);
            case LevelCellType.Redirect:
                return Mathf.Max(0, redirectBonus);
            case LevelCellType.ExtraBalls:
                return Mathf.Max(0, extraBallsBonus);
            default:
                return 0;
        }
    }
}

public readonly struct VolleyScoreSnapshot
{
    public string LevelKey { get; }
    public int CurrentScore { get; }
    public int BestScore { get; }

    public VolleyScoreSnapshot(string levelKey, int currentScore, int bestScore)
    {
        LevelKey = levelKey ?? string.Empty;
        CurrentScore = Mathf.Max(0, currentScore);
        BestScore = Mathf.Max(0, bestScore);
    }
}

public sealed class VolleyScoreModule
{
    private const string PlayerPrefsPrefix = "brickblast.score.best.";

    private readonly UIChessBoard chessBoard;
    private readonly VolleyScoreSettings settings;

    private int currentScore;
    private int bestScore;
    private string currentLevelKey = string.Empty;
    private bool settlementBonusApplied;
    private bool bestScoreDirty;

    public VolleyScoreSnapshot Snapshot => new VolleyScoreSnapshot(currentLevelKey, currentScore, bestScore);

    public VolleyScoreModule(UIChessBoard chessBoard, VolleyScoreSettings settings)
    {
        this.chessBoard = chessBoard;
        this.settings = settings ?? new VolleyScoreSettings();
    }

    public void ResetForLevel(string levelAddress)
    {
        SaveBestScoreIfNeeded();
        currentLevelKey = ResolveLevelKey(levelAddress);
        currentScore = 0;
        bestScore = LoadBestScore(currentLevelKey);
        settlementBonusApplied = false;
        bestScoreDirty = false;
        RefreshScoreView();
    }

    public void RegisterImpact(ChessBoardImpactSummary impactSummary)
    {
        if (!settings.Enabled || !impactSummary.HasAnyImpact)
        {
            return;
        }

        var delta = 0;
        delta += impactSummary.DamagedBrickCount * settings.DamageBrickPoints;
        delta += impactSummary.DestroyedBrickCount * settings.DestroyedBrickBonus;

        // 道具本体默认不直接给分，只统计它真正造成的砖块受伤/击毁收益。
        if (settings.AwardPointsForSpecialTriggers && impactSummary.HasTriggeredSpecial)
        {
            delta += settings.GetSpecialTriggerBonus(impactSummary.TriggeredSpecialType);
        }

        AddScore(delta);
    }

    public void CompleteSettlement(LevelSettlementResult settlementResult)
    {
        if (!settings.Enabled || settlementBonusApplied)
        {
            return;
        }

        settlementBonusApplied = true;
        switch (settlementResult)
        {
            case LevelSettlementResult.Victory:
                AddScore(settings.VictoryBonus);
                break;
            case LevelSettlementResult.Defeat:
                AddScore(settings.DefeatBonus);
                break;
        }

        SaveBestScoreIfNeeded();
    }

    public void ResumeAfterContinue()
    {
        settlementBonusApplied = false;
        RefreshScoreView();
    }

    private void AddScore(int delta)
    {
        if (delta <= 0)
        {
            return;
        }

        currentScore += delta;
        if (currentScore > bestScore)
        {
            bestScore = currentScore;
            bestScoreDirty = true;
        }

        RefreshScoreView();
    }

    private void RefreshScoreView()
    {
        chessBoard?.SetScore(currentScore);
    }

    private static string ResolveLevelKey(string levelAddress)
    {
        if (string.IsNullOrWhiteSpace(levelAddress))
        {
            return "default";
        }

        return levelAddress.Trim();
    }

    private static int LoadBestScore(string levelKey)
    {
        return PlayerPrefs.GetInt(BuildPlayerPrefsKey(levelKey), 0);
    }

    private void SaveBestScoreIfNeeded()
    {
        if (!bestScoreDirty)
        {
            return;
        }

        SaveBestScore(currentLevelKey, bestScore);
        bestScoreDirty = false;
    }

    private static void SaveBestScore(string levelKey, int value)
    {
        PlayerPrefs.SetInt(BuildPlayerPrefsKey(levelKey), Mathf.Max(0, value));
        PlayerPrefs.Save();
    }

    private static string BuildPlayerPrefsKey(string levelKey)
    {
        return PlayerPrefsPrefix + (levelKey ?? "default");
    }
}
