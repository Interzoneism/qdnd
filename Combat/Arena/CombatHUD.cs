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
                GD.Print("[CombatHUD] SetupUI started");

            // Main container
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore; // Allow clicks to pass through to 3D

            if (DebugUI)
                GD.Print($"[CombatHUD] MouseFilter set to: {MouseFilter}");

            SetupTurnTracker();

            // Bottom bar - Action Bar
            _bottomBar = new PanelContainer();
            _bottomBar.SetAnchorsPreset(LayoutPreset.BottomWide);
            _bottomBar.CustomMinimumSize = new Vector2(0, 100);
            _bottomBar.MouseFilter = MouseFilterEnum.Stop; // Catch mouse in UI areas
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            style.SetCornerRadiusAll(5);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.3f, 0.3f, 0.4f);
            _bottomBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(_bottomBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Bottom bar created with MouseFilter: {_bottomBar.MouseFilter}");

            var bottomLayout = new HBoxContainer();
            bottomLayout.Alignment = BoxContainer.AlignmentMode.End;
            bottomLayout.AddThemeConstantOverride("separation", 20);
            _bottomBar.AddChild(bottomLayout);

            // Action bar in center
            _actionBar = new HBoxContainer();
            _actionBar.AddThemeConstantOverride("separation", 5);
            bottomLayout.AddChild(_actionBar);

            _endTurnButton = new Button();
            _endTurnButton.Text = "End Turn";
            _endTurnButton.CustomMinimumSize = new Vector2(140, 60);
            
            // End Turn Button Styling
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.2f, 0.4f, 0.6f); // dark blue
            normalStyle.SetCornerRadiusAll(5);
            normalStyle.SetBorderWidthAll(2);
            normalStyle.BorderColor = new Color(0.6f, 0.5f, 0.2f); // gold-ish border
            _endTurnButton.AddThemeStyleboxOverride("normal", normalStyle);

            var hoverStyle = new StyleBoxFlat();
            hoverStyle.BgColor = new Color(0.3f, 0.5f, 0.7f);
            hoverStyle.SetCornerRadiusAll(5);
            hoverStyle.SetBorderWidthAll(2);
            hoverStyle.BorderColor = new Color(0.8f, 0.7f, 0.3f); 
            _endTurnButton.AddThemeStyleboxOverride("hover", hoverStyle);

            _endTurnButton.Pressed += OnEndTurnPressed;
            _endTurnButton.MouseFilter = MouseFilterEnum.Stop;
            bottomLayout.AddChild(_endTurnButton);

            // Initially hide action bar
            _bottomBar.Visible = false;

            SetupCombatLog();
            SetupResourceBar();
            SetupInspectPanel();
        }

        private void SetupTurnTracker()
        {
            // Top bar - Turn Tracker
            var topBar = new PanelContainer();
            topBar.SetAnchorsPreset(LayoutPreset.TopWide);
            topBar.CustomMinimumSize = new Vector2(0, 60);
            topBar.MouseFilter = MouseFilterEnum.Stop; // Catch mouse in UI areas
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            style.SetCornerRadiusAll(5);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.3f, 0.3f, 0.4f);
            topBar.AddThemeStyleboxOverride("panel", style);
            
            AddChild(topBar);

            if (DebugUI)
                GD.Print($"[CombatHUD] Top bar MouseFilter: {topBar.MouseFilter}");

            _turnTracker = new HBoxContainer();
            _turnTracker.Alignment = BoxContainer.AlignmentMode.Center;
            _turnTracker.AddThemeConstantOverride("separation", 10);
            topBar.AddChild(_turnTracker);
        }

        private void SetupResourceBar()
        {
            _resourceBar = new HBoxContainer();
            _resourceBar.SetAnchorsPreset(LayoutPreset.BottomLeft);
            _resourceBar.Position = new Vector2(20, -120);
            _resourceBar.AddThemeConstantOverride("separation", 10);
            AddChild(_resourceBar);
        }

        private void SetupInspectPanel()
        {
            // Inspect panel on left side
            _inspectPanel = new PanelContainer();
            _inspectPanel.SetAnchorsPreset(LayoutPreset.LeftWide);
            _inspectPanel.AnchorRight = 0;
            _inspectPanel.OffsetLeft = 10;
            _inspectPanel.OffsetRight = 250;
            _inspectPanel.OffsetTop = 150;
            _inspectPanel.OffsetBottom = -150;
            _inspectPanel.CustomMinimumSize = new Vector2(230, 200);
            _inspectPanel.Visible = false; // Hidden by default

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            style.SetCornerRadiusAll(5);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.3f, 0.3f, 0.4f);
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
            var container = new VBoxContainer();
            container.CustomMinimumSize = new Vector2(80, 0);
            _resourceBar.AddChild(container);

            var labelNode = new Label();
            labelNode.Text = label;
            labelNode.HorizontalAlignment = HorizontalAlignment.Center;
            labelNode.AddThemeFontSizeOverride("font_size", 12);
            container.AddChild(labelNode);

            var bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(70, 15);
            bar.Value = max > 0 ? (float)current / max * 100 : 0;
            bar.ShowPercentage = false;

            var barStyle = new StyleBoxFlat();
            barStyle.BgColor = color.Darkened(0.6f);
            bar.AddThemeStyleboxOverride("background", barStyle);

            var fillStyle = new StyleBoxFlat();
            fillStyle.BgColor = color;
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
            // Combat log panel on right side
            _logPanel = new PanelContainer();
            _logPanel.SetAnchorsPreset(LayoutPreset.RightWide);
            _logPanel.AnchorLeft = 1.0f;
            _logPanel.AnchorRight = 1.0f;
            _logPanel.AnchorTop = 0.1f;
            _logPanel.AnchorBottom = 0.7f;
            _logPanel.OffsetLeft = -280;
            _logPanel.OffsetRight = -10;
            _logPanel.OffsetTop = 10;
            _logPanel.CustomMinimumSize = new Vector2(260, 0);

            var logStyle = new StyleBoxFlat();
            logStyle.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            logStyle.SetCornerRadiusAll(5);
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
            btn.CustomMinimumSize = new Vector2(60, 60);
            btn.Text = $"[{index + 1}]";
            btn.TooltipText = "No ability";
            btn.Disabled = true;
            btn.MouseFilter = MouseFilterEnum.Stop; // Ensure buttons catch mouse events

            // Styling for ability buttons to look like slots
            var normalStyle = new StyleBoxFlat();
            normalStyle.BgColor = new Color(0.15f, 0.15f, 0.2f); // dark slot
            normalStyle.SetCornerRadiusAll(3);
            normalStyle.SetBorderWidthAll(1);
            normalStyle.BorderColor = new Color(0.4f, 0.4f, 0.4f);
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("disabled", normalStyle); // Same style for empty slots

            var hoverStyle = normalStyle.Duplicate() as StyleBoxFlat;
            if (hoverStyle != null) hoverStyle.BorderColor = new Color(0.8f, 0.8f, 0.8f);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);

            int capturedIndex = index;
            btn.Pressed += () => OnAbilityPressed(capturedIndex);

            if (DebugUI)
                GD.Print($"[CombatHUD] Created ability button {index} with MouseFilter: {btn.MouseFilter}");

            return btn;
        }

        private void OnStateChanged(StateTransitionEvent evt)
        {
            _combatStateLabel.Text = evt.ToState switch
            {
                CombatState.PlayerDecision => "Your Turn",
                CombatState.AIDecision => "Enemy Turn",
                CombatState.ActionExecution => "Action...",
                CombatState.CombatEnd => "Combat Ended",
                _ => evt.ToState.ToString()
            };

            // Show/hide action bar based on state
            if (_bottomBar != null)
            {
                _bottomBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }

            _endTurnButton.Disabled = evt.ToState != CombatState.PlayerDecision;

            // Show/hide resource bar based on state
            if (_resourceBar?.GetParent() is Control resourcePanel)
            {
                resourcePanel.Visible = evt.ToState == CombatState.PlayerDecision;
            }
        }

        private void OnTurnChanged(TurnChangeEvent evt)
        {
            _turnInfoLabel.Text = $"Round {evt.Round}";

            // Update turn tracker highlighting
            foreach (var kvp in _turnPortraits)
            {
                bool isActive = evt.CurrentCombatant?.Id == kvp.Key;
                var panel = kvp.Value as PanelContainer;
                if (panel != null)
                {
                    // Update styling to show active
                    var styleBox = new StyleBoxFlat();
                    styleBox.BgColor = isActive ? new Color(0.2f, 0.8f, 0.3f, 0.8f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
                    styleBox.SetCornerRadiusAll(5);
                    panel.AddThemeStyleboxOverride("panel", styleBox);
                }
            }

            // Update abilities for current combatant
            if (evt.CurrentCombatant != null && evt.CurrentCombatant.IsPlayerControlled)
            {
                UpdateAbilityButtons(evt.CurrentCombatant.Id);
                UpdateUnitInfo(evt.CurrentCombatant);

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
            // Larger active portrait
            panel.CustomMinimumSize = isActive ? new Vector2(70, 70) : new Vector2(50, 50);

            var styleBox = new StyleBoxFlat();
            // Dark background for portrait
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            styleBox.SetCornerRadiusAll(isActive ? 8 : 4);
            
            // Border color indicates faction
            // Player: Blue, Enemy: Red
            var factionColor = entry.IsPlayer ? new Color(0.2f, 0.5f, 1.0f) : new Color(1.0f, 0.3f, 0.3f);
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 2);
            styleBox.BorderColor = isActive ? new Color(0.9f, 0.8f, 0.3f) : factionColor.Darkened(0.2f); // Gold for active

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(vbox);

            // Init Label (top of card)
            var initLabel = new Label();
            initLabel.Text = entry.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            initLabel.AddThemeFontSizeOverride("font_size", isActive ? 12 : 10);
            initLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
            vbox.AddChild(initLabel);

            // Name
            var label = new Label();
            label.Text = entry.DisplayName.Length > 6 ? entry.DisplayName.Substring(0, 6) : entry.DisplayName;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", isActive ? 14 : 11);
            label.Modulate = entry.IsDead ? new Color(0.5f, 0.5f, 0.5f) : Colors.White;
            vbox.AddChild(label);

            // HP percent bar (instead of just text)
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(isActive ? 60 : 40, 4);
            hpBar.ShowPercentage = false;
            hpBar.Value = entry.HpPercent * 100;
            
            var bgStyle = new StyleBoxFlat { BgColor = new Color(0.2f, 0.1f, 0.1f) };
            var fillStyle = new StyleBoxFlat { BgColor = new Color(0.2f, 0.8f, 0.2f) };
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
            // Larger active portrait
            panel.CustomMinimumSize = isActive ? new Vector2(70, 70) : new Vector2(50, 50);

            var styleBox = new StyleBoxFlat();
            // Dark background for portrait
            styleBox.BgColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            styleBox.SetCornerRadiusAll(isActive ? 8 : 4);
            
            // Border color indicates faction
            // Player: Blue, Enemy: Red
            var factionColor = c.Faction == Faction.Player ? new Color(0.2f, 0.5f, 1.0f) : new Color(1.0f, 0.3f, 0.3f);
            
            styleBox.SetBorderWidthAll(isActive ? 3 : 2);
            styleBox.BorderColor = isActive ? new Color(0.9f, 0.8f, 0.3f) : factionColor.Darkened(0.2f); // Gold for active

            panel.AddThemeStyleboxOverride("panel", styleBox);

            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(vbox);

            // Init Label
            var initLabel = new Label();
            initLabel.Text = c.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            initLabel.AddThemeFontSizeOverride("font_size", isActive ? 12 : 10);
            initLabel.Modulate = new Color(0.7f, 0.7f, 0.7f);
            vbox.AddChild(initLabel);

            // Name
            var label = new Label();
            label.Text = c.Name.Length > 6 ? c.Name.Substring(0, 6) : c.Name;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", isActive ? 14 : 11);
            vbox.AddChild(label);

            // HP Bar
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(isActive ? 60 : 40, 4);
            hpBar.ShowPercentage = false;
            hpBar.Value = (float)c.Resources.CurrentHP / c.Resources.MaxHP * 100;
            
            var bgStyle = new StyleBoxFlat { BgColor = new Color(0.2f, 0.1f, 0.1f) };
            var fillStyle = new StyleBoxFlat { BgColor = new Color(0.2f, 0.8f, 0.2f) };
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
            // Clear existing
            foreach (var child in _unitInfoPanel.GetChildren())
            {
                child.QueueFree();
            }

            // Name
            var nameLabel = new Label();
            nameLabel.Text = c.Name;
            nameLabel.AddThemeFontSizeOverride("font_size", 18);
            _unitInfoPanel.AddChild(nameLabel);

            // HP bar
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(180, 20);
            hpBar.Value = (float)c.Resources.CurrentHP / c.Resources.MaxHP * 100;
            hpBar.ShowPercentage = false;
            _unitInfoPanel.AddChild(hpBar);

            // HP text
            var hpLabel = new Label();
            hpLabel.Text = $"HP: {c.Resources.CurrentHP} / {c.Resources.MaxHP}";
            _unitInfoPanel.AddChild(hpLabel);
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
