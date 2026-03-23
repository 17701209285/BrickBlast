using UnityEngine;

[DisallowMultipleComponent]
public class AimLineView : MonoBehaviour
{
    [SerializeField]
    [Min(0.001f)]
    private float PrimaryLineWidth = 8f;

    [SerializeField]
    [Min(0.001f)]
    private float ReflectionLineWidth = 8f;

    [SerializeField]
    private Color PrimaryLineColor = new Color(1f, 1f, 1f, 0.95f);

    [SerializeField]
    private Color ReflectionLineColor = new Color(1f, 1f, 1f, 0.55f);

    [SerializeField]
    private int SortingOrder = 500;

    [SerializeField]
    private Material LineMaterial;

    private RectTransform coordinateSpaceRectTransform;
    private LineRenderer primaryLineRenderer;
    private LineRenderer reflectionLineRenderer;
    private Material runtimeMaterial;

    private void Awake()
    {
        EnsureVisuals();
        Hide();
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
        ApplyStyle();
    }
#endif

    public void Show(in AimPreviewPath previewPath)
    {
        EnsureVisuals();

        var startWorldPosition = ToWorldPosition(previewPath.PrimarySegment.StartPoint);
        var hitWorldPosition = ToWorldPosition(previewPath.PrimarySegment.EndPoint);
        ApplyStyle();

        primaryLineRenderer.enabled = true;
        primaryLineRenderer.positionCount = 2;
        primaryLineRenderer.SetPosition(0, startWorldPosition);
        primaryLineRenderer.SetPosition(1, hitWorldPosition);

        if (previewPath.HasReflectionSegment)
        {
            var reflectionEndWorldPosition = ToWorldPosition(previewPath.ReflectionSegment.EndPoint);
            reflectionLineRenderer.enabled = true;
            reflectionLineRenderer.positionCount = 2;
            reflectionLineRenderer.SetPosition(0, hitWorldPosition);
            reflectionLineRenderer.SetPosition(1, reflectionEndWorldPosition);
            return;
        }

        reflectionLineRenderer.enabled = false;
        reflectionLineRenderer.positionCount = 0;
    }

    public void Hide()
    {
        EnsureVisuals();
        primaryLineRenderer.enabled = false;
        primaryLineRenderer.positionCount = 0;
        reflectionLineRenderer.enabled = false;
        reflectionLineRenderer.positionCount = 0;
    }

    public void SetLineMaterial(Material material)
    {
        if (LineMaterial == material)
        {
            return;
        }

        LineMaterial = material;
        ApplyStyle();
    }

    private void EnsureVisuals()
    {
        if (coordinateSpaceRectTransform == null)
        {
            coordinateSpaceRectTransform = transform.parent as RectTransform;
        }

        primaryLineRenderer = EnsureLineRenderer("Primary Line", ref primaryLineRenderer);
        reflectionLineRenderer = EnsureLineRenderer("Reflection Line", ref reflectionLineRenderer);
        ApplyStyle();
    }

    private LineRenderer EnsureLineRenderer(string childName, ref LineRenderer lineRenderer)
    {
        if (lineRenderer == null)
        {
            var child = transform.Find(childName);
            if (child != null)
            {
                lineRenderer = child.GetComponent<LineRenderer>();
            }
        }

        if (lineRenderer == null)
        {
            var childObject = new GameObject(childName, typeof(LineRenderer));
            childObject.transform.SetParent(transform, false);
            childObject.transform.localPosition = Vector3.zero;
            childObject.transform.localRotation = Quaternion.identity;
            childObject.transform.localScale = Vector3.one;
            lineRenderer = childObject.GetComponent<LineRenderer>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCapVertices = 8;
        lineRenderer.numCornerVertices = 8;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        lineRenderer.sortingOrder = SortingOrder;
        lineRenderer.loop = false;
        return lineRenderer;
    }

    private void ApplyStyle()
    {
        if (primaryLineRenderer == null || reflectionLineRenderer == null)
        {
            return;
        }

        var primaryWorldWidth = GetWorldWidth(PrimaryLineWidth);
        var reflectionWorldWidth = GetWorldWidth(ReflectionLineWidth);
        primaryLineRenderer.startWidth = primaryWorldWidth;
        primaryLineRenderer.endWidth = primaryWorldWidth;
        reflectionLineRenderer.startWidth = reflectionWorldWidth;
        reflectionLineRenderer.endWidth = reflectionWorldWidth;

        primaryLineRenderer.startColor = PrimaryLineColor;
        primaryLineRenderer.endColor = PrimaryLineColor;
        reflectionLineRenderer.startColor = ReflectionLineColor;
        reflectionLineRenderer.endColor = ReflectionLineColor;

        primaryLineRenderer.sortingOrder = SortingOrder;
        reflectionLineRenderer.sortingOrder = SortingOrder;
        var sharedMaterial = GetSharedMaterial();
        primaryLineRenderer.sharedMaterial = sharedMaterial;
        reflectionLineRenderer.sharedMaterial = sharedMaterial;
    }

    private Material GetSharedMaterial()
    {
        return LineMaterial != null ? LineMaterial : GetOrCreateRuntimeMaterial();
    }

    private Material GetOrCreateRuntimeMaterial()
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

    private float GetWorldWidth(float localPixelWidth)
    {
        if (coordinateSpaceRectTransform == null)
        {
            return Mathf.Max(0.001f, localPixelWidth * 0.01f);
        }

        var worldOffset = coordinateSpaceRectTransform.TransformVector(new Vector3(localPixelWidth, 0f, 0f));
        return Mathf.Max(0.001f, worldOffset.magnitude);
    }
}
