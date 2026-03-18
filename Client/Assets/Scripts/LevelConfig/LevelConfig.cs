using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class LevelConfig : MonoBehaviour
{
    [SerializeField]
    [Min(1)]
    private int width = 8;

    [FormerlySerializedAs("height")]
    [SerializeField]
    [Min(1)]
    private int visibleHeight = 10;

    [SerializeField]
    [Min(1)]
    private int totalRows = 10;

    [SerializeField]
    [Min(1)]
    private int dropRowCount = 3;

    [SerializeField]
    [Min(0)]
    private int editRowOffset;

    [SerializeField]
    private RectTransform cellRoot;

    [SerializeField]
    private LevelConfigScritable exportAsset;

    [SerializeField]
    private string exportDirectory = "Assets/AssetBundle/LevelConfig";

    [SerializeField]
    private string exportFileName = "LevelConfig";

    [SerializeField]
    private FCell[] authoredCells = Array.Empty<FCell>();

    public int Width => Mathf.Max(1, width);
    public int VisibleHeight => Mathf.Max(1, visibleHeight);
    public int TotalRows => Mathf.Max(VisibleHeight, totalRows);
    public int DropRowCount => Mathf.Max(1, dropRowCount);
    public int EditRowOffset => ClampEditRowOffset(editRowOffset);
    public RectTransform CellRoot => cellRoot;
    public LevelConfigScritable ExportAsset => exportAsset;

    private void Reset()
    {
        width = Mathf.Max(1, GlobleValue.ChessWidth);
        visibleHeight = Mathf.Max(1, GlobleValue.ChessHeight);
        totalRows = visibleHeight;
        dropRowCount = Mathf.Max(1, Mathf.Min(3, visibleHeight));
        editRowOffset = Mathf.Max(0, totalRows - visibleHeight);
        exportFileName = gameObject.name;
        authoredCells = Array.Empty<FCell>();
        AutoFindCellRoot();
        SyncGridConstraint();
        SyncCellCoordinates();
        LoadVisibleCellsFromAuthoredData();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampConfigValues();
        NormalizeAuthoredCells();
        AutoFindCellRoot();
        SyncGridConstraint();
        SyncCellCoordinates();
        LoadVisibleCellsFromAuthoredData();
    }
#endif

    public void SetExportAsset(LevelConfigScritable inExportAsset)
    {
        exportAsset = inExportAsset;
    }

    public string GetExportDirectory()
    {
        return string.IsNullOrWhiteSpace(exportDirectory) ? "Assets/AssetBundle/LevelConfig" : exportDirectory;
    }

    public string GetExportFileName()
    {
        return string.IsNullOrWhiteSpace(exportFileName) ? gameObject.name : exportFileName;
    }

    public int GetMaxEditRowOffset()
    {
        return Mathf.Max(0, TotalRows - VisibleHeight);
    }

    public int GetInitialVisibleStartRow()
    {
        return GetMaxEditRowOffset();
    }

    public int GetCurrentPageEndRow()
    {
        return Mathf.Min(TotalRows - 1, EditRowOffset + VisibleHeight - 1);
    }

    public int GetVisibleFilledCellCount()
    {
        var count = 0;
        var cells = GetOrderedCells();
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null && cells[i].Life > 0)
            {
                count++;
            }
        }

        return count;
    }

    public int GetTotalFilledCellCount()
    {
        return authoredCells?.Length ?? 0;
    }

    public void AutoFindCellRoot()
    {
        if (cellRoot != null)
        {
            return;
        }

        var gridLayoutGroup = GetComponentInChildren<GridLayoutGroup>(true);
        if (gridLayoutGroup != null)
        {
            cellRoot = gridLayoutGroup.transform as RectTransform;
        }
    }

    public void SetEditRowOffset(int value)
    {
        SaveVisibleCellsToAuthoredData();
        editRowOffset = ClampEditRowOffset(value);
        LoadVisibleCellsFromAuthoredData();
    }

    public void ShiftEditRowOffset(int delta)
    {
        SetEditRowOffset(EditRowOffset + delta);
    }

    public void ClearVisibleCells()
    {
        var cells = GetOrderedCells();
        for (int i = 0; i < cells.Count; i++)
        {
            cells[i].SetData(LevelCellType.Empty, 0);
        }

        SaveVisibleCellsToAuthoredData();
    }

    public void ClearAllCells()
    {
        authoredCells = Array.Empty<FCell>();
        LoadVisibleCellsFromAuthoredData();
    }

    public void CommitVisiblePage()
    {
        SaveVisibleCellsToAuthoredData();
    }

    public void RebuildGrid()
    {
        AutoFindCellRoot();
        if (cellRoot == null)
        {
            return;
        }

        SaveVisibleCellsToAuthoredData();

        var cells = GetOrderedCells();
        var requiredCount = Width * VisibleHeight;

        while (cells.Count < requiredCount)
        {
            var newCell = CreateCell(cells.Count > 0 ? cells[0] : null);
            if (newCell == null)
            {
                break;
            }

            cells.Add(newCell);
        }

        for (int i = cells.Count - 1; i >= requiredCount; i--)
        {
            DestroyCell(cells[i].gameObject);
            cells.RemoveAt(i);
        }

        SyncGridConstraint();
        SyncCellCoordinates();
        LoadVisibleCellsFromAuthoredData();
    }

    public void ExportTo(LevelConfigScritable target)
    {
        if (target == null)
        {
            return;
        }

        SaveVisibleCellsToAuthoredData();
        target.SetBoardInfo(Width, VisibleHeight, TotalRows, DropRowCount);
        target.SetCells(authoredCells);
    }

    public void ImportFrom(LevelConfigScritable source)
    {
        if (source == null)
        {
            return;
        }

        width = Mathf.Max(1, source.Width);
        visibleHeight = Mathf.Max(1, source.VisibleHeight);
        totalRows = Mathf.Max(visibleHeight, source.TotalRows);
        dropRowCount = Mathf.Max(1, source.DropRowCount);
        editRowOffset = Mathf.Max(0, totalRows - visibleHeight);
        authoredCells = CloneCells(source.Cells);

        ClampConfigValues();
        NormalizeAuthoredCells();
        RebuildGrid();
    }

    public LevelCell GetCell(int x, int localY)
    {
        if (x < 0 || x >= Width || localY < 0 || localY >= VisibleHeight)
        {
            return null;
        }

        var cells = GetOrderedCells();
        var index = localY * Width + x;
        return index >= 0 && index < cells.Count ? cells[index] : null;
    }

    public List<LevelCell> GetOrderedCells()
    {
        var cells = new List<LevelCell>();
        if (cellRoot == null)
        {
            return cells;
        }

        for (int i = 0; i < cellRoot.childCount; i++)
        {
            var child = cellRoot.GetChild(i);
            if (child.TryGetComponent(out LevelCell cell))
            {
                cells.Add(cell);
            }
        }

        return cells;
    }

    private void ClampConfigValues()
    {
        width = Mathf.Max(1, width);
        visibleHeight = Mathf.Max(1, visibleHeight);
        totalRows = Mathf.Max(visibleHeight, totalRows);
        dropRowCount = Mathf.Max(1, dropRowCount);
        editRowOffset = ClampEditRowOffset(editRowOffset);

        if (string.IsNullOrWhiteSpace(exportDirectory))
        {
            exportDirectory = "Assets/AssetBundle/LevelConfig";
        }

        if (string.IsNullOrWhiteSpace(exportFileName))
        {
            exportFileName = gameObject.name;
        }
    }

    private int ClampEditRowOffset(int value)
    {
        return Mathf.Clamp(value, 0, Mathf.Max(0, Mathf.Max(visibleHeight, totalRows) - Mathf.Max(1, visibleHeight)));
    }

    private void NormalizeAuthoredCells()
    {
        authoredCells = LevelConfigScritable.BuildNormalizedCells(authoredCells, Width, TotalRows);
    }

    private void SaveVisibleCellsToAuthoredData()
    {
        var windowStart = EditRowOffset;
        var windowEnd = windowStart + VisibleHeight;
        var mergedCells = new Dictionary<Vector2Int, FCell>();

        if (authoredCells != null)
        {
            for (int i = 0; i < authoredCells.Length; i++)
            {
                var cell = authoredCells[i];
                if (cell == null || cell.Y < windowStart || cell.Y >= windowEnd)
                {
                    if (cell != null)
                    {
                        mergedCells[new Vector2Int(cell.X, cell.Y)] = new FCell(cell.X, cell.Y, cell.Type, cell.Life);
                    }
                }
            }
        }

        var cells = GetOrderedCells();
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null || cell.Life <= 0)
            {
                continue;
            }

            var globalY = windowStart + cell.Y;
            mergedCells[new Vector2Int(cell.X, globalY)] = new FCell(cell.X, globalY, cell.Type, cell.Life);
        }

        var mergedList = new List<FCell>(mergedCells.Values);
        authoredCells = LevelConfigScritable.BuildNormalizedCells(mergedList, Width, TotalRows);
    }

    private void LoadVisibleCellsFromAuthoredData()
    {
        var cells = GetOrderedCells();
        if (cells.Count == 0)
        {
            return;
        }

        var authoredMap = BuildAuthoredCellMap();
        for (int i = 0; i < cells.Count; i++)
        {
            var cell = cells[i];
            if (cell == null)
            {
                continue;
            }

            var globalY = EditRowOffset + cell.Y;
            if (authoredMap.TryGetValue(new Vector2Int(cell.X, globalY), out var data))
            {
                cell.SetData(data.Type, data.Life);
            }
            else
            {
                cell.SetData(LevelCellType.Empty, 0);
            }
        }
    }

    private Dictionary<Vector2Int, FCell> BuildAuthoredCellMap()
    {
        var cellMap = new Dictionary<Vector2Int, FCell>();
        if (authoredCells == null)
        {
            return cellMap;
        }

        for (int i = 0; i < authoredCells.Length; i++)
        {
            var cell = authoredCells[i];
            if (cell == null)
            {
                continue;
            }

            cellMap[new Vector2Int(cell.X, cell.Y)] = cell;
        }

        return cellMap;
    }

    private void SyncGridConstraint()
    {
        if (cellRoot == null || !cellRoot.TryGetComponent(out GridLayoutGroup gridLayoutGroup))
        {
            return;
        }

        gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayoutGroup.constraintCount = Width;
    }

    private void SyncCellCoordinates()
    {
        var cells = GetOrderedCells();
        for (int i = 0; i < cells.Count; i++)
        {
            var x = i % Width;
            var localY = i / Width;
            cells[i].Configure(x, localY);
        }
    }

    private LevelCell CreateCell(LevelCell template)
    {
        LevelCell cell;
        if (template != null)
        {
            cell = Instantiate(template, cellRoot);
        }
        else
        {
            var cellObject = new GameObject(
                "Cell",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement),
                typeof(LevelCell));
            cellObject.transform.SetParent(cellRoot, false);
            cell = cellObject.GetComponent<LevelCell>();
        }

        if (cell == null)
        {
            return null;
        }

        cell.transform.SetParent(cellRoot, false);
        cell.gameObject.SetActive(true);
        return cell;
    }

    private static FCell[] CloneCells(IReadOnlyList<FCell> source)
    {
        if (source == null || source.Count == 0)
        {
            return Array.Empty<FCell>();
        }

        var copiedCells = new FCell[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            var cell = source[i];
            copiedCells[i] = cell == null ? null : new FCell(cell.X, cell.Y, cell.Type, cell.Life);
        }

        return copiedCells;
    }

    private static void DestroyCell(GameObject cellObject)
    {
        if (cellObject == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(cellObject);
            return;
        }
#endif

        Destroy(cellObject);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LevelConfig))]
