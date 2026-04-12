using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YooAsset;

public enum LevelSettlementResult
{
    None = 0,
    Victory = 1,
    Defeat = 2
}

[Serializable]
public sealed class DefeatContinueSettings
{
    // 中文：是否开启失败后的激励续关。
    // English: whether rewarded-continue after defeat is enabled.
    [SerializeField] private bool enabled = true;
    // 中文：续关时向上回退的可见行数，默认对标 Brick Blast 的 3 行。
    // English: how many visible rows are rewound on continue; defaults to Brick Blast's 3 rows.
    [SerializeField] [Min(1)] private int rollbackRows = 3;
    // 中文：激励视频在这个时间内还没准备好，就直接放行续关。
    // English: if the rewarded ad is still not ready within this timeout, the continue is granted directly.
    [SerializeField] [Min(0f)] private float rewardedLoadFallbackSeconds = 6f;
    // 中文：紫球当前先做成“续关后下一轮球”的临时外观色。
    // English: purple balls are currently represented as a temporary tint for the next volley after continue.
    [SerializeField] private Color purpleBallTint = new Color(0.96f, 0.56f, 0.98f, 1f);

    public bool Enabled => enabled;
    public int RollbackRows => Mathf.Max(1, rollbackRows);
    public float RewardedLoadFallbackSeconds => Mathf.Max(0f, rewardedLoadFallbackSeconds);
    public Color PurpleBallTint => purpleBallTint;
}

[DisallowMultipleComponent]
public class BallVolleyController : MonoBehaviour
{
    private enum DefeatContinueRequestState
    {
        Idle = 0,
        WaitingForRewardedLoad = 1,
        ShowingRewarded = 2
    }

    public event Action<LevelSettlementResult> LevelSettlementTriggered;

    [SerializeField]
    private AimLinePresenter AimLinePresenter;

    [SerializeField]
    private UIChessBoard ChessBoard;

    [SerializeField]
    private RectTransform LaunchBall;

    [SerializeField]
    private RectTransform ProjectileContainer;

    [SerializeField]
    private TextMeshProUGUI LaunchBallCountLabel;

    [SerializeField]
    private UIResultWindow ResultWindow;

    [SerializeField]
    private YooAssetLevelLoader LevelLoader;

    [SerializeField]
    private RectTransform ResultWindowParent;

    [SerializeField]
    private string ResultWindowAddress = "Assets/AssetBundle/Prefabs/UIResultWindow.prefab";

    [SerializeField]
    private LevelPlayAdsManager AdsManager;

    [SerializeField]
    private DefeatContinueSettings DefeatContinue = new DefeatContinueSettings();

    [SerializeField]
    [Min(0f)]
    private float VictoryResultDelaySeconds = 1f;

    [SerializeField]
    [Min(1)]
    private int InitialBallCount = 60;

    [SerializeField]
    [Min(100f)]
    private float BallSpeed = BallShootingConstants.DefaultBallSpeed;

    [SerializeField]
    [Min(0f)]
    private float LaunchInterval = BallShootingConstants.DefaultLaunchInterval;

    [SerializeField]
    [Range(5f, 85f)]
    private float SplitFanHalfAngle = BallShootingConstants.DefaultSplitFanHalfAngle;

    [SerializeField]
    [Range(0.6f, 1f)]
    private float BallCollisionRadiusScale = BallShootingConstants.DefaultCollisionRadiusScale;

    [SerializeField]
    [Min(0.01f)]
    private float CollisionSkin = BallShootingConstants.DefaultCollisionSkin;

    [SerializeField]
    [Min(0.001f)]
    private float SimulationStep = BallShootingConstants.DefaultSimulationStep;

    [SerializeField]
    [Range(1, 16)]
    private int MaxCollisionsPerStep = BallShootingConstants.DefaultMaxCollisionsPerStep;

    [SerializeField]
    [Min(0.5f)]
    private float FallbackSubstepDistance = BallShootingConstants.DefaultFallbackSubstepDistance;

    [SerializeField]
    [Min(100f)]
    private float RecallSpeed = 4200f;

    [SerializeField]
    [Min(1)]
    private int MaxRuntimeProjectileCount = BallShootingConstants.DefaultMaxRuntimeProjectileCount;

    [SerializeField]
    private bool MoveBoardDownAfterVolley = true;

    private readonly List<BallProjectile> activeProjectiles = new List<BallProjectile>(64);

    private BallProjectilePool projectilePool;
    private Graphic launchBallGraphic;
    private bool isAimSubscribed;
    private bool isBoardStateSubscribed;
    private bool volleyActive;
    private int currentBallCount;
    private int pendingExtraBallCount;
    private int pendingLaunchCount;
    private int activeProjectileCount;
    private float launchCooldown;
    private float aimUnlockTimer;
    private float collectorY;
    private Rect shotBounds;
    private Vector2 launchOrigin;
    private Vector2 launchDirection;
    private Vector2 firstLandingPoint;
    private bool hasRecordedFirstLanding;
    private bool projectilePoolWarmupPending;
    private AssetHandle resultWindowPrefabHandle;
    private bool isResultWindowLoading;
    private bool pendingResultIsVictory;
    private bool isAdsSubscribed;
    private bool defeatContinueUsedThisLevel;
    private bool pendingPurpleBallVolley;
    private bool currentVolleyUsesPurpleBalls;
    private bool defeatContinueRewardGranted;
    private bool defeatContinueAnimationInProgress;
    private LevelSettlementResult lastSettlementResult;
    private DefeatContinueRequestState defeatContinueRequestState;
    private Coroutine defeatContinueFallbackCoroutine;
    private Coroutine defeatContinueAnimationCoroutine;
    private Coroutine victoryResultDelayCoroutine;
    private Color defaultLaunchBallTint = Color.white;

