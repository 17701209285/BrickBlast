using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public enum LevelCellType
{
    Square = 0,
    Triangle = 1,
    Empty = 2
}

[Serializable]
public class FCell
{
    public int X;
    public int Y;
    public LevelCellType Type;
    public int Life;

    public FCell()
    {
    }

    public FCell(int x, int y, LevelCellType type, int life)
    {
        X = x;
        Y = y;
        Type = type;
        Life = life;
    }
}

[CreateAssetMenu(fileName = "LevelConfig", menuName = "Scriptable Objects/LevelConfig")]
public class LevelConfigScritable : ScriptableObject
{
    [Min(1)]
    public int Width = 8;

    [FormerlySerializedAs("Height")]
    [Min(1)]
    public int VisibleHeight = 10;

    [Min(1)]
    public int TotalRows = 10;

    [Min(1)]
    public int DropRowCount = 3;

    public FCell[] Cells = Array.Empty<FCell>();

    public void SetBoardInfo(int width, int visibleHeight, int totalRows, int dropRowCount)
    {
        Width = Mathf.Max(1, width);
        VisibleHeight = Mathf.Max(1, visibleHeight);
        TotalRows = Mathf.Max(VisibleHeight, totalRows);
        DropRowCount = Mathf.Max(1, dropRowCount);
    }

    public void SetCells(IReadOnlyList<FCell> cells)
    {
        Cells = BuildNormalizedCells(cells, Width, TotalRows);
    }

    public int GetLife(int x, int y)
    {
        var cell = FindCell(x, y);
        return cell == null ? 0 : Mathf.Max(0, cell.Life);
    }

    public LevelCellType GetCellType(int x, int y)
    {
        var cell = FindCell(x, y);
        return cell == null ? LevelCellType.Empty : NormalizeType(cell.Type, cell.Life);
    }

    public bool IsValidCoordinate(int x, int y)
    {
        return x >= 0 && x < Width && y >= 0 && y < TotalRows;
    }

    public int GetInitialVisibleStartRow()
    {
        return Mathf.Max(0, TotalRows - VisibleHeight);
    }

    public int GetRemainingDropRowCount(int visibleStartRow)
    {
        return Mathf.Clamp(visibleStartRow, 0, TotalRows);
    }

    public bool HasPendingDropRows(int visibleStartRow)
    {
        return GetRemainingDropRowCount(visibleStartRow) > 0;
    }

    public int GetNextDropSourceRow(int visibleStartRow)
    {
        var rowCount = GetNextDropCount(visibleStartRow);
        return Mathf.Clamp(visibleStartRow - rowCount, 0, TotalRows);
    }

    public int GetNextDropCount(int visibleStartRow)
    {
        return Mathf.Min(DropRowCount, GetRemainingDropRowCount(visibleStartRow));
    }

    public FCell[] GetInitialBoardCells()
    {
        return GetCellsInGlobalRowRange(GetInitialVisibleStartRow(), VisibleHeight);
    }

    public FCell[] GetDropBatchCells(int visibleStartRow)
    {
        var sourceRow = GetNextDropSourceRow(visibleStartRow);
        var rowCount = GetNextDropCount(visibleStartRow);
        return GetCellsInGlobalRowRange(sourceRow, rowCount);
    }

    public FCell[] GetCellsInGlobalRowRange(int startRow, int rowCount)
    {
        if (rowCount <= 0 || Cells == null || Cells.Length == 0)
        {
            return Array.Empty<FCell>();
        }

        var normalizedStartRow = Mathf.Clamp(startRow, 0, TotalRows);
        var normalizedEndRow = Mathf.Clamp(normalizedStartRow + rowCount, normalizedStartRow, TotalRows);
        if (normalizedStartRow >= normalizedEndRow)
        {
            return Array.Empty<FCell>();
        }

        var visibleCells = new List<FCell>();
        for (int i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];
            if (cell == null || cell.Y < normalizedStartRow || cell.Y >= normalizedEndRow)
            {
                continue;
            }

            visibleCells.Add(new FCell(cell.X, cell.Y - normalizedStartRow, cell.Type, cell.Life));
        }

        visibleCells.Sort(CompareCell);
        return visibleCells.ToArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Width = Mathf.Max(1, Width);
        VisibleHeight = Mathf.Max(1, VisibleHeight);
        TotalRows = Mathf.Max(VisibleHeight, TotalRows);
        DropRowCount = Mathf.Max(1, DropRowCount);
        NormalizeCells();
    }
