using Godot;

public partial class BoardController : Node3D
{
    private Camera3D _camera;
    private bool _isMenuBackground;

    // Currently hovered elements (mouse mode)
    private Cell _hoveredCell;
    private SmallBoard _hoveredBoard;

    // Controller selection state
    private bool _usingController;
    private int _selectedBoardX;
    private int _selectedBoardY;
    private int _selectedCellX;
    private int _selectedCellY;
    private Cell _controllerSelectedCell;

    // Array of small boards
    public SmallBoard[,] SmallBoards { get; private set; } = new SmallBoard[3, 3];

    public override void _Ready()
    {
        // Check if we're in a SubViewport (menu background)
        _isMenuBackground = GetViewport() is SubViewport;

        // Get camera reference (it's a sibling in Main scene)
        _camera = GetViewport().GetCamera3D();

        // Initialize small boards
        InitializeBoards();

        // Only register with GameManager if not in menu background
        if (!_isMenuBackground)
        {
            GameManager.Instance?.SetBoardController(this);

            // Try to load saved game if there's pending load data
            // Use CallDeferred to ensure all nodes are ready
            CallDeferred(nameof(TryLoadSavedGame));

            // Initialize controller selection to center
            _selectedBoardX = 1;
            _selectedBoardY = 1;
            _selectedCellX = 1;
            _selectedCellY = 1;
        }
    }

    private void TryLoadSavedGame()
    {
        GameManager.Instance?.TryLoadSavedGame();
    }

    private void InitializeBoards()
    {
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var boardNode = GetNode<SmallBoard>($"SmallBoard_{x}_{y}");
                if (boardNode != null)
                {
                    boardNode.BoardPosition = new Vector2I(x, y);
                    SmallBoards[x, y] = boardNode;
                }
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_isMenuBackground) return;

        // Only handle mouse hover when not using controller
        if (!_usingController)
        {
            HandleHover();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_isMenuBackground) return;

        // Detect input type and switch modes
        if (@event is InputEventMouseMotion || @event is InputEventMouseButton)
        {
            if (_usingController)
            {
                _usingController = false;
                ClearControllerSelection();
            }

            if (@event is InputEventMouseButton mouseButton)
            {
                if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
                {
                    HandleClick();
                }
            }
            return;
        }

