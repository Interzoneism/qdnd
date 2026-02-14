using QDND.Combat.Entities;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Rules.Conditions
{
    /// <summary>
    /// Provides combat context for evaluating BG3 condition expressions.
    /// Carries information about the source, target, attack type, spell, etc.
    /// so the condition evaluator can resolve functions like IsMeleeAttack(), HasStatus(), etc.
    /// </summary>
    public class ConditionContext
    {
        // ──────────────────────────────────────────────
        //  Core participants
        // ──────────────────────────────────────────────

        /// <summary>
        /// The combatant that owns the boost / passive being evaluated (the "self").
        /// In BG3 terms this is <c>context.Source</c>.
        /// </summary>
        public Combatant Source { get; set; }

        /// <summary>
        /// The target of the current action, if any.
        /// In BG3 terms this is <c>context.Target</c>.
        /// </summary>
        public Combatant Target { get; set; }

        // ──────────────────────────────────────────────
        //  Attack / spell metadata
        // ──────────────────────────────────────────────

        /// <summary>
        /// Whether the triggering action is a melee attack.
        /// </summary>
        public bool IsMelee { get; set; }

        /// <summary>
        /// Whether the triggering action is a ranged attack.
        /// </summary>
        public bool IsRanged { get; set; }

        /// <summary>
        /// Whether the triggering action is a weapon attack (as opposed to a spell attack).
        /// </summary>
        public bool IsWeaponAttack { get; set; }

        /// <summary>
        /// Whether the triggering action is a spell attack.
        /// </summary>
        public bool IsSpellAttack { get; set; }

        /// <summary>
        /// Whether the triggering action is a spell (includes non-attack spells).
        /// </summary>
        public bool IsSpell { get; set; }

        /// <summary>
        /// The spell ID being cast, if any.
        /// </summary>
        public string SpellId { get; set; }

        /// <summary>
        /// The damage type of the triggering action, if relevant.
        /// </summary>
        public DamageType? DamageType { get; set; }

        /// <summary>
        /// Whether the triggering roll was a critical hit.
        /// </summary>
        public bool IsCriticalHit { get; set; }

        /// <summary>
        /// Whether the triggering roll was a critical miss (natural 1).
        /// </summary>
        public bool IsCriticalMiss { get; set; }

        /// <summary>
        /// Whether the hit was successful (DamageFlags.Hit equivalent).
        /// </summary>
        public bool IsHit { get; set; }

        /// <summary>
        /// The weapon definition used for the attack, if any.
        /// </summary>
        public WeaponDefinition Weapon { get; set; }

        // ──────────────────────────────────────────────
        //  External service references (optional)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Optional reference to the status manager for HasStatus() checks.
        /// If null, HasStatus() will fall back to checking the combatant's boost container.
        /// </summary>
        public QDND.Combat.Statuses.StatusManager StatusManager { get; set; }

        // ──────────────────────────────────────────────
        //  Factory methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Creates a context for evaluating conditions during an attack roll.
        /// </summary>
        /// <param name="source">The attacker (boost owner)</param>
        /// <param name="target">The defender being attacked</param>
        /// <param name="isMelee">True if this is a melee attack</param>
        /// <param name="isWeapon">True if this is a weapon attack (vs spell attack)</param>
        /// <param name="weapon">The weapon being used, if any</param>
        /// <returns>A new <see cref="ConditionContext"/> configured for attack rolls</returns>
        public static ConditionContext ForAttackRoll(
            Combatant source,
            Combatant target,
            bool isMelee = true,
            bool isWeapon = true,
            WeaponDefinition weapon = null)
        {
            return new ConditionContext
            {
                Source = source,
                Target = target,
                IsMelee = isMelee,
                IsRanged = !isMelee,
                IsWeaponAttack = isWeapon,
                IsSpellAttack = !isWeapon,
                IsSpell = !isWeapon,
                Weapon = weapon ?? source?.MainHandWeapon,
            };
        }

        /// <summary>
        /// Creates a context for evaluating conditions during a saving throw.
        /// </summary>
        /// <param name="source">The combatant forcing the save (caster/attacker)</param>
        /// <param name="target">The combatant making the save</param>
        /// <param name="isSpell">Whether the save is forced by a spell</param>
        /// <returns>A new <see cref="ConditionContext"/> configured for saving throws</returns>
        public static ConditionContext ForSavingThrow(
            Combatant source,
            Combatant target,
            bool isSpell = false)
        {
            return new ConditionContext
            {
                Source = source,
                Target = target,
                IsSpell = isSpell,
            };
        }

        /// <summary>
        /// Creates a context for evaluating conditions during damage application.
        /// </summary>
        /// <param name="source">The combatant dealing damage</param>
        /// <param name="target">The combatant taking damage</param>
        /// <param name="damageType">The type of damage being dealt</param>
        /// <param name="isMelee">Whether the damage source is melee</param>
        /// <param name="isWeapon">Whether the damage comes from a weapon</param>
        /// <param name="isHit">Whether the attack hit</param>
        /// <param name="isCrit">Whether the attack was a critical hit</param>
        /// <param name="weapon">The weapon used, if any</param>
        /// <returns>A new <see cref="ConditionContext"/> configured for damage</returns>
        public static ConditionContext ForDamage(
            Combatant source,
            Combatant target,
            DamageType? damageType = null,
            bool isMelee = true,
            bool isWeapon = true,
            bool isHit = true,
            bool isCrit = false,
            WeaponDefinition weapon = null)
        {
            return new ConditionContext
            {
                Source = source,
                Target = target,
                DamageType = damageType,
                IsMelee = isMelee,
                IsRanged = !isMelee,
                IsWeaponAttack = isWeapon,
                IsSpellAttack = !isWeapon,
                IsSpell = !isWeapon,
                IsHit = isHit,
                IsCriticalHit = isCrit,
                Weapon = weapon ?? source?.MainHandWeapon,
            };
        }

        /// <summary>
        /// Creates a context for evaluating conditions when a status effect is applied or
        /// when checking passive conditions outside of a specific attack context.
        /// </summary>
        /// <param name="source">The combatant that owns the boost / passive</param>
        /// <param name="target">Optional target combatant</param>
        /// <returns>A new <see cref="ConditionContext"/> configured for general status checks</returns>
        public static ConditionContext ForStatus(Combatant source, Combatant target = null)
        {
            return new ConditionContext
            {
                Source = source,
                Target = target,
            };
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var parts = new System.Collections.Generic.List<string>();
            if (Source != null) parts.Add($"Src={Source.Name}");
            if (Target != null) parts.Add($"Tgt={Target.Name}");
            if (IsMelee) parts.Add("Melee");
            if (IsRanged) parts.Add("Ranged");
            if (IsWeaponAttack) parts.Add("WeaponAtk");
            if (IsSpellAttack) parts.Add("SpellAtk");
            if (IsSpell) parts.Add("Spell");
            if (IsHit) parts.Add("Hit");
            if (IsCriticalHit) parts.Add("Crit");
            if (DamageType.HasValue) parts.Add($"Dmg={DamageType.Value}");
            return $"ConditionContext({string.Join(", ", parts)})";
        }
    }
}
