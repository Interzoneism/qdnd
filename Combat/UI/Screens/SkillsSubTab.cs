using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Combat.UI.Controls;

namespace QDND.Combat.UI.Screens
{
    /// <summary>
    /// Skills sub-tab (head/profile icon) for the character info panel.
    /// Shows skills grouped by governing ability with proficiency indicators.
    /// </summary>
    public partial class SkillsSubTab : MarginContainer
    {
        // Standard 5e skill -> ability mapping, ordered by ability then alphabetically
        private static readonly (string Abbr, string FullAbility, string[] Skills)[] AbilitySkillGroups =
        {
            ("STR", "Strength", new[] { "Athletics" }),
            ("DEX", "Dexterity", new[] { "Acrobatics", "Sleight of Hand", "Stealth" }),
            ("INT", "Intelligence", new[] { "Arcana", "History", "Investigation", "Nature", "Religion" }),
            ("WIS", "Wisdom", new[] { "Animal Handling", "Insight", "Medicine", "Perception", "Survival" }),
            ("CHA", "Charisma", new[] { "Deception", "Intimidation", "Performance", "Persuasion" }),
        };

        private VBoxContainer _content;

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
            _content.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_content);
        }

        public void SetData(CharacterDisplayData data)
        {
            if (data == null || _content == null) return;

            HudTheme.ClearChildren(_content);

            var proficientSkills = data.ProficientSkills ?? new List<string>();
            var expertiseSkills = data.ExpertiseSkills ?? new List<string>();
            var skills = data.Skills ?? new Dictionary<string, int>();

            // Map abbreviation to score for lookup
            var scoreMap = new Dictionary<string, int>
            {
                ["STR"] = data.Strength,
                ["DEX"] = data.Dexterity,
                ["CON"] = data.Constitution,
                ["INT"] = data.Intelligence,
                ["WIS"] = data.Wisdom,
                ["CHA"] = data.Charisma,
            };

            foreach (var (abbr, fullAbility, skillNames) in AbilitySkillGroups)
            {
                // Skip groups with no skills (Constitution is omitted from the static array)
                if (skillNames.Length == 0) continue;

                int abilityScore = scoreMap.TryGetValue(abbr, out int s) ? s : 10;
                int abilityMod = AbilityModifier(abilityScore);

                // Ability header with modifier
                _content.AddChild(new SectionDivider($"{fullAbility} ({FormatModifier(abilityMod)})"));

                // Skills list
                foreach (var skillName in skillNames)
                {
                    bool isProficient = proficientSkills.Contains(skillName);
                    bool isExpertise = expertiseSkills.Contains(skillName);
                    int bonus = skills.TryGetValue(skillName, out int val) ? val : abilityMod;

                    var row = new HBoxContainer();
                    row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    row.AddThemeConstantOverride("separation", 4);
                    _content.AddChild(row);

                    // Proficiency marker
                    var marker = new Label();
                    if (isExpertise)
                    {
                        marker.Text = "\u25d0"; // half-filled circle for expertise
                        HudTheme.StyleLabel(marker, HudTheme.FontSmall, HudTheme.Gold);
                    }
                    else if (isProficient)
                    {
                        marker.Text = "\u25cf"; // solid circle
                        HudTheme.StyleLabel(marker, HudTheme.FontSmall, HudTheme.Gold);
                    }
                    else
                    {
                        marker.Text = " ";
                        HudTheme.StyleLabel(marker, HudTheme.FontSmall, HudTheme.TextDim);
                    }
                    marker.CustomMinimumSize = new Vector2(14, 0);
                    marker.HorizontalAlignment = HorizontalAlignment.Center;
                    row.AddChild(marker);

                    // Skill name
                    var nameLabel = new Label();
                    nameLabel.Text = skillName;
                    nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                    HudTheme.StyleLabel(nameLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
                    row.AddChild(nameLabel);

                    // Total bonus (right aligned)
                    var bonusLabel = new Label();
                    bonusLabel.Text = FormatModifier(bonus);
                    bonusLabel.HorizontalAlignment = HorizontalAlignment.Right;
                    bonusLabel.CustomMinimumSize = new Vector2(24, 0);
                    Color bonusColor = (isProficient || isExpertise) ? HudTheme.Gold : HudTheme.MutedBeige;
                    HudTheme.StyleLabel(bonusLabel, HudTheme.FontSmall, bonusColor);
                    row.AddChild(bonusLabel);
                }
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
