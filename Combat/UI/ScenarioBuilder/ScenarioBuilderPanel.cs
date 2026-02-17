using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.Entities;
using QDND.Combat.UI.Base;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.ScenarioBuilder
{
    /// <summary>
    /// Difficulty presets matching BG3 difficulty modes.
    /// </summary>
    public enum DifficultyMode
    {
        Explorer,
        Balanced,
        Tactician,
        HonourMode
    }

    /// <summary>
    /// Environment preset for the combat arena.
    /// </summary>
    public enum EnvironmentPreset
    {
        OpenField,
        Dungeon,
        Forest,
        Castle,
        Cave
    }

    /// <summary>
    /// Represents one combatant slot in the scenario builder.
    /// </summary>
    public class ScenarioSlot
    {
        public string Name { get; set; }
        public CharacterSheet Sheet { get; set; }
        public Faction Faction { get; set; }
        public bool IsPreset { get; set; }
    }

    /// <summary>
    /// Panel for building custom combat scenarios.
    /// Supports party/enemy management, difficulty selection, and environment choice.
    /// </summary>
    public partial class ScenarioBuilderPanel : HudResizablePanel
    {
        [Signal]
        public delegate void StartCombatRequestedEventHandler();

        private CharacterDataRegistry _characterRegistry;
        private DataRegistry _dataRegistry;

        // Combatant lists
        private readonly List<ScenarioSlot> _partySlots = new();
        private readonly List<ScenarioSlot> _enemySlots = new();

        // UI elements
        private VBoxContainer _partyList;
        private VBoxContainer _enemyList;
        private OptionButton _difficultySelector;
        private OptionButton _environmentSelector;
        private Label _statusLabel;
        private SpinBox _levelSpinBox;

        // State
        private DifficultyMode _difficulty = DifficultyMode.Balanced;
        private EnvironmentPreset _environment = EnvironmentPreset.OpenField;
        private int _defaultLevel = 3;

        public ScenarioBuilderPanel()
        {
            PanelTitle = "SCENARIO BUILDER";
            Resizable = true;
            MinSize = new Vector2(500, 480);
            MaxSize = new Vector2(900, 900);
        }

        /// <summary>
        /// Initialize with data registries.
        /// </summary>
        public void Initialize(CharacterDataRegistry characterRegistry, DataRegistry dataRegistry = null)
        {
            _characterRegistry = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            _dataRegistry = dataRegistry;
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 6);
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(vbox);

            // --- Settings row ---
            var settingsRow = new HBoxContainer();
            settingsRow.AddThemeConstantOverride("separation", 12);
            vbox.AddChild(settingsRow);

            // Difficulty
            var diffLabel = new Label();
            diffLabel.Text = "Difficulty:";
            HudTheme.StyleLabel(diffLabel, HudTheme.FontSmall, HudTheme.Gold);
            settingsRow.AddChild(diffLabel);

            _difficultySelector = new OptionButton();
            _difficultySelector.AddItem("Explorer", (int)DifficultyMode.Explorer);
            _difficultySelector.AddItem("Balanced", (int)DifficultyMode.Balanced);
            _difficultySelector.AddItem("Tactician", (int)DifficultyMode.Tactician);
            _difficultySelector.AddItem("Honour Mode", (int)DifficultyMode.HonourMode);
            _difficultySelector.Selected = 1; // Balanced
            _difficultySelector.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            _difficultySelector.ItemSelected += (index) => _difficulty = (DifficultyMode)index;
            settingsRow.AddChild(_difficultySelector);

            // Environment
            var envLabel = new Label();
            envLabel.Text = "Environment:";
            HudTheme.StyleLabel(envLabel, HudTheme.FontSmall, HudTheme.Gold);
            settingsRow.AddChild(envLabel);

            _environmentSelector = new OptionButton();
            _environmentSelector.AddItem("Open Field", (int)EnvironmentPreset.OpenField);
            _environmentSelector.AddItem("Dungeon", (int)EnvironmentPreset.Dungeon);
            _environmentSelector.AddItem("Forest", (int)EnvironmentPreset.Forest);
            _environmentSelector.AddItem("Castle", (int)EnvironmentPreset.Castle);
            _environmentSelector.AddItem("Cave", (int)EnvironmentPreset.Cave);
            _environmentSelector.Selected = 0;
            _environmentSelector.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            _environmentSelector.ItemSelected += (index) => _environment = (EnvironmentPreset)index;
            settingsRow.AddChild(_environmentSelector);

            // Level
            var levelLabel = new Label();
            levelLabel.Text = "Level:";
            HudTheme.StyleLabel(levelLabel, HudTheme.FontSmall, HudTheme.Gold);
            settingsRow.AddChild(levelLabel);

            _levelSpinBox = new SpinBox();
            _levelSpinBox.MinValue = 1;
            _levelSpinBox.MaxValue = 12;
            _levelSpinBox.Value = _defaultLevel;
            _levelSpinBox.Step = 1;
            _levelSpinBox.CustomMinimumSize = new Vector2(60, 0);
            _levelSpinBox.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            _levelSpinBox.ValueChanged += (val) => _defaultLevel = (int)val;
            settingsRow.AddChild(_levelSpinBox);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            vbox.AddChild(sep);

            // --- Two-column layout: Party | Enemies ---
            var columns = new HBoxContainer();
            columns.AddThemeConstantOverride("separation", 12);
            columns.SizeFlagsVertical = SizeFlags.ExpandFill;
            vbox.AddChild(columns);

            // Party column
            var partyCol = CreateTeamColumn("Party (Player)", HudTheme.PlayerBlue, true);
            columns.AddChild(partyCol);

            // Separator
            var colSep = new VSeparator();
            colSep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            columns.AddChild(colSep);

            // Enemy column
            var enemyCol = CreateTeamColumn("Enemies (Hostile)", HudTheme.EnemyRed, false);
            columns.AddChild(enemyCol);

            // --- Status and Start ---
            _statusLabel = new Label();
            HudTheme.StyleLabel(_statusLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            _statusLabel.HorizontalAlignment = HorizontalAlignment.Center;
            vbox.AddChild(_statusLabel);

            var startButton = new Button();
            startButton.Text = "Start Combat";
            startButton.CustomMinimumSize = new Vector2(160, 36);
            startButton.AddThemeFontSizeOverride("font_size", HudTheme.FontMedium);
            startButton.AddThemeStyleboxOverride("normal",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold, borderWidth: 2));
            startButton.AddThemeStyleboxOverride("hover",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorderBright, borderWidth: 2));
            startButton.AddThemeColorOverride("font_color", HudTheme.Gold);
            startButton.Pressed += OnStartCombat;

            var startContainer = new HBoxContainer();
            startContainer.Alignment = BoxContainer.AlignmentMode.Center;
            startContainer.AddChild(startButton);
            vbox.AddChild(startContainer);

            UpdateStatus();
        }

        private VBoxContainer CreateTeamColumn(string title, Color titleColor, bool isParty)
        {
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 4);
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.SizeFlagsVertical = SizeFlags.ExpandFill;

            var header = new Label();
            header.Text = title;
            HudTheme.StyleLabel(header, HudTheme.FontMedium, titleColor);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(header);

            // Scrollable list
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            col.AddChild(scroll);

            var list = new VBoxContainer();
            list.AddThemeConstantOverride("separation", 2);
            list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(list);

            if (isParty)
                _partyList = list;
            else
                _enemyList = list;

            // Add/Remove buttons
            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 4);
            btnRow.Alignment = BoxContainer.AlignmentMode.Center;
            col.AddChild(btnRow);

            var addRandomBtn = CreateSmallButton("+ Random");
            addRandomBtn.Pressed += () => AddRandomUnit(isParty);
            btnRow.AddChild(addRandomBtn);

            var removeBtn = CreateSmallButton("- Remove");
            removeBtn.Pressed += () => RemoveLastUnit(isParty);
            btnRow.AddChild(removeBtn);

            return col;
        }

        /// <summary>
        /// Add a random unit to the specified team.
        /// </summary>
        public void AddRandomUnit(bool isParty)
        {
            var slots = isParty ? _partySlots : _enemySlots;
            if (slots.Count >= 6) return; // Max 6 per side

            var faction = isParty ? Faction.Player : Faction.Hostile;
            int index = slots.Count + 1;

            // Create a random character using ScenarioGenerator pattern
            var builder = new CharacterBuilder();
            builder.SetName($"{(isParty ? "Hero" : "Foe")} {index}");
            builder.SetLevel(_defaultLevel);

            // Pick a random race & class
            var races = _characterRegistry?.GetAllRaces()?.ToList();
            var classes = _characterRegistry?.GetAllClasses()?.ToList();

            if (races?.Count > 0)
            {
                var race = races[GD.RandRange(0, races.Count - 1)];
                string subraceId = null;
                if (race.Subraces?.Count > 0)
                    subraceId = race.Subraces[GD.RandRange(0, race.Subraces.Count - 1)].Id;
                builder.SetRace(race.Id, subraceId);
            }

            if (classes?.Count > 0)
            {
                var cls = classes[GD.RandRange(0, classes.Count - 1)];
                string subclassId = null;
                if (cls.Subclasses?.Count > 0 && _defaultLevel >= cls.SubclassLevel)
                    subclassId = cls.Subclasses[GD.RandRange(0, cls.Subclasses.Count - 1)].Id;
                builder.SetClass(cls.Id, subclassId);
            }

            // Standard array-ish scores
            builder.SetAbilityScores(15, 14, 13, 12, 10, 8);

            var slot = new ScenarioSlot
            {
                Name = builder.Name,
                Sheet = builder.Build(),
                Faction = faction,
                IsPreset = false
            };

            slots.Add(slot);
            RefreshTeamList(isParty);
            UpdateStatus();
        }

        /// <summary>
        /// Add a custom character sheet to the specified team.
        /// </summary>
        public void AddCharacter(CharacterSheet sheet, bool isParty)
        {
            var slots = isParty ? _partySlots : _enemySlots;
            if (slots.Count >= 6) return;

            var slot = new ScenarioSlot
            {
                Name = sheet.Name,
                Sheet = sheet,
                Faction = isParty ? Faction.Player : Faction.Hostile,
                IsPreset = false
            };

            slots.Add(slot);
            RefreshTeamList(isParty);
            UpdateStatus();
        }

        private void RemoveLastUnit(bool isParty)
        {
            var slots = isParty ? _partySlots : _enemySlots;
            if (slots.Count == 0) return;

            slots.RemoveAt(slots.Count - 1);
            RefreshTeamList(isParty);
            UpdateStatus();
        }

        private void RefreshTeamList(bool isParty)
        {
            var list = isParty ? _partyList : _enemyList;
            var slots = isParty ? _partySlots : _enemySlots;

            if (list == null) return;

            foreach (var child in list.GetChildren())
                child.QueueFree();

            foreach (var slot in slots)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 4);

                var nameLabel = new Label();
                string classInfo = "";
                if (slot.Sheet?.ClassLevels?.Count > 0)
                {
                    var cl = slot.Sheet.ClassLevels[0];
                    classInfo = $" [{cl.ClassId} {slot.Sheet.TotalLevel}]";
                }
                nameLabel.Text = $"{slot.Name}{classInfo}";
                nameLabel.ClipText = true;
                nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
                row.AddChild(nameLabel);

                list.AddChild(row);
            }
        }

        private void UpdateStatus()
        {
            if (_statusLabel == null) return;

            _statusLabel.Text = $"Party: {_partySlots.Count}/6  |  Enemies: {_enemySlots.Count}/6  |  " +
                                $"Difficulty: {_difficulty}  |  Level: {_defaultLevel}";
        }

        private void OnStartCombat()
        {
            if (_partySlots.Count == 0 || _enemySlots.Count == 0)
            {
                _statusLabel.Text = "Need at least 1 unit on each side!";
                return;
            }

            EmitSignal(SignalName.StartCombatRequested);
        }

        /// <summary>
        /// Build a ScenarioDefinition from the current builder state.
        /// </summary>
        public ScenarioDefinition BuildScenario()
        {
            var scenario = new ScenarioDefinition
            {
                Id = $"custom_scenario_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}",
                Name = $"Custom {_partySlots.Count}v{_enemySlots.Count} ({_difficulty})",
                Units = new List<ScenarioUnit>()
            };

            float spacing = 2f;

            for (int i = 0; i < _partySlots.Count; i++)
            {
                var slot = _partySlots[i];
                scenario.Units.Add(CreateScenarioUnit(slot, i, -4f, i * spacing));
            }

            for (int i = 0; i < _enemySlots.Count; i++)
            {
                var slot = _enemySlots[i];
                scenario.Units.Add(CreateScenarioUnit(slot, i, 4f, i * spacing));
            }

            return scenario;
        }

        private ScenarioUnit CreateScenarioUnit(ScenarioSlot slot, int index, float x, float z)
        {
            var sheet = slot.Sheet;
            string factionPrefix = slot.Faction == Faction.Player ? "player" : "enemy";

            var unit = new ScenarioUnit
            {
                Id = $"{factionPrefix}_{index + 1}",
                Name = slot.Name ?? $"{factionPrefix}_{index + 1}",
                Faction = slot.Faction,
                X = x,
                Y = 0f,
                Z = z,
                RaceId = sheet?.RaceId,
                SubraceId = sheet?.SubraceId,
                AbilityBonus2 = sheet?.AbilityBonus2,
                AbilityBonus1 = sheet?.AbilityBonus1,
                FeatIds = sheet?.FeatIds,
                BackgroundId = sheet?.BackgroundId,
                BackgroundSkills = sheet?.BackgroundSkills
            };

            if (sheet != null)
            {
                unit.BaseStrength = sheet.BaseStrength;
                unit.BaseDexterity = sheet.BaseDexterity;
                unit.BaseConstitution = sheet.BaseConstitution;
                unit.BaseIntelligence = sheet.BaseIntelligence;
                unit.BaseWisdom = sheet.BaseWisdom;
                unit.BaseCharisma = sheet.BaseCharisma;

                if (sheet.ClassLevels?.Count > 0)
                {
                    unit.ClassLevels = sheet.ClassLevels
                        .GroupBy(cl => cl.ClassId)
                        .Select(g => new ClassLevelEntry
                        {
                            ClassId = g.Key,
                            SubclassId = g.First().SubclassId,
                            Levels = g.Count()
                        })
                        .ToList();
                }
            }

            return unit;
        }

        /// <summary>Current difficulty mode.</summary>
        public DifficultyMode Difficulty => _difficulty;

        /// <summary>Current environment preset.</summary>
        public EnvironmentPreset Environment => _environment;

        /// <summary>Current default level.</summary>
        public int DefaultLevel => _defaultLevel;

        /// <summary>Party slots (read-only).</summary>
        public IReadOnlyList<ScenarioSlot> PartySlots => _partySlots;

        /// <summary>Enemy slots (read-only).</summary>
        public IReadOnlyList<ScenarioSlot> EnemySlots => _enemySlots;

        private Button CreateSmallButton(string text)
        {
            var btn = new Button();
            btn.Text = text;
            btn.CustomMinimumSize = new Vector2(80, 24);
            btn.AddThemeFontSizeOverride("font_size", HudTheme.FontSmall);
            btn.AddThemeStyleboxOverride("normal",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder));
            btn.AddThemeStyleboxOverride("hover",
                HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.Gold));
            btn.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            return btn;
        }
    }
}