public class LevelConfigEditor : Editor
{
    private const float HeaderWidth = 52f;
    private const float CellSize = 40f;

    private SerializedProperty widthProperty;
    private SerializedProperty visibleHeightProperty;
    private SerializedProperty totalRowsProperty;
    private SerializedProperty dropRowCountProperty;
    private SerializedProperty cellRootProperty;
    private SerializedProperty exportAssetProperty;
    private SerializedProperty exportDirectoryProperty;
    private SerializedProperty exportFileNameProperty;

    private LevelCellType brushType = LevelCellType.Square;
    private int brushLife = 1;
    private Vector2 scrollPosition;

    private void OnEnable()
    {
        widthProperty = serializedObject.FindProperty("width");
        visibleHeightProperty = serializedObject.FindProperty("visibleHeight");
        totalRowsProperty = serializedObject.FindProperty("totalRows");
        dropRowCountProperty = serializedObject.FindProperty("dropRowCount");
        cellRootProperty = serializedObject.FindProperty("cellRoot");
        exportAssetProperty = serializedObject.FindProperty("exportAsset");
        exportDirectoryProperty = serializedObject.FindProperty("exportDirectory");
        exportFileNameProperty = serializedObject.FindProperty("exportFileName");
    }

    public override void OnInspectorGUI()
    {
        var config = (LevelConfig)target;

        serializedObject.Update();
        EditorGUILayout.PropertyField(widthProperty, new GUIContent("Width"));
        EditorGUILayout.PropertyField(visibleHeightProperty, new GUIContent("Visible Rows"));
        EditorGUILayout.PropertyField(totalRowsProperty, new GUIContent("Total Rows"));
        EditorGUILayout.PropertyField(dropRowCountProperty, new GUIContent("Drop Rows"));
        EditorGUILayout.PropertyField(cellRootProperty);
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(exportAssetProperty);
        var exportAssetChanged = EditorGUI.EndChangeCheck();
        EditorGUILayout.PropertyField(exportDirectoryProperty);
        EditorGUILayout.PropertyField(exportFileNameProperty);
        brushType = (LevelCellType)EditorGUILayout.EnumPopup("Brush Type", brushType);
        brushLife = Mathf.Max(0, EditorGUILayout.IntField("Brush Life", brushLife));
        serializedObject.ApplyModifiedProperties();

        if (exportAssetChanged)
        {
            var assignedAsset = exportAssetProperty.objectReferenceValue as LevelConfigScritable;
            if (assignedAsset != null)
            {
                Undo.RecordObject(config, "Load Level Config Asset");
                config.ImportFrom(assignedAsset);
                MarkConfigDirty(config);
                serializedObject.Update();
            }
        }

        var newOffset = EditorGUILayout.IntSlider(
            "Edit Row Offset",
            config.EditRowOffset,
            0,
            config.GetMaxEditRowOffset());
        if (newOffset != config.EditRowOffset)
        {
            Undo.RecordObject(config, "Change Edit Row Offset");
            config.SetEditRowOffset(newOffset);
            MarkConfigDirty(config);
        }

        EditorGUILayout.Space();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Auto Find Grid"))
            {
                config.AutoFindCellRoot();
                MarkConfigDirty(config);
            }

            if (GUILayout.Button("Rebuild Grid"))
            {
                config.RebuildGrid();
                MarkConfigDirty(config);
            }

            if (GUILayout.Button("Clear Page"))
            {
                config.ClearVisibleCells();
                MarkConfigDirty(config);
            }

            if (GUILayout.Button("Clear All"))
            {
                config.ClearAllCells();
                MarkConfigDirty(config);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Prev Page"))
            {
                config.ShiftEditRowOffset(-config.VisibleHeight);
                MarkConfigDirty(config);
            }

            if (GUILayout.Button("Next Page"))
            {
                config.ShiftEditRowOffset(config.VisibleHeight);
                MarkConfigDirty(config);
            }

            if (GUILayout.Button("Import Asset"))
            {
                ImportFromAsset(config);
            }

            if (GUILayout.Button("Export Asset"))
            {
                ExportToAsset(config);
            }
        }

        EnsureGridCellCount(config);

        EditorGUILayout.LabelField("Visible Range", $"{config.EditRowOffset} - {config.GetCurrentPageEndRow()}");
        EditorGUILayout.LabelField(
            "Gameplay Start Range",
            $"{config.GetInitialVisibleStartRow()} - {Mathf.Min(config.TotalRows - 1, config.GetInitialVisibleStartRow() + config.VisibleHeight - 1)}");
        EditorGUILayout.LabelField("Visible Filled Cells", config.GetVisibleFilledCellCount().ToString());
        EditorGUILayout.LabelField("Total Filled Cells", config.GetTotalFilledCellCount().ToString());
        EditorGUILayout.HelpBox(
            "Rows are authored from top to bottom using global Y. Gameplay starts on the bottom-most visible window, and each clear pulls Drop Rows from smaller global Y values above the screen.",
            MessageType.Info);
        EditorGUILayout.HelpBox(
            "Assign a LevelConfigScritable to Export Asset and this editor will immediately restore that map into the visual grid.",
            MessageType.None);

        if (config.CellRoot == null)
        {
            EditorGUILayout.HelpBox(
                "Assign the GridLayoutGroup root to Cell Root, or click Auto Find Grid first.",
                MessageType.Warning);
            return;
        }

        if (config.GetOrderedCells().Count != config.Width * config.VisibleHeight)
        {
            EditorGUILayout.HelpBox(
                "Grid cell count is still mismatched after auto rebuild. Please check the Cell Root object.",
                MessageType.Warning);
        }

        DrawGrid(config);
    }

