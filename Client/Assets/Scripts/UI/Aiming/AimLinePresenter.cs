using System;
using UnityEngine;

[DisallowMultipleComponent]
public class AimLinePresenter : MonoBehaviour
{
    public event Action<Vector2, Vector2> AimReleased;

    [SerializeField]
    private RectTransform AimArea;

    [SerializeField]
    private RectTransform AimOrigin;

    [SerializeField]
    private PointerDragInputSource InputSource;

    [SerializeField]
    private AimLineView AimLineView;

    [SerializeField]
    private AimImpactEffectView AimImpactEffectView;

    [SerializeField]
    private UIChessBoard ChessBoard;

    [SerializeField]
    private Material AimLineMaterial;

    [SerializeField]
    private Vector2 LaunchOriginOffset = new Vector2(0f, 180f);

    [SerializeField]
    [Min(0f)]
    private float MinVerticalDragDistance = 24f;

    [SerializeField]
    [Range(0f, 89f)]
    private float MinAimAngle = 15f;

    [SerializeField]
    [Range(91f, 180f)]
    private float MaxAimAngle = 165f;

    [SerializeField]
    [Min(1f)]
    private float MaxLineLength = 1600f;

    [SerializeField]
    [Min(0f)]
    private float BoundaryPadding = 6f;

    public bool HasValidAim { get; private set; }
    public Vector2 CurrentAimDirection { get; private set; } = Vector2.up;
    public AimBoundaryType PrimaryBoundaryType { get; private set; }
    public Vector2 PrimaryBoundaryHitPoint { get; private set; }
    public AimBoundaryType ReflectionBoundaryType { get; private set; }
    public Vector2 ReflectionBoundaryHitPoint { get; private set; }
    public RectTransform AimSpace => AimArea;
    public RectTransform AimOriginTransform => AimOrigin;

    private BallVolleyController ballVolleyController;
    private AimPreviewImpactData activeImpactData;
    private bool isSubscribed;
    private bool aimInputEnabled = true;

    private void Reset()
    {
        AimArea = transform as RectTransform;
    }

    private void Awake()
    {
        EnsureDependencies();
        HideAimLine();
    }

    private void OnEnable()
    {
        EnsureDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        HideAimLine();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MinAimAngle = Mathf.Clamp(MinAimAngle, 0f, 89f);
        MaxAimAngle = Mathf.Clamp(MaxAimAngle, 91f, 180f);
        if (AimArea == null)
        {
            AimArea = transform as RectTransform;
        }

        if (AimLineView != null)
        {
            AimLineView.SetLineMaterial(AimLineMaterial);
        }
    }
#endif

    private void HandleDragStarted(Vector2 screenPosition)
    {
        if (!aimInputEnabled)
        {
            return;
        }

        UpdateAimLine(screenPosition);
    }

    private void HandleDragMoved(Vector2 screenPosition)
    {
        if (!aimInputEnabled)
        {
            return;
        }

        UpdateAimLine(screenPosition);
    }

    private void HandleDragEnded(Vector2 _)
    {
        if (!aimInputEnabled)
        {
            HideAimLine();
            return;
        }

        if (HasValidAim)
        {
            AimReleased?.Invoke(GetOriginLocalPosition(), CurrentAimDirection);
        }

        HideAimLine();
    }

    public void SetAimInputEnabled(bool enabled)
    {
        if (aimInputEnabled == enabled)
        {
            return;
        }

        aimInputEnabled = enabled;
        if (!aimInputEnabled)
        {
            HideAimLine();
        }
    }

    public Rect GetShotBounds()
    {
        return GetPreviewBounds();
    }

