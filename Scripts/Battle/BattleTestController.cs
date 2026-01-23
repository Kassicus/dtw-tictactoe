using Godot;
using System;

/// <summary>
/// Test controller for debugging naval combat.
/// Press keys to fire cannons and test damage systems.
/// </summary>
public partial class BattleTestController : Node
{
    [Export] public NodePath BattleManagerPath { get; set; }
    [Export] public bool AutoStartBattle { get; set; } = true;

    private BattleManager _battleManager;

    public override void _Ready()
    {
        if (BattleManagerPath != null && !BattleManagerPath.IsEmpty)
        {
            _battleManager = GetNode<BattleManager>(BattleManagerPath);
        }
        else
        {
            _battleManager = BattleManager.Instance;
        }

        if (_battleManager == null)
        {
            _battleManager = GetNodeOrNull<BattleManager>("/root/NavalBattle/BattleManager");
        }

        if (AutoStartBattle && _battleManager != null)
        {
            CallDeferred(nameof(StartBattleDelayed));
        }

        GD.Print("BattleTestController ready. Controls:");
        GD.Print("  P - Fire player's port cannons");
        GD.Print("  S - Fire player's starboard cannons");
        GD.Print("  E - End command phase (execute)");
        GD.Print("  R - Reset battle");
        GD.Print("  D - Deal damage to player ship (test)");
    }

    private void StartBattleDelayed()
    {
        _battleManager?.StartBattle();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
        {
            HandleKeyPress(keyEvent.Keycode);
        }
    }

    private void HandleKeyPress(Key keycode)
    {
        if (_battleManager == null) return;

        var playerShip = _battleManager.PlayerShip;
        var enemyShip = _battleManager.EnemyShip;

        switch (keycode)
        {
            case Key.P:
                // Fire port broadside at enemy
                if (playerShip != null && enemyShip != null)
                {
                    var targetPos = enemyShip.GetTargetPoint("center");
                    _battleManager.QueueFireCommand(playerShip, ShipCannon.CannonSide.Port, targetPos);
                    GD.Print("Queued port broadside");
                }
                break;

            case Key.S:
                // Fire starboard broadside at enemy
                if (playerShip != null && enemyShip != null)
                {
                    var targetPos = enemyShip.GetTargetPoint("center");
                    _battleManager.QueueFireCommand(playerShip, ShipCannon.CannonSide.Starboard, targetPos);
                    GD.Print("Queued starboard broadside");
                }
                break;

            case Key.E:
                // End command phase
                _battleManager.EndCommandPhase();
                GD.Print("Ending command phase...");
                break;

            case Key.R:
                // Reset battle
                _battleManager.ResetBattle();
                _battleManager.StartBattle();
                GD.Print("Battle reset");
                break;

            case Key.D:
                // Test damage to player ship
                if (playerShip != null)
                {
                    foreach (var component in playerShip.GetAllComponents())
                    {
                        component.TakeDamage(30f);
                        GD.Print($"Dealt 30 damage to {component.ComponentName}");
                        break; // Just damage one component
                    }
                }
                break;

            case Key.Key1:
                // Fire single port cannon
                if (playerShip != null && enemyShip != null)
                {
                    playerShip.FireCannon(0, ShipCannon.CannonSide.Port, enemyShip.GetTargetPoint("center"));
                }
                break;

            case Key.Key2:
                // Fire single starboard cannon
                if (playerShip != null && enemyShip != null)
                {
                    playerShip.FireCannon(0, ShipCannon.CannonSide.Starboard, enemyShip.GetTargetPoint("center"));
                }
                break;
        }
    }
}
