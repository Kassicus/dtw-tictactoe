using Godot;

public partial class MainMenu : Control
{
    private Button _playButton;
    private Button _settingsButton;
    private Button _quitButton;
    private Control _settingsPanel;
    private Control _mainPanel;

    public override void _Ready()
    {
        _mainPanel = GetNode<Control>("MainPanel");
        _playButton = GetNode<Button>("MainPanel/VBoxContainer/PlayButton");
        _settingsButton = GetNode<Button>("MainPanel/VBoxContainer/SettingsButton");
        _quitButton = GetNode<Button>("MainPanel/VBoxContainer/QuitButton");
        _settingsPanel = GetNode<Control>("SettingsPanel");

        _playButton.Pressed += OnPlayPressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;

        // Connect settings panel back button
        var backButton = _settingsPanel.GetNode<Button>("VBoxContainer/BackButton");
        backButton.Pressed += OnSettingsBackPressed;

        // Connect resolution option
        var resolutionOption = _settingsPanel.GetNode<OptionButton>("VBoxContainer/ResolutionContainer/ResolutionOption");
        resolutionOption.ItemSelected += OnResolutionChanged;

        // Connect volume sliders
        var masterSlider = _settingsPanel.GetNode<HSlider>("VBoxContainer/MasterContainer/MasterSlider");
        var musicSlider = _settingsPanel.GetNode<HSlider>("VBoxContainer/MusicContainer/MusicSlider");
        var sfxSlider = _settingsPanel.GetNode<HSlider>("VBoxContainer/SFXContainer/SFXSlider");

        masterSlider.ValueChanged += OnMasterVolumeChanged;
        musicSlider.ValueChanged += OnMusicVolumeChanged;
        sfxSlider.ValueChanged += OnSFXVolumeChanged;

        // Initialize resolution options
        InitializeResolutionOptions(resolutionOption);

        // Load saved settings
        LoadSettings(masterSlider, musicSlider, sfxSlider, resolutionOption);
    }

    private void InitializeResolutionOptions(OptionButton resolutionOption)
    {
        resolutionOption.Clear();
        resolutionOption.AddItem("1280 x 720", 0);
        resolutionOption.AddItem("1600 x 900", 1);
        resolutionOption.AddItem("1920 x 1080", 2);
        resolutionOption.AddItem("2560 x 1440", 3);
        resolutionOption.AddItem("Fullscreen", 4);
    }

    private void LoadSettings(HSlider master, HSlider music, HSlider sfx, OptionButton resolution)
    {
        var config = new ConfigFile();
        var err = config.Load("user://settings.cfg");

        if (err == Error.Ok)
        {
            master.Value = (double)config.GetValue("audio", "master", 1.0);
            music.Value = (double)config.GetValue("audio", "music", 1.0);
            sfx.Value = (double)config.GetValue("audio", "sfx", 1.0);
            resolution.Selected = (int)config.GetValue("video", "resolution", 2);
        }
        else
        {
            // Default values
            master.Value = 1.0;
            music.Value = 1.0;
            sfx.Value = 1.0;
            resolution.Selected = 2; // 1920x1080
        }

        // Apply loaded settings
        ApplyResolution(resolution.Selected);
    }

    private void SaveSettings()
    {
        var config = new ConfigFile();

        var masterSlider = _settingsPanel.GetNode<HSlider>("VBoxContainer/MasterContainer/MasterSlider");
        var musicSlider = _settingsPanel.GetNode<HSlider>("VBoxContainer/MusicContainer/MusicSlider");
        var sfxSlider = _settingsPanel.GetNode<HSlider>("VBoxContainer/SFXContainer/SFXSlider");
        var resolutionOption = _settingsPanel.GetNode<OptionButton>("VBoxContainer/ResolutionContainer/ResolutionOption");

        config.SetValue("audio", "master", masterSlider.Value);
        config.SetValue("audio", "music", musicSlider.Value);
        config.SetValue("audio", "sfx", sfxSlider.Value);
        config.SetValue("video", "resolution", resolutionOption.Selected);

        config.Save("user://settings.cfg");
    }

    private void OnPlayPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
    }

    private void OnSettingsPressed()
    {
        _mainPanel.Visible = false;
        _settingsPanel.Visible = true;
    }

    private void OnSettingsBackPressed()
    {
        SaveSettings();
        _settingsPanel.Visible = false;
        _mainPanel.Visible = true;
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnResolutionChanged(long index)
    {
        ApplyResolution((int)index);
    }

    private void ApplyResolution(int index)
    {
        switch (index)
        {
            case 0: // 1280x720
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(new Vector2I(1280, 720));
                break;
            case 1: // 1600x900
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(new Vector2I(1600, 900));
                break;
            case 2: // 1920x1080
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
                break;
            case 3: // 2560x1440
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                DisplayServer.WindowSetSize(new Vector2I(2560, 1440));
                break;
            case 4: // Fullscreen
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
                break;
        }
    }

    private void OnMasterVolumeChanged(double value)
    {
        AudioManager.Instance?.SetMasterVolume((float)value);
    }

    private void OnMusicVolumeChanged(double value)
    {
        AudioManager.Instance?.SetMusicVolume((float)value);
    }

    private void OnSFXVolumeChanged(double value)
    {
        AudioManager.Instance?.SetSFXVolume((float)value);
    }
}
