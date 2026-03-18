using UnityEngine;

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
    private int SortingOrder = 520;

    private RectTransform coordinateSpaceRectTransform;
    private Material runtimeMaterial;
    private ImpactMarker primaryMarker;
    private ImpactMarker reflectionMarker;

    private void Awake()
    {
        EnsureVisuals();
        Hide();
    }

    private void Update()
    {
        UpdateMarker(primaryMarker, 0f);
        UpdateMarker(reflectionMarker, 0.7f);
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
    }
#endif

    public void Show(in AimPreviewPath previewPath)
    {
        EnsureVisuals();
        ConfigureMarker(primaryMarker, previewPath.PrimarySegment.HitBoundary, previewPath.PrimarySegment.BoundaryHitPoint, PrimaryRadius, PrimaryWidth, PrimaryColor);
        ConfigureMarker(
            reflectionMarker,
            previewPath.HasReflectionSegment && previewPath.ReflectionSegment.HitBoundary,
            previewPath.HasReflectionSegment ? previewPath.ReflectionSegment.BoundaryHitPoint : Vector2.zero,
            ReflectionRadius,
            ReflectionWidth,
            ReflectionColor);
    }

    public void Hide()
    {
        EnsureVisuals();
        HideMarker(primaryMarker);
        HideMarker(reflectionMarker);
    }

    private void EnsureVisuals()
    {
        if (coordinateSpaceRectTransform == null)
        {
            coordinateSpaceRectTransform = transform.parent as RectTransform;
        }

        primaryMarker ??= CreateMarker("Primary Impact");
        reflectionMarker ??= CreateMarker("Reflection Impact");
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
}
