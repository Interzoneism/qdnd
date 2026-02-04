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

        // Inspect panel
        private PanelContainer _inspectPanel;
        private VBoxContainer _inspectContent;
        private Label _inspectName;
        private Label _inspectFaction;
        private ProgressBar _inspectHpBar;
        private Label _inspectHpText;
        private VBoxContainer _inspectStatusList;
        private Label _inspectInitiative;

        public override void _Ready()
        {
            CallDeferred(nameof(DeferredInit));
        }

        private void DeferredInit()
        {
            if (Arena == null)
            {
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;
            }

            SetupUI();

            if (Arena != null)
            {
                // Subscribe to combat events via the context
                var context = Arena.Context;
                if (context != null)
                {
                    var stateMachine = context.GetService<CombatStateMachine>();
                    var turnQueue = context.GetService<TurnQueueService>();

                    if (stateMachine != null)
                        stateMachine.OnStateChanged += OnStateChanged;
                    if (turnQueue != null)
                        turnQueue.OnTurnChanged += OnTurnChanged;

                    var combatLog = context.GetService<CombatLog>();
                    if (combatLog != null)
                    {
                        combatLog.OnEntryAdded += OnLogEntryAdded;

                        // Also show existing entries
                        foreach (var entry in combatLog.GetRecentEntries(MaxLogEntries))
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

            // Bottom bar - Action Bar (BG3-style dark panel)
            _bottomBar = new PanelContainer();
            // Use explicit anchors for reliable positioning
            _bottomBar.AnchorLeft = 0.0f;
            _bottomBar.AnchorRight = 1.0f;
            _bottomBar.AnchorTop = 1.0f;
            _bottomBar.AnchorBottom = 1.0f;
            _bottomBar.OffsetTop = -100;
            _bottomBar.OffsetBottom = 0;
            _bottomBar.OffsetLeft = 0;
            _bottomBar.OffsetRight = 0;
            _bottomBar.CustomMinimumSize = new Vector2(0, 100);
            _bottomBar.MouseFilter = MouseFilterEnum.Stop;
            
            var style = new StyleBoxFlat();
            // BG3-style: Dark semi-transparent panel with subtle border
            style.BgColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            style.SetCornerRadiusAll(0);  // Sharp edges like BG3
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.25f, 0.22f, 0.18f);  // Bronze/gold tint
            _bottomBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(_bottomBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Bottom bar: {_bottomBar.Size} at {_bottomBar.Position}");

            if (DebugUI)
                GD.Print($"[CombatHUD] Bottom bar created with MouseFilter: {_bottomBar.MouseFilter}");

            // Main layout: Left spacer | Center Action Bar | Right End Turn
            var bottomLayout = new HBoxContainer();
            bottomLayout.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            bottomLayout.SizeFlagsVertical = SizeFlags.ExpandFill;
            bottomLayout.AddThemeConstantOverride("separation", 0);
            _bottomBar.AddChild(bottomLayout);

            // Left spacer for resources
            var leftSpacer = new Control();
            leftSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftSpacer.CustomMinimumSize = new Vector2(150, 0);
            bottomLayout.AddChild(leftSpacer);

            // Center: Action bar (ability slots)
            _actionBar = new HBoxContainer();
            _actionBar.AddThemeConstantOverride("separation", 4);
            _actionBar.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            bottomLayout.AddChild(_actionBar);

            // Right spacer
            var rightSpacer = new Control();
            rightSpacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            rightSpacer.CustomMinimumSize = new Vector2(20, 0);
            bottomLayout.AddChild(rightSpacer);

            // End Turn Button (BG3-style golden)
            _endTurnButton = new Button();
            _endTurnButton.Text = "END TURN";
            _endTurnButton.CustomMinimumSize = new Vector2(120, 70);
            
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.15f, 0.12f, 0.08f);  // Dark bronze
            normalStyle.SetCornerRadiusAll(0);  // Sharp corners like BG3
            normalStyle.SetBorderWidthAll(3);
            normalStyle.BorderColor = new Color(0.7f, 0.55f, 0.25f);  // Gold border
            _endTurnButton.AddThemeStyleboxOverride("normal", normalStyle);
            _endTurnButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.8f, 0.5f));  // Gold text
            _endTurnButton.AddThemeFontSizeOverride("font_size", 14);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.25f, 0.2f, 0.1f);  // Lighter bronze on hover
            hoverStyle.SetCornerRadiusAll(0);
            hoverStyle.SetBorderWidthAll(3);
            hoverStyle.BorderColor = new Color(0.9f, 0.75f, 0.35f);  // Brighter gold
            _endTurnButton.AddThemeStyleboxOverride("hover", hoverStyle);

            var pressedStyle = new StyleBoxFlat();
            pressedStyle.BgColor = new Color(0.1f, 0.08f, 0.05f);
            pressedStyle.SetCornerRadiusAll(0);
            pressedStyle.SetBorderWidthAll(3);
            pressedStyle.BorderColor = new Color(0.5f, 0.4f, 0.2f);
            _endTurnButton.AddThemeStyleboxOverride("pressed", pressedStyle);

            _endTurnButton.Pressed += OnEndTurnPressed;
            _endTurnButton.MouseFilter = MouseFilterEnum.Stop;
            bottomLayout.AddChild(_endTurnButton);

            // Create 12 ability buttons (like BG3's hotbar)
            for (int i = 0; i < 12; i++)
            {
                var btn = CreateAbilityButton(i);
                _actionBar.AddChild(btn);
                _abilityButtons.Add(btn);
            }

            // Bottom bar starts visible during player turn
            _bottomBar.Visible = true;

            SetupCombatLog();
            SetupResourceBar();
            SetupInspectPanel();
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
            // BG3-style resource indicators: left side of bottom bar
            _resourceBar = new HBoxContainer();
            // Position in the left area above the action bar
            _resourceBar.AnchorLeft = 0.0f;
            _resourceBar.AnchorRight = 0.0f;
            _resourceBar.AnchorTop = 1.0f;
            _resourceBar.AnchorBottom = 1.0f;
            _resourceBar.OffsetLeft = 15;
            _resourceBar.OffsetRight = 200;
            _resourceBar.OffsetTop = -95;
            _resourceBar.OffsetBottom = -10;
            _resourceBar.AddThemeConstantOverride("separation", 8);
            AddChild(_resourceBar);

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

            // BG3-style action resources
            CreateResourceDisplay("action", "ACT", new Color(0.2f, 0.65f, 0.3f), 1, 1);      // Green circle
            CreateResourceDisplay("bonus", "BNS", new Color(0.85f, 0.55f, 0.15f), 1, 1);     // Orange triangle
            CreateResourceDisplay("move", "MOV", new Color(0.7f, 0.6f, 0.2f), 30, 30);       // Yellow bar
            CreateResourceDisplay("reaction", "RXN", new Color(0.6f, 0.3f, 0.7f), 1, 1);     // Purple
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

        private void CreateResourceDisplay(string id, string label, Color color, int current, int max)
        {
            // BG3-style compact resource display
            var container = new VBoxContainer();
            container.CustomMinimumSize = new Vector2(42, 0);
            container.AddThemeConstantOverride("separation", 1);
            _resourceBar.AddChild(container);

            // Label at top
            var labelNode = new Label();
            labelNode.Text = label;
            labelNode.HorizontalAlignment = HorizontalAlignment.Center;
            labelNode.AddThemeFontSizeOverride("font_size", 9);
            labelNode.AddThemeColorOverride("font_color", color.Lightened(0.3f));
            container.AddChild(labelNode);

            // Progress bar (or pip for action/bonus/reaction)
            var bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(38, id == "move" ? 8 : 12);
            bar.Value = max > 0 ? (float)current / max * 100 : 0;
            bar.ShowPercentage = false;

            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);
            bgStyle.SetCornerRadiusAll(0);
            bgStyle.SetBorderWidthAll(1);
            bgStyle.BorderColor = color.Darkened(0.4f);
            bar.AddThemeStyleboxOverride("background", bgStyle);

            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = color;
            fillStyle.SetCornerRadiusAll(0);
            bar.AddThemeStyleboxOverride("fill", fillStyle);

            container.AddChild(bar);
            _resourceBars[id] = bar;

            var valueLabel = new Label();
            valueLabel.Text = $"{current}/{max}";
            valueLabel.HorizontalAlignment = HorizontalAlignment.Center;
            valueLabel.AddThemeFontSizeOverride("font_size", 11);
            container.AddChild(valueLabel);
            _resourceLabels[id] = valueLabel;
        }

        private void SetupCombatLog()
        {
            // Combat log panel on right side (BG3-style)
            _logPanel = new PanelContainer();
            // Use explicit anchors only
            _logPanel.AnchorLeft = 1.0f;
            _logPanel.AnchorRight = 1.0f;
            _logPanel.AnchorTop = 0.12f;
            _logPanel.AnchorBottom = 0.7f;
            _logPanel.OffsetLeft = -260;
            _logPanel.OffsetRight = -10;
            _logPanel.OffsetTop = 0;
            _logPanel.OffsetBottom = 0;
            _logPanel.CustomMinimumSize = new Vector2(240, 0);

            var logStyle = new StyleBoxFlat();
            logStyle.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.92f);
            logStyle.SetCornerRadiusAll(0);
            logStyle.SetBorderWidthAll(1);
            logStyle.BorderColor = new Color(0.25f, 0.22f, 0.18f);
            _logPanel.AddThemeStyleboxOverride("panel", logStyle);
            AddChild(_logPanel);

            var logVBox = new VBoxContainer();
            _logPanel.AddChild(logVBox);

            // Header
            var logHeader = new Label();
            logHeader.Text = "Combat Log";
            logHeader.HorizontalAlignment = HorizontalAlignment.Center;
            logHeader.AddThemeFontSizeOverride("font_size", 16);
            logVBox.AddChild(logHeader);

            // Separator
            var separator = new HSeparator();
            logVBox.AddChild(separator);

            // Scrollable log entries
            _logScroll = new ScrollContainer();
            _logScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            _logScroll.Set("horizontal_scroll_mode", 0); // 0 = Disabled
            logVBox.AddChild(_logScroll);

            _logContainer = new VBoxContainer();
            _logContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            _logContainer.AddThemeConstantOverride("separation", 2);
            _logScroll.AddChild(_logContainer);
        }

        private Button CreateAbilityButton(int index)
        {
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(56, 56);
            btn.Text = index < 9 ? $"{index + 1}" : (index == 9 ? "0" : "");
            btn.TooltipText = "Empty slot";
            btn.Disabled = true;
            btn.MouseFilter = MouseFilterEnum.Stop;

            // BG3-style ability slot: dark with bronze border
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.08f, 0.08f, 0.1f);  // Very dark
            normalStyle.SetCornerRadiusAll(0);  // Sharp corners
            normalStyle.SetBorderWidthAll(2);
            normalStyle.BorderColor = new Color(0.35f, 0.3f, 0.2f);  // Subtle bronze
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
            btn.AddThemeFontSizeOverride("font_size", 12);

            // Disabled style (empty slots)
            var disabledStyle = new StyleBoxFlat();
            disabledStyle.BgColor = new Color(0.06f, 0.06f, 0.08f);
            disabledStyle.SetCornerRadiusAll(0);
            disabledStyle.SetBorderWidthAll(1);
            disabledStyle.BorderColor = new Color(0.2f, 0.18f, 0.15f);
            btn.AddThemeStyleboxOverride("disabled", disabledStyle);
            btn.AddThemeColorOverride("font_disabled_color", new Color(0.3f, 0.3f, 0.3f));

            // Hover shows golden highlight
            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.12f, 0.11f, 0.1f);
            hoverStyle.SetCornerRadiusAll(0);
            hoverStyle.SetBorderWidthAll(2);
            hoverStyle.BorderColor = new Color(0.7f, 0.55f, 0.25f);  // Gold
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            int capturedIndex = index;
            btn.Pressed += () => OnAbilityPressed(capturedIndex);

            if (DebugUI)
                GD.Print($"[CombatHUD] Created ability button {index} with MouseFilter: {btn.MouseFilter}");

            return btn;
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            // Show/hide action bar based on state
            if (_bottomBar != null)
            {
                _bottomBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }

            if (_endTurnButton != null)
            {
                _endTurnButton.Disabled = evt.ToState != CombatState.PlayerDecision;
            }

            // Show/hide resource bar based on state
            if (_resourceBar != null)
            {
                _resourceBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
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
            if (_resourceBars.TryGetValue(id, out var bar))
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

            if (_resourceLabels.TryGetValue(id, out var label))
            {
                label.Text = $"{current}/{max}";
                label.Modulate = current <= 0 ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
            }
        }

        private void OnTurnOrderChanged()
        {
            if (Arena?.TurnTrackerModel == null) return;

            // Rebuild turn tracker from model
            var entries = Arena.TurnTrackerModel.Entries;
            RefreshTurnTrackerFromModel(entries, Arena.TurnTrackerModel.ActiveCombatantId);
        }

        private void OnActiveCombatantChanged(string combatantId)
        {
            // Update highlighting in turn tracker
            foreach (var kvp in _turnPortraits)
            {
                bool isActive = kvp.Key == combatantId;
                var panel = kvp.Value as PanelContainer;
                if (panel != null)
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
            var entry = Arena?.TurnTrackerModel?.GetEntry(combatantId);
            if (entry == null || !_turnPortraits.TryGetValue(combatantId, out var portrait)) return;

            // Update HP display in portrait
            // Find HP label in portrait
            var vbox = portrait.GetChild(0) as VBoxContainer;
            if (vbox != null && vbox.GetChildCount() >= 3)
            {
                var hpLabel = vbox.GetChild(2) as Label;
                if (hpLabel != null)
                {
                    int hp = (int)(entry.HpPercent * 100);
                    hpLabel.Text = $"{hp}%";
                    hpLabel.Modulate = entry.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
                }
            }
        }

        private void OnResourceChanged(string resourceId)
        {
            var resource = Arena?.ResourceBarModel?.GetResource(resourceId);
            if (resource == null) return;

            UpdateResourceBar(resourceId, resource.Current, resource.Maximum);
        }

        private void OnHealthChanged(int current, int max, int temp)
        {
            UpdateResourceBar("health", current, max);
        }

        private void OnActionsChanged()
        {
            if (Arena?.ActionBarModel == null) return;

            // Rebuild ability buttons from model
            var actions = Arena.ActionBarModel.Actions.ToList();
            for (int i = 0; i < _abilityButtons.Count; i++)
            {
                var btn = _abilityButtons[i];
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

        private void OnActionUpdated(string actionId)
        {
            if (Arena?.ActionBarModel == null) return;

            var actions = Arena.ActionBarModel.Actions.ToList();
            for (int i = 0; i < actions.Count && i < _abilityButtons.Count; i++)
            {
                if (actions[i].ActionId == actionId)
                {
                    var action = actions[i];
                    var btn = _abilityButtons[i];
                    btn.Disabled = !action.IsAvailable;

                    // Show cooldown/charge state visually
                    if (action.HasCooldown)
                    {
                        btn.Text = $"[{i + 1}]\n{action.DisplayName}\n(CD:{action.CooldownRemaining})";
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
            // BG3-style: Active portrait is taller
            panel.CustomMinimumSize = isActive ? new Vector2(60, 60) : new Vector2(50, 50);

            var styleBox = new StyleBoxFlat();
            // Dark background
            styleBox.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            styleBox.SetCornerRadiusAll(0);  // Sharp corners like BG3
            
            // Border: Player=Blue, Enemy=Red, Active=Gold
            var factionColor = entry.IsPlayer 
                ? new Color(0.3f, 0.5f, 0.8f)   // Blue for player
                : new Color(0.8f, 0.25f, 0.2f); // Red for enemy
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 2);
            styleBox.BorderColor = isActive 
                ? new Color(0.85f, 0.7f, 0.25f)  // Gold for active
                : factionColor;

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Initiative number at top
            var initLabel = new Label();
            initLabel.Text = entry.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            initLabel.AddThemeFontSizeOverride("font_size", 10);
            initLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.4f)); // Bronze
            vbox.AddChild(initLabel);

            // Name (truncated)
            var label = new Label();
            var displayName = entry.DisplayName.Length > 5 ? entry.DisplayName.Substring(0, 5) : entry.DisplayName;
            label.Text = displayName;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", isActive ? 12 : 10);
            label.AddThemeColorOverride("font_color", entry.IsDead 
                ? new Color(0.4f, 0.4f, 0.4f) 
                : Colors.White);
            vbox.AddChild(label);

            // HP Bar
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(isActive ? 50 : 40, 5);
            hpBar.ShowPercentage = false;
            hpBar.Value = entry.HpPercent * 100;
            
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.15f, 0.08f, 0.08f);
            bgStyle.SetCornerRadiusAll(0);
            var fillStyle = new StyleBoxFlat();
            // HP color based on health level
            fillStyle.BgColor = entry.HpPercent > 0.5f 
                ? new Color(0.2f, 0.7f, 0.25f)  // Green
                : entry.HpPercent > 0.25f 
                    ? new Color(0.8f, 0.65f, 0.2f)  // Yellow
                    : new Color(0.8f, 0.2f, 0.2f);  // Red
            fillStyle.SetCornerRadiusAll(0);
            hpBar.AddThemeStyleboxOverride("background", bgStyle);
            hpBar.AddThemeStyleboxOverride("fill", fillStyle);
            
            vbox.AddChild(hpBar);

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
            // BG3-style: Active portrait is taller
            panel.CustomMinimumSize = isActive ? new Vector2(60, 60) : new Vector2(50, 50);

            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = new Color(0.06f, 0.06f, 0.08f, 0.95f);
            styleBox.SetCornerRadiusAll(0);  // Sharp corners
            
            // Border: Player=Blue, Enemy=Red, Active=Gold
            var factionColor = c.Faction == Faction.Player 
                ? new Color(0.3f, 0.5f, 0.8f)
                : new Color(0.8f, 0.25f, 0.2f);
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 2);
            styleBox.BorderColor = isActive 
                ? new Color(0.85f, 0.7f, 0.25f)
                : factionColor;

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Initiative
            var initLabel = new Label();
            initLabel.Text = c.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            initLabel.AddThemeFontSizeOverride("font_size", 10);
            initLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.4f));
            vbox.AddChild(initLabel);

            // Name
            var label = new Label();
            var displayName = c.Name.Length > 5 ? c.Name.Substring(0, 5) : c.Name;
            label.Text = displayName;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", isActive ? 12 : 10);
            vbox.AddChild(label);

            // HP Bar with color based on health
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(isActive ? 50 : 40, 5);
            hpBar.ShowPercentage = false;
            var hpPercent = (float)c.Resources.CurrentHP / c.Resources.MaxHP;
            hpBar.Value = hpPercent * 100;
            
            var bgStyle = new StyleBoxFlat();
            bgStyle.BgColor = new Color(0.15f, 0.08f, 0.08f);
            bgStyle.SetCornerRadiusAll(0);
            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = hpPercent > 0.5f 
                ? new Color(0.2f, 0.7f, 0.25f)
                : hpPercent > 0.25f 
                    ? new Color(0.8f, 0.65f, 0.2f)
                    : new Color(0.8f, 0.2f, 0.2f);
            fillStyle.SetCornerRadiusAll(0);
            hpBar.AddThemeStyleboxOverride("background", bgStyle);
            hpBar.AddThemeStyleboxOverride("fill", fillStyle);

            vbox.AddChild(hpBar);

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
            AddLogEntry(entry);

            // Auto-scroll to bottom
            CallDeferred(nameof(ScrollLogToBottom));
        }

        private void ScrollLogToBottom()
        {
            if (_logScroll != null)
            {
                _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;
            }
        }

        private void AddLogEntry(CombatLogEntry entry)
        {
            if (_logContainer == null) return;

            // Remove old entries if over limit
            while (_logContainer.GetChildCount() >= MaxLogEntries)
            {
                var oldest = _logContainer.GetChild(0);
                oldest.QueueFree();
            }

            var label = new RichTextLabel();
            label.BbcodeEnabled = true;
            label.FitContent = true;
            label.ScrollActive = false;
            label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            label.CustomMinimumSize = new Vector2(0, 20);

            // Format based on entry type
            string text = FormatLogEntry(entry);
            label.Text = text;

            _logContainer.AddChild(label);
        }

        private string FormatLogEntry(CombatLogEntry entry)
        {
            string color = entry.Type switch
            {
                CombatLogEntryType.DamageDealt => "red",
                CombatLogEntryType.HealingDone => "green",
                CombatLogEntryType.StatusApplied => "purple",
                CombatLogEntryType.StatusRemoved => "gray",
                CombatLogEntryType.TurnStarted => "yellow",
                CombatLogEntryType.CombatStarted => "cyan",
                CombatLogEntryType.CombatEnded => "cyan",
                _ => "white"
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
            // Update inspect panel based on current selection
            if (Arena != null && !string.IsNullOrEmpty(Arena.SelectedCombatantId))
            {
                var combatant = Arena.Context?.GetCombatant(Arena.SelectedCombatantId);
                if (combatant != null)
                {
                    ShowInspect(combatant);
                }
            }
            else if (_inspectPanel?.Visible == true)
            {
                // Only hide if visible to avoid constant updates
                HideInspect();
            }
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
