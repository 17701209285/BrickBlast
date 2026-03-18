using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class LevelCell : MonoBehaviour
{
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
    public LevelCellType Type => life <= 0 ? LevelCellType.Empty : cellType;
    public int Life => life;

    private void Reset()
    {
        CacheComponents();
        RefreshPreview();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        life = Mathf.Max(0, life);
        if (life <= 0)
        {
            cellType = LevelCellType.Empty;
        }
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
        life = Mathf.Max(0, inLife);
        cellType = life <= 0 ? LevelCellType.Empty : inType;
        RefreshPreview();
    }

    public void SetType(LevelCellType inType)
    {
        cellType = inType;
        if (cellType == LevelCellType.Empty)
        {
            life = 0;
        }
        else if (life <= 0)
        {
            life = 1;
        }

        RefreshPreview();
    }

    public void SetLife(int inLife)
    {
        life = Mathf.Max(0, inLife);
        if (life <= 0)
        {
            cellType = LevelCellType.Empty;
        }
        else if (cellType == LevelCellType.Empty)
        {
            cellType = LevelCellType.Square;
        }

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
        if (type == LevelCellType.Empty || currentLife <= 0)
        {
            return new Color(0.635f, 0.635f, 0.635f, 1f);
        }

        var t = Mathf.Clamp01((currentLife - 1) / 8f);
        if (type == LevelCellType.Triangle)
        {
            return Color.Lerp(
                new Color(0.44f, 0.78f, 0.97f, 1f),
                new Color(0.08f, 0.45f, 0.89f, 1f),
                t);
        }

        return Color.Lerp(
            new Color(0.95f, 0.78f, 0.32f, 1f),
            new Color(0.88f, 0.29f, 0.23f, 1f),
            t);
    }

    public static string GetPreviewLifeLabel(LevelCellType type, int currentLife)
    {
        return type == LevelCellType.Empty || currentLife <= 0 ? string.Empty : currentLife.ToString();
    }

    public static string GetPreviewTypeMarker(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty || currentLife <= 0)
        {
            return string.Empty;
        }

        return type == LevelCellType.Triangle ? "T" : string.Empty;
    }

    public static Color GetPreviewTextColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty || currentLife <= 0)
        {
            return new Color(0f, 0f, 0f, 0f);
        }

        var backgroundColor = GetPreviewColor(type, currentLife);
        var luminance = (backgroundColor.r * 0.299f) + (backgroundColor.g * 0.587f) + (backgroundColor.b * 0.114f);
        return luminance < 0.58f ? Color.white : new Color(0.12f, 0.12f, 0.12f, 1f);
    }

    public static Color GetPreviewOutlineColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty || currentLife <= 0)
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
