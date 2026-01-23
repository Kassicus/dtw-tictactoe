using Godot;
using System;

/// <summary>
/// Health tracking system for ship components with visual state management.
/// Handles damage states: Healthy, Damaged, Critical, Destroyed.
/// </summary>
public partial class ComponentHealth : Node
{
    [Export] public float MaxHealth { get; set; } = 100f;
    [Export] public float CurrentHealth { get; private set; } = 100f;

    // Damage state thresholds (percentage of max health)
    private const float DamagedThreshold = 0.75f;
    private const float CriticalThreshold = 0.25f;
    private const float DestroyedThreshold = 0f;

    public enum DamageState
    {
        Healthy,    // 100-75%
        Damaged,    // 75-25%
        Critical,   // 25-1%
        Destroyed   // 0%
    }

    public DamageState State { get; private set; } = DamageState.Healthy;
    public float HealthPercentage => MaxHealth > 0 ? CurrentHealth / MaxHealth : 0f;
    public bool IsDestroyed => State == DamageState.Destroyed;

    [Signal]
    public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth, int state);

    [Signal]
    public delegate void DamageStateChangedEventHandler(int oldState, int newState);

    [Signal]
    public delegate void DestroyedEventHandler();

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        UpdateDamageState();
    }

    /// <summary>
    /// Apply damage to this component.
    /// </summary>
    /// <param name="amount">Amount of damage to apply</param>
    /// <returns>Actual damage dealt</returns>
    public float TakeDamage(float amount)
    {
        if (IsDestroyed || amount <= 0) return 0f;

        float oldHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        float actualDamage = oldHealth - CurrentHealth;

        UpdateDamageState();
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth, (int)State);

        return actualDamage;
    }

    /// <summary>
    /// Repair this component.
    /// </summary>
    /// <param name="amount">Amount of health to restore</param>
    /// <returns>Actual health restored</returns>
    public float Repair(float amount)
    {
        if (amount <= 0) return 0f;

        float oldHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
        float actualRepair = CurrentHealth - oldHealth;

        UpdateDamageState();
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth, (int)State);

        return actualRepair;
    }

    /// <summary>
    /// Set health directly (for loading saved states).
    /// </summary>
    public void SetHealth(float health)
    {
        CurrentHealth = Mathf.Clamp(health, 0f, MaxHealth);
        UpdateDamageState();
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth, (int)State);
    }

    /// <summary>
    /// Reset to full health.
    /// </summary>
    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        UpdateDamageState();
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth, (int)State);
    }

    private void UpdateDamageState()
    {
        var oldState = State;
        float percentage = HealthPercentage;

        if (percentage <= DestroyedThreshold)
        {
            State = DamageState.Destroyed;
        }
        else if (percentage <= CriticalThreshold)
        {
            State = DamageState.Critical;
        }
        else if (percentage <= DamagedThreshold)
        {
            State = DamageState.Damaged;
        }
        else
        {
            State = DamageState.Healthy;
        }

        if (oldState != State)
        {
            EmitSignal(SignalName.DamageStateChanged, (int)oldState, (int)State);

            if (State == DamageState.Destroyed)
            {
                EmitSignal(SignalName.Destroyed);
            }
        }
    }

    /// <summary>
    /// Get effectiveness multiplier based on damage state.
    /// Healthy = 1.0, Damaged = 0.75, Critical = 0.5, Destroyed = 0
    /// </summary>
    public float GetEffectivenessMultiplier()
    {
        return State switch
        {
            DamageState.Healthy => 1.0f,
            DamageState.Damaged => 0.75f,
            DamageState.Critical => 0.5f,
            DamageState.Destroyed => 0f,
            _ => 1.0f
        };
    }
}
