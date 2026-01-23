using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Handles damage calculation and distribution to ship components.
/// </summary>
public partial class DamageSystem : Node
{
    /// <summary>
    /// Damage type affects which components are most vulnerable.
    /// </summary>
    public enum DamageType
    {
        Solid,      // Standard cannonball - good vs hull
        Chain,      // Chain shot - devastating vs masts/sails
        Grape,      // Grapeshot - anti-personnel (future use)
        Explosive   // Explosive shell - area damage
    }

    /// <summary>
    /// Result of a damage calculation.
    /// </summary>
    public struct DamageResult
    {
        public ShipComponent Target;
        public float DamageDealt;
        public bool WasCriticalHit;
        public bool ComponentDestroyed;
    }

    // Component vulnerability multipliers by damage type
    private static readonly Dictionary<DamageType, Dictionary<Type, float>> VulnerabilityTable = new()
    {
        {
            DamageType.Solid, new Dictionary<Type, float>
            {
                { typeof(HullSection), 1.0f },
                { typeof(Mast), 0.7f },
                { typeof(ShipCannon), 0.8f }
            }
        },
        {
            DamageType.Chain, new Dictionary<Type, float>
            {
                { typeof(HullSection), 0.3f },
                { typeof(Mast), 1.5f },
                { typeof(ShipCannon), 0.5f }
            }
        },
        {
            DamageType.Grape, new Dictionary<Type, float>
            {
                { typeof(HullSection), 0.2f },
                { typeof(Mast), 0.3f },
                { typeof(ShipCannon), 0.6f }
            }
        },
        {
            DamageType.Explosive, new Dictionary<Type, float>
            {
                { typeof(HullSection), 1.2f },
                { typeof(Mast), 1.0f },
                { typeof(ShipCannon), 1.0f }
            }
        }
    };

    [Signal]
    public delegate void DamageDealtEventHandler(ShipComponent target, float damageDealt, bool wasCritical, bool wasDestroyed);

    /// <summary>
    /// Calculate and apply damage to a component.
    /// </summary>
    public DamageResult ApplyDamage(ShipComponent component, float baseDamage, DamageType damageType = DamageType.Solid)
    {
        var result = new DamageResult
        {
            Target = component,
            WasCriticalHit = false,
            ComponentDestroyed = false
        };

        if (component == null || component.IsDestroyed)
        {
            result.DamageDealt = 0;
            return result;
        }

        // Get vulnerability multiplier
        float vulnerability = GetVulnerability(component.GetType(), damageType);

        // Calculate final damage
        float finalDamage = baseDamage * vulnerability;

        // Critical hit chance (10% base)
        if (GD.Randf() < 0.1f)
        {
            finalDamage *= 1.5f;
            result.WasCriticalHit = true;
        }

        // Apply damage
        result.DamageDealt = component.TakeDamage(finalDamage);
        result.ComponentDestroyed = component.IsDestroyed;

        EmitSignal(SignalName.DamageDealt, result.Target, result.DamageDealt, result.WasCriticalHit, result.ComponentDestroyed);

        return result;
    }

    /// <summary>
    /// Apply area damage to multiple components.
    /// </summary>
    public List<DamageResult> ApplyAreaDamage(
        IEnumerable<ShipComponent> components,
        Vector3 impactPoint,
        float baseDamage,
        float radius,
        DamageType damageType = DamageType.Explosive)
    {
        var results = new List<DamageResult>();

        foreach (var component in components)
        {
            if (component == null || component.IsDestroyed) continue;

            float distance = component.GlobalPosition.DistanceTo(impactPoint);
            if (distance > radius) continue;

            // Damage falls off with distance
            float falloff = 1f - (distance / radius);
            falloff = Mathf.Max(0.2f, falloff); // Minimum 20% damage at edge

            var result = ApplyDamage(component, baseDamage * falloff, damageType);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Get vulnerability multiplier for a component type vs damage type.
    /// </summary>
    private float GetVulnerability(Type componentType, DamageType damageType)
    {
        if (VulnerabilityTable.TryGetValue(damageType, out var typeTable))
        {
            if (typeTable.TryGetValue(componentType, out var multiplier))
            {
                return multiplier;
            }
        }
        return 1.0f; // Default vulnerability
    }

    /// <summary>
    /// Calculate accuracy modifier based on conditions.
    /// </summary>
    public static float CalculateAccuracyModifier(
        float distance,
        float cannonDamagePercent,
        bool targetMoving = false,
        float windStrength = 0f)
    {
        float accuracy = 1.0f;

        // Distance penalty (starts at 20 units)
        if (distance > 20f)
        {
            accuracy -= (distance - 20f) * 0.02f;
        }

        // Cannon damage penalty
        accuracy *= cannonDamagePercent;

        // Moving target penalty
        if (targetMoving)
        {
            accuracy *= 0.8f;
        }

        // Wind penalty
        accuracy -= windStrength * 0.1f;

        return Mathf.Clamp(accuracy, 0.1f, 1.0f);
    }

    /// <summary>
    /// Determine which component should be hit based on aim point.
    /// </summary>
    public static ShipComponent DetermineHitComponent(Ship ship, Vector3 aimPoint, float accuracy)
    {
        if (ship == null) return null;

        // Apply accuracy scatter
        float scatter = (1f - accuracy) * 5f; // Max 5 unit scatter at 0% accuracy
        Vector3 actualHitPoint = aimPoint + new Vector3(
            (float)GD.RandRange(-scatter, scatter),
            (float)GD.RandRange(-scatter, scatter),
            (float)GD.RandRange(-scatter, scatter)
        );

        // Find closest component to hit point
        ShipComponent closestComponent = null;
        float closestDistance = float.MaxValue;

        foreach (var component in ship.GetAllComponents())
        {
            float distance = component.GlobalPosition.DistanceTo(actualHitPoint);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestComponent = component;
            }
        }

        return closestComponent;
    }
}
