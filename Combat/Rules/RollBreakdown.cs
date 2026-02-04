using System.Collections.Generic;
using System.Text;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Categories for roll breakdown entries.
    /// </summary>
    public enum BreakdownCategory
    {
        /// <summary>Base value from ability score or proficiency.</summary>
        Base,
        /// <summary>Ability score modifier (STR, DEX, etc).</summary>
        Ability,
        /// <summary>Proficiency bonus.</summary>
        Proficiency,
        /// <summary>Equipment bonus (weapons, armor, items).</summary>
        Equipment,
        /// <summary>Situational modifiers (cover, height, flanking).</summary>
        Situational,
        /// <summary>Status effect modifiers (bless, bane, etc).</summary>
        Status,
        /// <summary>Advantage/disadvantage indicator.</summary>
        Advantage
    }

    /// <summary>
    /// A single entry in a roll breakdown for tooltip display.
    /// </summary>
    public class BreakdownEntry
    {
        /// <summary>Source of the modifier (e.g., "High Ground", "STR", "Bless").</summary>
        public string Source { get; set; }

        /// <summary>Numeric value of the modifier (+2, -1, etc).</summary>
        public int Value { get; set; }

        /// <summary>Category of the modifier for UI grouping/styling.</summary>
        public BreakdownCategory Category { get; set; }

        public BreakdownEntry() { }

        public BreakdownEntry(string source, int value, BreakdownCategory category)
        {
            Source = source;
            Value = value;
            Category = category;
        }

        /// <summary>
        /// Format as signed string (e.g., "+2 (High Ground)").
        /// </summary>
        public override string ToString()
        {
            var sign = Value >= 0 ? "+" : "";
            return $"{sign}{Value} ({Source})";
        }
    }

    /// <summary>
    /// Structured breakdown of a d20 roll for UI tooltips.
    /// Provides all components that contributed to a roll result.
    /// </summary>
    public class RollBreakdown
    {
        /// <summary>The natural d20 roll value.</summary>
        public int NaturalRoll { get; set; }

        /// <summary>The final total after all modifiers.</summary>
        public int Total { get; set; }

        /// <summary>All modifiers that contributed to the roll.</summary>
        public List<BreakdownEntry> Modifiers { get; set; } = new();

        /// <summary>Whether advantage was applied.</summary>
        public bool HasAdvantage { get; set; }

        /// <summary>Whether disadvantage was applied.</summary>
        public bool HasDisadvantage { get; set; }

        /// <summary>Both roll values if advantage/disadvantage was applied (used, discarded).</summary>
        public (int Used, int Discarded)? AdvantageRolls { get; set; }

        /// <summary>Sources that granted advantage (for tooltip debugging).</summary>
        public List<string> AdvantageSources { get; set; } = new();

        /// <summary>Sources that granted disadvantage (for tooltip debugging).</summary>
        public List<string> DisadvantageSources { get; set; } = new();

        /// <summary>Whether this was a natural 20.</summary>
        public bool IsCritical => NaturalRoll == 20;

        /// <summary>Whether this was a natural 1.</summary>
        public bool IsCriticalFailure => NaturalRoll == 1;

        /// <summary>
        /// Add a modifier to the breakdown.
        /// </summary>
        public void AddModifier(string source, int value, BreakdownCategory category)
        {
            Modifiers.Add(new BreakdownEntry(source, value, category));
        }

        /// <summary>
        /// Format the breakdown as a human-readable string.
        /// Example: "d20(15) +3 (STR) +2 (Proficiency) +2 (High Ground) = 22"
        /// </summary>
        public string ToFormattedString()
        {
            var sb = new StringBuilder();

            // Die roll
            if (HasAdvantage && AdvantageRolls.HasValue)
            {
                sb.Append($"d20({AdvantageRolls.Value.Used}|{AdvantageRolls.Value.Discarded}) [ADV]");
            }
            else if (HasDisadvantage && AdvantageRolls.HasValue)
            {
                sb.Append($"d20({AdvantageRolls.Value.Used}|{AdvantageRolls.Value.Discarded}) [DIS]");
            }
            else
            {
                sb.Append($"d20({NaturalRoll})");
            }

            // Modifiers
            foreach (var mod in Modifiers)
            {
                sb.Append(' ');
                sb.Append(mod.ToString());
            }

            // Total
            sb.Append($" = {Total}");

            // Critical annotation
            if (IsCritical)
                sb.Append(" [CRIT]");
            else if (IsCriticalFailure)
                sb.Append(" [CRIT FAIL]");

            return sb.ToString();
        }

        /// <summary>
        /// Get modifiers filtered by category.
        /// </summary>
        public List<BreakdownEntry> GetModifiersByCategory(BreakdownCategory category)
        {
            return Modifiers.FindAll(m => m.Category == category);
        }

        /// <summary>
        /// Get total value of modifiers in a specific category.
        /// </summary>
        public int GetCategoryTotal(BreakdownCategory category)
        {
            int total = 0;
            foreach (var mod in Modifiers)
            {
                if (mod.Category == category)
                    total += mod.Value;
            }
            return total;
        }

        /// <summary>
        /// Get sum of all modifiers.
        /// </summary>
        public int GetTotalModifier()
        {
            int total = 0;
            foreach (var mod in Modifiers)
            {
                total += mod.Value;
            }
            return total;
        }

        /// <summary>
        /// Create a breakdown from a QueryResult.
        /// </summary>
        public static RollBreakdown FromQueryResult(QueryResult result)
        {
            var breakdown = new RollBreakdown
            {
                NaturalRoll = result.NaturalRoll,
                Total = (int)result.FinalValue,
                HasAdvantage = result.AdvantageState > 0,
                HasDisadvantage = result.AdvantageState < 0
            };

            // Set advantage rolls if available
            if (result.RollValues != null && result.RollValues.Length == 2)
            {
                int used = result.NaturalRoll;
                int discarded = result.RollValues[0] == used ? result.RollValues[1] : result.RollValues[0];
                breakdown.AdvantageRolls = (used, discarded);
            }

            // Convert applied modifiers
            foreach (var mod in result.AppliedModifiers)
            {
                var category = CategorizeModifier(mod);
                breakdown.AddModifier(mod.Source ?? mod.Name, (int)mod.Value, category);
            }

            return breakdown;
        }

        /// <summary>
        /// Categorize a Modifier based on its properties.
        /// </summary>
        private static BreakdownCategory CategorizeModifier(Modifier mod)
        {
            // Check source/name for category hints
            var source = (mod.Source ?? mod.Name ?? "").ToLowerInvariant();

            if (source.Contains("proficiency"))
                return BreakdownCategory.Proficiency;

            if (source.Contains("strength") || source.Contains("dexterity") ||
                source.Contains("constitution") || source.Contains("intelligence") ||
                source.Contains("wisdom") || source.Contains("charisma") ||
                source.Contains("str") || source.Contains("dex") ||
                source.Contains("con") || source.Contains("int") ||
                source.Contains("wis") || source.Contains("cha"))
                return BreakdownCategory.Ability;

            if (source.Contains("weapon") || source.Contains("armor") ||
                source.Contains("shield") || source.Contains("equipment") ||
                source.Contains("magic item") || source.Contains("ring") ||
                source.Contains("amulet") || source.Contains("sword") ||
                source.Contains("staff") || source.Contains("wand"))
                return BreakdownCategory.Equipment;

            if (source.Contains("cover") || source.Contains("high ground") ||
                source.Contains("low ground") || source.Contains("height") ||
                source.Contains("flanking") || source.Contains("prone") ||
                source.Contains("invisible") || source.Contains("darkness") ||
                source.Contains("obscured") || source.Contains("restrained"))
                return BreakdownCategory.Situational;

            if (source.Contains("bless") || source.Contains("bane") ||
                source.Contains("curse") || source.Contains("buff") ||
                source.Contains("debuff") || source.Contains("status") ||
                source.Contains("poisoned") || source.Contains("frightened") ||
                source.Contains("charmed") || source.Contains("blinded"))
                return BreakdownCategory.Status;

            if (mod.Type == ModifierType.Advantage)
                return BreakdownCategory.Advantage;

            if (mod.Type == ModifierType.Disadvantage)
                return BreakdownCategory.Advantage;

            // Default to base for unrecognized modifiers
            return BreakdownCategory.Base;
        }
    }
}
