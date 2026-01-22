using Godot;

public partial class Cell : Node3D
{
    private MeshInstance3D _meshInstance;
    private StandardMaterial3D _defaultMaterial;
    private StandardMaterial3D _highlightMaterial;
    private bool _isHighlighted;

    // Cell position within the small board (0-2, 0-2)
    public Vector2I LocalPosition { get; set; }

    // Reference to parent small board
    public SmallBoard ParentBoard { get; set; }

    // Occupancy tracking
    public bool IsOccupied { get; private set; }
    public Player OccupiedBy { get; private set; } = Player.None;

    public override void _Ready()
    {
        _meshInstance = GetNode<MeshInstance3D>("Floor/MeshInstance3D");

        // Store the default material
        _defaultMaterial = _meshInstance.GetSurfaceOverrideMaterial(0) as StandardMaterial3D;

        // Create highlight material (brighter with emission)
        _highlightMaterial = new StandardMaterial3D();
        _highlightMaterial.AlbedoColor = new Color(0.4f, 0.6f, 0.4f, 1f); // Greenish tint
        _highlightMaterial.EmissionEnabled = true;
        _highlightMaterial.Emission = new Color(0.2f, 0.4f, 0.2f, 1f);
        _highlightMaterial.EmissionEnergyMultiplier = 0.5f;
    }

    public void Highlight()
    {
        if (_isHighlighted) return;
        _isHighlighted = true;
        _meshInstance.SetSurfaceOverrideMaterial(0, _highlightMaterial);
    }

    public void Unhighlight()
    {
        if (!_isHighlighted) return;
        _isHighlighted = false;
        _meshInstance.SetSurfaceOverrideMaterial(0, _defaultMaterial);
    }

    public void SetOccupied(Player player)
    {
        IsOccupied = true;
        OccupiedBy = player;
    }

    public void Reset()
    {
        IsOccupied = false;
        OccupiedBy = Player.None;
    }

    /// <summary>
    /// Restore the cell state from saved data (used when loading a game).
    /// </summary>
    public void RestoreState(Player player)
    {
        if (player != Player.None)
        {
            IsOccupied = true;
            OccupiedBy = player;
        }
        else
        {
            IsOccupied = false;
            OccupiedBy = Player.None;
        }
    }
}
