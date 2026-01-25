using Godot;
using System;

/// <summary>
/// Ship-mounted cannon with health/damage system.
/// Adapted from existing Cannon.cs with added destruction mechanics.
/// </summary>
public partial class ShipCannon : ShipComponent
{
    public enum CannonSide
    {
        Port,       // Left side of ship
        Starboard   // Right side of ship
    }

    [Export] public CannonSide Side { get; set; } = CannonSide.Port;
    [Export] public int CannonIndex { get; set; } = 0;

    private Node3D _barrel;
    private Marker3D _muzzlePoint;
    private GpuParticles3D _muzzleFlash;
    private GpuParticles3D _smoke;
    private MeshInstance3D _cannonMesh;
    private Area3D _interactionArea;
    private MeshInstance3D _highlightMesh;

    // Shared haze material and mesh (created once, reused)
    private static StandardMaterial3D _hazeMaterial;
    private static QuadMesh _hazeMesh;

    private const float RecoilDistance = 0.5f;
    private const float RecoilDuration = 0.15f;
    private const float ArcHeight = 8f;
    private const float BaseFlightDuration = 1.2f;
    private const float FlightDurationPerUnit = 0.04f;
    private const float AimDuration = 0.3f;
    private const float InteractionRange = 2f;

    // Reload time (affected by damage)
    [Export] public float BaseReloadTime { get; set; } = 3f;
    private float _reloadProgress = 0f;
    private bool _isLoaded = true;
    private bool _isHighlighted = false;

    // Pending fire data
    private Vector3 _pendingTarget;
    private Action<Vector3> _pendingOnImpact;
    private Tween _aimTween;

    // Barrel default orientation (for resetting after fire)
    private Transform3D _barrelDefaultTransform;

    [Signal]
    public delegate void CannonFiredEventHandler(ShipCannon cannon, Vector3 targetPos);

    [Signal]
    public delegate void CannonReloadedEventHandler(ShipCannon cannon);

    public override void _Ready()
    {
        base._Ready();
        ComponentName = $"Cannon ({Side} #{CannonIndex + 1})";
        MaxHealth = 60f; // Cannons are somewhat fragile

        _barrel = GetNodeOrNull<Node3D>("Barrel");
        _muzzlePoint = GetNodeOrNull<Marker3D>("Barrel/MuzzlePoint");
        _cannonMesh = GetNodeOrNull<MeshInstance3D>("Base");

        if (_barrel == null)
        {
            // Create barrel if not in scene
            _barrel = new Node3D();
            _barrel.Name = "Barrel";
            AddChild(_barrel);

            _muzzlePoint = new Marker3D();
            _muzzlePoint.Name = "MuzzlePoint";
            _muzzlePoint.Position = new Vector3(0, 0, -2f);
            _barrel.AddChild(_muzzlePoint);
        }

        // Store the default barrel orientation for resetting after fire
        // The barrel should point in the cannon's local -Z direction (outward)
        // Reset to identity rotation with just the Y offset for the pivot
        _barrelDefaultTransform = new Transform3D(Basis.Identity, new Vector3(0, 0.2f, 0));
        _barrel.Transform = _barrelDefaultTransform;

        // Create particle systems
        CreateMuzzleFlash();
        CreateSmoke();

        // Create interaction area for player to fire cannon
        CreateInteractionArea();

        GD.Print($"ShipCannon _Ready - {ComponentName}");
    }

    private void CreateInteractionArea()
    {
        _interactionArea = new Area3D();
        _interactionArea.Name = "CannonInteractionArea";

        var collision = new CollisionShape3D();
        var sphere = new SphereShape3D();
        sphere.Radius = InteractionRange;
        collision.Shape = sphere;
        _interactionArea.AddChild(collision);

        // Create highlight ring
        _highlightMesh = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.6f;
        torus.OuterRadius = 0.8f;
        torus.Rings = 16;
        torus.RingSegments = 32;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.5f, 0.2f, 0.6f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.4f, 0.1f);
        mat.EmissionEnergyMultiplier = 1.5f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        torus.Material = mat;

        _highlightMesh.Mesh = torus;
        _highlightMesh.Rotation = new Vector3(Mathf.Pi / 2f, 0, 0);
        _highlightMesh.Visible = false;
        _interactionArea.AddChild(_highlightMesh);

