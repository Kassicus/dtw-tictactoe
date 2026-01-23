using Godot;
using System;

/// <summary>
/// Base class for interactable objects in the game world.
/// </summary>
public partial class InteractionPoint : Area3D
{
    [Export] public string InteractionPrompt { get; set; } = "Press E to interact";
    [Export] public string InteractionName { get; set; } = "Interact";
    [Export] public bool IsEnabled { get; set; } = true;

    // Visual feedback
    private MeshInstance3D _highlightMesh;
    private bool _isHighlighted = false;

    [Signal]
    public delegate void InteractedEventHandler(PlayerController player);

    [Signal]
    public delegate void HighlightChangedEventHandler(bool isHighlighted);

    public override void _Ready()
    {
        // Create collision shape if not present
        if (GetChildCount() == 0 || GetNodeOrNull<CollisionShape3D>("CollisionShape3D") == null)
        {
            var collision = new CollisionShape3D();
            var sphere = new SphereShape3D();
            sphere.Radius = 1.5f;
            collision.Shape = sphere;
            AddChild(collision);
        }

        // Create highlight visual
        CreateHighlightVisual();

        // Connect area signals
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    private void CreateHighlightVisual()
    {
        _highlightMesh = new MeshInstance3D();
        var torus = new TorusMesh();
        torus.InnerRadius = 0.8f;
        torus.OuterRadius = 1f;
        torus.Rings = 16;
        torus.RingSegments = 32;

        var mat = new StandardMaterial3D();
        mat.AlbedoColor = new Color(1f, 0.9f, 0.3f, 0.5f);
        mat.EmissionEnabled = true;
        mat.Emission = new Color(1f, 0.8f, 0.2f);
        mat.EmissionEnergyMultiplier = 1f;
        mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        torus.Material = mat;

        _highlightMesh.Mesh = torus;
        _highlightMesh.Rotation = new Vector3(Mathf.Pi / 2f, 0, 0); // Lay flat
        _highlightMesh.Visible = false;
        AddChild(_highlightMesh);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is PlayerController && IsEnabled)
        {
            SetHighlighted(true);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is PlayerController)
        {
            SetHighlighted(false);
        }
    }

    /// <summary>
    /// Called when a player interacts with this point.
    /// </summary>
    public virtual void OnInteract(PlayerController player)
    {
        if (!IsEnabled) return;

        EmitSignal(SignalName.Interacted, player);
        GD.Print($"Interacted with: {InteractionName}");
    }

    /// <summary>
    /// Set highlight state.
    /// </summary>
    public void SetHighlighted(bool highlighted)
    {
        if (_isHighlighted == highlighted) return;

        _isHighlighted = highlighted;
        if (_highlightMesh != null)
        {
            _highlightMesh.Visible = highlighted;

            // Animate highlight
            if (highlighted)
            {
                var tween = CreateTween();
                tween.SetLoops();
                tween.TweenProperty(_highlightMesh, "rotation:y", Mathf.Pi * 2f, 3f)
                    .From(0f);
            }
        }

        EmitSignal(SignalName.HighlightChanged, highlighted);
    }

    /// <summary>
    /// Enable or disable this interaction point.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled && _isHighlighted)
        {
            SetHighlighted(false);
        }
    }

    public bool IsHighlighted => _isHighlighted;
}
