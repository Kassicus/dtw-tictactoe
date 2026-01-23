using Godot;
using System;

/// <summary>
/// Mast component with sails. When damaged, reduces ship speed.
/// Can collapse when destroyed.
/// </summary>
public partial class Mast : ShipComponent
{
    public enum MastType
    {
        Foremast,   // Front mast
        Mainmast,   // Center (tallest)
        Mizzenmast  // Rear mast
    }

    [Export] public MastType Type { get; set; } = MastType.Mainmast;
    [Export] public float SpeedContribution { get; set; } = 0.33f; // How much this mast contributes to total speed

    private MeshInstance3D _mastMesh;
    private MeshInstance3D _sailMesh;
    private bool _hasCollapsed = false;

    // Original transform for collapse animation
    private Transform3D _originalTransform;

    [Signal]
    public delegate void SailDamagedEventHandler(Mast mast, float speedReduction);

    [Signal]
    public delegate void MastCollapsedEventHandler(Mast mast);

    public override void _Ready()
    {
        base._Ready();
        ComponentName = Type.ToString();
        MaxHealth = 80f; // Masts are somewhat fragile

        _originalTransform = Transform;

        // Find mesh components
        _mastMesh = GetNodeOrNull<MeshInstance3D>("MastMesh");
        _sailMesh = GetNodeOrNull<MeshInstance3D>("SailMesh");
    }

    /// <summary>
    /// Get current speed effectiveness (1.0 = full, 0 = no contribution).
    /// </summary>
    public float GetSpeedEffectiveness()
    {
        if (_hasCollapsed) return 0f;
        return Health.GetEffectivenessMultiplier() * SpeedContribution;
    }

    protected override void OnDamageStateChanged(int oldState, int newState)
    {
        base.OnDamageStateChanged(oldState, newState);

        var state = (ComponentHealth.DamageState)newState;

        // Emit speed reduction signal
        float reduction = 1f - Health.GetEffectivenessMultiplier();
        EmitSignal(SignalName.SailDamaged, this, reduction * SpeedContribution);

        // Update sail appearance
        UpdateSailVisuals(state);
    }

    protected override void OnComponentDestroyed()
    {
        base.OnComponentDestroyed();

        if (!_hasCollapsed)
        {
            CollapseMast();
        }
    }

    /// <summary>
    /// Animate mast collapse when destroyed.
    /// </summary>
    private void CollapseMast()
    {
        _hasCollapsed = true;
        EmitSignal(SignalName.MastCollapsed, this);

        // Animated collapse
        var tween = CreateTween();

        // Rotate to fall (random direction)
        var fallDirection = GD.Randf() > 0.5f ? 1f : -1f;
        var targetRotation = new Vector3(
            Mathf.DegToRad(85f) * fallDirection,
            Rotation.Y,
            Mathf.DegToRad(10f) * (float)GD.RandRange(-1, 1)
        );

        tween.TweenProperty(this, "rotation", targetRotation, 2f)
            .SetEase(Tween.EaseType.In)
            .SetTrans(Tween.TransitionType.Quad);

        // Spawn debris particles
        SpawnCollapseDebris();
    }

    private void SpawnCollapseDebris()
    {
        var debris = new GpuParticles3D();
        debris.Name = "CollapseDebris";
        debris.Amount = 30;
        debris.OneShot = true;
        debris.Lifetime = 3f;
        debris.Explosiveness = 0.8f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 1f;
        processMat.Direction = new Vector3(0, 0, 0);
        processMat.Spread = 180f;
        processMat.InitialVelocityMin = 3f;
        processMat.InitialVelocityMax = 8f;
        processMat.Gravity = new Vector3(0, -10f, 0);
        processMat.ScaleMin = 0.1f;
        processMat.ScaleMax = 0.4f;
        processMat.AngularVelocityMin = -180f;
        processMat.AngularVelocityMax = 180f;

        // Wood splinter color
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.6f, 0.4f, 0.2f, 1f));
        gradient.SetColor(1, new Color(0.4f, 0.3f, 0.15f, 0.5f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        debris.ProcessMaterial = processMat;

        // Simple box mesh for splinters
        var boxMesh = new BoxMesh();
        boxMesh.Size = new Vector3(0.1f, 0.3f, 0.05f);

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.5f, 0.35f, 0.2f);
        mat.VertexColorUseAsAlbedo = true;
        boxMesh.Material = mat;

        debris.DrawPass1 = boxMesh;

        GetTree().CurrentScene.AddChild(debris);
        debris.GlobalPosition = GlobalPosition + new Vector3(0, 3f, 0); // Mid-mast
        debris.Emitting = true;

        // Auto-cleanup
        var timer = GetTree().CreateTimer(5f);
        timer.Timeout += () => {
            if (IsInstanceValid(debris))
            {
                debris.QueueFree();
            }
        };
    }

    private void UpdateSailVisuals(ComponentHealth.DamageState state)
    {
        if (_sailMesh == null) return;

        // Reduce sail visibility/opacity based on damage
        var material = _sailMesh.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
        if (material == null)
        {
            material = new StandardMaterial3D();
            material.AlbedoColor = new Color(0.9f, 0.85f, 0.75f); // Canvas color
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _sailMesh.SetSurfaceOverrideMaterial(0, material);
        }

        // Tatter the sails based on damage
        float alpha = state switch
        {
            ComponentHealth.DamageState.Healthy => 1f,
            ComponentHealth.DamageState.Damaged => 0.8f,
            ComponentHealth.DamageState.Critical => 0.5f,
            ComponentHealth.DamageState.Destroyed => 0.2f,
            _ => 1f
        };

        var color = material.AlbedoColor;
        material.AlbedoColor = new Color(color.R, color.G, color.B, alpha);
    }

    protected override void ApplyDamageMaterial(ComponentHealth.DamageState state)
    {
        if (_mastMesh == null) return;

        var material = state switch
        {
            ComponentHealth.DamageState.Healthy => HealthyMaterial,
            ComponentHealth.DamageState.Damaged => DamagedMaterial,
            ComponentHealth.DamageState.Critical => CriticalMaterial,
            _ => CriticalMaterial
        };

        _mastMesh.SetSurfaceOverrideMaterial(0, material);
    }

    /// <summary>
    /// Whether this mast has collapsed.
    /// </summary>
    public bool HasCollapsed => _hasCollapsed;
}
