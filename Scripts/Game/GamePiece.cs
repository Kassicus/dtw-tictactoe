using Godot;
using System;

public partial class GamePiece : RigidBody3D
{
    [Signal]
    public delegate void PieceLandedEventHandler();

    private bool _hasLanded = false;
    private float _timeSinceSpawn = 0f;
    private const float MinTimeBeforeLanding = 0.5f;
    private const float VelocityThreshold = 0.5f;

    public Action OnLanded { get; set; }
    public bool PlayLandingSound { get; set; } = true;

    public override void _PhysicsProcess(double delta)
    {
        if (_hasLanded) return;

        _timeSinceSpawn += (float)delta;

        // Wait a bit before checking for landing
        if (_timeSinceSpawn < MinTimeBeforeLanding) return;

        // Check if piece has settled (low velocity)
        if (LinearVelocity.Length() < VelocityThreshold)
        {
            _hasLanded = true;
            if (PlayLandingSound)
            {
                AudioManager.Instance?.PlayPieceLand();
            }
            EmitSignal(SignalName.PieceLanded);
            OnLanded?.Invoke();
        }
    }
}
