using System;
using System.Linq;

namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Represents a parsed boost from the BG3 boost DSL.
    /// A boost is an atomic stat modifier that affects combat calculations.
    /// 
    /// Examples:
    /// - AC(2) → Type=AC, Parameters=[2], Condition=null
    /// - Resistance(Fire,Resistant) → Type=Resistance, Parameters=["Fire", "Resistant"], Condition=null
    /// - IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll) → Type=Advantage, Parameters=["AttackRoll"], Condition="not DistanceToTargetGreaterThan(3)"
    /// </summary>
    public class BoostDefinition
    {
        /// <summary>
        /// The type of boost (AC, Advantage, Resistance, etc.).
        /// </summary>
        public BoostType Type { get; set; }

        /// <summary>
        /// Parameters passed to the boost function.
        /// Types can be: int, float, string, or nested BoostDefinition.
        /// 
        /// Examples:
        /// - AC(2) → [2]
        /// - Resistance(Fire, Resistant) → ["Fire", "Resistant"]
        /// - DamageBonus(5, Piercing) → [5, "Piercing"]
        /// - WeaponDamage(1d4, Fire) → ["1d4", "Fire"]
        /// </summary>
        public object[] Parameters { get; set; }

        /// <summary>
        /// Optional condition string from IF() syntax.
        /// If present, the boost only applies when this condition evaluates to true.
        /// 
        /// Examples:
        /// - "not DistanceToTargetGreaterThan(3)"
        /// - "HasStatus(RAGING)"
        /// - "IsMeleeAttack()"
        /// 
        /// Condition evaluation is handled by a separate ConditionEvaluator (future implementation).
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// Original boost string that produced this definition.
        /// Useful for debugging and error reporting.
        /// </summary>
        public string RawBoost { get; set; }

        public BoostDefinition()
        {
            Parameters = Array.Empty<object>();
        }

        /// <summary>
        /// Returns true if this boost has a conditional requirement.
        /// </summary>
        public bool IsConditional => !string.IsNullOrWhiteSpace(Condition);

        /// <summary>
        /// Gets a parameter at the specified index, cast to the expected type.
        /// Returns default(T) if index is out of range or cast fails.
        /// </summary>
        public T GetParameter<T>(int index)
        {
            if (index < 0 || index >= Parameters.Length)
                return default;

            try
            {
                if (Parameters[index] is T typedValue)
                    return typedValue;

                // Try conversion for primitives
                return (T)Convert.ChangeType(Parameters[index], typeof(T));
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Gets a parameter as a string, or returns the default value if not found.
        /// </summary>
        public string GetStringParameter(int index, string defaultValue = "")
        {
            if (index < 0 || index >= Parameters.Length)
                return defaultValue;

            return Parameters[index]?.ToString() ?? defaultValue;
        }

        /// <summary>
        /// Gets a parameter as an integer, or returns the default value if not found or invalid.
        /// </summary>
        public int GetIntParameter(int index, int defaultValue = 0)
        {
            if (index < 0 || index >= Parameters.Length)
                return defaultValue;

            if (Parameters[index] is int intValue)
                return intValue;

            if (int.TryParse(Parameters[index]?.ToString(), out int parsed))
                return parsed;

            return defaultValue;
        }

        /// <summary>
        /// Gets a parameter as a float, or returns the default value if not found or invalid.
        /// </summary>
        public float GetFloatParameter(int index, float defaultValue = 0f)
        {
            if (index < 0 || index >= Parameters.Length)
                return defaultValue;

            if (Parameters[index] is float floatValue)
                return floatValue;

            if (Parameters[index] is int intValue)
                return intValue;

            if (float.TryParse(Parameters[index]?.ToString(), out float parsed))
                return parsed;

            return defaultValue;
        }

        public override string ToString()
        {
            var paramStr = Parameters.Length > 0
                ? string.Join(", ", Parameters.Select(p => p?.ToString() ?? "null"))
                : "";

            var conditionStr = IsConditional ? $"IF({Condition}):" : "";
            return $"{conditionStr}{Type}({paramStr})";
        }
    }
}
