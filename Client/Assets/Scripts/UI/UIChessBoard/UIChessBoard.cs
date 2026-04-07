using System;
using System.Collections;
using DG.Tweening;
using System.Collections.Generic;
using ImportedLevels;
using UnityEngine;
using TMPro;
using YooAsset;

public readonly struct ProjectileHitEffectResult
{
    public bool SplitIntoThreeWay { get; }
    public bool PassThrough { get; }
    public Vector2 SplitOrigin { get; }
    public Vector2 SplitDirection { get; }
    public bool RedirectCurrentProjectile { get; }
    public Vector2 RedirectOrigin { get; }
    public Vector2 RedirectDirection { get; }
    public int AddedBallCount { get; }

    public ProjectileHitEffectResult(
        bool splitIntoThreeWay,
        bool passThrough,
        Vector2 splitOrigin,
        Vector2 splitDirection,
        bool redirectCurrentProjectile,
        Vector2 redirectOrigin,
        Vector2 redirectDirection,
        int addedBallCount)
    {
        SplitIntoThreeWay = splitIntoThreeWay;
        PassThrough = passThrough;
        SplitOrigin = splitOrigin;
        SplitDirection = splitDirection;
        RedirectCurrentProjectile = redirectCurrentProjectile;
        RedirectOrigin = redirectOrigin;
        RedirectDirection = redirectDirection;
        AddedBallCount = Mathf.Max(0, addedBallCount);
    }
}

public enum ChessDamageSource
{
    Projectile = 0,
    HorizontalBlast = 1,
    VerticalBlast = 2,
    CrossBlast = 3
}

public class UIChessBoard : MonoBehaviour
{
    public event Action<bool> GameOverStateChanged;
    public event Action<ChessBoardImpactSummary> ImpactResolved;

    public readonly struct CollisionCandidate
    {
        public ChessElement Element { get; }
        public Rect RectInBoardSpace { get; }
        public LegacyBrickShapeType ShapeType { get; }

        public CollisionCandidate(ChessElement element, Rect rectInBoardSpace, LegacyBrickShapeType shapeType)
        {
            Element = element;
            RectInBoardSpace = rectInBoardSpace;
            ShapeType = shapeType;
        }
    }

    [SerializeField]
    private ChessElement OriginPrefab;

    [SerializeField]
    private Transform ParentTransform;

    [SerializeField]
    private LevelConfigScritable LevelConfig;

    [SerializeField]
    private bool LoadConfigOnStart = true;

    [SerializeField]
    private bool AnimateDrop = true;

    [SerializeField]
    [Min(0f)]
    private float DropDuration = 0.22f;

    [SerializeField]
    [Min(0f)]
    private float DropDurationPerExtraRow = 0.04f;

    [SerializeField]
    [Min(0f)]
    private float MaxDropDuration = 0.30f;

    [SerializeField]
    private Ease DropEase = Ease.OutCubic;

    [SerializeField]
    [Min(0f)]
    private float CollisionRectInset = 0f;

    [SerializeField]
    private UIChessBoardLayoutController LayoutController;

    [SerializeField]
    private TextMeshProUGUI CurrentLevel;

    [SerializeField]
    private TextMeshProUGUI NexttLevel;

    [SerializeField]
    private TextMeshProUGUI LastLevel;

    private ArrayList<ChessElement> chessElements;
    private readonly List<CollisionCandidate> collisionCandidates = new List<CollisionCandidate>(64);
    private readonly List<CollisionCandidate> specialTriggerCandidates = new List<CollisionCandidate>(32);
    private readonly Vector3[] playAreaWorldCornersBuffer = new Vector3[4];
    private LevelCellType[] shiftSourceTypesBuffer;
    private int[] shiftSourceLivesBuffer;
    private int[] shiftSourceSpecialValuesBuffer;
    private LegacyBrickShapeType[] shiftSourceShapeTypesBuffer;
    private RectTransform collisionCandidateSpace;
    private int collisionCandidateFrame = -1;
    private int boardWidth;
    private int boardHeight;
    private int visibleStartRow;
    private bool isGameOver;
    private string currentLevelAddress = string.Empty;

