using System.Collections.Generic;

namespace QDND.Data.Stats
{
    /// <summary>
    /// Complete BG3 armor data model parsed from Armor.txt.
    /// Represents a single armor entry with all mechanical properties including
    /// armor class, armor type, ability modifier caps, and on-equip effects.
    /// </summary>
    public class BG3ArmorData
    {
        // --- Core Identity ---

        /// <summary>Unique armor entry name (e.g., "ARM_Plate_Body", "_Body").</summary>
        public string Name { get; set; }

        /// <summary>Parent entry this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }

        // --- Armor Stats ---

        /// <summary>Base armor class value (e.g., 10 for robes, 18 for plate).</summary>
        public int ArmorClass { get; set; }

        /// <summary>
        /// Armor type classification (e.g., "None", "Leather", "StuddedLeather",
        /// "Hide", "ChainShirt", "ScaleMail", "BreastPlate", "HalfPlate",
        /// "RingMail", "ChainMail", "Splint", "Plate", "Padded", "Cloth").
        /// </summary>
        public string ArmorType { get; set; }

        /// <summary>Whether this item is a shield ("Yes" / "No").</summary>
        public string Shield { get; set; }

        /// <summary>
        /// Ability used for AC modifier (e.g., "Dexterity", "None").
        /// Heavy armor typically uses "None" while light/medium uses "Dexterity".
        /// </summary>
        public string ArmorClassAbility { get; set; }

        /// <summary>
        /// Maximum ability modifier that can be added to AC.
        /// Typically 2 for medium armor, absent (unlimited) for light armor.
        /// </summary>
        public int AbilityModifierCap { get; set; }

        // --- Proficiency ---

        /// <summary>
        /// Proficiency group required to wear without penalty.
        /// Example: "LightArmor", "MediumArmor", "HeavyArmor", "Shields"
        /// </summary>
        public string ProficiencyGroup { get; set; }

        // --- On-Equip Effects ---

        /// <summary>
        /// Boost effects applied when worn.
        /// Example: "Disadvantage(Skill,Stealth)", "AC(2)",
        /// "IF(not HasPassive('MediumArmorMaster', context.Source)):Disadvantage(Skill,Stealth)"
        /// </summary>
        public string Boosts { get; set; }

        /// <summary>
        /// Default boosts (inherited/baseline).
        /// </summary>
        public string DefaultBoosts { get; set; }

        /// <summary>
        /// Status effect applied when equipped.
        /// </summary>
        public string StatusOnEquip { get; set; }

        /// <summary>
        /// Semicolon-separated passives granted when equipped.
        /// Example: "ARM_SuperiorPadding_1_Passive;ARM_Ambusher_1_Passive"
        /// </summary>
        public string PassivesOnEquip { get; set; }

        /// <summary>
        /// Spells granted when equipped.
        /// </summary>
        public string Spells { get; set; }

        // --- Item Properties ---

        /// <summary>Item rarity (e.g., "Common", "Uncommon", "Rare", "VeryRare", "Legendary").</summary>
        public string Rarity { get; set; }

        /// <summary>Equipment slot (e.g., "Breast", "Gloves", "Boots").</summary>
        public string Slot { get; set; }

        /// <summary>Item weight in kg.</summary>
        public float Weight { get; set; }

        /// <summary>Item level.</summary>
        public int Level { get; set; }

        /// <summary>Action point costs to equip (e.g., "ActionPoint:1").</summary>
        public string UseCosts { get; set; }

        // --- Value ---

        /// <summary>Value level for pricing calculation.</summary>
        public int ValueLevel { get; set; }

        /// <summary>Value scale multiplier.</summary>
        public float ValueScale { get; set; }

        /// <summary>Value rounding mode.</summary>
        public int ValueRounding { get; set; }

        /// <summary>Value override (if set, replaces computed value).</summary>
        public int ValueOverride { get; set; }

        /// <summary>UUID for value table entry.</summary>
        public string ValueUUID { get; set; }

        // --- Status Immunities ---

        /// <summary>
        /// Semicolon-separated status IDs this armor is immune to.
        /// </summary>
        public string PersonalStatusImmunities { get; set; }

        // --- Loot Generation ---

        /// <summary>Minimum amount for loot generation.</summary>
        public int MinAmount { get; set; }

        /// <summary>Maximum amount for loot generation.</summary>
        public int MaxAmount { get; set; }

        /// <summary>Priority for loot generation.</summary>
        public int Priority { get; set; }

        /// <summary>Minimum level requirement for loot generation.</summary>
        public int MinLevel { get; set; }

        /// <summary>Maximum level for loot generation.</summary>
        public int MaxLevel { get; set; }

        // --- Misc ---

        /// <summary>Inventory tab where this item appears.</summary>
        public string InventoryTab { get; set; }

        /// <summary>Combo category for dyeing.</summary>
        public string ComboCategory { get; set; }

        /// <summary>Number of charges for special abilities.</summary>
        public int Charges { get; set; }

        /// <summary>Durability value.</summary>
        public int Durability { get; set; }

        /// <summary>Whether the item needs identification.</summary>
        public string NeedsIdentification { get; set; }

        /// <summary>Whether this is a unique item.</summary>
        public int Unique { get; set; }

        /// <summary>Attribute flags.</summary>
        public string Flags { get; set; }

        /// <summary>Extra properties string.</summary>
        public string ExtraProperties { get; set; }

        /// <summary>
        /// All raw key-value properties from the data file.
        /// Includes every field even if not mapped to a strongly-typed property.
        /// </summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();

        /// <summary>
        /// Returns a summary string with armor name, AC, and type.
        /// </summary>
        public override string ToString()
        {
            var shield = Shield == "Yes" ? " (Shield)" : "";
            var prof = !string.IsNullOrEmpty(ProficiencyGroup) ? $" [{ProficiencyGroup}]" : "";
            return $"{Name}: AC {ArmorClass} {ArmorType}{shield}{prof}";
        }
    }
}
