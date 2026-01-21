using Godot;
using System.Collections.Generic;

public partial class JellyEffect : Node
{
    [Export]
    public float SpringStiffness { get; set; } = 180.0f;

    [Export]
    public float Damping { get; set; } = 12.0f;

    [Export]
    public float ImpactSquash { get; set; } = 0.6f;

    [Export]
    public float MaxSquash { get; set; } = 0.9f;

    [Export]
    public float WobbleDecay { get; set; } = 4.0f;

    private RigidBody3D _body;
    private List<ShaderMaterial> _materials = new();
    private Vector3 _previousVelocity;
    private float _squashVelocity;
    private float _currentSquash;
    private float _wobbleIntensity;
    private float _wobbleTime;

    // Randomization per instance
    private float _stiffnessMultiplier;
    private float _dampingMultiplier;
    private float _wobblePhase;

    public override void _Ready()
    {
        _body = GetParent<RigidBody3D>();
        if (_body == null)
        {
            GD.PrintErr("JellyEffect: Parent is not a RigidBody3D!");
            return;
        }

        // Randomize parameters for this instance
        _stiffnessMultiplier = (float)GD.RandRange(0.85, 1.15);
        _dampingMultiplier = (float)GD.RandRange(0.9, 1.1);
        _wobblePhase = (float)GD.RandRange(0, Mathf.Tau);

        // Find all meshes and create unique materials for each
        FindMeshesRecursive(_body);
    }

    private void FindMeshesRecursive(Node node)
    {
        if (node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            int surfaceCount = mesh.Mesh.GetSurfaceCount();
            for (int i = 0; i < surfaceCount; i++)
            {
                var existingMat = mesh.GetActiveMaterial(i);

                if (existingMat is ShaderMaterial shaderMat)
                {
                    // Create a unique copy for this instance
                    var uniqueMat = (ShaderMaterial)shaderMat.Duplicate();
                    mesh.SetSurfaceOverrideMaterial(i, uniqueMat);
                    _materials.Add(uniqueMat);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            FindMeshesRecursive(child);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_body == null || _materials.Count == 0) return;

        float dt = (float)delta;
        var currentVelocity = _body.LinearVelocity;

        // Detect impact - sudden change in Y velocity while moving downward
        float prevY = _previousVelocity.Y;
        float currY = currentVelocity.Y;

        // Impact when we were going down and velocity changed significantly
        if (prevY < -1.0f && currY > prevY + 0.5f)
        {
            // Immediate squash - set position directly for snappy response
            float impactStrength = Mathf.Clamp((currY - prevY) / 15.0f, 0.2f, 1.0f);
            _currentSquash = ImpactSquash * impactStrength;
            _squashVelocity = 0; // Reset velocity, let spring take over

            // Wobble intensity based on impact
            _wobbleIntensity = 0.3f * impactStrength;
        }

        // Spring physics with randomized parameters
        float stiffness = SpringStiffness * _stiffnessMultiplier;
        float damping = Damping * _dampingMultiplier;

        // F = -kx - cv (spring force with damping)
        float springForce = -stiffness * _currentSquash;
        float dampingForce = -damping * _squashVelocity;

        _squashVelocity += (springForce + dampingForce) * dt;
        _currentSquash += _squashVelocity * dt;
        _currentSquash = Mathf.Clamp(_currentSquash, -MaxSquash, MaxSquash);

        // Wobble decay
        _wobbleTime += dt;
        _wobbleIntensity = Mathf.Max(0, _wobbleIntensity - WobbleDecay * dt);

        // Update all materials with randomized wobble phase
        foreach (var mat in _materials)
        {
            mat.SetShaderParameter("squash_amount", _currentSquash);
            mat.SetShaderParameter("wobble_amount", _wobbleIntensity);
            mat.SetShaderParameter("wobble_time", _wobbleTime + _wobblePhase);
        }

        _previousVelocity = currentVelocity;
    }
}
