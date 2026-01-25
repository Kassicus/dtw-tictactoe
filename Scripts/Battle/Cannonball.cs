using Godot;
using System;

/// <summary>
/// Cannonball projectile that travels in an arc to its target.
/// Adapted from GamePiece.cs guided flight system.
/// </summary>
public partial class Cannonball : Node3D
{
    [Export] public float Damage { get; set; } = 25f;
    [Export] public float Radius { get; set; } = 0.15f;

    // Flight properties
    private Vector3 _startPos;
    private Vector3 _endPos;
    private float _arcHeight;
    private float _flightDuration;
    private float _flightProgress;
    private bool _isFlying = false;

    // Track which ship fired this cannonball to prevent friendly fire
    public Ship FiringShip { get; set; }
    public Ship.ShipSide FiringSide { get; set; } = Ship.ShipSide.Player;

    // Callback when hitting target
    public Action<Vector3> OnImpact { get; set; }

    // Visual mesh
    private MeshInstance3D _mesh;
    private GpuParticles3D _trail;

    public override void _Ready()
    {
        CreateVisuals();
    }

    public override void _Process(double delta)
    {
        if (!_isFlying) return;

        _flightProgress += (float)delta;
        float t = Mathf.Clamp(_flightProgress / _flightDuration, 0f, 1f);

        // Update position along arc
        GlobalPosition = CalculateArcPosition(t);

        // Rotate to face direction of travel
        if (t < 0.99f)
        {
            float nextT = Mathf.Min(t + 0.01f, 1f);
            var nextPos = CalculateArcPosition(nextT);
            var direction = (nextPos - GlobalPosition).Normalized();
            if (direction.LengthSquared() > 0.001f)
            {
                LookAt(GlobalPosition + direction, Vector3.Up);
            }
        }

        // Check for landing
        if (t >= 1f)
        {
            OnLand();
        }
    }

    /// <summary>
    /// Start the cannonball flight to target.
    /// </summary>
    public void StartFlight(Vector3 startPos, Vector3 endPos, float arcHeight, float duration)
    {
        _startPos = startPos;
        _endPos = endPos;
        _arcHeight = arcHeight;
        _flightDuration = duration;
        _flightProgress = 0f;
        _isFlying = true;

        GlobalPosition = startPos;

        // Start trail
        if (_trail != null)
        {
            _trail.Emitting = true;
        }
    }

    /// <summary>
    /// Calculate position on parabolic arc at time t (0-1).
    /// </summary>
    private Vector3 CalculateArcPosition(float t)
    {
        // Linear interpolation for X and Z
        float x = Mathf.Lerp(_startPos.X, _endPos.X, t);
        float z = Mathf.Lerp(_startPos.Z, _endPos.Z, t);

        // Parabolic arc for Y
        float baseY = Mathf.Lerp(_startPos.Y, _endPos.Y, t);
        float arcOffset = 4f * t * (1f - t) * _arcHeight;
        float y = baseY + arcOffset;

        return new Vector3(x, y, z);
    }

    private void OnLand()
    {
        _isFlying = false;

        if (_trail != null)
        {
            _trail.Emitting = false;
        }

        // Spawn impact effect
        SpawnImpactEffect();

        // Invoke callback
        OnImpact?.Invoke(_endPos);

        // Check for hits (simple sphere overlap)
        CheckForHits();

        // Remove cannonball after short delay (let particles finish)
        var timer = GetTree().CreateTimer(0.5f);
        timer.Timeout += QueueFree;
    }

    private void CheckForHits()
    {
        // Use physics query to find overlapping bodies and areas
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = new PhysicsShapeQueryParameters3D();

        var sphere = new SphereShape3D();
        sphere.Radius = 2f; // Impact radius - larger for better hit detection
        query.Shape = sphere;
        query.Transform = new Transform3D(Basis.Identity, GlobalPosition);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        query.CollisionMask = 0xFFFFFFFF; // Check all layers

        var results = spaceState.IntersectShape(query, 32); // Max 32 results

        bool hitSomething = false;
        foreach (var result in results)
        {
            if (result.TryGetValue("collider", out var colliderVariant))
            {
                var collider = colliderVariant.As<Node>();
                // Check if we hit a ship component
                var component = FindShipComponent(collider);
                if (component != null && !hitSomething)
                {
                    component.TakeDamage(Damage);
                    GD.Print($"Cannonball hit {component.ComponentName} for {Damage} damage!");
                    hitSomething = true; // Only damage one component per cannonball
                }
            }
        }

        // If no physics hit, try direct distance check to ships
        if (!hitSomething)
        {
            CheckDirectShipHit();
        }
    }

