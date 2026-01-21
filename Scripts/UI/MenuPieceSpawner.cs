using Godot;

public partial class MenuPieceSpawner : Node3D
{
    private PackedScene _xPieceScene;
    private PackedScene _oPieceScene;
    private float _spawnTimer = 0f;
    private float _spawnInterval = 0.4f;
    private float _startupDelay = 0.5f;
    private bool _ready = false;

    // Board bounds for random spawning
    private const float BoardMin = -5.0f;
    private const float BoardMax = 5.0f;
    private const float SpawnHeight = 25f;
    private const float DespawnHeight = -5f;

    public override void _Ready()
    {
        _xPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/XPiece.tscn");
        _oPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/OPiece.tscn");

        // Create a large floor to catch pieces
        CreateFloor();
    }

    private void CreateFloor()
    {
        var floor = new StaticBody3D();
        var collision = new CollisionShape3D();
        var shape = new BoxShape3D();
        shape.Size = new Vector3(20f, 0.5f, 20f);
        collision.Shape = shape;
        collision.Position = new Vector3(0, -0.35f, 0);
        floor.AddChild(collision);
        AddChild(floor);
    }

    public override void _Process(double delta)
    {
        // Wait for physics to initialize
        if (!_ready)
        {
            _startupDelay -= (float)delta;
            if (_startupDelay <= 0)
            {
                _ready = true;
            }
            return;
        }

        _spawnTimer += (float)delta;

        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0f;
            SpawnRandomPiece();
        }

        // Clean up pieces that have fallen below the board
        CleanupPieces();
    }

    private void SpawnRandomPiece()
    {
        // Random position over the board
        var x = (float)GD.RandRange(BoardMin, BoardMax);
        var z = (float)GD.RandRange(BoardMin, BoardMax);
        var spawnPos = new Vector3(x, SpawnHeight, z);

        // Random piece type
        RigidBody3D piece;
        if (GD.Randf() > 0.5f)
        {
            piece = _xPieceScene.Instantiate<RigidBody3D>();
        }
        else
        {
            piece = _oPieceScene.Instantiate<RigidBody3D>();
        }

        AddChild(piece);
        piece.GlobalPosition = spawnPos;

        // Give initial downward velocity
        piece.LinearVelocity = new Vector3(0, -15f, 0);

        // Random rotation for visual interest
        piece.RotateY((float)GD.RandRange(0, Mathf.Tau));
    }

    private void CleanupPieces()
    {
        foreach (var child in GetChildren())
        {
            if (child is RigidBody3D piece)
            {
                if (piece.GlobalPosition.Y < DespawnHeight)
                {
                    piece.QueueFree();
                }
            }
        }
    }
}
