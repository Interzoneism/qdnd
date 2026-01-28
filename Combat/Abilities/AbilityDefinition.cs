using System;
using System.Collections.Generic;

namespace QDND.Combat.Abilities
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
    /// Resource costs for using an ability.
    /// </summary>
    public class AbilityCost
    {
        public bool UsesAction { get; set; } = true;
        public bool UsesBonusAction { get; set; }
        public bool UsesReaction { get; set; }
        public int MovementCost { get; set; }
        public Dictionary<string, int> ResourceCosts { get; set; } = new(); // e.g., "mana": 10
    }

    /// <summary>
    /// Cooldown configuration for an ability.
    /// </summary>
    public class AbilityCooldown
    {
        public int TurnCooldown { get; set; }       // Cooldown in turns
        public int RoundCooldown { get; set; }      // Cooldown in rounds
        public int MaxCharges { get; set; } = 1;    // Max charges/uses
        public bool ResetsOnCombatEnd { get; set; } = true;
    }

    /// <summary>
    /// Definition of an ability (data-driven).
    /// </summary>
    public class AbilityDefinition
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
        public int MaxTargets { get; set; } = 1;            // Max targets for multi

        /// <summary>
        /// Costs to use.
        /// </summary>
        public AbilityCost Cost { get; set; } = new();

        /// <summary>
        /// Cooldown configuration.
        /// </summary>
        public AbilityCooldown Cooldown { get; set; } = new();

        /// <summary>
        /// Requirements to use (weapon type, stance, etc).
        /// </summary>
        public List<AbilityRequirement> Requirements { get; set; } = new();

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
        /// AI hints for ability usage.
        /// </summary>
        public float AIBaseDesirability { get; set; } = 1.0f;
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
    /// Requirement to use an ability.
    /// </summary>
    public class AbilityRequirement
    {
        public string Type { get; set; }            // "weapon", "status", "resource", etc.
        public string Value { get; set; }           // Specific value to check
        public bool Inverted { get; set; }          // If true, requirement must NOT be met
    }

    /// <summary>
    /// Definition of an effect within an ability.
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
