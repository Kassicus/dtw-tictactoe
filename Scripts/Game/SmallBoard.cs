using Godot;

public partial class SmallBoard : Node3D
{
    private Node3D _highlightBorder;
    private bool _isHighlighted;

    // Board position in the main grid (0-2, 0-2)
    public Vector2I BoardPosition { get; set; }

    // Array of cells in this small board
    public Cell[,] Cells { get; private set; } = new Cell[3, 3];

    public override void _Ready()
    {
        _highlightBorder = GetNode<Node3D>("HighlightBorder");

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
}
