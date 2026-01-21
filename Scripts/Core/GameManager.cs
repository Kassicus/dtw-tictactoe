using Godot;

public enum Player
{
    None,
    X,
    O
}

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    private PackedScene _xPieceScene;
    private PackedScene _oPieceScene;
    private Camera3D _camera;

    public Player CurrentPlayer { get; private set; } = Player.X;

    [Signal]
    public delegate void TurnChangedEventHandler(Player player);

    [Signal]
    public delegate void PiecePlacedEventHandler(Player player, Vector2I boardPos, Vector2I cellPos);

    public override void _Ready()
    {
        Instance = this;

        // Load piece scenes
        _xPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/XPiece.tscn");
        _oPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/OPiece.tscn");
    }

    public void SetCamera(Camera3D camera)
    {
        _camera = camera;
    }

    public void PlacePiece(Cell cell)
    {
        if (cell.IsOccupied) return;

        // Mark cell as occupied
        cell.SetOccupied(CurrentPlayer);

        // Spawn the piece
        SpawnPiece(cell);

        // Emit signal
        EmitSignal(SignalName.PiecePlaced, (int)CurrentPlayer,
            cell.ParentBoard.BoardPosition, cell.LocalPosition);

        // Switch turns
        SwitchTurn();
    }

    private void SpawnPiece(Cell cell)
    {
        // Get the cell's world position
        var cellWorldPos = cell.GlobalPosition;

        // Spawn position: directly above the cell, high enough to fall past camera
        var spawnPos = new Vector3(cellWorldPos.X, 35f, cellWorldPos.Z);

        // Create the piece
        RigidBody3D piece;
        if (CurrentPlayer == Player.X)
        {
            piece = _xPieceScene.Instantiate<RigidBody3D>();
        }
        else
        {
            piece = _oPieceScene.Instantiate<RigidBody3D>();
        }

        // Add to scene and set position
        GetTree().CurrentScene.AddChild(piece);
        piece.GlobalPosition = spawnPos;

        // Add slight random rotation for visual interest
        piece.RotateY((float)GD.RandRange(-0.3, 0.3));
    }

    private void SwitchTurn()
    {
        CurrentPlayer = CurrentPlayer == Player.X ? Player.O : Player.X;
        EmitSignal(SignalName.TurnChanged, (int)CurrentPlayer);
    }

    public void ResetGame()
    {
        CurrentPlayer = Player.X;
        EmitSignal(SignalName.TurnChanged, (int)CurrentPlayer);
    }
}
