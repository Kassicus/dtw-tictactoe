using Godot;
using System;

/// <summary>
/// Main battle HUD displaying ship status, turn info, and action buttons.
/// </summary>
public partial class BattleHUD : Control
{
    // References to UI elements
    private Label _turnLabel;
    private Label _phaseLabel;
    private ProgressBar _playerHealthBar;
    private ProgressBar _enemyHealthBar;
    private Label _playerShipName;
    private Label _enemyShipName;
    private Button _firePortButton;
    private Button _fireStarboardButton;
    private Button _endTurnButton;
    private Panel _interactionPrompt;
    private Label _interactionText;

    // References
    private BattleManager _battleManager;

    public override void _Ready()
    {
        // Find or create UI elements
        CreateUI();

        // Find battle manager
        _battleManager = BattleManager.Instance;
        if (_battleManager == null)
        {
            _battleManager = GetNodeOrNull<BattleManager>("/root/NavalBattle/BattleManager");
        }

        // Connect signals
        if (_battleManager != null)
        {
            _battleManager.PhaseChanged += OnPhaseChanged;
            _battleManager.TurnChanged += OnTurnChanged;
            _battleManager.BattleEnded += OnBattleEnded;
        }

        // Connect button signals
        _firePortButton?.Connect("pressed", Callable.From(OnFirePortPressed));
        _fireStarboardButton?.Connect("pressed", Callable.From(OnFireStarboardPressed));
        _endTurnButton?.Connect("pressed", Callable.From(OnEndTurnPressed));

        // Initial update
        UpdateDisplay();
    }

    public override void _Process(double delta)
    {
        UpdateHealthBars();
    }

    private void CreateUI()
    {
        // Main container
        var mainContainer = new VBoxContainer();
        mainContainer.AnchorRight = 1;
        mainContainer.AnchorBottom = 1;
        mainContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(mainContainer);

        // Top bar (turn/phase info)
        var topBar = new HBoxContainer();
        topBar.AddThemeConstantOverride("separation", 20);
        mainContainer.AddChild(topBar);

        _turnLabel = new Label();
        _turnLabel.Text = "Turn 1";
        _turnLabel.AddThemeFontSizeOverride("font_size", 24);
        topBar.AddChild(_turnLabel);

        _phaseLabel = new Label();
        _phaseLabel.Text = "Command Phase";
        _phaseLabel.AddThemeFontSizeOverride("font_size", 20);
        topBar.AddChild(_phaseLabel);

        // Spacer
        var spacer = new Control();
        spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
        mainContainer.AddChild(spacer);

        // Bottom panel
        var bottomPanel = new HBoxContainer();
        bottomPanel.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        bottomPanel.AddThemeConstantOverride("separation", 50);
        mainContainer.AddChild(bottomPanel);

        // Player ship status
        var playerPanel = CreateShipStatusPanel("Your Ship", true);
        bottomPanel.AddChild(playerPanel);

        // Center spacer
        var centerSpacer = new Control();
        centerSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bottomPanel.AddChild(centerSpacer);

        // Action buttons
        var buttonPanel = new VBoxContainer();
        buttonPanel.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        bottomPanel.AddChild(buttonPanel);

        _firePortButton = new Button();
        _firePortButton.Text = "Fire Port (P)";
        _firePortButton.CustomMinimumSize = new Vector2(150, 40);
        buttonPanel.AddChild(_firePortButton);

        _fireStarboardButton = new Button();
        _fireStarboardButton.Text = "Fire Starboard (S)";
        _fireStarboardButton.CustomMinimumSize = new Vector2(150, 40);
        buttonPanel.AddChild(_fireStarboardButton);

        _endTurnButton = new Button();
        _endTurnButton.Text = "End Turn (E)";
        _endTurnButton.CustomMinimumSize = new Vector2(150, 40);
        buttonPanel.AddChild(_endTurnButton);

        // Enemy ship status
        var enemyPanel = CreateShipStatusPanel("Enemy Ship", false);
        bottomPanel.AddChild(enemyPanel);

        // Interaction prompt (center screen, hidden by default)
        _interactionPrompt = new Panel();
        _interactionPrompt.SetAnchorsPreset(LayoutPreset.Center);
        _interactionPrompt.CustomMinimumSize = new Vector2(200, 50);
        _interactionPrompt.Position = new Vector2(-100, -200);
        _interactionPrompt.Visible = false;
        AddChild(_interactionPrompt);

        _interactionText = new Label();
        _interactionText.Text = "Press E to interact";
        _interactionText.HorizontalAlignment = HorizontalAlignment.Center;
        _interactionText.VerticalAlignment = VerticalAlignment.Center;
        _interactionText.SetAnchorsPreset(LayoutPreset.FullRect);
        _interactionPrompt.AddChild(_interactionText);
    }

    private VBoxContainer CreateShipStatusPanel(string title, bool isPlayer)
    {
        var panel = new VBoxContainer();
        panel.CustomMinimumSize = new Vector2(200, 100);

        var nameLabel = new Label();
        nameLabel.Text = title;
        nameLabel.AddThemeFontSizeOverride("font_size", 18);
        panel.AddChild(nameLabel);

        if (isPlayer)
        {
            _playerShipName = nameLabel;
        }
        else
        {
            _enemyShipName = nameLabel;
        }

        var healthLabel = new Label();
        healthLabel.Text = "Hull Integrity";
        panel.AddChild(healthLabel);

        var healthBar = new ProgressBar();
        healthBar.CustomMinimumSize = new Vector2(180, 25);
        healthBar.Value = 100;
        healthBar.ShowPercentage = true;
        panel.AddChild(healthBar);

        if (isPlayer)
        {
            _playerHealthBar = healthBar;
        }
        else
        {
            _enemyHealthBar = healthBar;
        }

        return panel;
    }

