using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Type of breakdown.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum BreakdownType
    {
        AttackRoll,
        DamageRoll,
        HealingRoll,
        SavingThrow,
        ArmorClass,
        HitChance,
        DifficultyClass,
        SkillCheck,
        Initiative,
        Custom
    }

    /// <summary>
    /// A single component in a breakdown.
    /// </summary>
    public class BreakdownComponent
    {
        /// <summary>
        /// Source of this modifier (action, equipment, status, etc).
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// Category (base, modifier, multiplier, etc).
        /// </summary>
        public string Category { get; set; }

        /// <summary>
        /// Numeric value.
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Is this a percentage/multiplier rather than additive?
        /// </summary>
        public bool IsMultiplier { get; set; }

        /// <summary>
        /// Human-readable description.
        /// </summary>
        public string Description { get; set; }

        public BreakdownComponent() { }

        public BreakdownComponent(string source, float value, string category = "modifier", string description = null)
        {
            Source = source;
            Value = value;
            Category = category;
            Description = description ?? source;
        }

        public override string ToString()
        {
            string sign = Value >= 0 ? "+" : "";
            string valStr = IsMultiplier ? $"x{Value:F2}" : $"{sign}{Value}";
            return $"{Description}: {valStr}";
        }
    }

    /// <summary>
    /// Complete breakdown of a calculation.
    /// </summary>
    public class BreakdownPayload
    {
        /// <summary>
        /// Type of calculation.
        /// </summary>
        public BreakdownType Type { get; set; }

        /// <summary>
        /// Label for this breakdown.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Base value before modifiers.
        /// </summary>
        public float BaseValue { get; set; }

        /// <summary>
        /// Final calculated value.
        /// </summary>
        public float FinalValue { get; set; }

        /// <summary>
        /// All components that contributed.
        /// </summary>
        public List<BreakdownComponent> Components { get; set; } = new();

        /// <summary>
        /// The die roll if applicable.
        /// </summary>
        public int? DieRoll { get; set; }

        /// <summary>
        /// Number of dice rolled.
        /// </summary>
        public int? DiceCount { get; set; }

        /// <summary>
        /// Die size.
        /// </summary>
        public int? DieSize { get; set; }

        /// <summary>
        /// Was advantage applied?
        /// </summary>
        public bool HasAdvantage { get; set; }

        /// <summary>
        /// Was disadvantage applied?
        /// </summary>
        public bool HasDisadvantage { get; set; }

        /// <summary>
        /// Both rolls if advantage/disadvantage (used, discarded).
        /// </summary>
        public (int used, int discarded)? AdvantageRolls { get; set; }

        /// <summary>
        /// Was this a critical?
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Was this a critical failure?
        /// </summary>
        public bool IsCriticalFailure { get; set; }

        /// <summary>
        /// Target value to beat (DC or AC).
        /// </summary>
        public int? Target { get; set; }

        /// <summary>
        /// Did the roll succeed?
        /// </summary>
        public bool? Success { get; set; }

        /// <summary>
        /// Additional notes.
        /// </summary>
        public List<string> Notes { get; set; } = new();

        /// <summary>
        /// Add a component.
        /// </summary>
        public void Add(string source, float value, string category = "modifier", string description = null)
        {
            Components.Add(new BreakdownComponent(source, value, category, description));
        }

        /// <summary>
        /// Add a multiplier component.
        /// </summary>
        public void AddMultiplier(string source, float multiplier, string description = null)
        {
            Components.Add(new BreakdownComponent
            {
                Source = source,
                Value = multiplier,
                Category = "multiplier",
                IsMultiplier = true,
                Description = description ?? source
            });
        }

        /// <summary>
        /// Calculate final value from components.
        /// </summary>
        public float Calculate()
        {
            float total = BaseValue;
            float multiplier = 1f;

            foreach (var comp in Components)
            {
                if (comp.IsMultiplier)
                    multiplier *= comp.Value;
                else
                    total += comp.Value;
            }

            FinalValue = total * multiplier;
            return FinalValue;
        }

        /// <summary>
        /// Get sum of all additive modifiers.
        /// </summary>
        public float GetTotalModifier()
        {
            return Components.Where(c => !c.IsMultiplier).Sum(c => c.Value);
        }

        /// <summary>
        /// Get combined multiplier.
        /// </summary>
        public float GetTotalMultiplier()
        {
            float mult = 1f;
            foreach (var comp in Components.Where(c => c.IsMultiplier))
                mult *= comp.Value;
            return mult;
        }

        /// <summary>
        /// Format as human-readable string.
        /// </summary>
        public string Format()
        {
            var lines = new List<string>();
            lines.Add($"=== {Label ?? Type.ToString()} ===");

            if (DieRoll.HasValue)
            {
                string dieStr = $"Roll: {DieRoll}";
                if (DiceCount.HasValue && DieSize.HasValue)
                    dieStr = $"Roll: {DieRoll} ({DiceCount}d{DieSize})";
                if (HasAdvantage && AdvantageRolls.HasValue)
                    dieStr += $" [Advantage: used {AdvantageRolls.Value.used}, discarded {AdvantageRolls.Value.discarded}]";
                if (HasDisadvantage && AdvantageRolls.HasValue)
                    dieStr += $" [Disadvantage: used {AdvantageRolls.Value.used}, discarded {AdvantageRolls.Value.discarded}]";
                lines.Add(dieStr);
            }

            lines.Add($"Base: {BaseValue}");

            foreach (var comp in Components)
            {
                lines.Add($"  {comp}");
            }

            lines.Add($"Total: {FinalValue}");

            if (Target.HasValue)
            {
                string result = Success == true ? "SUCCESS" : "FAILURE";
                lines.Add($"vs {Target}: {result}");
            }

            if (IsCritical) lines.Add("[CRITICAL HIT]");
            if (IsCriticalFailure) lines.Add("[CRITICAL FAILURE]");

            foreach (var note in Notes)
                lines.Add($"* {note}");

            return string.Join("\n", lines);
        }

        /// <summary>
        /// Serialize to JSON.
        /// </summary>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        /// <summary>
        /// Convert to dictionary for CombatLogEntry.Breakdown.
        /// </summary>
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["type"] = Type.ToString(),
                ["base"] = BaseValue,
                ["final"] = FinalValue,
                ["modifiers"] = Components.Select(c => new Dictionary<string, object>
                {
                    ["source"] = c.Source,
                    ["value"] = c.Value,
                    ["isMultiplier"] = c.IsMultiplier
                }).ToList()
            };

            if (DieRoll.HasValue) dict["roll"] = DieRoll.Value;
            if (Target.HasValue) dict["target"] = Target.Value;
            if (Success.HasValue) dict["success"] = Success.Value;
            if (IsCritical) dict["critical"] = true;
            if (HasAdvantage) dict["advantage"] = true;
            if (HasDisadvantage) dict["disadvantage"] = true;

            return dict;
        }

        // Factory methods for common breakdown types

        /// <summary>
        /// Create an attack roll breakdown.
        /// </summary>
        public static BreakdownPayload AttackRoll(int dieRoll, int baseModifier, int targetAC)
        {
            var payload = new BreakdownPayload
            {
                Type = BreakdownType.AttackRoll,
                Label = "Attack Roll",
                DieRoll = dieRoll,
                DiceCount = 1,
                DieSize = 20,
                BaseValue = dieRoll,
                Target = targetAC,
                IsCritical = dieRoll == 20,
                IsCriticalFailure = dieRoll == 1
            };

            payload.Add("Base Modifier", baseModifier, "modifier");
            payload.Calculate();
            payload.Success = payload.IsCritical || (!payload.IsCriticalFailure && payload.FinalValue >= targetAC);

            return payload;
        }

        /// <summary>
        /// Create a damage roll breakdown.
        /// </summary>
        public static BreakdownPayload DamageRoll(int dieResult, int diceCount, int dieSize, int bonus = 0)
        {
            var payload = new BreakdownPayload
            {
                Type = BreakdownType.DamageRoll,
                Label = "Damage",
                DieRoll = dieResult,
                DiceCount = diceCount,
                DieSize = dieSize,
                BaseValue = dieResult
            };

            if (bonus != 0)
                payload.Add("Damage Bonus", bonus, "modifier");

            payload.Calculate();
            return payload;
        }

        /// <summary>
        /// Create a saving throw breakdown.
        /// </summary>
        public static BreakdownPayload SavingThrow(string saveType, int dieRoll, int modifier, int dc)
        {
            var payload = new BreakdownPayload
            {
                Type = BreakdownType.SavingThrow,
                Label = $"{saveType} Save",
                DieRoll = dieRoll,
                DiceCount = 1,
                DieSize = 20,
                BaseValue = dieRoll,
                Target = dc
            };

            payload.Add($"{saveType} Modifier", modifier, "modifier");
            payload.Calculate();
            payload.Success = payload.FinalValue >= dc;

            return payload;
        }

        /// <summary>
        /// Create an AC breakdown.
        /// </summary>
        public static BreakdownPayload ArmorClass(int baseAC)
        {
            return new BreakdownPayload
            {
                Type = BreakdownType.ArmorClass,
                Label = "Armor Class",
                BaseValue = baseAC,
                FinalValue = baseAC
            };
        }
    }
}
