using Godot;

public partial class BoardController : Node3D
{
    private Camera3D _camera;

    // Currently hovered elements
    private Cell _hoveredCell;
    private SmallBoard _hoveredBoard;

    // Array of small boards
    public SmallBoard[,] SmallBoards { get; private set; } = new SmallBoard[3, 3];

    public override void _Ready()
    {
        // Get camera reference (it's a sibling in Main scene)
        _camera = GetViewport().GetCamera3D();

        // Initialize small boards
        InitializeBoards();
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
        HandleHover();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
            {
                HandleClick();
            }
        }
    }

    private void HandleClick()
    {
        // If we have a hovered cell that isn't occupied, place a piece
        if (_hoveredCell != null && !_hoveredCell.IsOccupied)
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

        // Set new cell highlight
        _hoveredCell = cell;
        _hoveredCell?.Highlight();
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
