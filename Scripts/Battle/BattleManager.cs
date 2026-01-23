using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages the turn-based naval combat system.
/// Replaces GameManager for the Broadside game mode.
/// </summary>
public partial class BattleManager : Node
{
    public static BattleManager Instance { get; private set; }

    public enum BattlePhase
    {
        Setup,          // Ships positioning
        CommandPhase,   // Player issuing orders
        ExecutionPhase, // Orders being carried out
        ResolutionPhase,// Applying damage, checking win/loss
        Victory,        // Battle over
        Defeat
    }

    public enum BattleSide
    {
        Player,
        Enemy
    }

    // Battle state
    public BattlePhase CurrentPhase { get; private set; } = BattlePhase.Setup;
    public BattleSide CurrentTurn { get; private set; } = BattleSide.Player;
    public int TurnNumber { get; private set; } = 1;

    // Ships
    [Export] public NodePath PlayerShipPath { get; set; }
    [Export] public NodePath EnemyShipPath { get; set; }

    private Ship _playerShip;
    private Ship _enemyShip;

    // Pending commands for execution
    private List<BattleCommand> _pendingCommands = new();

    // Signals
    [Signal]
    public delegate void PhaseChangedEventHandler(int newPhase);

    [Signal]
    public delegate void TurnChangedEventHandler(int side, int turnNumber);

    [Signal]
    public delegate void BattleEndedEventHandler(int winner);

    [Signal]
    public delegate void CommandExecutedEventHandler();

    public override void _Ready()
    {
        Instance = this;

        // Find ships
        if (PlayerShipPath != null && !PlayerShipPath.IsEmpty)
        {
            _playerShip = GetNode<Ship>(PlayerShipPath);
        }
        if (EnemyShipPath != null && !EnemyShipPath.IsEmpty)
        {
            _enemyShip = GetNode<Ship>(EnemyShipPath);
        }

        // Connect ship signals
        if (_playerShip != null)
        {
            _playerShip.ShipSunk += OnShipSunk;
        }
        if (_enemyShip != null)
        {
            _enemyShip.ShipSunk += OnShipSunk;
        }

        GD.Print("BattleManager initialized");
    }

    /// <summary>
    /// Start the battle.
    /// </summary>
    public void StartBattle()
    {
        TurnNumber = 1;
        CurrentTurn = BattleSide.Player;
        SetPhase(BattlePhase.CommandPhase);

        EmitSignal(SignalName.TurnChanged, (int)CurrentTurn, TurnNumber);
        GD.Print($"Battle started! Turn {TurnNumber} - {CurrentTurn}'s turn");
    }

    /// <summary>
    /// Queue a fire command for execution.
    /// </summary>
    public void QueueFireCommand(Ship firingShip, ShipCannon.CannonSide side, Vector3 targetPos)
    {
        // Determine target ship based on who is firing
        Ship targetShip = firingShip == _playerShip ? _enemyShip : _playerShip;
        QueueFireCommand(firingShip, side, targetShip, targetPos);
    }

    /// <summary>
    /// Queue a fire command for execution with explicit target ship.
    /// </summary>
    public void QueueFireCommand(Ship firingShip, ShipCannon.CannonSide side, Ship targetShip, Vector3 targetPos)
    {
        if (CurrentPhase != BattlePhase.CommandPhase)
        {
            GD.PrintErr("Cannot queue commands outside of command phase");
            return;
        }

        var command = new BattleCommand
        {
            Type = BattleCommand.CommandType.FireBroadside,
            SourceShip = firingShip,
            TargetShip = targetShip,
            CannonSide = side,
            TargetPosition = targetPos
        };

        _pendingCommands.Add(command);
        GD.Print($"Queued fire command: {firingShip.ShipName} firing {side} broadside at {targetShip?.ShipName ?? "position"}");
    }

    /// <summary>
    /// End the command phase and execute all queued commands.
    /// </summary>
    public void EndCommandPhase()
    {
        if (CurrentPhase != BattlePhase.CommandPhase) return;

        SetPhase(BattlePhase.ExecutionPhase);
        ExecuteCommands();
    }

    private async void ExecuteCommands()
    {
        foreach (var command in _pendingCommands)
        {
            await ExecuteCommand(command);
            EmitSignal(SignalName.CommandExecuted);

            // Brief pause between commands for dramatic effect
            await ToSignal(GetTree().CreateTimer(0.5f), SceneTreeTimer.SignalName.Timeout);
        }

        _pendingCommands.Clear();

        // Move to resolution phase
        SetPhase(BattlePhase.ResolutionPhase);

        // Wait for projectiles to land
        await ToSignal(GetTree().CreateTimer(2f), SceneTreeTimer.SignalName.Timeout);

        // Check for battle end
        CheckBattleEnd();
    }

