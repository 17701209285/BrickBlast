using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class UIChessBoardLayoutController : MonoBehaviour
{
    [SerializeField]
    private RectTransform BoardArea;

    [SerializeField]
    private RectTransform BoardVisualContainer;

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
    private Image boardVisualImage;
    private int currentColumns = -1;
    private int currentRows = -1;
    private Vector2 lastRootSize = Vector2.zero;
    private bool baselineCaptured;
    private Rect baselineBoardRectInRootSpace;
    private Vector2 baselineBoardReferenceAnchorMin;
    private Vector2 baselineBoardReferenceAnchorMax;
    private Vector2 baselineBoardReferencePivot;
    private Vector2 baselineBoardReferenceAnchoredPosition;
    private Vector2 baselineBoardReferenceSizeDelta;
    private Vector2 baselineLaunchAnchoredPosition;
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
        ConfigureBoardVisual(cellSize);
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

        if (BoardVisualContainer == null && BoardArea != null)
        {
            var boardParent = BoardArea.parent as RectTransform;
            if (boardParent != null && boardParent != rootRectTransform && boardParent.GetComponent<Image>() != null)
            {
                BoardVisualContainer = boardParent;
            }
            else
            {
                BoardVisualContainer = BoardArea;
            }
        }

        if (LaunchBall == null)
        {
            LaunchBall = transform.Find("Ball") as RectTransform;
        }

        if (LegacyGridLayout == null && BoardArea != null)
        {
            LegacyGridLayout = BoardArea.GetComponent<GridLayoutGroup>();
        }

        if (LegacyGridLayout != null)
        {
            CellSpacing = LegacyGridLayout.spacing;
        }

        if (boardVisualImage == null && BoardVisualContainer != null)
        {
            boardVisualImage = BoardVisualContainer.GetComponent<Image>();
        }
    }

    private void CaptureBaseline(Rect rootRect)
    {
        var boardReference = BoardVisualContainer != null ? BoardVisualContainer : BoardArea;
        if (baselineCaptured || boardReference == null || LaunchBall == null)
        {
            return;
        }

        baselineBoardRectInRootSpace = GetRectInRootSpace(boardReference);
        baselineBoardReferenceAnchorMin = boardReference.anchorMin;
        baselineBoardReferenceAnchorMax = boardReference.anchorMax;
        baselineBoardReferencePivot = boardReference.pivot;
        baselineBoardReferenceAnchoredPosition = boardReference.anchoredPosition;
        baselineBoardReferenceSizeDelta = boardReference.sizeDelta;
        baselineLaunchAnchoredPosition = LaunchBall.anchoredPosition;
        baselineCaptured = true;
    }

    private float CalculateCellSize(Rect rootRect, int columns, int rows)
    {
        var horizontalSpacing = CellSpacing.x * Mathf.Max(0, columns - 1);
        var verticalSpacing = CellSpacing.y * Mathf.Max(0, rows - 1);
        var boardReference = BoardVisualContainer != null ? BoardVisualContainer : BoardArea;
        var boardRect = baselineCaptured
            ? baselineBoardRectInRootSpace
            : (boardReference != null ? GetRectInRootSpace(boardReference) : rootRect);
        var availableWidth = Mathf.Max(1f, boardRect.width - horizontalSpacing);
        var availableHeight = Mathf.Max(1f, boardRect.height - verticalSpacing);
        var widthLimited = availableWidth / columns;
        var heightLimited = availableHeight / rows;
        return Mathf.Max(1f, Mathf.Min(MaxCellSize, Mathf.Min(widthLimited, heightLimited)));
    }

    private void ConfigureBoardArea(float boardWidth, float boardHeight)
    {
        var boardReference = BoardVisualContainer != null ? BoardVisualContainer : BoardArea;
        if (boardReference == null)
        {
            return;
        }

        if (BoardArea != null && BoardArea != boardReference)
        {
            RestoreBoardReference(boardReference);

            var horizontalInset = Mathf.Max(0f, (baselineBoardRectInRootSpace.width - boardWidth) * 0.5f);
            var verticalInset = Mathf.Max(0f, (baselineBoardRectInRootSpace.height - boardHeight) * 0.5f);

            BoardArea.anchorMin = new Vector2(0f, 1f);
            BoardArea.anchorMax = new Vector2(0f, 1f);
            BoardArea.pivot = new Vector2(0f, 1f);
            BoardArea.anchoredPosition = new Vector2(horizontalInset, -verticalInset);
            BoardArea.sizeDelta = new Vector2(boardWidth, boardHeight);
            return;
        }

        boardReference.anchorMin = baselineBoardReferenceAnchorMin;
        boardReference.anchorMax = baselineBoardReferenceAnchorMax;
        boardReference.pivot = baselineBoardReferencePivot;
        boardReference.anchoredPosition = baselineBoardReferenceAnchoredPosition;
        boardReference.sizeDelta = new Vector2(boardWidth, boardHeight);
    }

    private void ConfigureBoardVisual(float cellSize)
    {
        if (boardVisualImage == null || boardVisualImage.type != Image.Type.Tiled || boardVisualImage.sprite == null)
        {
            return;
        }

        var tileSize = cellSize + CellSpacing.x;
        if (tileSize <= 0f)
        {
            return;
        }

        var pixelsPerUnit = boardVisualImage.pixelsPerUnit;
        if (pixelsPerUnit <= 0f)
        {
            return;
        }

        boardVisualImage.pixelsPerUnitMultiplier = boardVisualImage.sprite.rect.width / (tileSize * pixelsPerUnit);
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
        LaunchBall.anchoredPosition = new Vector2(0f, baselineLaunchAnchoredPosition.y);
    }

    private void RestoreBoardReference(RectTransform boardReference)
    {
        if (boardReference == null || !baselineCaptured)
        {
            return;
        }

        boardReference.anchorMin = baselineBoardReferenceAnchorMin;
        boardReference.anchorMax = baselineBoardReferenceAnchorMax;
        boardReference.pivot = baselineBoardReferencePivot;
        boardReference.anchoredPosition = baselineBoardReferenceAnchoredPosition;
        boardReference.sizeDelta = baselineBoardReferenceSizeDelta;
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
