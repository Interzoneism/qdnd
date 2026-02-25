using System.Collections.Generic;

namespace QDND.Data.Stats
{
    /// <summary>
    /// Complete BG3 weapon data model parsed from Weapon.txt.
    /// Represents a single weapon entry with all mechanical properties including
    /// damage dice, weapon properties, proficiency requirements, and on-equip effects.
    /// </summary>
    public class BG3WeaponData
    {
        // --- Core Identity ---

        /// <summary>Unique weapon entry name (e.g., "WPN_Longsword", "_BaseWeapon").</summary>
        public string Name { get; set; }

        /// <summary>Parent entry this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }

        // --- Damage ---

        /// <summary>
        /// Damage dice formula (e.g., "1d8", "2d6", "1d4").
        /// </summary>
        public string Damage { get; set; }

        /// <summary>
        /// Primary damage type (e.g., "Slashing", "Piercing", "Bludgeoning").
        /// </summary>
        public string DamageType { get; set; }

        /// <summary>
        /// Versatile damage dice when wielded two-handed (e.g., "1d10").
        /// Only present for weapons with the Versatile property.
        /// </summary>
        public string VersatileDamage { get; set; }

        /// <summary>Maximum damage range in centimetres for ranged/thrown weapons.</summary>
        public int DamageRange { get; set; }

        // --- Weapon Classification ---

        /// <summary>
        /// Semicolon-separated weapon property flags.
        /// Example: "Versatile;Melee;Dippable", "Finesse;Light;Thrown;Melee;Dippable"
        /// </summary>
        public string WeaponProperties { get; set; }

        /// <summary>
        /// Weapon group classification (e.g., "SimpleMeleeWeapon", "MartialRangedWeapon").
        /// </summary>
        public string WeaponGroup { get; set; }

        /// <summary>
        /// Semicolon-separated proficiency groups required.
        /// Example: "Longswords;MartialWeapons"
        /// </summary>
        public string ProficiencyGroup { get; set; }

        // --- On-Equip Effects ---

        /// <summary>
        /// Boosts granted when equipped in main hand.
        /// Example: "UnlockSpell(Target_PommelStrike);UnlockSpell(Target_Slash_New)"
        /// </summary>
        public string BoostsOnEquipMainHand { get; set; }

        /// <summary>
        /// Boosts granted when equipped in off hand.
        /// </summary>
        public string BoostsOnEquipOffHand { get; set; }

        /// <summary>
        /// General boosts (not hand-specific).
        /// </summary>
        public string Boosts { get; set; }

        /// <summary>
        /// Default boosts (inherited/baseline, e.g., WeaponEnchantment(1)).
        /// </summary>
        public string DefaultBoosts { get; set; }

        /// <summary>
        /// Passives granted when equipped in main hand.
        /// Example: "Overwhelm"
        /// </summary>
        public string PassivesMainHand { get; set; }

        /// <summary>
        /// Passives granted when equipped in off hand.
        /// </summary>
        public string PassivesOffHand { get; set; }

        /// <summary>
        /// Passives granted when equipped (any slot).
        /// </summary>
        public string PassivesOnEquip { get; set; }

        /// <summary>
        /// Status effect applied when equipped.
        /// </summary>
        public string StatusOnEquip { get; set; }

        // --- Item Properties ---

        /// <summary>Item rarity (e.g., "Common", "Uncommon", "Rare", "VeryRare", "Legendary").</summary>
        public string Rarity { get; set; }

        /// <summary>Equipment slot (e.g., "Melee Main Weapon", "Ranged Main Weapon").</summary>
        public string Slot { get; set; }

        /// <summary>Item weight in kg.</summary>
        public float Weight { get; set; }

        /// <summary>Weapon range in centimetres.</summary>
        public int WeaponRange { get; set; }

        /// <summary>Item level.</summary>
        public int Level { get; set; }

        /// <summary>Action point costs to use (e.g., "ActionPoint:1").</summary>
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
        /// Semicolon-separated status IDs this weapon is immune to.
        /// Example: "SILENCED;SG_Condition;BLEEDING;BURNING"
        /// </summary>
        public string PersonalStatusImmunities { get; set; }

        // --- Misc ---

        /// <summary>Item group classification.</summary>
        public string ItemGroup { get; set; }

        /// <summary>Inventory tab where this item appears.</summary>
        public string InventoryTab { get; set; }

        /// <summary>Item colour preset.</summary>
        public string ItemColor { get; set; }

        /// <summary>Weapon functors (special on-hit effects).</summary>
        public string WeaponFunctors { get; set; }

        /// <summary>Spells granted by this weapon.</summary>
        public string Spells { get; set; }

        /// <summary>Number of charges for special abilities.</summary>
        public int Charges { get; set; }

        /// <summary>Maximum charges.</summary>
        public int MaxCharges { get; set; }

        /// <summary>Whether the item needs identification.</summary>
        public string NeedsIdentification { get; set; }

        /// <summary>Whether this is a unique item.</summary>
        public int Unique { get; set; }

        /// <summary>Extra properties string.</summary>
        public string ExtraProperties { get; set; }

        /// <summary>Attribute flags.</summary>
        public string Flags { get; set; }

        /// <summary>
        /// All raw key-value properties from the data file.
        /// Includes every field even if not mapped to a strongly-typed property.
        /// </summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();

        /// <summary>
        /// Returns a summary string with weapon name, damage, and type.
        /// </summary>
        public override string ToString()
        {
            var props = !string.IsNullOrEmpty(WeaponProperties) ? $" [{WeaponProperties}]" : "";
            return $"{Name}: {Damage} {DamageType}{props}";
        }
    }
}
