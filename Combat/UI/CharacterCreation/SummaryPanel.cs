using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;
using QDND.Data.CharacterModel;

namespace QDND.Combat.UI.CharacterCreation
{
    /// <summary>
    /// Summary panel showing the final character build before confirmation.
    /// Displays Name, Race, Class, Level, Ability Scores (with mods), HP, AC, Speed,
    /// Features, Proficiencies, and Known Spells/Abilities.
    /// </summary>
    public partial class SummaryPanel : VBoxContainer
    {
        private RichTextLabel _summaryBody;
        private Label _errorLabel;

        public override void _Ready()
        {
            BuildLayout();
        }

        private void BuildLayout()
        {
            AddThemeConstantOverride("separation", 4);

            var header = new Label();
            header.Text = "Character Summary";
            HudTheme.StyleHeader(header, HudTheme.FontLarge);
            header.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(header);

            var sep = new HSeparator();
            sep.AddThemeStyleboxOverride("separator", HudTheme.CreateSeparatorStyle());
            AddChild(sep);

            // Error display (hidden by default)
            _errorLabel = new Label();
            _errorLabel.Visible = false;
            _errorLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            HudTheme.StyleLabel(_errorLabel, HudTheme.FontSmall, HudTheme.EnemyRed);
            AddChild(_errorLabel);

            // Scrollable summary text
            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            _summaryBody = new RichTextLabel();
            _summaryBody.BbcodeEnabled = true;
            _summaryBody.FitContent = true;
            _summaryBody.ScrollActive = false;
            _summaryBody.SelectionEnabled = true;
            _summaryBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _summaryBody.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _summaryBody.SizeFlagsVertical = SizeFlags.ExpandFill;
            _summaryBody.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontSmall);
            _summaryBody.AddThemeColorOverride("default_color", HudTheme.WarmWhite);
            scroll.AddChild(_summaryBody);
        }

