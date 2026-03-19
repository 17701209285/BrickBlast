namespace LegacyLevelImportTool;

internal static class Program
{
    private const string SourceRoot = @"E:\workspace\export_unity\Brick+Blast_1.6.4\ExportedProject\Assets\BrickBlast\assetbundles\gameassets\normalmapdata";
    private const string MappingPath = @"E:\workspace\export_unity\Brick+Blast_1.6.4\ExportedProject\Assets\BrickBlast\assetbundles\gameassets\normalmapdata\BBBrickMapMappingData.asset";
    private const string OutputDirectory = @"E:\workspace\Projects\BrickBlast\Client\Assets\AssetBundle\ImportedLevels\Legacy164";

    private static readonly Dictionary<string, string> AuthorAssetFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BOOSTER"] = "BBBrickMapDataBOOSTER.asset",
        ["L"] = "BBBrickMapDataL.asset",
        ["NEW"] = "BBBrickMapDataNEW.asset",
        ["NewMapVersion"] = "BBBrickMapDataNewMapVersion.asset",
        ["OPT"] = "BBBrickMapDataOPT.asset",
        ["specialyza"] = "BBBrickMapDataspecialyza.asset",
        ["TESTYXH"] = "BBBrickMapDataTESTYXH.asset",
        ["TESTYZA"] = "BBBrickMapDataTESTYZA.asset",
        ["YXH"] = "BBBrickMapDataYXH.asset",
        ["YZA"] = "BBBrickMapDataYZA.asset",
        ["ZAY"] = "BBBrickMapDataZAY.asset"
    };

    public static int Main()
    {
        var mappingEntries = new LegacyMappingParser().Parse(MappingPath);
        Console.WriteLine($"mapping_levels={mappingEntries.Count}");

        var parser = new LegacyMapAssetParser();
        var authorMaps = new Dictionary<string, IReadOnlyDictionary<int, LegacySourceMap>>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in AuthorAssetFileNames)
        {
            var assetPath = Path.Combine(SourceRoot, pair.Value);
            if (!File.Exists(assetPath))
            {
                Console.WriteLine($"WARN missing author asset: {pair.Key} => {assetPath}");
                continue;
            }

            authorMaps[pair.Key] = parser.Parse(assetPath);
            Console.WriteLine($"parsed_author={pair.Key} maps={authorMaps[pair.Key].Count}");
        }

        var importedLevels = new List<LegacyImportedLevel>(mappingEntries.Count);
        foreach (var mapping in mappingEntries.OrderBy(entry => entry.Level))
        {
            if (!authorMaps.TryGetValue(mapping.AuthorName, out var mapsById))
            {
                Console.WriteLine($"WARN missing author maps for level={mapping.Level} author={mapping.AuthorName}");
                continue;
            }

            if (!mapsById.TryGetValue(mapping.MapId, out var map))
            {
                Console.WriteLine($"WARN missing map for level={mapping.Level} author={mapping.AuthorName} mapId={mapping.MapId}");
                continue;
            }

            importedLevels.Add(new LegacyImportedLevel
            {
                Mapping = mapping,
                Map = map,
                Cells = BuildTargetCells(map)
            });
        }

        Console.WriteLine($"importable_levels={importedLevels.Count}");
        new TargetLevelAssetWriter().WriteAssets(OutputDirectory, importedLevels);
        Console.WriteLine($"wrote_assets={OutputDirectory}");
        return 0;
    }

    private static List<TargetCell> BuildTargetCells(LegacySourceMap map)
    {
        var cells = new List<TargetCell>();
        for (var y = 0; y < map.Rows.Count; y++)
        {
            var row = map.Rows[y];
            for (var x = 0; x < row.Count; x++)
            {
                var brick = row[x];
                if (brick.BrickId <= 0 || brick.BrickType <= 0)
                {
                    continue;
                }

                var previewType = MapPreviewType(brick);
                if (previewType == 2)
                {
                    continue;
                }

                cells.Add(new TargetCell
                {
                    X = x,
                    Y = y,
                    Type = previewType,
                    Life = MapPreviewLife(previewType, brick.BreakTime),
                    LegacyBrickId = brick.BrickId,
                    LegacyBrickType = brick.BrickType,
                    LegacyShapeType = brick.ShapeType,
                    LegacyAttributeType = brick.AttributeType,
                    LegacyToolType = brick.BrickToolType,
                    LegacyExtraAttributes = brick.ExtraAttributes,
                    LegacyBreakTime = brick.BreakTime,
                    LegacyIsMovable = brick.IsMovable,
                    LegacyMovePosition = brick.MovePosition,
                    LegacyIsCustomColor = brick.IsCustomColor,
                    LegacyCustomColorIndex = brick.CustomColorIndex,
                    LegacySize = brick.Size,
                    LegacyIsSplit = brick.IsSplit,
                    LegacyHitPosition = brick.HitPosition,
                    LegacyHitChangeType = brick.HitChangeType
                });
            }
        }

        return cells;
    }

    private static int MapPreviewType(LegacySourceBrick brick)
    {
        if (brick.BrickType == 0)
        {
            return 2;
        }

        if (brick.BrickType == 2)
        {
            return MapToolPreviewType(brick.BrickToolType, brick.ExtraAttributes);
        }

        if (brick.BrickType == 13)
        {
            return 5;
        }

        return IsTriangleShape(brick.ShapeType) ? 1 : 0;
    }

    private static int MapPreviewLife(int previewType, IntPair breakTime)
    {
        return previewType switch
        {
            3 or 4 or 5 => 0,
            _ => Math.Max(1, breakTime.X)
        };
    }

    private static int MapToolPreviewType(int toolType, string extraAttributes)
    {
        if (toolType == 3)
        {
            var extraValue = ExtractPrimaryExtraInt(extraAttributes);
            return extraValue == 2 ? 4 : 3;
        }

        if (toolType == 1)
        {
            return 5;
        }

        return 0;
    }

    private static bool IsTriangleShape(int shapeType)
    {
        return shapeType is 2 or 3 or 4 or 5 or 6;
    }

    private static int ExtractPrimaryExtraInt(string extraAttributes)
    {
        if (string.IsNullOrWhiteSpace(extraAttributes))
        {
            return 0;
        }

        var token = extraAttributes.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries)[0];
        return int.TryParse(token, out var value) ? value : 0;
    }
}