    public int BoardWidth => boardWidth;
    public int BoardHeight => boardHeight;
    public int BottomRowIndex => Mathf.Max(0, boardHeight - 1);
    public IReadOnlyList<CollisionCandidate> CollisionCandidates => collisionCandidates;
    public IReadOnlyList<CollisionCandidate> SpecialTriggerCandidates => specialTriggerCandidates;
    public bool IsGameOver => isGameOver;
    public string CurrentLevelAddress => ResolveLevelAddress(LevelConfig, currentLevelAddress);
    public RectTransform PrimaryShakeTarget => ParentTransform as RectTransform != null
        ? ParentTransform as RectTransform
        : transform as RectTransform;

    private void Start()
    {
        EnsureOriginPrefabReference();
        EnsureLayoutController();
        RefreshLevelLabels();
        if (LoadConfigOnStart)
        {
            ReloadLevel();
            return;
        }

        visibleStartRow = LevelConfig == null ? 0 : LevelConfig.GetInitialVisibleStartRow();
        InitChessBoard(GetBoardWidth(), GetBoardHeight());
        ClearVisibleBoard();
    }

    [ContextMenu("Reload Level")]
    public void ReloadLevel()
    {
        EnsureOriginPrefabReference();
        visibleStartRow = LevelConfig == null ? 0 : LevelConfig.GetInitialVisibleStartRow();
        currentLevelAddress = ResolveLevelAddress(LevelConfig, currentLevelAddress);
        RefreshLevelLabels();
        InitChessBoard(GetBoardWidth(), GetBoardHeight());
        ClearVisibleBoard();

        if (LevelConfig == null)
        {
            return;
        }

        ApplyCells(LevelConfig.GetInitialBoardCells());
    }

    [ContextMenu("Clear Visible Bricks")]
    public void ClearVisibleBricks()
    {
        ClearVisibleBoard();
    }

    [ContextMenu("Drop Next Batch")]
    public void DropNextBatch()
    {
        if (LevelConfig == null || !LevelConfig.HasPendingDropRows(visibleStartRow))
        {
            return;
        }

        var rowCount = LevelConfig.GetNextDropCount(visibleStartRow);
        if (rowCount <= 0)
        {
            return;
        }

        MoveBoardDown(rowCount);
        ApplyCells(LevelConfig.GetDropBatchCells(visibleStartRow), rowCount);
        visibleStartRow = Mathf.Max(0, visibleStartRow - rowCount);
    }

    [ContextMenu("Drop Next Batch After Clear")]
    public void DropNextBatchAfterClear()
    {
        ClearVisibleBoard();
        DropNextBatch();
    }

    [ContextMenu("Move Board Down One Row")]
    public void MoveBoardDownOneRow()
    {
        MoveBoardDownAndFillPendingRows(1);
    }

    public void MoveBoardDown(int rowCount)
    {
        if (rowCount <= 0)
        {
            return;
        }

        ShiftVisibleContentDown(rowCount);
    }

    private void MoveBoardDownAndFillPendingRows(int rowCount)
    {
        if (rowCount <= 0)
        {
            return;
        }

        MoveBoardDown(rowCount);

        if (LevelConfig == null)
        {
            return;
        }

        var pendingRowCount = Mathf.Min(rowCount, LevelConfig.GetRemainingDropRowCount(visibleStartRow));
        if (pendingRowCount <= 0)
        {
            return;
        }

        var sourceRow = Mathf.Max(0, visibleStartRow - pendingRowCount);
        ApplyCells(LevelConfig.GetCellsInGlobalRowRange(sourceRow, pendingRowCount), rowCount);
        visibleStartRow = Mathf.Max(0, visibleStartRow - pendingRowCount);
    }

    public ChessElement GetChessElement(int x, int y)
    {
        return chessElements?.Get(x, y);
    }

    public bool HasPendingDropBatch()
    {
        return LevelConfig != null && LevelConfig.HasPendingDropRows(visibleStartRow);
    }

    public bool HasPendingDropContent()
    {
        return LevelConfig != null && LevelConfig.HasPendingDropContent(visibleStartRow);
    }

