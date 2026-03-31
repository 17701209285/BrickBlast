using System.Collections.Generic;
using DG.Tweening;
using ImportedLevels;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChessElement : MonoBehaviour
{
    private enum BrickLifeBand
    {
        Blue,
        Green,
        Yellow,
        Red,
        Purple
    }

    private const int GreenLifeMin = 100;
    private const int YellowLifeMin = 150;
    private const int RedLifeMin = 200;
    private const int PurpleLifeMin = 250;

    private static readonly Color HorizontalBlastLowLifeColor = new Color(0.47f, 0.89f, 0.46f, 1f);
    private static readonly Color HorizontalBlastHighLifeColor = new Color(0.16f, 0.67f, 0.31f, 1f);
    private static readonly Color VerticalBlastLowLifeColor = new Color(0.39f, 0.86f, 0.93f, 1f);
    private static readonly Color VerticalBlastHighLifeColor = new Color(0.10f, 0.56f, 0.89f, 1f);
    private static readonly Color SplitThreeWayLowLifeColor = new Color(1.00f, 0.63f, 0.37f, 1f);
    private static readonly Color SplitThreeWayHighLifeColor = new Color(0.93f, 0.32f, 0.60f, 1f);
    private static readonly Color RedirectLowLifeColor = new Color(0.99f, 0.78f, 0.42f, 1f);
    private static readonly Color RedirectHighLifeColor = new Color(0.91f, 0.46f, 0.20f, 1f);
    private static readonly Color CrossBlastLowLifeColor = new Color(0.55f, 0.92f, 0.70f, 1f);
    private static readonly Color CrossBlastHighLifeColor = new Color(0.14f, 0.68f, 0.49f, 1f);
    private static readonly Color ExtraBallsLowLifeColor = new Color(0.76f, 0.96f, 0.49f, 1f);
    private static readonly Color ExtraBallsHighLifeColor = new Color(0.35f, 0.73f, 0.17f, 1f);
    private static readonly Color BlueBrickColor = new Color(0.42f, 0.80f, 0.97f, 1f);
    private static readonly Color GreenBrickColor = new Color(0.42f, 0.82f, 0.42f, 1f);
    private static readonly Color YellowBrickColor = new Color(0.98f, 0.82f, 0.34f, 1f);
    private static readonly Color RedBrickColor = new Color(0.92f, 0.32f, 0.28f, 1f);
    private static readonly Color PurpleBrickColor = new Color(0.86f, 0.46f, 0.93f, 1f);

    [SerializeField]
    private Vector2 m_Space;
    [SerializeField]
    private Vector2 m_Offset;
    [SerializeField]
    private Color m_EmptyColor = new Color(1f, 1f, 1f, 0f);
    [SerializeField]
    [Range(0f, 1f)]
    private float m_AimPreviewHighlight = 0.35f;

    [SerializeField]
    TextMeshProUGUI m_BrickLife;

    [SerializeField]
    private Image m_BodyImage;

    [SerializeField]
    private Sprite m_PurpleSquareBodySprite;

    [SerializeField]
    private Sprite m_RedSquareBodySprite;

    [SerializeField]
    private Sprite m_YellowSquareBodySprite;

    [SerializeField]
    private Sprite m_GreenSquareBodySprite;

    [SerializeField]
    private Sprite m_BlueSquareBodySprite;

    [SerializeField]
    private Sprite m_PurpleTriangleBodySprite;

    [SerializeField]
    private Sprite m_RedTriangleBodySprite;

    [SerializeField]
    private Sprite m_YellowTriangleBodySprite;

    [SerializeField]
    private Sprite m_GreenTriangleBodySprite;

    [SerializeField]
    private Sprite m_BlueTriangleBodySprite;

    private static readonly Color DarkLifeTextColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    private const float TriangleLifeLabelOffsetFactor = 1f / 6f;

    private ChessElementData ChessData;
    private RectTransform selfRectTransform;
    private Image image;
    private Image overlayImage;
    private Vector2 cellSize;
    private Vector2 baseAnchoredPosition;
    private LevelCellType cellType = LevelCellType.Empty;
    private int life;
    private int specialValue;
    private Tween moveTween;
    private Tween aimPreviewTween;
    private bool aimPreviewActive;
    private bool specialTouchedThisVolley;
    private int specialTriggerCountThisVolley;
    private LegacyBrickShapeType legacyShapeType = LegacyBrickShapeType.None;
    private IChessHitEffectPlayer[] hitEffectPlayers;
    private readonly Vector3[] worldCornersBuffer = new Vector3[4];

    public int X => ChessData?.X ?? -1;
    public int Y => ChessData?.Y ?? -1;
    public LevelCellType Type => LevelCellTypeUtility.NormalizeType(cellType, life);
    public int Life => LevelCellTypeUtility.NormalizeLife(cellType, life);
    public int SpecialValue => LevelCellTypeUtility.ResolveSpecialValue(cellType, specialValue);
    public LegacyBrickShapeType LegacyShapeType => legacyShapeType;
    public bool HasContent => Type != LevelCellType.Empty;
    public bool CountsAsBrick => LevelCellTypeUtility.UsesLife(Type);
    public bool IsSpecialItem => LevelCellTypeUtility.IsSpecial(Type);
    public bool WasSpecialTouchedThisVolley => IsSpecialItem && specialTouchedThisVolley;
    public int SpecialTriggerCountThisVolley => specialTriggerCountThisVolley;

    private void Awake()
    {
        CacheComponents();
        CacheHitEffectPlayers();
    }

    public void InIt(ChessElementData InData)
    {
        ChessData = InData;

        RefreshDebugName();
        RefreshPosition();
        RefreshView();
    }

    public void ApplyRuntimeLayout(Vector2 inCellSize, Vector2 inSpacing, Vector2 inOffset)
    {
        CacheComponents();
        m_Space = inSpacing;
        m_Offset = inOffset;

        if (selfRectTransform != null)
        {
            selfRectTransform.sizeDelta = inCellSize;
        }

        cellSize = inCellSize;
        RefreshPosition();
    }

    public void MoveTo(int inX,int inY)
    {
        if (ChessData == null)
        {
            return;
        }

        ChessData.SetPosition(inX, inY);
        RefreshPosition();
    }

    public float GetRowStep()
    {
        CacheComponents();
        return cellSize.y + m_Space.y;
    }

    public void SetCellContent(
        LevelCellType inType,
        int inLife,
        int inSpecialValue = 0,
        LegacyBrickShapeType inLegacyShapeType = LegacyBrickShapeType.None)
    {
        cellType = LevelCellTypeUtility.NormalizeType(inType, inLife);
        life = LevelCellTypeUtility.NormalizeLife(cellType, inLife);
        specialValue = LevelCellTypeUtility.ResolveSpecialValue(cellType, inSpecialValue);
        legacyShapeType = ResolveRuntimeShapeType(cellType, inLegacyShapeType);
        specialTouchedThisVolley = false;
        specialTriggerCountThisVolley = 0;
        RefreshView();
    }

    public bool TryApplyDamage(int damage, Vector2 hitPointInBoardSpace, ChessDamageSource damageSource, out ChessHitEffectContext hitContext)
    {
        hitContext = default;
        damage = Mathf.Max(0, damage);
        var currentType = Type;
        if (currentType == LevelCellType.Empty || damage <= 0)
        {
            return false;
        }

        var preHitColor = GetContentColor(currentType, Life);
        if (LevelCellTypeUtility.IsSpecial(currentType))
        {
            specialTouchedThisVolley = true;
            RefreshDebugName();
            hitContext = new ChessHitEffectContext(this, hitPointInBoardSpace, damageSource, damage, 0, 0, preHitColor);
            PlayHitEffect(hitContext);
            return true;
        }

        // 中文备注：方块的运行时生命统一从这里扣，后面不管你换什么受击特效，都可以复用这个入口。
        var previousLife = life;
        life = Mathf.Max(0, life - damage);
        if (life <= 0)
        {
            cellType = LevelCellType.Empty;
        }

        RefreshView();

        hitContext = new ChessHitEffectContext(this, hitPointInBoardSpace, damageSource, damage, previousLife, life, preHitColor);
        PlayHitEffect(hitContext);
        return true;
    }

    public void ClearContent()
    {
        SetCellContent(LevelCellType.Empty, 0, 0, LegacyBrickShapeType.None);
    }

    public bool ClearTouchedSpecialAtTurnEnd()
    {
        if (!WasSpecialTouchedThisVolley)
        {
            return false;
        }

        ClearContent();
        return true;
    }

    public bool ConsumeSpecialItem()
    {
        if (!IsSpecialItem)
        {
            return false;
        }

        ClearContent();
        return true;
    }

    public bool TryConsumeSpecialTriggerBudget(int maxTriggerCount)
    {
        if (!IsSpecialItem || maxTriggerCount <= 0 || specialTriggerCountThisVolley >= maxTriggerCount)
        {
            return false;
        }

        specialTriggerCountThisVolley++;
        RefreshDebugName();
        return true;
    }

    public void SetAimPreviewActive(bool active)
    {
        active = active && HasContent;
        if (aimPreviewActive == active)
        {
            return;
        }

        aimPreviewActive = active;
        RefreshAimPreviewState();
    }

    public Rect GetRectInSpace(RectTransform relativeTo)
    {
        CacheComponents();
        if (selfRectTransform == null || relativeTo == null)
        {
            return Rect.zero;
        }

        selfRectTransform.GetWorldCorners(worldCornersBuffer);

        var min = (Vector2)relativeTo.InverseTransformPoint(worldCornersBuffer[0]);
        var max = min;
        for (int i = 1; i < worldCornersBuffer.Length; i++)
        {
            var localCorner = (Vector2)relativeTo.InverseTransformPoint(worldCornersBuffer[i]);
            min = Vector2.Min(min, localCorner);
            max = Vector2.Max(max, localCorner);
        }

        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public void CopyContentFrom(ChessElement other)
    {
        if (other == null)
        {
            ClearContent();
            return;
        }

        SetCellContent(other.Type, other.Life, other.SpecialValue, other.LegacyShapeType);
    }

    public void ResetVisualPosition()
    {
        StopMoveAnimation();
        CacheComponents();
        if (selfRectTransform != null)
        {
            selfRectTransform.anchoredPosition = baseAnchoredPosition;
        }
    }

    public void PlayDropAnimationFromRows(int rowCount, float duration, Ease ease)
    {
        CacheComponents();
        if (selfRectTransform == null || rowCount <= 0 || !HasContent)
        {
            ResetVisualPosition();
            return;
        }

        var targetPosition = baseAnchoredPosition;
        var startPosition = targetPosition + Vector2.up * (GetRowStep() * rowCount);
        if (!Application.isPlaying || duration <= 0f)
        {
            selfRectTransform.anchoredPosition = targetPosition;
            return;
        }

        StopMoveAnimation();
        selfRectTransform.anchoredPosition = startPosition;
        moveTween = selfRectTransform
            .DOAnchorPos(targetPosition, duration)
            .SetEase(ease)
            .SetLink(gameObject)
            .OnComplete(() => moveTween = null);
    }

    private void RefreshPosition()
    {
        if (ChessData == null)
        {
            return;
        }

        CacheComponents();
        baseAnchoredPosition = new Vector2(
            ChessData.X * (cellSize.x + m_Space.x) + (cellSize.x * 0.5f) + m_Offset.x,
            -(ChessData.Y * (cellSize.y + m_Space.y) + (cellSize.y * 0.5f)) + m_Offset.y);
        selfRectTransform.anchoredPosition = baseAnchoredPosition;
    }

    private void RefreshView()
    {
        CacheComponents();
        RefreshDebugName();

        if (!HasContent && aimPreviewActive)
        {
            aimPreviewActive = false;
            StopAimPreviewAnimation();
        }

        var baseColor = GetContentColor(Type, Life);
        var displayColor = aimPreviewActive ? Color.Lerp(baseColor, Color.white, m_AimPreviewHighlight) : baseColor;
        var bodyImage = GetBodyImage(Type);
        var bodySprite = ResolveBodySprite(Type, Life);
        var inactiveBodyImage = GetInactiveBodyImage(bodyImage);

        if (bodyImage != null)
        {
            bodyImage.sprite = bodySprite;
            bodyImage.color = bodySprite != null
                ? (aimPreviewActive ? new Color(1f, 1f, 1f, 0.92f) : Color.white)
                : displayColor;
            bodyImage.enabled = HasContent || bodySprite != null;
            ApplyBodyRotation(bodyImage.rectTransform, Type, legacyShapeType);
        }

        if (inactiveBodyImage != null)
        {
            inactiveBodyImage.sprite = null;
            inactiveBodyImage.enabled = false;
            ApplyBodyRotation(inactiveBodyImage.rectTransform, LevelCellType.Empty, LegacyBrickShapeType.None);
        }

        if (overlayImage != null)
        {
            overlayImage.sprite = null;
            overlayImage.enabled = false;
        }

        RefreshLifeLabel();
    }

    private Color GetContentColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty)
        {
            return m_EmptyColor;
        }

        var t = LevelCellTypeUtility.UsesLife(type)
            ? Mathf.Clamp01((currentLife - 1) / 8f)
            : LevelCellTypeConstants.SpecialVisualBlendFactor;
        switch (type)
        {
            case LevelCellType.Triangle:
            case LevelCellType.Square:
                return GetBrickLifeBandColor(ResolveBrickLifeBand(currentLife));
            case LevelCellType.HorizontalBlast:
                return Color.Lerp(HorizontalBlastLowLifeColor, HorizontalBlastHighLifeColor, t);
            case LevelCellType.VerticalBlast:
                return Color.Lerp(VerticalBlastLowLifeColor, VerticalBlastHighLifeColor, t);
            case LevelCellType.SplitThreeWay:
                return Color.Lerp(SplitThreeWayLowLifeColor, SplitThreeWayHighLifeColor, t);
            case LevelCellType.Redirect:
                return Color.Lerp(RedirectLowLifeColor, RedirectHighLifeColor, t);
            case LevelCellType.CrossBlast:
                return Color.Lerp(CrossBlastLowLifeColor, CrossBlastHighLifeColor, t);
            case LevelCellType.ExtraBalls:
                return Color.Lerp(ExtraBallsLowLifeColor, ExtraBallsHighLifeColor, t);
            default:
                return GetBrickLifeBandColor(ResolveBrickLifeBand(currentLife));
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void RefreshDebugName()
    {
        if (ChessData == null)
        {
            return;
        }

        name = $"Element[X:{X} Y:{Y} Type:{Type} Life:{Life} Special:{SpecialValue} Shape:{LegacyShapeType}]";
    }

    private static LegacyBrickShapeType ResolveRuntimeShapeType(LevelCellType type, LegacyBrickShapeType shapeType)
    {
        if (type == LevelCellType.Empty)
        {
            return LegacyBrickShapeType.None;
        }

        if (shapeType != LegacyBrickShapeType.None)
        {
            return shapeType;
        }

        return type == LevelCellType.Triangle
            ? LegacyBrickShapeType.EquilateralTriangle
            : LegacyBrickShapeType.Square;
    }

    private Image GetBodyImage(LevelCellType type)
    {
        if (type == LevelCellType.Triangle && m_BodyImage != null && m_BodyImage != image)
        {
            return m_BodyImage;
        }

        return image;
    }

    private Image GetInactiveBodyImage(Image activeBodyImage)
    {
        if (m_BodyImage != null && m_BodyImage != image && m_BodyImage != activeBodyImage)
        {
            return m_BodyImage;
        }

        if (image != null && image != activeBodyImage)
        {
            return image;
        }

        return null;
    }

    private Sprite ResolveBodySprite(LevelCellType type, int currentLife)
    {
        if (!LevelCellTypeUtility.UsesLife(type))
        {
            return null;
        }

        return ResolveBodySprite(type == LevelCellType.Triangle, ResolveBrickLifeBand(currentLife));
    }

    private Sprite ResolveBodySprite(bool isTriangle, BrickLifeBand lifeBand)
    {
        switch (lifeBand)
        {
            case BrickLifeBand.Purple:
                return isTriangle ? FirstAssigned(m_PurpleTriangleBodySprite, m_RedTriangleBodySprite, m_YellowTriangleBodySprite, m_GreenTriangleBodySprite, m_BlueTriangleBodySprite)
                    : FirstAssigned(m_PurpleSquareBodySprite, m_RedSquareBodySprite, m_YellowSquareBodySprite, m_GreenSquareBodySprite, m_BlueSquareBodySprite);
            case BrickLifeBand.Red:
                return isTriangle ? FirstAssigned(m_RedTriangleBodySprite, m_YellowTriangleBodySprite, m_GreenTriangleBodySprite, m_BlueTriangleBodySprite, m_PurpleTriangleBodySprite)
                    : FirstAssigned(m_RedSquareBodySprite, m_YellowSquareBodySprite, m_GreenSquareBodySprite, m_BlueSquareBodySprite, m_PurpleSquareBodySprite);
            case BrickLifeBand.Yellow:
                return isTriangle ? FirstAssigned(m_YellowTriangleBodySprite, m_GreenTriangleBodySprite, m_BlueTriangleBodySprite, m_RedTriangleBodySprite, m_PurpleTriangleBodySprite)
                    : FirstAssigned(m_YellowSquareBodySprite, m_GreenSquareBodySprite, m_BlueSquareBodySprite, m_RedSquareBodySprite, m_PurpleSquareBodySprite);
            case BrickLifeBand.Green:
                return isTriangle ? FirstAssigned(m_GreenTriangleBodySprite, m_BlueTriangleBodySprite, m_YellowTriangleBodySprite, m_RedTriangleBodySprite, m_PurpleTriangleBodySprite)
                    : FirstAssigned(m_GreenSquareBodySprite, m_BlueSquareBodySprite, m_YellowSquareBodySprite, m_RedSquareBodySprite, m_PurpleSquareBodySprite);
            default:
                return isTriangle ? FirstAssigned(m_BlueTriangleBodySprite, m_GreenTriangleBodySprite, m_YellowTriangleBodySprite, m_RedTriangleBodySprite, m_PurpleTriangleBodySprite)
                    : FirstAssigned(m_BlueSquareBodySprite, m_GreenSquareBodySprite, m_YellowSquareBodySprite, m_RedSquareBodySprite, m_PurpleSquareBodySprite);
        }
    }

    private void ApplyBodyRotation(RectTransform bodyRectTransform, LevelCellType type, LegacyBrickShapeType shapeType)
    {
        if (bodyRectTransform == null)
        {
            return;
        }

        var rotationZ = 0f;
        if (type == LevelCellType.Triangle)
        {
            rotationZ = GetTriangleBodyRotation(shapeType);
        }

        bodyRectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
    }

    private static float GetTriangleBodyRotation(LegacyBrickShapeType shapeType)
    {
        switch (shapeType)
        {
            case LegacyBrickShapeType.RightTriangleLeftDown:
                return 0f;
            case LegacyBrickShapeType.RightTriangleLeftUp:
                return 270f;
            case LegacyBrickShapeType.RightTriangleRightUp:
                return 180f;
            case LegacyBrickShapeType.RightTriangleRightDown:
                return 90f;
            default:
                return 0f;
        }
    }

    private static BrickLifeBand ResolveBrickLifeBand(int currentLife)
    {
        if (currentLife >= PurpleLifeMin)
        {
            return BrickLifeBand.Purple;
        }

        if (currentLife >= RedLifeMin)
        {
            return BrickLifeBand.Red;
        }

        if (currentLife >= YellowLifeMin)
        {
            return BrickLifeBand.Yellow;
        }

        if (currentLife >= GreenLifeMin)
        {
            return BrickLifeBand.Green;
        }

        return BrickLifeBand.Blue;
    }

    private static Color GetBrickLifeBandColor(BrickLifeBand lifeBand)
    {
        switch (lifeBand)
        {
            case BrickLifeBand.Purple:
                return PurpleBrickColor;
            case BrickLifeBand.Red:
                return RedBrickColor;
            case BrickLifeBand.Yellow:
                return YellowBrickColor;
            case BrickLifeBand.Green:
                return GreenBrickColor;
            default:
                return BlueBrickColor;
        }
    }

    private static Sprite FirstAssigned(params Sprite[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null)
            {
                return candidates[i];
            }
        }

        return null;
    }

    private void CacheComponents()
    {
        if (selfRectTransform == null)
        {
            selfRectTransform = GetComponent<RectTransform>();
        }

        if (image == null)
        {
            image = GetComponent<Image>();
        }

        if (m_BodyImage == null)
        {
            m_BodyImage = image;
            var bodyTransform = transform.Find("body");
            if (bodyTransform != null)
            {
                var bodyImage = bodyTransform.GetComponent<Image>();
                if (bodyImage != null)
                {
                    m_BodyImage = bodyImage;
                }
            }
        }

        if (overlayImage == null)
        {
            var overlayTransform = transform.Find("overlay");
            if (overlayTransform != null)
            {
                overlayImage = overlayTransform.GetComponent<Image>();
            }
        }

        if (m_BrickLife == null)
        {
            var lifeLabels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < lifeLabels.Length; i++)
            {
                if (lifeLabels[i] != null && lifeLabels[i].gameObject != gameObject)
                {
                    m_BrickLife = lifeLabels[i];
                    break;
                }
            }
        }

        cellSize = selfRectTransform.sizeDelta;
    }

    private void RefreshLifeLabel()
    {
        if (m_BrickLife == null)
        {
            return;
        }

        var hasContent = HasContent;
        if (m_BrickLife.gameObject.activeSelf != hasContent)
        {
            m_BrickLife.gameObject.SetActive(hasContent);
        }

        if (!hasContent)
        {
            m_BrickLife.text = string.Empty;
            ApplyLifeLabelOffset(Type, legacyShapeType);
            return;
        }

        m_BrickLife.text = GetRuntimeLifeLabel(Type, life, specialValue);
        m_BrickLife.color = GetLifeTextColor(Type, Life);
        ApplyLifeLabelOffset(Type, legacyShapeType);
    }

    private void ApplyLifeLabelOffset(LevelCellType type, LegacyBrickShapeType shapeType)
    {
        if (m_BrickLife == null)
        {
            return;
        }

        var labelRect = m_BrickLife.rectTransform;
        if (labelRect == null)
        {
            return;
        }

        if (type != LevelCellType.Triangle)
        {
            labelRect.anchoredPosition = Vector2.zero;
            return;
        }

        var xOffset = cellSize.x * TriangleLifeLabelOffsetFactor;
        var yOffset = cellSize.y * TriangleLifeLabelOffsetFactor;
        switch (shapeType)
        {
            case LegacyBrickShapeType.RightTriangleLeftDown:
                labelRect.anchoredPosition = new Vector2(-xOffset, -yOffset);
                break;
            case LegacyBrickShapeType.RightTriangleLeftUp:
                labelRect.anchoredPosition = new Vector2(-xOffset, yOffset);
                break;
            case LegacyBrickShapeType.RightTriangleRightUp:
                labelRect.anchoredPosition = new Vector2(xOffset, yOffset);
                break;
            case LegacyBrickShapeType.RightTriangleRightDown:
                labelRect.anchoredPosition = new Vector2(xOffset, -yOffset);
                break;
            default:
                labelRect.anchoredPosition = Vector2.zero;
                break;
        }
    }

    private static string GetRuntimeLifeLabel(LevelCellType type, int currentLife, int currentSpecialValue)
    {
        if (type == LevelCellType.Empty)
        {
            return string.Empty;
        }

        if (type == LevelCellType.ExtraBalls)
        {
            return $"+{LevelCellTypeUtility.ResolveSpecialValue(type, currentSpecialValue)}";
        }

        var marker = GetRuntimeTypeMarker(type);
        if (LevelCellTypeUtility.IsSpecial(type))
        {
            return marker;
        }

        return string.IsNullOrEmpty(marker) ? currentLife.ToString() : $"{marker}{currentLife}";
    }

    private static string GetRuntimeTypeMarker(LevelCellType type)
    {
        switch (type)
        {
            case LevelCellType.HorizontalBlast:
                return "H";
            case LevelCellType.VerticalBlast:
                return "V";
            case LevelCellType.SplitThreeWay:
                return "3";
            case LevelCellType.Redirect:
                return "R";
            case LevelCellType.CrossBlast:
                return "X";
            case LevelCellType.ExtraBalls:
                return "E";
            default:
                return string.Empty;
        }
    }

    private Color GetLifeTextColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty)
        {
            return Color.clear;
        }

        var backgroundColor = GetContentColor(type, currentLife);
        var luminance = (backgroundColor.r * 0.299f) + (backgroundColor.g * 0.587f) + (backgroundColor.b * 0.114f);
        return luminance < 0.58f ? Color.white : DarkLifeTextColor;
    }

    private void CacheHitEffectPlayers()
    {
        var behaviours = GetComponents<MonoBehaviour>();
        var effectPlayers = new List<IChessHitEffectPlayer>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IChessHitEffectPlayer effectPlayer)
            {
                effectPlayers.Add(effectPlayer);
            }
        }

        if (effectPlayers.Count == 0)
        {
            gameObject.AddComponent<DefaultChessHitEffect>();
            behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IChessHitEffectPlayer effectPlayer)
                {
                    effectPlayers.Add(effectPlayer);
                }
            }
        }

        hitEffectPlayers = effectPlayers.ToArray();
    }

    private void StopMoveAnimation()
    {
        if (moveTween == null)
        {
            return;
        }

        moveTween.Kill(false);
        moveTween = null;
    }

    private void RefreshAimPreviewState()
    {
        CacheComponents();
        StopAimPreviewAnimation();
        RefreshView();
    }

    private void PlayHitEffect(in ChessHitEffectContext hitContext)
    {
        if (hitEffectPlayers == null || hitEffectPlayers.Length == 0)
        {
            CacheHitEffectPlayers();
        }

        if (hitEffectPlayers == null)
        {
            return;
        }

        for (int i = 0; i < hitEffectPlayers.Length; i++)
        {
            hitEffectPlayers[i]?.PlayHitEffect(hitContext);
        }
    }

    private void StopAimPreviewAnimation()
    {
        if (aimPreviewTween != null)
        {
            aimPreviewTween.Kill(false);
            aimPreviewTween = null;
        }

        if (selfRectTransform != null)
        {
            selfRectTransform.localScale = Vector3.one;
        }
    }
}
