using Godot;
using System;

/// <summary>
/// Third-person camera that follows the player character.
/// Adapted from GameCamera.cs with additional features for naval combat.
/// </summary>
public partial class PlayerCamera : Camera3D
{
    [Export] public NodePath TargetPath { get; set; }
    [Export] public float DistanceBehind { get; set; } = 5f;
    [Export] public float HeightAbove { get; set; } = 2.5f;
    [Export] public float FollowSpeed { get; set; } = 8f;
    [Export] public float RotationSpeed { get; set; } = 3f;
    [Export] public float MouseSensitivity { get; set; } = 0.003f;

    // Camera angles
    [Export] public float MinPitch { get; set; } = -30f;
    [Export] public float MaxPitch { get; set; } = 60f;

    private Node3D _target;
    private PlayerController _playerController;

    // Camera orbit angles
    private float _yaw = 0f;
    private float _pitch = 15f;

    // Smooth follow
    private Vector3 _currentPosition;
    private bool _isTransitioning = false;
    private float _transitionProgress = 0f;
    private Vector3 _transitionStartPos;
    private Vector3 _transitionEndPos;

    public override void _Ready()
    {
        // Find target
        if (TargetPath != null && !TargetPath.IsEmpty)
        {
            _target = GetNode<Node3D>(TargetPath);
        }

        if (_target is PlayerController controller)
        {
            _playerController = controller;
            _playerController.SetCamera(this);
        }

        _currentPosition = GlobalPosition;

        // Capture mouse for camera control
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        // Mouse look
        if (@event is InputEventMouseMotion mouseMotion)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                _yaw -= mouseMotion.Relative.X * MouseSensitivity;
                _pitch += mouseMotion.Relative.Y * MouseSensitivity; // Inverted for natural feel
                _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
            }
        }

        // Toggle mouse capture
        if (@event.IsActionPressed("ui_cancel"))
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else
            {
                Input.MouseMode = Input.MouseModeEnum.Captured;
            }
        }

        // Controller camera input
        if (@event is InputEventJoypadMotion joyMotion)
        {
            HandleJoystickCamera(joyMotion);
        }
    }

    private void HandleJoystickCamera(InputEventJoypadMotion motion)
    {
        // Right stick for camera
        if (motion.Axis == JoyAxis.RightX)
        {
            _yaw -= motion.AxisValue * RotationSpeed * 0.05f;
        }
        else if (motion.Axis == JoyAxis.RightY)
        {
            _pitch += motion.AxisValue * RotationSpeed * 0.05f; // Inverted for natural feel
            _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
        }
    }

    public override void _Process(double delta)
    {
        if (_target == null) return;

        // Get target position
        Vector3 targetPos = _playerController?.GetCameraTargetPosition() ?? _target.GlobalPosition;

        // Calculate camera position based on orbit
        Vector3 cameraOffset = CalculateCameraOffset();
        Vector3 desiredPosition = targetPos + cameraOffset;

        // Smooth follow
        if (_isTransitioning)
        {
            _transitionProgress += (float)delta * 2f;
            if (_transitionProgress >= 1f)
            {
                _transitionProgress = 1f;
                _isTransitioning = false;
            }
            float t = EaseInOutCubic(_transitionProgress);
            _currentPosition = _transitionStartPos.Lerp(_transitionEndPos, t);
        }
        else
        {
            _currentPosition = _currentPosition.Lerp(desiredPosition, FollowSpeed * (float)delta);
        }

        GlobalPosition = _currentPosition;

        // Look at target
        LookAt(targetPos, Vector3.Up);
    }

    private Vector3 CalculateCameraOffset()
    {
        // Spherical to Cartesian conversion
        float x = DistanceBehind * Mathf.Cos(_pitch) * Mathf.Sin(_yaw);
        float y = HeightAbove + DistanceBehind * Mathf.Sin(_pitch);
        float z = DistanceBehind * Mathf.Cos(_pitch) * Mathf.Cos(_yaw);

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Smoothly transition to a new camera position.
    /// </summary>
    public void TransitionTo(Vector3 targetPosition, float duration = 1f)
    {
        _transitionStartPos = _currentPosition;
        _transitionEndPos = targetPosition;
        _transitionProgress = 0f;
        _isTransitioning = true;
    }

    /// <summary>
    /// Set the camera target.
    /// </summary>
    public void SetTarget(Node3D target)
    {
        _target = target;
        if (_target is PlayerController controller)
        {
            _playerController = controller;
            _playerController.SetCamera(this);
        }
    }

    /// <summary>
    /// Reset camera to default position behind target.
    /// </summary>
    public void ResetPosition()
    {
        if (_target == null) return;

        _yaw = 0f;
        _pitch = Mathf.DegToRad(15f);

        var targetPos = _playerController?.GetCameraTargetPosition() ?? _target.GlobalPosition;
        _currentPosition = targetPos + CalculateCameraOffset();
        GlobalPosition = _currentPosition;
    }

    /// <summary>
    /// Set camera to look at a specific point (for targeting).
    /// </summary>
    public void FocusOn(Vector3 point)
    {
        var direction = (point - GlobalPosition).Normalized();
        _yaw = Mathf.Atan2(direction.X, direction.Z);
        _pitch = Mathf.Asin(direction.Y);
        _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitch), Mathf.DegToRad(MaxPitch));
    }

    /// <summary>
    /// Get the camera's forward direction (for aiming).
    /// </summary>
    public Vector3 GetAimDirection()
    {
        return -GlobalTransform.Basis.Z;
    }

    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
}
