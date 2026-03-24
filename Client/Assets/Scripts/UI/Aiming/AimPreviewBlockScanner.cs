using UnityEngine;

public static class AimPreviewBlockScanner
{
    private const float PreviewHitEpsilon = 0.01f;

    public static void ApplyPreview(UIChessBoard board, in AimPreviewImpactData impactData)
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
                if (chessElement == null)
                {
                    continue;
                }

                var isHit = chessElement.HasContent && impactData.Highlights(chessElement);
                chessElement.SetAimPreviewActive(isHit);
            }
        }
    }

    public static AimPreviewImpactData BuildImpactData(
        UIChessBoard board,
        RectTransform previewSpace,
        in AimPreviewPath previewPath,
        float ballRadius,
        float collisionTolerance)
    {
        if (board == null || previewSpace == null || ballRadius <= 0f)
        {
            return default;
        }

        board.RefreshCollisionCandidates(previewSpace);
        var previewHitTolerance = Mathf.Max(PreviewHitEpsilon, collisionTolerance);

        if (TryGetSegmentBlockHit(board, previewSpace, previewPath.PrimarySegment, ballRadius, previewHitTolerance, out var primaryHit))
        {
            return new AimPreviewImpactData(true, false, primaryHit.Point, primaryHit.Block, primaryHit.AdditionalBlock);
        }

        if (previewPath.HasReflectionSegment && TryGetSegmentBlockHit(board, previewSpace, previewPath.ReflectionSegment, ballRadius, previewHitTolerance, out var reflectionHit))
        {
            return new AimPreviewImpactData(true, true, reflectionHit.Point, reflectionHit.Block, reflectionHit.AdditionalBlock);
        }

        return default;
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

    private static bool TryGetSegmentBlockHit(
        UIChessBoard board,
        RectTransform previewSpace,
        AimPreviewSegment segment,
        float ballRadius,
        float hitTolerance,
        out BallCollisionHit hit)
    {
        hit = default;
        var segmentVector = segment.EndPoint - segment.StartPoint;
        var segmentLength = segmentVector.magnitude;
        if (segmentLength <= PreviewHitEpsilon)
        {
            return false;
        }

        var direction = segmentVector / segmentLength;
        return BallPhysicsUtility.TryGetFirstBlockHit(
            board,
            previewSpace,
            segment.StartPoint,
            direction,
            ballRadius,
            segmentLength,
            hitTolerance,
            out hit);
    }
}
