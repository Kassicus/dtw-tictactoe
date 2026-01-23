using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main ship controller that manages all ship components.
/// </summary>
public partial class Ship : Node3D
{
    public enum ShipSide
    {
        Player,
        Enemy
    }

    [Export] public string ShipName { get; set; } = "HMS Victory";
    [Export] public ShipSide Side { get; set; } = ShipSide.Player;

    // Component collections
    private List<HullSection> _hullSections = new();
    private List<Mast> _masts = new();
    private List<ShipCannon> _portCannons = new();
    private List<ShipCannon> _starboardCannons = new();

    // Ship state
    public float TotalHullIntegrity { get; private set; } = 1f;
    public float SailEffectiveness { get; private set; } = 1f;
    public bool IsSinking { get; private set; } = false;
    public bool IsSunk { get; private set; } = false;

    // Listing/tilting due to damage
    private Vector3 _listRotation = Vector3.Zero;
    private const float MaxListAngle = 25f; // Degrees
    private const float SinkingListAngle = 40f;

    // Sinking state
    private float _sinkProgress = 0f;
    private const float SinkThreshold = 0.3f; // Start sinking at 30% hull integrity
    private const float SinkSpeed = 0.5f; // Units per second when sinking

    // Damage system
    private DamageSystem _damageSystem;

    [Signal]
    public delegate void ShipDamagedEventHandler(Ship ship, float totalIntegrity);

    [Signal]
    public delegate void ShipSinkingEventHandler(Ship ship);

    [Signal]
    public delegate void ShipSunkEventHandler(Ship ship);

    [Signal]
    public delegate void CannonsFiredEventHandler(Ship ship, ShipCannon.CannonSide side);

    public override void _Ready()
    {
        // Add to ships group for easy lookup
        AddToGroup("ships");

        // Create damage system
        _damageSystem = new DamageSystem();
        AddChild(_damageSystem);

        // Find all components in children
        FindComponents();

        // Connect component signals
        ConnectComponentSignals();

        GD.Print($"Ship '{ShipName}' initialized with {_hullSections.Count} hull sections, " +
                 $"{_masts.Count} masts, {_portCannons.Count} port cannons, {_starboardCannons.Count} starboard cannons");
    }

    public override void _Process(double delta)
    {
        // Update ship listing based on hull damage
        UpdateListing((float)delta);

        // Handle sinking
        if (IsSinking && !IsSunk)
        {
            UpdateSinking((float)delta);
        }
    }

    private void FindComponents()
    {
        // Recursively find all ship components
        FindComponentsRecursive(this);
    }

    private void FindComponentsRecursive(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is HullSection hull)
            {
                _hullSections.Add(hull);
            }
            else if (child is Mast mast)
            {
                _masts.Add(mast);
            }
            else if (child is ShipCannon cannon)
            {
                if (cannon.Side == ShipCannon.CannonSide.Port)
                    _portCannons.Add(cannon);
                else
                    _starboardCannons.Add(cannon);
            }