    private async System.Threading.Tasks.Task ExecuteCommand(BattleCommand command)
    {
        switch (command.Type)
        {
            case BattleCommand.CommandType.FireBroadside:
                // Use distributed broadside targeting
                command.SourceShip?.FireBroadsideAtShip(command.CannonSide, command.TargetShip, command.TargetPosition);
                break;

            case BattleCommand.CommandType.FireSingleCannon:
                command.SourceShip?.FireCannon(command.CannonIndex, command.CannonSide, command.TargetPosition);
                break;
        }

        // Wait for cannons to fire
        await ToSignal(GetTree().CreateTimer(1f), SceneTreeTimer.SignalName.Timeout);
    }

    private void CheckBattleEnd()
    {
        if (_playerShip != null && _playerShip.IsSunk)
        {
            SetPhase(BattlePhase.Defeat);
            EmitSignal(SignalName.BattleEnded, (int)BattleSide.Enemy);
            GD.Print("Battle ended - Player defeated!");
            return;
        }

        if (_enemyShip != null && _enemyShip.IsSunk)
        {
            SetPhase(BattlePhase.Victory);
            EmitSignal(SignalName.BattleEnded, (int)BattleSide.Player);
            GD.Print("Battle ended - Player victory!");
            return;
        }

        // Continue to next turn
        NextTurn();
    }

    private void NextTurn()
    {
        // Switch sides
        CurrentTurn = CurrentTurn == BattleSide.Player ? BattleSide.Enemy : BattleSide.Player;

        // Increment turn if back to player
        if (CurrentTurn == BattleSide.Player)
        {
            TurnNumber++;
        }

        SetPhase(BattlePhase.CommandPhase);
        EmitSignal(SignalName.TurnChanged, (int)CurrentTurn, TurnNumber);

        GD.Print($"Turn {TurnNumber} - {CurrentTurn}'s turn");

        // If enemy turn, trigger AI
        if (CurrentTurn == BattleSide.Enemy)
        {
            ExecuteEnemyTurn();
        }
    }

    private async void ExecuteEnemyTurn()
    {
        // Simple AI: fire broadside at player ship
        if (_enemyShip == null || _playerShip == null) return;

        // Determine which side faces the player based on relative position
        var toPlayer = _playerShip.GlobalPosition - _enemyShip.GlobalPosition;
        var enemyRight = _enemyShip.GlobalTransform.Basis.X;
        var dot = toPlayer.Normalized().Dot(enemyRight);

        // If player is to the right (+X local), use starboard; otherwise port
        var side = dot > 0 ? ShipCannon.CannonSide.Starboard : ShipCannon.CannonSide.Port;

        GD.Print($"Enemy AI: Player is to the {(dot > 0 ? "starboard" : "port")} side, firing {side} cannons");

        // Target the center of the player ship - broadside will distribute shots
        var targetPos = _playerShip.GetTargetPoint("center");

        // Brief "thinking" delay
        await ToSignal(GetTree().CreateTimer(1f), SceneTreeTimer.SignalName.Timeout);

        QueueFireCommand(_enemyShip, side, targetPos);
        EndCommandPhase();
    }

    private void OnShipSunk(Ship ship)
    {
        GD.Print($"{ship.ShipName} has been sunk!");
        // Battle end will be handled in resolution phase
    }

    private void SetPhase(BattlePhase phase)
    {
        CurrentPhase = phase;
        EmitSignal(SignalName.PhaseChanged, (int)phase);
        GD.Print($"Phase changed to: {phase}");
    }

    #region Public API

    public Ship PlayerShip => _playerShip;
    public Ship EnemyShip => _enemyShip;

    /// <summary>
    /// Get the opposing ship.
    /// </summary>
    public Ship GetOpponent(Ship ship)
    {
        if (ship == _playerShip) return _enemyShip;
        if (ship == _enemyShip) return _playerShip;
        return null;
    }

    /// <summary>
    /// Reset the battle.
    /// </summary>
    public void ResetBattle()
    {
        _playerShip?.ResetShip();
        _enemyShip?.ResetShip();
        _pendingCommands.Clear();
        TurnNumber = 1;
        CurrentTurn = BattleSide.Player;
        SetPhase(BattlePhase.Setup);
    }

    #endregion
}

/// <summary>
/// Represents a queued battle command.
/// </summary>
public partial class BattleCommand : GodotObject
{
    public enum CommandType
    {
        FireBroadside,
        FireSingleCannon,
        Move, // Future expansion
        Repair // Future expansion
    }

    public CommandType Type { get; set; }
    public Ship SourceShip { get; set; }
    public Ship TargetShip { get; set; }
    public ShipCannon.CannonSide CannonSide { get; set; }
    public int CannonIndex { get; set; }
    public Vector3 TargetPosition { get; set; }
}