    private void UpdateAimLine(Vector2 screenPosition)
    {
        EnsureDependencies();
        if (AimArea == null || AimLineView == null)
        {
            return;
        }

        if (!TryGetAimDirection(screenPosition, out var originLocalPosition, out var aimDirection))
        {
            HideAimLine();
            return;
        }

        var paddedBounds = GetPreviewBounds();
        var previewLength = Mathf.Max(MaxLineLength, paddedBounds.size.magnitude * 2f);
        if (!AimPreviewPathCalculator.TryBuildOneBouncePath(
                paddedBounds,
                originLocalPosition,
                aimDirection,
                previewLength,
                out var previewPath))
        {
            HideAimLine();
            return;
        }

        CurrentAimDirection = aimDirection.normalized;
        HasValidAim = true;
        PrimaryBoundaryType = previewPath.PrimarySegment.BoundaryType;
        PrimaryBoundaryHitPoint = previewPath.PrimarySegment.BoundaryHitPoint;
        ReflectionBoundaryType = previewPath.HasReflectionSegment ? previewPath.ReflectionSegment.BoundaryType : AimBoundaryType.None;
        ReflectionBoundaryHitPoint = previewPath.HasReflectionSegment ? previewPath.ReflectionSegment.BoundaryHitPoint : previewPath.PrimarySegment.EndPoint;
        var impactData = AimPreviewBlockScanner.BuildImpactData(
            ChessBoard,
            AimArea,
            previewPath,
            GetPreviewBallRadius(),
            GetPreviewCollisionTolerance());
        AimLineView.Show(previewPath);
        if (AimImpactEffectView != null)
        {
            AimImpactEffectView.Show(previewPath, impactData);
        }
        AimPreviewBlockScanner.ApplyPreview(ChessBoard, activeImpactData, impactData);
        activeImpactData = impactData;
    }

