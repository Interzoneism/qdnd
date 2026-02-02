using Godot;
using System;
using System.Linq;
using QDND.Combat.Entities;

namespace QDND.Combat.Arena
{
    /// <summary>
    /// Debug controls panel for testing combat features.
    /// Toggle visibility with F1 (debug_toggle action).
    /// </summary>
    public partial class DebugPanel : Control
    {
        [Export] public CombatArena Arena;
        
        private PanelContainer _panel;
        private SpinBox _damageAmount;
        private SpinBox _healAmount;
        private LineEdit _statusId;
        private Label _targetLabel;
        private Label _infoLabel;
        
        public override void _Ready()
        {
            if (Arena == null)
            {
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;
            }
            
            SetupUI();
            Visible = false; // Hidden by default
        }
        
        public override void _UnhandledInput(InputEvent @event)
        {
            if (Input.IsActionJustPressed("debug_toggle"))
            {
                ToggleVisibility();
                GetViewport().SetInputAsHandled();
            }
        }
        
        public void ToggleVisibility()
        {
            Visible = !Visible;
            GD.Print($"[DebugPanel] Visibility: {Visible}");
        }
        
        private void SetupUI()
        {
            // Main panel - bottom left
            _panel = new PanelContainer();
            _panel.SetAnchorsPreset(LayoutPreset.BottomLeft);
            _panel.AnchorTop = 1.0f;
            _panel.AnchorBottom = 1.0f;
            _panel.OffsetTop = -280;
            _panel.OffsetLeft = 10;
            _panel.OffsetRight = 260;
            _panel.CustomMinimumSize = new Vector2(240, 260);
            
            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.15f, 0.1f, 0.1f, 0.95f);
            style.SetCornerRadiusAll(5);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.8f, 0.2f, 0.2f);
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);
            
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            _panel.AddChild(vbox);
            
            // Header
            var header = new Label();
            header.Text = "DEBUG PANEL [F1]";
            header.HorizontalAlignment = HorizontalAlignment.Center;
            header.AddThemeFontSizeOverride("font_size", 16);
            header.Modulate = new Color(1.0f, 0.4f, 0.4f);
            vbox.AddChild(header);
            
            vbox.AddChild(new HSeparator());
            
            // Target info
            _targetLabel = new Label();
            _targetLabel.Text = "Target: (none selected)";
            _targetLabel.AddThemeFontSizeOverride("font_size", 12);
            vbox.AddChild(_targetLabel);
            
            vbox.AddChild(new HSeparator());
            
            // Damage controls
            var damageRow = new HBoxContainer();
            damageRow.AddThemeConstantOverride("separation", 5);
            vbox.AddChild(damageRow);
            
            var damageLabel = new Label { Text = "Damage:" };
            damageLabel.CustomMinimumSize = new Vector2(70, 0);
            damageRow.AddChild(damageLabel);
            
            _damageAmount = new SpinBox();
            _damageAmount.MinValue = 1;
            _damageAmount.MaxValue = 100;
            _damageAmount.Value = 10;
            _damageAmount.CustomMinimumSize = new Vector2(60, 0);
            damageRow.AddChild(_damageAmount);
            
            var damageBtn = new Button { Text = "Apply" };
            damageBtn.CustomMinimumSize = new Vector2(60, 0);
            damageBtn.Pressed += OnDamagePressed;
            damageRow.AddChild(damageBtn);
            
            // Heal controls
            var healRow = new HBoxContainer();
            healRow.AddThemeConstantOverride("separation", 5);
            vbox.AddChild(healRow);
            
            var healLabel = new Label { Text = "Heal:" };
            healLabel.CustomMinimumSize = new Vector2(70, 0);
            healRow.AddChild(healLabel);
            
            _healAmount = new SpinBox();
            _healAmount.MinValue = 1;
            _healAmount.MaxValue = 100;
            _healAmount.Value = 10;
            _healAmount.CustomMinimumSize = new Vector2(60, 0);
            healRow.AddChild(_healAmount);
            
            var healBtn = new Button { Text = "Apply" };
            healBtn.CustomMinimumSize = new Vector2(60, 0);
            healBtn.Pressed += OnHealPressed;
            healRow.AddChild(healBtn);
            
            // Status controls
            var statusRow = new HBoxContainer();
            statusRow.AddThemeConstantOverride("separation", 5);
            vbox.AddChild(statusRow);
            
            var statusLabel = new Label { Text = "Status:" };
            statusLabel.CustomMinimumSize = new Vector2(70, 0);
            statusRow.AddChild(statusLabel);
            
            _statusId = new LineEdit();
            _statusId.PlaceholderText = "poisoned";
            _statusId.CustomMinimumSize = new Vector2(80, 0);
            statusRow.AddChild(_statusId);
            
            var statusBtn = new Button { Text = "Apply" };
            statusBtn.CustomMinimumSize = new Vector2(50, 0);
            statusBtn.Pressed += OnStatusPressed;
            statusRow.AddChild(statusBtn);
            
            vbox.AddChild(new HSeparator());
            
            // Combat controls
            var combatRow = new HBoxContainer();
            combatRow.AddThemeConstantOverride("separation", 5);
            combatRow.Alignment = BoxContainer.AlignmentMode.Center;
            vbox.AddChild(combatRow);
            
            var endCombatBtn = new Button { Text = "End Combat" };
            endCombatBtn.Pressed += OnEndCombatPressed;
            combatRow.AddChild(endCombatBtn);
            
            var killTargetBtn = new Button { Text = "Kill Target" };
            killTargetBtn.Pressed += OnKillTargetPressed;
            combatRow.AddChild(killTargetBtn);
            
            vbox.AddChild(new HSeparator());
            
            // Info label
            _infoLabel = new Label();
            _infoLabel.Text = "";
            _infoLabel.AddThemeFontSizeOverride("font_size", 11);
            _infoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _infoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_infoLabel);
        }
        
        public override void _Process(double delta)
        {
            // Update target label
            if (Arena != null && !string.IsNullOrEmpty(Arena.SelectedCombatantId))
            {
                var combatant = Arena.Context?.GetCombatant(Arena.SelectedCombatantId);
                if (combatant != null)
                {
                    _targetLabel.Text = $"Target: {combatant.Name} (HP: {combatant.Resources.CurrentHP}/{combatant.Resources.MaxHP})";
                }
            }
            else
            {
                _targetLabel.Text = "Target: (select a combatant)";
            }
        }
        
        private Combatant GetSelectedCombatant()
        {
            if (Arena == null || string.IsNullOrEmpty(Arena.SelectedCombatantId))
            {
                _infoLabel.Text = "No target selected!";
                return null;
            }
            return Arena.Context?.GetCombatant(Arena.SelectedCombatantId);
        }
        
        private void OnDamagePressed()
        {
            var target = GetSelectedCombatant();
            if (target == null) return;
            
            int amount = (int)_damageAmount.Value;
            target.Resources.TakeDamage(amount);
            
            // Update visual
            Arena.GetVisual(target.Id)?.ShowDamage(amount);
            Arena.GetVisual(target.Id)?.UpdateFromEntity();
            
            _infoLabel.Text = $"Dealt {amount} damage to {target.Name}";
            GD.Print($"[Debug] Dealt {amount} damage to {target.Name}");
        }
        
        private void OnHealPressed()
        {
            var target = GetSelectedCombatant();
            if (target == null) return;
            
            int amount = (int)_healAmount.Value;
            target.Resources.Heal(amount);
            
            // Update visual
            Arena.GetVisual(target.Id)?.ShowHealing(amount);
            Arena.GetVisual(target.Id)?.UpdateFromEntity();
            
            _infoLabel.Text = $"Healed {target.Name} for {amount}";
            GD.Print($"[Debug] Healed {target.Name} for {amount}");
        }
        
        private void OnStatusPressed()
        {
            var target = GetSelectedCombatant();
            if (target == null) return;
            
            string statusId = _statusId.Text;
            if (string.IsNullOrWhiteSpace(statusId))
            {
                statusId = "poisoned"; // Default
            }
            
            var statusManager = Arena.Context?.GetService<QDND.Combat.Statuses.StatusManager>();
            if (statusManager != null)
            {
                var instance = statusManager.ApplyStatus(statusId, "debug", target.Id, 3);
                
                if (instance != null)
                {
                    Arena.GetVisual(target.Id)?.ShowStatusApplied(statusId);
                    _infoLabel.Text = $"Applied {statusId} to {target.Name}";
                    GD.Print($"[Debug] Applied {statusId} to {target.Name}");
                }
                else
                {
                    _infoLabel.Text = $"Failed to apply {statusId}";
                }
            }
        }
        
        private void OnEndCombatPressed()
        {
            // Force end combat by killing all enemies or triggering end state
            _infoLabel.Text = "Forcing combat end...";
            GD.Print("[Debug] Force ending combat");
            
            // This is a debug action - in real games wouldn't be available
            var combatants = Arena.GetCombatants().ToList();
            foreach (var c in combatants.Where(c => c.Faction == Faction.Hostile && c.IsActive))
            {
                c.Resources.TakeDamage(9999);
                Arena.GetVisual(c.Id)?.UpdateFromEntity();
            }
        }
        
        private void OnKillTargetPressed()
        {
            var target = GetSelectedCombatant();
            if (target == null) return;
            
            target.Resources.TakeDamage(9999);
            Arena.GetVisual(target.Id)?.ShowDamage(9999);
            Arena.GetVisual(target.Id)?.UpdateFromEntity();
            
            _infoLabel.Text = $"Killed {target.Name}";
            GD.Print($"[Debug] Killed {target.Name}");
        }
    }
}
