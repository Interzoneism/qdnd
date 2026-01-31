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
        
        // UI Elements
        private HBoxContainer _turnTracker;
        private HBoxContainer _actionBar;
        private VBoxContainer _unitInfoPanel;
        private Button _endTurnButton;
        private Label _combatStateLabel;
        private Label _turnInfoLabel;
        
        // State
        private Dictionary<string, Control> _turnPortraits = new();
        private List<Button> _abilityButtons = new();
        
        public override void _Ready()
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
                }
            }
        }

        private void SetupUI()
        {
            // Main container
            SetAnchorsPreset(LayoutPreset.FullRect);
            
            // Top bar - Turn Tracker
            var topBar = new PanelContainer();
            topBar.SetAnchorsPreset(LayoutPreset.TopWide);
            topBar.CustomMinimumSize = new Vector2(0, 60);
            AddChild(topBar);
            
            _turnTracker = new HBoxContainer();
            _turnTracker.Alignment = BoxContainer.AlignmentMode.Center;
            _turnTracker.AddThemeConstantOverride("separation", 10);
            topBar.AddChild(_turnTracker);
            
            // Combat state label
            _combatStateLabel = new Label();
            _combatStateLabel.SetAnchorsPreset(LayoutPreset.TopLeft);
            _combatStateLabel.Position = new Vector2(10, 70);
            _combatStateLabel.Text = "Combat";
            _combatStateLabel.AddThemeFontSizeOverride("font_size", 18);
            AddChild(_combatStateLabel);
            
            // Turn info label
            _turnInfoLabel = new Label();
            _turnInfoLabel.SetAnchorsPreset(LayoutPreset.TopRight);
            _turnInfoLabel.Position = new Vector2(-200, 70);
            _turnInfoLabel.Size = new Vector2(190, 30);
            _turnInfoLabel.HorizontalAlignment = HorizontalAlignment.Right;
            _turnInfoLabel.Text = "Round 1";
            _turnInfoLabel.AddThemeFontSizeOverride("font_size", 18);
            AddChild(_turnInfoLabel);
            
            // Bottom bar - Action Bar
            var bottomBar = new PanelContainer();
            bottomBar.SetAnchorsPreset(LayoutPreset.BottomWide);
            bottomBar.CustomMinimumSize = new Vector2(0, 80);
            bottomBar.Position = new Vector2(0, -80);
            AddChild(bottomBar);
            
            var bottomLayout = new HBoxContainer();
            bottomLayout.Alignment = BoxContainer.AlignmentMode.Center;
            bottomLayout.AddThemeConstantOverride("separation", 20);
            bottomBar.AddChild(bottomLayout);
            
            // Unit info on left
            _unitInfoPanel = new VBoxContainer();
            _unitInfoPanel.CustomMinimumSize = new Vector2(200, 70);
            bottomLayout.AddChild(_unitInfoPanel);
            
            // Action bar in center
            _actionBar = new HBoxContainer();
            _actionBar.AddThemeConstantOverride("separation", 5);
            bottomLayout.AddChild(_actionBar);
            
            // Create ability buttons
            for (int i = 0; i < 6; i++)
            {
                var btn = CreateAbilityButton(i);
                _actionBar.AddChild(btn);
                _abilityButtons.Add(btn);
            }
            
            // End turn button on right
            _endTurnButton = new Button();
            _endTurnButton.Text = "End Turn\n[Space]";
            _endTurnButton.CustomMinimumSize = new Vector2(100, 60);
            _endTurnButton.Pressed += OnEndTurnPressed;
            bottomLayout.AddChild(_endTurnButton);
            
            // Initially hide action bar
            bottomBar.Visible = false;
        }

        private Button CreateAbilityButton(int index)
        {
            var btn = new Button();
            btn.CustomMinimumSize = new Vector2(70, 60);
            btn.Text = $"[{index + 1}]";
            btn.TooltipText = "No ability";
            btn.Disabled = true;
            
            int capturedIndex = index;
            btn.Pressed += () => OnAbilityPressed(capturedIndex);
            
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
            var bottomBar = _actionBar.GetParent()?.GetParent() as Control;
            if (bottomBar != null)
            {
                bottomBar.Visible = evt.ToState == CombatState.PlayerDecision;
            }
            
            _endTurnButton.Disabled = evt.ToState != CombatState.PlayerDecision;
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
            }
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
            panel.CustomMinimumSize = new Vector2(50, 50);
            
            var styleBox = new StyleBoxFlat();
            styleBox.BgColor = isActive ? new Color(0.2f, 0.8f, 0.3f, 0.8f) : new Color(0.2f, 0.2f, 0.2f, 0.8f);
            styleBox.SetCornerRadiusAll(5);
            panel.AddThemeStyleboxOverride("panel", styleBox);
            
            var vbox = new VBoxContainer();
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(vbox);
            
            // Faction indicator
            var factionColor = c.Faction == Faction.Player ? new Color(0.2f, 0.5f, 1.0f) : new Color(1.0f, 0.3f, 0.3f);
            var indicator = new ColorRect();
            indicator.Color = factionColor;
            indicator.CustomMinimumSize = new Vector2(40, 5);
            vbox.AddChild(indicator);
            
            // Name
            var label = new Label();
            label.Text = c.Name.Length > 6 ? c.Name.Substring(0, 6) : c.Name;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(label);
            
            // HP
            var hpLabel = new Label();
            hpLabel.Text = $"{c.Resources.CurrentHP}/{c.Resources.MaxHP}";
            hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hpLabel.AddThemeFontSizeOverride("font_size", 10);
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
            if (Arena == null || !Arena.IsPlayerTurn) return;
            
            var abilities = Arena.GetAbilitiesForCombatant(Arena.SelectedCombatantId);
            if (index >= 0 && index < abilities.Count)
            {
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
            Arena?.EndCurrentTurn();
        }
    }
}
