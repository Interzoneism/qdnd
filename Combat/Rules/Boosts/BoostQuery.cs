using QDND.Combat.Entities;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Context data for querying boosts that apply to a specific situation.
    /// Used to evaluate conditional boosts that depend on roll type, ability, target, etc.
    /// 
    /// Example usage:
    /// - Query for advantage on attack rolls: new BoostQuery(BoostType.Advantage, RollType.AttackRoll)
    /// - Query for AC bonuses: new BoostQuery(BoostType.AC)
    /// - Query for damage resistance: new BoostQuery(BoostType.Resistance, damageType: DamageType.Fire)
    /// </summary>
    public class BoostQuery
    {
        /// <summary>
        /// The type of boost being queried.
        /// </summary>
        public BoostType BoostType { get; set; }

        /// <summary>
        /// Optional: The type of roll being made (AttackRoll, SavingThrow, etc.).
        /// Relevant for Advantage/Disadvantage queries.
        /// </summary>
        public RollType? RollType { get; set; }

        /// <summary>
        /// Optional: The ability score being used (for ability checks or saving throws).
        /// Relevant for ability-specific Advantage/Disadvantage.
        /// </summary>
        public AbilityType? Ability { get; set; }

        /// <summary>
        /// Optional: The damage type being dealt/resisted.
        /// Relevant for Resistance and DamageBonus queries.
        /// </summary>
        public DamageType? DamageType { get; set; }

        /// <summary>
        /// Optional: The combatant initiating the action (attacker, caster, etc.).
        /// Used for evaluating conditional boosts.
        /// </summary>
        public Combatant Actor { get; set; }

        /// <summary>
        /// Optional: The combatant being targeted.
        /// Used for evaluating conditional boosts that depend on target properties.
        /// </summary>
        public Combatant Target { get; set; }

        /// <summary>
        /// Create a new boost query.
        /// </summary>
        /// <param name="boostType">The type of boost to query for</param>
        /// <param name="rollType">Optional roll type filter</param>
        /// <param name="ability">Optional ability filter</param>
        /// <param name="damageType">Optional damage type filter</param>
        /// <param name="actor">Optional actor context</param>
        /// <param name="target">Optional target context</param>
        public BoostQuery(
            BoostType boostType,
            RollType? rollType = null,
            AbilityType? ability = null,
            DamageType? damageType = null,
            Combatant actor = null,
            Combatant target = null)
        {
            BoostType = boostType;
            RollType = rollType;
            Ability = ability;
            DamageType = damageType;
            Actor = actor;
            Target = target;
        }

        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string> { $"Type={BoostType}" };
            if (RollType.HasValue) parts.Add($"Roll={RollType.Value}");
            if (Ability.HasValue) parts.Add($"Ability={Ability.Value}");
            if (DamageType.HasValue) parts.Add($"Damage={DamageType.Value}");
            if (Actor != null) parts.Add($"Actor={Actor.Name}");
            if (Target != null) parts.Add($"Target={Target.Name}");
            return $"BoostQuery({string.Join(", ", parts)})";
        }
    }
}