            FindComponentsRecursive(child);
        }
    }

    private void ConnectComponentSignals()
    {
        foreach (var hull in _hullSections)
        {
            hull.ComponentDamaged += OnComponentDamaged;
            hull.ComponentDestroyed += OnComponentDestroyed;
            hull.WaterIngressChanged += OnWaterIngressChanged;
        }

        foreach (var mast in _masts)
        {
            mast.ComponentDamaged += OnComponentDamaged;
            mast.ComponentDestroyed += OnComponentDestroyed;
            mast.MastCollapsed += OnMastCollapsed;
        }

        foreach (var cannon in _portCannons.Concat(_starboardCannons))
        {
            cannon.ComponentDamaged += OnComponentDamaged;
            cannon.ComponentDestroyed += OnComponentDestroyed;
        }
    }

    private void OnComponentDamaged(ShipComponent component, float damage)
    {
        RecalculateShipState();
        EmitSignal(SignalName.ShipDamaged, this, TotalHullIntegrity);
    }

    private void OnComponentDestroyed(ShipComponent component)
    {
        GD.Print($"{ShipName}: {component.ComponentName} destroyed!");
        RecalculateShipState();
    }

    private void OnWaterIngressChanged(HullSection section, float ingressRate)
    {
        // Water ingress accelerates sinking
        if (ingressRate > 0 && TotalHullIntegrity < SinkThreshold && !IsSinking)
        {
            StartSinking();
        }
    }

    private void OnMastCollapsed(Mast mast)
    {
        GD.Print($"{ShipName}: {mast.ComponentName} has collapsed!");
        RecalculateSailEffectiveness();
    }

    private void RecalculateShipState()
    {
        // Calculate total hull integrity
        if (_hullSections.Count > 0)
        {
            float totalHealth = 0f;
            float totalMaxHealth = 0f;

            foreach (var hull in _hullSections)
            {
                totalHealth += hull.CurrentHealth;
                totalMaxHealth += hull.MaxHealth;
            }

            TotalHullIntegrity = totalMaxHealth > 0 ? totalHealth / totalMaxHealth : 0f;
        }

        // Check for sinking condition
        if (TotalHullIntegrity <= SinkThreshold && !IsSinking)
        {
            StartSinking();
        }

        RecalculateSailEffectiveness();
    }

    private void RecalculateSailEffectiveness()
    {
        if (_masts.Count == 0)
        {
            SailEffectiveness = 0f;
            return;
        }

        float totalEffectiveness = 0f;
        foreach (var mast in _masts)
        {
            totalEffectiveness += mast.GetSpeedEffectiveness();
        }

        SailEffectiveness = totalEffectiveness;
    }

    private void UpdateListing(float delta)
    {
        // Calculate target list based on hull damage
        Vector3 targetList = Vector3.Zero;

        foreach (var hull in _hullSections)
        {
            if (hull.DamageState >= ComponentHealth.DamageState.Damaged)
            {
                Vector3 listDir = hull.GetListDirection();
                float severity = hull.GetListSeverity();
                targetList += listDir * severity * MaxListAngle;
            }
        }

        // Add sinking list
        if (IsSinking)
        {
            targetList.X = Mathf.DegToRad(SinkingListAngle * (_sinkProgress / 10f));
        }

        // Clamp listing
        targetList.X = Mathf.Clamp(targetList.X, -Mathf.DegToRad(SinkingListAngle), Mathf.DegToRad(SinkingListAngle));
        targetList.Z = Mathf.Clamp(targetList.Z, -Mathf.DegToRad(MaxListAngle), Mathf.DegToRad(MaxListAngle));

        // Smoothly interpolate to target list
        _listRotation = _listRotation.Lerp(targetList, delta * 0.5f);

        // Apply rotation (keep Y rotation intact)
        var currentRotation = Rotation;
        Rotation = new Vector3(_listRotation.X, currentRotation.Y, _listRotation.Z);
    }

    private void StartSinking()
    {
        if (IsSinking) return;

        IsSinking = true;
        EmitSignal(SignalName.ShipSinking, this);
        GD.Print($"{ShipName} is sinking!");
    }

    private void UpdateSinking(float delta)
    {
        _sinkProgress += SinkSpeed * delta * (1f - TotalHullIntegrity); // Sink faster with more damage

        // Move ship down
        var pos = Position;
        pos.Y -= SinkSpeed * delta;
        Position = pos;

        // Check if fully sunk
        if (pos.Y < -5f)
        {
            IsSunk = true;
            EmitSignal(SignalName.ShipSunk, this);
            GD.Print($"{ShipName} has sunk!");
        }
    }

    #region Combat Methods

    /// <summary>
    /// Fire all cannons on the specified side at a single target point.
    /// </summary>
    public void FireBroadside(ShipCannon.CannonSide side, Vector3 targetPos, Action<Vector3> onImpact = null)
    {
        // Use the distributed broadside with a default spread
        FireBroadsideAtShip(side, null, targetPos, onImpact);
    }

    /// <summary>
    /// Fire all cannons on the specified side, distributing shots across the target ship.
    /// </summary>
    public void FireBroadsideAtShip(ShipCannon.CannonSide side, Ship targetShip, Vector3 fallbackTarget, Action<Vector3> onImpact = null)
    {
        var cannons = side == ShipCannon.CannonSide.Port ? _portCannons : _starboardCannons;
        var readyCannons = cannons.Where(c => c.CanFire).ToList();

        if (readyCannons.Count == 0) return;

        // Generate distributed target points along the enemy ship
        var targetPoints = GenerateBroadsideTargets(targetShip, readyCannons.Count, fallbackTarget);

        int firedCount = 0;
        float delay = 0f;

        for (int i = 0; i < readyCannons.Count; i++)
        {
            var cannon = readyCannons[i];
            var target = targetPoints[i];

            // Add slight random offset for realism
            target += new Vector3(
                (float)GD.RandRange(-0.5, 0.5),
                (float)GD.RandRange(-0.3, 0.3),
                (float)GD.RandRange(-0.5, 0.5)
            );

            // Stagger fire slightly for effect
            var timer = GetTree().CreateTimer(delay);
            var cannonRef = cannon;
            var targetRef = target;
            timer.Timeout += () => cannonRef.Fire(targetRef, onImpact);

            delay += 0.12f; // 120ms between each cannon for ripple effect
            firedCount++;
        }

        if (firedCount > 0)
        {
            EmitSignal(SignalName.CannonsFired, this, (int)side);
            GD.Print($"{ShipName} fired {firedCount} cannons from {side} side in a broadside!");
        }
    }

    /// <summary>
    /// Generate target points distributed along the enemy ship for a broadside.
    /// </summary>
    private List<Vector3> GenerateBroadsideTargets(Ship targetShip, int cannonCount, Vector3 fallbackTarget)
    {
        var targets = new List<Vector3>();

        if (targetShip == null)
        {
            // No target ship, spread around the fallback point
            for (int i = 0; i < cannonCount; i++)
            {
                float spread = (i - (cannonCount - 1) / 2f) * 2f;
                targets.Add(fallbackTarget + new Vector3(0, 0, spread));
            }
            return targets;
        }

        // Get the target ship's dimensions and orientation
        var shipForward = targetShip.GlobalTransform.Basis.Z;
        var shipCenter = targetShip.GlobalPosition;

        // Ship is roughly 16 units long, distribute shots along its length
        float shipLength = 14f; // Slightly less than full length for better hits
        float shipHeight = 2f;  // Target the hull, not too high

        // Different target heights - mix of waterline, hull, and deck
        float[] heightOffsets = { -0.5f, 0f, 0.5f, 1f, 1.5f, 0f, -0.5f, 1f };

        for (int i = 0; i < cannonCount; i++)
        {
            // Distribute along the ship's length
            float t = cannonCount > 1 ? (float)i / (cannonCount - 1) : 0.5f;
            float zOffset = Mathf.Lerp(-shipLength / 2f, shipLength / 2f, t);

            // Vary the height
            float yOffset = heightOffsets[i % heightOffsets.Length];

            // Calculate target position in world space
            var targetPoint = shipCenter + shipForward * zOffset + Vector3.Up * yOffset;

            targets.Add(targetPoint);
        }

        return targets;
    }

    /// <summary>
    /// Fire a single cannon at a target.
    /// </summary>
    public bool FireCannon(int index, ShipCannon.CannonSide side, Vector3 targetPos, Action<Vector3> onImpact = null)
    {
        var cannons = side == ShipCannon.CannonSide.Port ? _portCannons : _starboardCannons;

        if (index < 0 || index >= cannons.Count) return false;

        var cannon = cannons[index];
        if (!cannon.CanFire) return false;

        cannon.Fire(targetPos, onImpact);
        return true;
    }

    /// <summary>
    /// Get all cannons that are ready to fire on the specified side.
    /// </summary>
    public IEnumerable<ShipCannon> GetReadyCannons(ShipCannon.CannonSide side)
    {
        var cannons = side == ShipCannon.CannonSide.Port ? _portCannons : _starboardCannons;
        return cannons.Where(c => c.CanFire);
    }

    /// <summary>
    /// Get number of operational cannons on a side.
    /// </summary>
    public int GetOperationalCannonCount(ShipCannon.CannonSide side)
    {
        var cannons = side == ShipCannon.CannonSide.Port ? _portCannons : _starboardCannons;
        return cannons.Count(c => !c.IsDestroyed);
    }

    #endregion

    #region Component Access

    /// <summary>
    /// Get all ship components.
    /// </summary>
    public IEnumerable<ShipComponent> GetAllComponents()
    {
        foreach (var hull in _hullSections) yield return hull;
        foreach (var mast in _masts) yield return mast;
        foreach (var cannon in _portCannons) yield return cannon;
        foreach (var cannon in _starboardCannons) yield return cannon;
    }

    /// <summary>
    /// Get hull sections.
    /// </summary>
    public IReadOnlyList<HullSection> HullSections => _hullSections;

    /// <summary>
    /// Get masts.
    /// </summary>
    public IReadOnlyList<Mast> Masts => _masts;

    /// <summary>
    /// Get port cannons.
    /// </summary>
    public IReadOnlyList<ShipCannon> PortCannons => _portCannons;

    /// <summary>
    /// Get starboard cannons.
    /// </summary>
    public IReadOnlyList<ShipCannon> StarboardCannons => _starboardCannons;

    #endregion

    #region Utility

    /// <summary>
    /// Get a point on the ship for targeting (e.g., center mass).
    /// </summary>
    public Vector3 GetTargetPoint(string area = "center")
    {
        return area.ToLower() switch
        {
            "bow" => GlobalPosition + GlobalTransform.Basis.Z * -6f + Vector3.Up * 0.5f,
            "stern" => GlobalPosition + GlobalTransform.Basis.Z * 6f + Vector3.Up * 0.5f,
            "port" => GlobalPosition + GlobalTransform.Basis.X * -3f + Vector3.Up * 0.5f,
            "starboard" => GlobalPosition + GlobalTransform.Basis.X * 3f + Vector3.Up * 0.5f,
            "port_bow" => GlobalPosition + GlobalTransform.Basis.Z * -4f + GlobalTransform.Basis.X * -2f + Vector3.Up * 0.3f,
            "port_stern" => GlobalPosition + GlobalTransform.Basis.Z * 4f + GlobalTransform.Basis.X * -2f + Vector3.Up * 0.3f,
            "starboard_bow" => GlobalPosition + GlobalTransform.Basis.Z * -4f + GlobalTransform.Basis.X * 2f + Vector3.Up * 0.3f,
            "starboard_stern" => GlobalPosition + GlobalTransform.Basis.Z * 4f + GlobalTransform.Basis.X * 2f + Vector3.Up * 0.3f,
            "waterline" => GlobalPosition + Vector3.Up * -0.5f,
            "deck" => GlobalPosition + Vector3.Up * 0.5f,
            "mast" => GlobalPosition + Vector3.Up * 6f,
            _ => GlobalPosition + Vector3.Up * 0.5f // Center hull, not too high
        };
    }

    /// <summary>
    /// Reset ship to full health (for testing/new game).
    /// </summary>
    public void ResetShip()
    {
        IsSinking = false;
        IsSunk = false;
        _sinkProgress = 0f;
        _listRotation = Vector3.Zero;
        Position = new Vector3(Position.X, 0, Position.Z);

        // Reset all component health
        foreach (var component in GetAllComponents())
        {
            var health = component.GetNode<ComponentHealth>("ComponentHealth");
            health?.ResetHealth();
        }

        RecalculateShipState();
    }

    #endregion
}