    private void EnsureGridCellCount(LevelConfig config)
    {
        if (config == null || config.CellRoot == null)
        {
            return;
        }

        var expectedCount = config.Width * config.VisibleHeight;
        var actualCount = config.GetOrderedCells().Count;
        if (actualCount == expectedCount)
        {
            return;
        }

        Undo.RecordObject(config, "Auto Rebuild Level Grid");
        config.RebuildGrid();
        MarkConfigDirty(config);
    }

    private void DrawGrid(LevelConfig config)
    {
        var cells = config.GetOrderedCells();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(HeaderWidth);
            for (int x = 0; x < config.Width; x++)
            {
                GUILayout.Label(x.ToString(), EditorStyles.miniLabel, GUILayout.Width(CellSize));
            }
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(240f));
        for (int localY = 0; localY < config.VisibleHeight; localY++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var globalY = config.EditRowOffset + localY;
                GUILayout.Label($"L{localY}/G{globalY}", EditorStyles.miniLabel, GUILayout.Width(HeaderWidth));

                for (int x = 0; x < config.Width; x++)
                {
                    var index = localY * config.Width + x;
                    var cell = index >= 0 && index < cells.Count ? cells[index] : null;
                    DrawCellButton(config, cell, x, localY, globalY);
                }
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawCellButton(LevelConfig config, LevelCell cell, int x, int localY, int globalY)
    {
        var rect = GUILayoutUtility.GetRect(CellSize, CellSize, GUILayout.Width(CellSize), GUILayout.Height(CellSize));
        var originalColor = GUI.backgroundColor;
        GUI.backgroundColor = cell != null ? LevelCell.GetPreviewColor(cell.Type, cell.Life) : Color.black;

        var label = GetCellLabel(cell);
        var tooltip = $"X:{x} LocalY:{localY} GlobalY:{globalY}";
        if (cell != null)
        {
            tooltip = $"X:{cell.X} LocalY:{cell.Y} GlobalY:{globalY} Type:{cell.Type} Life:{cell.Life}";
        }

        if (GUI.Button(rect, new GUIContent(label, tooltip)))
        {
            PaintCell(config, cell, brushType, brushLife);
        }

        var currentEvent = Event.current;
        if (cell != null
            && currentEvent.type == EventType.MouseDown
            && currentEvent.button == 1
            && rect.Contains(currentEvent.mousePosition))
        {
            PaintCell(config, cell, LevelCellType.Empty, 0);
            currentEvent.Use();
        }

        GUI.backgroundColor = originalColor;
    }

    private void PaintCell(LevelConfig config, LevelCell cell, LevelCellType type, int life)
    {
        if (cell == null)
        {
            return;
        }

        if (type != LevelCellType.Empty)
        {
            life = Mathf.Max(1, life);
        }

        Undo.RecordObject(cell, "Paint Level Cell");
        Undo.RecordObject(config, "Paint Level Cell");
        cell.SetData(type, life);
        config.CommitVisiblePage();
        MarkConfigDirty(config);
    }

    private void ImportFromAsset(LevelConfig config)
    {
        var source = ResolveImportSource(config);
        if (source == null)
        {
            EditorUtility.DisplayDialog(
                "Missing Asset",
                "Select a LevelConfigScritable asset in Project, or assign one to exportAsset before importing.",
                "OK");
            return;
        }

        Undo.RecordObject(config, "Import Level Config");
        config.ImportFrom(source);
        MarkConfigDirty(config);
    }

    private LevelConfigScritable ResolveImportSource(LevelConfig config)
    {
        if (Selection.activeObject is LevelConfigScritable selectedAsset)
        {
            return selectedAsset;
        }

        return config.ExportAsset;
    }

    private void ExportToAsset(LevelConfig config)
    {
        if (!TryResolveExportTarget(config, out var asset, out var assetPath))
        {
            EditorUtility.DisplayDialog("Export Failed", "Could not resolve the export target.", "OK");
            return;
        }

        try
        {
            Undo.RecordObject(asset, "Export Level Config");
            config.ExportTo(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(asset);
            EditorUtility.DisplayDialog("Export Success", $"Exported:\n{assetPath}", "OK");
        }
        catch (Exception exception)
        {
            EditorUtility.DisplayDialog("Export Failed", exception.Message, "OK");
        }
    }

    private bool TryResolveExportTarget(
        LevelConfig config,
        out LevelConfigScritable asset,
        out string assetPath)
    {
        asset = Selection.activeObject as LevelConfigScritable;
        if (asset != null)
        {
            assetPath = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(assetPath);
        }

        if (config.ExportAsset != null)
        {
            asset = config.ExportAsset;
            assetPath = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(assetPath);
        }

        var directory = NormalizeAssetDirectory(config.GetExportDirectory());
        var fileName = SanitizeFileName(config.GetExportFileName());
        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "LevelConfig";
        }

        EnsureFolderExists(directory);
        assetPath = GetNextIndexedAssetPath(directory, fileName);

        asset = CreateInstance<LevelConfigScritable>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return true;
    }

    private static string NormalizeAssetDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return "Assets/AssetBundle/LevelConfig";
        }

