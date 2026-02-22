using System;
using System.Collections.Generic;

namespace QDND.Combat.Actions
{
    /// <summary>
    /// Types of targets an ability can have.
    /// </summary>
    public enum TargetType
    {
        Self,           // Targets the caster
        SingleUnit,     // Targets one unit
        MultiUnit,      // Targets multiple specific units
        Point,          // Targets a location
        Cone,           // Cone AoE from caster
        Line,           // Line AoE from caster
        Circle,         // Circle AoE around point
        All,            // All units (allies/enemies)
        None            // No target (passive, aura)
    }

    /// <summary>
    /// Type of action required to use this ability.
    /// </summary>
    public enum CastingTimeType
    {
        Action,         // Full action
        BonusAction,    // Bonus action
        Reaction,       // Reaction (triggered)
        Free,           // Free action (no cost)
        Special         // Special timing (e.g., long rest)
    }

    /// <summary>
    /// Components required to cast a spell.
    /// </summary>
    [Flags]
    public enum SpellComponents
    {
        None = 0,
        Verbal = 1,     // Requires speech
        Somatic = 2,    // Requires gestures
        Material = 4    // Requires material components
    }

    /// <summary>
    /// School of magic for spells.
    /// </summary>
    public enum SpellSchool
    {
        None,
        Abjuration,
        Conjuration,
        Divination,
        Enchantment,
        Evocation,
        Illusion,
        Necromancy,
        Transmutation
    }

    /// <summary>
    /// Verbal intent categorization for AI behavior.
    /// </summary>
    public enum VerbalIntent
    {
        Unknown,
        Damage,
        Healing,
        Buff,
        Debuff,
        Utility,
        Control,
        Movement
    }

    /// <summary>
    /// What factions an ability can target.
    /// </summary>
    [Flags]
    public enum TargetFilter
    {
        None = 0,
        Self = 1,
        Allies = 2,
        Enemies = 4,
        Neutrals = 8,
        All = Self | Allies | Enemies | Neutrals
    }

    /// <summary>
    /// Resource costs for using an action.
    /// </summary>
    public class ActionCost
    {
        public bool UsesAction { get; set; }
        public bool UsesBonusAction { get; set; }
        public bool UsesReaction { get; set; }
        public int MovementCost { get; set; }
        public Dictionary<string, int> ResourceCosts { get; set; } = new(); // e.g., "mana": 10
    }

    /// <summary>
    /// Cooldown configuration for an action.
    /// </summary>
    public class ActionCooldown
    {
        public int TurnCooldown { get; set; }       // Cooldown in turns
        public int RoundCooldown { get; set; }      // Cooldown in rounds
        public int MaxCharges { get; set; } = 1;    // Max charges/uses
        public bool ResetsOnCombatEnd { get; set; } = true;
    }

    /// <summary>
    /// Definition of an ability (data-driven).
    /// </summary>
    public class ActionDefinition
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description text.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Icon identifier.
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Targeting configuration.
        /// </summary>
        public TargetType TargetType { get; set; } = TargetType.SingleUnit;
        public TargetFilter TargetFilter { get; set; } = TargetFilter.Enemies;
        public float Range { get; set; } = 5f;              // Range in units
        public float AreaRadius { get; set; }               // AoE radius
        public float ConeAngle { get; set; } = 60f;         // Cone angle in degrees
        public float LineWidth { get; set; } = 1f;          // Line width in units
        public int MaxTargets { get; set; } = 1;            // Max targets for multi

        /// <summary>
        /// Required tags that targets must have for this ability to affect them.
        /// If empty or null, no tag filtering is applied.
        /// Example: ["undead"] for Turn Undead, ["flying"] for anti-air abilities.
        /// </summary>
        public List<string> RequiredTags { get; set; } = new();

        /// <summary>
        /// Costs to use.
        /// </summary>
        public ActionCost Cost { get; set; } = new();

        /// <summary>
        /// Cooldown configuration.
        /// </summary>
        public ActionCooldown Cooldown { get; set; } = new();