        /// <summary>
        /// Refresh the summary with current builder state, including resolved stats.
        /// </summary>
        public void Refresh(CharacterDataRegistry registry, CharacterBuilder builder)
        {
            if (builder == null || registry == null) return;

            _errorLabel.Visible = false;
            _summaryBody.Clear();

            string gold = HudTheme.Gold.ToHtml(false);
            string green = HudTheme.ActionGreen.ToHtml(false);
            string dim = HudTheme.TextDim.ToHtml(false);

            // Try to resolve the character
            ResolvedCharacter resolved = null;
            try
            {
                resolved = builder.BuildAndResolve(registry);
            }
            catch (Exception)
            {
                // If resolution fails, show what we have
            }

            string text = "";

            // --- Name ---
            var nameLabel = builder.Name ?? "Unnamed";
            text += $"[color=#{gold}][b]{nameLabel}[/b][/color]\n\n";

            // --- Race ---
            var race = registry.GetRace(builder.RaceId);
            string raceName = race?.Name ?? builder.RaceId ?? "None";
            string subraceName = "";
            if (!string.IsNullOrEmpty(builder.SubraceId) && race?.Subraces != null)
            {
                var sub = race.Subraces.FirstOrDefault(s => s.Id == builder.SubraceId);
                subraceName = sub?.Name ?? builder.SubraceId;
            }
            text += $"[color=#{gold}]Race:[/color] {raceName}";
            if (!string.IsNullOrEmpty(subraceName))
                text += $" ({subraceName})";
            text += "\n";

            // --- Class ---
            var classDef = registry.GetClass(builder.ClassId);
            string className = classDef?.Name ?? builder.ClassId ?? "None";
            string subclassName = "";
            if (!string.IsNullOrEmpty(builder.SubclassId) && classDef?.Subclasses != null)
            {
                var sub = classDef.Subclasses.FirstOrDefault(s => s.Id == builder.SubclassId);
                subclassName = sub?.Name ?? builder.SubclassId;
            }
            text += $"[color=#{gold}]Class:[/color] {className} {builder.Level}";
            if (!string.IsNullOrEmpty(subclassName))
                text += $" ({subclassName})";
            text += "\n\n";

            // --- Ability Scores ---
            text += $"[color=#{gold}]Ability Scores:[/color]\n";
            string[] abilityNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
            int[] baseScores = { builder.Strength, builder.Dexterity, builder.Constitution, builder.Intelligence, builder.Wisdom, builder.Charisma };

            for (int i = 0; i < abilityNames.Length; i++)
            {
                int baseVal = baseScores[i];
                int racialBonus = 0;
                if (abilityNames[i].Equals(builder.AbilityBonus2, StringComparison.OrdinalIgnoreCase))
                    racialBonus = 2;
                else if (abilityNames[i].Equals(builder.AbilityBonus1, StringComparison.OrdinalIgnoreCase))
                    racialBonus = 1;

                int total = baseVal + racialBonus;
                if (resolved != null)
                {
                    var abilityType = Enum.Parse<AbilityType>(abilityNames[i]);
                    total = resolved.AbilityScores[abilityType];
                }

                int mod = CharacterSheet.GetModifier(total);
                string modStr = mod >= 0 ? $"+{mod}" : mod.ToString();
                string bonusStr = racialBonus > 0 ? $" [color=#{green}](+{racialBonus})[/color]" : "";
                text += $"  {abilityNames[i]}: {total} ({modStr}){bonusStr}\n";
            }

            // --- Combat Stats ---
            text += "\n";
            if (resolved != null)
            {
                text += $"[color=#{gold}]HP:[/color] {resolved.MaxHP}\n";
                text += $"[color=#{gold}]AC:[/color] {resolved.BaseAC}\n";
                text += $"[color=#{gold}]Speed:[/color] {resolved.Speed}ft\n";

                if (resolved.DarkvisionRange > 0)
                    text += $"[color=#{gold}]Darkvision:[/color] {resolved.DarkvisionRange}m\n";

                // Proficiency Bonus
                var sheet = resolved.Sheet;
                text += $"[color=#{gold}]Proficiency Bonus:[/color] +{sheet.ProficiencyBonus}\n";

                // Extra attacks
                if (resolved.ExtraAttacks > 0)
                    text += $"[color=#{gold}]Extra Attacks:[/color] {resolved.ExtraAttacks}\n";
            }

            // --- Features ---
            if (resolved?.Features?.Count > 0)
            {
                text += $"\n[color=#{gold}]Features:[/color]\n";
                var uniqueFeatures = resolved.Features.GroupBy(f => f.Name ?? f.Id).Select(g => g.First());
                foreach (var feat in uniqueFeatures)
                {
                    text += $"  • {feat.Name ?? feat.Id}";
                    if (!string.IsNullOrEmpty(feat.Source))
                        text += $" [color=#{dim}]({feat.Source})[/color]";
                    text += "\n";
                }
            }

            // --- Proficiencies ---
            if (resolved?.Proficiencies != null)
            {
                var profs = resolved.Proficiencies;
                text += $"\n[color=#{gold}]Proficiencies:[/color]\n";

                if (profs.SavingThrows.Count > 0)
                    text += $"  Saves: {string.Join(", ", profs.SavingThrows)}\n";
                if (profs.Skills.Count > 0)
                    text += $"  Skills: {string.Join(", ", profs.Skills)}\n";
                if (profs.Expertise.Count > 0)
                    text += $"  Expertise: {string.Join(", ", profs.Expertise)}\n";
                if (profs.ArmorCategories.Count > 0)
                    text += $"  Armor: {string.Join(", ", profs.ArmorCategories)}\n";
                if (profs.WeaponCategories.Count > 0)
                    text += $"  Weapons: {string.Join(", ", profs.WeaponCategories)}\n";
            }

            // --- Resistances ---
            if (resolved?.DamageResistances?.Count > 0)
                text += $"\n[color=#{gold}]Resistances:[/color] {string.Join(", ", resolved.DamageResistances)}\n";
            if (resolved?.DamageImmunities?.Count > 0)
                text += $"[color=#{gold}]Immunities:[/color] {string.Join(", ", resolved.DamageImmunities)}\n";

            // --- Known Abilities ---
            if (resolved?.AllAbilities?.Count > 0)
            {
                text += $"\n[color=#{gold}]Known Abilities:[/color]\n";
                foreach (var ability in resolved.AllAbilities.Take(20))
                {
                    text += $"  • {ability}\n";
                }
                if (resolved.AllAbilities.Count > 20)
                    text += $"  [color=#{dim}]... and {resolved.AllAbilities.Count - 20} more[/color]\n";
            }

            // --- Resources ---
            if (resolved?.Resources?.Count > 0)
            {
                text += $"\n[color=#{gold}]Resources:[/color]\n";
                foreach (var res in resolved.Resources)
                {
                    text += $"  {res.Key}: {res.Value}\n";
                }
            }

            // --- Feats ---
            if (builder.FeatIds?.Count > 0)
            {
                text += $"\n[color=#{gold}]Feats:[/color]\n";
                foreach (var featId in builder.FeatIds)
                {
                    var feat = registry.GetFeat(featId);
                    text += $"  • {feat?.Name ?? featId}\n";
                }
            }

            _summaryBody.AppendText(text);
        }

        /// <summary>
        /// Show validation errors on the summary panel.
        /// </summary>
        public void ShowErrors(List<string> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                _errorLabel.Visible = false;
                return;
            }

            _errorLabel.Visible = true;
            _errorLabel.Text = "Validation Errors:\n" + string.Join("\n", errors.Select(e => $"• {e}"));
        }
    }
}
