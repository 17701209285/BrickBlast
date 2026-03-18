using UnityEngine;

public static class AimPreviewBlockScanner
{
    public static void ApplyPreview(UIChessBoard board, RectTransform previewSpace, in AimPreviewPath previewPath)
    {
        if (board == null || previewSpace == null)
        {
            return;
        }

        for (int y = 0; y < board.BoardHeight; y++)
        {
            for (int x = 0; x < board.BoardWidth; x++)
            {
                var chessElement = board.GetChessElement(x, y);
                if (chessElement == null)
                {
                    continue;
                }

                var isHit = chessElement.HasContent && IsHitByPreview(previewSpace, chessElement, previewPath);
                chessElement.SetAimPreviewActive(isHit);
            }
        }
    }

    public static void ClearPreview(UIChessBoard board)
    {
        if (board == null)
        {
            return;
        }

        for (int y = 0; y < board.BoardHeight; y++)
        {
            for (int x = 0; x < board.BoardWidth; x++)
            {
                var chessElement = board.GetChessElement(x, y);
                if (chessElement != null)
                {
                    chessElement.SetAimPreviewActive(false);
                }
            }
        }
    }

    private static bool IsHitByPreview(RectTransform previewSpace, ChessElement chessElement, in AimPreviewPath previewPath)
    {
        var rect = chessElement.GetRectInSpace(previewSpace);
        if (Intersects(previewPath.PrimarySegment, rect))
        {
            return true;
        }

        return previewPath.HasReflectionSegment && Intersects(previewPath.ReflectionSegment, rect);
    }

    private static bool Intersects(AimPreviewSegment segment, Rect rect)
    {
        return SegmentIntersectsRect(segment.StartPoint, segment.EndPoint, rect);
    }

    private static bool SegmentIntersectsRect(Vector2 start, Vector2 end, Rect rect)
    {
        if (rect.Contains(start) || rect.Contains(end))
        {
            return true;
        }

        var direction = end - start;
        var tMin = 0f;
        var tMax = 1f;

        if (!Clip(-direction.x, start.x - rect.xMin, ref tMin, ref tMax))
        {
            return false;
        }

        if (!Clip(direction.x, rect.xMax - start.x, ref tMin, ref tMax))
        {
            return false;
        }

        if (!Clip(-direction.y, start.y - rect.yMin, ref tMin, ref tMax))
        {
            return false;
        }

        if (!Clip(direction.y, rect.yMax - start.y, ref tMin, ref tMax))
        {
            return false;
        }

        return tMax >= tMin && tMax >= 0f && tMin <= 1f;
    }

    private static bool Clip(float denominator, float numerator, ref float tMin, ref float tMax)
    {
        const float epsilon = 0.0001f;
        if (Mathf.Abs(denominator) < epsilon)
        {
            return numerator >= 0f;
        }

        var t = numerator / denominator;
        if (denominator < 0f)
        {
            if (t > tMax)
            {
                return false;
            }

            if (t > tMin)
            {
                tMin = t;
            }

            return true;
        }

        if (t < tMin)
        {
            return false;
        }

        if (t < tMax)
        {
            tMax = t;
        }

        return true;
    }
}
