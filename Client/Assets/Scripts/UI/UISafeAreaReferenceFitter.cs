using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class UISafeAreaReferenceFitter : MonoBehaviour
{
    [SerializeField]
    private RectTransform Target;

    [SerializeField]
    private Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    [SerializeField]
    private bool ConstrainToSafeArea = true;

    private RectTransform parentRectTransform;
    private Vector2 lastParentSize;
    private Rect lastSafeArea;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private void Reset()
    {
        Target = transform as RectTransform;
    }

    private void Awake()
    {
        ApplyLayout();
    }

    private void OnEnable()
    {
        ApplyLayout();
    }

    private void Start()
    {
        ApplyLayout();
    }

    private void Update()
    {
        if (NeedsRefresh())
        {
            ApplyLayout();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        ApplyLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ReferenceResolution.x = Mathf.Max(1f, ReferenceResolution.x);
        ReferenceResolution.y = Mathf.Max(1f, ReferenceResolution.y);

        if (!Application.isPlaying)
        {
            ApplyLayout();
        }
    }
#endif

    private bool NeedsRefresh()
    {
        if (Target == null)
        {
            Target = transform as RectTransform;
        }

        if (Target == null)
        {
            return false;
        }

        parentRectTransform = Target.parent as RectTransform;
        if (parentRectTransform == null)
        {
            return false;
        }

        var currentParentSize = parentRectTransform.rect.size;
        var currentSafeArea = GetCurrentSafeArea();
        if (lastScreenWidth != Screen.width || lastScreenHeight != Screen.height)
        {
            return true;
        }

        if (currentParentSize != lastParentSize)
        {
            return true;
        }

        return currentSafeArea != lastSafeArea;
    }

    private void ApplyLayout()
    {
        if (Target == null)
        {
            Target = transform as RectTransform;
        }

        if (Target == null)
        {
            return;
        }

        parentRectTransform = Target.parent as RectTransform;
        if (parentRectTransform == null)
        {
            return;
        }

        var parentRect = parentRectTransform.rect;
        if (parentRect.width <= 0f || parentRect.height <= 0f)
        {
            return;
        }

        var safeArea = GetCurrentSafeArea();
        var safeAreaRect = ToParentLocalRect(parentRect, safeArea);
        if (safeAreaRect.width <= 0f || safeAreaRect.height <= 0f)
        {
            return;
        }

        var referenceWidth = Mathf.Max(1f, ReferenceResolution.x);
        var referenceHeight = Mathf.Max(1f, ReferenceResolution.y);
        var scale = Mathf.Min(safeAreaRect.width / referenceWidth, safeAreaRect.height / referenceHeight);
        if (!float.IsFinite(scale) || scale <= 0f)
        {
            scale = 1f;
        }

        Target.anchorMin = new Vector2(0.5f, 0.5f);
        Target.anchorMax = new Vector2(0.5f, 0.5f);
        Target.pivot = new Vector2(0.5f, 0.5f);
        Target.sizeDelta = new Vector2(referenceWidth, referenceHeight);
        Target.localScale = new Vector3(scale, scale, 1f);

        var targetLocalPosition = Target.localPosition;
        targetLocalPosition.x = safeAreaRect.center.x;
        targetLocalPosition.y = safeAreaRect.center.y;
        Target.localPosition = targetLocalPosition;

        lastParentSize = parentRect.size;
        lastSafeArea = safeArea;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }

    private Rect GetCurrentSafeArea()
    {
        if (!ConstrainToSafeArea || Screen.width <= 0 || Screen.height <= 0)
        {
            return new Rect(0f, 0f, Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
        }

        var safeArea = Screen.safeArea;
        if (safeArea.width <= 0f || safeArea.height <= 0f)
        {
            safeArea = new Rect(0f, 0f, Screen.width, Screen.height);
        }

        return safeArea;
    }

    private static Rect ToParentLocalRect(Rect parentRect, Rect safeArea)
    {
        var screenWidth = Mathf.Max(1f, Screen.width);
        var screenHeight = Mathf.Max(1f, Screen.height);

        var xMin = Mathf.Lerp(parentRect.xMin, parentRect.xMax, safeArea.xMin / screenWidth);
        var xMax = Mathf.Lerp(parentRect.xMin, parentRect.xMax, safeArea.xMax / screenWidth);
        var yMin = Mathf.Lerp(parentRect.yMin, parentRect.yMax, safeArea.yMin / screenHeight);
        var yMax = Mathf.Lerp(parentRect.yMin, parentRect.yMax, safeArea.yMax / screenHeight);

        return Rect.MinMaxRect(
            Mathf.Min(xMin, xMax),
            Mathf.Min(yMin, yMax),
            Mathf.Max(xMin, xMax),
            Mathf.Max(yMin, yMax));
    }
}
