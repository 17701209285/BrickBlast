using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class ChessElement : MonoBehaviour
{
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

    private ChessElementData ChessData;
    private RectTransform selfRectTransform;
    private Image image;
    private Vector2 cellSize;
    private Vector2 baseAnchoredPosition;
    private LevelCellType cellType = LevelCellType.Empty;
    private int life;
    private Tween moveTween;
    private Tween aimPreviewTween;
    private bool aimPreviewActive;
    private IChessHitEffectPlayer[] hitEffectPlayers;
    private readonly Vector3[] worldCornersBuffer = new Vector3[4];

    public int X => ChessData?.X ?? -1;
    public int Y => ChessData?.Y ?? -1;
    public LevelCellType Type => life <= 0 ? LevelCellType.Empty : cellType;
    public int Life => life;
    public bool HasContent => Type != LevelCellType.Empty && Life > 0;

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

    public void SetCellContent(LevelCellType inType, int inLife)
    {
        life = Mathf.Max(0, inLife);
        cellType = life <= 0 ? LevelCellType.Empty : inType;
        RefreshView();
    }

    public bool TryApplyDamage(int damage, Vector2 hitPointInBoardSpace, out ChessHitEffectContext hitContext)
    {
        hitContext = default;
        damage = Mathf.Max(0, damage);
        if (!HasContent || damage <= 0)
        {
            return false;
        }

        // 中文备注：方块的运行时生命统一从这里扣，后面不管你换什么受击特效，都可以复用这个入口。
        var previousLife = life;
        life = Mathf.Max(0, life - damage);
        if (life <= 0)
        {
            cellType = LevelCellType.Empty;
        }

        RefreshView();

        hitContext = new ChessHitEffectContext(this, hitPointInBoardSpace, damage, previousLife, life);
        PlayHitEffect(hitContext);
        return true;
    }

    public void ClearContent()
    {
        SetCellContent(LevelCellType.Empty, 0);
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

        SetCellContent(other.Type, other.Life);
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
    }

    private Color GetContentColor(LevelCellType type, int currentLife)
    {
        if (type == LevelCellType.Empty || currentLife <= 0)
        {
            return m_EmptyColor;
        }

        var t = Mathf.Clamp01((currentLife - 1) / 8f);
        if (type == LevelCellType.Triangle)
        {
            return Color.Lerp(m_TriangleLowLifeColor, m_TriangleHighLifeColor, t);
        }

        return Color.Lerp(m_SquareLowLifeColor, m_SquareHighLifeColor, t);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void RefreshDebugName()
    {
        if (ChessData == null)
        {
            return;
        }

        name = $"Element[X:{X} Y:{Y} Type:{Type} Life:{Life}]";
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

        cellSize = selfRectTransform.sizeDelta;
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
