using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AimImpactEffectView : MonoBehaviour
{
    [SerializeField]
    [Min(0.001f)]
    private float PrimaryRadius = 20f;

    [SerializeField]
    [Min(0.001f)]
    private float ReflectionRadius = 16f;

    [SerializeField]
    [Min(0.001f)]
    private float PrimaryWidth = 3f;

    [SerializeField]
    [Min(0.001f)]
    private float ReflectionWidth = 2f;

    [SerializeField]
    private Color PrimaryColor = new Color(1f, 1f, 1f, 0.95f);

    [SerializeField]
    private Color ReflectionColor = new Color(1f, 1f, 1f, 0.6f);

    [SerializeField]
    [Range(8, 64)]
    private int CircleSegments = 28;

    [SerializeField]
    [Range(0f, 0.5f)]
    private float PulseScale = 0.14f;

    [SerializeField]
    [Min(0f)]
    private float PulseSpeed = 5f;

    [SerializeField]
    [Range(0f, 1f)]
    private float BlockImpactAlpha = 0.96f;

    [SerializeField]
    [Range(0f, 0.25f)]
    private float BlockImpactPulseScale = 0.05f;

    [SerializeField]
    [Min(0f)]
    private float BlockImpactPulseSpeed = 4.2f;

    [SerializeField]
    private int SortingOrder = 520;

    private RectTransform coordinateSpaceRectTransform;
    private RectTransform selfRectTransform;
    private RectTransform previewBallTemplateRectTransform;
    private Image previewBallTemplateImage;
    private Material runtimeMaterial;
    private ImpactMarker primaryMarker;
    private ImpactMarker reflectionMarker;
    private ImpactBallMarker blockImpactBall;

    private void Awake()
    {
        EnsureVisuals();
        Hide();
    }

    private void Update()
    {
        UpdateMarker(primaryMarker, 0f);
        UpdateMarker(reflectionMarker, 0.7f);
        UpdateImpactBall(blockImpactBall, 0.35f);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(runtimeMaterial);
            return;
        }

        DestroyImmediate(runtimeMaterial);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureVisuals();
        ApplyStyle(primaryMarker, PrimaryWidth, PrimaryColor);
        ApplyStyle(reflectionMarker, ReflectionWidth, ReflectionColor);
        ApplyImpactBallStyle(blockImpactBall);
    }
