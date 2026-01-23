using Godot;
using System;

public partial class CannonController : Node3D
{
    public static CannonController Instance { get; private set; }

    // Arrays of cannons for each player (3 per side)
    [Export] public Cannon[] CannonsX { get; set; } = new Cannon[3];
    [Export] public Cannon[] CannonsO { get; set; } = new Cannon[3];

    private PackedScene _xPieceScene;
    private PackedScene _oPieceScene;

    // Track which cannon fires next for each player
    private int _nextCannonIndexX = 0;
    private int _nextCannonIndexO = 0;

    public override void _Ready()
    {
        Instance = this;

        // Load piece scenes
        _xPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/XPiece.tscn");
        _oPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/OPiece.tscn");

        // Get cannon references if not set via export
        CannonsX[0] ??= GetNodeOrNull<Cannon>("CannonX1");
        CannonsX[1] ??= GetNodeOrNull<Cannon>("CannonX2");
        CannonsX[2] ??= GetNodeOrNull<Cannon>("CannonX3");

        CannonsO[0] ??= GetNodeOrNull<Cannon>("CannonO1");
        CannonsO[1] ??= GetNodeOrNull<Cannon>("CannonO2");
        CannonsO[2] ??= GetNodeOrNull<Cannon>("CannonO3");
    }

    /// <summary>
    /// Fire a piece at the specified cell from the next cannon in rotation.
    /// </summary>
    public void FireAtCell(Cell cell, Player player, Action onLanded)
    {
        var cannon = GetNextCannonForPlayer(player);
        var pieceScene = GetPieceSceneForPlayer(player);

        if (cannon == null || pieceScene == null)
        {
            GD.PrintErr($"CannonController: Missing cannon or piece scene for {player}");
            return;
        }

        cannon.Fire(cell, pieceScene, onLanded);
    }

    private Cannon GetNextCannonForPlayer(Player player)
    {
        if (player == Player.X)
        {
            var cannon = CannonsX[_nextCannonIndexX];
            _nextCannonIndexX = (_nextCannonIndexX + 1) % CannonsX.Length;
            return cannon;
        }
        else if (player == Player.O)
        {
            var cannon = CannonsO[_nextCannonIndexO];
            _nextCannonIndexO = (_nextCannonIndexO + 1) % CannonsO.Length;
            return cannon;
        }
        return null;
    }

    private PackedScene GetPieceSceneForPlayer(Player player)
    {
        return player switch
        {
            Player.X => _xPieceScene,
            Player.O => _oPieceScene,
            _ => null
        };
    }

    /// <summary>
    /// Get the active cannon for the current player (returns next in rotation without advancing).
    /// </summary>
    public Cannon GetActiveCannonForPlayer(Player player)
    {
        return player switch
        {
            Player.X => CannonsX[_nextCannonIndexX],
            Player.O => CannonsO[_nextCannonIndexO],
            _ => null
        };
    }
}
