using Godot;

public partial class OrbitingCamera : Camera3D
{
    [Export]
    public float OrbitRadius { get; set; } = 20f;

    [Export]
    public float OrbitHeight { get; set; } = 12f;

    [Export]
    public float OrbitSpeed { get; set; } = 0.15f;

    private float _angle = 0f;
    private Vector3 _targetPosition = Vector3.Zero;

    public override void _Process(double delta)
    {
        _angle += OrbitSpeed * (float)delta;

        // Calculate camera position on orbit
        float x = Mathf.Cos(_angle) * OrbitRadius;
        float z = Mathf.Sin(_angle) * OrbitRadius;

        GlobalPosition = new Vector3(x, OrbitHeight, z);

        // Always look at the center of the board
        LookAt(_targetPosition, Vector3.Up);
    }
}
