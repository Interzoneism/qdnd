using System.Collections.Generic;

namespace QDND.Data.Stats
{
    /// <summary>
    /// Complete BG3 object data model parsed from Object.txt.
    /// Represents a single object entry (potions, scrolls, containers, world objects)
    /// with all mechanical properties including vitality, resistances, and on-equip effects.
    /// </summary>
    public class BG3ObjectData
    {
        // --- Core Identity ---

        /// <summary>Unique object entry name (e.g., "OBJ_Potion_Healing", "_BaseItem").</summary>
        public string Name { get; set; }

        /// <summary>Parent entry this inherits from (via "using" directive).</summary>
        public string ParentId { get; set; }

        // --- Object Stats ---

        /// <summary>Hit points of the object (-1 means invulnerable).</summary>
        public int Vitality { get; set; }

        /// <summary>Item weight in kg.</summary>
        public float Weight { get; set; }

        // --- Value ---

        /// <summary>Value level for pricing calculation.</summary>
        public int ValueLevel { get; set; }

        /// <summary>Value override (if set, replaces computed value).</summary>
        public int ValueOverride { get; set; }

        /// <summary>Supply value for camp supplies.</summary>
        public int SupplyValue { get; set; }

        // --- Classification ---

        /// <summary>Item rarity (e.g., "Common", "Uncommon", "Rare", "VeryRare", "Legendary").</summary>
        public string Rarity { get; set; }

        /// <summary>Object category classification.</summary>
        public string ObjectCategory { get; set; }

        /// <summary>Attribute flags (e.g., "Grounded", "InvulnerableAndInteractive;Grounded").</summary>
        public string Flags { get; set; }

        /// <summary>Inventory tab where this item appears.</summary>
        public string InventoryTab { get; set; }

        /// <summary>GUID reference to the root template.</summary>
        public string RootTemplate { get; set; }

        /// <summary>Equipment slot.</summary>
        public string Slot { get; set; }

        // --- Damage Resistances ---

        /// <summary>Resistance to physical (untyped) damage.</summary>
        public string PhysicalResistance { get; set; }

        /// <summary>Resistance to acid damage.</summary>
        public string AcidResistance { get; set; }

        /// <summary>Resistance to bludgeoning damage.</summary>
        public string BludgeoningResistance { get; set; }

        /// <summary>Resistance to cold damage.</summary>
        public string ColdResistance { get; set; }

        /// <summary>Resistance to fire damage.</summary>
        public string FireResistance { get; set; }

        /// <summary>Resistance to force damage.</summary>
        public string ForceResistance { get; set; }

        /// <summary>Resistance to lightning damage.</summary>
        public string LightningResistance { get; set; }

        /// <summary>Resistance to necrotic damage.</summary>
        public string NecroticResistance { get; set; }

        /// <summary>Resistance to piercing damage.</summary>
        public string PiercingResistance { get; set; }

        /// <summary>Resistance to poison damage.</summary>
        public string PoisonResistance { get; set; }

        /// <summary>Resistance to psychic damage.</summary>
        public string PsychicResistance { get; set; }

        /// <summary>Resistance to radiant damage.</summary>
        public string RadiantResistance { get; set; }

        /// <summary>Resistance to slashing damage.</summary>
        public string SlashingResistance { get; set; }

        /// <summary>Resistance to thunder damage.</summary>
        public string ThunderResistance { get; set; }

        // --- Misc Properties ---

        /// <summary>Game size classification (e.g., "Large", "Tiny").</summary>
        public string GameSize { get; set; }

        /// <summary>Item level.</summary>
        public int Level { get; set; }

        /// <summary>Action point costs to use (e.g., "ActionPoint:1").</summary>
        public string UseCosts { get; set; }

        /// <summary>Spells granted by this object.</summary>
        public string Spells { get; set; }

        /// <summary>General boosts granted by this object.</summary>
        public string Boosts { get; set; }

        /// <summary>Default baseline boosts.</summary>
        public string DefaultBoosts { get; set; }

        /// <summary>Status effect immunities (semicolon-separated).</summary>
        public string PersonalStatusImmunities { get; set; }

        /// <summary>Object AC value.</summary>
        public int Armor { get; set; }

        /// <summary>Item use classification (e.g., "None").</summary>
        public string ItemUseType { get; set; }

        /// <summary>Conditions required to use this item.</summary>
        public string UseConditions { get; set; }

        /// <summary>Passives granted when equipped.</summary>
        public string PassivesOnEquip { get; set; }

        /// <summary>Status effect applied when equipped.</summary>
        public string StatusOnEquip { get; set; }

        /// <summary>Extra properties string.</summary>
        public string ExtraProperties { get; set; }

        /// <summary>Whether the item needs identification.</summary>
        public string NeedsIdentification { get; set; }

        /// <summary>Whether this is a unique item.</summary>
        public int Unique { get; set; }

        // --- Loot Generation ---

        /// <summary>Minimum drop amount.</summary>
        public int MinAmount { get; set; }

        /// <summary>Maximum drop amount.</summary>
        public int MaxAmount { get; set; }

        /// <summary>Loot priority.</summary>
        public int Priority { get; set; }

        /// <summary>Minimum level for loot generation.</summary>
        public int MinLevel { get; set; }

        /// <summary>Maximum level for loot generation.</summary>
        public int MaxLevel { get; set; }

        // --- Raw Data ---

        /// <summary>
        /// All raw key-value properties from the data file.
        /// Includes every field even if not mapped to a strongly-typed property.
        /// </summary>
        public Dictionary<string, string> RawProperties { get; set; } = new();

        /// <summary>
        /// Returns a summary string with object name and key stats.
        /// </summary>
        public override string ToString()
        {
            var cat = !string.IsNullOrEmpty(ObjectCategory) ? $" [{ObjectCategory}]" : "";
            var vit = Vitality != 0 ? $" HP:{Vitality}" : "";
            return $"{Name}{cat}{vit}";
        }
    }
}
