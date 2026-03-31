using UnityEngine;

public static class AimPreviewBlockScanner
{
    private const float PreviewHitEpsilon = 0.01f;

    public static void ApplyPreview(UIChessBoard board, in AimPreviewImpactData previousImpactData, in AimPreviewImpactData nextImpactData)
    {
        if (board == null)
        {
            return;
        }

        DisableRemovedHighlight(previousImpactData.HighlightBlock, previousImpactData, nextImpactData);
        DisableRemovedHighlight(previousImpactData.AdditionalHighlightBlock, previousImpactData, nextImpactData);
        EnableNewHighlight(nextImpactData.HighlightBlock, previousImpactData, nextImpactData);
        EnableNewHighlight(nextImpactData.AdditionalHighlightBlock, previousImpactData, nextImpactData);
    }

    public static void ClearPreview(UIChessBoard board, in AimPreviewImpactData impactData)
    {
        if (board == null)
        {
            return;
        }

        SetPreviewActive(impactData.HighlightBlock, false);
        SetPreviewActive(impactData.AdditionalHighlightBlock, false);
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
        var previewHitTolerance = Mathf.Max(
            PreviewHitEpsilon,
            BallPhysicsUtility.CalculateSweepCollisionEpsilon(collisionTolerance));

        if (TryGetSegmentBlockHit(board, previewSpace, previewPath.PrimarySegment, ballRadius, previewHitTolerance, out var primaryHit))
        {
            return new AimPreviewImpactData(true, false, primaryHit.ImpactPoint, primaryHit.Normal, primaryHit.Block, primaryHit.AdditionalBlock);
        }

        if (previewPath.HasReflectionSegment && TryGetSegmentBlockHit(board, previewSpace, previewPath.ReflectionSegment, ballRadius, previewHitTolerance, out var reflectionHit))
        {
            return new AimPreviewImpactData(true, true, reflectionHit.ImpactPoint, reflectionHit.Normal, reflectionHit.Block, reflectionHit.AdditionalBlock);
        }

        return default;
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
            null,
            null,
            out hit);
    }

    private static void DisableRemovedHighlight(
        ChessElement chessElement,
        in AimPreviewImpactData previousImpactData,
        in AimPreviewImpactData nextImpactData)
    {
        if (chessElement == null || !previousImpactData.Highlights(chessElement) || nextImpactData.Highlights(chessElement))
        {
            return;
        }

        SetPreviewActive(chessElement, false);
    }

    private static void EnableNewHighlight(
        ChessElement chessElement,
        in AimPreviewImpactData previousImpactData,
        in AimPreviewImpactData nextImpactData)
    {
        if (chessElement == null || !nextImpactData.Highlights(chessElement))
        {
            return;
        }

        if (previousImpactData.Highlights(chessElement))
        {
            return;
        }

        SetPreviewActive(chessElement, true);
    }

    private static void SetPreviewActive(ChessElement chessElement, bool active)
    {
        if (chessElement == null)
        {
            return;
        }

        chessElement.SetAimPreviewActive(active);
    }
}
