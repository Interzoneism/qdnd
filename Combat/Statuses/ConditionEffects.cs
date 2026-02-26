using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Statuses
{
    /// <summary>
    /// The 14 D&amp;D 5e conditions as defined in the PHB, with BG3 mappings.
    /// </summary>
    public enum ConditionType
    {
        Blinded,
        Charmed,
        Deafened,
        Frightened,
        Grappled,
        Incapacitated,
        Invisible,
        Paralyzed,
        Petrified,
        Poisoned,
        Prone,
        Restrained,
        Stunned,
        Unconscious,
        Exhaustion,
        Frozen
    }

    /// <summary>
    /// Describes the mechanical effects of a D&amp;D 5e condition.
    /// Used by the RulesEngine to apply condition-based modifiers during resolution.
    /// </summary>
    public class ConditionMechanics
    {
        /// <summary>Condition type this describes.</summary>
        public ConditionType Type { get; init; }

        /// <summary>Attack rolls against this creature have Advantage.</summary>
        public bool GrantsAdvantageToAttackers { get; init; }

        /// <summary>Attack rolls against this creature have Disadvantage (e.g., Invisible).</summary>
        public bool GrantsDisadvantageToAttackers { get; init; }

        /// <summary>This creature's attack rolls have Disadvantage.</summary>
        public bool HasDisadvantageOnAttacks { get; init; }

        /// <summary>This creature's attack rolls have Advantage (e.g., Invisible).</summary>
        public bool HasAdvantageOnAttacks { get; init; }

        /// <summary>Auto-fail Strength and Dexterity saving throws.</summary>
        public bool AutoFailStrDexSaves { get; init; }

        /// <summary>Can't take Actions or Reactions (Incapacitated core).</summary>
        public bool IsIncapacitated { get; init; }

        /// <summary>Speed becomes 0.</summary>
        public bool SpeedZero { get; init; }

        /// <summary>Melee hits against this creature are automatic Critical Hits.</summary>
        public bool MeleeAutocrits { get; init; }

        /// <summary>Disadvantage on Dexterity saving throws only.</summary>
        public bool HasDisadvantageOnDexSaves { get; init; }

        /// <summary>Disadvantage on ability checks.</summary>
        public bool HasDisadvantageOnAbilityChecks { get; init; }

        /// <summary>Resistance to all damage types (Petrified).</summary>
        public bool HasResistanceToAllDamage { get; init; }

        /// <summary>Can't move.</summary>
        public bool CantMove { get; init; }

        /// <summary>Can't speak.</summary>
        public bool CantSpeak { get; init; }

        /// <summary>Prone-specific: melee attacks against have Advantage, ranged attacks against have Disadvantage.</summary>
        public bool ProneAttackRules { get; init; }
    }

    /// <summary>
    /// Centralized system providing D&amp;D 5e/BG3 condition mechanical effects.
    /// Maps status IDs to their exact mechanical consequences, used by the RulesEngine
    /// to apply condition modifiers during attack, save, and damage resolution.
    /// </summary>
    public static class ConditionEffects
    {
        /// <summary>
        /// Maps status IDs (lowercase) to their ConditionType.
        /// Multiple status IDs can map to the same condition (e.g., asleep → Unconscious).
        /// </summary>
        private static readonly Dictionary<string, ConditionType> StatusToCondition = new(StringComparer.OrdinalIgnoreCase)
        {
            // Blinded
            { "blinded", ConditionType.Blinded },
            { "darkness_obscured", ConditionType.Blinded },

            // Charmed
            { "charmed", ConditionType.Charmed },
            { "commanded", ConditionType.Charmed },
            { "crown_of_madness", ConditionType.Charmed },

            // Deafened
            { "deafened", ConditionType.Deafened },

            // Frightened
            { "frightened", ConditionType.Frightened },

            // Grappled
            { "grappled", ConditionType.Grappled },

            // Incapacitated
            { "incapacitated", ConditionType.Incapacitated },
            { "hypnotised", ConditionType.Incapacitated },
            { "hypnotized", ConditionType.Incapacitated },
            { "hypnotic_pattern", ConditionType.Incapacitated },
            { "banished", ConditionType.Incapacitated },

            // Invisible
            { "invisible", ConditionType.Invisible },
            { "greater_invisible", ConditionType.Invisible },

            // Paralyzed
            { "paralyzed", ConditionType.Paralyzed },

            // Petrified
            { "petrified", ConditionType.Petrified },

            // Poisoned
            { "poisoned", ConditionType.Poisoned },

            // Prone
            { "prone", ConditionType.Prone },

            // Restrained
            { "restrained", ConditionType.Restrained },
            { "webbed", ConditionType.Restrained },
            { "ensnared", ConditionType.Restrained },
            { "ensnared_vines", ConditionType.Restrained },
            { "immobilized", ConditionType.Restrained },

            // Stunned
            { "stunned", ConditionType.Stunned },

            // Frightened aliases
            { "feared", ConditionType.Frightened },

            // Frozen (immobilized + incapacitated)
            { "frozen", ConditionType.Frozen },

            // Exhaustion
            { "exhaustion", ConditionType.Exhaustion },
            { "exhausted", ConditionType.Exhaustion },

            // Unconscious (maps to asleep, downed)
            { "asleep", ConditionType.Unconscious },
            { "downed", ConditionType.Unconscious },
            { "unconscious", ConditionType.Unconscious },
        };

        /// <summary>
        /// Condition mechanics definitions — exact BG3/5e effects for each condition.
        /// </summary>
        private static readonly Dictionary<ConditionType, ConditionMechanics> MechanicsMap = new()
        {
            {
                ConditionType.Blinded, new ConditionMechanics
                {
                    Type = ConditionType.Blinded,
                    HasDisadvantageOnAttacks = true,
                    GrantsAdvantageToAttackers = true,
                }
            },
            {
                ConditionType.Charmed, new ConditionMechanics
                {
                    Type = ConditionType.Charmed,
                    // Can't attack the charmer — handled via targeting validation, not modifier
                    // Charmer has Advantage on social checks — not relevant in combat
                }
            },
            {
                ConditionType.Deafened, new ConditionMechanics
                {
                    Type = ConditionType.Deafened,
                    // Auto-fail hearing checks — handled by ability check system
                }
            },
            {
                ConditionType.Frightened, new ConditionMechanics
                {
                    Type = ConditionType.Frightened,
                    HasDisadvantageOnAttacks = true,
                    HasDisadvantageOnAbilityChecks = true,
                    // Can't willingly move closer to fear source — handled by movement validation
                }
            },
            {
                ConditionType.Grappled, new ConditionMechanics
                {
                    Type = ConditionType.Grappled,
                    SpeedZero = true,
                    CantMove = true,
                }
            },
            {
                ConditionType.Incapacitated, new ConditionMechanics
                {
                    Type = ConditionType.Incapacitated,
                    IsIncapacitated = true,
                }
            },
            {
                ConditionType.Invisible, new ConditionMechanics
                {
                    Type = ConditionType.Invisible,
                    HasAdvantageOnAttacks = true,
                    GrantsDisadvantageToAttackers = true,
                }
            },
            {
                ConditionType.Paralyzed, new ConditionMechanics
                {
                    Type = ConditionType.Paralyzed,
                    IsIncapacitated = true,
                    CantMove = true,
                    CantSpeak = true,
                    AutoFailStrDexSaves = true,
                    GrantsAdvantageToAttackers = true,
                    MeleeAutocrits = true,
                }
            },
            {
                ConditionType.Petrified, new ConditionMechanics
                {
                    Type = ConditionType.Petrified,
                    IsIncapacitated = true,
                    CantMove = true,
                    CantSpeak = true,
                    AutoFailStrDexSaves = true,
                    GrantsAdvantageToAttackers = true,
                    HasResistanceToAllDamage = true,
                    // Immune to poison and disease — handled by status immunity
                }
            },
            {
                ConditionType.Poisoned, new ConditionMechanics
                {
                    Type = ConditionType.Poisoned,
                    HasDisadvantageOnAttacks = true,
                    HasDisadvantageOnAbilityChecks = true,
                }
            },
            {
                ConditionType.Prone, new ConditionMechanics
                {
                    Type = ConditionType.Prone,
                    HasDisadvantageOnAttacks = true,
                    ProneAttackRules = true,
                    // Melee within 5ft: Advantage to attackers
                    // Ranged beyond 5ft: Disadvantage to attackers
                    // Standing costs half movement — handled by movement system
                }
            },
            {
                ConditionType.Restrained, new ConditionMechanics
                {
                    Type = ConditionType.Restrained,
                    SpeedZero = true,
                    CantMove = true,
                    HasDisadvantageOnAttacks = true,
                    GrantsAdvantageToAttackers = true,
                    HasDisadvantageOnDexSaves = true,
                }
            },
            {
                ConditionType.Stunned, new ConditionMechanics
                {
                    Type = ConditionType.Stunned,
                    IsIncapacitated = true,
                    CantMove = true,
                    CantSpeak = true,
                    AutoFailStrDexSaves = true,
                    GrantsAdvantageToAttackers = true,
                }
            },
            {
                ConditionType.Unconscious, new ConditionMechanics
                {
                    Type = ConditionType.Unconscious,
                    IsIncapacitated = true,
                    CantMove = true,
                    CantSpeak = true,
                    AutoFailStrDexSaves = true,
                    GrantsAdvantageToAttackers = true,
                    MeleeAutocrits = true,
                }
            },
            {
                ConditionType.Exhaustion, new ConditionMechanics
                {
                    Type = ConditionType.Exhaustion,
                    HasDisadvantageOnAbilityChecks = true,
                    HasDisadvantageOnAttacks = true,
                }
            },
            {
                ConditionType.Frozen, new ConditionMechanics
                {
                    Type = ConditionType.Frozen,
                    IsIncapacitated = true,
                    CantMove = true,
                    SpeedZero = true,
                    AutoFailStrDexSaves = true,
                    GrantsAdvantageToAttackers = true,
                    MeleeAutocrits = true,
                }
            },
        };

        /// <summary>
        /// Check if a status ID corresponds to a recognized D&amp;D 5e condition.
        /// </summary>
        public static bool IsCondition(string statusId)
        {
            return !string.IsNullOrWhiteSpace(statusId) &&
                   StatusToCondition.ContainsKey(statusId);
        }

        /// <summary>
        /// Get the ConditionType for a status ID. Returns null if not a condition.
        /// </summary>
        public static ConditionType? GetConditionType(string statusId)
        {
            if (string.IsNullOrWhiteSpace(statusId))
                return null;

            return StatusToCondition.TryGetValue(statusId, out var type) ? type : null;
        }

        /// <summary>
        /// Get the full mechanical effects for a status ID. Returns null if not a condition.
        /// </summary>
        public static ConditionMechanics GetConditionMechanics(string statusId)
        {
            var conditionType = GetConditionType(statusId);
            if (!conditionType.HasValue)
                return null;

            return MechanicsMap.TryGetValue(conditionType.Value, out var mechanics) ? mechanics : null;
        }

        /// <summary>
        /// Get the full mechanical effects for a ConditionType.
        /// </summary>
        public static ConditionMechanics GetConditionMechanics(ConditionType type)
        {
            return MechanicsMap.TryGetValue(type, out var mechanics) ? mechanics : null;
        }

        /// <summary>
        /// Check if a condition on the target gives attackers Advantage.
        /// For Prone, only melee attacks within 5ft grant Advantage.
        /// </summary>
        /// <param name="targetStatusId">The status ID on the target.</param>
        /// <param name="isMeleeAttack">Whether the incoming attack is melee (for Prone rules).</param>
        public static bool ShouldAttackerHaveAdvantage(string targetStatusId, bool isMeleeAttack = true)
        {
            var mechanics = GetConditionMechanics(targetStatusId);
            if (mechanics == null)
                return false;

            if (mechanics.ProneAttackRules)
            {
                // Prone: melee within 5ft = advantage, ranged = disadvantage
                return isMeleeAttack;
            }

            return mechanics.GrantsAdvantageToAttackers;
        }

        /// <summary>
        /// Check if a condition on the target gives attackers Disadvantage.
        /// For Prone, ranged attacks have Disadvantage.
        /// </summary>
        public static bool ShouldAttackerHaveDisadvantage(string targetStatusId, bool isMeleeAttack = true)
        {
            var mechanics = GetConditionMechanics(targetStatusId);
            if (mechanics == null)
                return false;

            if (mechanics.ProneAttackRules)
            {
                // Prone: ranged attacks = disadvantage
                return !isMeleeAttack;
            }

            return mechanics.GrantsDisadvantageToAttackers;
        }

        /// <summary>
        /// Check if a condition causes auto-fail on a specific saving throw type.
        /// Paralyzed, Petrified, Stunned, and Unconscious auto-fail STR and DEX saves.
        /// </summary>
        /// <param name="statusId">The condition status ID.</param>
        /// <param name="saveAbility">The ability type ("STR", "DEX", "CON", etc.).</param>
        public static bool ShouldAutoFailSave(string statusId, string saveAbility)
        {
            var mechanics = GetConditionMechanics(statusId);
            if (mechanics == null || !mechanics.AutoFailStrDexSaves)
                return false;

            var ability = saveAbility?.Trim().ToUpperInvariant();
            return ability == "STR" || ability == "STRENGTH" ||
                   ability == "DEX" || ability == "DEXTERITY";
        }

        /// <summary>
        /// Check if melee hits against a creature with this condition are automatic critical hits.
        /// Applies to Paralyzed and Unconscious.
        /// </summary>
        public static bool ShouldMeleeAutoCrit(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics?.MeleeAutocrits ?? false;
        }

        /// <summary>
        /// Check if a condition makes the creature's own attacks have Advantage.
        /// Applies to Invisible.
        /// </summary>
        public static bool HasAdvantageOnOwnAttacks(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics?.HasAdvantageOnAttacks ?? false;
        }

        /// <summary>
        /// Check if a condition makes the creature's own attacks have Disadvantage.
        /// Applies to Blinded, Frightened, Poisoned, Prone, Restrained.
        /// </summary>
        public static bool HasDisadvantageOnOwnAttacks(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics?.HasDisadvantageOnAttacks ?? false;
        }

        /// <summary>
        /// Check if a condition causes Disadvantage on DEX saves only (not auto-fail).
        /// Applies to Restrained.
        /// </summary>
        public static bool HasDisadvantageOnDexSaves(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics?.HasDisadvantageOnDexSaves ?? false;
        }

        /// <summary>
        /// Check if the condition makes the creature incapacitated (can't take actions/reactions).
        /// </summary>
        public static bool IsIncapacitating(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics?.IsIncapacitated ?? false;
        }

        /// <summary>
        /// Check if the condition makes speed 0 / prevents movement.
        /// </summary>
        public static bool PreventsMovement(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics != null && (mechanics.SpeedZero || mechanics.CantMove);
        }

        /// <summary>
        /// Check if this condition grants resistance to all damage (Petrified).
        /// </summary>
        public static bool HasResistanceToAllDamage(string statusId)
        {
            var mechanics = GetConditionMechanics(statusId);
            return mechanics?.HasResistanceToAllDamage ?? false;
        }

        /// <summary>
        /// Collect all advantage/disadvantage effects from conditions on a combatant's active statuses.
        /// Used by the RulesEngine to resolve combat modifiers.
        /// </summary>
        /// <param name="activeStatusIds">All active status IDs on the combatant.</param>
        /// <param name="isMeleeAttack">Whether to evaluate for melee attack context.</param>
        /// <returns>Aggregate condition effects.</returns>
        public static AggregateConditionEffects GetAggregateEffects(IEnumerable<string> activeStatusIds, bool isMeleeAttack = true)
        {
            var result = new AggregateConditionEffects();

            foreach (var statusId in activeStatusIds)
            {
                var mechanics = GetConditionMechanics(statusId);
                if (mechanics == null)
                    continue;

                result.ActiveConditions.Add(mechanics.Type);

                // Own attacks
                if (mechanics.HasAdvantageOnAttacks)
                    result.AttackAdvantageSources.Add(statusId);
                if (mechanics.HasDisadvantageOnAttacks)
                    result.AttackDisadvantageSources.Add(statusId);

                // Incoming attacks (evaluated for the target)
                if (mechanics.ProneAttackRules)
                {
                    if (isMeleeAttack)
                        result.DefenseAdvantageSources.Add(statusId); // attackers have advantage
                    else
                        result.DefenseDisadvantageSources.Add(statusId); // attackers have disadvantage
                }
                else
                {
                    if (mechanics.GrantsAdvantageToAttackers)
                        result.DefenseAdvantageSources.Add(statusId);
                    if (mechanics.GrantsDisadvantageToAttackers)
                        result.DefenseDisadvantageSources.Add(statusId);
                }

                // Saves
                if (mechanics.AutoFailStrDexSaves)
                    result.AutoFailStrDexSaves = true;
                if (mechanics.HasDisadvantageOnDexSaves)
                    result.HasDisadvantageOnDexSaves = true;
                if (mechanics.HasDisadvantageOnAbilityChecks)
                    result.HasDisadvantageOnAbilityChecks = true;

                // Combat state
                if (mechanics.IsIncapacitated)
                    result.IsIncapacitated = true;
                if (mechanics.SpeedZero || mechanics.CantMove)
                    result.CantMove = true;
                if (mechanics.MeleeAutocrits)
                    result.MeleeAutocrits = true;
                if (mechanics.HasResistanceToAllDamage)
                    result.HasResistanceToAllDamage = true;
            }

            return result;
        }

        /// <summary>
        /// Get all known condition status IDs.
        /// </summary>
        public static IReadOnlyCollection<string> GetAllConditionStatusIds()
        {
            return StatusToCondition.Keys;
        }

        /// <summary>
        /// Get all status IDs that map to a specific condition type.
        /// </summary>
        public static List<string> GetStatusIdsForCondition(ConditionType conditionType)
        {
            return StatusToCondition
                .Where(kvp => kvp.Value == conditionType)
                .Select(kvp => kvp.Key)
                .ToList();
        }
    }

    /// <summary>
    /// Aggregated condition effects from all active conditions on a combatant.
    /// Provides easy-to-query summary for the RulesEngine.
    /// </summary>
    public class AggregateConditionEffects
    {
        /// <summary>Active condition types.</summary>
        public HashSet<ConditionType> ActiveConditions { get; } = new();

        /// <summary>Sources granting advantage on own attacks (e.g., Invisible).</summary>
        public List<string> AttackAdvantageSources { get; } = new();

        /// <summary>Sources granting disadvantage on own attacks (e.g., Blinded, Poisoned).</summary>
        public List<string> AttackDisadvantageSources { get; } = new();

        /// <summary>Sources granting advantage to attackers (e.g., Paralyzed, Stunned).</summary>
        public List<string> DefenseAdvantageSources { get; } = new();

        /// <summary>Sources granting disadvantage to attackers (e.g., Invisible).</summary>
        public List<string> DefenseDisadvantageSources { get; } = new();

        /// <summary>Auto-fail Strength and Dexterity saving throws.</summary>
        public bool AutoFailStrDexSaves { get; set; }

        /// <summary>Disadvantage on DEX saves only (Restrained).</summary>
        public bool HasDisadvantageOnDexSaves { get; set; }

        /// <summary>Disadvantage on ability checks (Frightened, Poisoned).</summary>
        public bool HasDisadvantageOnAbilityChecks { get; set; }

        /// <summary>Can't take actions or reactions.</summary>
        public bool IsIncapacitated { get; set; }

        /// <summary>Can't move.</summary>
        public bool CantMove { get; set; }

        /// <summary>Melee hits are auto-crits (Paralyzed, Unconscious).</summary>
        public bool MeleeAutocrits { get; set; }

        /// <summary>Resistance to all damage (Petrified).</summary>
        public bool HasResistanceToAllDamage { get; set; }

        /// <summary>Whether any conditions are active.</summary>
        public bool HasAnyCondition => ActiveConditions.Count > 0;
    }
}
