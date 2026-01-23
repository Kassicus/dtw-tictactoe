using Godot;
using System;

public partial class Cannon : Node3D
{
    [Export] public Player OwnerPlayer { get; set; } = Player.X;

    private Node3D _barrel;
    private Marker3D _muzzlePoint;
    private GpuParticles3D _muzzleFlash;
    private GpuParticles3D _smoke;

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
        _smoke.Amount = 40;
        _smoke.OneShot = true;
        _smoke.Explosiveness = 0.85f;
        _smoke.Lifetime = 2.0;
        _smoke.VisibilityAabb = new Aabb(new Vector3(-10, -5, -10), new Vector3(20, 15, 20));

        // Create process material
        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.5f;
        processMat.Direction = new Vector3(0, 0.3f, -1);
        processMat.Spread = 40f;
        processMat.InitialVelocityMin = 5f;
        processMat.InitialVelocityMax = 10f;
        processMat.Gravity = new Vector3(0, 2f, 0); // Rises slightly
        processMat.DampingMin = 2f;
        processMat.DampingMax = 4f;
        processMat.ScaleMin = 2f;
        processMat.ScaleMax = 4f;

        // Scale over lifetime - grow as they rise
        var scaleCurve = new Curve();
        scaleCurve.AddPoint(new Vector2(0, 0.3f));
        scaleCurve.AddPoint(new Vector2(0.3f, 0.7f));
        scaleCurve.AddPoint(new Vector2(1f, 1f));
        var scaleCurveTex = new CurveTexture();
        scaleCurveTex.Curve = scaleCurve;
        processMat.ScaleCurve = scaleCurveTex;

        // Color gradient - light gray to darker gray, fading out
        var gradient = new Gradient();
        gradient.SetOffset(0, 0f);
        gradient.SetOffset(1, 1f);
        gradient.SetColor(0, new Color(0.9f, 0.9f, 0.85f, 0.8f));
        gradient.SetColor(1, new Color(0.4f, 0.4f, 0.4f, 0f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        // Add some turbulence for billowing effect
        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 2f;
        processMat.TurbulenceNoiseScale = 1.5f;

        _smoke.ProcessMaterial = processMat;

        // Create mesh for particles
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(2.5f, 2.5f);

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
