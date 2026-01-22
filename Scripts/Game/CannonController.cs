using Godot;
using System;

public partial class CannonController : Node3D
{
    public static CannonController Instance { get; private set; }

    [Export] public Cannon CannonX { get; set; }
    [Export] public Cannon CannonO { get; set; }

    private PackedScene _xPieceScene;
    private PackedScene _oPieceScene;

    public override void _Ready()
    {
        Instance = this;

        // Load piece scenes
        _xPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/XPiece.tscn");
        _oPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/OPiece.tscn");

        // Get cannon references if not set via export
        CannonX ??= GetNodeOrNull<Cannon>("CannonX");
        CannonO ??= GetNodeOrNull<Cannon>("CannonO");
    }

    /// <summary>
    /// Fire a piece at the specified cell from the correct cannon.
    /// </summary>
    public void FireAtCell(Cell cell, Player player, Action onLanded)
    {
        var cannon = GetCannonForPlayer(player);
        var pieceScene = GetPieceSceneForPlayer(player);

        if (cannon == null || pieceScene == null)
        {
            GD.PrintErr($"CannonController: Missing cannon or piece scene for {player}");
            return;
        }

        cannon.Fire(cell, pieceScene, onLanded);
    }

    private Cannon GetCannonForPlayer(Player player)
    {
        return player switch
        {
            Player.X => CannonX,
            Player.O => CannonO,
            _ => null
        };
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
    /// Get the active cannon for the current player.
    /// </summary>
    public Cannon GetActiveCannonForPlayer(Player player)
    {
        return GetCannonForPlayer(player);
    }
}