    public int CurrentBallCount => GetNextVolleyBallCount();
    public bool IsVolleyActive => volleyActive;
    public float PreviewCollisionTolerance => Mathf.Max(0.01f, CollisionSkin);
    public LevelSettlementResult LastSettlementResult => lastSettlementResult;

    private void Awake()
    {
        currentBallCount = Mathf.Max(1, InitialBallCount);
        EnsureDependencies();
        WarmupProjectilePool();
        CacheLaunchBallGraphic();
        RefreshLaunchBallVisual();
        RefreshLaunchBallCountLabel();
        SetLaunchBallVisible(true);
        SetLaunchBallCountVisible(true);
    }

    private void OnEnable()
    {
        EnsureDependencies();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        CancelDefeatContinueFlow(resetButtonState: false);
        StopDefeatContinueAnimation();
        StopVictoryResultDelay();
        StopVolleyImmediately();
    }

    private void OnDestroy()
    {
        ReleaseHandle(ref resultWindowPrefabHandle);
    }

    private void Update()
    {
        var deltaTime = Time.deltaTime;
        if (aimUnlockTimer > 0f)
        {
            aimUnlockTimer = Mathf.Max(0f, aimUnlockTimer - deltaTime);
            if (aimUnlockTimer <= 0f)
            {
                if (ChessBoard == null || !ChessBoard.IsGameOver)
                {
                    AimLinePresenter?.SetAimInputEnabled(true);
                    SetLaunchBallCountVisible(true);
                }
            }
        }

        if (!volleyActive)
        {
            FlushProjectilePoolWarmupIfIdle();
            return;
        }

        if (pendingLaunchCount > 0)
        {
            launchCooldown -= deltaTime;
            while (pendingLaunchCount > 0 && launchCooldown <= 0f)
            {
                LaunchSingleBall();
                pendingLaunchCount--;
                launchCooldown += LaunchInterval;
            }
        }

        ChessBoard?.RefreshCollisionCandidates(GetSimulationSpace());

        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = activeProjectiles[i];
            if (projectile == null || !projectile.IsFlying)
            {
                activeProjectiles.RemoveAt(i);
                continue;
            }

            projectile.Tick(deltaTime);
        }
    }

    public void SetBallCount(int ballCount)
    {
        currentBallCount = Mathf.Max(1, ballCount);
        pendingExtraBallCount = 0;
        pendingPurpleBallVolley = false;
        currentVolleyUsesPurpleBalls = false;
        ScheduleProjectilePoolWarmup();
        RefreshLaunchBallVisual();
        RefreshLaunchBallCountLabel();
    }

    public void NotifyProjectileReturned(BallProjectile projectile, Vector2 landingPoint)
    {
        ReleaseActiveProjectile(projectile);

        // 中文备注：下一回合的起点取“本轮第一个落回底线的球”，这里记录的就是那个位置。
        if (!hasRecordedFirstLanding)
        {
            hasRecordedFirstLanding = true;
            firstLandingPoint = new Vector2(
                Mathf.Clamp(landingPoint.x, shotBounds.xMin + GetBallRadius(), shotBounds.xMax - GetBallRadius()),
                collectorY);
        }

        if (volleyActive && pendingLaunchCount <= 0 && activeProjectileCount <= 0)
        {
            CompleteVolley();
        }
    }

    public void HandleSplitTrigger(BallProjectile sourceProjectile, Vector2 splitOriginLocalPosition, Vector2 baseDirection)
    {
        if (sourceProjectile == null)
        {
            return;
        }

        if (!volleyActive)
        {
            ReleaseActiveProjectile(sourceProjectile);
            return;
        }

        var splitPlan = BallSplitSpawnPlanner.CreatePlan(activeProjectileCount, MaxRuntimeProjectileCount, SplitFanHalfAngle, baseDirection);
        if (splitPlan.ReuseSourceProjectile)
        {
            sourceProjectile.Launch(CreateLaunchData(splitOriginLocalPosition, splitPlan.GetDirection(0), false));
            return;
        }

        ReleaseActiveProjectile(sourceProjectile);
        for (int i = 0; i < splitPlan.DirectionCount; i++)
        {
            LaunchSplitProjectile(splitOriginLocalPosition, splitPlan.GetDirection(i));
        }
    }

    public void HandleRedirectTrigger(BallProjectile sourceProjectile, Vector2 redirectOriginLocalPosition, Vector2 redirectDirection)
    {
        if (sourceProjectile == null)
        {
            return;
        }

        if (!volleyActive)
        {
            ReleaseActiveProjectile(sourceProjectile);
            return;
        }

        sourceProjectile.Launch(CreateLaunchData(redirectOriginLocalPosition, redirectDirection, sourceProjectile.CanTriggerSplitSpecial));
    }

    public void AddBallCount(int ballCountDelta)
    {
        if (ballCountDelta <= 0)
        {
            return;
        }

        pendingExtraBallCount += ballCountDelta;
        ScheduleProjectilePoolWarmup();
        RefreshLaunchBallCountLabel();
    }

    public void RecallAllProjectilesAndAdvanceTurn()
    {
        if (!volleyActive)
        {
            return;
        }

        pendingLaunchCount = 0;
        launchCooldown = 0f;
        var recallTarget = GetRecallLandingPoint();
        var hasFlyingProjectiles = false;

        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = activeProjectiles[i];
            if (projectile == null)
            {
                activeProjectiles.RemoveAt(i);
                continue;
            }

            if (!projectile.IsFlying)
            {
                activeProjectiles.RemoveAt(i);
                continue;
            }

            projectile.RecallTo(recallTarget, RecallSpeed);
            hasFlyingProjectiles = true;
        }

        activeProjectileCount = hasFlyingProjectiles ? activeProjectiles.Count : 0;
        if (hasFlyingProjectiles)
        {
            return;
        }

        firstLandingPoint = recallTarget;
        hasRecordedFirstLanding = true;
        CompleteVolley();
    }

    private void HandleAimReleased(Vector2 originLocalPosition, Vector2 aimDirection)
    {
        if (volleyActive || IsLevelTransitionLoading() || IsResultWindowVisible() || AimLinePresenter == null || ChessBoard == null || ChessBoard.IsGameOver)
        {
            return;
        }

        BeginVolley(originLocalPosition, aimDirection);
    }

    private void BeginVolley(Vector2 originLocalPosition, Vector2 aimDirection)
    {
        EnsureDependencies();
        WarmupProjectilePool();
        projectilePoolWarmupPending = false;
        if (LaunchBall == null || (ChessBoard != null && ChessBoard.IsGameOver))
        {
            return;
        }

        launchOrigin = originLocalPosition;
        launchDirection = aimDirection.normalized;
        collectorY = originLocalPosition.y;
        shotBounds = AimLinePresenter != null ? AimLinePresenter.GetShotBounds() : GetFallbackBounds();
        firstLandingPoint = originLocalPosition;
        hasRecordedFirstLanding = false;
        currentVolleyUsesPurpleBalls = pendingPurpleBallVolley;
        pendingPurpleBallVolley = false;
        // 中文：额外球只吃“下一回合”一次，这里在真正开球时统一结算并清空。
        // English: extra balls apply to the next volley only, so they are consumed here when the volley actually starts.
        pendingLaunchCount = Mathf.Max(1, GetNextVolleyBallCount());
        pendingExtraBallCount = 0;
        activeProjectileCount = 0;
        launchCooldown = 0f;
        aimUnlockTimer = 0f;
        volleyActive = true;

        ChessBoard?.RefreshCollisionCandidates(GetSimulationSpace());

        SetLaunchBallVisible(false);
        SetLaunchBallCountVisible(false);
        AimLinePresenter?.SetAimInputEnabled(false);
    }

    private void LaunchSingleBall()
    {
        var projectile = AcquireProjectile();
        if (projectile == null)
        {
            return;
        }

        projectile.Launch(CreateLaunchData(launchOrigin, launchDirection, true));
        activeProjectiles.Add(projectile);
        activeProjectileCount++;
    }

    private void CompleteVolley()
    {
        volleyActive = false;
        currentVolleyUsesPurpleBalls = false;

        if (!hasRecordedFirstLanding)
        {
            firstLandingPoint = launchOrigin;
        }

        MoveLaunchBallTo(firstLandingPoint);
        SetLaunchBallVisible(true);
        RefreshLaunchBallCountLabel();

        ChessBoard?.ClearTouchedSpecialItemsAtTurnEnd();

        var aimLockDuration = 0f;
        if (TryHandleLevelCompleted())
        {
            return;
        }

        if (MoveBoardDownAfterVolley && ChessBoard != null)
        {
            // 中文：下压前记录一份可见盘面快照，失败续关时再按回退行数恢复。
            // English: capture the visible board before shifting down so continue can restore it later.
            ChessBoard.CaptureContinueSnapshot();
            ChessBoard.MoveBoardDownOneRow();
            if (ChessBoard.IsGameOver)
            {
                return;
            }

            if (TryHandleLevelCompleted())
            {
                return;
            }

            aimLockDuration = ChessBoard.GetPredictedDropAnimationDuration(1);
        }

        if (aimLockDuration > 0f)
        {
            aimUnlockTimer = aimLockDuration;
            SetLaunchBallCountVisible(false);
            return;
        }

        SetLaunchBallCountVisible(true);
        AimLinePresenter?.SetAimInputEnabled(true);
        RefreshLaunchBallVisual();
    }

    private void StopVolleyImmediately()
    {
        volleyActive = false;
        pendingLaunchCount = 0;
        activeProjectileCount = 0;
        aimUnlockTimer = 0f;
        currentVolleyUsesPurpleBalls = false;

        projectilePool?.ReleaseAll();
        activeProjectiles.Clear();

        SetLaunchBallVisible(true);
        RefreshLaunchBallVisual();
        RefreshLaunchBallCountLabel();
        SetLaunchBallCountVisible(true);

        if (ChessBoard == null || !ChessBoard.IsGameOver)
        {
            AimLinePresenter?.SetAimInputEnabled(true);
        }
    }

    private void EnsureDependencies()
    {
        if (AimLinePresenter == null)
        {
            AimLinePresenter = GetComponent<AimLinePresenter>();
        }

        if (ChessBoard == null)
        {
            ChessBoard = GetComponent<UIChessBoard>();
        }

        if (LaunchBall == null)
        {
            if (AimLinePresenter != null && AimLinePresenter.AimOriginTransform != null)
            {
                LaunchBall = AimLinePresenter.AimOriginTransform;
            }
            else
            {
                LaunchBall = transform.Find("Ball") as RectTransform;
            }
        }

        if (ProjectileContainer == null)
        {
            ProjectileContainer = transform.Find("Ball Runtime") as RectTransform;
        }

        if (LaunchBallCountLabel == null && LaunchBall != null)
        {
            var labelTransform = LaunchBall.Find("Number");
            if (labelTransform != null)
            {
                LaunchBallCountLabel = labelTransform.GetComponent<TextMeshProUGUI>();
            }

            if (LaunchBallCountLabel == null)
            {
                LaunchBallCountLabel = LaunchBall.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (ResultWindowParent == null)
        {
            var parentCanvas = GetComponentInParent<Canvas>();
            ResultWindowParent = parentCanvas == null ? transform as RectTransform : parentCanvas.transform as RectTransform;
        }

        if (LevelLoader == null)
        {
            LevelLoader = GetComponent<YooAssetLevelLoader>();
        }

        if (AdsManager == null)
        {
            AdsManager = LevelPlayAdsManager.Instance;
        }

        EnsureProjectileCanvas();
        EnsureProjectilePool();
    }

    private void Subscribe()
    {
        if (!isAimSubscribed && AimLinePresenter != null)
        {
            AimLinePresenter.AimReleased += HandleAimReleased;
            isAimSubscribed = true;
        }

        if (!isBoardStateSubscribed && ChessBoard != null)
        {
            ChessBoard.GameOverStateChanged += HandleChessBoardGameOverStateChanged;
            isBoardStateSubscribed = true;
            HandleChessBoardGameOverStateChanged(ChessBoard.IsGameOver);
        }

        SubscribeAds();
    }

    private void Unsubscribe()
    {
        if (isAimSubscribed && AimLinePresenter != null)
        {
            AimLinePresenter.AimReleased -= HandleAimReleased;
            isAimSubscribed = false;
        }

        if (isBoardStateSubscribed && ChessBoard != null)
        {
            ChessBoard.GameOverStateChanged -= HandleChessBoardGameOverStateChanged;
            isBoardStateSubscribed = false;
        }

        UnsubscribeAds();
    }

    private BallProjectile AcquireProjectile()
    {
        if (projectilePool == null)
        {
            return null;
        }

        return projectilePool.Acquire();
    }

    private void EnsureProjectilePool()
    {
        if (projectilePool != null || LaunchBall == null)
        {
            return;
        }

        projectilePool = new BallProjectilePool(LaunchBall.gameObject, GetOrCreateProjectileContainer());
    }

    private void LaunchSplitProjectile(Vector2 originLocalPosition, Vector2 direction)
    {
        var projectile = AcquireProjectile();
        if (projectile == null)
        {
            return;
        }

        projectile.Launch(CreateLaunchData(originLocalPosition, direction, false));
        activeProjectiles.Add(projectile);
        activeProjectileCount++;
    }

    private void ReleaseActiveProjectile(BallProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        projectilePool?.Release(projectile);
        activeProjectiles.Remove(projectile);
        activeProjectileCount = Mathf.Max(0, activeProjectileCount - 1);
    }

    private RectTransform GetOrCreateProjectileContainer()
    {
        if (ProjectileContainer != null)
        {
            EnsureProjectileCanvas();
            return ProjectileContainer;
        }

        var containerObject = new GameObject("Ball Runtime", typeof(RectTransform));
        ProjectileContainer = containerObject.GetComponent<RectTransform>();
        ProjectileContainer.SetParent(GetSimulationSpace(), false);
        ProjectileContainer.SetAsLastSibling();
        ProjectileContainer.anchorMin = Vector2.zero;
        ProjectileContainer.anchorMax = Vector2.one;
        ProjectileContainer.offsetMin = Vector2.zero;
        ProjectileContainer.offsetMax = Vector2.zero;
        ProjectileContainer.pivot = new Vector2(0.5f, 0.5f);
        EnsureProjectileCanvas();
        return ProjectileContainer;
    }

    private void EnsureProjectileCanvas()
    {
        if (ProjectileContainer == null)
        {
            return;
        }

        var containerCanvas = ProjectileContainer.GetComponent<Canvas>();
        if (containerCanvas == null)
        {
            containerCanvas = ProjectileContainer.gameObject.AddComponent<Canvas>();
        }

        // 中文备注：球在独立子 Canvas 里移动时，只会重建这一小块 UI，
        // 不会把整张棋盘和其它界面一起拖进 Canvas Rebuild。
        var parentCanvas = ProjectileContainer.GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            containerCanvas.renderMode = parentCanvas.renderMode;
            containerCanvas.worldCamera = parentCanvas.worldCamera;
            containerCanvas.planeDistance = parentCanvas.planeDistance;
            containerCanvas.sortingLayerID = parentCanvas.sortingLayerID;
            containerCanvas.sortingOrder = parentCanvas.sortingOrder;
            containerCanvas.targetDisplay = parentCanvas.targetDisplay;
        }

        containerCanvas.overrideSorting = false;
        containerCanvas.pixelPerfect = false;
    }

    private RectTransform GetSimulationSpace()
    {
        if (AimLinePresenter != null && AimLinePresenter.AimSpace != null)
        {
            return AimLinePresenter.AimSpace;
        }

        return LaunchBall == null ? transform as RectTransform : LaunchBall.parent as RectTransform;
    }

    private Rect GetFallbackBounds()
    {
        var simulationSpace = GetSimulationSpace();
        if (simulationSpace != null)
        {
            return simulationSpace.rect;
        }

        return new Rect(
            -BallShootingConstants.DefaultFallbackWidth * 0.5f,
            -BallShootingConstants.DefaultFallbackHeight * 0.5f,
            BallShootingConstants.DefaultFallbackWidth,
            BallShootingConstants.DefaultFallbackHeight);
    }

    private float GetBallRadius()
    {
        if (LaunchBall == null)
        {
            return BallShootingConstants.DefaultBallRadius;
        }

        return Mathf.Min(LaunchBall.rect.width, LaunchBall.rect.height) * 0.5f;
    }

    private float GetCollisionRadius()
    {
        // 中文备注：视觉半径和碰撞半径拆开，
        // 可以避免“看起来还没碰到砖，脚本已经先反弹”的空气碰撞感。
        return GetBallRadius() * Mathf.Clamp(BallCollisionRadiusScale, 0.6f, 1f);
    }

    private BallProjectileLaunchData CreateLaunchData(Vector2 originLocalPosition, Vector2 direction, bool canTriggerSplitSpecial)
    {
        return new BallProjectileLaunchData(
            this,
            ChessBoard,
            GetSimulationSpace(),
            originLocalPosition,
            direction,
            BallSpeed,
            GetCollisionRadius(),
            shotBounds,
            collectorY,
            CollisionSkin,
            SimulationStep,
            MaxCollisionsPerStep,
            FallbackSubstepDistance,
            canTriggerSplitSpecial,
            GetCurrentVolleyBallTint());
    }

    private void WarmupProjectilePool()
    {
        projectilePool?.Warmup(GetProjectileWarmupCount());
    }

    private void ScheduleProjectilePoolWarmup()
    {
        if (volleyActive)
        {
            projectilePoolWarmupPending = true;
            return;
        }

        WarmupProjectilePool();
        projectilePoolWarmupPending = false;
    }

    private void FlushProjectilePoolWarmupIfIdle()
    {
        if (!projectilePoolWarmupPending)
        {
            return;
        }

        WarmupProjectilePool();
        projectilePoolWarmupPending = false;
    }

    private int GetProjectileWarmupCount()
    {
        return Mathf.Max(
            MaxRuntimeProjectileCount,
            Mathf.Max(
                BallShootingConstants.MinimumWarmProjectileCount,
                Mathf.Max(1, GetNextVolleyBallCount()) * BallShootingConstants.ProjectileWarmupMultiplier));
    }

    private void MoveLaunchBallTo(Vector2 localPosition)
    {
        if (LaunchBall != null)
        {
            LaunchBall.anchoredPosition = localPosition;
        }
    }

    private void CacheLaunchBallGraphic()
    {
        if (LaunchBall == null)
        {
            return;
        }

        launchBallGraphic = LaunchBall.GetComponent<Graphic>();
        if (launchBallGraphic != null)
        {
            defaultLaunchBallTint = launchBallGraphic.color;
        }
    }

    private void HandleChessBoardGameOverStateChanged(bool isGameOver)
    {
        if (isGameOver)
        {
            pendingLaunchCount = 0;
            aimUnlockTimer = 0f;

            if (!volleyActive)
            {
                SetLaunchBallVisible(true);
                RefreshLaunchBallCountLabel();
                SetLaunchBallCountVisible(true);
            }

            AimLinePresenter?.SetAimInputEnabled(false);
            OnBottomRowGameOver();
            ShowResultWindow(false);
            return;
        }

        if (defeatContinueAnimationInProgress)
        {
            return;
        }

        if (!volleyActive && aimUnlockTimer <= 0f && !IsResultWindowVisible() && !IsLevelTransitionLoading())
        {
            SetLaunchBallVisible(true);
            RefreshLaunchBallCountLabel();
            SetLaunchBallCountVisible(true);
            AimLinePresenter?.SetAimInputEnabled(true);
        }
    }

    protected virtual void OnBottomRowGameOver()
    {
        var bottomRowIndex = ChessBoard == null ? -1 : ChessBoard.BottomRowIndex;
        Debug.Log($"[BallVolleyController] Game over: a brick reached the last row ({bottomRowIndex}). Launching is now disabled.", this);
    }

    private void RefreshLaunchBallCountLabel()
    {
        if (LaunchBallCountLabel == null)
        {
            return;
        }

        LaunchBallCountLabel.text = GetNextVolleyBallCount().ToString();
        LaunchBallCountLabel.raycastTarget = false;
    }

    private void RefreshLaunchBallVisual()
    {
        if (launchBallGraphic == null)
        {
            CacheLaunchBallGraphic();
        }

        if (launchBallGraphic == null)
        {
            return;
        }

        // 中文：紫球先做成“下一回合的临时球态”，只影响这一轮的球外观。
        // English: the purple-ball rescue is implemented as a temporary next-volley ball state
        // that only affects the look of that volley's balls for now.
        launchBallGraphic.color = pendingPurpleBallVolley ? DefeatContinue.PurpleBallTint : defaultLaunchBallTint;
    }

    private int GetNextVolleyBallCount()
    {
        return Mathf.Max(1, currentBallCount + pendingExtraBallCount);
    }

    private Color GetCurrentVolleyBallTint()
    {
        return currentVolleyUsesPurpleBalls ? DefeatContinue.PurpleBallTint : defaultLaunchBallTint;
    }

    private Vector2 GetRecallLandingPoint()
    {
        var fallbackX = Mathf.Clamp(launchOrigin.x, shotBounds.xMin + GetBallRadius(), shotBounds.xMax - GetBallRadius());
        if (!hasRecordedFirstLanding)
        {
            return new Vector2(fallbackX, collectorY);
        }

        return new Vector2(
            Mathf.Clamp(firstLandingPoint.x, shotBounds.xMin + GetBallRadius(), shotBounds.xMax - GetBallRadius()),
            collectorY);
    }

    private void SetLaunchBallCountVisible(bool visible)
    {
        if (LaunchBallCountLabel == null)
        {
            return;
        }

        if (LaunchBallCountLabel.gameObject.activeSelf != visible)
        {
            LaunchBallCountLabel.gameObject.SetActive(visible);
        }
    }

    private void SetLaunchBallVisible(bool visible)
    {
        if (launchBallGraphic == null)
        {
            CacheLaunchBallGraphic();
        }

        if (launchBallGraphic != null)
        {
            launchBallGraphic.enabled = visible;
        }
    }

    private bool TryHandleLevelCompleted()
    {
        if (ChessBoard == null || !ChessBoard.IsLevelCompleted())
        {
            return false;
        }

        ShowResultWindow(true);
        return true;
    }

    private void ShowResultWindow(bool isVictory)
    {
        NotifyLevelSettlement(isVictory ? LevelSettlementResult.Victory : LevelSettlementResult.Defeat);
        pendingResultIsVictory = isVictory;
        aimUnlockTimer = 0f;
        AimLinePresenter?.SetAimInputEnabled(false);
        StopVictoryResultDelay();

        if (isVictory)
        {
            ChessBoard?.PlayGameOverEffect();
            if (ResultWindow == null && !isResultWindowLoading)
            {
                StartCoroutine(LoadResultWindowRoutine(showOnLoaded: false));
            }

            victoryResultDelayCoroutine = StartCoroutine(ShowVictoryResultWindowAfterDelay());
            return;
        }

        ShowResultWindowImmediate();
    }

    private void ShowResultWindowImmediate()
    {
        if (ResultWindow != null)
        {
            ConfigureAndShowResultWindow();
            return;
        }

        if (!isResultWindowLoading)
        {
            StartCoroutine(LoadResultWindowRoutine(showOnLoaded: true));
        }
    }

    private IEnumerator ShowVictoryResultWindowAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, VictoryResultDelaySeconds));
        victoryResultDelayCoroutine = null;
        ShowResultWindowImmediate();
    }

    private IEnumerator LoadResultWindowRoutine(bool showOnLoaded)
    {
        isResultWindowLoading = true;

        var package = ResolveResourcePackage();
        if (package == null)
        {
            Debug.LogError("[BallVolleyController] Can not load result window because YooAsset package is not ready.", this);
            isResultWindowLoading = false;
            yield break;
        }

        var handle = package.LoadAssetAsync<GameObject>(ResultWindowAddress);
        yield return handle;

        if (handle.Status != EOperationStatus.Succeed)
        {
            Debug.LogError("[BallVolleyController] Failed to load result window: " + handle.LastError, this);
            if (handle.IsValid)
            {
                handle.Release();
            }

            isResultWindowLoading = false;
            yield break;
        }

        var prefab = handle.GetAssetObject<GameObject>();
        if (prefab == null)
        {
            Debug.LogError("[BallVolleyController] Result window prefab is invalid: " + ResultWindowAddress, this);
            handle.Release();
            isResultWindowLoading = false;
            yield break;
        }

        ReleaseHandle(ref resultWindowPrefabHandle);
        resultWindowPrefabHandle = handle;

        var parent = ResolveResultWindowParent();
        var instance = Instantiate(prefab, parent, false);
        instance.transform.SetAsLastSibling();
        ResultWindow = instance.GetComponent<UIResultWindow>();
        if (ResultWindow == null)
        {
            Debug.LogError("[BallVolleyController] Result window prefab does not contain UIResultWindow.", instance);
            Destroy(instance);
            isResultWindowLoading = false;
            yield break;
        }

        isResultWindowLoading = false;
        if (showOnLoaded)
        {
            ConfigureAndShowResultWindow();
        }
    }

    private void ConfigureAndShowResultWindow()
    {
        if (ResultWindow == null)
        {
            return;
        }

        ResultWindow.transform.SetAsLastSibling();
        ResultWindow.Show(BuildResultWindowPresentation(), HandleResultPrimaryButtonClicked);
    }

    private void HandleResultPrimaryButtonClicked()
    {
        if (!pendingResultIsVictory && CanOfferDefeatContinue())
        {
            BeginDefeatContinueRequest();
            return;
        }

        if (IsLevelTransitionLoading())
        {
            return;
        }

        if (LevelLoader == null)
        {
            Debug.LogError("[BallVolleyController] Level loader is missing.", this);
            return;
        }

        ResultWindow?.SetPrimaryButtonInteractable(false);
        StopVolleyImmediately();

        if (pendingResultIsVictory && LevelLoader.CanLoadNextLevel())
        {
            LevelLoader.LoadNextLevel(HandleLevelTransitionCompleted);
            return;
        }

        LevelLoader.ReloadCurrentLevel(HandleLevelTransitionCompleted);
    }

    private void HandleLevelTransitionCompleted(bool succeeded)
    {
        if (!succeeded)
        {
            ResultWindow?.SetPrimaryButtonInteractable(true);
            return;
        }

        ResetDefeatContinueStateForCurrentLevel();
        if (ResultWindow != null)
        {
            ResultWindow.Hide();
        }

        lastSettlementResult = LevelSettlementResult.None;

        if (!volleyActive && aimUnlockTimer <= 0f && (ChessBoard == null || !ChessBoard.IsGameOver))
        {
            SetLaunchBallVisible(true);
            RefreshLaunchBallCountLabel();
            SetLaunchBallCountVisible(true);
            AimLinePresenter?.SetAimInputEnabled(true);
        }
    }

    private Transform ResolveResultWindowParent()
    {
        if (ResultWindowParent != null)
        {
            return ResultWindowParent;
        }

        var parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
        {
            ResultWindowParent = parentCanvas.transform as RectTransform;
            return ResultWindowParent;
        }

        return transform;
    }

    private ResourcePackage ResolveResourcePackage()
    {
        var runtime = YooAssetGameRuntime.Instance;
        if (runtime != null && runtime.Settings != null)
        {
            var configuredPackage = YooAssets.TryGetPackage(runtime.Settings.PackageName);
            if (configuredPackage != null)
            {
                return configuredPackage;
            }
        }

        return YooAssets.TryGetPackage("DefaultPackage");
    }

    private bool IsResultWindowVisible()
    {
        return ResultWindow != null && ResultWindow.IsVisible;
    }

    private bool IsLevelTransitionLoading()
    {
        return LevelLoader != null && LevelLoader.IsLoading;
    }

    private void NotifyLevelSettlement(LevelSettlementResult settlementResult)
    {
        if (settlementResult == LevelSettlementResult.None || lastSettlementResult == settlementResult)
        {
            return;
        }

        lastSettlementResult = settlementResult;
        LevelSettlementTriggered?.Invoke(settlementResult);
    }

    private UIResultWindowPresentation BuildResultWindowPresentation()
    {
        if (pendingResultIsVictory)
        {
            var canAdvance = LevelLoader != null && LevelLoader.CanLoadNextLevel();
            return new UIResultWindowPresentation("通关成功", canAdvance ? "下一关" : "重新开始");
        }

        // 中文：每关只允许一次失败续关，用掉以后结果窗直接回退到“重新开始”。
        // English: each level grants at most one defeat continue; once spent, the result window falls back to Restart.
        return new UIResultWindowPresentation("挑战失败", CanOfferDefeatContinue() ? "继续" : "重新开始");
    }

    private bool CanOfferDefeatContinue()
    {
        return DefeatContinue.Enabled && !defeatContinueUsedThisLevel;
    }

    private void BeginDefeatContinueRequest()
    {
        if (defeatContinueRequestState != DefeatContinueRequestState.Idle)
        {
            return;
        }

        EnsureDependencies();
        SubscribeAds();
        ResultWindow?.SetPrimaryButtonInteractable(false);
        ResultWindow?.SetPrimaryButtonLabel("加载中...");

        if (AdsManager == null)
        {
            Debug.LogWarning("[BallVolleyController] Rewarded ads manager is missing. Granting defeat continue immediately.", this);
            GrantDefeatContinue();
            return;
        }

        if (AdsManager.IsRewardedReady && TryShowDefeatContinueRewarded())
        {
            return;
        }

        // 中文：和 Brick Blast 一样，激励视频在限定时间内没准备好，就直接放行继续。
        // English: matching Brick Blast, if the rewarded ad does not become ready within the timeout,
        // we grant the continue directly instead of blocking the player.
        defeatContinueRequestState = DefeatContinueRequestState.WaitingForRewardedLoad;
        AdsManager.LoadRewardedAd();
        StartDefeatContinueFallbackTimer();
    }

    private bool TryShowDefeatContinueRewarded()
    {
        if (AdsManager == null || !AdsManager.IsRewardedReady)
        {
            return false;
        }

        if (!AdsManager.ShowRewardedAd())
        {
            return false;
        }

        defeatContinueRequestState = DefeatContinueRequestState.ShowingRewarded;
        StopDefeatContinueFallbackTimer();
        ResultWindow?.SetPrimaryButtonLabel("观看中...");
        return true;
    }

    private void StartDefeatContinueFallbackTimer()
    {
        StopDefeatContinueFallbackTimer();
        defeatContinueFallbackCoroutine = StartCoroutine(DefeatContinueFallbackRoutine());
    }

    private void StopDefeatContinueFallbackTimer()
    {
        if (defeatContinueFallbackCoroutine == null)
        {
            return;
        }

        StopCoroutine(defeatContinueFallbackCoroutine);
        defeatContinueFallbackCoroutine = null;
    }

    private IEnumerator DefeatContinueFallbackRoutine()
    {
        var deadline = Time.unscaledTime + DefeatContinue.RewardedLoadFallbackSeconds;
        while (defeatContinueRequestState == DefeatContinueRequestState.WaitingForRewardedLoad && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        defeatContinueFallbackCoroutine = null;
        if (defeatContinueRequestState != DefeatContinueRequestState.WaitingForRewardedLoad)
        {
            yield break;
        }

        Debug.LogWarning("[BallVolleyController] Rewarded ad did not become ready in time. Granting defeat continue directly.", this);
        GrantDefeatContinue();
    }

    private void GrantDefeatContinue()
    {
        StopDefeatContinueFallbackTimer();
        defeatContinueRequestState = DefeatContinueRequestState.Idle;
        defeatContinueRewardGranted = false;

        // 中文：续关不是重开，而是把当前可见盘面回拨若干行后继续这一局。
        // English: a continue does not restart the stage; it rewinds the current visible board and resumes the same run.
        if (ChessBoard == null || !ChessBoard.HasContinueSnapshot())
        {
            Debug.LogError("[BallVolleyController] Defeat continue failed because no rollback snapshot was available.", this);
            ResultWindow?.SetPrimaryButtonInteractable(true);
            ResultWindow?.SetPrimaryButtonLabel("重新开始");
            return;
        }

        defeatContinueUsedThisLevel = true;
        // 中文：紫球状态只保留到“续关后的下一轮发射”为止，避免污染后续所有回合。
        // English: the purple-ball state is intentionally limited to the next volley after continue only.
        pendingPurpleBallVolley = true;
        RefreshLaunchBallVisual();

        if (ResultWindow != null)
        {
            ResultWindow.Hide();
        }

        StopDefeatContinueAnimation();
        defeatContinueAnimationCoroutine = StartCoroutine(PlayDefeatContinueAnimationRoutine());
    }

    private void CancelDefeatContinueFlow(bool resetButtonState)
    {
        StopDefeatContinueFallbackTimer();
        defeatContinueRequestState = DefeatContinueRequestState.Idle;
        defeatContinueRewardGranted = false;

        if (!resetButtonState || ResultWindow == null)
        {
            return;
        }

        ResultWindow.SetPrimaryButtonInteractable(true);
        ResultWindow.SetPrimaryButtonLabel(CanOfferDefeatContinue() ? "继续" : "重新开始");
    }

    private void StopDefeatContinueAnimation()
    {
        if (defeatContinueAnimationCoroutine == null)
        {
            return;
        }

        StopCoroutine(defeatContinueAnimationCoroutine);
        defeatContinueAnimationCoroutine = null;
        defeatContinueAnimationInProgress = false;
    }

    private void StopVictoryResultDelay()
    {
        if (victoryResultDelayCoroutine == null)
        {
            return;
        }

        StopCoroutine(victoryResultDelayCoroutine);
        victoryResultDelayCoroutine = null;
    }

    private IEnumerator PlayDefeatContinueAnimationRoutine()
    {
        defeatContinueAnimationInProgress = true;
        SetLaunchBallVisible(true);
        RefreshLaunchBallCountLabel();
        SetLaunchBallCountVisible(false);
        AimLinePresenter?.SetAimInputEnabled(false);

        Debug.Log($"[BallVolleyController] Defeat continue granted. Rolling back {DefeatContinue.RollbackRows} rows.", this);
        yield return StartCoroutine(ChessBoard.PlayContinueRestoreAnimation(DefeatContinue.RollbackRows));

        defeatContinueAnimationInProgress = false;
        defeatContinueAnimationCoroutine = null;
        lastSettlementResult = LevelSettlementResult.None;
        SetLaunchBallVisible(true);
        RefreshLaunchBallCountLabel();
        SetLaunchBallCountVisible(true);
        AimLinePresenter?.SetAimInputEnabled(true);
    }

    private void ResetDefeatContinueStateForCurrentLevel()
    {
        CancelDefeatContinueFlow(resetButtonState: false);
        // 中文：进入新关或重开时，清空本关的一次性续关资格和所有临时紫球状态。
        // English: a fresh attempt resets the one-time continue entitlement and all temporary purple-ball state.
        defeatContinueUsedThisLevel = false;
        pendingPurpleBallVolley = false;
        currentVolleyUsesPurpleBalls = false;
        RefreshLaunchBallVisual();
    }

    private void SubscribeAds()
    {
        if (isAdsSubscribed)
        {
            return;
        }

        if (AdsManager == null)
        {
            AdsManager = LevelPlayAdsManager.Instance;
        }

        if (AdsManager == null)
        {
            return;
        }

        AdsManager.RewardedLoaded += HandleRewardedLoaded;
        AdsManager.RewardedCompleted += HandleRewardedCompleted;
        AdsManager.RewardedClosed += HandleRewardedClosed;
        isAdsSubscribed = true;
    }

    private void UnsubscribeAds()
    {
        if (!isAdsSubscribed || AdsManager == null)
        {
            isAdsSubscribed = false;
            return;
        }

        AdsManager.RewardedLoaded -= HandleRewardedLoaded;
        AdsManager.RewardedCompleted -= HandleRewardedCompleted;
        AdsManager.RewardedClosed -= HandleRewardedClosed;
        isAdsSubscribed = false;
    }

    private void HandleRewardedLoaded()
    {
        if (defeatContinueRequestState != DefeatContinueRequestState.WaitingForRewardedLoad)
        {
            return;
        }

        // 中文：只要 6 秒兜底窗口内加载成功，就优先走正常观看广告的路径。
        // English: if the ad becomes ready before the fallback timeout, prefer the normal rewarded flow.
        TryShowDefeatContinueRewarded();
    }

    private void HandleRewardedCompleted()
    {
        if (defeatContinueRequestState != DefeatContinueRequestState.ShowingRewarded)
        {
            return;
        }

        // 中文：这里只标记“奖励资格已拿到”，真正续关在广告关闭后统一执行。
        // English: this only marks the reward as earned; the actual continue is finalized after the ad closes.
        defeatContinueRewardGranted = true;
        Debug.Log("[BallVolleyController] Rewarded ad reported completion for defeat continue.", this);
    }

    private void HandleRewardedClosed()
    {
        if (defeatContinueRequestState != DefeatContinueRequestState.ShowingRewarded)
        {
            return;
        }

        if (defeatContinueRewardGranted)
        {
            GrantDefeatContinue();
            return;
        }

        // 中文：如果广告没拿到奖励就不给续关，按钮状态恢复到当前结果窗默认动作。
        // English: if the ad closes without reward, continue is cancelled and the result window returns to its default action.
        Debug.LogWarning("[BallVolleyController] Rewarded ad closed without reward grant. Continue cancelled.", this);
        CancelDefeatContinueFlow(resetButtonState: true);
    }

    private static void ReleaseHandle(ref AssetHandle handle)
    {
        if (handle == null)
        {
            return;
        }

        if (handle.IsValid)
        {
            handle.Release();
        }

        handle = null;
    }
}
