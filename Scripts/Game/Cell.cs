using Godot;

public partial class Cell : Node3D
{
    private MeshInstance3D _meshInstance;
    private StandardMaterial3D _defaultMaterial;
    private StandardMaterial3D _highlightMaterial;
    private StandardMaterial3D _occupiedMaterialX;
    private StandardMaterial3D _occupiedMaterialO;
    private StandardMaterial3D _currentOccupiedMaterial;
    private bool _isHighlighted;
    private bool _isOccupiedGlowActive;
    private Tween _landingGlowTween;

    // Player colors
    private static readonly Color XColor = new Color(1.0f, 0.3f, 0.3f); // Red
    private static readonly Color OColor = new Color(0.3f, 0.5f, 1.0f); // Blue

    // Glow intensities
    private const float FlashIntensity = 1.5f;
    private const float SubtleGlowIntensity = 0.4f;

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

        // Create highlight material (for hover - greenish tint)
        _highlightMaterial = new StandardMaterial3D();
        _highlightMaterial.AlbedoColor = new Color(0.4f, 0.6f, 0.4f, 1f);
        _highlightMaterial.EmissionEnabled = true;
        _highlightMaterial.Emission = new Color(0.2f, 0.4f, 0.2f, 1f);
        _highlightMaterial.EmissionEnergyMultiplier = 0.5f;

        // Create occupied material for X (red glow)
        _occupiedMaterialX = new StandardMaterial3D();
        _occupiedMaterialX.AlbedoColor = _defaultMaterial?.AlbedoColor ?? new Color(0.3f, 0.3f, 0.3f);
        _occupiedMaterialX.EmissionEnabled = true;
        _occupiedMaterialX.Emission = XColor;
        _occupiedMaterialX.EmissionEnergyMultiplier = SubtleGlowIntensity;

        // Create occupied material for O (blue glow)
        _occupiedMaterialO = new StandardMaterial3D();
        _occupiedMaterialO.AlbedoColor = _defaultMaterial?.AlbedoColor ?? new Color(0.3f, 0.3f, 0.3f);
        _occupiedMaterialO.EmissionEnabled = true;
        _occupiedMaterialO.Emission = OColor;
        _occupiedMaterialO.EmissionEnergyMultiplier = SubtleGlowIntensity;
    }

    public void Highlight()
    {
        if (_isHighlighted) return;
        _isHighlighted = true;
        // Don't override occupied glow with highlight
        if (!_isOccupiedGlowActive)
        {
            _meshInstance.SetSurfaceOverrideMaterial(0, _highlightMaterial);
        }
    }

    public void Unhighlight()
    {
        if (!_isHighlighted) return;
        _isHighlighted = false;
        if (!_isOccupiedGlowActive)
        {
            _meshInstance.SetSurfaceOverrideMaterial(0, _defaultMaterial);
        }
    }

    /// <summary>
    /// Show a glow effect when a piece lands on this cell.
    /// Flashes bright in the player's color, then fades to a subtle permanent glow.
    /// </summary>
    public void ShowLandingGlow()
    {
        // Use the cell's OccupiedBy to determine color
        if (OccupiedBy == Player.None) return;

        _isOccupiedGlowActive = true;

        // Select the appropriate material based on player
        _currentOccupiedMaterial = OccupiedBy == Player.X ? _occupiedMaterialX : _occupiedMaterialO;

        // Cancel any existing tween
        _landingGlowTween?.Kill();

        // Start with bright flash
        _currentOccupiedMaterial.EmissionEnergyMultiplier = FlashIntensity;
        _meshInstance.SetSurfaceOverrideMaterial(0, _currentOccupiedMaterial);

        // Create tween to fade from flash to subtle glow
        _landingGlowTween = CreateTween();

        // Hold the bright flash briefly
        _landingGlowTween.TweenInterval(0.15f);

        // Fade down to subtle glow (but don't go to zero - keep the glow)
        _landingGlowTween.TweenMethod(
            Callable.From<float>(SetGlowIntensity),
            FlashIntensity, SubtleGlowIntensity, 0.4f
        ).SetEase(Tween.EaseType.Out);
    }

    private void SetGlowIntensity(float intensity)
    {
        if (_currentOccupiedMaterial != null)
        {
            _currentOccupiedMaterial.EmissionEnergyMultiplier = intensity;
        }
    }

    /// <summary>
    /// Apply the permanent occupied glow without the flash animation.
    /// Used when loading saved games.
    /// </summary>
    public void ApplyOccupiedGlow()
    {
        if (OccupiedBy == Player.None) return;

        _isOccupiedGlowActive = true;
        _currentOccupiedMaterial = OccupiedBy == Player.X ? _occupiedMaterialX : _occupiedMaterialO;
        _currentOccupiedMaterial.EmissionEnergyMultiplier = SubtleGlowIntensity;
        _meshInstance.SetSurfaceOverrideMaterial(0, _currentOccupiedMaterial);
    }

    /// <summary>
    /// Clear the occupied glow (used when resetting the game).
    /// </summary>
    private void ClearOccupiedGlow()
    {
        _landingGlowTween?.Kill();
        _isOccupiedGlowActive = false;
        _currentOccupiedMaterial = null;
        _meshInstance.SetSurfaceOverrideMaterial(0, _isHighlighted ? _highlightMaterial : _defaultMaterial);
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
        ClearOccupiedGlow();
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
            ApplyOccupiedGlow();
        }
        else
        {
            IsOccupied = false;
            OccupiedBy = Player.None;
            ClearOccupiedGlow();
        }
    }
}
