using Godot;

public partial class PauseMenu : CanvasLayer
{
    private ColorRect _overlay;
    private Button _resumeButton;
    private Button _quitButton;

    public override void _Ready()
    {
        _overlay = GetNode<ColorRect>("Overlay");
        _resumeButton = GetNode<Button>("Overlay/Panel/VBoxContainer/ResumeButton");
        _quitButton = GetNode<Button>("Overlay/Panel/VBoxContainer/QuitButton");

        _resumeButton.Pressed += OnResumePressed;
        _quitButton.Pressed += OnQuitPressed;

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

    private void OnQuitPressed()
    {
        // Unpause before changing scenes
        GetTree().Paused = false;
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }
}
