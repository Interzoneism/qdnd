using System.Collections.Generic;

namespace QDND.Data.Stats
{
    /// <summary>
    /// Complete BG3 character stat block data model parsed from Character.txt.
    /// Represents a single character entry with all mechanical properties including
    /// ability scores, resistances, action resources, and progression data.
    /// </summary>
    public class BG3CharacterData
    {
        // --- Core Identity ---

        /// <summary>Unique character entry name (e.g., "_Base", "Human_Melee", "POC_Player_Fighter").</summary>
        public string Name { get; set; }

        /// <summary>Parent entry this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }

        // --- Ability Scores ---

        /// <summary>Strength ability score (default 10).</summary>
        public int Strength { get; set; }

        /// <summary>Dexterity ability score (default 10).</summary>
        public int Dexterity { get; set; }

        /// <summary>Constitution ability score (default 10).</summary>
        public int Constitution { get; set; }

        /// <summary>Intelligence ability score (default 10).</summary>
        public int Intelligence { get; set; }

        /// <summary>Wisdom ability score (default 10).</summary>
        public int Wisdom { get; set; }

        /// <summary>Charisma ability score (default 10).</summary>
        public int Charisma { get; set; }

        // --- Combat Stats ---

        /// <summary>Character level.</summary>
        public int Level { get; set; }

        /// <summary>Base armor class value.</summary>
        public int Armor { get; set; }

        /// <summary>Armor type worn (e.g., "None", "Leather", "ScaleMail", "Plate").</summary>
        public string ArmorType { get; set; }

        /// <summary>Hit points / vitality.</summary>
        public int Vitality { get; set; }

        /// <summary>Initiative bonus.</summary>
        public int Initiative { get; set; }

        // --- Resources ---

        /// <summary>
        /// Semicolon-separated action resource grants.
        /// Example: "ActionPoint:1;BonusActionPoint:1;Movement:9;ReactionActionPoint:1"
        /// </summary>
        public string ActionResources { get; set; }

        /// <summary>
        /// Semicolon-separated passive abilities.
        /// Example: "AttackOfOpportunity;DarknessRules"
        /// </summary>
        public string Passives { get; set; }

        /// <summary>
        /// Default boosts applied to this character.
        /// Example: "BlockRegainHP(Undead;Construct)"
        /// </summary>
        public string DefaultBoosts { get; set; }

        // --- Resistances (13 damage types) ---

        /// <summary>Acid damage resistance flags (e.g., "Resistant", "Immune", "Vulnerable").</summary>
        public string AcidResistance { get; set; }

        /// <summary>Bludgeoning damage resistance flags.</summary>
        public string BludgeoningResistance { get; set; }

        /// <summary>Cold damage resistance flags.</summary>
        public string ColdResistance { get; set; }

        /// <summary>Fire damage resistance flags.</summary>
        public string FireResistance { get; set; }

        /// <summary>Force damage resistance flags.</summary>
        public string ForceResistance { get; set; }

        /// <summary>Lightning damage resistance flags.</summary>
        public string LightningResistance { get; set; }

        /// <summary>Necrotic damage resistance flags.</summary>
        public string NecroticResistance { get; set; }

        /// <summary>Piercing damage resistance flags.</summary>
        public string PiercingResistance { get; set; }

        /// <summary>Poison damage resistance flags.</summary>
        public string PoisonResistance { get; set; }

        /// <summary>Psychic damage resistance flags.</summary>
        public string PsychicResistance { get; set; }

        /// <summary>Radiant damage resistance flags.</summary>
        public string RadiantResistance { get; set; }

        /// <summary>Slashing damage resistance flags.</summary>
        public string SlashingResistance { get; set; }

        /// <summary>Thunder damage resistance flags.</summary>
        public string ThunderResistance { get; set; }

        // --- Proficiency & Casting ---

        /// <summary>
        /// Semicolon-separated proficiency groups.
        /// Example: "SimpleWeapons;MartialWeapons;MediumArmor;HeavyArmor;LightArmor;Shields"
        /// </summary>
        public string ProficiencyGroup { get; set; }

        /// <summary>Proficiency bonus value override (if set).</summary>
        public int ProficiencyBonus { get; set; }

        /// <summary>
        /// Primary spellcasting ability.
        /// Example: "Intelligence", "Wisdom", "Charisma"
        /// </summary>
        public string SpellCastingAbility { get; set; }

        /// <summary>Ability used for unarmed melee attacks (default "Strength").</summary>
        public string UnarmedAttackAbility { get; set; }

        /// <summary>Ability used for unarmed ranged attacks.</summary>
        public string UnarmedRangedAttackAbility { get; set; }

        // --- Class & Progression ---

        /// <summary>Character class name (e.g., "Fighter", "Wizard", "Cleric").</summary>
        public string Class { get; set; }

        /// <summary>
        /// Semicolon-separated progression UUIDs or descriptors.
        /// Links to ProgressionDescriptions.lsx data.
        /// </summary>
        public string Progressions { get; set; }

        /// <summary>Progression type identifier.</summary>
        public string ProgressionType { get; set; }

        // --- Difficulty & Status ---

        /// <summary>
        /// Difficulty-dependent statuses applied to this character.
        /// Example: "STATUS_EASY: HEALTHREDUCTION_EASYMODE; STATUS_HARD: HEALTHBOOST_HARDCORE"
        /// </summary>
        public string DifficultyStatuses { get; set; }

        /// <summary>
        /// Semicolon-separated status IDs this character is immune to.
        /// Example: "SILENCED;SG_Condition;BLEEDING;BURNING"
        /// </summary>
        public string PersonalStatusImmunities { get; set; }

        // --- Perception ---

        /// <summary>Sight range in centimetres.</summary>
        public int Sight { get; set; }

        /// <summary>Hearing range in centimetres.</summary>
        public int Hearing { get; set; }

        /// <summary>Field of view in degrees.</summary>
        public int FOV { get; set; }

        /// <summary>Darkvision range in centimetres (0 = none).</summary>
        public string DarkvisionRange { get; set; }

        /// <summary>Minimum detection range.</summary>
        public string MinimumDetectionRange { get; set; }

        // --- Misc ---

        /// <summary>Character weight in kg.</summary>
        public float Weight { get; set; }

        /// <summary>XP reward UUID on death.</summary>
        public string XPReward { get; set; }

        /// <summary>Attribute flags (e.g., "ObscurityWithoutSneaking").</summary>
        public string Flags { get; set; }

        /// <summary>Footstep type for audio.</summary>
        public string StepsType { get; set; }

        /// <summary>Path influence weights for AI pathfinding.</summary>
        public string PathInfluence { get; set; }

        /// <summary>UUID for proficiency bonus scaling table.</summary>
        public string ProficiencyBonusScaling { get; set; }

        /// <summary>
        /// All raw key-value properties from the data file.
        /// Includes every field even if not mapped to a strongly-typed property.
        /// </summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();

        /// <summary>
        /// Returns a summary string with the character name, class, and level.
        /// </summary>
        public override string ToString()
        {
            var cls = !string.IsNullOrEmpty(Class) ? $" ({Class})" : "";
            return $"{Name}{cls} Lv{Level} STR:{Strength} DEX:{Dexterity} CON:{Constitution} INT:{Intelligence} WIS:{Wisdom} CHA:{Charisma}";
        }
    }
}