#endif

    public void Show(in AimPreviewPath previewPath, in AimPreviewImpactData impactData)
    {
        EnsureVisuals();

        var hasPrimaryBlockImpact = impactData.HasBlockImpact && !impactData.IsReflectionImpact;
        var showPrimaryBoundary = previewPath.PrimarySegment.HitBoundary && !hasPrimaryBlockImpact;
        var showReflectionBoundary = previewPath.HasReflectionSegment && previewPath.ReflectionSegment.HitBoundary && !impactData.HasBlockImpact;

        ConfigureMarker(
            primaryMarker,
            showPrimaryBoundary,
            previewPath.PrimarySegment.BoundaryHitPoint,
            PrimaryRadius,
            PrimaryWidth,
            PrimaryColor);

        ConfigureMarker(
            reflectionMarker,
            showReflectionBoundary,
            previewPath.HasReflectionSegment ? previewPath.ReflectionSegment.BoundaryHitPoint : Vector2.zero,
            ReflectionRadius,
            ReflectionWidth,
            ReflectionColor);

        ConfigureImpactBall(blockImpactBall, impactData.HasBlockImpact, impactData.BlockImpactCenterPoint);
    }

    public void Hide()
    {
        EnsureVisuals();
        HideMarker(primaryMarker);
        HideMarker(reflectionMarker);
        HideImpactBall(blockImpactBall);
    }

    private void EnsureVisuals()
    {
        if (coordinateSpaceRectTransform == null)
        {
            coordinateSpaceRectTransform = transform.parent as RectTransform;
        }

        if (selfRectTransform == null)
        {
            selfRectTransform = transform as RectTransform;
        }

        StretchToCoordinateSpace();
        CachePreviewBallTemplate();

        primaryMarker ??= CreateMarker("Primary Impact");
        reflectionMarker ??= CreateMarker("Reflection Impact");
        blockImpactBall ??= CreateImpactBall("Block Impact Ball");

        ApplyImpactBallStyle(blockImpactBall);
    }

    private void StretchToCoordinateSpace()
    {
        if (selfRectTransform == null)
        {
            return;
        }

        selfRectTransform.anchorMin = Vector2.zero;
        selfRectTransform.anchorMax = Vector2.one;
        selfRectTransform.offsetMin = Vector2.zero;
        selfRectTransform.offsetMax = Vector2.zero;
        selfRectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void CachePreviewBallTemplate()
    {
        if (previewBallTemplateRectTransform != null && previewBallTemplateImage != null)
        {
            return;
        }

        if (coordinateSpaceRectTransform == null)
        {
            return;
        }

        previewBallTemplateRectTransform = coordinateSpaceRectTransform.Find("Ball") as RectTransform;
        if (previewBallTemplateRectTransform != null)
        {
            previewBallTemplateImage = previewBallTemplateRectTransform.GetComponent<Image>();
        }
    }

    private ImpactMarker CreateMarker(string name)
    {
        var markerObject = new GameObject(name, typeof(LineRenderer));
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localPosition = Vector3.zero;
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;

        var lineRenderer = markerObject.GetComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 6;
        lineRenderer.numCornerVertices = 6;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        lineRenderer.sharedMaterial = GetOrCreateMaterial();
        lineRenderer.sortingOrder = SortingOrder;

        return new ImpactMarker
        {
            LineRenderer = lineRenderer
        };
    }

    private ImpactBallMarker CreateImpactBall(string name)
    {
        var markerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        markerObject.transform.SetParent(transform, false);
        markerObject.transform.localPosition = Vector3.zero;
        markerObject.transform.localRotation = Quaternion.identity;
        markerObject.transform.localScale = Vector3.one;
        markerObject.transform.SetAsLastSibling();

        var rectTransform = markerObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        var image = markerObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.maskable = false;

        markerObject.SetActive(false);

        return new ImpactBallMarker
        {
            RectTransform = rectTransform,
            Image = image
        };
    }

    private void ConfigureMarker(ImpactMarker marker, bool active, Vector2 hitLocalPoint, float radius, float width, Color color)
    {
        marker.Active = active;
        if (!active)
        {
            HideMarker(marker);
            return;
        }

        marker.WorldCenter = ToWorldPosition(hitLocalPoint);
        marker.BaseWorldRadius = GetWorldScalar(radius);
        ApplyStyle(marker, width, color);
        marker.LineRenderer.enabled = true;
        UpdateCircle(marker, 1f, color);
    }

    private void ConfigureImpactBall(ImpactBallMarker marker, bool active, Vector2 localCenterPoint)
    {
        if (marker?.RectTransform == null || marker.Image == null)
        {
            return;
        }

        marker.Active = active;
        if (!active)
        {
            HideImpactBall(marker);
            return;
        }

        ApplyImpactBallStyle(marker);
        marker.BaseAnchoredPosition = localCenterPoint;
        marker.RectTransform.anchoredPosition = localCenterPoint;
        marker.RectTransform.gameObject.SetActive(true);
        marker.RectTransform.SetAsLastSibling();
        marker.Image.enabled = true;
        UpdateImpactBall(marker, 0.35f);
    }

    private void HideMarker(ImpactMarker marker)
    {
        if (marker?.LineRenderer == null)
        {
            return;
        }

        marker.Active = false;
        marker.LineRenderer.enabled = false;
        marker.LineRenderer.positionCount = 0;
    }

    private void HideImpactBall(ImpactBallMarker marker)
    {
        if (marker?.RectTransform == null || marker.Image == null)
        {
            return;
        }

        marker.Active = false;
        marker.Image.enabled = false;
        marker.RectTransform.gameObject.SetActive(false);
    }

    private void UpdateMarker(ImpactMarker marker, float phaseOffset)
    {
        if (marker?.LineRenderer == null || !marker.Active)
        {
            return;
        }

        var pulse = 1f + (Mathf.Sin((Time.unscaledTime * PulseSpeed) + phaseOffset) * PulseScale);
        var alphaPulse = Mathf.Lerp(0.75f, 1f, (Mathf.Sin((Time.unscaledTime * PulseSpeed) + phaseOffset) + 1f) * 0.5f);
        var color = marker.BaseColor;
        color.a *= alphaPulse;
        UpdateCircle(marker, pulse, color);
    }

    private void UpdateImpactBall(ImpactBallMarker marker, float phaseOffset)
    {
        if (marker?.RectTransform == null || marker.Image == null || !marker.Active)
        {
            return;
        }

        var pulse = 1f + (Mathf.Sin((Time.unscaledTime * BlockImpactPulseSpeed) + phaseOffset) * BlockImpactPulseScale);
        var alphaPulse = Mathf.Lerp(0.88f, 1f, (Mathf.Sin((Time.unscaledTime * BlockImpactPulseSpeed) + phaseOffset) + 1f) * 0.5f);
        var color = marker.BaseColor;
        color.a *= alphaPulse;

        marker.RectTransform.anchoredPosition = marker.BaseAnchoredPosition;
        marker.RectTransform.sizeDelta = marker.BaseSize * pulse;
        marker.Image.color = color;
    }

    private void UpdateCircle(ImpactMarker marker, float scale, Color color)
    {
        var lineRenderer = marker.LineRenderer;
        var radius = Mathf.Max(0.0001f, marker.BaseWorldRadius * scale);
        lineRenderer.positionCount = CircleSegments + 1;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;

        for (int i = 0; i <= CircleSegments; i++)
        {
            var t = (float)i / CircleSegments;
            var angle = t * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            lineRenderer.SetPosition(i, marker.WorldCenter + offset);
        }
    }

    private void ApplyStyle(ImpactMarker marker, float width, Color color)
    {
        if (marker?.LineRenderer == null)
        {
            return;
        }

        var worldWidth = GetWorldScalar(width);
        marker.LineRenderer.startWidth = worldWidth;
        marker.LineRenderer.endWidth = worldWidth;
        marker.LineRenderer.sortingOrder = SortingOrder;
        marker.BaseColor = color;
    }

    private void ApplyImpactBallStyle(ImpactBallMarker marker)
    {
        if (marker?.RectTransform == null || marker.Image == null)
        {
            return;
        }

        CachePreviewBallTemplate();

        var baseSize = previewBallTemplateRectTransform != null
            ? previewBallTemplateRectTransform.rect.size
            : new Vector2(50f, 50f);

        if (baseSize.x <= 0f || baseSize.y <= 0f)
        {
            baseSize = previewBallTemplateRectTransform != null
                ? previewBallTemplateRectTransform.sizeDelta
                : new Vector2(50f, 50f);
        }

        marker.BaseSize = baseSize;
        marker.RectTransform.sizeDelta = baseSize;

        if (previewBallTemplateImage != null)
        {
            marker.Image.sprite = previewBallTemplateImage.sprite;
            marker.Image.overrideSprite = previewBallTemplateImage.overrideSprite;
            marker.Image.type = previewBallTemplateImage.type;
            marker.Image.preserveAspect = previewBallTemplateImage.preserveAspect;
            marker.Image.material = previewBallTemplateImage.material;
            marker.Image.pixelsPerUnitMultiplier = previewBallTemplateImage.pixelsPerUnitMultiplier;

            var color = previewBallTemplateImage.color;
            color.a = Mathf.Clamp01(color.a * Mathf.Clamp01(BlockImpactAlpha));
            marker.BaseColor = color;
            marker.Image.color = color;
            return;
        }

        var fallbackColor = PrimaryColor;
        fallbackColor.a = Mathf.Clamp01(BlockImpactAlpha);
        marker.BaseColor = fallbackColor;
        marker.Image.color = fallbackColor;
    }

    private Material GetOrCreateMaterial()
    {
        if (runtimeMaterial != null)
        {
            return runtimeMaterial;
        }

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        runtimeMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        runtimeMaterial.renderQueue = 4000;
        return runtimeMaterial;
    }

    private Vector3 ToWorldPosition(Vector2 localPosition)
    {
        if (coordinateSpaceRectTransform == null)
        {
            return transform.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
        }

        return coordinateSpaceRectTransform.TransformPoint(new Vector3(localPosition.x, localPosition.y, 0f));
    }

    private float GetWorldScalar(float localPixelSize)
    {
        if (coordinateSpaceRectTransform == null)
        {
            return Mathf.Max(0.001f, localPixelSize * 0.01f);
        }

        var worldOffset = coordinateSpaceRectTransform.TransformVector(new Vector3(localPixelSize, 0f, 0f));
        return Mathf.Max(0.001f, worldOffset.magnitude);
    }

    private sealed class ImpactMarker
    {
        public LineRenderer LineRenderer;
        public bool Active;
        public Vector3 WorldCenter;
        public float BaseWorldRadius;
        public Color BaseColor;
    }

    private sealed class ImpactBallMarker
    {
        public RectTransform RectTransform;
        public Image Image;
        public bool Active;
        public Vector2 BaseAnchoredPosition;
        public Vector2 BaseSize;
        public Color BaseColor;
    }
}
