using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class LevelCell : MonoBehaviour
{
    private static readonly Color PreviewEmptyColor = new Color(0.635f, 0.635f, 0.635f, 1f);
    private static readonly Color PreviewSquareLowLifeColor = new Color(0.95f, 0.78f, 0.32f, 1f);
    private static readonly Color PreviewSquareHighLifeColor = new Color(0.88f, 0.29f, 0.23f, 1f);
    private static readonly Color PreviewTriangleLowLifeColor = new Color(0.44f, 0.78f, 0.97f, 1f);
    private static readonly Color PreviewTriangleHighLifeColor = new Color(0.08f, 0.45f, 0.89f, 1f);
    private static readonly Color PreviewHorizontalBlastLowLifeColor = new Color(0.47f, 0.89f, 0.46f, 1f);
    private static readonly Color PreviewHorizontalBlastHighLifeColor = new Color(0.16f, 0.67f, 0.31f, 1f);
    private static readonly Color PreviewVerticalBlastLowLifeColor = new Color(0.39f, 0.86f, 0.93f, 1f);
    private static readonly Color PreviewVerticalBlastHighLifeColor = new Color(0.10f, 0.56f, 0.89f, 1f);
    private static readonly Color PreviewSplitThreeWayLowLifeColor = new Color(1.00f, 0.63f, 0.37f, 1f);
    private static readonly Color PreviewSplitThreeWayHighLifeColor = new Color(0.93f, 0.32f, 0.60f, 1f);
    private static readonly Color PreviewRedirectLowLifeColor = new Color(0.99f, 0.78f, 0.42f, 1f);
    private static readonly Color PreviewRedirectHighLifeColor = new Color(0.91f, 0.46f, 0.20f, 1f);
    private static readonly Color PreviewCrossBlastLowLifeColor = new Color(0.55f, 0.92f, 0.70f, 1f);
    private static readonly Color PreviewCrossBlastHighLifeColor = new Color(0.14f, 0.68f, 0.49f, 1f);
    private static readonly Color PreviewExtraBallsLowLifeColor = new Color(0.76f, 0.96f, 0.49f, 1f);
    private static readonly Color PreviewExtraBallsHighLifeColor = new Color(0.35f, 0.73f, 0.17f, 1f);

    [SerializeField]
    private int x;

    [SerializeField]
    private int y;

    [SerializeField]
    private LevelCellType cellType = LevelCellType.Square;

    [SerializeField]
    [Min(0)]
    private int life;

    [SerializeField]
    [HideInInspector]
    private Image previewImage;

    public int X => x;
    public int Y => y;
    public LevelCellType Type => LevelCellTypeUtility.NormalizeType(cellType, life);
    public int Life => LevelCellTypeUtility.NormalizeLife(cellType, life);

    private void Reset()
    {
        CacheComponents();
        RefreshPreview();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        life = Mathf.Max(0, life);
        cellType = LevelCellTypeUtility.NormalizeType(cellType, life);
        life = LevelCellTypeUtility.NormalizeLife(cellType, life);
        CacheComponents();
        RefreshPreview();
    }
#endif

    public void Configure(int inX, int inY)
    {
        x = inX;
        y = inY;
        RefreshPreview();
    }

    public void SetData(LevelCellType inType, int inLife)
    {
        cellType = LevelCellTypeUtility.NormalizeType(inType, inLife);
        life = LevelCellTypeUtility.NormalizeLife(cellType, inLife);
        RefreshPreview();
    }

    public void SetType(LevelCellType inType)
    {
        cellType = inType;
        if (cellType == LevelCellType.Empty)
        {
            life = 0;
        }
        else if (LevelCellTypeUtility.IsSpecial(cellType))
        {
            life = 0;
        }
        else if (life <= 0)
        {
            life = 1;
        }

        cellType = LevelCellTypeUtility.NormalizeType(cellType, life);
        life = LevelCellTypeUtility.NormalizeLife(cellType, life);

        RefreshPreview();
    }

    public void SetLife(int inLife)
    {
        var nextType = cellType;
        if (nextType == LevelCellType.Empty && inLife > 0)
        {
            nextType = LevelCellType.Square;
        }

        cellType = LevelCellTypeUtility.NormalizeType(nextType, inLife);
        life = LevelCellTypeUtility.NormalizeLife(cellType, inLife);

        RefreshPreview();
    }

    public void RefreshPreview()
    {
        CacheComponents();
        name = $"Cell [{x},{y}] Type:{Type} Life:{life}";

        if (previewImage != null)
        {
            previewImage.color = GetPreviewColor(Type, life);
        }
    }

    public static Color GetPreviewColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty)
        {
            return PreviewEmptyColor;
        }

        var t = LevelCellTypeUtility.UsesLife(type)
            ? Mathf.Clamp01((currentLife - 1) / 8f)
            : LevelCellTypeConstants.SpecialVisualBlendFactor;
        switch (type)
        {
            case LevelCellType.Triangle:
                return Color.Lerp(PreviewTriangleLowLifeColor, PreviewTriangleHighLifeColor, t);
            case LevelCellType.HorizontalBlast:
                return Color.Lerp(PreviewHorizontalBlastLowLifeColor, PreviewHorizontalBlastHighLifeColor, t);
            case LevelCellType.VerticalBlast:
                return Color.Lerp(PreviewVerticalBlastLowLifeColor, PreviewVerticalBlastHighLifeColor, t);
            case LevelCellType.SplitThreeWay:
                return Color.Lerp(PreviewSplitThreeWayLowLifeColor, PreviewSplitThreeWayHighLifeColor, t);
            case LevelCellType.Redirect:
                return Color.Lerp(PreviewRedirectLowLifeColor, PreviewRedirectHighLifeColor, t);
            case LevelCellType.CrossBlast:
                return Color.Lerp(PreviewCrossBlastLowLifeColor, PreviewCrossBlastHighLifeColor, t);
            case LevelCellType.ExtraBalls:
                return Color.Lerp(PreviewExtraBallsLowLifeColor, PreviewExtraBallsHighLifeColor, t);
            default:
                return Color.Lerp(PreviewSquareLowLifeColor, PreviewSquareHighLifeColor, t);
        }
    }

    public static string GetPreviewLifeLabel(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty || LevelCellTypeUtility.IsSpecial(type))
        {
            return string.Empty;
        }

        return currentLife <= 0 ? string.Empty : currentLife.ToString();
    }

    public static string GetPreviewTypeMarker(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty)
        {
            return string.Empty;
        }

        switch (type)
        {
            case LevelCellType.Triangle:
                return "T";
            case LevelCellType.HorizontalBlast:
                return "H";
            case LevelCellType.VerticalBlast:
                return "V";
            case LevelCellType.SplitThreeWay:
                return "3";
            case LevelCellType.Redirect:
                return "R";
            case LevelCellType.CrossBlast:
                return "X";
            case LevelCellType.ExtraBalls:
                return "E";
            default:
                return string.Empty;
        }
    }

    public static Color GetPreviewTextColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty)
        {
            return new Color(0f, 0f, 0f, 0f);
        }

        var backgroundColor = GetPreviewColor(type, currentLife);
        var luminance = (backgroundColor.r * 0.299f) + (backgroundColor.g * 0.587f) + (backgroundColor.b * 0.114f);
        return luminance < 0.58f ? Color.white : new Color(0.12f, 0.12f, 0.12f, 1f);
    }

    public static Color GetPreviewOutlineColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty)
        {
            return new Color(0f, 0f, 0f, 0f);
        }

        var textColor = GetPreviewTextColor(type, currentLife);
        return textColor.grayscale < 0.5f
            ? new Color(1f, 1f, 1f, 0.45f)
            : new Color(0f, 0f, 0f, 0.65f);
    }

    private void CacheComponents()
    {
        if (previewImage == null)
        {
            previewImage = GetComponent<Image>();
        }
    }
}
