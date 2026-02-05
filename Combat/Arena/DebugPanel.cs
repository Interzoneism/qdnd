using Godot;
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Environment;

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
        private ScrollContainer _scrollContainer;

        // Existing controls
        private SpinBox _damageAmount;
        private SpinBox _healAmount;
        private LineEdit _statusId;
        private Label _targetLabel;
        private Label _infoLabel;

        // New controls
        private OptionButton _spawnFactionSelector;
        private OptionButton _surfaceTypeSelector;
        private SpinBox _initiativeInput;
        private SpinBox _forceRollInput;

        // Collapsible sections
        private Button _targetingSectionHeader;
        private VBoxContainer _targetingSectionContent;
        private Button _spawningSectionHeader;
        private VBoxContainer _spawningSectionContent;
        private Button _debugToolsSectionHeader;
        private VBoxContainer _debugToolsSectionContent;

        // Forced roll state
        private int? _forcedNextRoll = null;

        public override void _Ready()
        {
            if (Arena == null)
            {
                Arena = GetTree().Root.FindChild("CombatArena", true, false) as CombatArena;
            }

            SetupUI();
            Visible = false; // Hidden by default

            // Subscribe to RulesEngine events for forced roll
            if (Arena?.Context != null)
            {
                var rulesEngine = Arena.Context.GetService<QDND.Combat.Rules.RulesEngine>();
                if (rulesEngine != null)
                {
                    // Hook into dice rolling (we'll need to modify RulesEngine later or use a different approach)
                    GD.Print("[DebugPanel] Ready - RulesEngine found");
                }
            }
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
            // Main panel - bottom left with scroll
            // Use explicit anchors instead of SetAnchorsPreset (doesn't work properly)
            _panel = new PanelContainer();
            _panel.AnchorLeft = 0.0f;
            _panel.AnchorRight = 0.0f;
            _panel.AnchorTop = 0.2f;
            _panel.AnchorBottom = 1.0f;
            _panel.OffsetTop = 0;
            _panel.OffsetLeft = 10;
            _panel.OffsetRight = 310;
            _panel.OffsetBottom = -10;
            _panel.CustomMinimumSize = new Vector2(300, 0);

            var style = new StyleBoxFlat();
            style.BgColor = new Color(0.15f, 0.1f, 0.1f, 0.95f);
            style.SetCornerRadiusAll(5);
            style.SetBorderWidthAll(2);
            style.BorderColor = new Color(0.8f, 0.2f, 0.2f);
            _panel.AddThemeStyleboxOverride("panel", style);
            AddChild(_panel);

            // Scroll container
            _scrollContainer = new ScrollContainer();
            _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            _panel.AddChild(_scrollContainer);

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            _scrollContainer.AddChild(vbox);

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

            // === TARGETING SECTION ===
            (_targetingSectionHeader, _targetingSectionContent) = CreateCollapsibleSection(vbox, "TARGETING");
            SetupTargetingSection(_targetingSectionContent);

            // === SPAWNING SECTION ===
            (_spawningSectionHeader, _spawningSectionContent) = CreateCollapsibleSection(vbox, "SPAWNING");
            SetupSpawningSection(_spawningSectionContent);

            // === DEBUG TOOLS SECTION ===
            (_debugToolsSectionHeader, _debugToolsSectionContent) = CreateCollapsibleSection(vbox, "DEBUG TOOLS");
            SetupDebugToolsSection(_debugToolsSectionContent);

            vbox.AddChild(new HSeparator());

            // Info label
            _infoLabel = new Label();
            _infoLabel.Text = "";
            _infoLabel.AddThemeFontSizeOverride("font_size", 11);
            _infoLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _infoLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_infoLabel);
        }

        private (Button header, VBoxContainer content) CreateCollapsibleSection(VBoxContainer parent, string sectionName)
        {
            var header = new Button();
            header.Text = $"▼ {sectionName}";
            header.Alignment = HorizontalAlignment.Left;
            header.ToggleMode = false;
            header.Flat = true;
            header.AddThemeFontSizeOverride("font_size", 13);
            header.Modulate = new Color(1.0f, 0.8f, 0.3f);
            parent.AddChild(header);

            var content = new VBoxContainer();
            content.AddThemeConstantOverride("separation", 4);
            content.Visible = true; // Start expanded
            parent.AddChild(content);

            // Toggle visibility on header click
            header.Pressed += () =>
            {
                content.Visible = !content.Visible;
                header.Text = (content.Visible ? "▼ " : "▶ ") + sectionName;
            };

            return (header, content);
        }

        private void SetupTargetingSection(VBoxContainer parent)
        {
            // Damage controls
            var damageRow = new HBoxContainer();
            damageRow.AddThemeConstantOverride("separation", 5);
            parent.AddChild(damageRow);

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
            parent.AddChild(healRow);

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
            parent.AddChild(statusRow);

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

            // Combat controls
            parent.AddChild(new HSeparator());
            var combatRow = new HBoxContainer();
            combatRow.AddThemeConstantOverride("separation", 5);
            combatRow.Alignment = BoxContainer.AlignmentMode.Center;
            parent.AddChild(combatRow);

            var killTargetBtn = new Button { Text = "Kill Target" };
            killTargetBtn.Pressed += OnKillTargetPressed;
            combatRow.AddChild(killTargetBtn);

            var endCombatBtn = new Button { Text = "End Combat" };
            endCombatBtn.Pressed += OnEndCombatPressed;
            combatRow.AddChild(endCombatBtn);
        }

        private void SetupSpawningSection(VBoxContainer parent)
        {
            // Spawn Unit controls
            var spawnUnitRow = new HBoxContainer();
            spawnUnitRow.AddThemeConstantOverride("separation", 5);
            parent.AddChild(spawnUnitRow);

            _spawnFactionSelector = new OptionButton();
            _spawnFactionSelector.AddItem("Player", 0);
            _spawnFactionSelector.AddItem("Enemy", 1);
            _spawnFactionSelector.CustomMinimumSize = new Vector2(80, 0);
            spawnUnitRow.AddChild(_spawnFactionSelector);

            var spawnUnitBtn = new Button { Text = "Spawn Unit" };
            spawnUnitBtn.CustomMinimumSize = new Vector2(100, 0);
            spawnUnitBtn.Pressed += OnSpawnUnitPressed;
            spawnUnitRow.AddChild(spawnUnitBtn);

            // Spawn Surface controls
            var spawnSurfaceRow = new HBoxContainer();
            spawnSurfaceRow.AddThemeConstantOverride("separation", 5);
            parent.AddChild(spawnSurfaceRow);

            _surfaceTypeSelector = new OptionButton();
            _surfaceTypeSelector.AddItem("Fire", 0);
            _surfaceTypeSelector.AddItem("Ice", 1);
            _surfaceTypeSelector.AddItem("Poison", 2);
            _surfaceTypeSelector.AddItem("Oil", 3);
            _surfaceTypeSelector.CustomMinimumSize = new Vector2(80, 0);
            spawnSurfaceRow.AddChild(_surfaceTypeSelector);

            var spawnSurfaceBtn = new Button { Text = "Spawn Surface" };
            spawnSurfaceBtn.CustomMinimumSize = new Vector2(100, 0);
            spawnSurfaceBtn.Pressed += OnSpawnSurfacePressed;
            spawnSurfaceRow.AddChild(spawnSurfaceBtn);
        }

        private void SetupDebugToolsSection(VBoxContainer parent)
        {
            // Set Initiative
            var initiativeRow = new HBoxContainer();
            initiativeRow.AddThemeConstantOverride("separation", 5);
            parent.AddChild(initiativeRow);

            var initLabel = new Label { Text = "Initiative:" };
            initLabel.CustomMinimumSize = new Vector2(70, 0);
            initiativeRow.AddChild(initLabel);

            _initiativeInput = new SpinBox();
            _initiativeInput.MinValue = 1;
            _initiativeInput.MaxValue = 30;
            _initiativeInput.Value = 15;
            _initiativeInput.CustomMinimumSize = new Vector2(60, 0);
            initiativeRow.AddChild(_initiativeInput);

            var initBtn = new Button { Text = "Set" };
            initBtn.CustomMinimumSize = new Vector2(50, 0);
            initBtn.Pressed += OnSetInitiativePressed;
            initiativeRow.AddChild(initBtn);

            // Force Roll
            var rollRow = new HBoxContainer();
            rollRow.AddThemeConstantOverride("separation", 5);
            parent.AddChild(rollRow);

            var rollLabel = new Label { Text = "Force Roll:" };
            rollLabel.CustomMinimumSize = new Vector2(70, 0);
            rollRow.AddChild(rollLabel);

            _forceRollInput = new SpinBox();
            _forceRollInput.MinValue = 1;
            _forceRollInput.MaxValue = 20;
            _forceRollInput.Value = 20;
            _forceRollInput.CustomMinimumSize = new Vector2(60, 0);
            rollRow.AddChild(_forceRollInput);

            var rollBtn = new Button { Text = "Set" };
            rollBtn.CustomMinimumSize = new Vector2(50, 0);
            rollBtn.Pressed += OnForceRollPressed;
            rollRow.AddChild(rollBtn);

            // Utility buttons
            parent.AddChild(new HSeparator());

            var utilRow1 = new HBoxContainer();
            utilRow1.AddThemeConstantOverride("separation", 5);
            parent.AddChild(utilRow1);

            var hashBtn = new Button { Text = "Print State Hash" };
            hashBtn.Pressed += OnPrintStateHashPressed;
            utilRow1.AddChild(hashBtn);

            var logBtn = new Button { Text = "Export Combat Log" };
            logBtn.Pressed += OnExportLogPressed;
            utilRow1.AddChild(logBtn);

            var utilRow2 = new HBoxContainer();
            utilRow2.AddThemeConstantOverride("separation", 5);
            parent.AddChild(utilRow2);

            var fogBtn = new Button { Text = "Toggle Fog of War" };
            fogBtn.Pressed += OnToggleFogPressed;
            utilRow2.AddChild(fogBtn);
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

        private void OnSpawnUnitPressed()
        {
            if (Arena == null || Arena.Context == null) return;

            var factionIndex = _spawnFactionSelector.Selected;
            var faction = factionIndex == 0 ? Faction.Player : Faction.Hostile;

            // Create a new combatant at a random position near the center
            var rng = new Random();
            var x = (float)(rng.NextDouble() * 10 - 5); // -5 to 5
            var z = (float)(rng.NextDouble() * 10 - 5);
            var randomId = Guid.NewGuid().ToString("N").Substring(0, 8);

            var id = $"debug_{faction}_{randomId}";
            var name = $"{faction} Unit";
            var initiative = (int)(rng.NextDouble() * 20) + 1;
            var combatant = new Combatant(id, name, faction, 20, initiative);
            combatant.Position = new Vector3(x, 0, z);

            // Add to context and turn queue
            Arena.Context.RegisterCombatant(combatant);
            var turnQueue = Arena.Context.GetService<QDND.Combat.Services.TurnQueueService>();
            if (turnQueue != null)
            {
                // Note: TurnQueue might not support adding mid-combat, this is debug only
                GD.PrintErr("[Debug] Warning: Adding combatant mid-combat may cause issues");
            }

            // Spawn visual
            var visual = new CombatantVisual();
            visual.Initialize(combatant, Arena);
            visual.Position = new Vector3(x * Arena.TileSize, 0, z * Arena.TileSize); // World-space meters (TileSize=1)
            visual.Name = $"Visual_{combatant.Id}";

            var container = Arena.GetNode("Combatants");
            container.AddChild(visual);

            _infoLabel.Text = $"Spawned {faction} unit at ({x:F1}, {z:F1})";
            GD.Print($"[Debug] Spawned {faction} unit: {combatant.Id}");
        }

        private void OnSpawnSurfacePressed()
        {
            if (Arena == null || Arena.Context == null) return;

            var surfaceManager = Arena.Context.GetService<SurfaceManager>();
            if (surfaceManager == null)
            {
                _infoLabel.Text = "SurfaceManager not found!";
                return;
            }

            var surfaceTypes = new[] { "fire", "ice", "poison", "oil" };
            var surfaceId = surfaceTypes[_surfaceTypeSelector.Selected];

            // Spawn at origin or near selected combatant
            Vector3 position = Vector3.Zero;
            if (!string.IsNullOrEmpty(Arena.SelectedCombatantId))
            {
                var combatant = Arena.Context.GetCombatant(Arena.SelectedCombatantId);
                if (combatant != null)
                {
                    position = combatant.Position;
                }
            }

            surfaceManager.CreateSurface(surfaceId, position, 5f, "debug");

            _infoLabel.Text = $"Spawned {surfaceId} at {position}";
            GD.Print($"[Debug] Spawned surface: {surfaceId} at {position}");
        }

        private void OnSetInitiativePressed()
        {
            var target = GetSelectedCombatant();
            if (target == null) return;

            int newInitiative = (int)_initiativeInput.Value;
            target.Initiative = newInitiative;

            _infoLabel.Text = $"Set {target.Name} initiative to {newInitiative}";
            GD.Print($"[Debug] Set {target.Name} initiative to {newInitiative}");

            // Note: This won't change turn order mid-combat without resorting
            GD.PrintErr("[Debug] Warning: Changing initiative mid-combat may not affect turn order");
        }

        private void OnForceRollPressed()
        {
            int forcedValue = (int)_forceRollInput.Value;
            _forcedNextRoll = forcedValue;

            _infoLabel.Text = $"Next d20 roll will be {forcedValue}";
            GD.Print($"[Debug] Force next roll to {forcedValue}");
            GD.PrintErr("[Debug] Note: Roll forcing not yet fully implemented in RulesEngine");
        }

        private void OnPrintStateHashPressed()
        {
            if (Arena == null || Arena.Context == null) return;

            var combatants = Arena.GetCombatants().ToList();
            var hash = ComputeStateHash(combatants);

            GD.Print($"[Debug] === STATE HASH ===");
            GD.Print($"Hash: {hash}");
            GD.Print($"Combatants: {combatants.Count}");
            foreach (var c in combatants)
            {
                GD.Print($"  {c.Id}: HP={c.Resources.CurrentHP}/{c.Resources.MaxHP}, Active={c.IsActive}, Faction={c.Faction}");
            }

            _infoLabel.Text = $"State Hash: {hash} (see console)";
        }

        private string ComputeStateHash(List<Combatant> combatants)
        {
            // Simple deterministic hash based on combatant state
            var hash = combatants.Count * 31;
            foreach (var c in combatants.OrderBy(c => c.Id))
            {
                hash = hash * 31 + c.Resources.CurrentHP;
                hash = hash * 31 + (c.IsActive ? 1 : 0);
                hash = hash * 31 + (int)c.Faction;
            }
            return hash.ToString("X8");
        }

        private void OnExportLogPressed()
        {
            if (Arena == null || Arena.Context == null) return;

            var combatLog = Arena.Context.GetService<QDND.Combat.Services.CombatLog>();
            if (combatLog == null)
            {
                _infoLabel.Text = "CombatLog not found!";
                return;
            }

            var entries = combatLog.GetRecentEntries(1000);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"combat_log_{timestamp}.txt";
            var path = Path.Combine(ProjectSettings.GlobalizePath("user://"), filename);

            try
            {
                using (var writer = new StreamWriter(path))
                {
                    writer.WriteLine($"Combat Log Export - {DateTime.Now}");
                    writer.WriteLine("=".PadRight(60, '='));
                    writer.WriteLine();

                    foreach (var entry in entries)
                    {
                        writer.WriteLine($"[{entry.Timestamp:HH:mm:ss}] [{entry.Type}] {entry.Message}");
                    }
                }

                _infoLabel.Text = $"Exported to {filename}";
                GD.Print($"[Debug] Combat log exported to: {path}");
            }
            catch (Exception ex)
            {
                _infoLabel.Text = $"Export failed: {ex.Message}";
                GD.PrintErr($"[Debug] Log export failed: {ex.Message}");
            }
        }

        private void OnToggleFogPressed()
        {
            // Placeholder - fog of war not yet implemented
            _infoLabel.Text = "Fog of War not yet implemented";
            GD.Print("[Debug] Fog of War toggle requested (not implemented)");
        }
    }
}
