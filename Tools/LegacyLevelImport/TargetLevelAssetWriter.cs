using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LegacyLevelImportTool;

internal sealed class TargetLevelAssetWriter
{
    private const string LevelConfigScriptGuid = "e1006f56108cecc46b1800745f2abf0a";

    public void WriteAssets(string outputDirectory, IReadOnlyList<LegacyImportedLevel> importedLevels)
    {
        Directory.CreateDirectory(outputDirectory);

        foreach (var importedLevel in importedLevels)
        {
            var assetName = $"Legacy164_Level{importedLevel.Mapping.Level:D4}";
            var assetPath = Path.Combine(outputDirectory, assetName + ".asset");
            File.WriteAllText(assetPath, BuildLevelAsset(assetName, importedLevel), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(
                assetPath + ".meta",
                BuildAssetMeta(assetName),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        var indexPath = Path.Combine(outputDirectory, "Legacy164_LevelIndex.json");
        var indexPayload = importedLevels.Select(level => new
        {
            level.Mapping.Level,
            level.Mapping.AuthorName,
            level.Mapping.MapId,
            level.Mapping.Difficulty,
            Width = level.Map.ColumnCount,
            VisibleHeight = level.Map.VisibleRowCount,
            TotalRows = level.Map.RowCount,
            DropRowCount = level.Mapping.DoNotPush ? 0 : 1,
            CellCount = level.Cells.Count
        });

        File.WriteAllText(
            indexPath,
            JsonSerializer.Serialize(indexPayload, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(
            indexPath + ".meta",
            BuildTextMeta("Legacy164_LevelIndex.json"),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string BuildLevelAsset(string assetName, LegacyImportedLevel importedLevel)
    {
        var builder = new StringBuilder();
        builder.AppendLine("%YAML 1.1");
        builder.AppendLine("%TAG !u! tag:unity3d.com,2011:");
        builder.AppendLine("--- !u!114 &11400000");
        builder.AppendLine("MonoBehaviour:");
        builder.AppendLine("  m_ObjectHideFlags: 0");
        builder.AppendLine("  m_CorrespondingSourceObject: {fileID: 0}");
        builder.AppendLine("  m_PrefabInstance: {fileID: 0}");
        builder.AppendLine("  m_PrefabAsset: {fileID: 0}");
        builder.AppendLine("  m_GameObject: {fileID: 0}");
        builder.AppendLine("  m_Enabled: 1");
        builder.AppendLine("  m_EditorHideFlags: 0");
        builder.AppendLine($"  m_Script: {{fileID: 11500000, guid: {LevelConfigScriptGuid}, type: 3}}");
        builder.AppendLine($"  m_Name: {assetName}");
        builder.AppendLine("  m_EditorClassIdentifier: Assembly-CSharp::LevelConfigScritable");
        builder.AppendLine($"  Width: {importedLevel.Map.ColumnCount}");
        builder.AppendLine($"  VisibleHeight: {importedLevel.Map.VisibleRowCount}");
        builder.AppendLine($"  TotalRows: {importedLevel.Map.RowCount}");
        builder.AppendLine($"  DropRowCount: {(importedLevel.Mapping.DoNotPush ? 0 : 1)}");

        if (importedLevel.Cells.Count == 0)
        {
            builder.AppendLine("  Cells: []");
        }
        else
        {
            builder.AppendLine("  Cells:");
            foreach (var cell in importedLevel.Cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X))
            {
                builder.AppendLine($"  - X: {cell.X}");
                builder.AppendLine($"    Y: {cell.Y}");
                builder.AppendLine($"    Type: {cell.Type}");
                builder.AppendLine($"    Life: {cell.Life}");
                builder.AppendLine($"    LegacyBrickId: {cell.LegacyBrickId}");
                builder.AppendLine($"    LegacyBrickType: {cell.LegacyBrickType}");
                builder.AppendLine($"    LegacyShapeType: {cell.LegacyShapeType}");
                builder.AppendLine($"    LegacyAttributeType: {cell.LegacyAttributeType}");
                builder.AppendLine($"    LegacyToolType: {cell.LegacyToolType}");
                builder.AppendLine($"    LegacyExtraAttributes: {EscapeYamlScalar(cell.LegacyExtraAttributes)}");
                builder.AppendLine($"    LegacyBreakTime: {{x: {cell.LegacyBreakTime.X}, y: {cell.LegacyBreakTime.Y}}}");
                builder.AppendLine($"    LegacyIsMovable: {BoolToInt(cell.LegacyIsMovable)}");
                builder.AppendLine($"    LegacyMovePosition: {cell.LegacyMovePosition.ToString(CultureInfo.InvariantCulture)}");
                builder.AppendLine($"    LegacyIsCustomColor: {BoolToInt(cell.LegacyIsCustomColor)}");
                builder.AppendLine($"    LegacyCustomColorIndex: {cell.LegacyCustomColorIndex}");
                builder.AppendLine($"    LegacySize: {{x: {cell.LegacySize.X}, y: {cell.LegacySize.Y}}}");
                builder.AppendLine($"    LegacyIsSplit: {BoolToInt(cell.LegacyIsSplit)}");
                builder.AppendLine($"    LegacyHitPosition: {{x: {cell.LegacyHitPosition.X}, y: {cell.LegacyHitPosition.Y}}}");
                builder.AppendLine($"    LegacyHitChangeType: {cell.LegacyHitChangeType}");
            }
        }

        builder.AppendLine("  LegacyMetadata:");
        builder.AppendLine("    ImportedFromLegacy164: 1");
        builder.AppendLine($"    SourceLevel: {importedLevel.Mapping.Level}");
        builder.AppendLine($"    SourceMapId: {importedLevel.Map.MapId}");
        builder.AppendLine($"    SourceAuthorName: {EscapeYamlScalar(importedLevel.Mapping.AuthorName)}");
        builder.AppendLine($"    SourceDifficulty: {importedLevel.Mapping.Difficulty}");
        builder.AppendLine($"    SourceBrickRandomRange: {{x: {importedLevel.Mapping.BrickRandomRange.X}, y: {importedLevel.Mapping.BrickRandomRange.Y}}}");
        builder.AppendLine($"    SourceAddCoinBrickRange: {{x: {importedLevel.Mapping.AddCoinBrickRange.X}, y: {importedLevel.Mapping.AddCoinBrickRange.Y}}}");
        builder.AppendLine($"    SourceDoNotPush: {BoolToInt(importedLevel.Mapping.DoNotPush)}");
        builder.AppendLine($"    SourceScoreHex: {EscapeYamlScalar(importedLevel.Map.ScoreHex)}");
        builder.AppendLine($"    StartPosition: {importedLevel.Map.StartPosition.ToString(CultureInfo.InvariantCulture)}");
        builder.AppendLine($"    ColorRange: {{x: {importedLevel.Map.ColorRange.X}, y: {importedLevel.Map.ColorRange.Y}}}");
        builder.AppendLine($"    ColorTopLimit: {{x: {importedLevel.Map.ColorTopLimit.X}, y: {importedLevel.Map.ColorTopLimit.Y}}}");
        builder.AppendLine($"    ChargeMax: {importedLevel.Map.ChargeMax}");
        builder.AppendLine($"    BallCount: {importedLevel.Map.BallCount}");
        AppendQuadArray(builder, "Barriers", importedLevel.Map.Barriers);
        AppendQuadArray(builder, "Rivers", importedLevel.Map.Rivers);
        return builder.ToString();
    }

    private static void AppendQuadArray(StringBuilder builder, string fieldName, IReadOnlyList<IntQuad> values)
    {
        if (values.Count == 0)
        {
            builder.AppendLine($"    {fieldName}: []");
            return;
        }

        builder.AppendLine($"    {fieldName}:");
        foreach (var value in values)
        {
            builder.AppendLine($"    - x: {value.X}");
            builder.AppendLine($"      y: {value.Y}");
            builder.AppendLine($"      z: {value.Z}");
            builder.AppendLine($"      w: {value.W}");
        }
    }

    private static string BuildAssetMeta(string assetName)
    {
        return
            "fileFormatVersion: 2\n" +
            $"guid: {CreateGuid(assetName)}\n" +
            "NativeFormatImporter:\n" +
            "  externalObjects: {}\n" +
            "  mainObjectFileID: 11400000\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";
    }

    private static string BuildTextMeta(string assetName)
    {
        return
            "fileFormatVersion: 2\n" +
            $"guid: {CreateGuid(assetName)}\n" +
            "TextScriptImporter:\n" +
            "  externalObjects: {}\n" +
            "  userData: \n" +
            "  assetBundleName: \n" +
            "  assetBundleVariant: \n";
    }

    private static string CreateGuid(string seed)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(seed));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string EscapeYamlScalar(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        var escaped = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static int BoolToInt(bool value)
    {
        return value ? 1 : 0;
    }
}