    private bool TryGetAimDirection(Vector2 screenPosition, out Vector2 originLocalPosition, out Vector2 aimDirection)
    {
        originLocalPosition = GetOriginLocalPosition();
        aimDirection = Vector2.up;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                AimArea,
                screenPosition,
                GetEventCamera(),
                out var pointerLocalPosition))
        {
            return false;
        }

        var direction = pointerLocalPosition - originLocalPosition;
        if (direction.y <= MinVerticalDragDistance)
        {
            return false;
        }

        var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        GetAimAngleBounds(originLocalPosition, out var minAimAngle, out var maxAimAngle);
        angle = Mathf.Clamp(angle, minAimAngle, maxAimAngle);

        aimDirection = DegreeToDirection(angle);
        return true;
    }

    private Rect GetPreviewBounds()
    {
        if (AimArea == null)
        {
            return new Rect(-540f, -960f, 1080f, 1920f);
        }

        if (ChessBoard != null && ChessBoard.TryGetPlayAreaRect(AimArea, BoundaryPadding, BoundaryPadding, out var playAreaRect))
        {
            return playAreaRect;
        }

        var rect = AimArea.rect;
        var xMin = rect.xMin + BoundaryPadding;
        var yMin = rect.yMin + BoundaryPadding;
        var xMax = rect.xMax - BoundaryPadding;
        var yMax = rect.yMax - BoundaryPadding;
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private Vector2 GetOriginLocalPosition()
    {
        if (AimArea == null)
        {
            return LaunchOriginOffset;
        }

        if (AimOrigin != null)
        {
            var worldCenter = AimOrigin.TransformPoint(AimOrigin.rect.center);
            return AimArea.InverseTransformPoint(worldCenter);
        }

        var rect = AimArea.rect;
        return new Vector2(rect.center.x + LaunchOriginOffset.x, rect.yMin + LaunchOriginOffset.y);
    }

    private void GetAimAngleBounds(Vector2 originLocalPosition, out float minAimAngle, out float maxAimAngle)
    {
        var previewBounds = GetPreviewBounds();
        if (previewBounds.width <= 0f)
        {
            minAimAngle = MinAimAngle;
            maxAimAngle = MaxAimAngle;
            return;
        }

        var boardHalfWidth = Mathf.Max(0.001f, previewBounds.width * 0.5f);
        var cellWidth = previewBounds.width / Mathf.Max(1f, LevelConfigScritable.FixedBoardWidth);
        var firstColumnCenterInset = Mathf.Max(0.001f, cellWidth * 0.5f);
        var shallowReferenceAngle = Mathf.Min(
            Mathf.Clamp(MinAimAngle, 0.5f, 89f),
            Mathf.Clamp(180f - MaxAimAngle, 0.5f, 89f));
        var referenceRise = Mathf.Tan(shallowReferenceAngle * Mathf.Deg2Rad) * (boardHalfWidth + firstColumnCenterInset);
        var leftTravelDistance = Mathf.Max(0.001f, (originLocalPosition.x - previewBounds.xMin) + firstColumnCenterInset);
        var rightTravelDistance = Mathf.Max(0.001f, (previewBounds.xMax - originLocalPosition.x) + firstColumnCenterInset);

        minAimAngle = Mathf.Clamp(
            Mathf.Atan2(referenceRise, rightTravelDistance) * Mathf.Rad2Deg,
            0.5f,
            89f);
        maxAimAngle = Mathf.Clamp(
            180f - (Mathf.Atan2(referenceRise, leftTravelDistance) * Mathf.Rad2Deg),
            91f,
            179.5f);

        if (minAimAngle >= maxAimAngle)
        {
            var centerAngle = (minAimAngle + maxAimAngle) * 0.5f;
            minAimAngle = Mathf.Clamp(centerAngle - 0.5f, 0.5f, 89f);
            maxAimAngle = Mathf.Clamp(centerAngle + 0.5f, 91f, 179.5f);
        }
    }

    private float GetPreviewBallRadius()
    {
        if (ballVolleyController != null)
        {
            return Mathf.Max(1f, ballVolleyController.PreviewCollisionRadius);
        }

        if (AimOrigin == null)
        {
            return 25f;
        }

        return Mathf.Min(AimOrigin.rect.width, AimOrigin.rect.height) * 0.5f;
    }

    private float GetPreviewCollisionTolerance()
    {
        return ballVolleyController != null
            ? ballVolleyController.PreviewCollisionTolerance
            : BallShootingConstants.DefaultCollisionSkin;
    }

    private Camera GetEventCamera()
    {
        var canvas = AimArea == null ? null : AimArea.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private void HideAimLine()
    {
        HasValidAim = false;
        PrimaryBoundaryType = AimBoundaryType.None;
        PrimaryBoundaryHitPoint = Vector2.zero;
        ReflectionBoundaryType = AimBoundaryType.None;
        ReflectionBoundaryHitPoint = Vector2.zero;
        if (AimLineView != null)
        {
            AimLineView.Hide();
        }

        if (AimImpactEffectView != null)
        {
            AimImpactEffectView.Hide();
        }

        AimPreviewBlockScanner.ClearPreview(ChessBoard, activeImpactData);
        activeImpactData = default;
    }

    private void EnsureDependencies()
    {
        if (AimArea == null)
        {
            AimArea = transform as RectTransform;
        }

        if (InputSource == null)
        {
            InputSource = GetComponent<PointerDragInputSource>();
        }

        if (InputSource == null)
        {
            InputSource = gameObject.AddComponent<PointerDragInputSource>();
        }

        if (ChessBoard == null)
        {
            ChessBoard = GetComponent<UIChessBoard>();
        }

        if (ballVolleyController == null)
        {
            ballVolleyController = GetComponent<BallVolleyController>();
        }

        if (AimLineView == null)
        {
            AimLineView = GetComponentInChildren<AimLineView>(true);
        }

        if (AimLineView == null)
        {
            AimLineView = CreateAimLineView();
        }

        AimLineView?.SetLineMaterial(AimLineMaterial);

        if (AimImpactEffectView == null)
        {
            AimImpactEffectView = GetComponentInChildren<AimImpactEffectView>(true);
        }

        if (AimImpactEffectView == null)
        {
            AimImpactEffectView = CreateAimImpactEffectView();
        }
    }

    private AimLineView CreateAimLineView()
    {
        var lineObject = new GameObject("Aim Line", typeof(RectTransform), typeof(AimLineView));
        var lineRectTransform = lineObject.GetComponent<RectTransform>();
        lineRectTransform.SetParent(AimArea != null ? AimArea : transform, false);
        lineRectTransform.SetAsLastSibling();
        return lineObject.GetComponent<AimLineView>();
    }

    private AimImpactEffectView CreateAimImpactEffectView()
    {
        var effectObject = new GameObject("Aim Impact Effects", typeof(RectTransform), typeof(AimImpactEffectView));
        var effectRectTransform = effectObject.GetComponent<RectTransform>();
        effectRectTransform.SetParent(AimArea != null ? AimArea : transform, false);
        effectRectTransform.SetAsLastSibling();
        return effectObject.GetComponent<AimImpactEffectView>();
    }

    private void Subscribe()
    {
        if (isSubscribed || InputSource == null)
        {
            return;
        }

        InputSource.DragStarted += HandleDragStarted;
        InputSource.DragMoved += HandleDragMoved;
        InputSource.DragEnded += HandleDragEnded;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || InputSource == null)
        {
            return;
        }

        InputSource.DragStarted -= HandleDragStarted;
        InputSource.DragMoved -= HandleDragMoved;
        InputSource.DragEnded -= HandleDragEnded;
        isSubscribed = false;
    }

    private static Vector2 DegreeToDirection(float degree)
    {
        var radian = degree * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radian), Mathf.Sin(radian));
    }
}
