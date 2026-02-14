namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Defines damage resistance levels for damage type modifiers.
    /// These map to damage multipliers applied to incoming damage.
    /// </summary>
    public enum ResistanceLevel
    {
        /// <summary>
        /// Vulnerable to damage - takes 2x damage (200% multiplier).
        /// </summary>
        Vulnerable,

        /// <summary>
        /// Normal damage - takes 1x damage (100% multiplier).
        /// This is the default state when no resistance/vulnerability/immunity applies.
        /// </summary>
        Normal,

        /// <summary>
        /// Resistant to damage - takes 0.5x damage (50% multiplier).
        /// </summary>
        Resistant,

        /// <summary>
        /// Immune to damage - takes 0x damage (0% multiplier).
        /// Completely negates all damage of the specified type.
        /// </summary>
        Immune
    }
}
