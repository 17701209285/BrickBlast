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

[DisallowMultipleComponent]
public class BallVolleyController : MonoBehaviour
{
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
    private LevelSettlementResult lastSettlementResult;

    public int CurrentBallCount => currentBallCount;
    public bool IsVolleyActive => volleyActive;
    public float PreviewCollisionRadius => GetCollisionRadius();
    public float PreviewCollisionTolerance => Mathf.Max(0.01f, CollisionSkin);
    public LevelSettlementResult LastSettlementResult => lastSettlementResult;

    private void Awake()
    {
        currentBallCount = Mathf.Max(1, InitialBallCount);
        EnsureDependencies();
        WarmupProjectilePool();
        CacheLaunchBallGraphic();
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

        PruneInactiveProjectiles();

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

        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = activeProjectiles[i];
            if (projectile == null || !projectile.IsFlying)
            {
                continue;
            }

            projectile.Tick(deltaTime);
        }

        PruneInactiveProjectiles();
    }

    public void SetBallCount(int ballCount)
    {
        currentBallCount = Mathf.Max(1, ballCount);
        ScheduleProjectilePoolWarmup();
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

        if (volleyActive && pendingLaunchCount <= 0 && !HasFlyingProjectiles())
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

        currentBallCount += ballCountDelta;
        ScheduleProjectilePoolWarmup();
        RefreshLaunchBallCountLabel();
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
        pendingLaunchCount = Mathf.Max(1, currentBallCount);
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
        SyncActiveProjectileCount();
    }

    private void CompleteVolley()
    {
        volleyActive = false;

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
    }

    private void StopVolleyImmediately()
    {
        volleyActive = false;
        pendingLaunchCount = 0;
        activeProjectileCount = 0;
        aimUnlockTimer = 0f;

        projectilePool?.ReleaseAll();
        activeProjectiles.Clear();

        SetLaunchBallVisible(true);
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
        SyncActiveProjectileCount();
    }

    private void ReleaseActiveProjectile(BallProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        var removed = activeProjectiles.Remove(projectile);
        projectilePool?.Release(projectile);
        if (removed)
        {
            SyncActiveProjectileCount();
        }
    }

    private void PruneInactiveProjectiles()
    {
        var removedAny = false;
        for (int i = activeProjectiles.Count - 1; i >= 0; i--)
        {
            var projectile = activeProjectiles[i];
            if (projectile != null && projectile.IsFlying)
            {
                continue;
            }

            activeProjectiles.RemoveAt(i);
            removedAny = true;
        }

        if (removedAny)
        {
            SyncActiveProjectileCount();
        }
    }

    private bool HasFlyingProjectiles()
    {
        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            var projectile = activeProjectiles[i];
            if (projectile != null && projectile.IsFlying)
            {
                return true;
            }
        }

        return false;
    }

    private void SyncActiveProjectileCount()
    {
        activeProjectileCount = activeProjectiles.Count;
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
            canTriggerSplitSpecial);
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
                Mathf.Max(1, currentBallCount) * BallShootingConstants.ProjectileWarmupMultiplier));
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

        LaunchBallCountLabel.text = currentBallCount.ToString();
        LaunchBallCountLabel.raycastTarget = false;
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

        if (ResultWindow != null)
        {
            ConfigureAndShowResultWindow();
            return;
        }

        if (!isResultWindowLoading)
        {
            StartCoroutine(LoadResultWindowRoutine());
        }
    }

    private IEnumerator LoadResultWindowRoutine()
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

        ConfigureAndShowResultWindow();
        isResultWindowLoading = false;
    }

    private void ConfigureAndShowResultWindow()
    {
        if (ResultWindow == null)
        {
            return;
        }

        ResultWindow.transform.SetAsLastSibling();
        ResultWindow.Show(pendingResultIsVictory, LevelLoader != null && LevelLoader.CanLoadNextLevel(), HandleResultPrimaryButtonClicked);
    }

    private void HandleResultPrimaryButtonClicked()
    {
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
