using Godot;
using System;

public partial class GamePiece : RigidBody3D
{
    [Signal]
    public delegate void PieceLandedEventHandler();

    private bool _hasLanded = false;
    private float _timeSinceSpawn = 0f;
    private const float MinTimeBeforeLanding = 0.5f; // Don't check landing immediately
    private const float VelocityThreshold = 0.5f; // Consider landed when velocity is below this

    public Action OnLanded { get; set; }

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
            EmitSignal(SignalName.PieceLanded);
            OnLanded?.Invoke();
        }
    }
}
