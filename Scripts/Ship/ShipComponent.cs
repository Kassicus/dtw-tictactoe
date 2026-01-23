using Godot;
using System;

/// <summary>
/// Base class for all damageable ship components (hull sections, masts, cannons).
/// Provides health management and visual damage state feedback.
/// </summary>
public partial class ShipComponent : Node3D
{
    [Export] public string ComponentName { get; set; } = "Component";
    [Export] public float MaxHealth { get; set; } = 100f;

    protected ComponentHealth Health { get; private set; }
    protected Ship ParentShip { get; private set; }

    // Visual effect nodes (created in subclasses or assigned in scene)
    protected GpuParticles3D DamageSmoke { get; set; }
    protected GpuParticles3D DamageFire { get; set; }

    // Materials for damage visualization
    protected StandardMaterial3D HealthyMaterial { get; set; }
    protected StandardMaterial3D DamagedMaterial { get; set; }
    protected StandardMaterial3D CriticalMaterial { get; set; }

    [Signal]
    public delegate void ComponentDamagedEventHandler(ShipComponent component, float damage);

    [Signal]
    public delegate void ComponentDestroyedEventHandler(ShipComponent component);

    public override void _Ready()
    {
        // Create and configure health component
        Health = new ComponentHealth();
        Health.MaxHealth = MaxHealth;
        AddChild(Health);

        // Connect health signals
        Health.DamageStateChanged += OnDamageStateChanged;
        Health.Destroyed += OnDestroyed;

        // Find parent ship
        ParentShip = FindParentShip();

        // Create damage visualization materials
        CreateDamageMaterials();

        // Create damage particle effects
        CreateDamageEffects();
    }

    private Ship FindParentShip()
    {
        var parent = GetParent();
        while (parent != null)
        {
            if (parent is Ship ship)
            {
                return ship;
            }
            parent = parent.GetParent();
        }
        return null;
    }

    /// <summary>
    /// Apply damage to this component.
    /// </summary>
    public virtual float TakeDamage(float amount)
    {
        float actualDamage = Health.TakeDamage(amount);
        if (actualDamage > 0)
        {
            EmitSignal(SignalName.ComponentDamaged, this, actualDamage);
            OnTakeDamage(actualDamage);
        }
        return actualDamage;
    }

    /// <summary>
    /// Called when damage is taken. Override in subclasses for specific behavior.
    /// </summary>
    protected virtual void OnTakeDamage(float damage)
    {
        // Spawn hit effect, play sound, etc.
    }

    /// <summary>
    /// Called when damage state changes. Updates visuals.
    /// </summary>
    protected virtual void OnDamageStateChanged(int oldState, int newState)
    {
        var state = (ComponentHealth.DamageState)newState;
        UpdateDamageVisuals(state);
    }

    /// <summary>
    /// Called when component is destroyed.
    /// </summary>
    protected virtual void OnDestroyed()
    {
        EmitSignal(SignalName.ComponentDestroyed, this);
        OnComponentDestroyed();
    }

    /// <summary>
    /// Override in subclasses for destruction behavior.
    /// </summary>
    protected virtual void OnComponentDestroyed()
    {
        // Play destruction animation, spawn debris, etc.
    }

    /// <summary>
    /// Update visual appearance based on damage state.
    /// </summary>
    protected virtual void UpdateDamageVisuals(ComponentHealth.DamageState state)
    {
        // Update smoke/fire particles
        if (DamageSmoke != null)
        {
            DamageSmoke.Emitting = state >= ComponentHealth.DamageState.Damaged;
        }

        if (DamageFire != null)
        {
            DamageFire.Emitting = state >= ComponentHealth.DamageState.Critical;
        }

        // Apply damage material to meshes
        ApplyDamageMaterial(state);
    }

