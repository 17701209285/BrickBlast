using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIChessBoardLayoutController : MonoBehaviour
{
    [SerializeField]
    private RectTransform BoardArea;

    [SerializeField]
    private RectTransform LaunchBall;

    [SerializeField]
    private GridLayoutGroup LegacyGridLayout;

    [SerializeField]
    private Vector2 CellSpacing = new Vector2(3f, 3f);

    [SerializeField]
    [Min(1f)]
    private float MaxCellSize = 129f;

    private RectTransform rootRectTransform;
    private int currentColumns = -1;
    private int currentRows = -1;
    private Vector2 lastRootSize = Vector2.zero;
    private bool baselineCaptured;
    private float boardTopInset;
    private float launchOriginY;
    private float launchGap;
    private readonly Vector3[] cornerBuffer = new Vector3[4];

    public void ApplyLayout(int columns, int rows, ChessElement template, Transform boardParentTransform)
    {
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);

        EnsureReferences(boardParentTransform);
        if (rootRectTransform == null || BoardArea == null)
        {
            return;
        }

        var rootRect = rootRectTransform.rect;
        if (rootRect.width <= 0f || rootRect.height <= 0f)
        {
            return;
        }

        CaptureBaseline(rootRect);

        var rootSize = rootRect.size;
        if (currentColumns == columns && currentRows == rows && Approximately(rootSize, lastRootSize))
        {
            return;
        }

        var cellSize = CalculateCellSize(rootRect, columns, rows);
        var cellSizeVector = new Vector2(cellSize, cellSize);
        var boardWidth = GetAxisLength(cellSize, CellSpacing.x, columns);
        var boardHeight = GetAxisLength(cellSize, CellSpacing.y, rows);

        ConfigureBoardArea(boardWidth, boardHeight);
        ConfigureTemplate(template, cellSizeVector);
        ConfigureRuntimeElements(cellSizeVector);
        ConfigureLegacyGrid(columns, cellSizeVector);
        ConfigureLaunchBall();

        currentColumns = columns;
        currentRows = rows;
        lastRootSize = rootSize;
    }

    private void OnRectTransformDimensionsChange()
    {
        if (currentColumns <= 0 || currentRows <= 0)
        {
            return;
        }

        ApplyLayout(currentColumns, currentRows, null, BoardArea);
    }

    private void EnsureReferences(Transform boardParentTransform)
    {
        if (rootRectTransform == null)
        {
            rootRectTransform = transform as RectTransform;
        }

        if (BoardArea == null)
        {
            BoardArea = boardParentTransform as RectTransform;
        }

        if (BoardArea == null)
        {
            BoardArea = transform.Find("Grid Layout") as RectTransform;
        }

        if (LaunchBall == null)
        {
            LaunchBall = transform.Find("Ball") as RectTransform;
        }

        if (LegacyGridLayout == null && BoardArea != null)
        {
            LegacyGridLayout = BoardArea.GetComponent<GridLayoutGroup>();
        }
    }

    private void CaptureBaseline(Rect rootRect)
    {
        if (baselineCaptured || BoardArea == null || LaunchBall == null)
        {
            return;
        }

        var boardRect = GetRectInRootSpace(BoardArea);
        var launchRect = GetRectInRootSpace(LaunchBall);
        boardTopInset = Mathf.Max(0f, rootRect.yMax - boardRect.yMax);
        launchOriginY = launchRect.center.y;
        launchGap = Mathf.Max(0f, boardRect.yMin - launchOriginY);
        baselineCaptured = true;
    }

    private float CalculateCellSize(Rect rootRect, int columns, int rows)
    {
        var horizontalSpacing = CellSpacing.x * Mathf.Max(0, columns - 1);
        var verticalSpacing = CellSpacing.y * Mathf.Max(0, rows - 1);
        var availableWidth = Mathf.Max(1f, rootRect.width - horizontalSpacing);
        var boardTopY = rootRect.yMax - boardTopInset;
        var boardBottomLimitY = launchOriginY + launchGap;
        var availableHeight = Mathf.Max(1f, boardTopY - boardBottomLimitY - verticalSpacing);
        var widthLimited = availableWidth / columns;
        var heightLimited = availableHeight / rows;
        return Mathf.Max(1f, Mathf.Min(MaxCellSize, Mathf.Min(widthLimited, heightLimited)));
    }

    private void ConfigureBoardArea(float boardWidth, float boardHeight)
    {
        var boardTopY = rootRectTransform.rect.yMax - boardTopInset;

        BoardArea.anchorMin = new Vector2(0.5f, 0.5f);
        BoardArea.anchorMax = new Vector2(0.5f, 0.5f);
        BoardArea.pivot = new Vector2(0.5f, 1f);
        BoardArea.anchoredPosition = new Vector2(0f, boardTopY);
        BoardArea.sizeDelta = new Vector2(boardWidth, boardHeight);
    }

    private void ConfigureTemplate(ChessElement template, Vector2 cellSize)
    {
        if (template == null)
        {
            return;
        }

        template.ApplyRuntimeLayout(cellSize, CellSpacing, Vector2.zero);
    }

    private void ConfigureRuntimeElements(Vector2 cellSize)
    {
        if (BoardArea == null)
        {
            return;
        }

        var elements = BoardArea.GetComponentsInChildren<ChessElement>(true);
        for (int i = 0; i < elements.Length; i++)
        {
            elements[i].ApplyRuntimeLayout(cellSize, CellSpacing, Vector2.zero);
        }
    }

    private void ConfigureLegacyGrid(int columns, Vector2 cellSize)
    {
        if (LegacyGridLayout == null)
        {
            return;
        }

        LegacyGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        LegacyGridLayout.constraintCount = columns;
        LegacyGridLayout.cellSize = cellSize;
        LegacyGridLayout.spacing = CellSpacing;
    }

    private void ConfigureLaunchBall()
    {
        if (LaunchBall == null)
        {
            return;
        }

        LaunchBall.anchorMin = new Vector2(0.5f, 0.5f);
        LaunchBall.anchorMax = new Vector2(0.5f, 0.5f);
        LaunchBall.pivot = new Vector2(0.5f, 0.5f);
        LaunchBall.anchoredPosition = new Vector2(0f, launchOriginY);
    }

    private static float GetAxisLength(float cellSize, float spacing, int count)
    {
        return (cellSize * count) + (spacing * Mathf.Max(0, count - 1));
    }

    private static bool Approximately(Vector2 a, Vector2 b)
    {
        return Mathf.Abs(a.x - b.x) <= 0.01f && Mathf.Abs(a.y - b.y) <= 0.01f;
    }

    private Rect GetRectInRootSpace(RectTransform target)
    {
        target.GetWorldCorners(cornerBuffer);

        var min = (Vector2)rootRectTransform.InverseTransformPoint(cornerBuffer[0]);
        var max = min;
        for (int i = 1; i < cornerBuffer.Length; i++)
        {
            var localCorner = (Vector2)rootRectTransform.InverseTransformPoint(cornerBuffer[i]);
            min = Vector2.Min(min, localCorner);
            max = Vector2.Max(max, localCorner);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