        AddChild(_interactionArea);
    }

    /// <summary>
    /// Check if a player is within interaction range of this cannon.
    /// </summary>
    public bool IsPlayerInRange(Vector3 playerPosition)
    {
        return GlobalPosition.DistanceTo(playerPosition) <= InteractionRange;
    }

    /// <summary>
    /// Set the highlight state for this cannon.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (_isHighlighted == highlighted) return;
        _isHighlighted = highlighted;

        if (_highlightMesh != null)
        {
            // Only show highlight if cannon can fire AND faces the enemy
            bool canFireAtEnemy = CanFire && CanFireAtEnemy();
            _highlightMesh.Visible = highlighted && canFireAtEnemy;
        }
    }

    /// <summary>
    /// Check if this cannon can fire at the enemy (is loaded, not destroyed, and faces enemy).
    /// </summary>
    public bool CanFireAtEnemy()
    {
        if (!CanFire) return false;

        var parentShip = GetParentShip();
        if (parentShip == null) return false;

        // Find enemy ship
        foreach (var node in GetTree().GetNodesInGroup("ships"))
        {
            if (node is Ship ship && ship != parentShip)
            {
                return IsFacingTarget(ship.GetTargetPoint("center"));
            }
        }
        return false;
    }

    /// <summary>
    /// Get the direction this cannon faces (outward from the ship).
    /// </summary>
    public Vector3 GetFacingDirection()
    {
        // The cannon fires in its local -Z direction
        return -GlobalTransform.Basis.Z;
    }

    /// <summary>
    /// Check if this cannon is facing toward a target position.
    /// </summary>
    public bool IsFacingTarget(Vector3 targetPos)
    {
        var toTarget = (targetPos - GlobalPosition).Normalized();
        var facing = GetFacingDirection();
        float dot = facing.Dot(toTarget);
        // If dot product > 0, the cannon is roughly facing the target
        return dot > 0.1f;
    }

    /// <summary>
    /// Fire at the enemy ship (used when player interacts with cannon).
    /// </summary>
    public void FireAtEnemy()
    {
        if (!CanFire) return;

        // Find the enemy ship
        var parentShip = GetParentShip();
        if (parentShip == null) return;

        Ship enemyShip = null;
        foreach (var node in GetTree().GetNodesInGroup("ships"))
        {
            if (node is Ship ship && ship != parentShip)
            {
                enemyShip = ship;
                break;
            }
        }

        if (enemyShip == null) return;

        // Calculate target position on enemy ship
        var targetPos = enemyShip.GetTargetPoint("center");

        // Check if this cannon faces the enemy
        if (!IsFacingTarget(targetPos))
        {
            GD.Print($"{ComponentName} is not facing the enemy - cannot fire!");
            return;
        }

        // Add slight randomness for realism
        targetPos += new Vector3(
            (float)GD.RandRange(-1.0, 1.0),
            (float)GD.RandRange(-0.5, 0.5),
            (float)GD.RandRange(-1.0, 1.0)
        );

        Fire(targetPos, null);
        GD.Print($"Player fired {ComponentName} at {enemyShip.ShipName}!");
    }

    public override void _Process(double delta)
    {
        // Handle reload
        if (!_isLoaded && !IsDestroyed)
        {
            float reloadSpeed = Health.GetEffectivenessMultiplier(); // Damaged = slower reload
            _reloadProgress += (float)delta * reloadSpeed;

            if (_reloadProgress >= GetReloadTime())
            {
                _isLoaded = true;
                _reloadProgress = 0f;
                EmitSignal(SignalName.CannonReloaded, this);
            }
        }
    }

    /// <summary>
    /// Get current reload time (affected by damage).
    /// </summary>
    public float GetReloadTime()
    {
        float multiplier = DamageState switch
        {
            ComponentHealth.DamageState.Healthy => 1f,
            ComponentHealth.DamageState.Damaged => 1.5f,  // 50% slower
            ComponentHealth.DamageState.Critical => 2.5f, // 150% slower
            _ => float.MaxValue // Can't reload if destroyed
        };
        return BaseReloadTime * multiplier;
    }

    /// <summary>
    /// Whether this cannon can fire (loaded and not destroyed).
    /// </summary>
    public bool CanFire => _isLoaded && !IsDestroyed;

    /// <summary>
    /// Get reload progress (0-1).
    /// </summary>
    public float ReloadProgress => _isLoaded ? 1f : _reloadProgress / GetReloadTime();

    /// <summary>
    /// Fire at a target position (world coordinates).
    /// </summary>
    public void Fire(Vector3 targetPos, Action<Vector3> onImpact = null)
    {
        if (!CanFire)
        {
            GD.Print($"{ComponentName} cannot fire - Loaded: {_isLoaded}, Destroyed: {IsDestroyed}");
            return;
        }

        _pendingTarget = targetPos;
        _pendingOnImpact = onImpact;

        // Calculate aim point
        var startPos = _muzzlePoint?.GlobalPosition ?? GlobalPosition;
        var midPoint = (startPos + targetPos) / 2f + new Vector3(0, ArcHeight * 0.5f, 0);

        // Aim then fire
        AimBarrelAt(midPoint, OnAimComplete);
    }

    private void OnAimComplete()
    {
        var startPos = _muzzlePoint?.GlobalPosition ?? GlobalPosition;
        var endPos = _pendingTarget;

        // Calculate flight duration
        float distance = startPos.DistanceTo(endPos);
        float flightDuration = BaseFlightDuration + (distance * FlightDurationPerUnit);

        // Spawn cannonball projectile
        SpawnCannonball(startPos, endPos, flightDuration);

        // Effects
        PlayRecoilAnimation();
        PlayMuzzleFlash();
        PlayFireSound();

        // Cannon is now unloaded
        _isLoaded = false;
        _reloadProgress = 0f;

        EmitSignal(SignalName.CannonFired, this, endPos);

        // Clear pending
        _pendingTarget = Vector3.Zero;
        _pendingOnImpact = null;

        // Reset barrel to default position after a delay
        ResetBarrelDelayed(1.5f);
    }

    private void ResetBarrelDelayed(float delay)
    {
        if (_barrel == null) return;

        var timer = GetTree().CreateTimer(delay);
        timer.Timeout += () =>
        {
            if (_barrel != null && IsInstanceValid(_barrel))
            {
                var tween = CreateTween();
                tween.TweenProperty(_barrel, "transform", _barrelDefaultTransform, 0.5f)
                    .SetEase(Tween.EaseType.InOut)
                    .SetTrans(Tween.TransitionType.Quad);
            }
        };
    }

    private void SpawnCannonball(Vector3 startPos, Vector3 endPos, float duration)
    {
        var cannonball = new Cannonball();
        cannonball.OnImpact = _pendingOnImpact;

        var parentShip = GetParentShip();
        cannonball.FiringShip = parentShip;
        if (parentShip != null)
        {
            cannonball.FiringSide = parentShip.Side;
            GD.Print($"Cannonball fired from {parentShip.ShipName} (Side: {parentShip.Side})");
        }

        GetTree().CurrentScene.AddChild(cannonball);
        cannonball.StartFlight(startPos, endPos, ArcHeight, duration);
    }

    private Ship GetParentShip()
    {
        Node current = this;
        while (current != null)
        {
            if (current is Ship ship)
            {
                return ship;
            }
            current = current.GetParent();
        }
        return null;
    }

    private void AimBarrelAt(Vector3 targetPos, Action onComplete = null)
    {
        if (_barrel == null)
        {
            onComplete?.Invoke();
            return;
        }

        var barrelWorldPos = _barrel.GlobalPosition;
        var direction = targetPos - barrelWorldPos;

        if (direction.LengthSquared() < 0.001f)
        {
            onComplete?.Invoke();
            return;
        }

        var currentBasis = _barrel.GlobalTransform.Basis;
        _barrel.LookAt(targetPos, Vector3.Up);
        var targetQuat = _barrel.GlobalTransform.Basis.GetRotationQuaternion();

        _barrel.GlobalTransform = new Transform3D(currentBasis, _barrel.GlobalPosition);
        var currentQuat = currentBasis.GetRotationQuaternion();

        _aimTween?.Kill();
        _aimTween = CreateTween();
        _aimTween.TweenMethod(
            Callable.From<float>((t) => {
                var interpolated = currentQuat.Slerp(targetQuat, t);
                _barrel.GlobalTransform = new Transform3D(new Basis(interpolated), _barrel.GlobalPosition);
            }),
            0f, 1f, AimDuration
        ).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Quad);

        if (onComplete != null)
        {
            _aimTween.TweenCallback(Callable.From(onComplete));
        }
    }

    protected override void OnComponentDestroyed()
    {
        base.OnComponentDestroyed();

        // Cannon explosion effect
        SpawnDestructionExplosion();
    }

    private void SpawnDestructionExplosion()
    {
        var explosion = new GpuParticles3D();
        explosion.Name = "CannonExplosion";
        explosion.Amount = 50;
        explosion.OneShot = true;
        explosion.Lifetime = 1.5f;
        explosion.Explosiveness = 1f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.5f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 180f;
        processMat.InitialVelocityMin = 5f;
        processMat.InitialVelocityMax = 12f;
        processMat.Gravity = new Vector3(0, -8f, 0);
        processMat.ScaleMin = 0.2f;
        processMat.ScaleMax = 0.8f;

        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.3f, 0.7f, 1f };
        gradient.Colors = new Color[] {
            new Color(1f, 0.9f, 0.6f, 1f),
            new Color(1f, 0.5f, 0.2f, 0.8f),
            new Color(0.3f, 0.3f, 0.3f, 0.5f),
            new Color(0.2f, 0.2f, 0.2f, 0f)
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        explosion.ProcessMaterial = processMat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.8f, 0.8f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        quadMesh.Material = mat;

        explosion.DrawPass1 = quadMesh;

        GetTree().CurrentScene.AddChild(explosion);
        explosion.GlobalPosition = GlobalPosition;
        explosion.Emitting = true;

        var timer = GetTree().CreateTimer(3f);
        timer.Timeout += () => {
            if (IsInstanceValid(explosion)) explosion.QueueFree();
        };
    }

    #region Particle Systems (adapted from Cannon.cs)

    private void CreateMuzzleFlash()
    {
        _muzzleFlash = new GpuParticles3D();
        _muzzleFlash.Name = "MuzzleFlashParticles";
        _muzzleFlash.Emitting = false;
        _muzzleFlash.Amount = 24;
        _muzzleFlash.OneShot = true;
        _muzzleFlash.Explosiveness = 1.0f;
        _muzzleFlash.Lifetime = 0.3;
        _muzzleFlash.VisibilityAabb = new Aabb(new Vector3(-5, -5, -5), new Vector3(10, 10, 10));

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.3f;
        processMat.Direction = new Vector3(0, 0, -1);
        processMat.Spread = 60f;
        processMat.InitialVelocityMin = 15f;
        processMat.InitialVelocityMax = 25f;
        processMat.Gravity = new Vector3(0, -3, 0);
        processMat.ScaleMin = 0.8f;
        processMat.ScaleMax = 1.8f;
        processMat.ParticleFlagAlignY = true;

        var gradient = new Gradient();
        gradient.SetOffset(0, 0f);
        gradient.SetOffset(1, 1f);
        gradient.SetColor(0, new Color(1f, 1f, 0.8f, 1f));
        gradient.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        _muzzleFlash.ProcessMaterial = processMat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.15f, 1.2f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Disabled;
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.7f, 0.3f);
        mat.EmissionEnergyMultiplier = 5f;
        quadMesh.Material = mat;

        _muzzleFlash.DrawPass1 = quadMesh;

        if (_muzzlePoint != null)
        {
            _muzzleFlash.Position = _muzzlePoint.Position;
        }
        _barrel?.AddChild(_muzzleFlash);
    }

    private void CreateSmoke()
    {
        _smoke = new GpuParticles3D();
        _smoke.Name = "SmokeParticles";
        _smoke.Emitting = false;
        _smoke.Amount = 100;
        _smoke.OneShot = true;
        _smoke.Explosiveness = 0.95f;
        _smoke.Lifetime = 3.5;
        _smoke.Randomness = 0.5f;
        _smoke.VisibilityAabb = new Aabb(new Vector3(-15, -5, -20), new Vector3(30, 35, 40));

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        processMat.EmissionBoxExtents = new Vector3(0.3f, 0.3f, 0.8f);
        processMat.Direction = new Vector3(0, 0, -1);
        processMat.Spread = 18f;
        processMat.InitialVelocityMin = 3f;
        processMat.InitialVelocityMax = 10f;
        processMat.Gravity = new Vector3(0.05f, 2.5f, 0);
        processMat.DampingMin = 5f;
        processMat.DampingMax = 8f;
        processMat.ScaleMin = 1.3f;
        processMat.ScaleMax = 5f;

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 0.1f));
        scaleCurve.AddPoint(new Vector2(0.08f, 0.15f));
        scaleCurve.AddPoint(new Vector2(0.18f, 0.25f));
        scaleCurve.AddPoint(new Vector2(0.3f, 0.4f));
        scaleCurve.AddPoint(new Vector2(0.45f, 0.55f));
        scaleCurve.AddPoint(new Vector2(0.6f, 0.7f));
        scaleCurve.AddPoint(new Vector2(0.8f, 0.88f));
        scaleCurve.AddPoint(new Vector2(1f, 1.0f));
        var scaleCurveTex = new CurveTexture();
        scaleCurveTex.Curve = scaleCurve;
        processMat.ScaleCurve = scaleCurveTex;

        var velocityCurve = new Curve();
        velocityCurve.AddPoint(new Vector2(0, 1f));
        velocityCurve.AddPoint(new Vector2(0.12f, 0.4f));
        velocityCurve.AddPoint(new Vector2(0.3f, 0.15f));
        velocityCurve.AddPoint(new Vector2(0.6f, 0.05f));
        velocityCurve.AddPoint(new Vector2(1f, 0.02f));
        var velocityCurveTex = new CurveTexture();
        velocityCurveTex.Curve = velocityCurve;
        processMat.LinearAccelCurve = velocityCurveTex;
        processMat.LinearAccelMin = -8f;
        processMat.LinearAccelMax = -4f;

        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.05f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };
        gradient.Colors = new Color[] {
            new Color(1.0f, 0.95f, 0.8f, 0.9f),
            new Color(0.9f, 0.87f, 0.8f, 0.85f),
            new Color(0.75f, 0.73f, 0.68f, 0.75f),
            new Color(0.6f, 0.58f, 0.55f, 0.6f),
            new Color(0.5f, 0.48f, 0.46f, 0.45f),
            new Color(0.42f, 0.42f, 0.4f, 0.3f),
            new Color(0.38f, 0.38f, 0.38f, 0.15f),
            new Color(0.35f, 0.35f, 0.35f, 0f)
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 5f;
        processMat.TurbulenceNoiseScale = 3f;
        processMat.TurbulenceNoiseSpeed = new Vector3(1f, 0.6f, 1f);

        var turbulenceInfluenceCurve = new Curve();
        turbulenceInfluenceCurve.AddPoint(new Vector2(0, 0.15f));
        turbulenceInfluenceCurve.AddPoint(new Vector2(0.15f, 0.35f));
        turbulenceInfluenceCurve.AddPoint(new Vector2(0.3f, 0.6f));
        turbulenceInfluenceCurve.AddPoint(new Vector2(0.5f, 0.8f));
        turbulenceInfluenceCurve.AddPoint(new Vector2(1f, 1f));
        var turbulenceInfluenceTex = new CurveTexture();
        turbulenceInfluenceTex.Curve = turbulenceInfluenceCurve;
        processMat.TurbulenceInfluenceOverLife = turbulenceInfluenceTex;

        processMat.AngularVelocityMin = -30f;
        processMat.AngularVelocityMax = 30f;

        _smoke.ProcessMaterial = processMat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(3.5f, 3.5f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;

        var radialGradient = CreateSoftCircleTexture();
        mat.AlbedoTexture = radialGradient;

        quadMesh.Material = mat;
        _smoke.DrawPass1 = quadMesh;

        if (_muzzlePoint != null)
        {
            _smoke.Position = _muzzlePoint.Position;
        }
        _barrel?.AddChild(_smoke);
    }

    private void PlayRecoilAnimation()
    {
        if (_barrel == null) return;

        var tween = CreateTween();
        var originalPos = _barrel.Position;
        var recoilPos = originalPos + new Vector3(0, 0, RecoilDistance);

        tween.TweenProperty(_barrel, "position", recoilPos, RecoilDuration * 0.3)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_barrel, "position", originalPos, RecoilDuration * 0.7)
            .SetEase(Tween.EaseType.Out);
    }

    private void PlayMuzzleFlash()
    {
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Restart();
            _muzzleFlash.Emitting = true;
        }

        if (_smoke != null)
        {
            _smoke.Restart();
            _smoke.Emitting = true;
        }

        SpawnLingeringHaze();
    }

    private void SpawnLingeringHaze()
    {
        if (_hazeMaterial == null)
        {
            _hazeMaterial = new StandardMaterial3D();
            _hazeMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
            _hazeMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
            _hazeMaterial.VertexColorUseAsAlbedo = true;
            _hazeMaterial.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
            _hazeMaterial.AlbedoTexture = CreateSoftCircleTexture();
        }

        if (_hazeMesh == null)
        {
            _hazeMesh = new QuadMesh();
            _hazeMesh.Size = new Vector2(4f, 4f);
            _hazeMesh.Material = _hazeMaterial;
        }

        var haze = new GpuParticles3D();
        haze.Name = "LingeringHaze";
        haze.Amount = 15;
        haze.OneShot = true;
        haze.Explosiveness = 0.7f;
        haze.Lifetime = 60f;
        haze.Randomness = 0.4f;
        haze.VisibilityAabb = new Aabb(new Vector3(-20, -5, -20), new Vector3(40, 50, 40));

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 1.5f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 30f;
        processMat.InitialVelocityMin = 0.3f;
        processMat.InitialVelocityMax = 0.8f;
        processMat.Gravity = new Vector3(0, 0.6f, 0);
        processMat.DampingMin = 0.2f;
        processMat.DampingMax = 0.5f;
        processMat.ScaleMin = 3f;
        processMat.ScaleMax = 7f;

        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 0.4f));
        scaleCurve.AddPoint(new Vector2(0.3f, 0.7f));
        scaleCurve.AddPoint(new Vector2(0.7f, 0.9f));
        scaleCurve.AddPoint(new Vector2(1f, 1f));
        var scaleCurveTex = new CurveTexture();
        scaleCurveTex.Curve = scaleCurve;
        processMat.ScaleCurve = scaleCurveTex;

        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.1f, 0.6f, 1f };
        gradient.Colors = new Color[] {
            new Color(0.5f, 0.5f, 0.48f, 0.12f),
            new Color(0.45f, 0.45f, 0.43f, 0.1f),
            new Color(0.4f, 0.4f, 0.4f, 0.06f),
            new Color(0.38f, 0.38f, 0.38f, 0f)
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 1.5f;
        processMat.TurbulenceNoiseScale = 4f;
        processMat.TurbulenceNoiseSpeed = new Vector3(0.3f, 0.2f, 0.3f);

        haze.ProcessMaterial = processMat;
        haze.DrawPass1 = _hazeMesh;

        GetTree().CurrentScene.AddChild(haze);
        haze.GlobalPosition = _muzzlePoint?.GlobalPosition ?? GlobalPosition;
        haze.Emitting = true;

        var timer = GetTree().CreateTimer(62f);
        timer.Timeout += () => {
            if (IsInstanceValid(haze)) haze.QueueFree();
        };
    }

    private void PlayFireSound()
    {
        AudioManager.Instance?.PlayCannonFire();
    }

    private static ImageTexture CreateSoftCircleTexture()
    {
        int size = 64;
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);

        float center = size / 2f;
        float maxDist = center;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp(1f - (dist / maxDist), 0f, 1f);
                alpha = alpha * alpha * (3f - 2f * alpha);

                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }

    #endregion

    protected override void ApplyDamageMaterial(ComponentHealth.DamageState state)
    {
        if (_cannonMesh == null) return;

        // Cannons use darker metal material
        var material = new StandardMaterial3D();
        material.Metallic = 0.8f;
        material.Roughness = 0.6f;

        material.AlbedoColor = state switch
        {
            ComponentHealth.DamageState.Healthy => new Color(0.3f, 0.3f, 0.35f), // Dark metal
            ComponentHealth.DamageState.Damaged => new Color(0.25f, 0.22f, 0.2f), // Rusty
            ComponentHealth.DamageState.Critical => new Color(0.15f, 0.1f, 0.1f), // Very dark
            _ => new Color(0.1f, 0.1f, 0.1f)
        };

        if (state >= ComponentHealth.DamageState.Critical)
        {
            material.EmissionEnabled = true;
            material.Emission = new Color(1f, 0.3f, 0.1f);
            material.EmissionEnergyMultiplier = 0.3f;
        }

        _cannonMesh.SetSurfaceOverrideMaterial(0, material);
    }
}