    public bool AreVisibleBricksCleared()
    {
        if (chessElements == null)
        {
            return true;
        }

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var chessElement = chessElements.Get(x, y);
                if (chessElement != null && chessElement.CountsAsBrick)
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool HasBrickReachedBottomRow()
    {
        if (chessElements == null || boardHeight <= 0)
        {
            return false;
        }

        var bottomRow = BottomRowIndex;
        for (int x = 0; x < boardWidth; x++)
        {
            var chessElement = chessElements.Get(x, bottomRow);
            if (chessElement != null && chessElement.CountsAsBrick)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryDropNextBatchIfCleared()
    {
        if (!AreVisibleBricksCleared() || !HasPendingDropBatch())
        {
            return false;
        }

        DropNextBatch();
        return true;
    }

    public bool IsLevelCompleted()
    {
        return AreVisibleBricksCleared() && !HasPendingDropContent();
    }

    public int GetPendingDropRowCount()
    {
        return LevelConfig == null ? 0 : LevelConfig.GetNextDropCount(visibleStartRow);
    }

    public ProjectileHitEffectResult ResolveProjectileBlockHit(in BallCollisionHit hit)
    {
        return ResolveProjectileBlockHit(hit, true);
    }

    public ProjectileHitEffectResult ResolveProjectileBlockHit(in BallCollisionHit hit, bool allowSplitSpecial)
    {
        if (hit.Type != BallCollisionType.Block && hit.Type != BallCollisionType.SpecialTrigger)
        {
            return default;
        }

        var result = default(ProjectileHitEffectResult);
        var impactAccumulator = new ChessBoardImpactAccumulator(ChessDamageSource.Projectile, hit.ImpactPoint);
        ApplyDamageWithEffects(
            hit.Block,
            1,
            hit.ImpactPoint,
            hit.ImpactDirection,
            ChessDamageSource.Projectile,
            ref result,
            impactAccumulator,
            allowSplitSpecial);

        if (hit.Type == BallCollisionType.Block && hit.AdditionalBlock != null && hit.AdditionalBlock != hit.Block)
        {
            ApplyDamageWithEffects(
                hit.AdditionalBlock,
                1,
                hit.AdditionalImpactPoint,
                hit.ImpactDirection,
                ChessDamageSource.Projectile,
                ref result,
                impactAccumulator,
                allowSplitSpecial);
        }

        RebuildCollisionCandidates();

        if (impactAccumulator.HasAnyImpact)
        {
            ImpactResolved?.Invoke(impactAccumulator.BuildSummary());
        }

        return result;
    }

    public ProjectileHitEffectResult ResolveProjectileBlockHit(ChessElement target, Vector2 hitPointInBoardSpace)
    {
        return ResolveProjectileBlockHit(new BallCollisionHit(
            BallCollisionType.Block,
            0f,
            hitPointInBoardSpace,
            Vector2.zero,
            hitPointInBoardSpace,
            Vector2.up,
            target));
    }

    public void ClearTouchedSpecialItemsAtTurnEnd()
    {
        if (chessElements == null)
        {
            return;
        }

        var clearedAny = false;
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var chessElement = chessElements.Get(x, y);
                if (chessElement != null && chessElement.ClearTouchedSpecialAtTurnEnd())
                {
                    clearedAny = true;
                }
            }
        }

        if (clearedAny)
        {
            RebuildCollisionCandidates();
        }
    }

    public float GetPredictedDropAnimationDuration(int rowCount)
    {
        return GetDropAnimationDuration(rowCount);
    }

    public void RefreshCollisionCandidates(RectTransform relativeTo)
    {
        if (relativeTo == null)
        {
            RebuildCollisionCandidates();
            return;
        }

        if (collisionCandidateSpace == relativeTo && collisionCandidateFrame == Time.frameCount)
        {
            return;
        }

        // 中文备注：碰撞矩形仍然会缓存，但只在“当前帧”内复用。
        // 这样能保留大部分性能收益，同时避免跨帧缓存和 UI 实际位置漂掉。
        RebuildCollisionCandidates(relativeTo);
    }

    public bool TryGetPlayAreaRect(RectTransform relativeTo, float horizontalPadding, float topPadding, out Rect playAreaRect)
    {
        playAreaRect = default;
        var playFieldRectTransform = ParentTransform as RectTransform;
        if (playFieldRectTransform == null || relativeTo == null)
        {
            return false;
        }

        playFieldRectTransform.GetWorldCorners(playAreaWorldCornersBuffer);

        var min = (Vector2)relativeTo.InverseTransformPoint(playAreaWorldCornersBuffer[0]);
        var max = min;
        for (int i = 1; i < playAreaWorldCornersBuffer.Length; i++)
        {
            var localCorner = (Vector2)relativeTo.InverseTransformPoint(playAreaWorldCornersBuffer[i]);
            min = Vector2.Min(min, localCorner);
            max = Vector2.Max(max, localCorner);
        }

        // 中文备注：球的左右墙和顶部都应该跟棋盘可玩区域对齐，而不是跟整张背景 UI 对齐。
        playAreaRect = Rect.MinMaxRect(
            min.x + horizontalPadding,
            min.y,
            max.x - horizontalPadding,
            max.y - topPadding);
        return playAreaRect.width > 0f && playAreaRect.height > 0f;
    }

    public void SetLevelConfig(LevelConfigScritable levelConfig, string levelAddress = null)
    {
        LevelConfig = levelConfig;
        currentLevelAddress = ResolveLevelAddress(levelConfig, levelAddress);
        visibleStartRow = LevelConfig == null ? 0 : LevelConfig.GetInitialVisibleStartRow();
        RefreshLevelLabels();
        ApplyBoardLayout();
        SetGameOverState(false);
    }

    private void RefreshLevelLabels()
    {
        if (!TryGetCurrentLevelNumber(out var currentLevelNumber))
        {
            SetLevelLabel(CurrentLevel, "?");
            SetLevelLabel(NexttLevel, string.Empty);
            SetLevelLabel(LastLevel, string.Empty);
            return;
        }

        SetLevelLabel(CurrentLevel, currentLevelNumber.ToString());
        SetLevelLabel(NexttLevel, (currentLevelNumber + 1).ToString());
        SetLevelLabel(LastLevel, currentLevelNumber > 1 ? (currentLevelNumber - 1).ToString() : string.Empty);
    }

    public bool TryGetCurrentLevelNumber(out int levelNumber)
    {
        levelNumber = 0;
        if (LevelConfig == null)
        {
            return false;
        }

        var levelName = LevelConfig.name;
        if (string.IsNullOrWhiteSpace(levelName))
        {
            return false;
        }

        var endIndexExclusive = -1;
        for (int i = levelName.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(levelName[i]))
            {
                continue;
            }

            endIndexExclusive = i + 1;
            break;
        }

        if (endIndexExclusive <= 0)
        {
            return false;
        }

        var startIndexInclusive = endIndexExclusive - 1;
        while (startIndexInclusive >= 0 && char.IsDigit(levelName[startIndexInclusive]))
        {
            startIndexInclusive--;
        }

        var numberStart = startIndexInclusive + 1;
        var numberLength = endIndexExclusive - numberStart;
        if (numberLength <= 0)
        {
            return false;
        }

        return int.TryParse(levelName.Substring(numberStart, numberLength), out levelNumber) && levelNumber > 0;
    }

