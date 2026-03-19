using UnityEngine;

public static class LevelCellTypeUtility
{
    public static bool IsSpecial(LevelCellType type)
    {
        switch (type)
        {
            case LevelCellType.HorizontalBlast:
            case LevelCellType.VerticalBlast:
            case LevelCellType.SplitThreeWay:
                return true;
            default:
                return false;
        }
    }

    public static bool UsesLife(LevelCellType type)
    {
        return type != LevelCellType.Empty && !IsSpecial(type);
    }

    public static LevelCellType NormalizeType(LevelCellType type, int life)
    {
        if (type == LevelCellType.Empty)
        {
            return LevelCellType.Empty;
        }

        return UsesLife(type) && life <= 0
            ? LevelCellType.Empty
            : type;
    }

    public static int NormalizeLife(LevelCellType type, int life)
    {
        var normalizedType = NormalizeType(type, life);
        if (normalizedType == LevelCellType.Empty || IsSpecial(normalizedType))
        {
            return 0;
        }

        return Mathf.Max(1, life);
    }

    public static bool HasSerializedContent(LevelCellType type, int life)
    {
        return NormalizeType(type, life) != LevelCellType.Empty;
    }
}
