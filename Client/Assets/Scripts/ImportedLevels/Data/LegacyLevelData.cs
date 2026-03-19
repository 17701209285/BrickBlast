using System;
using UnityEngine;

namespace ImportedLevels
{
    [Serializable]
    public enum LegacyBrickType
    {
        None = 0,
        Normal = 1,
        Tool = 2,
        Explosion = 3,
        Ghost = 4,
        Rotation = 5,
        Large = 6,
        Unbreakable = 7,
        Key = 8,
        ClearAll = 9,
        Invisible = 10,
        Cloud = 11,
        Leaf = 12,
        AddBall = 13,
        CatchBall = 14,
        AddCoin = 15,
        Reproduction = 16
    }

    [Serializable]
    public enum LegacyBrickShapeType
    {
        None = 0,
        Square = 1,
        RightTriangleLeftDown = 2,
        RightTriangleLeftUp = 3,
        RightTriangleRightUp = 4,
        RightTriangleRightDown = 5,
        EquilateralTriangle = 6,
        Circle = 7
    }

    [Serializable]
    public enum LegacyAttributeType
    {
        None = 0,
        Chain = 1,
        Frozen = 2,
        Lock = 3,
        Snow = 4,
        Ice = 5,
        IceCover = 6
    }

    [Serializable]
    public enum LegacyBrickToolType
    {
        None = 0,
        AddBall = 1,
        Charge = 2,
        Laser = 3,
        AddDamage = 4,
        Turning = 5,
        Hole = 6,
        Gather = 7
    }

    [Serializable]
    public enum LegacyRotateDirection
    {
        None = 0,
        ClockwiseRotation = 1,
        CounterclockwiseRotation = 2
    }

    [Serializable]
    public struct LegacyRectRange
    {
        public int x;
        public int y;
        public int z;
        public int w;

        public LegacyRectRange(int xValue, int yValue, int zValue, int wValue)
        {
            x = xValue;
            y = yValue;
            z = zValue;
            w = wValue;
        }
    }

    [Serializable]
    public sealed class LegacyLevelMetadata
    {
        public bool ImportedFromLegacy164;
        public int SourceLevel;
        public int SourceMapId;
        public string SourceAuthorName = string.Empty;
        public int SourceDifficulty;
        public Vector2Int SourceBrickRandomRange;
        public Vector2Int SourceAddCoinBrickRange;
        public bool SourceDoNotPush;
        public string SourceScoreHex = string.Empty;
        public float StartPosition;
        public Vector2Int ColorRange;
        public Vector2Int ColorTopLimit;
        public int ChargeMax;
        public int BallCount;
        public LegacyRectRange[] Barriers = Array.Empty<LegacyRectRange>();
        public LegacyRectRange[] Rivers = Array.Empty<LegacyRectRange>();
    }

    public static class LegacyLevelTypeMapper
    {
        public static LevelCellType MapPreviewType(
            LegacyBrickType brickType,
            LegacyBrickShapeType shapeType,
            LegacyBrickToolType toolType,
            string extraAttributes)
        {
            if (brickType == LegacyBrickType.None)
            {
                return LevelCellType.Empty;
            }

            if (brickType == LegacyBrickType.Tool)
            {
                return MapToolPreviewType(toolType, extraAttributes);
            }

            if (brickType == LegacyBrickType.AddBall)
            {
                return LevelCellType.SplitThreeWay;
            }

            return IsTriangleShape(shapeType) ? LevelCellType.Triangle : LevelCellType.Square;
        }

        public static int MapPreviewLife(LevelCellType previewType, Vector2Int breakTime)
        {
            if (!LevelCellTypeUtility.UsesLife(previewType))
            {
                return 0;
            }

            return Mathf.Max(1, breakTime.x);
        }

        public static bool IsTriangleShape(LegacyBrickShapeType shapeType)
        {
            switch (shapeType)
            {
                case LegacyBrickShapeType.RightTriangleLeftDown:
                case LegacyBrickShapeType.RightTriangleLeftUp:
                case LegacyBrickShapeType.RightTriangleRightUp:
                case LegacyBrickShapeType.RightTriangleRightDown:
                case LegacyBrickShapeType.EquilateralTriangle:
                    return true;
                default:
                    return false;
            }
        }

        public static int ExtractPrimaryExtraInt(string extraAttributes)
        {
            if (string.IsNullOrWhiteSpace(extraAttributes))
            {
                return 0;
            }

            var separators = new[] { ';', '|', ',' };
            var firstToken = extraAttributes.Split(separators, StringSplitOptions.RemoveEmptyEntries)[0];
            return int.TryParse(firstToken, out var value) ? value : 0;
        }

        private static LevelCellType MapToolPreviewType(LegacyBrickToolType toolType, string extraAttributes)
        {
            switch (toolType)
            {
                case LegacyBrickToolType.Laser:
                {
                    var laserKind = ExtractPrimaryExtraInt(extraAttributes);
                    if (laserKind == 2)
                    {
                        return LevelCellType.VerticalBlast;
                    }

                    if (laserKind == 1 || laserKind == 3)
                    {
                        return LevelCellType.HorizontalBlast;
                    }

                    return LevelCellType.HorizontalBlast;
                }
                case LegacyBrickToolType.AddBall:
                    return LevelCellType.SplitThreeWay;
                default:
                    return LevelCellType.Square;
            }
        }
    }
}
