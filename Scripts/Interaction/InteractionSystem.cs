using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages detection and highlighting of nearby interaction points.
/// </summary>
public partial class InteractionSystem : Node
{
    [Export] public float InteractionRange { get; set; } = 2.5f;

    private List<InteractionPoint> _nearbyPoints = new();
    private InteractionPoint _closestPoint;

    [Signal]
    public delegate void NearestPointChangedEventHandler(InteractionPoint point);

    public override void _Process(double delta)
    {
        UpdateNearestPoint();
    }

    /// <summary>
    /// Register an interaction point.
    /// </summary>
    public void RegisterPoint(InteractionPoint point)
    {
        if (!_nearbyPoints.Contains(point))
        {
            _nearbyPoints.Add(point);
        }
    }

    /// <summary>
    /// Unregister an interaction point.
    /// </summary>
    public void UnregisterPoint(InteractionPoint point)
    {
        _nearbyPoints.Remove(point);
        if (_closestPoint == point)
        {
            _closestPoint = null;
        }
    }

    /// <summary>
    /// Get the nearest interaction point to a position.
    /// </summary>
    public InteractionPoint GetNearestInteractionPoint(Vector3 position)
    {
        InteractionPoint nearest = null;
        float nearestDist = float.MaxValue;

        // Find all interaction points in the scene
        var points = FindInteractionPoints();

        foreach (var point in points)
        {
            if (!point.IsEnabled) continue;

            float dist = point.GlobalPosition.DistanceTo(position);
            if (dist < InteractionRange && dist < nearestDist)
            {
                nearestDist = dist;
                nearest = point;
            }
        }

        return nearest;
    }

    private void UpdateNearestPoint()
    {
        var parent = GetParent();
        if (parent is not Node3D node3D) return;

        var nearest = GetNearestInteractionPoint(node3D.GlobalPosition);

        if (nearest != _closestPoint)
        {
            // Unhighlight old point
            _closestPoint?.SetHighlighted(false);

            _closestPoint = nearest;

            // Highlight new point
            _closestPoint?.SetHighlighted(true);

            EmitSignal(SignalName.NearestPointChanged, _closestPoint);
        }
    }

    private List<InteractionPoint> FindInteractionPoints()
    {
        var points = new List<InteractionPoint>();

        // Get all nodes in the scene
        var root = GetTree().CurrentScene;
        FindInteractionPointsRecursive(root, points);

        return points;
    }

    private void FindInteractionPointsRecursive(Node node, List<InteractionPoint> points)
    {
        if (node is InteractionPoint point)
        {
            points.Add(point);
        }

        foreach (var child in node.GetChildren())
        {
            FindInteractionPointsRecursive(child, points);
        }
    }

    /// <summary>
    /// Get the currently closest interaction point.
    /// </summary>
    public InteractionPoint ClosestPoint => _closestPoint;
}
