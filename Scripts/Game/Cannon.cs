using Godot;
using System;

public partial class Cannon : Node3D
{
    [Export] public Player OwnerPlayer { get; set; } = Player.X;

    private Node3D _barrel;
    private Marker3D _muzzlePoint;
    private GpuParticles3D _muzzleFlash;
    private GpuParticles3D _smoke;

    // Shared haze material and mesh (created once, reused)
    private static StandardMaterial3D _hazeMaterial;
    private static QuadMesh _hazeMesh;

    private const float RecoilDistance = 0.5f;
    private const float RecoilDuration = 0.15f;

    // Arc height for the guided flight (how high above start/end the peak is)
    private const float ArcHeight = 8f;

    // Base flight duration - actual duration scales with distance
    private const float BaseFlightDuration = 1.2f;
    private const float FlightDurationPerUnit = 0.04f;

    public override void _Ready()
    {
        _barrel = GetNode<Node3D>("Barrel");
        _muzzlePoint = GetNode<Marker3D>("Barrel/MuzzlePoint");

        // Create particle systems programmatically (more reliable than scene file)
        CreateMuzzleFlash();
        CreateSmoke();

        GD.Print($"Cannon _Ready - MuzzleFlash: {_muzzleFlash != null}, Smoke: {_smoke != null}");
    }

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

        // Create process material
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

        // Align particles to velocity so they radiate outward
        processMat.ParticleFlagAlignY = true;

        // Color gradient - white to orange to transparent
        var gradient = new Gradient();
        gradient.SetOffset(0, 0f);
        gradient.SetOffset(1, 1f);
        gradient.SetColor(0, new Color(1f, 1f, 0.8f, 1f));
        gradient.SetColor(1, new Color(1f, 0.3f, 0.1f, 0f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        _muzzleFlash.ProcessMaterial = processMat;

        // Create thin rectangle mesh for flash streaks
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.15f, 1.2f); // Thin and tall

        // Material for the flash - no billboard so velocity alignment works
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

        // Position at muzzle
        _muzzleFlash.Position = _muzzlePoint.Position;
        _barrel.AddChild(_muzzleFlash);
    }

