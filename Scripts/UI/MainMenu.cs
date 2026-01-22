using Godot;

public partial class MainMenu : Control
{
    // Static flag to signal that a saved game should be loaded
    public static GameSaveData PendingLoadData { get; set; }

    private Button _playButton;
    private Button _loadGameButton;
    private Button _settingsButton;
    private Button _quitButton;
    private Control _settingsPanel;
    private Control _mainPanel;
    private Control _gameModePanel;
    private SubViewport _backgroundViewport;

    public override void _Ready()
    {
        _mainPanel = GetNode<Control>("MainPanel");
        _playButton = GetNode<Button>("MainPanel/VBoxContainer/PlayButton");
        _loadGameButton = GetNode<Button>("MainPanel/VBoxContainer/LoadGameButton");
        _settingsButton = GetNode<Button>("MainPanel/VBoxContainer/SettingsButton");
        _quitButton = GetNode<Button>("MainPanel/VBoxContainer/QuitButton");
        _settingsPanel = GetNode<Control>("SettingsPanel");
        _gameModePanel = GetNode<Control>("GameModePanel");
        _backgroundViewport = GetNode<SubViewport>("SubViewportContainer/SubViewport");

        _playButton.Pressed += OnPlayPressed;
        _loadGameButton.Pressed += OnLoadGamePressed;
        _settingsButton.Pressed += OnSettingsPressed;
        _quitButton.Pressed += OnQuitPressed;

        // Enable/disable load button based on save existence
        UpdateLoadButtonState();

        // Sync SubViewport size with window size
        GetTree().Root.SizeChanged += OnWindowSizeChanged;
        UpdateSubViewportSize();

        // Connect settings panel back button
        var backButton = _settingsPanel.GetNode<Button>("VBoxContainer/BackButton");
        backButton.Pressed += OnSettingsBackPressed;

        // Connect game mode panel buttons
        var pvpButton = _gameModePanel.GetNode<Button>("VBoxContainer/PlayerVsPlayerButton");
        var pvcButton = _gameModePanel.GetNode<Button>("VBoxContainer/PlayerVsCPUButton");
        var gameModeBackButton = _gameModePanel.GetNode<Button>("VBoxContainer/BackButton");

        pvpButton.Pressed += OnPlayerVsPlayerPressed;
        pvcButton.Pressed += OnPlayerVsCPUPressed;
        gameModeBackButton.Pressed += OnGameModeBackPressed;

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
        _mainPanel.Visible = false;
        _gameModePanel.Visible = true;
    }

    private void OnPlayerVsPlayerPressed()
    {
        GameManager.CurrentGameMode = GameMode.PlayerVsPlayer;
        GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
    }

    private void OnPlayerVsCPUPressed()
    {
        GameManager.CurrentGameMode = GameMode.PlayerVsCPU;
        GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
    }

    private void OnGameModeBackPressed()
    {
        _gameModePanel.Visible = false;
        _mainPanel.Visible = true;
    }

    private void OnLoadGamePressed()
    {
        var saveData = SaveManager.LoadGame();
        if (saveData != null)
        {
            // Store the save data to be loaded by GameManager
            PendingLoadData = saveData;
            GameManager.CurrentGameMode = saveData.GameMode;
            GetTree().ChangeSceneToFile("res://Scenes/Main.tscn");
        }
        else
        {
            // Update button to show error
            _loadGameButton.Text = "No Save Found";
            GetTree().CreateTimer(1.5f).Timeout += () =>
            {
                _loadGameButton.Text = "Load Game";
                UpdateLoadButtonState();
            };
        }
    }

    private void UpdateLoadButtonState()
    {
        bool saveExists = SaveManager.SaveExists();
        _loadGameButton.Disabled = !saveExists;
        if (!saveExists)
        {
            _loadGameButton.Modulate = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
        else
        {
            _loadGameButton.Modulate = new Color(1f, 1f, 1f, 1f);
        }
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
        // Use shared implementation from AudioManager
        AudioManager.ApplyResolution(index);

        // Update SubViewport size after resolution change
        // Use deferred call to ensure window size is updated first
        CallDeferred(nameof(UpdateSubViewportSize));
    }

    private void OnWindowSizeChanged()
    {
        UpdateSubViewportSize();
    }

    private void UpdateSubViewportSize()
    {
        if (_backgroundViewport != null)
        {
            var windowSize = GetTree().Root.Size;
            _backgroundViewport.Size = windowSize;
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