        /// <summary>
        /// Requirements to use (weapon type, stance, etc).
        /// </summary>
        public List<ActionRequirement> Requirements { get; set; } = new();

        /// <summary>
        /// Effects to execute.
        /// </summary>
        public List<EffectDefinition> Effects { get; set; } = new();

        /// <summary>
        /// Save/attack parameters.
        /// </summary>
        public AttackType? AttackType { get; set; }
        public string SaveType { get; set; }                // e.g., "dexterity", "wisdom"
        public int? SaveDC { get; set; }
        public int SaveDCBonus { get; set; }         // Added bonus to computed save DC (e.g., +2 for some weapon actions)
        public bool HalfDamageOnSave { get; set; }

        /// <summary>
        /// Tags for synergy checks.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Animation/VFX/SFX hooks.
        /// </summary>
        public string AnimationId { get; set; }
        public string VfxId { get; set; }
        public string SfxId { get; set; }

        /// <summary>
        /// Whether this ability requires concentration to maintain.
        /// If true, casting another concentration ability will end this effect.
        /// Taking damage forces a Constitution save to maintain.
        /// </summary>
        public bool RequiresConcentration { get; set; }

        /// <summary>
        /// The status ID to track for concentration.
        /// If not specified, uses the first apply_status effect's StatusId.
        /// </summary>
        public string ConcentrationStatusId { get; set; }

        /// <summary>
        /// AI hints for ability usage.
        /// </summary>
        public float AIBaseDesirability { get; set; } = 1.0f;

        /// <summary>
        /// Available variants for this ability (e.g., fire/cold/lightning for Chromatic Orb).
        /// If empty, ability has no variants.
        /// </summary>
        public List<ActionVariant> Variants { get; set; } = new();

        /// <summary>
        /// Whether this ability supports upcasting (casting at higher resource cost for more power).
        /// </summary>
        public bool CanUpcast { get; set; }

        /// <summary>
        /// Configuration for how this ability scales when upcast.
        /// Only used if CanUpcast is true.
        /// </summary>
        public UpcastScaling UpcastScaling { get; set; }

        // --- BG3-Specific Properties ---

        /// <summary>
        /// Spell level (0 for cantrips, 1-9 for leveled spells).
        /// Non-spell abilities should leave this at 0.
        /// </summary>
        public int SpellLevel { get; set; }

        /// <summary>
        /// School of magic this spell belongs to.
        /// </summary>
        public SpellSchool School { get; set; } = SpellSchool.None;

        /// <summary>
        /// Type of action required to cast/use this ability.
        /// </summary>
        public CastingTimeType CastingTime { get; set; } = CastingTimeType.Action;

        /// <summary>
        /// Components required to cast this spell.
        /// </summary>
        public SpellComponents Components { get; set; } = SpellComponents.None;

        /// <summary>
        /// BG3 spell type classification (Target, Projectile, Shout, Zone, etc).
        /// Used for determining animation and VFX behavior.
        /// </summary>
        public string BG3SpellType { get; set; }

        /// <summary>
        /// Raw BG3 SpellProperties formula string.
        /// Complex effect formula like "DealDamage(1d8,Fire);ApplyStatus(BURNING,100,3)".
        /// Converted to Effects list during import but preserved for reference.
        /// </summary>
        public string BG3SpellProperties { get; set; }

        /// <summary>
        /// Raw BG3 SpellRoll formula (attack roll or saving throw).
        /// Examples: "Attack(AttackType.MeleeSpellAttack)", "SavingThrow(Ability.Dexterity, SourceSpellDC())"
        /// </summary>
        public string BG3SpellRoll { get; set; }

        /// <summary>
        /// Raw BG3 SpellSuccess formula (effects on successful hit/save).
        /// </summary>
        public string BG3SpellSuccess { get; set; }

        /// <summary>
        /// Raw BG3 SpellFail formula (effects on failed hit/miss).
        /// </summary>
        public string BG3SpellFail { get; set; }

