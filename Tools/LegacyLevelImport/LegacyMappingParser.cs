namespace LegacyLevelImportTool;

internal sealed class LegacyMappingParser
{
    public IReadOnlyList<LegacyLevelMappingEntry> Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        var results = new List<LegacyLevelMappingEntry>();
        LegacyLevelMappingEntry? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("%") || line.StartsWith("---"))
            {
                continue;
            }

            if (line.StartsWith("- Level: "))
            {
                if (current != null)
                {
                    results.Add(current);
                }

                current = new LegacyLevelMappingEntry
                {
                    Level = ParseInt(line, "- Level: ")
                };
                continue;
            }

            if (current == null)
            {
                continue;
            }

            if (line.StartsWith("AuthorName: "))
            {
                current.AuthorName = ParseString(line, "AuthorName: ");
            }
            else if (line.StartsWith("MapID: "))
            {
                current.MapId = ParseInt(line, "MapID: ");
            }
            else if (line.StartsWith("Difficulty: "))
            {
                current.Difficulty = ParseInt(line, "Difficulty: ");
            }
            else if (line.StartsWith("BrickRandomRange: "))
            {
                current.BrickRandomRange = IntPair.Parse(ParseString(line, "BrickRandomRange: "));
            }
            else if (line.StartsWith("AddCoinBrickRange: "))
            {
                current.AddCoinBrickRange = IntPair.Parse(ParseString(line, "AddCoinBrickRange: "));
            }
            else if (line.StartsWith("DoNotPush: "))
            {
                current.DoNotPush = ParseBool(line, "DoNotPush: ");
            }
        }

        if (current != null)
        {
            results.Add(current);
        }

        return results;
    }

    private static int ParseInt(string line, string prefix)
    {
        return int.Parse(ParseString(line, prefix), System.Globalization.CultureInfo.InvariantCulture);
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
}
