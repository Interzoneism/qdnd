namespace QDND.Combat.Rules.Functors
{
    /// <summary>
    /// Specifies who the functor targets.
    /// BG3 supports SELF/TARGET prefixes on functors.
    /// </summary>
    public enum FunctorTarget
    {
        /// <summary>Default target — context-dependent (usually the status/passive target).</summary>
        Default,

        /// <summary>Targets the source/owner (e.g., SELF:ApplyStatus(BURNING)).</summary>
        Self,

        /// <summary>Targets the other party (e.g., TARGET:RemoveStatus(BLESSED)).</summary>
        Target
    }

    /// <summary>
    /// Data class representing a single parsed functor definition.
    /// A functor is a discrete operation like DealDamage(1d4,Fire) or ApplyStatus(BURNING,100,2).
    /// </summary>
    public class FunctorDefinition
    {
        /// <summary>
        /// The type of functor operation.
        /// </summary>
        public FunctorType Type { get; set; }

        /// <summary>
        /// Parsed parameters for the functor.
        /// Contents vary by functor type; all stored as strings for maximum flexibility.
        /// Examples:
        /// - DealDamage: ["1d4", "Fire"]
        /// - ApplyStatus: ["BURNING", "100", "2"]
        /// - RemoveStatus: ["SELF", "RAGE"]
        /// </summary>
        public string[] Parameters { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Who this functor targets (parsed from SELF:/TARGET: prefix).
        /// </summary>
        public FunctorTarget TargetOverride { get; set; } = FunctorTarget.Default;

        /// <summary>
        /// Optional IF() condition string (e.g., "HasStatus('RAGING')").
        /// Null if unconditional.
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// The original raw string before parsing, for debugging/logging.
        /// </summary>
        public string RawString { get; set; }

        /// <summary>
        /// Returns a human-readable representation of this functor.
        /// </summary>
        public override string ToString()
        {
            string prefix = TargetOverride switch
            {
                FunctorTarget.Self => "SELF:",
                FunctorTarget.Target => "TARGET:",
                _ => ""
            };
            string cond = !string.IsNullOrEmpty(Condition) ? $"IF({Condition}):" : "";
            string args = Parameters.Length > 0 ? string.Join(",", Parameters) : "";
            return $"{cond}{prefix}{Type}({args})";
        }
    }
}
