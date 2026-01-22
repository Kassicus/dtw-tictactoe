using Godot;

public enum Player
{
    None,
    X,
    O
}

public enum GameState
{
    Playing,
    WaitingForPiece,
    GameOver
}

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }
    public static GameMode CurrentGameMode { get; set; } = GameMode.PlayerVsPlayer;

    private PackedScene _xPieceScene;
    private PackedScene _oPieceScene;
    private BoardController _boardController;
    private CPUPlayer _cpuPlayer;

    public Player CurrentPlayer { get; private set; } = Player.X;
    public GameState State { get; private set; } = GameState.Playing;
    public Player GameWinner { get; private set; } = Player.None;

    // Track pending move info for when piece lands
    private Player _pendingPlayer;
    private SmallBoard _pendingBoard;

    // Win patterns for the main board (same as small board)
    private static readonly int[][] WinPatterns = new int[][]
    {
        // Rows
        new[] { 0, 0, 1, 0, 2, 0 },
        new[] { 0, 1, 1, 1, 2, 1 },
        new[] { 0, 2, 1, 2, 2, 2 },
        // Columns
        new[] { 0, 0, 0, 1, 0, 2 },
        new[] { 1, 0, 1, 1, 1, 2 },
        new[] { 2, 0, 2, 1, 2, 2 },
        // Diagonals
        new[] { 0, 0, 1, 1, 2, 2 },
        new[] { 2, 0, 1, 1, 0, 2 }
    };

    [Signal]
    public delegate void TurnChangedEventHandler(Player player);

    [Signal]
    public delegate void PiecePlacedEventHandler(Player player, Vector2I boardPos, Vector2I cellPos);

    [Signal]
    public delegate void SmallBoardWonEventHandler(Player player, Vector2I boardPos);

    [Signal]
    public delegate void GameWonEventHandler(Player winner);

    [Signal]
    public delegate void GameDrawEventHandler();

    public override void _Ready()
    {
        Instance = this;

        // Load piece scenes
        _xPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/XPiece.tscn");
        _oPieceScene = GD.Load<PackedScene>("res://Scenes/Pieces/OPiece.tscn");
    }

    private void EnsureCPUPlayerExists()
    {
        if (CurrentGameMode == GameMode.PlayerVsCPU && _cpuPlayer == null)
        {
            _cpuPlayer = new CPUPlayer();
        }
    }

    public void SetBoardController(BoardController controller)
    {
        _boardController = controller;
    }

    public void PlacePiece(Cell cell)
    {
        if (State != GameState.Playing) return;
        if (cell.IsOccupied) return;
        if (cell.ParentBoard.IsWon) return;

        _pendingPlayer = CurrentPlayer;
        _pendingBoard = cell.ParentBoard;

        // Mark cell as occupied
        cell.SetOccupied(CurrentPlayer);

        // Set state to waiting
        State = GameState.WaitingForPiece;

        // Spawn the piece with landing callback
        SpawnPiece(cell);

        // Emit signal
        EmitSignal(SignalName.PiecePlaced, (int)CurrentPlayer,
            cell.ParentBoard.BoardPosition, cell.LocalPosition);
    }

    private void SpawnPiece(Cell cell)
    {
        // Use cannon system if available
        if (CannonController.Instance != null)
        {
            CannonController.Instance.FireAtCell(cell, CurrentPlayer, OnPieceLanded);
            return;
        }

        // Fallback to direct spawn (legacy behavior)
        SpawnPieceFalling(cell, CurrentPlayer, OnPieceLanded);
    }

    /// <summary>
    /// Spawn a piece that falls from above (fallback when cannons unavailable).
    /// </summary>
    private void SpawnPieceFalling(Cell cell, Player player, System.Action onLanded)
    {
        // Get the cell's world position
        var cellWorldPos = cell.GlobalPosition;

        // Spawn position: just above the camera so pieces appear quickly
        var spawnPos = new Vector3(cellWorldPos.X, 22f, cellWorldPos.Z);

        // Create the piece
        GamePiece piece;
        if (player == Player.X)
        {
            piece = _xPieceScene.Instantiate<GamePiece>();
        }
        else
        {
            piece = _oPieceScene.Instantiate<GamePiece>();
        }

        // Set landing callback
        piece.OnLanded = onLanded;

        // Add to scene and set position
        GetTree().CurrentScene.AddChild(piece);
        piece.GlobalPosition = spawnPos;

        // Give initial downward velocity so pieces fall faster
        piece.LinearVelocity = new Vector3(0, -15f, 0);

        // Add slight random rotation for visual interest
        piece.RotateY((float)GD.RandRange(-0.3, 0.3));
    }

    private void OnPieceLanded()
    {
        if (State != GameState.WaitingForPiece) return;

        var player = _pendingPlayer;
        var smallBoard = _pendingBoard;

        // Check if this move wins the small board
        if (smallBoard.CheckAndShowWin(player))
        {
            EmitSignal(SignalName.SmallBoardWon, (int)player, smallBoard.BoardPosition);

            // Check if this wins the game
            if (CheckForGameWin(player))
            {
                State = GameState.GameOver;
                GameWinner = player;
                EmitSignal(SignalName.GameWon, (int)player);
                GD.Print($"Game Over! {player} wins!");
                return;
            }
        }

        // Check for draw (all boards either won or full)
        if (CheckForDraw())
        {
            State = GameState.GameOver;
            EmitSignal(SignalName.GameDraw);
            GD.Print("Game Over! It's a draw!");
            return;
        }

        // Switch turns and allow next move
        State = GameState.Playing;
        SwitchTurn();
    }

    private bool CheckForGameWin(Player player)
    {
        if (_boardController == null) return false;

        foreach (var pattern in WinPatterns)
        {
            var board1 = _boardController.SmallBoards[pattern[0], pattern[1]];
            var board2 = _boardController.SmallBoards[pattern[2], pattern[3]];
            var board3 = _boardController.SmallBoards[pattern[4], pattern[5]];

            if (board1.Winner == player &&
                board2.Winner == player &&
                board3.Winner == player)
            {
                return true;
            }
        }

        return false;
    }

    private bool CheckForDraw()
    {
        if (_boardController == null) return false;

        // Check if all small boards are either won or full
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var board = _boardController.SmallBoards[x, y];
                if (!board.IsWon && !board.IsFull())
                {
                    return false; // Still playable cells
                }
            }
        }

        return true;
    }

    private void SwitchTurn()
    {
        CurrentPlayer = CurrentPlayer == Player.X ? Player.O : Player.X;
        EmitSignal(SignalName.TurnChanged, (int)CurrentPlayer);

        // Trigger CPU move if it's the CPU's turn
        if (CurrentGameMode == GameMode.PlayerVsCPU && CurrentPlayer == Player.O)
        {
            EnsureCPUPlayerExists();
            ExecuteCPUMove();
        }
    }

    private async void ExecuteCPUMove()
    {
        try
        {
            // Add delay for better UX
            await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);

            // Make sure we're still in a valid state to make a move
            if (State != GameState.Playing || CurrentPlayer != Player.O) return;

            var (board, cell) = _cpuPlayer.GetBestMove(_boardController, Player.O);
            if (cell != null && board != null)
            {
                PlacePiece(cell);
            }
        }
        catch (System.Exception e)
        {
            GD.PrintErr($"CPU Move Error: {e.Message}");
        }
    }

    public void ResetGame()
    {
        CurrentPlayer = Player.X;
        State = GameState.Playing;
        GameWinner = Player.None;
        EmitSignal(SignalName.TurnChanged, (int)CurrentPlayer);
    }

    /// <summary>
    /// Try to load a saved game if pending load data exists.
    /// Called by BoardController after initialization.
    /// </summary>
    public void TryLoadSavedGame()
    {
        var saveData = MainMenu.PendingLoadData;
        if (saveData == null) return;

        // Clear the pending data
        MainMenu.PendingLoadData = null;

        LoadFromSave(saveData);
    }

    private void LoadFromSave(GameSaveData saveData)
    {
        if (_boardController == null)
        {
            GD.PrintErr("GameManager: Cannot load save - BoardController is null");
            return;
        }

        GD.Print("GameManager: Loading saved game...");

        // Restore game state
        CurrentPlayer = saveData.CurrentPlayer;
        State = saveData.State;
        GameWinner = saveData.GameWinner;

        // Restore board state
        for (int bx = 0; bx < 3; bx++)
        {
            for (int by = 0; by < 3; by++)
            {
                var board = _boardController.SmallBoards[bx, by];
                int boardIndex = bx * 3 + by;

                // Restore each cell
                for (int cx = 0; cx < 3; cx++)
                {
                    for (int cy = 0; cy < 3; cy++)
                    {
                        var cell = board.Cells[cx, cy];
                        int cellIndex = bx * 27 + by * 9 + cx * 3 + cy;
                        var occupiedBy = saveData.CellOccupancy[cellIndex];

                        cell.RestoreState(occupiedBy);

                        // Spawn piece if cell is occupied
                        if (occupiedBy != Player.None)
                        {
                            SpawnPieceStatic(cell, occupiedBy);
                        }
                    }
                }

                // Restore board winner
                board.RestoreWinner(saveData.SmallBoardWinners[boardIndex]);
            }
        }

        // Emit signal so UI updates
        EmitSignal(SignalName.TurnChanged, (int)CurrentPlayer);

        // If game was over, emit appropriate signal
        if (State == GameState.GameOver)
        {
            if (GameWinner != Player.None)
            {
                EmitSignal(SignalName.GameWon, (int)GameWinner);
            }
            else
            {
                EmitSignal(SignalName.GameDraw);
            }
        }

        GD.Print($"GameManager: Game loaded - {CurrentPlayer}'s turn, State: {State}");
    }

    /// <summary>
    /// Spawn a piece at its final position (no falling animation).
    /// Used when loading a saved game.
    /// </summary>
    private void SpawnPieceStatic(Cell cell, Player player)
    {
        var cellWorldPos = cell.GlobalPosition;

        // Final position: just above the cell surface
        var finalPos = new Vector3(cellWorldPos.X, cellWorldPos.Y + 0.1f, cellWorldPos.Z);

        // Create the piece
        GamePiece piece;
        if (player == Player.X)
        {
            piece = _xPieceScene.Instantiate<GamePiece>();
        }
        else
        {
            piece = _oPieceScene.Instantiate<GamePiece>();
        }

        // Disable landing callback since we're restoring state
        piece.PlayLandingSound = false;

        // Add to scene and set position
        GetTree().CurrentScene.AddChild(piece);
        piece.GlobalPosition = finalPos;

        // Freeze the piece so it doesn't move
        piece.Freeze = true;
    }
}
