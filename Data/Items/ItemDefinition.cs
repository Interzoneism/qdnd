namespace QDND.Data.Items
{
    /// <summary>
    /// Resolved item definition that bridges BG3 object data to runtime item usage.
    /// </summary>
    public class ItemDefinition
    {
        // Identity
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }

        // Classification
        public ItemUseCategory UseCategory { get; set; }
        public string Rarity { get; set; }
        public string ObjectCategory { get; set; }
        public string InventoryTab { get; set; }

        // Physical
        public float Weight { get; set; }
        public int ValueLevel { get; set; }

        // Usage
        public string UseCosts { get; set; }
        public string UseConditions { get; set; }
        public bool IsConsumable { get; set; }
        public int MaxStackSize { get; set; }

        // Effect resolution
        public string UseActionId { get; set; }
        public string LinkedSpellId { get; set; }
        public string LinkedStatusId { get; set; }
        public string HealingFormula { get; set; }

        // Presentation
        public string IconPath { get; set; }

        // BG3 metadata
        public string Boosts { get; set; }
        public string DefaultBoosts { get; set; }
        public string PassivesOnEquip { get; set; }
        public string StatusOnEquip { get; set; }
    }

    public enum ItemUseCategory
    {
        None,
        Potion,
        Scroll,
        Throwable,
        Consumable,
        Arrow,
        Grenade,
        Common
    }
}