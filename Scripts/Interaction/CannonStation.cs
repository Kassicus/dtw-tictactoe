using Godot;
using System;

/// <summary>
/// Interaction point for commanding a ship cannon.
/// </summary>
public partial class CannonStation : InteractionPoint
{
    [Export] public NodePath CannonPath { get; set; }

    private ShipCannon _cannon;
    private PlayerController _currentPlayer;
    private bool _isAiming = false;

    // Targeting
    private Vector3 _aimTarget = Vector3.Zero;
    private Node3D _targetIndicator;

    [Signal]
    public delegate void CannonCommandedEventHandler(ShipCannon cannon, Vector3 target);

    public override void _Ready()
    {
        base._Ready();

        InteractionPrompt = "Press E to command cannon";
        InteractionName = "Cannon Station";

        // Find the cannon
        if (CannonPath != null && !CannonPath.IsEmpty)
        {
            _cannon = GetNode<ShipCannon>(CannonPath);
        }
        else
        {
            // Try to find cannon in parent
            _cannon = GetParent() as ShipCannon;
            if (_cannon == null)
            {
                _cannon = GetParentOrNull<Node3D>()?.GetNodeOrNull<ShipCannon>(".");
            }
        }

        if (_cannon != null)
        {
            InteractionName = $"Command {_cannon.ComponentName}";
            UpdatePromptBasedOnCannonState();
        }

        // Create target indicator
        CreateTargetIndicator();
    }

    public override void _Process(double delta)
    {
        if (_isAiming && _currentPlayer != null)
        {
            UpdateAiming();
        }

        // Update prompt based on cannon state
        if (_cannon != null)
        {
            UpdatePromptBasedOnCannonState();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_isAiming) return;

        // Fire on click/confirm
        if (@event.IsActionPressed("fire") || @event.IsActionPressed("interact"))
        {
            FireCannon();
        }

        // Cancel aiming
        if (@event.IsActionPressed("ui_cancel"))
        {
            ExitAimingMode();
        }
    }

    public override void OnInteract(PlayerController player)
    {
        if (_cannon == null || _cannon.IsDestroyed)
        {
            GD.Print("Cannon is destroyed!");
            return;
        }

        if (!_cannon.CanFire)
        {
            GD.Print($"Cannon is reloading: {(_cannon.ReloadProgress * 100):F0}%");
            return;
        }

        base.OnInteract(player);
        _currentPlayer = player;
        EnterAimingMode();
    }

    private void EnterAimingMode()
    {
        _isAiming = true;

        // Show target indicator
        if (_targetIndicator != null)
        {
            _targetIndicator.Visible = true;
        }

        // Find enemy ship to target
        var battleManager = BattleManager.Instance;
        if (battleManager != null)
        {
            var playerShip = _cannon.GetParent() as Ship;
            while (playerShip == null && _cannon.GetParent() != null)
            {
                playerShip = _cannon.GetParent().GetParent() as Ship;
            }

            var enemy = battleManager.GetOpponent(playerShip);
            if (enemy != null)
            {
                _aimTarget = enemy.GetTargetPoint("center");
            }
        }

        GD.Print("Entered aiming mode. Click to fire, ESC to cancel.");
    }

    private void ExitAimingMode()
    {
        _isAiming = false;
        _currentPlayer = null;

        if (_targetIndicator != null)
        {
            _targetIndicator.Visible = false;
        }
    }

    private void UpdateAiming()
    {
        // Get camera aim direction
        var camera = _currentPlayer?.GetNode<PlayerCamera>("../PlayerCamera");
        if (camera == null)
        {
            camera = GetViewport().GetCamera3D() as PlayerCamera;
        }

        if (camera != null)
        {
            // Raycast from camera to find target point
            var spaceState = GetWorld3D().DirectSpaceState;
            var from = camera.GlobalPosition;
            var to = from + camera.GetAimDirection() * 200f;

            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

            var result = spaceState.IntersectRay(query);
            if (result.Count > 0)
            {
                _aimTarget = result["position"].AsVector3();
            }
            else
            {
                // Default to point along aim direction
                _aimTarget = from + camera.GetAimDirection() * 50f;
                _aimTarget.Y = 0; // Sea level
            }
        }

        // Update target indicator position
        if (_targetIndicator != null)
        {
            _targetIndicator.GlobalPosition = _aimTarget;
        }
    }

    private void FireCannon()
    {
        if (_cannon == null || !_cannon.CanFire)
        {
            GD.Print("Cannot fire cannon!");
            return;
        }

        _cannon.Fire(_aimTarget);
        EmitSignal(SignalName.CannonCommanded, _cannon, _aimTarget);

        GD.Print($"Fired cannon at {_aimTarget}");

        ExitAimingMode();
    }

    private void CreateTargetIndicator()
    {
        _targetIndicator = new Node3D();
        _targetIndicator.Name = "TargetIndicator";

        // Create a ring mesh
        var mesh = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.8f;
        torus.OuterRadius = 1.2f;
        torus.Rings = 16;
        torus.RingSegments = 32;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.2f, 0.2f, 0.7f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.3f, 0.2f);
        mat.EmissionEnergyMultiplier = 2f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        torus.Material = mat;

        mesh.Mesh = torus;
        mesh.Rotation = new Vector3(Mathf.Pi / 2f, 0, 0);

        _targetIndicator.AddChild(mesh);
        _targetIndicator.Visible = false;

        GetTree().CurrentScene.AddChild(_targetIndicator);
    }

    private void UpdatePromptBasedOnCannonState()
    {
        if (_cannon == null)
        {
            InteractionPrompt = "No cannon assigned";
            return;
        }

        if (_cannon.IsDestroyed)
        {
            InteractionPrompt = "Cannon destroyed";
            IsEnabled = false;
        }
        else if (!_cannon.CanFire)
        {
            InteractionPrompt = $"Reloading... {(_cannon.ReloadProgress * 100):F0}%";
        }
        else
        {
            InteractionPrompt = "Press E to aim cannon";
            IsEnabled = true;
        }
    }

    /// <summary>
    /// Get the associated cannon.
    /// </summary>
    public ShipCannon Cannon => _cannon;

    /// <summary>
    /// Whether currently in aiming mode.
    /// </summary>
    public bool IsAiming => _isAiming;
}