    /// <summary>
    /// Create materials for different damage states.
    /// </summary>
    protected virtual void CreateDamageMaterials()
    {
        // Healthy: normal wood appearance
        HealthyMaterial = new StandardMaterial3D();
        HealthyMaterial.AlbedoColor = new Color(0.55f, 0.35f, 0.2f); // Brown wood

        // Damaged: darkened, cracked appearance
        DamagedMaterial = new StandardMaterial3D();
        DamagedMaterial.AlbedoColor = new Color(0.4f, 0.25f, 0.15f); // Darker wood

        // Critical: charred, burning appearance
        CriticalMaterial = new StandardMaterial3D();
        CriticalMaterial.AlbedoColor = new Color(0.2f, 0.15f, 0.1f); // Very dark
        CriticalMaterial.EmissionEnabled = true;
        CriticalMaterial.Emission = new Color(1f, 0.3f, 0.1f); // Orange glow
        CriticalMaterial.EmissionEnergyMultiplier = 0.5f;
    }

    /// <summary>
    /// Apply the appropriate damage material to mesh instances.
    /// </summary>
    protected virtual void ApplyDamageMaterial(ComponentHealth.DamageState state)
    {
        // Override in subclasses to apply materials to specific meshes
    }

    /// <summary>
    /// Create smoke and fire particle effects for damage visualization.
    /// </summary>
    protected virtual void CreateDamageEffects()
    {
        // Create damage smoke
        DamageSmoke = CreateDamageSmokeParticles();
        AddChild(DamageSmoke);

        // Create fire for critical damage
        DamageFire = CreateDamageFireParticles();
        AddChild(DamageFire);
    }

    private GpuParticles3D CreateDamageSmokeParticles()
    {
        var smoke = new GpuParticles3D();
        smoke.Name = "DamageSmoke";
        smoke.Emitting = false;
        smoke.Amount = 30;
        smoke.Lifetime = 3f;
        smoke.Explosiveness = 0.1f;
        smoke.Randomness = 0.3f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.5f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 30f;
        processMat.InitialVelocityMin = 0.5f;
        processMat.InitialVelocityMax = 1.5f;
        processMat.Gravity = new Vector3(0, 1f, 0); // Rise
        processMat.ScaleMin = 0.5f;
        processMat.ScaleMax = 2f;

        // Gray smoke color
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.4f, 0.4f, 0.4f, 0.6f));
        gradient.SetColor(1, new Color(0.3f, 0.3f, 0.3f, 0f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 2f;

        smoke.ProcessMaterial = processMat;

        // Create quad mesh for particles
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(1.5f, 1.5f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        quadMesh.Material = mat;

        smoke.DrawPass1 = quadMesh;

        return smoke;
    }

    private GpuParticles3D CreateDamageFireParticles()
    {
        var fire = new GpuParticles3D();
        fire.Name = "DamageFire";
        fire.Emitting = false;
        fire.Amount = 40;
        fire.Lifetime = 1f;
        fire.Explosiveness = 0.2f;
        fire.Randomness = 0.4f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.3f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 20f;
        processMat.InitialVelocityMin = 1f;
        processMat.InitialVelocityMax = 3f;
        processMat.Gravity = new Vector3(0, 2f, 0);
        processMat.ScaleMin = 0.3f;
        processMat.ScaleMax = 1f;

        // Fire color: yellow -> orange -> red -> transparent
        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.3f, 0.7f, 1f };
        gradient.Colors = new Color[] {
            new Color(1f, 1f, 0.5f, 1f),   // Bright yellow
            new Color(1f, 0.6f, 0.2f, 0.9f), // Orange
            new Color(1f, 0.2f, 0.1f, 0.6f), // Red
            new Color(0.5f, 0.1f, 0.05f, 0f) // Dark red, fading
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        fire.ProcessMaterial = processMat;

        // Create quad mesh
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.8f, 0.8f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.5f, 0.2f);
        mat.EmissionEnergyMultiplier = 2f;
        quadMesh.Material = mat;

        fire.DrawPass1 = quadMesh;

        return fire;
    }

    // Public accessors for health state
    public float CurrentHealth => Health?.CurrentHealth ?? 0f;
    public float HealthPercent => Health?.HealthPercentage ?? 0f;
    public bool IsDestroyed => Health?.IsDestroyed ?? false;
    public ComponentHealth.DamageState DamageState => Health?.State ?? ComponentHealth.DamageState.Healthy;
}
