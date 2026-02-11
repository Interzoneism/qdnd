using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Abilities
{
    /// <summary>
    /// Defines a variant of an ability, allowing different versions
    /// with modified damage types, effects, or costs.
    /// Examples: Chromatic Orb (fire/cold/lightning), Elemental Weapon
    /// </summary>
    public class AbilityVariant
    {
        /// <summary>
        /// Unique identifier for this variant (e.g., "fire_chromatic_orb").
        /// </summary>
        public string VariantId { get; set; }

        /// <summary>
        /// Display name shown in UI (e.g., "Fire").
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Description text for this variant.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Icon identifier for this variant (if different from base ability).
        /// </summary>
        public string Icon { get; set; }

        /// <summary>
        /// Replace the base damage type with this type.
        /// If null, base damage type is preserved.
        /// </summary>
        public string ReplaceDamageType { get; set; }

        /// <summary>
        /// Add flat damage to base effects.
        /// </summary>
        public int AdditionalDamage { get; set; }

        /// <summary>
        /// Add additional dice to damage rolls (e.g., "1d6").
        /// Parsed and added to base dice formulas.
        /// </summary>
        public string AdditionalDice { get; set; }

        /// <summary>
        /// Replace applied status with this status ID.
        /// If null, original status is preserved.
        /// </summary>
        public string ReplaceStatusId { get; set; }

        /// <summary>
        /// Additional cost for this variant (on top of base cost).
        /// </summary>
        public AbilityCost AdditionalCost { get; set; }

        /// <summary>
        /// Additional effects appended to the base ability effects.
        /// </summary>
        public List<EffectDefinition> AdditionalEffects { get; set; } = new();

        /// <summary>
        /// Tags to add to the ability when using this variant.
        /// </summary>
        public HashSet<string> AdditionalTags { get; set; } = new();

        /// <summary>
        /// Tags to remove from the ability when using this variant.
        /// </summary>
        public HashSet<string> RemoveTags { get; set; } = new();

        /// <summary>
        /// VFX override for this variant.
        /// </summary>
        public string VfxId { get; set; }

        /// <summary>
        /// SFX override for this variant.
        /// </summary>
        public string SfxId { get; set; }

        /// <summary>
        /// Override the action type (e.g., "bonus" for Quickened Spell metamagic).
        /// Valid values: "action", "bonus", "reaction"
        /// If null, uses the base ability's action type.
        /// </summary>
        public string ActionTypeOverride { get; set; }

        /// <summary>
        /// Override the maximum number of targets (for Twinned Spell metamagic).
        /// If null, uses the base ability's max targets.
        /// </summary>
        public int? MaxTargetsOverride { get; set; }

        /// <summary>
        /// Override the target type (for metamagic effects that change targeting).
        /// If null, uses the base ability's target type.
        /// </summary>
        public string TargetTypeOverride { get; set; }
    }

    /// <summary>
    /// Configuration for upcast scaling of an ability.
    /// </summary>
    public class UpcastScaling
    {
        /// <summary>
        /// Resource key for upcast cost (e.g., "spell_slot").
        /// The upcast level determines the cost multiplier.
        /// </summary>
        public string ResourceKey { get; set; } = "spell_slot";

        /// <summary>
        /// Base cost at minimum cast level.
        /// </summary>
        public int BaseCost { get; set; } = 1;

        /// <summary>
        /// Cost increase per upcast level.
        /// </summary>
        public int CostPerLevel { get; set; } = 1;

        /// <summary>
        /// Additional dice per upcast level (e.g., "1d8" per level).
        /// </summary>
        public string DicePerLevel { get; set; }

        /// <summary>
        /// Flat damage increase per upcast level.
        /// </summary>
        public int DamagePerLevel { get; set; }

        /// <summary>
        /// Additional targets per upcast level.
        /// </summary>
        public int TargetsPerLevel { get; set; }

        /// <summary>
        /// Additional duration (in turns) per upcast level.
        /// </summary>
        public int DurationPerLevel { get; set; }

        /// <summary>
        /// Maximum upcast level allowed (0 = no limit except resources).
        /// </summary>
        public int MaxUpcastLevel { get; set; } = 9;
    }

    /// <summary>
    /// Options passed when executing an ability with variant/upcast.
    /// </summary>
    public class AbilityExecutionOptions
    {
        /// <summary>
        /// The variant ID to use, or null for base ability.
        /// </summary>
        public string VariantId { get; set; }

        /// <summary>
        /// The upcast level (0 = base level, 1+ = upcast).
        /// </summary>
        public int UpcastLevel { get; set; }

        /// <summary>
        /// Optional point target for area/point-targeted abilities.
        /// </summary>
        public Vector3? TargetPosition { get; set; }

        /// <summary>
        /// If true, skip cost validation and consumption (used for Extra Attack).
        /// </summary>
        public bool SkipCostValidation { get; set; }

        /// <summary>
        /// Create default options (no variant, no upcast).
        /// </summary>
        public static AbilityExecutionOptions Default => new()
        {
            VariantId = null,
            UpcastLevel = 0,
            TargetPosition = null,
            SkipCostValidation = false
        };
    }
}
