using Godot;
using System;

/// <summary>
/// Ocean controller that manages wave animation and shader parameters.
/// </summary>
public partial class Ocean : MeshInstance3D
{
    [Export] public float WaveSpeed { get; set; } = 0.5f;
    [Export] public float WaveHeight { get; set; } = 0.3f;
    [Export] public float WaveFrequency { get; set; } = 2f;
    [Export] public float FoamAmount { get; set; } = 0.3f;

    [Export] public Color ShallowColor { get; set; } = new Color(0.2f, 0.5f, 0.6f, 0.9f);
    [Export] public Color DeepColor { get; set; } = new Color(0.05f, 0.15f, 0.25f, 1f);

    private ShaderMaterial _material;

    public override void _Ready()
    {
        // Get or create the shader material
        var existingMaterial = GetSurfaceOverrideMaterial(0);
        if (existingMaterial is ShaderMaterial shaderMat)
        {
            _material = shaderMat;
        }
        else
        {
            CreateOceanMaterial();
        }

        UpdateShaderParameters();
    }

    private void CreateOceanMaterial()
    {
        // Load or create the ocean shader
        var shader = GD.Load<Shader>("res://Resources/Shaders/ocean.gdshader");
        if (shader == null)
        {
            GD.PrintErr("Ocean shader not found!");
            return;
        }

        _material = new ShaderMaterial();
        _material.Shader = shader;

        // Create noise texture for wave variation
        var noiseTexture = CreateNoiseTexture();
        _material.SetShaderParameter("noise_texture", noiseTexture);

        SetSurfaceOverrideMaterial(0, _material);
    }

    private NoiseTexture2D CreateNoiseTexture()
    {
        var noise = new FastNoiseLite();
        noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        noise.Frequency = 0.02f;

        var texture = new NoiseTexture2D();
        texture.Width = 512;
        texture.Height = 512;
        texture.Noise = noise;
        texture.Seamless = true;

        return texture;
    }

    public void UpdateShaderParameters()
    {
        if (_material == null) return;

        _material.SetShaderParameter("wave_speed", WaveSpeed);
        _material.SetShaderParameter("wave_height", WaveHeight);
        _material.SetShaderParameter("wave_frequency", WaveFrequency);
        _material.SetShaderParameter("foam_amount", FoamAmount);
        _material.SetShaderParameter("shallow_color", ShallowColor);
        _material.SetShaderParameter("deep_color", DeepColor);
    }

    /// <summary>
    /// Set wave intensity (for storms, calm seas, etc.)
    /// </summary>
    public void SetSeaState(float intensity)
    {
        WaveHeight = Mathf.Lerp(0.1f, 1.5f, intensity);
        WaveSpeed = Mathf.Lerp(0.3f, 1.5f, intensity);
        FoamAmount = Mathf.Lerp(0.1f, 0.8f, intensity);
        UpdateShaderParameters();
    }

    /// <summary>
    /// Get the approximate wave height at a world position.
    /// </summary>
    public float GetWaveHeightAt(Vector3 worldPos)
    {
        float time = (float)Time.GetTicksMsec() / 1000f * WaveSpeed;

        // Match the shader calculation
        float wave1 = Mathf.Sin(worldPos.X * WaveFrequency + time * 1.5f) *
                      Mathf.Cos(worldPos.Z * WaveFrequency * 0.7f + time);

        float wave2 = Mathf.Sin(worldPos.X * WaveFrequency * 0.5f - time) *
                      Mathf.Cos(worldPos.Z * WaveFrequency * 1.2f + time * 0.8f);

        float waveFactor = wave1 * 0.6f + wave2 * 0.3f;
        return waveFactor * WaveHeight;
    }
}