        /// <summary>
        /// BG3 spell flags as a set (IsAttack, IsMelee, IsHarmful, IsConcentration, etc).
        /// Parsed from semicolon-separated BG3 flag string.
        /// </summary>
        public HashSet<string> BG3Flags { get; set; } = new();

        /// <summary>
        /// Verbal intent for AI decision making.
        /// Indicates the primary purpose of this ability.
        /// </summary>
        public VerbalIntent Intent { get; set; } = VerbalIntent.Unknown;

        /// <summary>
        /// BG3 requirement conditions formula.
        /// Example: "not Dead() and not Downed()".
        /// </summary>
        public string BG3RequirementConditions { get; set; }

        /// <summary>
        /// BG3 target conditions formula.
        /// Example: "Character() and not Ally()".
        /// </summary>
        public string BG3TargetConditions { get; set; }

        /// <summary>
        /// Number of projectiles spawned (for Projectile-type spells).
        /// </summary>
        public int ProjectileCount { get; set; } = 1;

        /// <summary>
        /// Tooltip damage list from BG3 (for display purposes).
        /// Example: "DealDamage(1d8,Fire)".
        /// </summary>
        public string TooltipDamageList { get; set; }

        /// <summary>
        /// Tooltip attack/save type from BG3.
        /// Example: "MeleeSpellAttack", "Constitution".
        /// </summary>
        public string TooltipAttackSave { get; set; }

        /// <summary>
        /// Reference back to source BG3 spell ID for debugging/lookups.
        /// </summary>
        public string BG3SourceId { get; set; }

        /// <summary>
        /// Whether this action is a summon action (spawns creatures/objects).
        /// Summon actions are forbidden in canonical parity scenarios.
        /// </summary>
        public bool IsSummon { get; set; }
    }

    /// <summary>
    /// Types of attacks.
    /// </summary>
    public enum AttackType
    {
        MeleeWeapon,
        RangedWeapon,
        MeleeSpell,
        RangedSpell
    }

    /// <summary>
    /// Requirement to use an action.
    /// </summary>
    public class ActionRequirement
    {
        public string Type { get; set; }            // "weapon", "status", "resource", etc.
        public string Value { get; set; }           // Specific value to check
        public bool Inverted { get; set; }          // If true, requirement must NOT be met
    }

    /// <summary>
    /// Definition of an effect within an action.
    /// </summary>
    public class EffectDefinition
    {
        public string Type { get; set; }            // "damage", "heal", "apply_status", etc.

        /// <summary>
        /// Primary value (damage amount, heal amount, etc).
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Dice to roll for value (e.g., "2d6", "1d8+3").
        /// </summary>
        public string DiceFormula { get; set; }

        /// <summary>
        /// Damage/heal type.
        /// </summary>
        public string DamageType { get; set; }

        /// <summary>
        /// Status to apply (for apply_status effect).
        /// </summary>
        public string StatusId { get; set; }
        public int StatusDuration { get; set; }
        public int StatusStacks { get; set; } = 1;

        /// <summary>
        /// Target override (if different from ability target).
        /// </summary>
        public EffectTargetType TargetType { get; set; } = EffectTargetType.AbilityTarget;

        /// <summary>
        /// Condition for effect to apply.
        /// </summary>
        public string Condition { get; set; }       // "on_hit", "on_crit", "on_save_fail", etc.

        /// <summary>
        /// If true, targets that succeed on their saving throw take half damage instead of no damage.
        /// Used by many AoE spells like Fireball, Shatter, Lightning Bolt.
        /// </summary>
        public bool SaveTakesHalf { get; set; }

        /// <summary>
        /// Scaling factors.
        /// </summary>
        public Dictionary<string, float> Scaling { get; set; } = new();

        /// <summary>
        /// Extra effect-specific parameters.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
    }

    /// <summary>
    /// Target type for individual effects.
    /// </summary>
    public enum EffectTargetType
    {
        AbilityTarget,      // Use ability's resolved targets
        Self,               // Always the caster
        AllInArea,          // All units in AoE
        AlliesInArea,       // Only allies in AoE
        EnemiesInArea       // Only enemies in AoE
    }
}