    private void CreateSmoke()
    {
        _smoke = new GpuParticles3D();
        _smoke.Name = "SmokeParticles";
        _smoke.Emitting = false;
        _smoke.Amount = 100;  // More particles to fill plume from barrel to end
        _smoke.OneShot = true;
        _smoke.Explosiveness = 0.95f;  // Big burst all at once, tiny trickle after
        _smoke.Lifetime = 3.5;  // Longer lifetime for slower dissipation
        _smoke.Randomness = 0.5f;  // 50% randomness in lifetime - creates size variation
        _smoke.VisibilityAabb = new Aabb(new Vector3(-15, -5, -20), new Vector3(30, 35, 40));  // Taller for rising smoke

        // Create process material for black powder plume
        var processMat = new ParticleProcessMaterial();

        // Emit from a box elongated along the barrel axis for directional plume
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
        processMat.EmissionBoxExtents = new Vector3(0.3f, 0.3f, 0.8f);  // Elongated along Z (barrel axis)

        // Direction straight out of barrel with more spread for separation
        processMat.Direction = new Vector3(0, 0, -1);
        processMat.Spread = 18f;  // More spread for particle separation

        // Shorter throw - burst stays closer to barrel
        processMat.InitialVelocityMin = 3f;   // Some smoke lingers at muzzle
        processMat.InitialVelocityMax = 10f;  // Reduced max for shorter throw

        // Strong upward gravity - smoke rises continuously as it fades
        // Damping will slow horizontal movement but gravity keeps Y moving
        processMat.Gravity = new Vector3(0.05f, 2.5f, 0);

        // High damping stops horizontal throw quickly, but gravity overcomes it vertically
        processMat.DampingMin = 5f;
        processMat.DampingMax = 8f;

        // Base scale - wide range for variation in final sizes (1.3 to 5.0)
        processMat.ScaleMin = 1.3f;
        processMat.ScaleMax = 5f;

        // Scale over lifetime - starts small, spreads earlier since throw is shorter
        // Curve goes 0 to 1, multiplied by ScaleMin/ScaleMax for final size variation
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 0.1f));      // Starts very small and tight
        scaleCurve.AddPoint(new Vector2(0.08f, 0.15f)); // Brief tight burst
        scaleCurve.AddPoint(new Vector2(0.18f, 0.25f)); // Starts spreading as it slows
        scaleCurve.AddPoint(new Vector2(0.3f, 0.4f));   // Expanding
        scaleCurve.AddPoint(new Vector2(0.45f, 0.55f)); // Growing
        scaleCurve.AddPoint(new Vector2(0.6f, 0.7f));   // More growth
        scaleCurve.AddPoint(new Vector2(0.8f, 0.88f));  // Still growing
        scaleCurve.AddPoint(new Vector2(1f, 1.0f));     // Full size at death - varies per particle
        var scaleCurveTex = new CurveTexture();
        scaleCurveTex.Curve = scaleCurve;
        processMat.ScaleCurve = scaleCurveTex;

        // Velocity over lifetime - decelerates horizontal, gravity handles vertical rise
        var velocityCurve = new Curve();
        velocityCurve.AddPoint(new Vector2(0, 1f));
        velocityCurve.AddPoint(new Vector2(0.12f, 0.4f));  // Quick initial slowdown
        velocityCurve.AddPoint(new Vector2(0.3f, 0.15f));  // Mostly stopped horizontally
        velocityCurve.AddPoint(new Vector2(0.6f, 0.05f));  // Drifting, gravity takes over
        velocityCurve.AddPoint(new Vector2(1f, 0.02f));    // Nearly still horizontally
        var velocityCurveTex = new CurveTexture();
        velocityCurveTex.Curve = velocityCurve;
        processMat.LinearAccelCurve = velocityCurveTex;
        processMat.LinearAccelMin = -8f;   // Strong horizontal deceleration
        processMat.LinearAccelMax = -4f;

        // Color gradient - black powder smoke: starts bright/tan from the flash,
        // continuously fades throughout lifetime as it grows and dissipates
        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.05f, 0.15f, 0.3f, 0.5f, 0.7f, 0.85f, 1f };
        gradient.Colors = new Color[] {
            new Color(1.0f, 0.95f, 0.8f, 0.9f),    // Bright tan/white from muzzle flash
            new Color(0.9f, 0.87f, 0.8f, 0.85f),   // Still bright, starting to fade
            new Color(0.75f, 0.73f, 0.68f, 0.75f), // Warm gray, fading
            new Color(0.6f, 0.58f, 0.55f, 0.6f),   // Medium gray, continuing fade
            new Color(0.5f, 0.48f, 0.46f, 0.45f),  // Darker gray, more transparent
            new Color(0.42f, 0.42f, 0.4f, 0.3f),   // Dark gray, quite faded
            new Color(0.38f, 0.38f, 0.38f, 0.15f), // Very faded
            new Color(0.35f, 0.35f, 0.35f, 0f)     // Gone
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        // Turbulence for billowing effect - adds spread especially as smoke slows
        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 5f;   // Strong turbulence for dramatic spread at end
        processMat.TurbulenceNoiseScale = 3f;      // Larger scale noise for bigger billows
        processMat.TurbulenceNoiseSpeed = new Vector3(1f, 0.6f, 1f);

        // Turbulence influence - kicks in earlier since particles stop sooner
        var turbulenceInfluenceCurve = new Curve();
        turbulenceInfluenceCurve.AddPoint(new Vector2(0, 0.15f));   // Slight turbulence at start
        turbulenceInfluenceCurve.AddPoint(new Vector2(0.15f, 0.35f)); // Builds quickly
        turbulenceInfluenceCurve.AddPoint(new Vector2(0.3f, 0.6f));  // Strong as particles slow
        turbulenceInfluenceCurve.AddPoint(new Vector2(0.5f, 0.8f));  // Near full
        turbulenceInfluenceCurve.AddPoint(new Vector2(1f, 1f));      // Full turbulence
        var turbulenceInfluenceTex = new CurveTexture();
        turbulenceInfluenceTex.Curve = turbulenceInfluenceCurve;
        processMat.TurbulenceInfluenceOverLife = turbulenceInfluenceTex;

        // Add some angular velocity for rotation variation
        processMat.AngularVelocityMin = -30f;
        processMat.AngularVelocityMax = 30f;

        _smoke.ProcessMaterial = processMat;

        // Create mesh for particles - larger base size for billowing clouds
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(3.5f, 3.5f);

        // Material for smoke with soft circular texture
        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;

        // Create a radial gradient texture for soft round particles
        var radialGradient = CreateSoftCircleTexture();
        mat.AlbedoTexture = radialGradient;

        quadMesh.Material = mat;

        _smoke.DrawPass1 = quadMesh;

        // Position at muzzle
        _smoke.Position = _muzzlePoint.Position;
        _barrel.AddChild(_smoke);
    }

    // Traverse speed for aiming (radians per second equivalent in duration)
    private const float AimDuration = 0.3f;

    // Pending fire data (used after aim completes)
    private Cell _pendingTarget;
    private PackedScene _pendingPieceScene;
    private Action _pendingOnLanded;
    private Tween _aimTween;

    /// <summary>
    /// Fire a piece at the target cell using guided flight (guaranteed accuracy).
    /// The cannon will aim first, then fire after traversing.
    /// </summary>
    public void Fire(Cell target, PackedScene pieceScene, Action onLanded)
    {
        // Store firing parameters for after aim completes
        _pendingTarget = target;
        _pendingPieceScene = pieceScene;
        _pendingOnLanded = onLanded;

        // Calculate aim point (with arc consideration)
        var endPos = target.GlobalPosition;
        var startPos = _muzzlePoint.GlobalPosition;
        var midPoint = (startPos + endPos) / 2f + new Vector3(0, ArcHeight * 0.5f, 0);

        // Aim the barrel, then fire when done
        AimBarrelAt(midPoint, OnAimComplete);
    }

    /// <summary>
    /// Called when barrel has finished aiming - now fire the piece.
    /// </summary>
    private void OnAimComplete()
    {
        if (_pendingTarget == null || _pendingPieceScene == null) return;

        var startPos = _muzzlePoint.GlobalPosition;
        var endPos = _pendingTarget.GlobalPosition;

        // Calculate flight duration based on distance
        float distance = startPos.DistanceTo(endPos);
        float flightDuration = BaseFlightDuration + (distance * FlightDurationPerUnit);

        // Create the piece
        var piece = _pendingPieceScene.Instantiate<GamePiece>();
        piece.OnLanded = _pendingOnLanded;
        piece.TargetCell = _pendingTarget;

        // Add to scene
        GetTree().CurrentScene.AddChild(piece);

        // Start guided flight - piece will follow arc and land exactly on target
        piece.StartGuidedFlight(startPos, endPos, ArcHeight, flightDuration);

        // Add slight random initial rotation for visual variety
        piece.RotateY((float)GD.RandRange(-0.5, 0.5));

        // Effects
        PlayRecoilAnimation();
        PlayMuzzleFlash();
        PlayFireSound();

        // Clear pending data
        _pendingTarget = null;
        _pendingPieceScene = null;
        _pendingOnLanded = null;
    }

    /// <summary>
    /// Smoothly rotate the barrel to aim at the target position, then call onComplete.
    /// </summary>
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

        // Calculate target rotation
        var currentBasis = _barrel.GlobalTransform.Basis;

        // Create a temporary transform to get the target rotation
        var tempTransform = _barrel.GlobalTransform;
        _barrel.LookAt(targetPos, Vector3.Up);
        var targetQuat = _barrel.GlobalTransform.Basis.GetRotationQuaternion();

        // Restore current rotation
        _barrel.GlobalTransform = new Transform3D(currentBasis, _barrel.GlobalPosition);
        var currentQuat = currentBasis.GetRotationQuaternion();

        // Cancel any existing aim tween
        _aimTween?.Kill();

        // Smoothly interpolate rotation
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
        // Trigger muzzle flash (bright, quick burst)
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Restart();
            _muzzleFlash.Emitting = true;
        }

        // Trigger smoke (billowing cloud)
        if (_smoke != null)
        {
            _smoke.Restart();
            _smoke.Emitting = true;
        }

        // Spawn lingering battlefield haze
        SpawnLingeringHaze();
    }

    /// <summary>
    /// Spawns a separate particle system for lingering battlefield haze that rises slowly.
    /// This creates the accumulating smoke effect during battle.
    /// </summary>
    private void SpawnLingeringHaze()
    {
        // Create shared material/mesh if not yet created
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

        // Create the haze particle system (spawned in world space, not attached to barrel)
        var haze = new GpuParticles3D();
        haze.Name = "LingeringHaze";
        haze.Amount = 15;  // Small amount per shot
        haze.OneShot = true;
        haze.Explosiveness = 0.7f;  // Most emit together
        haze.Lifetime = 60f;  // Very long lifetime - lingers in the air
        haze.Randomness = 0.4f;
        haze.VisibilityAabb = new Aabb(new Vector3(-20, -5, -20), new Vector3(40, 50, 40));

        // Process material for slow-rising haze
        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 1.5f;  // Spread out emission

        // Slight outward push, mostly rises
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 30f;
        processMat.InitialVelocityMin = 0.3f;
        processMat.InitialVelocityMax = 0.8f;

        // Constant slow rise - smoke drifts upward throughout lifetime
        processMat.Gravity = new Vector3(0, 0.6f, 0);

        // Very light damping - keeps drifting
        processMat.DampingMin = 0.2f;
        processMat.DampingMax = 0.5f;

        // Large, wispy particles
        processMat.ScaleMin = 3f;
        processMat.ScaleMax = 7f;

        // Grow slightly over lifetime
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 0.4f));
        scaleCurve.AddPoint(new Vector2(0.3f, 0.7f));
        scaleCurve.AddPoint(new Vector2(0.7f, 0.9f));
        scaleCurve.AddPoint(new Vector2(1f, 1f));
        var scaleCurveTex = new CurveTexture();
        scaleCurveTex.Curve = scaleCurve;
        processMat.ScaleCurve = scaleCurveTex;

        // Very faint, slowly fading - this is the haze that accumulates
        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.1f, 0.6f, 1f };
        gradient.Colors = new Color[] {
            new Color(0.5f, 0.5f, 0.48f, 0.12f),  // Starts faint
            new Color(0.45f, 0.45f, 0.43f, 0.1f), // Slight fade
            new Color(0.4f, 0.4f, 0.4f, 0.06f),   // Fading
            new Color(0.38f, 0.38f, 0.38f, 0f)    // Gone
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        // Light turbulence for gentle drift
        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 1.5f;
        processMat.TurbulenceNoiseScale = 4f;
        processMat.TurbulenceNoiseSpeed = new Vector3(0.3f, 0.2f, 0.3f);

        haze.ProcessMaterial = processMat;
        haze.DrawPass1 = _hazeMesh;

        // Position at muzzle world position (not attached to barrel)
        haze.GlobalPosition = _muzzlePoint.GlobalPosition;

        // Add to scene root so it persists independently
        GetTree().CurrentScene.AddChild(haze);

        // Start emitting
        haze.Emitting = true;

        // Auto-remove after particles are done (lifetime + buffer)
        var timer = GetTree().CreateTimer(62f);
        timer.Timeout += () => {
            if (IsInstanceValid(haze))
            {
                haze.QueueFree();
            }
        };
    }

    private void PlayFireSound()
    {
        AudioManager.Instance?.PlayCannonFire();
    }

    /// <summary>
    /// Create a soft circular gradient texture for round smoke particles.
    /// </summary>
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

                // Soft falloff from center to edge
                float alpha = Mathf.Clamp(1f - (dist / maxDist), 0f, 1f);
                // Apply smoothstep for softer edges
                alpha = alpha * alpha * (3f - 2f * alpha);

                image.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        var texture = ImageTexture.CreateFromImage(image);
        return texture;
    }
}
