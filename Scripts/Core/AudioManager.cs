using Godot;
using System.Collections.Generic;

public partial class AudioManager : Node
{
    public static AudioManager Instance { get; private set; }

    private AudioStreamPlayer _musicPlayer;
    private AudioStreamPlayer _musicPlayerCrossfade;
    private Tween _fadeTween;

    // Music library
    private List<AudioStream> _tracks = new();
    private List<int> _shuffledIndices = new();
    private int _currentIndex = -1;

    // SFX library
    private Dictionary<string, AudioStream> _sfx = new();
    private Dictionary<string, List<AudioStream>> _sfxGroups = new();
    private List<AudioStreamPlayer> _sfxPool = new();
    private const int SfxPoolSize = 8;

    // Current state
    private bool _isPlaying = false;
    private float _musicVolume = 1.0f;
    private const float FadeDuration = 1.5f;

    public override void _Ready()
    {
        Instance = this;

        // Create two music players for crossfading
        _musicPlayer = new AudioStreamPlayer();
        _musicPlayer.Bus = "Music";
        _musicPlayer.Finished += OnTrackFinished;
        AddChild(_musicPlayer);

        _musicPlayerCrossfade = new AudioStreamPlayer();
        _musicPlayerCrossfade.Bus = "Music";
        AddChild(_musicPlayerCrossfade);

        // Create SFX player pool
        for (int i = 0; i < SfxPoolSize; i++)
        {
            var sfxPlayer = new AudioStreamPlayer();
            sfxPlayer.Bus = "SFX";
            AddChild(sfxPlayer);
            _sfxPool.Add(sfxPlayer);
        }

        // Load all tracks from the Music folder
        LoadTracks();

        // Load all SFX
        LoadSFX();

        // Load saved volume settings
        LoadVolumeSettings();

        // Load saved video settings (resolution)
        LoadVideoSettings();

        // Auto-start music
        StartMusic();
    }

    private void LoadTracks()
    {
        var musicPath = "res://Resources/Music/";
        var dir = DirAccess.Open(musicPath);

        if (dir == null)
        {
            GD.Print("Music folder not found, creating it...");
            DirAccess.MakeDirAbsolute(ProjectSettings.GlobalizePath(musicPath));
            return;
        }

        dir.ListDirBegin();
        var fileName = dir.GetNext();

        while (fileName != "")
        {
            if (!dir.CurrentIsDir())
            {
                // Check for audio files (skip .import files)
                if (fileName.EndsWith(".ogg") || fileName.EndsWith(".mp3") || fileName.EndsWith(".wav"))
                {
                    var stream = GD.Load<AudioStream>(musicPath + fileName);
                    if (stream != null)
                    {
                        _tracks.Add(stream);
                        GD.Print($"Loaded music track: {fileName}");
                    }
                }
            }
            fileName = dir.GetNext();
        }

        dir.ListDirEnd();

        // Initial shuffle
        Shuffle();
    }