        // Controller/keyboard input
        if (@event is InputEventJoypadButton || @event is InputEventJoypadMotion || @event is InputEventKey)
        {
            // Switch to controller mode
            if (!_usingController)
            {
                _usingController = true;
                ClearHover();
                UpdateControllerSelection();
            }

            // Handle navigation
            if (@event.IsActionPressed("ui_left"))
            {
                MoveSelection(-1, 0);
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_right"))
            {
                MoveSelection(1, 0);
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_up"))
            {
                MoveSelection(0, -1);
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_down"))
            {
                MoveSelection(0, 1);
                GetViewport().SetInputAsHandled();
            }
            else if (@event.IsActionPressed("ui_accept"))
            {
                HandleControllerConfirm();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void MoveSelection(int dx, int dy)
    {
        // Calculate new cell position within the full 9x9 grid
        int globalX = _selectedBoardX * 3 + _selectedCellX;
        int globalY = _selectedBoardY * 3 + _selectedCellY;

        globalX = Mathf.Clamp(globalX + dx, 0, 8);
        globalY = Mathf.Clamp(globalY + dy, 0, 8);

        // Convert back to board and cell coordinates
        _selectedBoardX = globalX / 3;
        _selectedBoardY = globalY / 3;
        _selectedCellX = globalX % 3;
        _selectedCellY = globalY % 3;

        UpdateControllerSelection();
    }

    private void UpdateControllerSelection()
    {
        // Clear previous selection
        _controllerSelectedCell?.Unhighlight();
        _hoveredBoard?.Unhighlight();

        // Get the new selected cell
        var board = SmallBoards[_selectedBoardX, _selectedBoardY];
        var cell = board.Cells[_selectedCellX, _selectedCellY];

        // Update highlights
        _hoveredBoard = board;
        _controllerSelectedCell = cell;

        board.Highlight();
        if (!board.IsWon && !cell.IsOccupied)
        {
            cell.Highlight();
        }
    }

    private void ClearControllerSelection()
    {
        _controllerSelectedCell?.Unhighlight();
        _controllerSelectedCell = null;
    }

    private void HandleControllerConfirm()
    {
        // Block when it's CPU's turn in PvCPU mode
        if (GameManager.CurrentGameMode == GameMode.PlayerVsCPU &&
            GameManager.Instance?.CurrentPlayer == Player.O)
        {
            return;
        }

        // Block during camera transitions
        if (GameCamera.Instance?.IsTransitioning == true)
        {
            return;
        }

        if (_controllerSelectedCell != null &&
            !_controllerSelectedCell.IsOccupied &&
            !_controllerSelectedCell.ParentBoard.IsWon)
        {
            GameManager.Instance?.PlacePiece(_controllerSelectedCell);
        }
    }

    private void HandleClick()
    {
        // Block clicks when it's CPU's turn in PvCPU mode
        if (GameManager.CurrentGameMode == GameMode.PlayerVsCPU &&
            GameManager.Instance?.CurrentPlayer == Player.O)
        {
            return;
        }

        // Block during camera transitions
        if (GameCamera.Instance?.IsTransitioning == true)
        {
            return;
        }

        // If we have a hovered cell that isn't occupied and board isn't won, place a piece
        if (_hoveredCell != null &&
            !_hoveredCell.IsOccupied &&
            !_hoveredCell.ParentBoard.IsWon)
        {
            GameManager.Instance?.PlacePiece(_hoveredCell);
        }
    }

    private void HandleHover()
    {
        var mousePos = GetViewport().GetMousePosition();
        var from = _camera.ProjectRayOrigin(mousePos);
        var to = from + _camera.ProjectRayNormal(mousePos) * 100f;

        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = result["collider"].As<Node3D>();

            // First try to find a Cell
            var cell = FindParentOfType<Cell>(collider);
            if (cell != null)
            {
                SetHoveredCell(cell);
                return;
            }

            // If no cell, try to find a SmallBoard (hit the board area between cells)
            var board = FindParentOfType<SmallBoard>(collider);
            if (board != null)
            {
                SetHoveredBoard(board);
                return;
            }
        }

        // Nothing hovered - clear highlights
        ClearHover();
    }

    private T FindParentOfType<T>(Node node) where T : Node
    {
        while (node != null)
        {
            if (node is T found)
                return found;
            node = node.GetParent();
        }
        return null;
    }

    private void SetHoveredCell(Cell cell)
    {
        var board = cell.ParentBoard;

        // If same cell, nothing to do
        if (cell == _hoveredCell && board == _hoveredBoard) return;

        // Clear previous cell highlight only (board might stay the same)
        _hoveredCell?.Unhighlight();

        // Update board highlight if changed
        if (board != _hoveredBoard)
        {
            _hoveredBoard?.Unhighlight();
            _hoveredBoard = board;
            _hoveredBoard?.Highlight();
        }

        // Set new cell highlight (only if not in a won board and cell not occupied)
        _hoveredCell = cell;
        if (!board.IsWon && !cell.IsOccupied)
        {
            _hoveredCell?.Highlight();
        }
    }

    private void SetHoveredBoard(SmallBoard board)
    {
        // Clear cell highlight since we're between cells
        if (_hoveredCell != null)
        {
            _hoveredCell.Unhighlight();
            _hoveredCell = null;
        }

        // If same board, nothing more to do
        if (board == _hoveredBoard) return;

        // Update board highlight
        _hoveredBoard?.Unhighlight();
        _hoveredBoard = board;
        _hoveredBoard?.Highlight();
    }

    private void ClearHover()
    {
        _hoveredCell?.Unhighlight();
        _hoveredBoard?.Unhighlight();

        _hoveredCell = null;
        _hoveredBoard = null;
    }
}
