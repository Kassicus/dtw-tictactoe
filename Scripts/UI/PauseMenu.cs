using Godot;

public partial class PauseMenu : CanvasLayer
{
    private ColorRect _overlay;
    private Button _resumeButton;
    private Button _saveButton;
    private Button _quitButton;
    private BoardController _boardController;

    public override void _Ready()
    {
        _overlay = GetNode<ColorRect>("Overlay");
        _resumeButton = GetNode<Button>("Overlay/Panel/VBoxContainer/ResumeButton");
        _saveButton = GetNode<Button>("Overlay/Panel/VBoxContainer/SaveButton");
        _quitButton = GetNode<Button>("Overlay/Panel/VBoxContainer/QuitButton");

        _resumeButton.Pressed += OnResumePressed;
        _saveButton.Pressed += OnSavePressed;
        _quitButton.Pressed += OnQuitPressed;

        // Get BoardController reference (sibling in Main scene)
        _boardController = GetTree().CurrentScene.GetNodeOrNull<BoardController>("GameBoard");

        // Start hidden
        _overlay.Visible = false;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event.IsActionPressed("ui_cancel")) // Escape key
        {
            TogglePause();
            GetViewport().SetInputAsHandled();
        }
    }

    private void TogglePause()
    {
        if (_overlay.Visible)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    private void Pause()
    {
        _overlay.Visible = true;
        GetTree().Paused = true;
        _resumeButton.GrabFocus();
    }

    private void Resume()
    {
        _overlay.Visible = false;
        GetTree().Paused = false;
    }

    private void OnResumePressed()
    {
        Resume();
    }

    private void OnSavePressed()
    {
        if (_boardController != null)
        {
            bool success = SaveManager.SaveGame(_boardController);
            if (success)
            {
                // Update button text temporarily to show feedback
                _saveButton.Text = "Saved!";
                GetTree().CreateTimer(1.0f).Timeout += () => _saveButton.Text = "Save Game";
            }
            else
            {
                _saveButton.Text = "Save Failed";
                GetTree().CreateTimer(1.0f).Timeout += () => _saveButton.Text = "Save Game";
            }
        }
    }

    private void OnQuitPressed()
    {
        // Unpause before changing scenes
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }
}
