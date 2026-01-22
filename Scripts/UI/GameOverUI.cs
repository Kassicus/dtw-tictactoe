using Godot;

public partial class GameOverUI : CanvasLayer
{
    private ColorRect _overlay;
    private Label _winnerLabel;
    private Button _playAgainButton;

    public override void _Ready()
    {
        _overlay = GetNode<ColorRect>("Overlay");
        _winnerLabel = GetNode<Label>("Overlay/Panel/VBoxContainer/WinnerLabel");
        _playAgainButton = GetNode<Button>("Overlay/Panel/VBoxContainer/PlayAgainButton");

        _playAgainButton.Pressed += OnPlayAgainPressed;

        // Connect to GameManager signals
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameWon += OnGameWon;
            GameManager.Instance.GameDraw += OnGameDraw;
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameWon -= OnGameWon;
            GameManager.Instance.GameDraw -= OnGameDraw;
        }
    }

    private void OnGameWon(Player winner)
    {
        string winnerText = winner == Player.X ? "X Wins!" : "O Wins!";
        Color winnerColor = winner == Player.X
            ? new Color(0.9f, 0.3f, 0.3f)
            : new Color(0.3f, 0.7f, 0.9f);

        _winnerLabel.Text = winnerText;
        _winnerLabel.AddThemeColorOverride("font_color", winnerColor);
        _overlay.Visible = true;
        _playAgainButton.GrabFocus();
    }

    private void OnGameDraw()
    {
        _winnerLabel.Text = "It's a Draw!";
        _winnerLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f));
        _overlay.Visible = true;
        _playAgainButton.GrabFocus();
    }

    private void OnPlayAgainPressed()
    {
        _overlay.Visible = false;
        // Reload the scene to reset the game
        GetTree().ReloadCurrentScene();
    }
}
