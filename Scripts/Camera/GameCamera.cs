using Godot;

public partial class GameCamera : Camera3D
{
    public static GameCamera Instance { get; private set; }

    /// <summary>
    /// How far behind the cannon the camera sits.
    /// </summary>
    [Export] public float DistanceBehind { get; set; } = 6f;

    /// <summary>
    /// How high above the cannon the camera sits.
    /// </summary>
    [Export] public float HeightAbove { get; set; } = 12f;

    /// <summary>
    /// Duration of camera transition between cannons.
    /// </summary>
    [Export] public float TransitionDuration { get; set; } = 1.0f;

    /// <summary>
    /// Whether the camera is currently transitioning.
    /// </summary>
    public bool IsTransitioning { get; private set; }

    private Tween _activeTween;
    private Cannon _currentCannon;
    private bool _initialized;

    public override void _Ready()
    {
        Instance = this;

        // Defer initialization to ensure all singletons are ready
        CallDeferred(nameof(Initialize));
    }

    private void Initialize()
    {
        if (_initialized) return;

        // Connect to turn changed signal
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TurnChanged += OnTurnChanged;
        }

        // Set initial camera position
        SetInitialCameraPosition();
        _initialized = true;
    }

    private void SetInitialCameraPosition()
    {
        // Wait for CannonController to be ready
        if (CannonController.Instance == null)
        {
            // Try again next frame
            CallDeferred(nameof(SetInitialCameraPosition));
            return;
        }

        var player = GameManager.Instance?.CurrentPlayer ?? Player.X;
        var cannon = CannonController.Instance.GetActiveCannonForPlayer(player);

        if (cannon != null)
        {
            TransitionToCannon(cannon, instant: true);
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TurnChanged -= OnTurnChanged;
        }
    }

    private void OnTurnChanged(Player player)
    {
        // In PvCPU mode, camera stays on player's side (X)
        if (GameManager.CurrentGameMode == GameMode.PlayerVsCPU)
        {
            // Only transition to X's cannon, stay there for CPU turn
            if (_currentCannon == null)
            {
                var cannon = CannonController.Instance?.GetActiveCannonForPlayer(Player.X);
                if (cannon != null)
                {
                    TransitionToCannon(cannon, instant: true);
                }
            }
            return;
        }

        // In PvP mode, switch camera to active player's cannon
        var activePlayerCannon = CannonController.Instance?.GetActiveCannonForPlayer(player);
        if (activePlayerCannon != null && activePlayerCannon != _currentCannon)
        {
            TransitionToCannon(activePlayerCannon, instant: false);
        }
    }

    /// <summary>
    /// Transition camera to view from behind the specified cannon.
    /// </summary>
    public void TransitionToCannon(Cannon cannon, bool instant = false)
    {
        if (cannon == null) return;

        _currentCannon = cannon;

        // Calculate target position and rotation
        var cannonPos = cannon.GlobalPosition;
        var targetPos = CalculateCameraPosition(cannon);
        var targetLookAt = Vector3.Zero; // Look at the center of the board

        if (instant)
        {
            GlobalPosition = targetPos;
            LookAt(targetLookAt, Vector3.Up);
            return;
        }

        // Cancel any existing transition
        _activeTween?.Kill();
        IsTransitioning = true;

        // Create smooth transition
        _activeTween = CreateTween();
        _activeTween.SetParallel(true);
        _activeTween.SetEase(Tween.EaseType.InOut);
        _activeTween.SetTrans(Tween.TransitionType.Cubic);

        // Animate position
        _activeTween.TweenProperty(this, "global_position", targetPos, TransitionDuration);

        // Animate rotation (look direction)
        var currentBasis = GlobalTransform.Basis;
        var targetTransform = GlobalTransform.LookingAt(targetLookAt, Vector3.Up);
        var targetBasis = targetTransform.Basis;

        // We need to animate the rotation via quaternion for smooth interpolation
        var startQuat = currentBasis.GetRotationQuaternion();
        var endQuat = targetBasis.GetRotationQuaternion();

        _activeTween.TweenMethod(
            Callable.From<float>((t) => {
                var interpolated = startQuat.Slerp(endQuat, t);
                GlobalTransform = new Transform3D(new Basis(interpolated), GlobalPosition);
            }),
            0.0f,
            1.0f,
            TransitionDuration
        );

        // Mark transition complete when done
        _activeTween.SetParallel(false);
        _activeTween.TweenCallback(Callable.From(() => {
            IsTransitioning = false;
            // Final look adjustment to ensure accuracy
            LookAt(targetLookAt, Vector3.Up);
        }));
    }

    private Vector3 CalculateCameraPosition(Cannon cannon)
    {
        // Get cannon position but center X to 0 (center of cannon row)
        var cannonPos = cannon.GlobalPosition;
        cannonPos.X = 0; // Center camera on the row of cannons, not individual cannon

        var directionToCenter = (Vector3.Zero - cannonPos);
        directionToCenter.Y = 0; // Keep horizontal
        directionToCenter = directionToCenter.Normalized();

        // Camera is positioned behind the cannon row (opposite of direction to center)
        // and elevated above
        var cameraPos = cannonPos - directionToCenter * DistanceBehind;
        cameraPos.Y = cannon.GlobalPosition.Y + HeightAbove;

        return cameraPos;
    }

    /// <summary>
    /// Instantly position camera for initial scene setup.
    /// </summary>
    public void SetInitialPosition(Player player)
    {
        var cannon = CannonController.Instance?.GetActiveCannonForPlayer(player);
        if (cannon != null)
        {
            TransitionToCannon(cannon, instant: true);
        }
    }
}