        directory = directory.Replace("\\", "/").Trim();
        return directory.StartsWith("Assets") ? directory : $"Assets/{directory.TrimStart('/')}";
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var invalidChar in invalidChars)
        {
            fileName = fileName.Replace(invalidChar.ToString(), string.Empty);
        }

        return fileName.Trim();
    }

    private static void EnsureFolderExists(string directory)
    {
        if (AssetDatabase.IsValidFolder(directory))
        {
            return;
        }

        var segments = directory.Split('/');
        var current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            var next = $"{current}/{segments[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[i]);
            }

            current = next;
        }
    }

    private static string GetNextIndexedAssetPath(string directory, string fileName)
    {
        var index = 1;
        while (true)
        {
            var assetPath = $"{directory}/{fileName}_{index}.asset";
            if (AssetDatabase.LoadAssetAtPath<LevelConfigScritable>(assetPath) == null)
            {
                return assetPath;
            }

            index++;
        }
    }

    private void MarkConfigDirty(LevelConfig config)
    {
        if (config == null)
        {
            return;
        }

        EditorUtility.SetDirty(config);

        if (config.CellRoot != null && config.CellRoot.TryGetComponent(out GridLayoutGroup gridLayoutGroup))
        {
            EditorUtility.SetDirty(gridLayoutGroup);
        }

        var cells = config.GetOrderedCells();
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != null)
            {
                EditorUtility.SetDirty(cells[i]);
            }
        }

        if (config.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(config.gameObject.scene);
        }
    }

    private static string GetCellLabel(LevelCell cell)
    {
        if (cell == null || cell.Type == LevelCellType.Empty || cell.Life <= 0)
        {
            return string.Empty;
        }

        var typeLabel = cell.Type == LevelCellType.Triangle ? "T" : "S";
        return $"{typeLabel}{cell.Life}";
    }
}
#endif
