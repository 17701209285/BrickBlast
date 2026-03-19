using System.Globalization;

namespace LegacyLevelImportTool;

internal sealed class LegacyMapAssetParser
{
    private enum Section
    {
        None,
        Bricks,
        Barriers,
        Rivers
    }

    public IReadOnlyDictionary<int, LegacySourceMap> Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        var maps = new Dictionary<int, LegacySourceMap>();

        LegacySourceMap? currentMap = null;
        List<LegacySourceBrick>? currentRow = null;
        LegacySourceBrick? currentBrick = null;
        IntQuadBuilder? currentQuad = null;
        Section section = Section.None;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("%") || line.StartsWith("---"))
            {
                continue;
            }

            if (line.StartsWith("- MapID: "))
            {
                FinalizeQuad(currentMap, section, currentQuad);
                currentQuad = null;
                currentBrick = null;
                currentRow = null;
                section = Section.None;

                currentMap = new LegacySourceMap
                {
                    MapId = ParseInt(line, "- MapID: ")
                };
                maps[currentMap.MapId] = currentMap;
                continue;
            }

            if (currentMap == null)
            {
                continue;
            }

            if (line == "Bricks:")
            {
                FinalizeQuad(currentMap, section, currentQuad);
                currentQuad = null;
                currentBrick = null;
                currentRow = null;
                section = Section.Bricks;
                continue;
            }

            if (line == "Barriers:")
            {
                FinalizeQuad(currentMap, section, currentQuad);
                currentQuad = null;
                currentBrick = null;
                currentRow = null;
                section = Section.Barriers;
                continue;
            }

            if (line == "Barriers: []")
            {
                FinalizeQuad(currentMap, section, currentQuad);
                currentQuad = null;
                currentBrick = null;
                currentRow = null;
                section = Section.None;
                continue;
            }

            if (line == "Rivers:")
            {
                FinalizeQuad(currentMap, section, currentQuad);
                currentQuad = null;
                currentBrick = null;
                currentRow = null;
                section = Section.Rivers;
                continue;
            }

            if (line == "Rivers: []")
            {
                FinalizeQuad(currentMap, section, currentQuad);
                currentQuad = null;
                currentBrick = null;
                currentRow = null;
                section = Section.None;
                continue;
            }

            if (section == Section.Bricks)
            {
                if (line.StartsWith("- list:"))
                {
                    currentBrick = null;
                    currentRow = new List<LegacySourceBrick>();
                    currentMap.Rows.Add(currentRow);
                    continue;
                }

                if (line.StartsWith("- BrickID: "))
                {
                    currentBrick = new LegacySourceBrick
                    {
                        BrickId = ParseInt(line, "- BrickID: ")
                    };

                    currentRow ??= new List<LegacySourceBrick>();
                    if (currentMap.Rows.Count == 0 || !ReferenceEquals(currentMap.Rows[^1], currentRow))
                    {
                        currentMap.Rows.Add(currentRow);
                    }

                    currentRow.Add(currentBrick);
                    continue;
                }

                if (currentBrick != null && TryParseBrickField(currentBrick, line))
                {
                    continue;
                }
            }

            if (section == Section.Barriers || section == Section.Rivers)
            {
                if (line.StartsWith("- m_X: "))
                {
                    FinalizeQuad(currentMap, section, currentQuad);
                    currentQuad = new IntQuadBuilder
                    {
                        X = ParseInt(line, "- m_X: ")
                    };
                    continue;
                }

                if (currentQuad != null)
                {
                    if (line.StartsWith("m_Y: "))
                    {
                        currentQuad.Y = ParseInt(line, "m_Y: ");
                        continue;
                    }

                    if (line.StartsWith("m_Z: "))
                    {
                        currentQuad.Z = ParseInt(line, "m_Z: ");
                        continue;
                    }

                    if (line.StartsWith("m_W: "))
                    {
                        currentQuad.W = ParseInt(line, "m_W: ");
                        continue;
                    }
                }
            }

            FinalizeQuad(currentMap, section, currentQuad);
            currentQuad = null;
            currentBrick = null;

            if (line.StartsWith("Row: "))
            {
                section = Section.None;
                currentMap.RowCount = ParseInt(line, "Row: ");
            }
            else if (line.StartsWith("VisibleRow: "))
            {
                currentMap.VisibleRowCount = ParseInt(line, "VisibleRow: ");
            }
            else if (line.StartsWith("Column: "))
            {
                currentMap.ColumnCount = ParseInt(line, "Column: ");
            }
            else if (line.StartsWith("Score: "))
            {
                currentMap.ScoreHex = ParseString(line, "Score: ");
            }
            else if (line.StartsWith("StartPosition: "))
            {
                currentMap.StartPosition = ParseFloat(line, "StartPosition: ");
            }
            else if (line.StartsWith("ColorRange: "))
            {
                currentMap.ColorRange = IntPair.Parse(ParseString(line, "ColorRange: "));
            }
            else if (line.StartsWith("ColorTopLimit: "))
            {
                currentMap.ColorTopLimit = IntPair.Parse(ParseString(line, "ColorTopLimit: "));
            }
            else if (line.StartsWith("ChargeMax: "))
            {
                currentMap.ChargeMax = ParseInt(line, "ChargeMax: ");
            }
            else if (line.StartsWith("BallCount: "))
            {
                currentMap.BallCount = ParseInt(line, "BallCount: ");
            }
        }

        FinalizeQuad(currentMap, section, currentQuad);
        return maps;
    }

    private static bool TryParseBrickField(LegacySourceBrick brick, string line)
    {
        if (line.StartsWith("BrickType: "))
        {
            brick.BrickType = ParseInt(line, "BrickType: ");
            return true;
        }

        if (line.StartsWith("ShapeType: "))
        {
            brick.ShapeType = ParseInt(line, "ShapeType: ");
            return true;
        }

        if (line.StartsWith("AttributeType: "))
        {
            brick.AttributeType = ParseInt(line, "AttributeType: ");
            return true;
        }

        if (line.StartsWith("BrickToolType: "))
        {
            brick.BrickToolType = ParseInt(line, "BrickToolType: ");
            return true;
        }

        if (line.StartsWith("ExtraAttributes:"))
        {
            brick.ExtraAttributes = line.Length == "ExtraAttributes:".Length
                ? string.Empty
                : ParseString(line, "ExtraAttributes: ");
            return true;
        }

        if (line.StartsWith("BreakTime: "))
        {
            brick.BreakTime = IntPair.Parse(ParseString(line, "BreakTime: "));
            return true;
        }

        if (line.StartsWith("IsMovable: "))
        {
            brick.IsMovable = ParseBool(line, "IsMovable: ");
            return true;
        }

        if (line.StartsWith("MovePosition: "))
        {
            brick.MovePosition = ParseFloat(line, "MovePosition: ");
            return true;
        }

        if (line.StartsWith("IsCustomColor: "))
        {
            brick.IsCustomColor = ParseBool(line, "IsCustomColor: ");
            return true;
        }

        if (line.StartsWith("CustomColorIndex: "))
        {
            brick.CustomColorIndex = ParseInt(line, "CustomColorIndex: ");
            return true;
        }

        if (line.StartsWith("Size: "))
        {
            brick.Size = IntPair.Parse(ParseString(line, "Size: "));
            return true;
        }

        if (line.StartsWith("IsSplit: "))
        {
            brick.IsSplit = ParseBool(line, "IsSplit: ");
            return true;
        }

        if (line.StartsWith("HitPosition: "))
        {
            brick.HitPosition = IntPair.Parse(ParseString(line, "HitPosition: "));
            return true;
        }

        if (line.StartsWith("HitChangeType: "))
        {
            brick.HitChangeType = ParseInt(line, "HitChangeType: ");
            return true;
        }

        return false;
    }

    private static void FinalizeQuad(LegacySourceMap? currentMap, Section section, IntQuadBuilder? quad)
    {
        if (currentMap == null || quad == null)
        {
            return;
        }

        var value = quad.Build();
        if (section == Section.Barriers)
        {
            currentMap.Barriers.Add(value);
        }
        else if (section == Section.Rivers)
        {
            currentMap.Rivers.Add(value);
        }
    }

    private static int ParseInt(string line, string prefix)
    {
        return int.Parse(ParseString(line, prefix), CultureInfo.InvariantCulture);
    }

    private static float ParseFloat(string line, string prefix)
    {
        return float.Parse(ParseString(line, prefix), CultureInfo.InvariantCulture);
    }

    private static bool ParseBool(string line, string prefix)
    {
        var value = ParseString(line, prefix);
        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string ParseString(string line, string prefix)
    {
        return line.Substring(prefix.Length).Trim();
    }

    private sealed class IntQuadBuilder
    {
        public int X { get; init; }

        public int Y { get; set; }

        public int Z { get; set; }

        public int W { get; set; }

        public IntQuad Build()
        {
            return new IntQuad(X, Y, Z, W);
        }
    }
}