    private static void SetLevelLabel(TextMeshProUGUI label, string value)
    {
        if (label == null)
        {
            return;
        }

        label.text = value ?? string.Empty;
    }

    private static string ResolveLevelAddress(LevelConfigScritable levelConfig, string explicitAddress)
    {
        if (string.IsNullOrWhiteSpace(explicitAddress) == false)
        {
            return explicitAddress;
        }

        if (levelConfig == null || string.IsNullOrWhiteSpace(levelConfig.name))
        {
            return string.Empty;
        }

        return $"Assets/AssetBundle/LevelConfig/{levelConfig.name}.asset";
    }

    private void InitChessBoard(int width, int height)
    {
        EnsureOriginPrefabReference();
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        ApplyBoardLayout(width, height);

        if (OriginPrefab == null)
        {
            Debug.LogError("[UIChessBoard] OriginPrefab is missing. Failed to initialize chess board.", this);
            return;
        }

        if (chessElements != null && boardWidth == width && boardHeight == height)
        {
            return;
        }

        boardWidth = width;
        boardHeight = height;
        ClearInstancedElements();

        chessElements = new ArrayList<ChessElement>(boardWidth, boardHeight);

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var chessElement = InstanceChessElement(OriginPrefab);
                if (chessElement == null)
                {
                    continue;
                }

                chessElement.transform.SetParent(ParentTransform, false);
                chessElement.gameObject.SetActive(true);
                chessElement.InIt(new ChessElementData(x, y));
                chessElement.ClearContent();
                chessElements[x, y] = chessElement;
            }
        }
    }

    private void ClearVisibleBoard()
    {
        if (chessElements == null)
        {
            return;
        }

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var chessElement = chessElements.Get(x, y);
                if (chessElement != null)
                {
                    chessElement.ClearContent();
                    chessElement.ResetVisualPosition();
                }
            }
        }

        SetGameOverState(false);
        RebuildCollisionCandidates();
    }

    private void ShiftVisibleContentDown(int rowCount)
    {
        if (chessElements == null || rowCount <= 0)
        {
            return;
        }

        EnsureShiftSourceBuffers();
        var sourceTypes = shiftSourceTypesBuffer;
        var sourceLives = shiftSourceLivesBuffer;
        var sourceSpecialValues = shiftSourceSpecialValuesBuffer;
        var sourceShapeTypes = shiftSourceShapeTypesBuffer;
        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var source = chessElements.Get(x, y);
                var index = y * boardWidth + x;
                sourceTypes[index] = source == null ? LevelCellType.Empty : source.Type;
                sourceLives[index] = source == null ? 0 : source.Life;
                sourceSpecialValues[index] = source == null ? 0 : source.SpecialValue;
                sourceShapeTypes[index] = source == null ? LegacyBrickShapeType.None : source.LegacyShapeType;
            }
        }

        for (int y = boardHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var target = chessElements.Get(x, y);
                if (target == null)
                {
                    continue;
                }

                var sourceY = y - rowCount;
                if (sourceY >= 0)
                {
                    var sourceIndex = sourceY * boardWidth + x;
                    var sourceType = sourceTypes[sourceIndex];
                    var sourceLife = sourceLives[sourceIndex];
                    var sourceSpecialValue = sourceSpecialValues[sourceIndex];
                    var sourceShapeType = sourceShapeTypes[sourceIndex];
                    target.SetCellContent(sourceType, sourceLife, sourceSpecialValue, sourceShapeType);

                    var shouldAnimateDrop = LevelCellTypeUtility.HasSerializedContent(sourceType, sourceLife);
                    PlayDropAnimation(target, shouldAnimateDrop ? rowCount : 0);
                }
                else
                {
                    target.ClearContent();
                    target.ResetVisualPosition();
                }
            }
        }

        EvaluateBottomRowGameOver();
        RebuildCollisionCandidates();
    }

    private void EnsureShiftSourceBuffers()
    {
        var requiredLength = Mathf.Max(1, boardWidth * boardHeight);
        if (shiftSourceTypesBuffer == null || shiftSourceTypesBuffer.Length != requiredLength)
        {
            shiftSourceTypesBuffer = new LevelCellType[requiredLength];
        }

        if (shiftSourceLivesBuffer == null || shiftSourceLivesBuffer.Length != requiredLength)
        {
            shiftSourceLivesBuffer = new int[requiredLength];
        }

        if (shiftSourceSpecialValuesBuffer == null || shiftSourceSpecialValuesBuffer.Length != requiredLength)
        {
            shiftSourceSpecialValuesBuffer = new int[requiredLength];
        }

        if (shiftSourceShapeTypesBuffer == null || shiftSourceShapeTypesBuffer.Length != requiredLength)
        {
            shiftSourceShapeTypesBuffer = new LegacyBrickShapeType[requiredLength];
        }
    }

    private void ApplyCells(FCell[] cells, int animateFromRows = 0)
    {
        if (cells == null || chessElements == null)
        {
            return;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell == null)
            {
                continue;
            }

            var chessElement = chessElements.Get(cell.X, cell.Y);
            if (chessElement != null)
            {
                chessElement.SetCellContent(cell.Type, cell.Life, cell.SpecialValue, cell.LegacyShapeType);
                PlayDropAnimation(chessElement, animateFromRows);
            }
        }

        EvaluateBottomRowGameOver();
        RebuildCollisionCandidates();
    }

    private void EvaluateBottomRowGameOver()
    {
        if (isGameOver || !HasBrickReachedBottomRow())
        {
            return;
        }

        SetGameOverState(true);
    }

    private void SetGameOverState(bool value)
    {
        if (isGameOver == value)
        {
            return;
        }

        isGameOver = value;
        GameOverStateChanged?.Invoke(isGameOver);
    }

    private void PlayDropAnimation(ChessElement chessElement, int rowCount)
    {
        if (chessElement == null)
        {
            return;
        }

        if (!AnimateDrop || rowCount <= 0)
        {
            chessElement.ResetVisualPosition();
            return;
        }

        chessElement.PlayDropAnimationFromRows(rowCount, GetDropAnimationDuration(rowCount), DropEase);
    }

    private float GetDropAnimationDuration(int rowCount)
    {
        if (rowCount <= 0)
        {
            return 0f;
        }

        var extraRows = Mathf.Max(0, rowCount - 1);
        var scaledDuration = DropDuration + (DropDurationPerExtraRow * extraRows);
        return Mathf.Min(MaxDropDuration, scaledDuration);
    }

    private int GetBoardWidth()
    {
        return LevelConfigScritable.FixedBoardWidth;
    }

    private int GetBoardHeight()
    {
        return LevelConfigScritable.FixedVisibleHeight;
    }

    private void ApplyBoardLayout()
    {
        ApplyBoardLayout(GetBoardWidth(), GetBoardHeight());
    }

    private void ApplyBoardLayout(int width, int height)
    {
        EnsureOriginPrefabReference();
        EnsureLayoutController();
        LayoutController?.ApplyLayout(width, height, OriginPrefab, ParentTransform);
    }

    private void EnsureOriginPrefabReference()
    {
        if (OriginPrefab != null)
        {
            return;
        }

        var itemTransform = transform.Find("Item");
        if (itemTransform != null)
        {
            OriginPrefab = itemTransform.GetComponent<ChessElement>();
        }

        if (OriginPrefab == null)
        {
            OriginPrefab = GetComponentInChildren<ChessElement>(true);
        }
    }

    private void EnsureLayoutController()
    {
        if (LayoutController != null)
        {
            return;
        }

        LayoutController = GetComponent<UIChessBoardLayoutController>();
        if (LayoutController == null)
        {
            LayoutController = gameObject.AddComponent<UIChessBoardLayoutController>();
        }
    }

    private void ApplyDamageWithEffects(
        ChessElement target,
        int damage,
        Vector2 hitPointInBoardSpace,
        Vector2 impactDirection,
        ChessDamageSource source,
        ref ProjectileHitEffectResult projectileHitEffect,
        ChessBoardImpactAccumulator impactAccumulator,
        bool allowSplitSpecial = true)
    {
        if (target == null || damage <= 0 || !target.HasContent)
        {
            return;
        }

        var isSpecialItem = target.IsSpecialItem;
        if (!target.TryApplyDamage(damage, hitPointInBoardSpace, source, out var hitContext))
        {
            return;
        }

        impactAccumulator?.RegisterDamage(hitContext);

        if (!isSpecialItem)
        {
            return;
        }

        var specialEffectResult = ChessSpecialEffectProcessor.TryTrigger(
            this,
            target,
            impactDirection,
            source,
            impactAccumulator,
            allowSplitSpecial);
        if (specialEffectResult.IsTriggered)
        {
            projectileHitEffect = MergeProjectileHitEffects(projectileHitEffect, specialEffectResult.ToProjectileHitEffectResult());
        }
    }

    private static ProjectileHitEffectResult MergeProjectileHitEffects(in ProjectileHitEffectResult current, in ProjectileHitEffectResult next)
    {
        if (!current.SplitIntoThreeWay &&
            !current.PassThrough &&
            !current.RedirectCurrentProjectile &&
            current.AddedBallCount <= 0)
        {
            return next;
        }

        if (!next.SplitIntoThreeWay &&
            !next.PassThrough &&
            !next.RedirectCurrentProjectile &&
            next.AddedBallCount <= 0)
        {
            return current;
        }

        return new ProjectileHitEffectResult(
            current.SplitIntoThreeWay || next.SplitIntoThreeWay,
            current.PassThrough || next.PassThrough,
            next.SplitIntoThreeWay ? next.SplitOrigin : current.SplitOrigin,
            next.SplitIntoThreeWay ? next.SplitDirection : current.SplitDirection,
            current.RedirectCurrentProjectile || next.RedirectCurrentProjectile,
            next.RedirectCurrentProjectile ? next.RedirectOrigin : current.RedirectOrigin,
            next.RedirectCurrentProjectile ? next.RedirectDirection : current.RedirectDirection,
            current.AddedBallCount + next.AddedBallCount);
    }

    internal bool ApplyBlastDamageToTarget(ChessElement target, ChessDamageSource source, ChessBoardImpactAccumulator impactAccumulator)
    {
        if (target == null || !target.CountsAsBrick)
        {
            return false;
        }

        var previousType = target.Type;
        var previousLife = target.Life;
        var ignoredProjectileHit = default(ProjectileHitEffectResult);
        ApplyDamageWithEffects(
            target,
            LevelCellTypeConstants.SpecialBlastDamage,
            GetElementCenterInBoardSpace(target),
            Vector2.zero,
            source,
            ref ignoredProjectileHit,
            impactAccumulator);
        return previousType != target.Type || previousLife != target.Life;
    }

    private Vector2 GetElementCenterInBoardSpace(ChessElement target)
    {
        if (target == null)
        {
            return Vector2.zero;
        }

        var boardRectTransform = transform as RectTransform;
        if (boardRectTransform == null)
        {
            return Vector2.zero;
        }

        var rect = target.GetRectInSpace(boardRectTransform);
        return rect.center;
    }

    internal Vector2 GetSplitLaunchOrigin(ChessElement target)
    {
        if (target == null)
        {
            return Vector2.zero;
        }

        var boardRectTransform = transform as RectTransform;
        if (boardRectTransform == null)
        {
            return Vector2.zero;
        }

        var rect = target.GetRectInSpace(boardRectTransform);
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return rect.center;
        }

        // 中文备注：分裂/重定向从道具中心重新发射。
        // 边界起射会让第一段命中距离接近 0，容易被 sweep 当成无效接触直接放行。
        return rect.center;
    }

    private void ClearInstancedElements()
    {
        if (ParentTransform == null)
        {
            return;
        }

        for (int i = ParentTransform.childCount - 1; i >= 0; i--)
        {
            var child = ParentTransform.GetChild(i);
            DestroyElement(child.gameObject);
        }
    }

    private T InstanceChessElement<T>(T inOrigin) where T : UnityEngine.Object
    {
        var insPrefab = Instantiate(inOrigin);
        if (insPrefab == null)
        {
            return null;
        }

        return insPrefab;
    }

    private static void DestroyElement(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(gameObject);
            return;
        }

        DestroyImmediate(gameObject);
    }

    private void RebuildCollisionCandidates()
    {
        RebuildCollisionCandidates(transform as RectTransform);
    }

    private void RebuildCollisionCandidates(RectTransform relativeTo)
    {
        collisionCandidates.Clear();
        specialTriggerCandidates.Clear();
        collisionCandidateSpace = relativeTo;
        collisionCandidateFrame = Application.isPlaying ? Time.frameCount : -1;
        if (chessElements == null || relativeTo == null)
        {
            return;
        }

        for (int y = 0; y < boardHeight; y++)
        {
            for (int x = 0; x < boardWidth; x++)
            {
                var chessElement = chessElements.Get(x, y);
                if (chessElement == null || !chessElement.HasContent)
                {
                    continue;
                }

                var collisionRect = GetInsetCollisionRect(chessElement.GetRectInSpace(relativeTo));
                if (chessElement.CountsAsBrick)
                {
                    collisionCandidates.Add(new CollisionCandidate(chessElement, collisionRect, chessElement.LegacyShapeType));
                    continue;
                }

                if (chessElement.IsSpecialItem)
                {
                    specialTriggerCandidates.Add(new CollisionCandidate(chessElement, collisionRect, chessElement.LegacyShapeType));
                }
            }
        }
    }

    private Rect GetInsetCollisionRect(Rect sourceRect)
    {
        var inset = Mathf.Max(0f, CollisionRectInset);
        var maxInsetX = Mathf.Max(0f, (sourceRect.width * 0.5f) - 1f);
        var maxInsetY = Mathf.Max(0f, (sourceRect.height * 0.5f) - 1f);
        var appliedInsetX = Mathf.Min(inset, maxInsetX);
        var appliedInsetY = Mathf.Min(inset, maxInsetY);

        // 中文备注：实际碰撞矩形轻微内收一点，
        // 能让球的接触点更贴近你看到的砖块边缘，避免“空气反弹”和“视觉穿进去”之间来回拉扯。
        return Rect.MinMaxRect(
            sourceRect.xMin + appliedInsetX,
            sourceRect.yMin + appliedInsetY,
            sourceRect.xMax - appliedInsetX,
            sourceRect.yMax - appliedInsetY);
    }
}
