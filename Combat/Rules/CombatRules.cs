using System;

namespace QDND.Combat.Rules
{
    /// <summary>
    /// Canonical combat-rule constants. Internal distance unit is meters.
    /// </summary>
    public static class CombatRules
    {
        /// <summary>
        /// Default melee reach for baseline weapon attacks.
        /// </summary>
        public const float DefaultMeleeReachMeters = 1.5f;

        /// <summary>
        /// Opportunity attack trigger range.
        /// </summary>
        public const float OpportunityAttackRangeMeters = 1.5f;

        /// <summary>
        /// Threshold for melee auto-crits (paralyzed/unconscious/frozen targets).
        /// </summary>
        public const float MeleeAutocritRangeMeters = 1.5f;

        /// <summary>
        /// BG3-style baseline movement budget (30ft ≈ 9m).
        /// </summary>
        public const float DefaultMovementBudgetMeters = 9.0f;

        /// <summary>
        /// Canonical counterspell radius (60ft ≈ 18m).
        /// </summary>
        public const float CounterspellRangeMeters = 18.0f;

        /// <summary>
        /// Feet-to-meters conversion ratio.
        /// </summary>
        public const float FeetToMeters = 0.3048f;

        /// <summary>
        /// Heuristic threshold for legacy feet-like values.
        /// Values above this are treated as feet and converted to meters.
        /// </summary>
        public const float LegacyFeetHeuristicThreshold = 15.0f;

        public static float ConvertFeetToMeters(float feet)
        {
            return feet * FeetToMeters;
        }

        /// <summary>
        /// Normalizes legacy feet-like values to meters.
        /// </summary>
        public static float NormalizeDistanceToMeters(float value)
        {
            if (value <= 0)
            {
                return value;
            }

            if (value > LegacyFeetHeuristicThreshold)
            {
                return (float)Math.Round(ConvertFeetToMeters(value), 3);
            }

            return value;
        }
    }
}