#endif

    public static FCell[] BuildNormalizedCells(IReadOnlyList<FCell> cells, int width, int totalRows)
    {
        if (cells == null || cells.Count == 0)
        {
            return Array.Empty<FCell>();
        }

        width = Mathf.Max(1, width);
        totalRows = Mathf.Max(1, totalRows);

        var uniqueCells = new Dictionary<Vector2Int, FCell>();
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null)
            {
                continue;
            }

            if (cell.X < 0 || cell.X >= width || cell.Y < 0 || cell.Y >= totalRows || cell.Life <= 0)
            {
                continue;
            }

            uniqueCells[new Vector2Int(cell.X, cell.Y)] = new FCell(
                cell.X,
                cell.Y,
                NormalizeType(cell.Type, cell.Life),
                cell.Life);
        }

        if (uniqueCells.Count == 0)
        {
            return Array.Empty<FCell>();
        }

        var normalizedCells = new List<FCell>(uniqueCells.Values);
        normalizedCells.Sort(CompareCell);
        return normalizedCells.ToArray();
    }

    private void NormalizeCells()
    {
        Cells = BuildNormalizedCells(Cells, Width, TotalRows);
    }

    private FCell FindCell(int x, int y)
    {
        if (Cells == null)
        {
            return null;
        }

        for (int i = 0; i < Cells.Length; i++)
        {
            var cell = Cells[i];
            if (cell != null && cell.X == x && cell.Y == y)
            {
                return cell;
            }
        }

        return null;
    }

    private static int CompareCell(FCell left, FCell right)
    {
        var yCompare = left.Y.CompareTo(right.Y);
        return yCompare != 0 ? yCompare : left.X.CompareTo(right.X);
    }

    private static LevelCellType NormalizeType(LevelCellType type, int life)
    {
        return life <= 0 ? LevelCellType.Empty : type;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LevelConfigScritable))]
public class LevelConfigScritableEditor : Editor
{
    private const float HeaderWidth = 52f;
    private const float CellSize = 40f;

    private GUIStyle previewLabelStyle;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        var config = (LevelConfigScritable)target;
        if (config == null)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Map Preview", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Preview Range", $"0 - {Mathf.Max(0, config.TotalRows - 1)}");
        EditorGUILayout.LabelField("Filled Cells", (config.Cells?.Length ?? 0).ToString());
        EditorGUILayout.HelpBox(
            "This is a read-only preview of the exported map. The full map is shown at once so you can inspect the entire authored layout directly in the asset inspector.",
            MessageType.Info);

        DrawGridPreview(config);
    }

    private void DrawGridPreview(LevelConfigScritable config)
    {
        if (config.Width <= 0 || config.VisibleHeight <= 0)
        {
            return;
        }

        var cellMap = BuildCellMap(config);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(HeaderWidth);
            for (int x = 0; x < config.Width; x++)
            {
                GUILayout.Label(x.ToString(), EditorStyles.miniLabel, GUILayout.Width(CellSize));
            }
        }

        for (int globalY = 0; globalY < config.TotalRows; globalY++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"G{globalY}", EditorStyles.miniLabel, GUILayout.Width(HeaderWidth));

                for (int x = 0; x < config.Width; x++)
                {
                    cellMap.TryGetValue(new Vector2Int(x, globalY), out var cell);
                    DrawPreviewCell(cell, x, globalY);
                }
            }
        }
    }

    private void DrawPreviewCell(FCell cell, int x, int globalY)
    {
        var type = cell == null ? LevelCellType.Empty : cell.Type;
        var life = cell == null ? 0 : cell.Life;
        var rect = GUILayoutUtility.GetRect(CellSize, CellSize, GUILayout.Width(CellSize), GUILayout.Height(CellSize));

        EditorGUI.DrawRect(rect, LevelCell.GetPreviewColor(type, life));
        GUI.Box(rect, GUIContent.none);

        var label = GetCellLabel(cell);
        if (!string.IsNullOrEmpty(label))
        {
            GUI.Label(
                rect,
                new GUIContent(label, $"X:{x} GlobalY:{globalY} Type:{type} Life:{life}"),
                GetPreviewLabelStyle());
        }
    }

    private static Dictionary<Vector2Int, FCell> BuildCellMap(LevelConfigScritable config)
    {
        var cellMap = new Dictionary<Vector2Int, FCell>();
        if (config.Cells == null)
        {
            return cellMap;
        }

        for (int i = 0; i < config.Cells.Length; i++)
        {
            var cell = config.Cells[i];
            if (cell == null || cell.Life <= 0)
            {
                continue;
            }

            cellMap[new Vector2Int(cell.X, cell.Y)] = cell;
        }

        return cellMap;
    }

    private GUIStyle GetPreviewLabelStyle()
    {
        if (previewLabelStyle != null)
        {
            return previewLabelStyle;
        }

        previewLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
        {
            alignment = TextAnchor.MiddleCenter
        };
        previewLabelStyle.normal.textColor = Color.black;
        return previewLabelStyle;
    }
    private static string GetCellLabel(FCell cell)
    {
        if (cell == null || cell.Life <= 0 || cell.Type == LevelCellType.Empty)
        {
            return string.Empty;
        }

        return $"{(cell.Type == LevelCellType.Triangle ? "T" : "S")}{cell.Life}";
    }
}
#endif
