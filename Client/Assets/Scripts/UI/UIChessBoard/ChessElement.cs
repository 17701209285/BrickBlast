using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ChessElement : MonoBehaviour
{
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
    [SerializeField]
    private Vector2 m_Space;
    [SerializeField]
    private Vector2 m_Offset;
    [SerializeField]
    private Color m_EmptyColor = new Color(1f, 1f, 1f, 0.08f);
    [SerializeField]
    private Color m_SquareLowLifeColor = new Color(0.96f, 0.79f, 0.33f, 1f);
    [SerializeField]
    private Color m_SquareHighLifeColor = new Color(0.88f, 0.29f, 0.23f, 1f);
    [SerializeField]
    private Color m_TriangleLowLifeColor = new Color(0.44f, 0.78f, 0.97f, 1f);
    [SerializeField]
    private Color m_TriangleHighLifeColor = new Color(0.08f, 0.45f, 0.89f, 1f);
    [SerializeField]
    [Range(0f, 1f)]
    private float m_AimPreviewHighlight = 0.35f;
    [SerializeField]
    [Min(1f)]
    private float m_AimPreviewScale = 1.06f;
    [SerializeField]
    [Min(0f)]
    private float m_AimPreviewPulseDuration = 0.2f;

    [SerializeField]
    TextMeshProUGUI m_BrickLife;

    private static readonly Color DarkLifeTextColor = new Color(0.12f, 0.12f, 0.12f, 1f);

    private ChessElementData ChessData;
    private RectTransform selfRectTransform;
    private Image image;
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
    private IChessHitEffectPlayer[] hitEffectPlayers;
    private readonly Vector3[] worldCornersBuffer = new Vector3[4];

    public int X => ChessData?.X ?? -1;
    public int Y => ChessData?.Y ?? -1;
    public LevelCellType Type => LevelCellTypeUtility.NormalizeType(cellType, life);
    public int Life => LevelCellTypeUtility.NormalizeLife(cellType, life);
    public int SpecialValue => LevelCellTypeUtility.ResolveSpecialValue(cellType, specialValue);
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

    public void SetCellContent(LevelCellType inType, int inLife, int inSpecialValue = 0)
    {
        cellType = LevelCellTypeUtility.NormalizeType(inType, inLife);
        life = LevelCellTypeUtility.NormalizeLife(cellType, inLife);
        specialValue = LevelCellTypeUtility.ResolveSpecialValue(cellType, inSpecialValue);
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
        SetCellContent(LevelCellType.Empty, 0, 0);
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

        SetCellContent(other.Type, other.Life, other.SpecialValue);
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

        if (image != null)
        {
            var baseColor = GetContentColor(Type, Life);
            image.color = aimPreviewActive ? Color.Lerp(baseColor, Color.white, m_AimPreviewHighlight) : baseColor;
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
                return Color.Lerp(m_TriangleLowLifeColor, m_TriangleHighLifeColor, t);
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
                return Color.Lerp(m_SquareLowLifeColor, m_SquareHighLifeColor, t);
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void RefreshDebugName()
    {
        if (ChessData == null)
        {
            return;
        }

        name = $"Element[X:{X} Y:{Y} Type:{Type} Life:{Life} Special:{SpecialValue}]";
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
            return;
        }

        m_BrickLife.text = GetRuntimeLifeLabel(Type, life, specialValue);
        m_BrickLife.color = GetLifeTextColor(Type, Life);
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

        if (!aimPreviewActive || selfRectTransform == null || !Application.isPlaying)
        {
            return;
        }

        selfRectTransform.localScale = Vector3.one;
        aimPreviewTween = selfRectTransform
            .DOScale(m_AimPreviewScale, Mathf.Max(0.01f, m_AimPreviewPulseDuration))
            .SetEase(Ease.OutQuad)
            .SetLoops(-1, LoopType.Yoyo)
            .SetLink(gameObject);
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