    private void CheckDirectShipHit()
    {
        // Fallback: check distance to all ships in scene
        var ships = GetTree().GetNodesInGroup("ships");
        if (ships.Count == 0)
        {
            // Try to find ships by type
            foreach (var node in GetTree().CurrentScene.GetChildren())
            {
                if (node is Ship ship)
                {
                    CheckShipHit(ship);
                }
                // Check children too
                foreach (var child in node.GetChildren())
                {
                    if (child is Ship childShip)
                    {
                        CheckShipHit(childShip);
                    }
                }
            }
        }
        else
        {
            foreach (var shipNode in ships)
            {
                if (shipNode is Ship ship)
                {
                    CheckShipHit(ship);
                }
            }
        }
    }

    private void CheckShipHit(Ship ship)
    {
        // Skip friendly fire - check by side to be robust
        if (ship.Side == FiringSide)
        {
            GD.Print($"Cannonball: Preventing friendly fire on {ship.ShipName} (same side: {FiringSide})");
            return;
        }

        float distance = GlobalPosition.DistanceTo(ship.GlobalPosition);
        if (distance < 10f) // Within ship bounds
        {
            // Find closest hull section
            ShipComponent closestComponent = null;
            float closestDist = float.MaxValue;

            foreach (var component in ship.GetAllComponents())
            {
                float compDist = GlobalPosition.DistanceTo(component.GlobalPosition);
                if (compDist < closestDist)
                {
                    closestDist = compDist;
                    closestComponent = component;
                }
            }

            if (closestComponent != null && closestDist < 8f)
            {
                closestComponent.TakeDamage(Damage);
                GD.Print($"Cannonball hit {closestComponent.ComponentName} on {ship.ShipName} for {Damage} damage!");
            }
        }
    }

    private ShipComponent FindShipComponent(Node node)
    {
        var current = node;
        while (current != null)
        {
            if (current is ShipComponent component)
            {
                // Check for friendly fire - skip if this component belongs to a ship on the same side
                var parentShip = FindParentShip(component);
                if (parentShip != null && parentShip.Side == FiringSide)
                {
                    GD.Print($"Cannonball: Preventing friendly fire on component {component.ComponentName} (same side: {FiringSide})");
                    return null; // Ignore friendly fire
                }
                return component;
            }
            current = current.GetParent();
        }
        return null;
    }

    private Ship FindParentShip(Node node)
    {
        var current = node;
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

    private void CreateVisuals()
    {
        // Create sphere mesh for cannonball
        _mesh = new MeshInstance3D();
        var sphere = new SphereMesh();
        sphere.Radius = Radius;
        sphere.Height = Radius * 2f;
        sphere.RadialSegments = 16;
        sphere.Rings = 8;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f); // Dark iron
        mat.Metallic = 0.9f;
        mat.Roughness = 0.4f;
        sphere.Material = mat;

        _mesh.Mesh = sphere;
        AddChild(_mesh);

        // Create smoke trail
        CreateTrailEffect();
    }

