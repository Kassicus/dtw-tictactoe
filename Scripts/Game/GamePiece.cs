using Godot;
using System;

public partial class GamePiece : RigidBody3D
{
    [Signal]
    public delegate void PieceLandedEventHandler();

    // Flight parameters
    private Vector3 _startPos;
    private Vector3 _endPos;
    private float _arcHeight;
    private float _flightDuration;
    private float _flightProgress = 0f;
    private bool _isInFlight = false;
    private bool _hasLanded = false;

    public Action OnLanded { get; set; }
    public bool PlayLandingSound { get; set; } = true;
    public Cell TargetCell { get; set; }

    /// <summary>
    /// Start a guided flight along a parabolic arc to the target position.
    /// The piece will land exactly on the target.
    /// </summary>
    public void StartGuidedFlight(Vector3 startPos, Vector3 endPos, float arcHeight, float duration)
    {
        _startPos = startPos;
        _endPos = endPos;
        _arcHeight = arcHeight;
        _flightDuration = duration;
        _flightProgress = 0f;
        _isInFlight = true;
        _hasLanded = false;

        // Disable physics during guided flight
        Freeze = true;

        // Set initial position
        GlobalPosition = startPos;
    }

    public override void _Process(double delta)
    {
        if (!_isInFlight || _hasLanded) return;

        _flightProgress += (float)delta;

        // Calculate normalized progress (0 to 1)
        float t = Mathf.Clamp(_flightProgress / _flightDuration, 0f, 1f);

        // Interpolate position along parabolic arc
        GlobalPosition = CalculateArcPosition(t);

        // Add rotation during flight for visual interest
        RotateY((float)delta * 2f);

        // Check if flight is complete
        if (t >= 1f)
        {
            CompleteFlight();
        }
    }

    /// <summary>
    /// Calculate position along the parabolic arc at time t (0 to 1).
    /// </summary>
    private Vector3 CalculateArcPosition(float t)
    {
        // Linear interpolation for X and Z
        float x = Mathf.Lerp(_startPos.X, _endPos.X, t);
        float z = Mathf.Lerp(_startPos.Z, _endPos.Z, t);

        // Parabolic arc for Y
        // The arc peaks at t=0.5 with height = max(startY, endY) + arcHeight
        float baseY = Mathf.Lerp(_startPos.Y, _endPos.Y, t);

        // Parabola that is 0 at t=0 and t=1, and 1 at t=0.5
        // Formula: 4 * t * (1 - t) gives values from 0 to 1 with peak at 0.5
        float arcOffset = 4f * t * (1f - t) * _arcHeight;

        float y = baseY + arcOffset;

        return new Vector3(x, y, z);
    }

    /// <summary>
    /// Complete the flight - snap to exact target position and trigger landing.
    /// </summary>
    private void CompleteFlight()
    {
        _isInFlight = false;
        _hasLanded = true;

        // Snap to exact target position
        GlobalPosition = _endPos;

        // Play landing sound
        if (PlayLandingSound)
        {
            AudioManager.Instance?.PlayPieceLand();
        }

        // Light up the target cell
        TargetCell?.ShowLandingGlow();

        // Emit signal and invoke callback
        EmitSignal(SignalName.PieceLanded);
        OnLanded?.Invoke();
    }
}
