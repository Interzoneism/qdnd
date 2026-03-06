using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QDND.Data.Validation
{
    /// <summary>
    /// Configuration for a spell verification scenario. Agents create these to specify
    /// optimal test parameters for each spell/action being verified.
    /// </summary>
    public class SpellVerificationSetup
    {
        /// <summary>Required. The action/spell ID to test (e.g. "fireball", "magic_missile").</summary>
        [JsonPropertyName("action_id")]
        public string ActionId { get; set; }

        /// <summary>Caster class ID (e.g. "wizard", "cleric"). Null = auto-determine from action tags.</summary>
        [JsonPropertyName("caster_class")]
        public string CasterClass { get; set; }

        /// <summary>Caster subclass ID. Null = none.</summary>
        [JsonPropertyName("caster_subclass")]
        public string CasterSubclass { get; set; }

        /// <summary>Caster level (1-12). Default 5.</summary>
        [JsonPropertyName("caster_level")]
        public int CasterLevel { get; set; } = 5;

        /// <summary>Caster race ID. Null = random.</summary>
        [JsonPropertyName("caster_race")]
        public string CasterRace { get; set; }

        /// <summary>Caster subrace ID. Null = none.</summary>
        [JsonPropertyName("caster_subrace")]
        public string CasterSubrace { get; set; }

        /// <summary>Override ability scores [STR, DEX, CON, INT, WIS, CHA]. Null = standard array assigned by class priority.</summary>
        [JsonPropertyName("ability_scores")]
        public int[] AbilityScores { get; set; }

        /// <summary>Main hand weapon override. Null = auto-equip by class/action.</summary>
        [JsonPropertyName("main_hand_weapon")]
        public string MainHandWeapon { get; set; }

        /// <summary>Off-hand weapon/shield override. Null = auto.</summary>
        [JsonPropertyName("off_hand")]
        public string OffHand { get; set; }

        /// <summary>Armor override. Null = auto-equip by class.</summary>
        [JsonPropertyName("armor")]
        public string Armor { get; set; }

        /// <summary>Number of targets (1-6). Default 1. Use 3+ for AoE verification.</summary>
        [JsonPropertyName("target_count")]
        public int TargetCount { get; set; } = 1;

        /// <summary>Target formation: "single", "line", "cluster", "spread". Default "single".</summary>
        [JsonPropertyName("target_formation")]
        public string TargetFormation { get; set; } = "single";

        /// <summary>Target faction: "hostile" or "ally". Use "ally" for healing/buff spells.</summary>
        [JsonPropertyName("target_faction")]
        public string TargetFaction { get; set; } = "hostile";

        /// <summary>Distance from caster to primary target in meters. 0 = auto-determine from action range.</summary>
        [JsonPropertyName("target_distance")]
        public float TargetDistance { get; set; } = 0f;

        /// <summary>If true, caster starts at 50% HP (for self-heal testing).</summary>
        [JsonPropertyName("caster_wounded")]
        public bool CasterWounded { get; set; } = false;

        /// <summary>If true, targets start at 50% HP (for heal testing on allies).</summary>
        [JsonPropertyName("targets_wounded")]
        public bool TargetsWounded { get; set; } = false;

        /// <summary>Additional actions to grant the caster alongside the test action.</summary>
        [JsonPropertyName("additional_actions")]
        public List<string> AdditionalActions { get; set; }

        /// <summary>BG3 wiki URL for reference.</summary>
        [JsonPropertyName("bg3_wiki_url")]
        public string Bg3WikiUrl { get; set; }

        /// <summary>Expected behavior notes from BG3 wiki research.</summary>
        [JsonPropertyName("expected_behavior")]
        public SpellExpectedBehavior ExpectedBehavior { get; set; }

        /// <summary>Free-text notes explaining why this setup was chosen.</summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; }
    }

    /// <summary>
    /// Expected spell behavior from BG3 wiki, used for post-test verification.
    /// </summary>
    public class SpellExpectedBehavior
    {
        [JsonPropertyName("spell_level")]
        public int? SpellLevel { get; set; }

        [JsonPropertyName("school")]
        public string School { get; set; }

        [JsonPropertyName("damage_dice")]
        public string DamageDice { get; set; }

        [JsonPropertyName("damage_type")]
        public string DamageType { get; set; }

        [JsonPropertyName("save_type")]
        public string SaveType { get; set; }

        [JsonPropertyName("save_dc_formula")]
        public string SaveDcFormula { get; set; }

        [JsonPropertyName("half_damage_on_save")]
        public bool? HalfDamageOnSave { get; set; }

        [JsonPropertyName("requires_concentration")]
        public bool? RequiresConcentration { get; set; }

        [JsonPropertyName("area_type")]
        public string AreaType { get; set; }

        [JsonPropertyName("area_radius")]
        public float? AreaRadius { get; set; }

        [JsonPropertyName("range")]
        public float? Range { get; set; }

        [JsonPropertyName("applies_status")]
        public string AppliesStatus { get; set; }

        [JsonPropertyName("status_duration")]
        public int? StatusDuration { get; set; }

        [JsonPropertyName("healing_dice")]
        public string HealingDice { get; set; }

        [JsonPropertyName("upcast_per_level")]
        public string UpcastPerLevel { get; set; }

        [JsonPropertyName("attack_type")]
        public string AttackType { get; set; }

        [JsonPropertyName("max_targets")]
        public int? MaxTargets { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}
