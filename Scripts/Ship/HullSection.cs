using Godot;
using System;

/// <summary>
/// Hull section component (bow, stern, port, starboard).
/// When damaged, causes water ingress and ship listing.
/// </summary>
public partial class HullSection : ShipComponent
{
    public enum HullPosition
    {
        Bow,        // Front
        Stern,      // Back
        Port,       // Left side
        Starboard   // Right side
    }

    [Export] public HullPosition HullPos { get; set; } = HullPosition.Port;
    [Export] public float WaterIngress { get; private set; } = 0f;

    // Water ingress rate per damage state (units per second)
    private const float DamagedIngress = 0.1f;
    private const float CriticalIngress = 0.5f;
    private const float DestroyedIngress = 2f;

    // Reference to mesh for damage visualization
    private MeshInstance3D _hullMesh;

    // Collision area for projectile detection
    private Area3D _hitArea;

    [Signal]
    public delegate void WaterIngressChangedEventHandler(HullSection section, float ingressRate);

    public override void _Ready()
    {
        base._Ready();
        ComponentName = $"Hull ({HullPos})";
        MaxHealth = 150f; // Hull sections are tougher

        // Find hull mesh if assigned
        _hullMesh = GetNodeOrNull<MeshInstance3D>("Mesh");

        // Create collision area for hit detection
        CreateHitArea();
    }

    private void CreateHitArea()
    {
        _hitArea = new Area3D();
        _hitArea.Name = "HitArea";
        _hitArea.CollisionLayer = 4; // Layer 3 for ship components
        _hitArea.CollisionMask = 0;

        var collision = new CollisionShape3D();
        var shape = new BoxShape3D();

        // Size based on hull position
        shape.Size = HullPos switch
        {
            HullPosition.Bow => new Vector3(5f, 3f, 4f),
            HullPosition.Stern => new Vector3(5f, 3f, 4f),
            HullPosition.Port => new Vector3(2f, 3f, 12f),
            HullPosition.Starboard => new Vector3(2f, 3f, 12f),
            _ => new Vector3(4f, 3f, 4f)
        };

        collision.Shape = shape;
        _hitArea.AddChild(collision);
        AddChild(_hitArea);
    }

    public override void _Process(double delta)
    {
        // Calculate water ingress based on damage state
        float ingressRate = GetIngressRate();
        if (ingressRate > 0 && ParentShip != null)
        {
            WaterIngress += ingressRate * (float)delta;
            // Ship handles actual flooding
        }
    }

    private float GetIngressRate()
    {
        return DamageState switch
        {
            ComponentHealth.DamageState.Damaged => DamagedIngress,
            ComponentHealth.DamageState.Critical => CriticalIngress,
            ComponentHealth.DamageState.Destroyed => DestroyedIngress,
            _ => 0f
        };
    }

    protected override void OnDamageStateChanged(int oldState, int newState)
    {
        base.OnDamageStateChanged(oldState, newState);

        float ingressRate = GetIngressRate();
        EmitSignal(SignalName.WaterIngressChanged, this, ingressRate);

        // Position smoke/fire at appropriate hull location
        PositionDamageEffects();
    }

    private void PositionDamageEffects()
    {
        // Position effects at waterline for hull damage
        if (DamageSmoke != null)
        {
            DamageSmoke.Position = new Vector3(0, -0.5f, 0);
        }
        if (DamageFire != null)
        {
            DamageFire.Position = new Vector3(0, 0, 0);
        }
    }

    protected override void ApplyDamageMaterial(ComponentHealth.DamageState state)
    {
        if (_hullMesh == null) return;

        var material = state switch
        {
            ComponentHealth.DamageState.Healthy => HealthyMaterial,
            ComponentHealth.DamageState.Damaged => DamagedMaterial,
            ComponentHealth.DamageState.Critical => CriticalMaterial,
            _ => CriticalMaterial
        };

        _hullMesh.SetSurfaceOverrideMaterial(0, material);
    }

    /// <summary>
    /// Get the direction this hull section affects ship listing.
    /// </summary>
    public Vector3 GetListDirection()
    {
        return HullPos switch
        {
            HullPosition.Port => new Vector3(0, 0, -1),      // List to port (negative Z rotation)
            HullPosition.Starboard => new Vector3(0, 0, 1), // List to starboard
            HullPosition.Bow => new Vector3(-1, 0, 0),      // Pitch forward
            HullPosition.Stern => new Vector3(1, 0, 0),     // Pitch backward
            _ => Vector3.Zero
        };
    }

    /// <summary>
    /// Get listing severity based on water ingress.
    /// </summary>
    public float GetListSeverity()
    {
        // More water = more listing
        return Mathf.Clamp(WaterIngress / 100f, 0f, 1f);
    }
}
