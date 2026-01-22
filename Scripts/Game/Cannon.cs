using Godot;
using System;

public partial class Cannon : Node3D
{
    [Export] public Player OwnerPlayer { get; set; } = Player.X;

    private Node3D _barrel;
    private Marker3D _muzzlePoint;
    private GpuParticles3D _muzzleFlash;

    private const float RecoilDistance = 0.5f;
    private const float RecoilDuration = 0.15f;

    public override void _Ready()
    {
        _barrel = GetNode<Node3D>("Barrel");
        _muzzlePoint = GetNode<Marker3D>("Barrel/MuzzlePoint");
        _muzzleFlash = GetNodeOrNull<GpuParticles3D>("Barrel/MuzzleFlash");
    }

    /// <summary>
    /// Fire a piece at the target cell.
    /// </summary>
    public void Fire(Cell target, PackedScene pieceScene, Action onLanded)
    {
        var targetPos = target.GlobalPosition + new Vector3(0, 0.15f, 0); // Slightly above cell

        // Calculate launch parameters from muzzle to target
        var spawnPos = _muzzlePoint.GlobalPosition;
        var velocity = CalculateLaunchVelocity(spawnPos, targetPos);

        // Aim the barrel toward the initial velocity direction
        AimBarrelAt(spawnPos + velocity.Normalized() * 5f);

        // Create the piece
        var piece = pieceScene.Instantiate<GamePiece>();
        piece.OnLanded = onLanded;

        // Add to scene at muzzle position
        GetTree().CurrentScene.AddChild(piece);
        piece.GlobalPosition = spawnPos;

        // Prepare piece for accurate ballistic flight
        PreparePieceForFlight(piece);

        // Apply launch velocity
        piece.LinearVelocity = velocity;

        // Add slight random rotation for visual interest
        piece.RotateY((float)GD.RandRange(-0.2, 0.2));

        // Effects
        PlayRecoilAnimation();
        PlayMuzzleFlash();
        PlayFireSound();
    }

    /// <summary>
    /// Rotate the barrel to aim at the target position.
    /// </summary>
    private void AimBarrelAt(Vector3 targetPos)
    {
        if (_barrel == null) return;

        var barrelWorldPos = _barrel.GlobalPosition;
        var direction = targetPos - barrelWorldPos;

        if (direction.LengthSquared() > 0.001f)
        {
            _barrel.LookAt(targetPos, Vector3.Up);
        }
    }

    /// <summary>
    /// Calculate launch velocity for a nice parabolic arc to the target.
    /// Uses a fixed flight time for consistent, predictable arcs.
    /// </summary>
    public static Vector3 CalculateLaunchVelocity(Vector3 start, Vector3 target)
    {
        float gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

        // Use fixed flight time for consistent arcs (~1.5 seconds)
        float flightTime = 1.5f;

        // Horizontal velocity (constant, no air resistance)
        float vx = (target.X - start.X) / flightTime;
        float vz = (target.Z - start.Z) / flightTime;

        // Vertical velocity: solve for vy where target.Y = start.Y + vy*t - 0.5*g*t^2
        // vy = (target.Y - start.Y + 0.5*g*t^2) / t
        float vy = (target.Y - start.Y + 0.5f * gravity * flightTime * flightTime) / flightTime;

        return new Vector3(vx, vy, vz);
    }

    /// <summary>
    /// Prepare piece for ballistic flight (disable damping).
    /// </summary>
    private static void PreparePieceForFlight(GamePiece piece)
    {
        // Disable damping for accurate ballistic trajectory
        piece.LinearDamp = 0f;
        piece.AngularDamp = 0f;
        piece.GravityScale = 1f;
    }

    private void PlayRecoilAnimation()
    {
        if (_barrel == null) return;

        var tween = CreateTween();
        var originalPos = _barrel.Position;
        var recoilPos = originalPos + new Vector3(0, 0, RecoilDistance);

        // Recoil back
        tween.TweenProperty(_barrel, "position", recoilPos, RecoilDuration * 0.3)
            .SetEase(Tween.EaseType.Out);
        // Return forward
        tween.TweenProperty(_barrel, "position", originalPos, RecoilDuration * 0.7)
            .SetEase(Tween.EaseType.Out);
    }

    private void PlayMuzzleFlash()
    {
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Emitting = true;
        }
    }

    private void PlayFireSound()
    {
        // Try to use AudioManager first (for consistent SFX handling)
        AudioManager.Instance?.PlayCannonFire();
    }
}
