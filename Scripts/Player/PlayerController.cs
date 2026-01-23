using Godot;
using System;

/// <summary>
/// Third-person player character controller for navigating the ship deck.
/// Handles movement, camera-relative controls, and interaction with ship objects.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    // Movement settings
    [Export] public float WalkSpeed { get; set; } = 4f;
    [Export] public float SprintSpeed { get; set; } = 7f;
    [Export] public float Acceleration { get; set; } = 10f;
    [Export] public float Deceleration { get; set; } = 15f;
    [Export] public float RotationSpeed { get; set; } = 10f;

    // Jump/gravity
    [Export] public float JumpVelocity { get; set; } = 4.5f;
    [Export] public float Gravity { get; set; } = 9.8f;

    // Interaction
    [Export] public float InteractionRange { get; set; } = 2f;

    // References
    private Node3D _cameraTarget;  // What the camera follows
    private PlayerCamera _camera;
    private InteractionSystem _interactionSystem;
    private Ship _currentShip;

    // State
    private bool _isSprinting = false;
    private bool _isInteracting = false;
    private Vector3 _moveDirection = Vector3.Zero;

    [Signal]
    public delegate void InteractionStartedEventHandler(InteractionPoint point);

    [Signal]
    public delegate void InteractionEndedEventHandler();

    public override void _Ready()
    {
        // Create camera target node (camera follows this)
        _cameraTarget = new Node3D();
        _cameraTarget.Name = "CameraTarget";
        AddChild(_cameraTarget);
        _cameraTarget.Position = new Vector3(0, 1.5f, 0); // Eye level

        // Find or create camera
        _camera = GetNodeOrNull<PlayerCamera>("PlayerCamera");

        // Create interaction system
        _interactionSystem = new InteractionSystem();
        _interactionSystem.InteractionRange = InteractionRange;
        AddChild(_interactionSystem);

        // Find parent ship
        FindParentShip();
    }

    private void FindParentShip()
    {
        var parent = GetParent();
        while (parent != null)
        {
            if (parent is Ship ship)
            {
                _currentShip = ship;
                break;
            }
            parent = parent.GetParent();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_isInteracting)
        {
            // Handle interaction mode input
            HandleInteractionInput();
            return;
        }

        // Handle movement
        HandleMovement((float)delta);

        // Apply gravity
        ApplyGravity((float)delta);

        // Move the character
        MoveAndSlide();
    }

    public override void _Input(InputEvent @event)
    {
        // Handle interaction key
        if (@event.IsActionPressed("interact"))
        {
            TryInteract();
        }

        // Handle sprint toggle
        if (@event.IsActionPressed("sprint"))
        {
            _isSprinting = true;
        }
        if (@event.IsActionReleased("sprint"))
        {
            _isSprinting = false;
        }

        // Exit interaction
        if (_isInteracting && @event.IsActionPressed("ui_cancel"))
        {
            EndInteraction();
        }
    }

    private void HandleMovement(float delta)
    {
        // Get input direction
        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

        if (inputDir.LengthSquared() > 0.01f)
        {
            // Get camera-relative direction
            var cameraTransform = _camera?.GlobalTransform ?? GlobalTransform;
            var forward = -cameraTransform.Basis.Z;
            var right = cameraTransform.Basis.X;

            // Flatten to horizontal plane
            forward.Y = 0;
            forward = forward.Normalized();
            right.Y = 0;
            right = right.Normalized();

            // Calculate move direction
            _moveDirection = (forward * -inputDir.Y + right * inputDir.X).Normalized();

            // Rotate character to face movement direction
            RotateTowardDirection(_moveDirection, delta);
        }
        else
        {
            _moveDirection = Vector3.Zero;
        }

        // Calculate target velocity
        float speed = _isSprinting ? SprintSpeed : WalkSpeed;
        Vector3 targetVelocity = _moveDirection * speed;

        // Smoothly interpolate velocity
        var velocity = Velocity;
        if (_moveDirection.LengthSquared() > 0.01f)
        {
            velocity.X = Mathf.Lerp(velocity.X, targetVelocity.X, Acceleration * delta);
            velocity.Z = Mathf.Lerp(velocity.Z, targetVelocity.Z, Acceleration * delta);
        }
        else
        {
            velocity.X = Mathf.Lerp(velocity.X, 0, Deceleration * delta);
            velocity.Z = Mathf.Lerp(velocity.Z, 0, Deceleration * delta);
        }

        Velocity = velocity;
    }

    private void RotateTowardDirection(Vector3 direction, float delta)
    {
        if (direction.LengthSquared() < 0.01f) return;

        float targetAngle = Mathf.Atan2(direction.X, direction.Z);
        float currentAngle = Rotation.Y;

        float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, RotationSpeed * delta);
        Rotation = new Vector3(Rotation.X, newAngle, Rotation.Z);
    }

    private void ApplyGravity(float delta)
    {
        var velocity = Velocity;

        if (!IsOnFloor())
        {
            velocity.Y -= Gravity * delta;
        }
        else if (Input.IsActionJustPressed("jump"))
        {
            velocity.Y = JumpVelocity;
        }

        Velocity = velocity;
    }

    private void TryInteract()
    {
        // Find nearest interaction point
        var nearest = _interactionSystem.GetNearestInteractionPoint(GlobalPosition);
        if (nearest != null && !_isInteracting)
        {
            StartInteraction(nearest);
        }
    }

    private void StartInteraction(InteractionPoint point)
    {
        _isInteracting = true;
        point.OnInteract(this);
        EmitSignal(SignalName.InteractionStarted, point);
    }

    private void EndInteraction()
    {
        _isInteracting = false;
        EmitSignal(SignalName.InteractionEnded);
    }

    private void HandleInteractionInput()
    {
        // Interaction-specific input is handled by the interaction point
    }

    /// <summary>
    /// Get the camera target position for camera follow.
    /// </summary>
    public Vector3 GetCameraTargetPosition()
    {
        return _cameraTarget?.GlobalPosition ?? GlobalPosition + new Vector3(0, 1.5f, 0);
    }

    /// <summary>
    /// Set the camera reference.
    /// </summary>
    public void SetCamera(PlayerCamera camera)
    {
        _camera = camera;
    }

    /// <summary>
    /// Get the current ship the player is on.
    /// </summary>
    public Ship CurrentShip => _currentShip;

    /// <summary>
    /// Whether the player is currently in an interaction.
    /// </summary>
    public bool IsInteracting => _isInteracting;
}
