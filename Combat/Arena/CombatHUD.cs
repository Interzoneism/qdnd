using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Entities;
using QDND.Combat.UI;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Combat HUD controller. Manages turn tracker, action bar, and unit info displays.
    /// </summary>
    public partial class CombatHUD : Control
    {
        [Export] public CombatArena Arena;
        [Export] public bool DebugUI = true;

        // UI Elements
        private HBoxContainer _turnTracker;
        private HBoxContainer _actionBar;
        private VBoxContainer _unitInfoPanel;
        private PanelContainer _bottomBar;
        private Button _endTurnButton;
        private Label _combatStateLabel;
        private Label _turnInfoLabel;

        // State
        private Dictionary<string, Control> _turnPortraits = new();
        private List<Button> _abilityButtons = new();

        // Combat Log
        private PanelContainer _logPanel;
        private ScrollContainer _logScroll;
        private VBoxContainer _logContainer;
        private const int MaxLogEntries = 30;

        // Resource display
        private HBoxContainer _resourceBar;
        private Dictionary<string, ProgressBar> _resourceBars = new();
        private Dictionary<string, Label> _resourceLabels = new();

        // Inspect panel (left side - hidden by default)
        private PanelContainer _inspectPanel;
        private VBoxContainer _inspectContent;
        private Label _inspectName;
        private Label _inspectFaction;
        private ProgressBar _inspectHpBar;
        private Label _inspectHpText;
        private VBoxContainer _inspectStatusList;
        private Label _inspectInitiative;

        // Character Portrait (bottom-right, per layout spec)
        private PanelContainer _portraitPanel;
        private Label _portraitName;
        private Label _portraitAC;
        private ProgressBar _portraitHpBar;
        private Label _portraitHpText;

        // Cleanup flag to prevent zombie callbacks
        private bool _disposed = false;

        // Store service references for cleanup
        private CombatStateMachine _stateMachine;
        private TurnQueueService _turnQueue;
        private CombatLog _combatLog;

        public override void _Ready()
        {
            CallDeferred(nameof(DeferredInit));
        }

        public override void _ExitTree()
        {
            _disposed = true;
            UnsubscribeFromEvents();
            base._ExitTree();
        }

        /// <summary>
        /// Public cleanup method for graceful shutdown. Call before freeing.
        /// </summary>
        public void Cleanup()
        {
            GD.Print($"[CombatHUD] Cleanup called - already disposed={_disposed}");
            _disposed = true;
            UnsubscribeFromEvents();
            GD.Print("[CombatHUD] Cleanup complete");
        }

        private void UnsubscribeFromEvents()
        {
            // Unsubscribe from combat service events
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= OnStateChanged;
            
            if (_turnQueue != null)
                _turnQueue.OnTurnChanged -= OnTurnChanged;
            
            if (_combatLog != null)
                _combatLog.OnEntryAdded -= OnLogEntryAdded;

            // Unsubscribe from UI model events
            if (Arena != null)
            {
                if (Arena.TurnTrackerModel != null)
                {
                    Arena.TurnTrackerModel.TurnOrderChanged -= OnTurnOrderChanged;
                    Arena.TurnTrackerModel.ActiveCombatantChanged -= OnActiveCombatantChanged;
                    Arena.TurnTrackerModel.EntryUpdated -= OnTurnEntryUpdated;
                }

                if (Arena.ResourceBarModel != null)
                {
                    Arena.ResourceBarModel.ResourceChanged -= OnResourceChanged;
                    Arena.ResourceBarModel.HealthChanged -= OnHealthChanged;
                }

                if (Arena.ActionBarModel != null)
                {
                    Arena.ActionBarModel.ActionsChanged -= OnActionsChanged;
                    Arena.ActionBarModel.ActionUpdated -= OnActionUpdated;
                }
            }

            // Unsubscribe from button events
            if (_endTurnButton != null)
                _endTurnButton.Pressed -= OnEndTurnPressed;
        }

        private void DeferredInit()
        {
            GD.Print($"[CombatHUD] DeferredInit called - _disposed={_disposed}, IsValid={IsInstanceValid(this)}, InTree={IsInsideTree()}");
            
            // Guard against running after cleanup/disposal (can happen due to deferred call timing)
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
            {
                GD.Print("[CombatHUD] DeferredInit skipped - already disposed or removed from tree");
                return;
            }

            if (Arena == null)
            {
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;
            }

            // Skip HUD setup in auto-battle mode UNLESS full-fidelity mode is enabled
            if (Arena != null && Arena.IsAutoBattleMode && !QDND.Tools.DebugFlags.IsFullFidelity)
            {
                GD.Print("[CombatHUD] Auto-battle mode detected (fast mode) - disabling HUD");
                _disposed = true;  // Mark as disposed so event handlers skip their work
                return;
            }

            GD.Print("[CombatHUD] DeferredInit proceeding with setup");

            SetupUI();

            if (Arena != null)
            {
                // Subscribe to combat events via the context
                var context = Arena.Context;
                if (context != null)
                {
                    _stateMachine = context.GetService<CombatStateMachine>();
                    _turnQueue = context.GetService<TurnQueueService>();

                    if (_stateMachine != null)
                        _stateMachine.OnStateChanged += OnStateChanged;
                    if (_turnQueue != null)
                        _turnQueue.OnTurnChanged += OnTurnChanged;

                    _combatLog = context.GetService<CombatLog>();
                    if (_combatLog != null)
                    {
                        _combatLog.OnEntryAdded += OnLogEntryAdded;

                        // Also show existing entries
                        foreach (var entry in _combatLog.GetRecentEntries(MaxLogEntries))
                        {
                            AddLogEntry(entry);
                        }
                    }
                }

                // Initial population of turn tracker
                InitializeTurnTracker();

                // Subscribe to UI models if available
                if (Arena.TurnTrackerModel != null)
                {
                    Arena.TurnTrackerModel.TurnOrderChanged += OnTurnOrderChanged;
                    Arena.TurnTrackerModel.ActiveCombatantChanged += OnActiveCombatantChanged;
                    Arena.TurnTrackerModel.EntryUpdated += OnTurnEntryUpdated;
                }

                if (Arena.ResourceBarModel != null)
                {
                    Arena.ResourceBarModel.ResourceChanged += OnResourceChanged;
                    Arena.ResourceBarModel.HealthChanged += OnHealthChanged;
                }

                if (Arena.ActionBarModel != null)
                {
                    Arena.ActionBarModel.ActionsChanged += OnActionsChanged;
                    Arena.ActionBarModel.ActionUpdated += OnActionUpdated;
                }
            }
        }

        private void InitializeTurnTracker()
        {
            if (Arena == null) return;

            var combatants = Arena.GetCombatants();
            var currentId = Arena.Context?.GetService<TurnQueueService>()?.CurrentCombatant?.Id;
            RefreshTurnTracker(combatants, currentId);
        }

        private void SetupUI()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] === NEW BG3-STYLE HUD SETUP ===");

            // Main container
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore; // Allow clicks to pass through to 3D

            if (DebugUI)
                GD.Print($"[CombatHUD] MouseFilter set to: {MouseFilter}");

            SetupTurnTracker();

            // Hotbar Panel per layout spec
            // Position: X: 660px (centered), Y: 968px
            // Size: 608px wide × 56px tall
            _bottomBar = new PanelContainer();
            _bottomBar.AnchorLeft = 0.5f;  // Center-anchored
            _bottomBar.AnchorRight = 0.5f;
            _bottomBar.AnchorTop = 1.0f;
            _bottomBar.AnchorBottom = 1.0f;
            // Center the 608px wide panel at center of screen
            _bottomBar.OffsetLeft = -304;   // -608/2 = -304
            _bottomBar.OffsetRight = 304;   // +608/2 = 304
            _bottomBar.OffsetTop = -112;    // 1080 - 968 = 112 from bottom
            _bottomBar.OffsetBottom = -56;  // 112 - 56 = 56 from bottom
            _bottomBar.CustomMinimumSize = new Vector2(608, 56);
            _bottomBar.MouseFilter = MouseFilterEnum.Stop;
            
            var style = new StyleBoxFlat();
            // rgba(0, 0, 0, 0.7) per spec, 6px radius
            style.BgColor = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            style.SetCornerRadiusAll(6);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.15f);  // rgba(255,255,255,0.15)
            _bottomBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(_bottomBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Hotbar panel created");

            // Center: Action bar (ability slots) - 48x48 slots with 4px spacing, 12 slots
            _actionBar = new HBoxContainer();
            _actionBar.AddThemeConstantOverride("separation", 4);  // 4px spacing per spec
            _actionBar.Alignment = BoxContainer.AlignmentMode.Center;
            _bottomBar.AddChild(_actionBar);

            // Create 12 ability buttons (like BG3's hotbar)
            for (int i = 0; i < 12; i++)
            {
                var btn = CreateAbilityButton(i);
                _actionBar.AddChild(btn);
                _abilityButtons.Add(btn);
            }

            // End Turn Button - separate panel, absolute positioned
            // Position per spec: X: 1716px, Y: 956px, Size: 180x84
            SetupEndTurnButton();

            // Bottom bar starts visible during player turn
            _bottomBar.Visible = true;

            SetupCombatLog();
            SetupResourceBar();
            SetupInspectPanel();
            SetupCharacterPortrait();
        }

        private void SetupTurnTracker()
        {
            // Top bar - Turn Tracker (BG3-style)
            var topBar = new PanelContainer();
            // Use explicit anchors for reliable positioning
            topBar.AnchorLeft = 0.0f;
            topBar.AnchorRight = 1.0f;
            topBar.AnchorTop = 0.0f;
            topBar.AnchorBottom = 0.0f;
            topBar.OffsetTop = 0;
            topBar.OffsetBottom = 70;
            topBar.OffsetLeft = 0;
            topBar.OffsetRight = 0;
            topBar.CustomMinimumSize = new Vector2(0, 70);
            topBar.MouseFilter = MouseFilterEnum.Stop;
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            style.SetCornerRadiusAll(0);  // Sharp edges like BG3
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.25f, 0.22f, 0.18f);
            topBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(topBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Top bar: {topBar.Size} at {topBar.Position}");

            _turnTracker = new HBoxContainer();
            _turnTracker.Alignment = BoxContainer.AlignmentMode.Center;
            _turnTracker.AddThemeConstantOverride("separation", 10);
            topBar.AddChild(_turnTracker);
        }

        private void SetupResourceBar()
        {
            // Action Economy panel per layout spec
            // Position: Left side, not overlapping hotbar (hotbar starts at ~656px)
            // Size: 280px wide × 64px tall
            var resourcePanel = new PanelContainer();
            resourcePanel.AnchorLeft = 0.0f;
            resourcePanel.AnchorRight = 0.0f;
            resourcePanel.AnchorTop = 1.0f;
            resourcePanel.AnchorBottom = 1.0f;
            resourcePanel.OffsetLeft = 20;       // Move to far left to avoid hotbar overlap
            resourcePanel.OffsetRight = 300;     // 20 + 280 = 300
            resourcePanel.OffsetTop = -80;       // Raise slightly to avoid bottom edge
            resourcePanel.OffsetBottom = -16;    // 80 - 64 = 16 from bottom
            resourcePanel.CustomMinimumSize = new Vector2(280, 64);

            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);  // rgba(0,0,0,0.6) per spec
            panelStyle.SetCornerRadiusAll(8);
            resourcePanel.AddThemeStyleboxOverride("panel", panelStyle);
            AddChild(resourcePanel);

            _resourceBar = new HBoxContainer();
            _resourceBar.Alignment = BoxContainer.AlignmentMode.Center;
            _resourceBar.AddThemeConstantOverride("separation", 6);  // 6px between resource boxes per spec
            resourcePanel.AddChild(_resourceBar);

            // Initialize with default resource displays
            InitializeResourceDisplays();
        }

        private void InitializeResourceDisplays()
        {
            // Clear existing
            foreach (var child in _resourceBar.GetChildren())
                child.QueueFree();
            _resourceBars.Clear();
            _resourceLabels.Clear();

            // Resource colors per layout spec
            // ACT (Action): #51CF66 (green)
            // BNS (Bonus Action): #FFA94D (orange)
            // MOV (Movement): #FFD43B (yellow)
            // RXN (Reaction): #CC5DE8 (purple)
            CreateResourceDisplay("action", "ACT", new Color(0.318f, 0.812f, 0.4f), 1, 1);      // Green #51CF66
            CreateResourceDisplay("bonus", "BNS", new Color(1.0f, 0.663f, 0.302f), 1, 1);       // Orange #FFA94D
            CreateResourceDisplay("move", "MOV", new Color(1.0f, 0.831f, 0.231f), 30, 30);      // Yellow #FFD43B
            CreateResourceDisplay("reaction", "RXN", new Color(0.8f, 0.365f, 0.91f), 1, 1);     // Purple #CC5DE8
        }

        private void SetupInspectPanel()
        {
            // Inspect panel on left side (BG3-style unit info)
            _inspectPanel = new PanelContainer();
            // Use explicit anchors only
            _inspectPanel.AnchorLeft = 0.0f;
            _inspectPanel.AnchorRight = 0.0f;
            _inspectPanel.AnchorTop = 0.15f;
            _inspectPanel.AnchorBottom = 0.6f;
            _inspectPanel.OffsetLeft = 10;
            _inspectPanel.OffsetRight = 230;
            _inspectPanel.OffsetTop = 0;
            _inspectPanel.OffsetBottom = 0;
            _inspectPanel.CustomMinimumSize = new Vector2(210, 180);
            _inspectPanel.Visible = false; // Hidden by default

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.92f);
            style.SetCornerRadiusAll(0);
            style.SetBorderWidthAll(1);
            style.BorderColor = new Color(0.25f, 0.22f, 0.18f);
            _inspectPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(_inspectPanel);

            _inspectContent = new VBoxContainer();
            _inspectContent.AddThemeConstantOverride("separation", 8);
            _inspectPanel.AddChild(_inspectContent);

            // Name
            _inspectName = new Label();
            _inspectName.AddThemeFontSizeOverride("font_size", 18);
            _inspectName.HorizontalAlignment = HorizontalAlignment.Center;
            _inspectContent.AddChild(_inspectName);

            // Faction
            _inspectFaction = new Label();
            _inspectFaction.AddThemeFontSizeOverride("font_size", 12);
            _inspectFaction.HorizontalAlignment = HorizontalAlignment.Center;
            _inspectContent.AddChild(_inspectFaction);

            // Separator
            _inspectContent.AddChild(new HSeparator());

            // HP Bar
            var hpContainer = new VBoxContainer();
            _inspectContent.AddChild(hpContainer);

            var hpLabel = new Label();
            hpLabel.Text = "Health";
            hpLabel.AddThemeFontSizeOverride("font_size", 12);
            hpContainer.AddChild(hpLabel);

            _inspectHpBar = new ProgressBar();
            _inspectHpBar.CustomMinimumSize = new Vector2(200, 20);
            _inspectHpBar.ShowPercentage = false;

            var hpBgStyle = new StyleBoxFlat();
            hpBgStyle.BgColor = new Color(0.3f, 0.1f, 0.1f);
            _inspectHpBar.AddThemeStyleboxOverride("background", hpBgStyle);

            var hpFillStyle = new StyleBoxFlat();
            hpFillStyle.BgColor = new Color(0.2f, 0.8f, 0.2f);
            _inspectHpBar.AddThemeStyleboxOverride("fill", hpFillStyle);
            hpContainer.AddChild(_inspectHpBar);

            _inspectHpText = new Label();
            _inspectHpText.AddThemeFontSizeOverride("font_size", 12);
            _inspectHpText.HorizontalAlignment = HorizontalAlignment.Center;
            hpContainer.AddChild(_inspectHpText);

            // Initiative
            _inspectInitiative = new Label();
            _inspectInitiative.AddThemeFontSizeOverride("font_size", 12);
            _inspectContent.AddChild(_inspectInitiative);

            // Statuses section
            _inspectContent.AddChild(new HSeparator());

            var statusHeader = new Label();
            statusHeader.Text = "Active Effects";
            statusHeader.AddThemeFontSizeOverride("font_size", 14);
            _inspectContent.AddChild(statusHeader);

            _inspectStatusList = new VBoxContainer();
            _inspectStatusList.AddThemeConstantOverride("separation", 2);
            _inspectContent.AddChild(_inspectStatusList);
        }

        private void SetupCharacterPortrait()
        {
            // Character Portrait - bottom-right, above End Turn button to avoid overlap
            // Repositioned to not overlap combat log
            // Size: 120x120px (reduced from 144x144)
            _portraitPanel = new PanelContainer();
            _portraitPanel.AnchorLeft = 1.0f;
            _portraitPanel.AnchorRight = 1.0f;
            _portraitPanel.AnchorTop = 1.0f;
            _portraitPanel.AnchorBottom = 1.0f;
            // Position to bottom-right but above hotbar and combat log
            _portraitPanel.OffsetLeft = -230;  // Further left to avoid combat log
            _portraitPanel.OffsetRight = -110; // 230 - 120 = 110 from right edge
            _portraitPanel.OffsetTop = -220;   // Higher up to avoid overlap
            _portraitPanel.OffsetBottom = -100; // 220 - 120 = 100 from bottom
            _portraitPanel.CustomMinimumSize = new Vector2(120, 120);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.157f, 0.157f, 0.176f, 0.9f); // rgba(40, 40, 45, 0.9)
            style.SetCornerRadiusAll(8);
            style.SetBorderWidthAll(4);
            style.BorderColor = new Color(0.318f, 0.8f, 0.318f); // Green border by default (healthy)
            _portraitPanel.AddThemeStyleboxOverride("panel", style);
            AddChild(_portraitPanel);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            _portraitPanel.AddChild(vbox);

            // AC display at top
            _portraitAC = new Label();
            _portraitAC.Text = "AC: --";
            _portraitAC.AddThemeFontSizeOverride("font_size", 12);
            _portraitAC.HorizontalAlignment = HorizontalAlignment.Left;
            vbox.AddChild(_portraitAC);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer);

            // Character name (centered)
            _portraitName = new Label();
            _portraitName.Text = "SELECT";
            _portraitName.AddThemeFontSizeOverride("font_size", 14);
            _portraitName.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_portraitName);

            // Another spacer
            var spacer2 = new Control();
            spacer2.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(spacer2);

            // HP Bar
            _portraitHpBar = new ProgressBar();
            _portraitHpBar.CustomMinimumSize = new Vector2(100, 10);
            _portraitHpBar.ShowPercentage = false;
            _portraitHpBar.Value = 100;

            var hpBgStyle = new StyleBoxFlat();
            hpBgStyle.BgColor = new Color(0.3f, 0.1f, 0.1f);
            _portraitHpBar.AddThemeStyleboxOverride("background", hpBgStyle);

            var hpFillStyle = new StyleBoxFlat();
            hpFillStyle.BgColor = new Color(0.318f, 0.8f, 0.318f); // Green
            _portraitHpBar.AddThemeStyleboxOverride("fill", hpFillStyle);
            vbox.AddChild(_portraitHpBar);

            // HP text
            _portraitHpText = new Label();
            _portraitHpText.Text = "--/--";
            _portraitHpText.AddThemeFontSizeOverride("font_size", 11);
            _portraitHpText.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_portraitHpText);

            if (DebugUI)
                GD.Print("[CombatHUD] Character portrait panel created");
        }

        private void UpdateCharacterPortrait(Combatant combatant)
        {
            if (_portraitPanel == null || combatant == null) return;

            _portraitName.Text = combatant.Name.Length > 10 
                ? combatant.Name.Substring(0, 10) 
                : combatant.Name;

            // Update HP
            float hpPercent = combatant.Resources.MaxHP > 0
                ? (float)combatant.Resources.CurrentHP / combatant.Resources.MaxHP * 100
                : 0;
            _portraitHpBar.Value = hpPercent;
            _portraitHpText.Text = $"{combatant.Resources.CurrentHP}/{combatant.Resources.MaxHP}";

            // Update HP bar and border color based on health
            var hpFillStyle = new StyleBoxFlat();
            var borderColor = GetHealthColor(hpPercent);
            hpFillStyle.BgColor = borderColor;
            _portraitHpBar.AddThemeStyleboxOverride("fill", hpFillStyle);

            // Update panel border color
            var panelStyle = new StyleBoxFlat();
            panelStyle.BgColor = new Color(0.157f, 0.157f, 0.176f, 0.9f);
            panelStyle.SetCornerRadiusAll(8);
            panelStyle.SetBorderWidthAll(4);
            panelStyle.BorderColor = borderColor;
            _portraitPanel.AddThemeStyleboxOverride("panel", panelStyle);

            // AC (if we have stats - for now show initiative)
            _portraitAC.Text = $"AC: {10 + combatant.Initiative / 2}"; // Placeholder formula
        }

        private void SetupEndTurnButton()
        {
            // End Turn Button - bottom-right, non-overlapping with portrait
            // Positioned to right of character portrait
            // Size: 120x60 (reduced to fit better)
            _endTurnButton = new Button();
            _endTurnButton.AnchorLeft = 1.0f;
            _endTurnButton.AnchorRight = 1.0f;
            _endTurnButton.AnchorTop = 1.0f;
            _endTurnButton.AnchorBottom = 1.0f;
            // Position to far right, below combat log
            _endTurnButton.OffsetLeft = -132;    // 12px margin from right
            _endTurnButton.OffsetRight = -12;    // 132 - 120 = 12 from right edge
            _endTurnButton.OffsetTop = -76;      // Above resource bar
            _endTurnButton.OffsetBottom = -16;   // 76 - 60 = 16 from bottom
            _endTurnButton.CustomMinimumSize = new Vector2(120, 60);
            _endTurnButton.Text = "END TURN";
            _endTurnButton.MouseFilter = MouseFilterEnum.Stop;
            
            // CYAN per layout spec #00CED1
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.0f, 0.808f, 0.82f);
            normalStyle.SetCornerRadiusAll(6);
            normalStyle.SetBorderWidthAll(0);
            _endTurnButton.AddThemeStyleboxOverride("normal", normalStyle);
            _endTurnButton.AddThemeColorOverride("font_color", Colors.White);
            _endTurnButton.AddThemeFontSizeOverride("font_size", 16);  // Reduced for smaller button

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.0f, 0.93f, 0.94f);  // Brighter cyan
            hoverStyle.SetCornerRadiusAll(6);
            _endTurnButton.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = new StyleBoxFlat();
            pressedStyle.BgColor = new Color(0.0f, 0.65f, 0.66f);  // Darker cyan
            pressedStyle.SetCornerRadiusAll(6);
            _endTurnButton.AddThemeStyleboxOverride("pressed", pressedStyle);

            var disabledStyle = new StyleBoxFlat();
            disabledStyle.BgColor = new Color(0.4f, 0.4f, 0.4f);  // #666666 per spec
            disabledStyle.SetCornerRadiusAll(6);
            _endTurnButton.AddThemeStyleboxOverride("disabled", disabledStyle);
            _endTurnButton.AddThemeColorOverride("font_disabled_color", new Color(0.7f, 0.7f, 0.7f));

            _endTurnButton.Pressed += OnEndTurnPressed;
            AddChild(_endTurnButton);

            if (DebugUI)
            {
                GD.Print("[CombatHUD] End Turn button created");
                CallDeferred(nameof(DebugEndTurnButton));
            }
        }

        private void DebugEndTurnButton()
        {
            // Guard against running after cleanup/disposal
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (_endTurnButton != null && DebugUI)
            {
                GD.Print($"[CombatHUD] END TURN Debug:");
                GD.Print($"  Position: {_endTurnButton.Position}");
                GD.Print($"  Size: {_endTurnButton.Size}");  
                GD.Print($"  GlobalPosition: {_endTurnButton.GlobalPosition}");
                GD.Print($"  Visible: {_endTurnButton.Visible}");
                GD.Print($"  IsInsideTree: {_endTurnButton.IsInsideTree()}");
                GD.Print($"  Anchors: L={_endTurnButton.AnchorLeft}, R={_endTurnButton.AnchorRight}, T={_endTurnButton.AnchorTop}, B={_endTurnButton.AnchorBottom}");
                GD.Print($"  Offsets: L={_endTurnButton.OffsetLeft}, R={_endTurnButton.OffsetRight}, T={_endTurnButton.OffsetTop}, B={_endTurnButton.OffsetBottom}");
            }
        }

        private Color GetHealthColor(float hpPercent)
        {
            if (hpPercent > 50)
                return new Color(0.318f, 0.8f, 0.318f);  // Green #51CF66
            else if (hpPercent > 25)
                return new Color(0.902f, 0.831f, 0.263f); // Yellow #FF943B/E6D443
            else
                return new Color(0.902f, 0.263f, 0.263f); // Red #FF6B6B
        }

        private void CreateResourceDisplay(string id, string label, Color color, int current, int max)
        {
            // Per layout spec: 62px wide × 56px tall per resource
            var container = new VBoxContainer();
            container.CustomMinimumSize = new Vector2(62, 56);
            container.AddThemeConstantOverride("separation", 2);
            _resourceBar.AddChild(container);

            // Label at top (12px per spec)
            var labelNode = new Label();
            labelNode.Text = label;
            labelNode.HorizontalAlignment = HorizontalAlignment.Center;
            labelNode.AddThemeFontSizeOverride("font_size", 12);
            labelNode.AddThemeColorOverride("font_color", color.Lightened(0.3f));
            container.AddChild(labelNode);

            // Fill bar (54px wide × 32px tall per spec)
            var bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(54, id == "move" ? 16 : 24);
            bar.Value = max > 0 ? (float)current / max * 100 : 0;
            bar.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(1.0f, 1.0f, 1.0f, 0.1f);  // 10% white per spec
            bgStyle.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("background", bgStyle);

            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = color;
            fillStyle.SetCornerRadiusAll(2);
            bar.AddThemeStyleboxOverride("fill", fillStyle);

            container.AddChild(bar);
            _resourceBars[id] = bar;

            // Value label (16px bold per spec)
            var valueLabel = new Label();
            valueLabel.Text = $"{current}/{max}";
            valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            valueLabel.AddThemeFontSizeOverride("font_size", 16);
            container.AddChild(valueLabel);
            _resourceLabels[id] = valueLabel;
        }

        private void SetupCombatLog()
        {
            // Combat log panel per layout spec
            // Position: X: 1588px, Y: 80px (from top)
            // Size: 312px wide × 840px tall
            _logPanel = new PanelContainer();
            _logPanel.AnchorLeft = 1.0f;
            _logPanel.AnchorRight = 1.0f;
            _logPanel.AnchorTop = 0.0f;
            _logPanel.AnchorBottom = 0.0f;
            // In 1920 width: 1920 - 1588 = 332 from right edge
            _logPanel.OffsetLeft = -332;
            _logPanel.OffsetRight = -20;   // ~20px margin from right edge
            _logPanel.OffsetTop = 80;      // Y: 80px from top per spec
            _logPanel.OffsetBottom = 920;  // 80 + 840 = 920
            _logPanel.CustomMinimumSize = new Vector2(312, 840);

            var logStyle = new StyleBoxFlat();
            // rgba(20, 20, 25, 0.9) per spec
            logStyle.BgColor = new Color(0.078f, 0.078f, 0.098f, 0.9f);
            logStyle.SetCornerRadiusAll(8);    // 8px radius per spec
            logStyle.SetBorderWidthAll(2);     // 2px solid border per spec
            logStyle.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.15f);  // rgba(255,255,255,0.15)
            _logPanel.AddThemeStyleboxOverride("panel", logStyle);
            AddChild(_logPanel);

            var logVBox = new VBoxContainer();
            logVBox.AddThemeConstantOverride("separation", 0);
            _logPanel.AddChild(logVBox);

            // Header per spec: 40px height, slightly darker bg
            var headerPanel = new PanelContainer();
            headerPanel.CustomMinimumSize = new Vector2(0, 40);
            var headerStyle = new StyleBoxFlat();
            headerStyle.BgColor = new Color(0.118f, 0.118f, 0.137f, 0.95f); // rgba(30, 30, 35, 0.95)
            headerStyle.SetCornerRadiusAll(0);
            headerPanel.AddThemeStyleboxOverride("panel", headerStyle);
            logVBox.AddChild(headerPanel);

            var logHeader = new Label();
            logHeader.Text = "Combat Log";
            logHeader.HorizontalAlignment = HorizontalAlignment.Left;
            logHeader.VerticalAlignment = VerticalAlignment.Center;
            logHeader.AddThemeFontSizeOverride("font_size", 16);  // 16px bold per spec
            logHeader.CustomMinimumSize = new Vector2(0, 40);
            headerPanel.AddChild(logHeader);

            // Scrollable log entries
            _logScroll = new ScrollContainer();
            _logScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _logScroll.Set("horizontal_scroll_mode", 0); // Disabled
            logVBox.AddChild(_logScroll);

            _logContainer = new VBoxContainer();
            _logContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _logContainer.AddThemeConstantOverride("separation", 6);  // 6px padding per spec
            _logScroll.AddChild(_logContainer);

            if (DebugUI)
                GD.Print("[CombatHUD] Combat log panel created");
        }

        private Button CreateAbilityButton(int index)
        {
            var btn = new Button();
            // Per layout spec: 48x48 per slot
            btn.CustomMinimumSize = new Vector2(48, 48);
            // Slot labels: 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, -, =
            string[] slotLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
            btn.Text = index < slotLabels.Length ? slotLabels[index] : "";
            btn.TooltipText = "Empty slot";
            btn.Disabled = true;
            btn.MouseFilter = MouseFilterEnum.Stop;

            // Per layout spec: 2px solid border, 4px radius, dark background
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.235f, 0.235f, 0.235f, 0.8f);  // rgba(60, 60, 60, 0.8)
            normalStyle.SetCornerRadiusAll(4);
            normalStyle.SetBorderWidthAll(2);
            normalStyle.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.3f);  // 30% white
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeColorOverride("font_color", Colors.White);
            btn.AddThemeFontSizeOverride("font_size", 11);  // 11px bold per spec

            // Disabled style (empty slots) - dashed border per spec
            var disabledStyle = new StyleBoxFlat();
            disabledStyle.BgColor = new Color(0.157f, 0.157f, 0.157f, 0.5f);  // rgba(40,40,40,0.5)
            disabledStyle.SetCornerRadiusAll(4);
            disabledStyle.SetBorderWidthAll(2);
            disabledStyle.BorderColor = new Color(1.0f, 1.0f, 1.0f, 0.3f);  // 30% opacity
            btn.AddThemeStyleboxOverride("disabled", disabledStyle);
            btn.AddThemeColorOverride("font_disabled_color", new Color(0.5f, 0.5f, 0.5f));

            // Hover: brighter border
            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.3f, 0.3f, 0.35f, 0.9f);
            hoverStyle.SetCornerRadiusAll(4);
            hoverStyle.SetBorderWidthAll(2);
            hoverStyle.BorderColor = new Color(1.0f, 0.843f, 0.0f);  // Gold #FFD700
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            int capturedIndex = index;
            btn.Pressed += () => OnAbilityPressed(capturedIndex);

            if (DebugUI)
                GD.Print($"[CombatHUD] Created ability button {index} with MouseFilter: {btn.MouseFilter}");

            return btn;
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            // Show/hide action bar based on state
            if (_bottomBar != null && IsInstanceValid(_bottomBar))
            {
                _bottomBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }

            if (_endTurnButton != null && IsInstanceValid(_endTurnButton))
            {
                _endTurnButton.Disabled = evt.ToState != CombatState.PlayerDecision;
            }

            // Show/hide resource bar based on state
            if (_resourceBar != null && IsInstanceValid(_resourceBar))
            {
                _resourceBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            // Refresh the turn tracker to update active highlighting
            if (Arena != null)
            {
                var combatants = Arena.GetCombatants();
                RefreshTurnTracker(combatants, evt.CurrentCombatant?.Id);
            }

            // Update abilities for current combatant
            if (evt.CurrentCombatant != null && evt.CurrentCombatant.IsPlayerControlled)
            {
                UpdateAbilityButtons(evt.CurrentCombatant.Id);

                // Default full resources at turn start
                UpdateResources(1, 1, 1, 1, 30, 30, 1, 1);
            }
        }

        public void UpdateResources(int action, int maxAction, int bonus, int maxBonus, int move, int maxMove, int reaction, int maxReaction)
        {
            UpdateResourceBar("action", action, maxAction);
            UpdateResourceBar("bonus", bonus, maxBonus);
            UpdateResourceBar("move", move, maxMove);
            UpdateResourceBar("reaction", reaction, maxReaction);
        }

        private void UpdateResourceBar(string id, int current, int max)
        {
            if (_disposed || !IsInstanceValid(this)) return;

            if (_resourceBars.TryGetValue(id, out var bar) && IsInstanceValid(bar))
            {
                bar.Value = max > 0 ? (float)current / max * 100 : 0;

                // Color based on depleted state
                var style = bar.GetThemeStylebox("fill") as StyleBoxFlat;
                if (style != null && current <= 0)
                {
                    // Create new style for depleted state
                    var depletedStyle = new StyleBoxFlat();
                    depletedStyle.BgColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);
                    bar.AddThemeStyleboxOverride("fill", depletedStyle);
                }
            }

            if (_resourceLabels.TryGetValue(id, out var label) && IsInstanceValid(label))
            {
                label.Text = $"{current}/{max}";
                label.Modulate = current <= 0 ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
            }
        }

        private void OnTurnOrderChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (Arena?.TurnTrackerModel == null) return;

            // Rebuild turn tracker from model
            var entries = Arena.TurnTrackerModel.Entries;
            RefreshTurnTrackerFromModel(entries, Arena.TurnTrackerModel.ActiveCombatantId);
        }

        private void OnActiveCombatantChanged(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            // Update highlighting in turn tracker
            foreach (var kvp in _turnPortraits)
            {
                bool isActive = kvp.Key == combatantId;
                var panel = kvp.Value as PanelContainer;
                if (panel != null && IsInstanceValid(panel))
                {
                    var styleBox = new StyleBoxFlat();
                    styleBox.BgColor = isActive ? new Color(0.2f, 0.8f, 0.3f, 0.8f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    styleBox.SetCornerRadiusAll(5);
                    panel.AddThemeStyleboxOverride("panel", styleBox);
                }
            }
        }

        private void OnTurnEntryUpdated(string combatantId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            var entry = Arena?.TurnTrackerModel?.GetEntry(combatantId);
            if (entry == null || !_turnPortraits.TryGetValue(combatantId, out var portrait)) return;

            if (!IsInstanceValid(portrait)) return;

            // Update HP display in portrait
            // Find HP label in portrait
            var vbox = portrait.GetChild(0) as VBoxContainer;
            if (vbox != null && IsInstanceValid(vbox) && vbox.GetChildCount() >= 3)
            {
                var hpLabel = vbox.GetChild(2) as Label;
                if (hpLabel != null && IsInstanceValid(hpLabel))
                {
                    int hp = (int)(entry.HpPercent * 100);
                    hpLabel.Text = $"{hp}%";
                    hpLabel.Modulate = entry.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
                }
            }
        }

        private void OnResourceChanged(string resourceId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            var resource = Arena?.ResourceBarModel?.GetResource(resourceId);
            if (resource == null) return;

            UpdateResourceBar(resourceId, resource.Current, resource.Maximum);
        }

        private void OnHealthChanged(int current, int max, int temp)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            UpdateResourceBar("health", current, max);
        }

        private void OnActionsChanged()
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (Arena?.ActionBarModel == null) return;

            // Rebuild ability buttons from model
            var actions = Arena.ActionBarModel.Actions.ToList();
            for (int i = 0; i < _abilityButtons.Count; i++)
            {
                var btn = _abilityButtons[i];
                if (btn != null && IsInstanceValid(btn))
                {
                    if (i < actions.Count)
                    {
                        var action = actions[i];
                        btn.Text = $"[{i + 1}]\n{action.DisplayName}";
                        btn.TooltipText = action.Description ?? action.DisplayName;
                        btn.Disabled = !action.IsAvailable;
                    }
                    else
                    {
                        btn.Text = $"[{i + 1}]";
                        btn.TooltipText = "No ability";
                        btn.Disabled = true;
                    }
                }
            }
        }

        private void OnActionUpdated(string actionId)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (Arena?.ActionBarModel == null) return;

            var actions = Arena.ActionBarModel.Actions.ToList();
            for (int i = 0; i < actions.Count && i < _abilityButtons.Count; i++)
            {
                if (actions[i].ActionId == actionId)
                {
                    var action = actions[i];
                    var btn = _abilityButtons[i];
                    if (btn != null && IsInstanceValid(btn))
                    {
                        btn.Disabled = !action.IsAvailable;

                        // Show cooldown/charge state visually
                        if (action.HasCooldown)
                        {
                            btn.Text = $"[{i + 1}]\n{action.DisplayName}\n(CD:{action.CooldownRemaining})";
                        }
                    }
                    break;
                }
            }
        }

        private void RefreshTurnTrackerFromModel(IReadOnlyList<TurnTrackerEntry> entries, string activeId)
        {
            // Clear existing
            foreach (var child in _turnTracker.GetChildren())
            {
                child.QueueFree();
            }
            _turnPortraits.Clear();

            // Create portrait for each entry
            foreach (var entry in entries.OrderByDescending(e => e.Initiative))
            {
                var portrait = CreateTurnPortraitFromEntry(entry, entry.CombatantId == activeId);
                _turnTracker.AddChild(portrait);
                _turnPortraits[entry.CombatantId] = portrait;
            }
        }

        private Control CreateTurnPortraitFromEntry(TurnTrackerEntry entry, bool isActive)
        {
            var panel = new PanelContainer();
            // Per layout spec: 120px wide × 72px tall (reduced for better fit)
            panel.CustomMinimumSize = isActive ? new Vector2(100, 68) : new Vector2(90, 62);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.0f, 0.0f, 0.0f, 0.7f);
            styleBox.SetCornerRadiusAll(8);
            
            // Border: Player=Blue #4DABF7, Enemy=Red #FF6B6B, Active=Gold #FFD700
            var factionColor = entry.IsPlayer 
                ? new Color(0.302f, 0.671f, 0.969f)
                : new Color(1.0f, 0.42f, 0.42f);
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 2);
            styleBox.BorderColor = isActive 
                ? new Color(1.0f, 0.843f, 0.0f)
                : factionColor;

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Top row: Initiative and Name
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(topRow);

            var initLabel = new Label();
            initLabel.Text = entry.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Left;
            initLabel.AddThemeFontSizeOverride("font_size", isActive ? 16 : 14);
            initLabel.AddThemeColorOverride("font_color", Colors.White);
            topRow.AddChild(initLabel);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            topRow.AddChild(spacer);

            var nameLabel = new Label();
            var displayName = entry.DisplayName.Length > 8 ? entry.DisplayName.Substring(0, 8) : entry.DisplayName;
            nameLabel.Text = displayName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Right;
            nameLabel.AddThemeFontSizeOverride("font_size", isActive ? 14 : 12);
            nameLabel.AddThemeColorOverride("font_color", entry.IsDead ? new Color(0.4f, 0.4f, 0.4f) : Colors.White);
            topRow.AddChild(nameLabel);

            // Middle spacer
            var midSpacer = new Control();
            midSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(midSpacer);

            // HP Bar
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 10);
            hpBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpBar.ShowPercentage = false;
            hpBar.Value = entry.HpPercent * 100;
            
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.2f, 0.1f, 0.1f);
            bgStyle.SetCornerRadiusAll(2);
            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = entry.HpPercent > 0.5f 
                ? new Color(0.318f, 0.812f, 0.4f)
                : entry.HpPercent > 0.25f 
                    ? new Color(1.0f, 0.831f, 0.231f)
                    : new Color(1.0f, 0.42f, 0.42f);
            fillStyle.SetCornerRadiusAll(2);
            hpBar.AddThemeStyleboxOverride("background", bgStyle);
            hpBar.AddThemeStyleboxOverride("fill", fillStyle);
            vbox.AddChild(hpBar);

            // HP Numbers
            int currentHp = (int)(entry.HpPercent * 100);  // Approximate since we only have percent
            var hpLabel = new Label();
            hpLabel.Text = $"{currentHp}%";
            hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hpLabel.AddThemeFontSizeOverride("font_size", isActive ? 14 : 11);
            hpLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(hpLabel);

            return panel;
        }

        public void RefreshTurnTracker(IEnumerable<Combatant> combatants, string activeId)
        {
            // Clear existing
            foreach (var child in _turnTracker.GetChildren())
            {
                child.QueueFree();
            }
            _turnPortraits.Clear();

            // Create portrait for each combatant
            foreach (var c in combatants.OrderByDescending(c => c.Initiative))
            {
                var portrait = CreateTurnPortrait(c, c.Id == activeId);
                _turnTracker.AddChild(portrait);
                _turnPortraits[c.Id] = portrait;
            }
        }

        private Control CreateTurnPortrait(Combatant c, bool isActive)
        {
            var panel = new PanelContainer();
            // Per layout spec: 120px wide × 72px tall (reduced from spec for better fit)
            panel.CustomMinimumSize = isActive ? new Vector2(100, 68) : new Vector2(90, 62);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.0f, 0.0f, 0.0f, 0.7f); // rgba(0, 0, 0, 0.7) per spec
            styleBox.SetCornerRadiusAll(8);  // 8px radius per spec
            
            // Border: Player=Blue #4DABF7, Enemy=Red #FF6B6B, Active=Gold #FFD700
            var factionColor = c.Faction == Faction.Player 
                ? new Color(0.302f, 0.671f, 0.969f)  // Blue #4DABF7
                : new Color(1.0f, 0.42f, 0.42f);     // Red #FF6B6B
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 2);
            styleBox.BorderColor = isActive 
                ? new Color(1.0f, 0.843f, 0.0f)  // Gold #FFD700
                : factionColor;

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Top row: Initiative (left) and Name (right)
            var topRow = new HBoxContainer();
            topRow.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(topRow);

            // Initiative number - top-left, 18px bold per spec
            var initLabel = new Label();
            initLabel.Text = c.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Left;
            initLabel.AddThemeFontSizeOverride("font_size", isActive ? 16 : 14);
            initLabel.AddThemeColorOverride("font_color", Colors.White);
            topRow.AddChild(initLabel);

            // Spacer
            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            topRow.AddChild(spacer);

            // Name - top-right, truncate if > 8 chars per spec
            var nameLabel = new Label();
            var displayName = c.Name.Length > 8 ? c.Name.Substring(0, 8) : c.Name;
            nameLabel.Text = displayName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Right;
            nameLabel.AddThemeFontSizeOverride("font_size", isActive ? 14 : 12);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            topRow.AddChild(nameLabel);

            // Middle spacer
            var midSpacer = new Control();
            midSpacer.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(midSpacer);

            // HP Bar - full width, 10px tall per spec
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 10);
            hpBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hpBar.ShowPercentage = false;
            var hpPercent = c.Resources.MaxHP > 0 
                ? (float)c.Resources.CurrentHP / c.Resources.MaxHP 
                : 0;
            hpBar.Value = hpPercent * 100;
            
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.2f, 0.1f, 0.1f);
            bgStyle.SetCornerRadiusAll(2);
            var fillStyle = new StyleBoxFlat();
            // HP colors per spec: Green >50%, Yellow 25-50%, Red <25%
            fillStyle.BgColor = hpPercent > 0.5f 
                ? new Color(0.318f, 0.812f, 0.4f)   // Green #51CF66
                : hpPercent > 0.25f 
                    ? new Color(1.0f, 0.831f, 0.231f)  // Yellow #FFD43B
                    : new Color(1.0f, 0.42f, 0.42f);   // Red #FF6B6B
            fillStyle.SetCornerRadiusAll(2);
            hpBar.AddThemeStyleboxOverride("background", bgStyle);
            hpBar.AddThemeStyleboxOverride("fill", fillStyle);
            vbox.AddChild(hpBar);

            // HP Numbers - centered, 14px bold per spec
            var hpLabel = new Label();
            hpLabel.Text = $"{c.Resources.CurrentHP}/{c.Resources.MaxHP}";
            hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hpLabel.AddThemeFontSizeOverride("font_size", isActive ? 14 : 11);
            hpLabel.AddThemeColorOverride("font_color", Colors.White);
            vbox.AddChild(hpLabel);

            return panel;
        }

        private void UpdateAbilityButtons(string combatantId)
        {
            var abilities = Arena?.GetAbilitiesForCombatant(combatantId) ?? new List<QDND.Combat.Abilities.AbilityDefinition>();

            for (int i = 0; i < _abilityButtons.Count; i++)
            {
                var btn = _abilityButtons[i];

                if (i < abilities.Count)
                {
                    var ability = abilities[i];
                    btn.Text = $"[{i + 1}]\n{ability.Name}";
                    btn.TooltipText = $"{ability.Name}\n{ability.Description}";
                    btn.Disabled = false;
                }
                else
                {
                    btn.Text = $"[{i + 1}]";
                    btn.TooltipText = "No ability";
                    btn.Disabled = true;
                }
            }
        }

        private void UpdateUnitInfo(Combatant c)
        {
            // Unit info is now shown in the inspect panel
            // This method is kept for compatibility but does nothing
        }

        private void OnAbilityPressed(int index)
        {
            if (DebugUI)
                GD.Print($"[CombatHUD] OnAbilityPressed({index}) - Arena: {Arena != null}, IsPlayerTurn: {Arena?.IsPlayerTurn}");

            if (Arena == null || !Arena.IsPlayerTurn) return;

            var abilities = Arena.GetAbilitiesForCombatant(Arena.SelectedCombatantId);
            if (DebugUI)
                GD.Print($"[CombatHUD] Abilities count: {abilities?.Count ?? 0}");

            if (index >= 0 && index < abilities.Count)
            {
                if (DebugUI)
                    GD.Print($"[CombatHUD] Selecting ability: {abilities[index].Id}");

                Arena.SelectAbility(abilities[index].Id);

                // Highlight the selected button
                for (int i = 0; i < _abilityButtons.Count; i++)
                {
                    var btn = _abilityButtons[i];
                    btn.Modulate = i == index ? new Color(1.2f, 1.2f, 0.5f) : Colors.White;
                }
            }
        }

        private void OnEndTurnPressed()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] OnEndTurnPressed");
            Arena?.EndCurrentTurn();
        }

        private void OnPassTurnPressed()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] OnPassTurnPressed");
            // Pass turn is essentially the same as ending turn without taking an action
            Arena?.EndCurrentTurn();
        }

        private void OnDefendPressed()
        {
            if (DebugUI)
                GD.Print("[CombatHUD] OnDefendPressed");
            // Placeholder: In a full implementation, this would apply a defensive stance status
            // For now, just end the turn
            GD.Print("[CombatHUD] Defend action (placeholder - just ends turn)");
            Arena?.EndCurrentTurn();
        }

        private void OnLogEntryAdded(CombatLogEntry entry)
        {
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            AddLogEntry(entry);

            // Auto-scroll to bottom
            CallDeferred(nameof(ScrollLogToBottom));
        }

        private void ScrollLogToBottom()
        {
            // Guard against running after cleanup/disposal
            if (_disposed || !IsInstanceValid(this) || !IsInsideTree())
                return;

            if (_logScroll != null && IsInstanceValid(_logScroll))
            {
                _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;
            }
        }

        private void AddLogEntry(CombatLogEntry entry)
        {
            if (_disposed || !IsInstanceValid(this)) return;
            if (_logContainer == null || !IsInstanceValid(_logContainer)) return;

            // Remove old entries if over limit
            while (_logContainer.GetChildCount() >= MaxLogEntries)
            {
                var oldest = _logContainer.GetChild(0);
                if (IsInstanceValid(oldest))
                    oldest.QueueFree();
                else
                    break; // Safety: avoid infinite loop
            }

            var label = new RichTextLabel();
            label.BbcodeEnabled = true;
            label.FitContent = true;
            label.ScrollActive = false;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.CustomMinimumSize = new Vector2(0, 20);
            label.AddThemeFontSizeOverride("normal_font_size", 12);  // Reduced from default
            label.AddThemeFontSizeOverride("bold_font_size", 13);

            // Format based on entry type
            string text = FormatLogEntry(entry);
            label.Text = text;

            _logContainer.AddChild(label);
        }

        private string FormatLogEntry(CombatLogEntry entry)
        {
            // Combat log colors per layout spec
            // System: #00CED1 (cyan)
            // Turn start: #FFD700 (gold)
            // Damage: #FF6B6B (red)
            // Healing: #51CF66 (green)
            // Default: #CCCCCC (light grey)
            string color = entry.Type switch
            {
                CombatLogEntryType.DamageDealt => "#FF6B6B",      // Red
                CombatLogEntryType.HealingDone => "#51CF66",      // Green
                CombatLogEntryType.StatusApplied => "#CC5DE8",    // Purple
                CombatLogEntryType.StatusRemoved => "#888888",    // Gray
                CombatLogEntryType.TurnStarted => "#FFD700",      // Gold
                CombatLogEntryType.CombatStarted => "#00CED1",    // Cyan
                CombatLogEntryType.CombatEnded => "#00CED1",      // Cyan
                _ => "#CCCCCC"                                    // Light grey
            };

            string message = entry.Format();
            if (string.IsNullOrEmpty(message))
            {
                message = entry.Message ?? entry.Type.ToString();
            }

            return $"[color={color}]{message}[/color]";
        }

        public override void _Process(double delta)
        {
            // Update portrait based on current selection (inspect panel kept hidden per spec)
            if (Arena != null && !string.IsNullOrEmpty(Arena.SelectedCombatantId))
            {
                var combatant = Arena.Context?.GetCombatant(Arena.SelectedCombatantId);
                if (combatant != null)
                {
                    // ShowInspect(combatant); // Disabled - inspect panel overlaps hotbar
                    UpdateCharacterPortrait(combatant);
                }
            }
            // Inspect panel stays hidden to prevent overlaps
        }

        public void ShowInspect(Combatant combatant)
        {
            if (combatant == null)
            {
                _inspectPanel.Visible = false;
                return;
            }

            _inspectPanel.Visible = true;

            // Name and faction
            _inspectName.Text = combatant.Name;
            _inspectFaction.Text = combatant.Faction.ToString();
            _inspectFaction.Modulate = combatant.Faction == Entities.Faction.Player
                ? new Color(0.3f, 0.6f, 1.0f)
                : new Color(1.0f, 0.4f, 0.4f);

            // HP
            float hpPercent = combatant.Resources.MaxHP > 0
                ? (float)combatant.Resources.CurrentHP / combatant.Resources.MaxHP * 100
                : 0;
            _inspectHpBar.Value = hpPercent;
            _inspectHpText.Text = $"{combatant.Resources.CurrentHP} / {combatant.Resources.MaxHP}";

            // Color HP bar based on health
            var fillStyle = new StyleBoxFlat();
            if (hpPercent < 25)
                fillStyle.BgColor = new Color(0.9f, 0.2f, 0.2f);
            else if (hpPercent < 50)
                fillStyle.BgColor = new Color(0.9f, 0.7f, 0.2f);
            else
                fillStyle.BgColor = new Color(0.2f, 0.8f, 0.2f);
            _inspectHpBar.AddThemeStyleboxOverride("fill", fillStyle);

            // Initiative
            _inspectInitiative.Text = $"Initiative: {combatant.Initiative}";

            // Clear and populate statuses
            foreach (var child in _inspectStatusList.GetChildren())
            {
                child.QueueFree();
            }

            // Get statuses from StatusManager
            if (Arena?.Context != null)
            {
                var statusManager = Arena.Context.GetService<QDND.Combat.Statuses.StatusManager>();
                if (statusManager != null)
                {
                    var statuses = statusManager.GetStatuses(combatant.Id);
                    if (statuses != null && statuses.Count > 0)
                    {
                        foreach (var status in statuses)
                        {
                            var statusLabel = new Label();
                            statusLabel.Text = $"• {status.Definition.Name} ({status.RemainingDuration} turns)";
                            statusLabel.AddThemeFontSizeOverride("font_size", 11);
                            statusLabel.Modulate = new Color(0.8f, 0.6f, 1.0f);
                            _inspectStatusList.AddChild(statusLabel);
                        }
                    }
                    else
                    {
                        var noStatus = new Label();
                        noStatus.Text = "(none)";
                        noStatus.AddThemeFontSizeOverride("font_size", 11);
                        noStatus.Modulate = new Color(0.5f, 0.5f, 0.5f);
                        _inspectStatusList.AddChild(noStatus);
                    }
                }
            }
        }

        public void HideInspect()
        {
            _inspectPanel.Visible = false;
        }
    }
}
