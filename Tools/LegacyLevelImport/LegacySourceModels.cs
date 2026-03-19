using System.Globalization;

namespace LegacyLevelImportTool;

internal sealed class LegacyLevelMappingEntry
{
    public int Level { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public int MapId { get; set; }

    public int Difficulty { get; set; }

    public IntPair BrickRandomRange { get; set; }

    public IntPair AddCoinBrickRange { get; set; }

    public bool DoNotPush { get; set; }
}

internal sealed class LegacySourceMap
{
    public int MapId { get; set; }

    public List<List<LegacySourceBrick>> Rows { get; } = new();

    public int RowCount { get; set; }

    public int VisibleRowCount { get; set; }

    public int ColumnCount { get; set; }

    public string ScoreHex { get; set; } = string.Empty;

    public float StartPosition { get; set; }

    public IntPair ColorRange { get; set; }

    public IntPair ColorTopLimit { get; set; }

    public int ChargeMax { get; set; }

    public int BallCount { get; set; }

    public List<IntQuad> Barriers { get; } = new();

    public List<IntQuad> Rivers { get; } = new();
}

internal sealed class LegacySourceBrick
{
    public int BrickId { get; set; }

    public int BrickType { get; set; }

    public int ShapeType { get; set; }

    public int AttributeType { get; set; }

    public int BrickToolType { get; set; }

    public string ExtraAttributes { get; set; } = string.Empty;

    public IntPair BreakTime { get; set; }

    public bool IsMovable { get; set; }

    public float MovePosition { get; set; }

    public bool IsCustomColor { get; set; }

    public int CustomColorIndex { get; set; }

    public IntPair Size { get; set; }

    public bool IsSplit { get; set; }

    public IntPair HitPosition { get; set; }

    public int HitChangeType { get; set; }
}

internal sealed class LegacyImportedLevel
{
    public LegacyLevelMappingEntry Mapping { get; init; } = new();

    public LegacySourceMap Map { get; init; } = new();

    public List<TargetCell> Cells { get; init; } = new();
}

internal sealed class TargetCell
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Type { get; init; }

    public int Life { get; init; }

    public int LegacyBrickId { get; init; }

    public int LegacyBrickType { get; init; }

    public int LegacyShapeType { get; init; }

    public int LegacyAttributeType { get; init; }

    public int LegacyToolType { get; init; }

    public string LegacyExtraAttributes { get; init; } = string.Empty;

    public IntPair LegacyBreakTime { get; init; }

    public bool LegacyIsMovable { get; init; }

    public float LegacyMovePosition { get; init; }

    public bool LegacyIsCustomColor { get; init; }

    public int LegacyCustomColorIndex { get; init; }

    public IntPair LegacySize { get; init; }

    public bool LegacyIsSplit { get; init; }

    public IntPair LegacyHitPosition { get; init; }

    public int LegacyHitChangeType { get; init; }
}

internal readonly record struct IntPair(int X, int Y)
{
    public static IntPair Parse(string raw)
    {
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"\{x:\s*(-?\d+),\s*y:\s*(-?\d+)\}");
        if (!match.Success)
        {
            return default;
        }

        return new IntPair(
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }
}

internal readonly record struct IntQuad(int X, int Y, int Z, int W);
