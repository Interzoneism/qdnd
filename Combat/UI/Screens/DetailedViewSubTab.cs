using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Detailed View sub-tab (list icon) for the character info panel.
    /// Shows granular mechanical details: HP, AC, class/race, attributes,
    /// proficiencies, saving throws, and tags.
    /// </summary>
    public partial class DetailedViewSubTab : MarginContainer
    {
        private static readonly (string Abbr, string Full)[] AbilityOrder =
        {
            ("STR", "Strength"),
            ("DEX", "Dexterity"),
            ("CON", "Constitution"),
            ("INT", "Intelligence"),
            ("WIS", "Wisdom"),
            ("CHA", "Charisma"),
        };

        private VBoxContainer _content;

        // Basic stats
        private Label _hpValue;
        private Label _acValue;
        private Label _classValue;
        private Label _raceValue;
        private Label _backgroundValue;

        // Attributes
        private Label _initiativeValue;
        private Label _speedValue;
        private Label _darkvisionValue;
        private Label _typeValue;
        private Label _sizeValue;
        private Label _weightValue;
        private Label _carryCapValue;

        // Proficiency
        private Label _profBonusValue;
        private VBoxContainer _proficienciesContainer;

        // Saving throws
        private HBoxContainer _savesRow;

        // Tags
        private VBoxContainer _tagsContainer;

        public override void _Ready()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;

            AddThemeConstantOverride("margin_left", 4);
            AddThemeConstantOverride("margin_right", 4);
            AddThemeConstantOverride("margin_top", 4);
            AddThemeConstantOverride("margin_bottom", 4);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            _content = new VBoxContainer();
            _content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_content);

            BuildLayout();
        }

        public void SetData(CharacterDisplayData data)
        {
            if (data == null || _content == null) return;

            // Basic stats
            string hpText = data.TempHp > 0
                ? $"{data.HpCurrent} / {data.HpMax}  (+{data.TempHp} temp)"
                : $"{data.HpCurrent} / {data.HpMax}";
            _hpValue.Text = hpText;
            _acValue.Text = data.ArmorClass.ToString();
            _classValue.Text = data.Level > 0 ? $"Level {data.Level} {data.Class ?? ""}" : (data.Class ?? "");
            _raceValue.Text = data.Race ?? "";
            _backgroundValue.Text = data.Background ?? "\u2014";

            // Attributes
            _initiativeValue.Text = FormatModifier(data.Initiative);
            _speedValue.Text = data.Speed > 0 ? $"{data.Speed / 5 * 1.5:F1}m / {data.Speed}ft" : "\u2014";
            _darkvisionValue.Text = data.DarkvisionRange > 0
                ? $"{data.DarkvisionRange / 5 * 1.5:F0}m / {data.DarkvisionRange}ft"
                : "None";
            _typeValue.Text = data.CreatureType ?? "Humanoid";
            _sizeValue.Text = data.Size ?? "Medium";
            _weightValue.Text = data.CharacterWeight > 0 ? $"{data.CharacterWeight} kg" : "\u2014";
            _carryCapValue.Text = data.CarryingCapacity > 0
                ? data.CarryingCapacity.ToString()
                : $"{data.Strength * 15}";

            _profBonusValue.Text = $"+{data.ProficiencyBonus}";

            // Proficiencies
            RefreshProficiencies(data);

            // Saving throws
            RefreshSavingThrows(data);

            // Tags
            RefreshTags(data.Tags);
        }

        // ── Layout ─────────────────────────────────────────────────

        private void BuildLayout()
        {
            // Basic Statistics
            var statsBox = new VBoxContainer();
            statsBox.AddThemeConstantOverride("separation", 3);
            _content.AddChild(statsBox);

            _hpValue = AddStatRow(statsBox, "Hit Points");
            _acValue = AddStatRow(statsBox, "Armour Class");
            _classValue = AddStatRow(statsBox, "Class");
            _raceValue = AddStatRow(statsBox, "Race");
            _backgroundValue = AddStatRow(statsBox, "Background");

            // Attributes
            _content.AddChild(new SectionDivider("Attributes"));

            var attrsBox = new VBoxContainer();
            attrsBox.AddThemeConstantOverride("separation", 3);
            _content.AddChild(attrsBox);

            _initiativeValue = AddStatRow(attrsBox, "Initiative");
            _speedValue = AddStatRow(attrsBox, "Movement Speed");
            _darkvisionValue = AddStatRow(attrsBox, "Darkvision");
            _typeValue = AddStatRow(attrsBox, "Type");
            _sizeValue = AddStatRow(attrsBox, "Size");
            _weightValue = AddStatRow(attrsBox, "Weight");
            _carryCapValue = AddStatRow(attrsBox, "Carrying Capacity");

            // Proficiency Bonus
            _content.AddChild(new SectionDivider("Proficiency Bonus"));

            var profBonusBox = new VBoxContainer();
            profBonusBox.AddThemeConstantOverride("separation", 3);
            _content.AddChild(profBonusBox);
            _profBonusValue = AddStatRow(profBonusBox, "Bonus");

            _proficienciesContainer = new VBoxContainer();
            _proficienciesContainer.AddThemeConstantOverride("separation", 2);
            _proficienciesContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddChild(_proficienciesContainer);

            // Saving Throw Bonus
            _content.AddChild(new SectionDivider("Saving Throw Bonus"));

            _savesRow = new HBoxContainer();
            _savesRow.AddThemeConstantOverride("separation", 4);
            _savesRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _savesRow.Alignment = BoxContainer.AlignmentMode.Center;
            _content.AddChild(_savesRow);

            // Tags
            _content.AddChild(new SectionDivider("Tags"));

            _tagsContainer = new VBoxContainer();
            _tagsContainer.AddThemeConstantOverride("separation", 4);
            _tagsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _content.AddChild(_tagsContainer);
        }

        private Label AddStatRow(VBoxContainer parent, string key)
        {
            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(row);

            var keyLabel = new Label();
            keyLabel.Text = key;
            keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            HudTheme.StyleLabel(keyLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            row.AddChild(keyLabel);

            var valLabel = new Label();
            valLabel.Text = "\u2014";
            valLabel.HorizontalAlignment = HorizontalAlignment.Right;
            HudTheme.StyleLabel(valLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            row.AddChild(valLabel);

            return valLabel;
        }

        // ── Refresh Helpers ────────────────────────────────────────

        private void RefreshProficiencies(CharacterDisplayData data)
        {
            HudTheme.ClearChildren(_proficienciesContainer);

            AddProficiencyRow("Armor", data.ArmorProficiencies);
            AddProficiencyRow("Simple Weapons",
                FilterProficiencies(data.WeaponProficiencies, "Simple"));
            AddProficiencyRow("Martial Weapons",
                FilterProficiencies(data.WeaponProficiencies, "Martial"));
            AddProficiencyRow("Tools", data.ToolProficiencies);
        }

        private void AddProficiencyRow(string category, List<string> items)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _proficienciesContainer.AddChild(row);

            bool hasProficiency = items != null && items.Count > 0;

            // Count prefix or X mark
            var prefix = new Label();
            if (hasProficiency)
            {
                prefix.Text = $"x{items.Count}";
                HudTheme.StyleLabel(prefix, HudTheme.FontSmall, HudTheme.Gold);
            }
            else
            {
                prefix.Text = "\u2717"; // cross mark
                HudTheme.StyleLabel(prefix, HudTheme.FontSmall, HudTheme.TextDim);
            }
            prefix.CustomMinimumSize = new Vector2(24, 0);
            prefix.HorizontalAlignment = HorizontalAlignment.Center;
            row.AddChild(prefix);

            // Category name
            var nameLabel = new Label();
            nameLabel.Text = category;
            nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall,
                hasProficiency ? HudTheme.WarmWhite : HudTheme.TextDim);
            row.AddChild(nameLabel);
        }

        private static List<string> FilterProficiencies(List<string> all, string keyword)
        {
            if (all == null) return new List<string>();
            var result = new List<string>();
            foreach (var p in all)
            {
                if (p != null && p.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    result.Add(p);
            }
            return result;
        }

        private void RefreshSavingThrows(CharacterDisplayData data)
        {
            HudTheme.ClearChildren(_savesRow);

            int[] scores =
            {
                data.Strength, data.Dexterity, data.Constitution,
                data.Intelligence, data.Wisdom, data.Charisma,
            };

            for (int i = 0; i < AbilityOrder.Length; i++)
            {
                var (abbr, fullName) = AbilityOrder[i];
                bool proficient = data.ProficientSaves != null && data.ProficientSaves.Contains(fullName);

                int mod;
                if (data.SavingThrowModifiers != null && 
                    (data.SavingThrowModifiers.TryGetValue(abbr, out int storedMod) || 
                     data.SavingThrowModifiers.TryGetValue(fullName, out storedMod)))
                {
                    mod = storedMod;
                }
                else
                {
                    mod = AbilityModifier(scores[i]);
                    if (proficient) mod += data.ProficiencyBonus;
                }

                // Hex-styled box
                var hexPanel = new PanelContainer();
                hexPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                hexPanel.AddThemeStyleboxOverride("panel", CreateHexStyle(proficient));
                _savesRow.AddChild(hexPanel);

                var vbox = new VBoxContainer();
                vbox.AddThemeConstantOverride("separation", 0);
                vbox.Alignment = BoxContainer.AlignmentMode.Center;
                hexPanel.AddChild(vbox);

                var abbrLabel = new Label();
                abbrLabel.Text = abbr;
                abbrLabel.HorizontalAlignment = HorizontalAlignment.Center;
                HudTheme.StyleLabel(abbrLabel, HudTheme.FontTiny,
                    proficient ? HudTheme.Gold : HudTheme.GoldMuted);
                vbox.AddChild(abbrLabel);

                var modLabel = new Label();
                modLabel.Text = FormatModifier(mod);
                modLabel.HorizontalAlignment = HorizontalAlignment.Center;
                HudTheme.StyleLabel(modLabel, HudTheme.FontSmall,
                    proficient ? HudTheme.Gold : HudTheme.WarmWhite);
                vbox.AddChild(modLabel);
            }
        }

        private static StyleBoxFlat CreateHexStyle(bool proficient)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(0.04f, 0.035f, 0.06f, 0.95f);
            sb.SetCornerRadiusAll(4);
            sb.SetBorderWidthAll(proficient ? 2 : 1);
            sb.BorderColor = proficient ? HudTheme.Gold : HudTheme.PanelBorderSubtle;
            sb.ContentMarginLeft = 2;
            sb.ContentMarginRight = 2;
            sb.ContentMarginTop = 4;
            sb.ContentMarginBottom = 4;
            return sb;
        }

        private void RefreshTags(List<string> tags)
        {
            HudTheme.ClearChildren(_tagsContainer);

            if (tags == null || tags.Count == 0)
            {
                var none = new Label();
                none.Text = "None";
                HudTheme.StyleLabel(none, HudTheme.FontSmall, HudTheme.TextDim);
                _tagsContainer.AddChild(none);
                return;
            }

            // Render tags in a horizontal row with pill-style backgrounds
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            _tagsContainer.AddChild(row);

            foreach (var tag in tags)
            {
                var tagPanel = new PanelContainer();
                tagPanel.AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                    bgColor: new Color(HudTheme.SecondaryDark.R, HudTheme.SecondaryDark.G, HudTheme.SecondaryDark.B, 0.8f),
                    borderColor: HudTheme.PanelBorderSubtle,
                    cornerRadius: 3, borderWidth: 1, contentMargin: 4));

                var tagLabel = new Label();
                tagLabel.Text = tag;
                HudTheme.StyleLabel(tagLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
                tagPanel.AddChild(tagLabel);

                row.AddChild(tagPanel);
            }
        }

        // ── Utility ────────────────────────────────────────────────

        private static int AbilityModifier(int score)
        {
            return (int)Math.Floor((score - 10) / 2.0);
        }

        private static string FormatModifier(int mod)
        {
            return mod >= 0 ? $"+{mod}" : mod.ToString();
        }

    }
}
