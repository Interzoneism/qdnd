using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Full character-sheet tab for the unified character/inventory screen.
    /// Scrollable layout: header, HP, ability scores, combat stats, saves,
    /// skills, resistances/immunities/vulnerabilities, features, resources.
    /// </summary>
    public partial class CharacterTab : MarginContainer
    {
        // ── Skill → Ability mapping (standard 5e) ──────────────────
        private static readonly Dictionary<string, string> SkillAbilityMap = new()
        {
            ["Acrobatics"]      = "DEX",
            ["Animal Handling"] = "WIS",
            ["Arcana"]          = "INT",
            ["Athletics"]       = "STR",
            ["Deception"]       = "CHA",
            ["History"]         = "INT",
            ["Insight"]         = "WIS",
            ["Intimidation"]    = "CHA",
            ["Investigation"]   = "INT",
            ["Medicine"]        = "WIS",
            ["Nature"]          = "INT",
            ["Perception"]      = "WIS",
            ["Performance"]     = "CHA",
            ["Persuasion"]      = "CHA",
            ["Religion"]        = "INT",
            ["Sleight of Hand"] = "DEX",
            ["Stealth"]         = "DEX",
            ["Survival"]        = "WIS",
        };

        // ── Save abbreviation list (in display order) ──────────────
        private static readonly (string Abbr, string Full)[] SaveOrder =
        {
            ("STR", "Strength"),
            ("DEX", "Dexterity"),
            ("CON", "Constitution"),
            ("INT", "Intelligence"),
            ("WIS", "Wisdom"),
            ("CHA", "Charisma"),
        };

        private VBoxContainer _content;

        // Cached section containers for updates
        private Label _nameLabel;
        private Label _raceLabel;
        private Label _classLabel;
        private ProgressBar _xpBar;
        private Label _xpLabel;
        private Label _hpLabel;
        private ProgressBar _hpBar;
        private HBoxContainer _abilityRow;
        private GridContainer _combatGrid;
        private VBoxContainer _savesBox;
        private GridContainer _skillsGrid;
        private VBoxContainer _resistancesBox;
        private VBoxContainer _featuresBox;
        private VBoxContainer _resourcesBox;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            _content = new VBoxContainer();
            _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddThemeConstantOverride("separation", 10);
            scroll.AddChild(_content);

            BuildLayout();

            // Dark background matching equipment tab
            AddThemeConstantOverride("margin_left", 8);
            AddThemeConstantOverride("margin_right", 8);
            AddThemeConstantOverride("margin_top", 4);
            AddThemeConstantOverride("margin_bottom", 4);
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>
        /// Populate every section with the supplied character data.
        /// </summary>
        public void SetData(CharacterDisplayData data)
        {
            if (data == null || _content == null) return;

            // Header
            _nameLabel.Text = data.Name ?? "Unknown";
            _raceLabel.Text = data.Race ?? "Unknown";
            _classLabel.Text = data.Level > 0 ? $"Level {data.Level} {data.Class ?? ""}" : (data.Class ?? "");

            float xpPct = data.ExperienceToNextLevel > 0
                ? (float)data.Experience / data.ExperienceToNextLevel * 100f
                : 0f;
            _xpBar.Value = xpPct;
            _xpLabel.Text = $"XP {data.Experience} / {data.ExperienceToNextLevel}";

            // HP
            float hpPct = data.HpMax > 0
                ? (float)data.HpCurrent / data.HpMax * 100f
                : 0f;
            string hpText = data.TempHp > 0
                ? $"HP {data.HpCurrent} / {data.HpMax}  (+{data.TempHp} temp)"
                : $"HP {data.HpCurrent} / {data.HpMax}";
            _hpLabel.Text = hpText;
            _hpBar.Value = hpPct;
            _hpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPct)));

            // Ability scores
            UpdateAbilityScores(data);

            // Combat stats
            UpdateCombatStats(data);

            // Saving throws
            UpdateSavingThrows(data);

            // Skills
            UpdateSkills(data);

            // Resistances / Immunities / Vulnerabilities
            UpdateResistances(data);

            // Features
            UpdateFeatures(data);

            // Resources
            UpdateResources(data);
        }

        // ── Layout construction ────────────────────────────────────

        private void BuildLayout()
        {
            BuildHeader();
            BuildHpSection();
            BuildAbilityScores();
            BuildCombatStats();
            BuildSavingThrows();
            BuildSkills();
            BuildResistances();
            BuildFeatures();
            BuildResources();
        }

        // ── 1. Header ─────────────────────────────────────────────

        private void BuildHeader()
        {
            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 4);
            _content.AddChild(box);

            _nameLabel = new Label();
            _nameLabel.Text = "—";
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_nameLabel, 24, HudTheme.Gold);
            box.AddChild(_nameLabel);

            _raceLabel = new Label();
            _raceLabel.Text = "";
            _raceLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_raceLabel, HudTheme.FontNormal, HudTheme.MutedBeige);
            box.AddChild(_raceLabel);

            _classLabel = new Label();
            _classLabel.Text = "";
            _classLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_classLabel, HudTheme.FontNormal, HudTheme.MutedBeige);
            box.AddChild(_classLabel);

            // XP progress
            _xpBar = CreateProgressBar(HudTheme.GoldMuted);
            _xpBar.CustomMinimumSize = new Vector2(0, 8);
            box.AddChild(_xpBar);

            _xpLabel = new Label();
            _xpLabel.Text = "XP 0 / 0";
            _xpLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(_xpLabel, HudTheme.FontTiny, HudTheme.TextDim);
            box.AddChild(_xpLabel);
        }

        // ── 2. HP Section ──────────────────────────────────────────

        private void BuildHpSection()
        {
            _content.AddChild(new SectionDivider("Hit Points"));

            var box = new VBoxContainer();
            box.AddThemeConstantOverride("separation", 4);
            _content.AddChild(box);

            _hpLabel = new Label();
            _hpLabel.Text = "HP — / —";
            HudTheme.StyleLabel(_hpLabel, HudTheme.FontMedium, HudTheme.WarmWhite);
            box.AddChild(_hpLabel);

            _hpBar = CreateProgressBar(HudTheme.HealthGreen);
            _hpBar.CustomMinimumSize = new Vector2(0, 12);
            box.AddChild(_hpBar);
        }

        // ── 3. Ability Scores ──────────────────────────────────────

        private void BuildAbilityScores()
        {
            _content.AddChild(new SectionDivider("Ability Scores"));

            _abilityRow = new HBoxContainer();
            _abilityRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _abilityRow.AddThemeConstantOverride("separation", 6);
            _abilityRow.Alignment = BoxContainer.AlignmentMode.Center;
            _content.AddChild(_abilityRow);

            // Placeholder boxes will be populated via SetData
            foreach (var (abbr, _) in SaveOrder)
            {
                var box = new AbilityScoreBox(abbr, 10);
                _abilityRow.AddChild(box);
            }
        }

        // ── 4. Combat Stats ────────────────────────────────────────

        private void BuildCombatStats()
        {
            _content.AddChild(new SectionDivider("Combat Stats"));

            _combatGrid = new GridContainer();
            _combatGrid.Columns = 4;
            _combatGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _combatGrid.AddThemeConstantOverride("h_separation", 8);
            _combatGrid.AddThemeConstantOverride("v_separation", 4);
            _content.AddChild(_combatGrid);

            // 4 stat boxes created as placeholders
            AddCombatStatBox("AC", "—");
            AddCombatStatBox("INIT", "—");
            AddCombatStatBox("SPEED", "—");
            AddCombatStatBox("PROF", "—");
        }

        private void AddCombatStatBox(string label, string value)
        {
            var panel = new PanelContainer();
            panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            panel.AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                bgColor: HudTheme.SecondaryDark,
                cornerRadius: HudTheme.CornerRadiusSmall,
                contentMargin: 6));

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            vbox.Alignment = BoxContainer.AlignmentMode.Center;
            panel.AddChild(vbox);

            var labelNode = new Label();
            labelNode.Text = label;
            labelNode.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(labelNode, HudTheme.FontTiny, HudTheme.GoldMuted);
            vbox.AddChild(labelNode);

            var valNode = new Label();
            valNode.Text = value;
            valNode.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(valNode, HudTheme.FontLarge, HudTheme.WarmWhite);
            vbox.AddChild(valNode);

            _combatGrid.AddChild(panel);
        }

        // ── 5. Saving Throws ───────────────────────────────────────

        private void BuildSavingThrows()
        {
            _content.AddChild(new SectionDivider("Saving Throws"));

            _savesBox = new VBoxContainer();
            _savesBox.AddThemeConstantOverride("separation", 2);
            _content.AddChild(_savesBox);
        }

        // ── 6. Skills ──────────────────────────────────────────────

        private void BuildSkills()
        {
            _content.AddChild(new SectionDivider("Skills"));

            _skillsGrid = new GridContainer();
            _skillsGrid.Columns = 2;
            _skillsGrid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _skillsGrid.AddThemeConstantOverride("h_separation", 12);
            _skillsGrid.AddThemeConstantOverride("v_separation", 2);
            _content.AddChild(_skillsGrid);
        }

        // ── 7. Resistances / Immunities / Vulnerabilities ──────────

        private void BuildResistances()
        {
            _content.AddChild(new SectionDivider("Defenses"));

            _resistancesBox = new VBoxContainer();
            _resistancesBox.AddThemeConstantOverride("separation", 6);
            _content.AddChild(_resistancesBox);
        }

        // ── 8. Features & Traits ───────────────────────────────────

        private void BuildFeatures()
        {
            _content.AddChild(new SectionDivider("Features & Traits"));

            _featuresBox = new VBoxContainer();
            _featuresBox.AddThemeConstantOverride("separation", 2);
            _content.AddChild(_featuresBox);
        }

        // ── 9. Resources ───────────────────────────────────────────

        private void BuildResources()
        {
            _content.AddChild(new SectionDivider("Resources"));

            _resourcesBox = new VBoxContainer();
            _resourcesBox.AddThemeConstantOverride("separation", 4);
            _content.AddChild(_resourcesBox);
        }

        // ── Update helpers ─────────────────────────────────────────

        private void UpdateAbilityScores(CharacterDisplayData data)
        {
            int[] scores =
            {
                data.Strength, data.Dexterity, data.Constitution,
                data.Intelligence, data.Wisdom, data.Charisma,
            };

            for (int i = 0; i < _abilityRow.GetChildCount() && i < scores.Length; i++)
            {
                if (_abilityRow.GetChild(i) is AbilityScoreBox box)
                    box.UpdateScore(scores[i]);
            }
        }

        private void UpdateCombatStats(CharacterDisplayData data)
        {
            string[] values =
            {
                data.ArmorClass.ToString(),
                FormatModifier(data.Initiative),
                $"{data.Speed} ft",
                $"+{data.ProficiencyBonus}",
            };

            for (int i = 0; i < _combatGrid.GetChildCount() && i < values.Length; i++)
            {
                if (_combatGrid.GetChild(i) is PanelContainer panel
                    && panel.GetChildOrNull<VBoxContainer>(0) is { } vbox
                    && vbox.GetChildCount() >= 2
                    && vbox.GetChild(1) is Label valLabel)
                {
                    valLabel.Text = values[i];
                }
            }
        }

        private void UpdateSavingThrows(CharacterDisplayData data)
        {
            ClearChildren(_savesBox);

            int[] scores =
            {
                data.Strength, data.Dexterity, data.Constitution,
                data.Intelligence, data.Wisdom, data.Charisma,
            };

            for (int i = 0; i < SaveOrder.Length; i++)
            {
                var (abbr, full) = SaveOrder[i];
                bool proficient = data.ProficientSaves != null && data.ProficientSaves.Contains(abbr);
                int modifier = AbilityModifier(scores[i]);
                if (proficient) modifier += data.ProficiencyBonus;

                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);

                // Proficiency diamond
                var diamond = new Label();
                diamond.Text = proficient ? "●" : "○";
                diamond.CustomMinimumSize = new Vector2(14, 0);
                diamond.HorizontalAlignment = HorizontalAlignment.Center;
                HudTheme.StyleLabel(diamond, HudTheme.FontSmall, proficient ? HudTheme.Gold : HudTheme.TextDim);
                row.AddChild(diamond);

                // Save name
                var nameLabel = new Label();
                nameLabel.Text = full;
                nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
                row.AddChild(nameLabel);

                // Modifier value
                var modLabel = new Label();
                modLabel.Text = FormatModifier(modifier);
                modLabel.HorizontalAlignment = HorizontalAlignment.Right;
                HudTheme.StyleLabel(modLabel, HudTheme.FontSmall, proficient ? HudTheme.Gold : HudTheme.MutedBeige);
                row.AddChild(modLabel);

                _savesBox.AddChild(row);
            }
        }

        private void UpdateSkills(CharacterDisplayData data)
        {
            ClearChildren(_skillsGrid);

            if (data.Skills == null || data.Skills.Count == 0)
            {
                var empty = new Label();
                empty.Text = "No skills";
                HudTheme.StyleLabel(empty, HudTheme.FontSmall, HudTheme.TextDim);
                _skillsGrid.AddChild(empty);
                return;
            }

            // Sort alphabetically
            var sorted = new SortedDictionary<string, int>(data.Skills);
            foreach (var (skillName, bonus) in sorted)
            {
                bool proficient = bonus > 0; // approximate; we use the provided value directly
                var row = new HBoxContainer();
                row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddThemeConstantOverride("separation", 4);

                var dot = new Label();
                dot.Text = proficient ? "●" : "○";
                dot.CustomMinimumSize = new Vector2(12, 0);
                dot.HorizontalAlignment = HorizontalAlignment.Center;
                HudTheme.StyleLabel(dot, HudTheme.FontTiny, proficient ? HudTheme.Gold : HudTheme.TextDim);
                row.AddChild(dot);

                var nameLabel = new Label();
                nameLabel.Text = skillName;
                nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
                row.AddChild(nameLabel);

                var valLabel = new Label();
                valLabel.Text = FormatModifier(bonus);
                valLabel.HorizontalAlignment = HorizontalAlignment.Right;
                HudTheme.StyleLabel(valLabel, HudTheme.FontSmall, proficient ? HudTheme.Gold : HudTheme.MutedBeige);
                row.AddChild(valLabel);

                _skillsGrid.AddChild(row);
            }
        }

        private void UpdateResistances(CharacterDisplayData data)
        {
            ClearChildren(_resistancesBox);

            bool anyContent = false;

            if (data.Resistances != null && data.Resistances.Count > 0)
            {
                AddDefenseSubSection(_resistancesBox, "Resistances", data.Resistances);
                anyContent = true;
            }

            if (data.Immunities != null && data.Immunities.Count > 0)
            {
                AddDefenseSubSection(_resistancesBox, "Immunities", data.Immunities);
                anyContent = true;
            }

            if (data.Vulnerabilities != null && data.Vulnerabilities.Count > 0)
            {
                AddDefenseSubSection(_resistancesBox, "Vulnerabilities", data.Vulnerabilities);
                anyContent = true;
            }

            if (!anyContent)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _resistancesBox.AddChild(none);
            }
        }

        private void AddDefenseSubSection(VBoxContainer parent, string title, List<string> items)
        {
            var header = new Label();
            header.Text = title;
            HudTheme.StyleLabel(header, HudTheme.FontSmall, HudTheme.GoldMuted);
            parent.AddChild(header);

            foreach (string item in items)
            {
                var entry = new Label();
                entry.Text = $"  • {item}";
                HudTheme.StyleLabel(entry, HudTheme.FontSmall, HudTheme.WarmWhite);
                parent.AddChild(entry);
            }
        }

        private static string ResolveFeatureIconPath(string featureName)
        {
            return HudIcons.ResolvePassiveFeatureIcon(null, featureName, null);
        }

        private HBoxContainer CreateFeatureRow(string featureName, int fontSize, Color textColor, string iconPathOverride = null)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;

            string iconPath = !string.IsNullOrWhiteSpace(iconPathOverride)
                ? iconPathOverride
                : ResolveFeatureIconPath(featureName);

            var icon = new TextureRect();
            icon.CustomMinimumSize = new Vector2(24, 24);
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var tex = HudIcons.LoadTextureSafe(iconPath);
            if (tex != null)
                icon.Texture = tex;
            else
                icon.Modulate = new Color(1, 1, 1, 0.3f);
            row.AddChild(icon);

            var label = new Label();
            label.Text = featureName;
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            HudTheme.StyleLabel(label, fontSize, textColor);
            row.AddChild(label);

            return row;
        }

        private void UpdateFeatures(CharacterDisplayData data)
        {
            ClearChildren(_featuresBox);

            if (data.Features == null || data.Features.Count == 0)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _featuresBox.AddChild(none);
                return;
            }

            foreach (string feature in data.Features)
            {
                var row = CreateFeatureRow(feature, HudTheme.FontSmall, HudTheme.WarmWhite);
                _featuresBox.AddChild(row);
            }

            // Notable features (with descriptions)
            if (data.NotableFeatures != null && data.NotableFeatures.Count > 0)
            {
                var spacer = new HSeparator();
                spacer.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
                _featuresBox.AddChild(spacer);

                foreach (var nf in data.NotableFeatures)
                {
                    string iconOverride = !string.IsNullOrWhiteSpace(nf.IconPath) ? nf.IconPath : null;
                    var row = CreateFeatureRow(nf.Name, HudTheme.FontSmall, HudTheme.Gold, iconOverride);
                    _featuresBox.AddChild(row);

                    if (!string.IsNullOrWhiteSpace(nf.Description))
                    {
                        var descLabel = new Label();
                        descLabel.Text = $"    {nf.Description}";
                        descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                        HudTheme.StyleLabel(descLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
                        _featuresBox.AddChild(descLabel);
                    }
                }
            }
        }

        private void UpdateResources(CharacterDisplayData data)
        {
            ClearChildren(_resourcesBox);

            if (data.Resources == null || data.Resources.Count == 0)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _resourcesBox.AddChild(none);
                return;
            }

            foreach (var (resName, (current, max)) in data.Resources)
            {
                var row = new HBoxContainer();
                row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddThemeConstantOverride("separation", 8);

                var nameLabel = new Label();
                nameLabel.Text = resName;
                nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
                row.AddChild(nameLabel);

                var valLabel = new Label();
                valLabel.Text = $"{current} / {max}";
                valLabel.HorizontalAlignment = HorizontalAlignment.Right;
                float pct = max > 0 ? (float)current / max : 0f;
                Color valColor = pct > 0.5f ? HudTheme.Gold : pct > 0.25f ? HudTheme.HealthYellow : HudTheme.HealthRed;
                HudTheme.StyleLabel(valLabel, HudTheme.FontSmall, valColor);
                row.AddChild(valLabel);

                _resourcesBox.AddChild(row);
            }
        }

        // ── Utility ────────────────────────────────────────────────

        private static ProgressBar CreateProgressBar(Color fillColor)
        {
            var bar = new ProgressBar();
            bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            bar.ShowPercentage = false;
            bar.MinValue = 0;
            bar.MaxValue = 100;
            bar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            bar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(fillColor));
            return bar;
        }

        private static int AbilityModifier(int score)
        {
            return (int)Math.Floor((score - 10) / 2.0);
        }

        private static string FormatModifier(int mod)
        {
            return mod >= 0 ? $"+{mod}" : mod.ToString();
        }

        private static void ClearChildren(Node parent)
        {
            for (int i = parent.GetChildCount() - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                parent.RemoveChild(child);
                child.QueueFree();
            }
        }
    }
}