    private void UpdateDisplay()
    {
        if (_battleManager == null) return;

        _turnLabel.Text = $"Turn {_battleManager.TurnNumber}";
        _phaseLabel.Text = $"{_battleManager.CurrentPhase}";

        // Update ship names
        if (_battleManager.PlayerShip != null)
        {
            _playerShipName.Text = _battleManager.PlayerShip.ShipName;
        }
        if (_battleManager.EnemyShip != null)
        {
            _enemyShipName.Text = _battleManager.EnemyShip.ShipName;
        }

        // Update button states
        bool canCommand = _battleManager.CurrentPhase == BattleManager.BattlePhase.CommandPhase &&
                         _battleManager.CurrentTurn == BattleManager.BattleSide.Player;

        // Only enable the side that faces the enemy
        var sideFacingEnemy = GetSideFacingEnemy();
        _firePortButton.Disabled = !canCommand || sideFacingEnemy != ShipCannon.CannonSide.Port;
        _fireStarboardButton.Disabled = !canCommand || sideFacingEnemy != ShipCannon.CannonSide.Starboard;
        _endTurnButton.Disabled = !canCommand;

        // Update button text to indicate which side is active
        _firePortButton.Text = sideFacingEnemy == ShipCannon.CannonSide.Port ? "Fire Port (P) >" : "Fire Port (P)";
        _fireStarboardButton.Text = sideFacingEnemy == ShipCannon.CannonSide.Starboard ? "Fire Starboard (S) >" : "Fire Starboard (S)";
    }

    private void UpdateHealthBars()
    {
        if (_battleManager == null) return;

        if (_battleManager.PlayerShip != null)
        {
            _playerHealthBar.Value = _battleManager.PlayerShip.TotalHullIntegrity * 100f;
        }

        if (_battleManager.EnemyShip != null)
        {
            _enemyHealthBar.Value = _battleManager.EnemyShip.TotalHullIntegrity * 100f;
        }
    }

    private void OnPhaseChanged(int phase)
    {
        UpdateDisplay();
    }

    private void OnTurnChanged(int side, int turnNumber)
    {
        UpdateDisplay();
    }

    private void OnBattleEnded(int winner)
    {
        var winnerSide = (BattleManager.BattleSide)winner;
        _phaseLabel.Text = winnerSide == BattleManager.BattleSide.Player ? "VICTORY!" : "DEFEAT!";
        _phaseLabel.AddThemeFontSizeOverride("font_size", 32);

        _firePortButton.Disabled = true;
        _fireStarboardButton.Disabled = true;
        _endTurnButton.Disabled = true;
    }

    private void OnFirePortPressed()
    {
        if (_battleManager?.PlayerShip == null || _battleManager?.EnemyShip == null) return;

        // Determine which side faces the enemy
        var sideToFire = GetSideFacingEnemy();
        if (sideToFire != ShipCannon.CannonSide.Port)
        {
            GD.Print("Port cannons don't face the enemy!");
            return;
        }

        var target = _battleManager.EnemyShip.GetTargetPoint("center");
        _battleManager.QueueFireCommand(_battleManager.PlayerShip, ShipCannon.CannonSide.Port, target);
    }

    private void OnFireStarboardPressed()
    {
        if (_battleManager?.PlayerShip == null || _battleManager?.EnemyShip == null) return;

        // Determine which side faces the enemy
        var sideToFire = GetSideFacingEnemy();
        if (sideToFire != ShipCannon.CannonSide.Starboard)
        {
            GD.Print("Starboard cannons don't face the enemy!");
            return;
        }

        var target = _battleManager.EnemyShip.GetTargetPoint("center");
        _battleManager.QueueFireCommand(_battleManager.PlayerShip, ShipCannon.CannonSide.Starboard, target);
    }

    /// <summary>
    /// Determine which cannon side faces the enemy ship.
    /// </summary>
    private ShipCannon.CannonSide GetSideFacingEnemy()
    {
        if (_battleManager?.PlayerShip == null || _battleManager?.EnemyShip == null)
            return ShipCannon.CannonSide.Starboard;

        var playerShip = _battleManager.PlayerShip;
        var enemyShip = _battleManager.EnemyShip;

        // Get direction to enemy in world space
        var toEnemy = enemyShip.GlobalPosition - playerShip.GlobalPosition;

        // Get player ship's right vector (starboard direction)
        var starboardDir = playerShip.GlobalTransform.Basis.X;

        // Dot product: positive = enemy is to starboard, negative = enemy is to port
        float dot = toEnemy.Normalized().Dot(starboardDir);

        return dot > 0 ? ShipCannon.CannonSide.Starboard : ShipCannon.CannonSide.Port;
    }

    private void OnEndTurnPressed()
    {
        _battleManager?.EndCommandPhase();
    }

    /// <summary>
    /// Show an interaction prompt.
    /// </summary>
    public void ShowInteractionPrompt(string text)
    {
        _interactionText.Text = text;
        _interactionPrompt.Visible = true;
    }

    /// <summary>
    /// Hide the interaction prompt.
    /// </summary>
    public void HideInteractionPrompt()
    {
        _interactionPrompt.Visible = false;
    }
}