    private void CreateTrailEffect()
    {
        _trail = new GpuParticles3D();
        _trail.Name = "Trail";
        _trail.Amount = 30;
        _trail.Lifetime = 0.5f;
        _trail.Emitting = false;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Point;
        processMat.Direction = new Vector3(0, 0, 0);
        processMat.Spread = 10f;
        processMat.InitialVelocityMin = 0f;
        processMat.InitialVelocityMax = 0.5f;
        processMat.Gravity = new Vector3(0, 0.3f, 0);
        processMat.ScaleMin = 0.1f;
        processMat.ScaleMax = 0.3f;

        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.4f, 0.35f, 0.3f, 0.5f));
        gradient.SetColor(1, new Color(0.3f, 0.3f, 0.3f, 0f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        _trail.ProcessMaterial = processMat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.3f, 0.3f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        quadMesh.Material = mat;

        _trail.DrawPass1 = quadMesh;
        AddChild(_trail);
    }

    private void SpawnImpactEffect()
    {
        // Determine if we hit water or ship (simple height check for now)
        bool hitWater = _endPos.Y < 0.5f;

        if (hitWater)
        {
            SpawnWaterSplash();
        }
        else
        {
            SpawnWoodImpact();
        }
    }

    private void SpawnWaterSplash()
    {
        var splash = new GpuParticles3D();
        splash.Name = "WaterSplash";
        splash.Amount = 60;
        splash.OneShot = true;
        splash.Lifetime = 1.5f;
        splash.Explosiveness = 0.9f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.3f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 60f;
        processMat.InitialVelocityMin = 5f;
        processMat.InitialVelocityMax = 12f;
        processMat.Gravity = new Vector3(0, -15f, 0);
        processMat.ScaleMin = 0.1f;
        processMat.ScaleMax = 0.4f;

        // Blue water color
        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.5f, 1f };
        gradient.Colors = new Color[] {
            new Color(0.6f, 0.8f, 1f, 0.8f),
            new Color(0.4f, 0.6f, 0.9f, 0.5f),
            new Color(0.3f, 0.5f, 0.8f, 0f)
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        splash.ProcessMaterial = processMat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(0.4f, 0.4f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        quadMesh.Material = mat;

        splash.DrawPass1 = quadMesh;

        GetTree().CurrentScene.AddChild(splash);
        splash.GlobalPosition = _endPos;
        splash.Emitting = true;

        var timer = GetTree().CreateTimer(3f);
        timer.Timeout += () => {
            if (IsInstanceValid(splash)) splash.QueueFree();
        };
    }

    private void SpawnWoodImpact()
    {
        var impact = new GpuParticles3D();
        impact.Name = "WoodImpact";
        impact.Amount = 40;
        impact.OneShot = true;
        impact.Lifetime = 1f;
        impact.Explosiveness = 0.95f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.2f;
        processMat.Direction = new Vector3(0, 0, 0);
        processMat.Spread = 180f;
        processMat.InitialVelocityMin = 3f;
        processMat.InitialVelocityMax = 10f;
        processMat.Gravity = new Vector3(0, -12f, 0);
        processMat.ScaleMin = 0.05f;
        processMat.ScaleMax = 0.2f;
        processMat.AngularVelocityMin = -360f;
        processMat.AngularVelocityMax = 360f;

        // Wood splinter colors
        var gradient = new Gradient();
        gradient.Offsets = new float[] { 0f, 0.3f, 1f };
        gradient.Colors = new Color[] {
            new Color(0.8f, 0.6f, 0.3f, 1f),  // Light wood
            new Color(0.6f, 0.4f, 0.2f, 0.8f), // Medium wood
            new Color(0.4f, 0.3f, 0.15f, 0f)   // Fading
        };
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        impact.ProcessMaterial = processMat;

        // Use small boxes for wood splinters
        var boxMesh = new BoxMesh();
        boxMesh.Size = new Vector3(0.08f, 0.25f, 0.04f);

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(0.6f, 0.4f, 0.25f);
        mat.VertexColorUseAsAlbedo = true;
        boxMesh.Material = mat;

        impact.DrawPass1 = boxMesh;

        GetTree().CurrentScene.AddChild(impact);
        impact.GlobalPosition = _endPos;
        impact.Emitting = true;

        // Also spawn smoke puff
        SpawnImpactSmoke();

        var timer = GetTree().CreateTimer(3f);
        timer.Timeout += () => {
            if (IsInstanceValid(impact)) impact.QueueFree();
        };
    }

    private void SpawnImpactSmoke()
    {
        var smoke = new GpuParticles3D();
        smoke.Name = "ImpactSmoke";
        smoke.Amount = 20;
        smoke.OneShot = true;
        smoke.Lifetime = 2f;
        smoke.Explosiveness = 0.8f;

        var processMat = new ParticleProcessMaterial();
        processMat.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        processMat.EmissionSphereRadius = 0.3f;
        processMat.Direction = new Vector3(0, 1, 0);
        processMat.Spread = 40f;
        processMat.InitialVelocityMin = 1f;
        processMat.InitialVelocityMax = 3f;
        processMat.Gravity = new Vector3(0, 1f, 0);
        processMat.ScaleMin = 0.3f;
        processMat.ScaleMax = 1.5f;

        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.5f, 0.45f, 0.4f, 0.6f));
        gradient.SetColor(1, new Color(0.4f, 0.4f, 0.4f, 0f));
        var gradientTex = new GradientTexture1D();
        gradientTex.Gradient = gradient;
        processMat.ColorRamp = gradientTex;

        processMat.TurbulenceEnabled = true;
        processMat.TurbulenceNoiseStrength = 2f;

        smoke.ProcessMaterial = processMat;

        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(1.5f, 1.5f);

        var mat = new StandardMaterial3D();
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        mat.VertexColorUseAsAlbedo = true;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        quadMesh.Material = mat;

        smoke.DrawPass1 = quadMesh;

        GetTree().CurrentScene.AddChild(smoke);
        smoke.GlobalPosition = _endPos;
        smoke.Emitting = true;

        var timer = GetTree().CreateTimer(4f);
        timer.Timeout += () => {
            if (IsInstanceValid(smoke)) smoke.QueueFree();
        };
    }
}