    private void LoadSFX()
    {
        var sfxPath = "res://Resources/SFX/";
        var dir = DirAccess.Open(sfxPath);

        if (dir == null)
        {
            GD.Print("SFX folder not found, creating it...");
            DirAccess.MakeDirAbsolute(ProjectSettings.GlobalizePath(sfxPath));
            return;
        }

        dir.ListDirBegin();
        var fileName = dir.GetNext();

        while (fileName != "")
        {
            if (dir.CurrentIsDir() && !fileName.StartsWith("."))
            {
                // Load subfolder as an SFX group (for random selection)
                LoadSFXGroup(sfxPath + fileName + "/", fileName);
            }
            else if (!dir.CurrentIsDir())
            {
                if (fileName.EndsWith(".ogg") || fileName.EndsWith(".mp3") || fileName.EndsWith(".wav"))
                {
                    var sfxName = fileName.GetBaseName();
                    var stream = GD.Load<AudioStream>(sfxPath + fileName);
                    if (stream != null)
                    {
                        _sfx[sfxName] = stream;
                        GD.Print($"Loaded SFX: {sfxName}");
                    }
                }
            }
            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
    }

    private void LoadSFXGroup(string path, string groupName)
    {
        var dir = DirAccess.Open(path);
        if (dir == null) return;

        var sounds = new List<AudioStream>();

        dir.ListDirBegin();
        var fileName = dir.GetNext();

        while (fileName != "")
        {
            if (!dir.CurrentIsDir())
            {
                if (fileName.EndsWith(".ogg") || fileName.EndsWith(".mp3") || fileName.EndsWith(".wav"))
                {
                    var stream = GD.Load<AudioStream>(path + fileName);
                    if (stream != null)
                    {
                        sounds.Add(stream);
                    }
                }
            }
            fileName = dir.GetNext();
        }

        dir.ListDirEnd();

        if (sounds.Count > 0)
        {
            _sfxGroups[groupName] = sounds;
            GD.Print($"Loaded SFX group '{groupName}' with {sounds.Count} sounds");
        }
    }

    public void PlaySFX(string sfxName, float pitchVariation = 0f)
    {
        if (!_sfx.ContainsKey(sfxName))
        {
            GD.PrintErr($"SFX not found: {sfxName}");
            return;
        }

        // Find an available player from the pool
        AudioStreamPlayer player = null;
        foreach (var p in _sfxPool)
        {
            if (!p.Playing)
            {
                player = p;
                break;
            }
        }

        // If all players are busy, use the first one (cut off oldest sound)
        player ??= _sfxPool[0];

        player.Stream = _sfx[sfxName];

        // Apply pitch variation if specified
        if (pitchVariation > 0)
        {
            player.PitchScale = 1.0f + (float)GD.RandRange(-pitchVariation, pitchVariation);
        }
        else
        {
            player.PitchScale = 1.0f;
        }

        player.Play();
    }

    public void PlaySFXGroup(string groupName, float pitchVariation = 0f)
    {
        if (!_sfxGroups.ContainsKey(groupName))
        {
            GD.PrintErr($"SFX group not found: {groupName}");
            return;
        }

        var sounds = _sfxGroups[groupName];
        var randomIndex = (int)(GD.Randi() % sounds.Count);
        var stream = sounds[randomIndex];

        // Find an available player from the pool
        AudioStreamPlayer player = null;
        foreach (var p in _sfxPool)
        {
            if (!p.Playing)
            {
                player = p;
                break;
            }
        }

        player ??= _sfxPool[0];

        player.Stream = stream;

        if (pitchVariation > 0)
        {
            player.PitchScale = 1.0f + (float)GD.RandRange(-pitchVariation, pitchVariation);
        }
        else
        {
            player.PitchScale = 1.0f;
        }

        player.Play();
    }

    public void PlayPieceLand()
    {
        // Try group first, fall back to single file
        if (_sfxGroups.ContainsKey("PieceLand"))
        {
            PlaySFXGroup("PieceLand", 0.1f);
        }
        else if (_sfx.ContainsKey("piece_land"))
        {
            PlaySFX("piece_land", 0.1f);
        }
    }

    public void PlayCannonFire()
    {
        // Try group first, fall back to single file
        if (_sfxGroups.ContainsKey("CannonFire"))
        {
            PlaySFXGroup("CannonFire", 0.15f);
        }
        else if (_sfx.ContainsKey("cannon_fire"))
        {
            PlaySFX("cannon_fire", 0.15f);
        }
    }

    private void Shuffle()
    {
        _shuffledIndices.Clear();
        for (int i = 0; i < _tracks.Count; i++)
        {
            _shuffledIndices.Add(i);
        }

        // Fisher-Yates shuffle
        for (int i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            int j = (int)(GD.Randi() % (i + 1));
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }
    }

    public void StartMusic()
    {
        if (_tracks.Count == 0) return;
        if (_isPlaying) return;

        _isPlaying = true;
        PlayNextTrack();
    }

    private void PlayNextTrack()
    {
        if (_tracks.Count == 0) return;

        _currentIndex++;
        if (_currentIndex >= _shuffledIndices.Count)
        {
            // Reshuffle when we've played all tracks
            Shuffle();
            _currentIndex = 0;
        }

        var trackIndex = _shuffledIndices[_currentIndex];
        var newStream = _tracks[trackIndex];

        if (_musicPlayer.Playing)
        {
            CrossfadeTo(newStream);
        }
        else
        {
            _musicPlayer.Stream = newStream;
            _musicPlayer.VolumeDb = Mathf.LinearToDb(_musicVolume);
            _musicPlayer.Play();
        }
    }

    private void OnTrackFinished()
    {
        if (_isPlaying)
        {
            PlayNextTrack();
        }
    }

    private void CrossfadeTo(AudioStream newStream)
    {
        // Kill any existing fade
        _fadeTween?.Kill();

        // Swap players - crossfade becomes main
        (_musicPlayer, _musicPlayerCrossfade) = (_musicPlayerCrossfade, _musicPlayer);

        // Reconnect finished signal to new main player
        if (_musicPlayerCrossfade.IsConnected(AudioStreamPlayer.SignalName.Finished, Callable.From(OnTrackFinished)))
        {
            _musicPlayerCrossfade.Finished -= OnTrackFinished;
        }
        if (!_musicPlayer.IsConnected(AudioStreamPlayer.SignalName.Finished, Callable.From(OnTrackFinished)))
        {
            _musicPlayer.Finished += OnTrackFinished;
        }

        // Setup new track on the new main player
        _musicPlayer.Stream = newStream;
        _musicPlayer.VolumeDb = Mathf.LinearToDb(0.0001f); // Start silent
        _musicPlayer.Play();

        // Create crossfade tween
        _fadeTween = CreateTween();
        _fadeTween.SetParallel(true);

        // Fade in new track
        _fadeTween.TweenMethod(
            Callable.From<float>(vol => _musicPlayer.VolumeDb = Mathf.LinearToDb(vol)),
            0.0001f, _musicVolume, FadeDuration
        );

        // Fade out old track
        _fadeTween.TweenMethod(
            Callable.From<float>(vol => _musicPlayerCrossfade.VolumeDb = Mathf.LinearToDb(vol)),
            _musicVolume, 0.0001f, FadeDuration
        );

        _fadeTween.Chain().TweenCallback(Callable.From(() => _musicPlayerCrossfade.Stop()));
    }

    public void StopMusic(bool fade = true)
    {
        _isPlaying = false;

        if (!_musicPlayer.Playing) return;

        if (fade)
        {
            _fadeTween?.Kill();
            _fadeTween = CreateTween();
            _fadeTween.TweenMethod(
                Callable.From<float>(vol => _musicPlayer.VolumeDb = Mathf.LinearToDb(vol)),
                _musicVolume, 0.0001f, FadeDuration
            );
            _fadeTween.TweenCallback(Callable.From(() => _musicPlayer.Stop()));
        }
        else
        {
            _musicPlayer.Stop();
        }
    }

    public void SkipTrack()
    {
        if (_tracks.Count == 0) return;
        PlayNextTrack();
    }

    public void SetMasterVolume(float volume)
    {
        var busIndex = AudioServer.GetBusIndex("Master");
        AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(volume));
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = volume;
        var busIndex = AudioServer.GetBusIndex("Music");
        AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(volume));
    }

    public void SetSFXVolume(float volume)
    {
        var busIndex = AudioServer.GetBusIndex("SFX");
        AudioServer.SetBusVolumeDb(busIndex, Mathf.LinearToDb(volume));
    }

    private void LoadVolumeSettings()
    {
        var config = new ConfigFile();
        var err = config.Load("user://settings.cfg");

        if (err == Error.Ok)
        {
            SetMasterVolume((float)(double)config.GetValue("audio", "master", 1.0));
            SetMusicVolume((float)(double)config.GetValue("audio", "music", 1.0));
            SetSFXVolume((float)(double)config.GetValue("audio", "sfx", 1.0));
        }
    }

    private void LoadVideoSettings()
    {
        var config = new ConfigFile();
        var err = config.Load("user://settings.cfg");

        int resolutionIndex = 2; // Default to 1920x1080
        if (err == Error.Ok)
        {
            resolutionIndex = (int)config.GetValue("video", "resolution", 2);
        }

        ApplyResolution(resolutionIndex);
    }

    /// <summary>
    /// Apply the resolution setting. Can be called from anywhere to ensure consistent resolution.
    /// </summary>
    public static void ApplyResolution(int index)
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

    public int TrackCount => _tracks.Count;
}
