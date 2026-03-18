using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class BallVolleyController : MonoBehaviour
{
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
    [Min(1)]
    private int InitialBallCount = 60;

    [SerializeField]
    [Min(100f)]
    private float BallSpeed = 2600f;

    [SerializeField]
    [Min(0f)]
    private float LaunchInterval = 0.02f;

    [SerializeField]
    [Range(0.6f, 1f)]
    private float BallCollisionRadiusScale = 1f;

    [SerializeField]
    [Min(0.01f)]
    private float CollisionSkin = 1f;

    [SerializeField]
    [Min(0.001f)]
    private float SimulationStep = 1f / 90f;

    [SerializeField]
    [Range(1, 16)]
    private int MaxCollisionsPerStep = 6;

    [SerializeField]
    [Min(0.5f)]
    private float FallbackSubstepDistance = 6f;

    [SerializeField]
    private bool MoveBoardDownAfterVolley = true;

    private readonly List<BallProjectile> projectilePool = new List<BallProjectile>();
    private readonly List<BallProjectile> activeProjectiles = new List<BallProjectile>(64);

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

    public int CurrentBallCount => currentBallCount;
    public bool IsVolleyActive => volleyActive;

    private void Awake()
    {
        EnsureDependencies();
        currentBallCount = Mathf.Max(1, InitialBallCount);
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
        RefreshLaunchBallCountLabel();
    }

    public void NotifyProjectileReturned(BallProjectile projectile, Vector2 landingPoint)
    {
        if (projectile != null)
        {
            projectile.ReturnToPool();
            activeProjectiles.Remove(projectile);
        }

        activeProjectileCount = Mathf.Max(0, activeProjectileCount - 1);

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

    private void HandleAimReleased(Vector2 originLocalPosition, Vector2 aimDirection)
    {
        if (volleyActive || AimLinePresenter == null || ChessBoard == null || ChessBoard.IsGameOver)
        {
            return;
        }

        BeginVolley(originLocalPosition, aimDirection);
    }

    private void BeginVolley(Vector2 originLocalPosition, Vector2 aimDirection)
    {
        EnsureDependencies();
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
        var projectile = GetOrCreateProjectile();
        if (projectile == null)
        {
            return;
        }

        projectile.Launch(
            this,
            ChessBoard,
            GetSimulationSpace(),
            launchOrigin,
            launchDirection,
            BallSpeed,
            GetCollisionRadius(),
            shotBounds,
            collectorY,
            CollisionSkin,
            SimulationStep,
            MaxCollisionsPerStep,
            FallbackSubstepDistance);
        activeProjectiles.Add(projectile);
        activeProjectileCount++;
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

        var aimLockDuration = 0f;
        if (MoveBoardDownAfterVolley && ChessBoard != null)
        {
            ChessBoard.MoveBoardDownOneRow();
            if (ChessBoard.IsGameOver)
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

        for (int i = 0; i < projectilePool.Count; i++)
        {
            projectilePool[i]?.ReturnToPool();
        }

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

        EnsureProjectileCanvas();
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

    private BallProjectile GetOrCreateProjectile()
    {
        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (projectilePool[i] != null && !projectilePool[i].IsFlying)
            {
                return projectilePool[i];
            }
        }

        if (LaunchBall == null)
        {
            return null;
        }

        var projectileObject = Instantiate(LaunchBall.gameObject, GetOrCreateProjectileContainer(), false);
        projectileObject.name = $"Projectile {projectilePool.Count + 1}";
        projectileObject.SetActive(false);

        var projectileCountLabel = projectileObject.transform.Find("Number");
        if (projectileCountLabel != null)
        {
            projectileCountLabel.gameObject.SetActive(false);
        }

        var projectileGraphic = projectileObject.GetComponent<Graphic>();
        if (projectileGraphic != null)
        {
            projectileGraphic.enabled = true;
            projectileGraphic.raycastTarget = false;
        }

        var projectile = projectileObject.GetComponent<BallProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<BallProjectile>();
        }

        projectilePool.Add(projectile);
        return projectile;
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
        return simulationSpace == null ? new Rect(-540f, -960f, 1080f, 1920f) : simulationSpace.rect;
    }

    private float GetBallRadius()
    {
        if (LaunchBall == null)
        {
            return 25f;
        }

        return Mathf.Min(LaunchBall.rect.width, LaunchBall.rect.height) * 0.5f;
    }

    private float GetCollisionRadius()
    {
        // 中文备注：视觉半径和碰撞半径拆开，
        // 可以避免“看起来还没碰到砖，脚本已经先反弹”的空气碰撞感。
        return GetBallRadius() * Mathf.Clamp(BallCollisionRadiusScale, 0.6f, 1f);
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
            return;
        }

        if (!volleyActive && aimUnlockTimer <= 0f)
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
}
