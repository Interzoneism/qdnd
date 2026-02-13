using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Overlays
{
    /// <summary>
    /// Data for character sheet display.
    /// </summary>
    public class CharacterSheetData
    {
        public string Name { get; set; }
        public string Race { get; set; }
        public string Class { get; set; }
        public int Level { get; set; }
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public int TempHp { get; set; }

        // Ability scores
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        // Combat stats
        public int ArmorClass { get; set; }
        public int Initiative { get; set; }
        public int Speed { get; set; }
        public int ProficiencyBonus { get; set; }

        // Saving throws (proficient)
        public List<string> ProficientSaves { get; set; } = new();

        // Skills
        public Dictionary<string, int> Skills { get; set; } = new();

        // Features
        public List<string> Features { get; set; } = new();

        // Resources
        public Dictionary<string, (int current, int max)> Resources { get; set; } = new();
    }

    /// <summary>
    /// Character detail sheet modal.
    /// </summary>
    public partial class CharacterSheetModal : HudResizablePanel
    {
        private ScrollContainer _scrollContainer;
        private VBoxContainer _sheetContentContainer;

        public CharacterSheetModal()
        {
            PanelTitle = "CHARACTER SHEET";
            Resizable = true;
            MinSize = new Vector2(360, 300);
            MaxSize = new Vector2(600, 900);
        }

        protected override void BuildContent(Control parent)
        {
            _scrollContainer = new ScrollContainer();
            _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scrollContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(_scrollContainer);

            _sheetContentContainer = new VBoxContainer();
            _sheetContentContainer.AddThemeConstantOverride("separation", 8);
            _scrollContainer.AddChild(_sheetContentContainer);
        }

        /// <summary>
        /// Set the character data.
        /// </summary>
        public void SetCombatant(CharacterSheetData data)
        {
            // Clear existing content
            foreach (var child in _sheetContentContainer.GetChildren())
            {
                child.QueueFree();
            }

            // Header
            BuildHeader(data);

            // HP
            BuildHpSection(data);

            // Ability scores
            BuildAbilityScores(data);

            // Combat stats
            BuildCombatStats(data);

            // Saving throws
            BuildSavingThrows(data);

            // Skills
            BuildSkills(data);

            // Features
            BuildFeatures(data);

            // Resources
            BuildResources(data);

            Visible = true;
            CallDeferred(nameof(CenterOnScreen));
        }

        /// <summary>
        /// Clear the sheet.
        /// </summary>
        public void Clear()
        {
            foreach (var child in _sheetContentContainer.GetChildren())
            {
                child.QueueFree();
            }
            Visible = false;
        }

        private void BuildHeader(CharacterSheetData data)
        {
            var nameLabel = new Label();
            nameLabel.Text = data.Name;
            HudTheme.StyleHeader(nameLabel, HudTheme.FontTitle);
            _sheetContentContainer.AddChild(nameLabel);

            var raceClassLabel = new Label();
            raceClassLabel.Text = $"{data.Race} {data.Class} {data.Level}";
            HudTheme.StyleLabel(raceClassLabel, HudTheme.FontMedium, HudTheme.MutedBeige);
            _sheetContentContainer.AddChild(raceClassLabel);

            AddSeparator();
        }

        private void BuildHpSection(CharacterSheetData data)
        {
            var header = CreateSectionHeader("HIT POINTS");
            _sheetContentContainer.AddChild(header);

            var hpLabel = new Label();
            hpLabel.Text = $"{data.HpCurrent} / {data.HpMax}";
            if (data.TempHp > 0)
                hpLabel.Text += $" (+{data.TempHp} temp)";
            HudTheme.StyleLabel(hpLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            _sheetContentContainer.AddChild(hpLabel);

            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 12);
            hpBar.ShowPercentage = false;
            hpBar.MaxValue = data.HpMax;
            hpBar.Value = data.HpCurrent;
            hpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            float hpPercent = data.HpMax > 0 ? (float)((data.HpCurrent / (double)data.HpMax) * 100.0) : 0f;
            hpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(
                HudTheme.GetHealthColor(hpPercent)));
            _sheetContentContainer.AddChild(hpBar);

            AddSeparator();
        }

        private void BuildAbilityScores(CharacterSheetData data)
        {
            var header = CreateSectionHeader("ABILITY SCORES");
            _sheetContentContainer.AddChild(header);

            var grid = new GridContainer();
            grid.Columns = 2;
            grid.AddThemeConstantOverride("h_separation", 12);
            grid.AddThemeConstantOverride("v_separation", 4);
            _sheetContentContainer.AddChild(grid);

            AddAbilityScore(grid, "STR", data.Strength);
            AddAbilityScore(grid, "DEX", data.Dexterity);
            AddAbilityScore(grid, "CON", data.Constitution);
            AddAbilityScore(grid, "INT", data.Intelligence);
            AddAbilityScore(grid, "WIS", data.Wisdom);
            AddAbilityScore(grid, "CHA", data.Charisma);

            AddSeparator();
        }

        private void AddAbilityScore(GridContainer grid, string name, int score)
        {
            int modifier = (score - 10) / 2;
            string modStr = modifier >= 0 ? $"+{modifier}" : modifier.ToString();

            var label = new Label();
            label.Text = $"{name}:";
            HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
            grid.AddChild(label);

            var value = new Label();
            value.Text = $"{score} ({modStr})";
            HudTheme.StyleLabel(value, HudTheme.FontSmall, HudTheme.WarmWhite);
            grid.AddChild(value);
        }

        private void BuildCombatStats(CharacterSheetData data)
        {
            var header = CreateSectionHeader("COMBAT STATS");
            _sheetContentContainer.AddChild(header);

            var grid = new GridContainer();
            grid.Columns = 2;
            grid.AddThemeConstantOverride("h_separation", 12);
            grid.AddThemeConstantOverride("v_separation", 4);
            _sheetContentContainer.AddChild(grid);

            AddStatRow(grid, "AC", data.ArmorClass.ToString());
            AddStatRow(grid, "Initiative", $"+{data.Initiative}");
            AddStatRow(grid, "Speed", $"{data.Speed} ft");
            AddStatRow(grid, "Prof Bonus", $"+{data.ProficiencyBonus}");

            AddSeparator();
        }

        private void BuildSavingThrows(CharacterSheetData data)
        {
            var header = CreateSectionHeader("SAVING THROWS");
            _sheetContentContainer.AddChild(header);

            var savesLabel = new Label();
            savesLabel.Text = data.ProficientSaves.Count > 0
                ? "Proficient: " + string.Join(", ", data.ProficientSaves)
                : "None";
            HudTheme.StyleLabel(savesLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            _sheetContentContainer.AddChild(savesLabel);

            AddSeparator();
        }

        private void BuildSkills(CharacterSheetData data)
        {
            if (data.Skills.Count == 0) return;

            var header = CreateSectionHeader("SKILLS");
            _sheetContentContainer.AddChild(header);

            foreach (var skill in data.Skills)
            {
                var label = new Label();
                label.Text = $"{skill.Key}: +{skill.Value}";
                HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
                _sheetContentContainer.AddChild(label);
            }

            AddSeparator();
        }

        private void BuildFeatures(CharacterSheetData data)
        {
            if (data.Features.Count == 0) return;

            var header = CreateSectionHeader("FEATURES");
            _sheetContentContainer.AddChild(header);

            foreach (var feature in data.Features)
            {
                var label = new Label();
                label.Text = $"• {feature}";
                HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
                _sheetContentContainer.AddChild(label);
            }

            AddSeparator();
        }

        private void BuildResources(CharacterSheetData data)
        {
            if (data.Resources.Count == 0) return;

            var header = CreateSectionHeader("RESOURCES");
            _sheetContentContainer.AddChild(header);

            foreach (var resource in data.Resources)
            {
                var label = new Label();
                label.Text = $"{resource.Key}: {resource.Value.current}/{resource.Value.max}";
                HudTheme.StyleLabel(label, HudTheme.FontSmall, HudTheme.MutedBeige);
                _sheetContentContainer.AddChild(label);
            }
        }

        private Label CreateSectionHeader(string text)
        {
            var label = new Label();
            label.Text = text;
            HudTheme.StyleHeader(label, HudTheme.FontMedium);
            return label;
        }

        private void AddStatRow(GridContainer grid, string label, string value)
        {
            var labelControl = new Label();
            labelControl.Text = label + ":";
            HudTheme.StyleLabel(labelControl, HudTheme.FontSmall, HudTheme.MutedBeige);
            grid.AddChild(labelControl);

            var valueControl = new Label();
            valueControl.Text = value;
            HudTheme.StyleLabel(valueControl, HudTheme.FontSmall, HudTheme.WarmWhite);
            grid.AddChild(valueControl);
        }

        private void AddSeparator()
        {
            var separator = new PanelContainer();
            separator.CustomMinimumSize = new Vector2(0, 1);
            separator.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            _contentContainer.AddChild(separator);
        }

        private void CenterOnScreen()
        {
            var viewport = GetViewportRect();
            var size = Size;
            GlobalPosition = new Vector2(
                (viewport.Size.X - size.X) / 2,
                (viewport.Size.Y - size.Y) / 2
            );
        }
    }
}
