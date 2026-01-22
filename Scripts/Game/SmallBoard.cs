using Godot;

public partial class SmallBoard : Node3D
{
    private Node3D _highlightBorder;
    private Node3D _xWinnerBorder;
    private Node3D _oWinnerBorder;
    private bool _isHighlighted;

    // Board position in the main grid (0-2, 0-2)
    public Vector2I BoardPosition { get; set; }

    // Array of cells in this small board
    public Cell[,] Cells { get; private set; } = new Cell[3, 3];

    // Winner of this small board
    public Player Winner { get; private set; } = Player.None;
    public bool IsWon => Winner != Player.None;

    // Win patterns: indices for rows, columns, and diagonals
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

    public override void _Ready()
    {
        _highlightBorder = GetNode<Node3D>("HighlightBorder");
        _xWinnerBorder = GetNode<Node3D>("XWinnerBorder");
        _oWinnerBorder = GetNode<Node3D>("OWinnerBorder");

        // Find and store references to all cells
        InitializeCells();
    }

    private void InitializeCells()
    {
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                var cellNode = GetNode<Cell>($"Cell_{x}_{y}");
                if (cellNode != null)
                {
                    cellNode.LocalPosition = new Vector2I(x, y);
                    cellNode.ParentBoard = this;
                    Cells[x, y] = cellNode;
                }
            }
        }
    }

    public void Highlight()
    {
        // Don't show hover highlight if board is already won
        if (IsWon) return;
        if (_isHighlighted) return;
        _isHighlighted = true;
        _highlightBorder.Visible = true;
    }

    public void Unhighlight()
    {
        if (!_isHighlighted) return;
        _isHighlighted = false;
        _highlightBorder.Visible = false;
    }

    public Cell GetCell(int x, int y)
    {
        if (x >= 0 && x < 3 && y >= 0 && y < 3)
            return Cells[x, y];
        return null;
    }

    /// <summary>
    /// Check if the given player has won this small board and show the highlight.
    /// Call this after a piece has landed.
    /// </summary>
    public bool CheckAndShowWin(Player player)
    {
        if (IsWon) return false;

        foreach (var pattern in WinPatterns)
        {
            var cell1 = Cells[pattern[0], pattern[1]];
            var cell2 = Cells[pattern[2], pattern[3]];
            var cell3 = Cells[pattern[4], pattern[5]];

            if (cell1.OccupiedBy == player &&
                cell2.OccupiedBy == player &&
                cell3.OccupiedBy == player)
            {
                SetWinner(player);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if this board is full (all cells occupied).
    /// </summary>
    public bool IsFull()
    {
        for (int x = 0; x < 3; x++)
        {
            for (int y = 0; y < 3; y++)
            {
                if (!Cells[x, y].IsOccupied)
                    return false;
            }
        }
        return true;
    }

    private void SetWinner(Player player)
    {
        Winner = player;

        // Hide hover highlight
        Unhighlight();

        // Show the appropriate winner border
        if (player == Player.X)
        {
            _xWinnerBorder.Visible = true;
        }
        else if (player == Player.O)
        {
            _oWinnerBorder.Visible = true;
        }
    }

    /// <summary>
    /// Restore the board winner state from saved data (used when loading a game).
    /// </summary>
    public void RestoreWinner(Player player)
    {
        if (player != Player.None)
        {
            SetWinner(player);
        }
        else
        {
            // Reset winner state
            Winner = Player.None;
            _xWinnerBorder.Visible = false;
            _oWinnerBorder.Visible = false;
        }
    }
}
