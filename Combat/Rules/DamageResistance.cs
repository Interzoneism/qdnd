namespace QDND.Combat.Rules
{
    /// <summary>
    /// Factory methods for creating damage resistance/vulnerability/immunity modifiers.
    /// </summary>
    public static class DamageResistance
    {
        /// <summary>
        /// Creates a resistance modifier for the specified damage type (50% damage = half damage).
        /// Uses percentage modifier with -50 value, which translates to 0.5x multiplier.
        /// </summary>
        /// <param name="damageType">The damage type to resist (e.g., "fire", "cold").</param>
        /// <param name="source">Source of the resistance (e.g., "Ring of Fire Resistance", "Racial Trait").</param>
        /// <returns>A modifier that halves damage of the specified type.</returns>
        public static Modifier CreateResistance(string damageType, string source = "Resistance")
        {
            var normalizedType = DamageTypes.NormalizeType(damageType);
            var damageTag = DamageTypes.ToTag(damageType);

            return new Modifier
            {
                Name = $"Resistance: {damageType}",
                Source = source,
                Target = ModifierTarget.DamageTaken,
                Type = ModifierType.Percentage,
                Value = -50, // 1.0 + (-50/100) = 0.5 multiplier
                Priority = 100, // Resist/vuln apply in normal multiplier stage
                Condition = ctx => ctx != null && ctx.Tags.Contains(damageTag)
            };
        }

        /// <summary>
        /// Creates a vulnerability modifier for the specified damage type (200% damage = double damage).
        /// Uses percentage modifier with +100 value, which translates to 2.0x multiplier.
        /// </summary>
        /// <param name="damageType">The damage type to be vulnerable to.</param>
        /// <param name="source">Source of the vulnerability.</param>
        /// <returns>A modifier that doubles damage of the specified type.</returns>
        public static Modifier CreateVulnerability(string damageType, string source = "Vulnerability")
        {
            var normalizedType = DamageTypes.NormalizeType(damageType);
            var damageTag = DamageTypes.ToTag(damageType);

            return new Modifier
            {
                Name = $"Vulnerability: {damageType}",
                Source = source,
                Target = ModifierTarget.DamageTaken,
                Type = ModifierType.Percentage,
                Value = 100, // 1.0 + (100/100) = 2.0 multiplier
                Priority = 100,
                Condition = ctx => ctx != null && ctx.Tags.Contains(damageTag)
            };
        }

        /// <summary>
        /// Creates an immunity modifier for the specified damage type (0% damage = no damage).
        /// Uses percentage modifier with -100 value, which translates to 0.0x multiplier.
        /// Has lower priority (50) than resistance/vulnerability so it applies first.
        /// </summary>
        /// <param name="damageType">The damage type to be immune to.</param>
        /// <param name="source">Source of the immunity.</param>
        /// <returns>A modifier that nullifies damage of the specified type.</returns>
        public static Modifier CreateImmunity(string damageType, string source = "Immunity")
        {
            var normalizedType = DamageTypes.NormalizeType(damageType);
            var damageTag = DamageTypes.ToTag(damageType);

            return new Modifier
            {
                Name = $"Immunity: {damageType}",
                Source = source,
                Target = ModifierTarget.DamageTaken,
                Type = ModifierType.Percentage,
                Value = -100, // 1.0 + (-100/100) = 0.0 multiplier
                Priority = 50, // Immunity applies first (lower priority = earlier)
                Condition = ctx => ctx != null && ctx.Tags.Contains(damageTag)
            };
        }
    }
}
