using UnityEngine;

public static class GlobleValue 
{
    public static int ChessWidth = 11;
    public static int ChessHeight = 14;

    /// <summary>
    /// 水平消除特效
    /// </summary>
    public static string EFFECT_HORIZONTAL = "effect_horizontal";

    /// <summary>
    /// 竖直消除特效
    /// </summary>
    public static string EFFECT_VERTICAL = "effect_vertical";

    /// <summary>
    /// 十字消除特效
    /// </summary>
    public static string EFFECT_CROSS = "effect_cross";

    /// <summary>
    /// 特效播放完成
    /// </summary>
    public static string EFFECT_COMPLETE = "effect_complete";
}
