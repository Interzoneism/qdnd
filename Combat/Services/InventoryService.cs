using System;
using System.Collections.Generic;
using System.Linq;
using QDND.Combat.Entities;
using QDND.Data.CharacterModel;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Categories of items for inventory slot limits.
    /// </summary>
    public enum ItemCategory
    {
        Weapon,
        Armor,
        Shield,
        Potion,
        Scroll,
        Throwable,
        Misc
    }

    /// <summary>
    /// A single item instance in the inventory.
    /// Wraps an equipment definition with stack count and metadata.
    /// </summary>
    public class InventoryItem
    {
        /// <summary>Unique instance ID for this stack.</summary>
        public string InstanceId { get; set; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>The equipment definition ID (weapon/armor ID).</summary>
        public string DefinitionId { get; set; }

        /// <summary>Display name.</summary>
        public string Name { get; set; }

        /// <summary>Item category for slot limiting.</summary>
        public ItemCategory Category { get; set; }

        /// <summary>How many in this stack (1 for equipment, >1 for consumables).</summary>
        public int Quantity { get; set; } = 1;

        /// <summary>Brief description for tooltip.</summary>
        public string Description { get; set; } = "";

        /// <summary>Weight per unit in pounds.</summary>
        public int Weight { get; set; }

        /// <summary>Optional icon path (res://).</summary>
        public string IconPath { get; set; }

        /// <summary>Reference to weapon definition if this is a weapon.</summary>
        public WeaponDefinition WeaponDef { get; set; }

        /// <summary>Reference to armor definition if this is armor/shield.</summary>
        public ArmorDefinition ArmorDef { get; set; }

        /// <summary>The action ID to execute when this item is used in combat. Null for non-usable items.</summary>
        public string UseActionId { get; set; }

        /// <summary>Whether this item is consumed on use (potions, scrolls, throwables).</summary>
        public bool IsConsumable { get; set; }

        /// <summary>Maximum stack size for this item type.</summary>
        public int MaxStackSize { get; set; } = 1;

        /// <summary>Rarity for UI coloring.</summary>
        public ItemRarity Rarity { get; set; } = ItemRarity.Common;

        /// <summary>Build a short stat line for tooltips.</summary>
        public string GetStatLine()
        {
            if (WeaponDef != null)
            {
                var props = WeaponDef.Properties != WeaponProperty.None ? $" ({WeaponDef.Properties})" : "";
                var range = WeaponDef.IsRanged ? $" Range {WeaponDef.NormalRange}/{WeaponDef.LongRange}ft" : "";
                return $"{WeaponDef.DamageDice} {WeaponDef.DamageType}{props}{range}";
            }
            if (ArmorDef != null)
            {
                var dex = ArmorDef.MaxDexBonus.HasValue ? $" (max DEX +{ArmorDef.MaxDexBonus})" : " (+DEX)";
                var stealth = ArmorDef.StealthDisadvantage ? " Stealth Disadv." : "";
                return $"AC {ArmorDef.BaseAC}{dex}{stealth}";
            }
            return Description;
        }
    }

    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        VeryRare,
        Legendary
    }

    /// <summary>
    /// BG3-style equipment slot names for the paper doll.
    /// </summary>
    public enum EquipSlot
    {
        MainHand,
        OffHand,
        Armor,
        Helmet,
        Gloves,
        Boots,
        Amulet,
        Ring1,
        Ring2,
        Cloak
    }

    /// <summary>
    /// Per-combatant inventory: grid bag + equipment slots.
    /// </summary>
    public class Inventory
    {
        /// <summary>Flat list of bag items (grid inventory).</summary>
        public List<InventoryItem> BagItems { get; } = new();

        /// <summary>Equipped items by slot.</summary>
        public Dictionary<EquipSlot, InventoryItem> EquippedItems { get; } = new();

        /// <summary>Max bag slots (grid size).</summary>
        public int MaxBagSlots { get; set; } = 40;

        /// <summary>Per-category limits (0 = unlimited).</summary>
        public Dictionary<ItemCategory, int> CategoryLimits { get; set; } = new()
        {
            { ItemCategory.Weapon, 6 },
            { ItemCategory.Armor, 4 },
            { ItemCategory.Shield, 2 },
            { ItemCategory.Potion, 10 },
            { ItemCategory.Scroll, 8 },
            { ItemCategory.Throwable, 10 },
            { ItemCategory.Misc, 20 }
        };

        /// <summary>
        /// Count items of a given category in the bag (excludes equipped).
        /// </summary>
        public int CountCategory(ItemCategory cat)
        {
            return BagItems.Where(i => i.Category == cat).Sum(i => i.Quantity);
        }

        /// <summary>
        /// Check whether an item can be added respecting category limits and bag capacity.
        /// </summary>
        public bool CanAddItem(InventoryItem item)
        {
            if (BagItems.Count >= MaxBagSlots) return false;
            if (CategoryLimits.TryGetValue(item.Category, out int limit) && limit > 0)
                return CountCategory(item.Category) + item.Quantity <= limit;
            return true;
        }

        /// <summary>Add an item to the bag.</summary>
        public bool AddItem(InventoryItem item)
        {
            if (!CanAddItem(item)) return false;
            BagItems.Add(item);
            return true;
        }

        /// <summary>Remove an item from the bag by instance ID.</summary>
        public bool RemoveItem(string instanceId)
        {
            var item = BagItems.FirstOrDefault(i => i.InstanceId == instanceId);
            if (item == null) return false;
            return BagItems.Remove(item);
        }

        /// <summary>Get an item from the bag by instance ID.</summary>
        public InventoryItem GetItem(string instanceId)
        {
            return BagItems.FirstOrDefault(i => i.InstanceId == instanceId);
        }

        /// <summary>Equip an item from the bag to the given slot. Returns any previously equipped item (returned to bag).</summary>
        public InventoryItem Equip(string instanceId, EquipSlot slot)
        {
            var item = BagItems.FirstOrDefault(i => i.InstanceId == instanceId);
            if (item == null) return null;

            InventoryItem previous = null;
            if (EquippedItems.TryGetValue(slot, out var existing))
            {
                previous = existing;
                BagItems.Add(previous); // Return old item to bag
            }

            BagItems.Remove(item);
            EquippedItems[slot] = item;
            return previous;
        }

        /// <summary>Unequip an item from a slot back to the bag.</summary>
        public bool Unequip(EquipSlot slot)
        {
            if (!EquippedItems.TryGetValue(slot, out var item)) return false;
            EquippedItems.Remove(slot);
            BagItems.Add(item);
            return true;
        }

        /// <summary>Get the equipped item in a slot, or null.</summary>
        public InventoryItem GetEquipped(EquipSlot slot)
        {
            return EquippedItems.GetValueOrDefault(slot);
        }
    }

    /// <summary>
    /// Central inventory service managing inventories for all combatants.
    /// </summary>
    public class InventoryService
    {
        private readonly Dictionary<string, Inventory> _inventories = new();
        private readonly CharacterDataRegistry _charRegistry;

        /// <summary>Fired when an inventory changes (combatantId).</summary>
        public event Action<string> OnInventoryChanged;

        /// <summary>Fired when equipment changes (combatantId, slot).</summary>
        public event Action<string, EquipSlot> OnEquipmentChanged;

        public InventoryService(CharacterDataRegistry charRegistry)
        {
            _charRegistry = charRegistry;
        }

        /// <summary>Get or create inventory for a combatant.</summary>
        public Inventory GetInventory(string combatantId)
        {
            if (!_inventories.TryGetValue(combatantId, out var inv))
            {
                inv = new Inventory();
                _inventories[combatantId] = inv;
            }
            return inv;
        }

        /// <summary>
        /// Initialize a combatant's inventory from their current equipment loadout.
        /// Populates equipment slots and adds some starter bag items.
        /// </summary>
        public void InitializeFromCombatant(Combatant combatant)
        {
            if (combatant == null) return;
            var inv = GetInventory(combatant.Id);

            // Equip current weapons/armor into slots
            if (combatant.MainHandWeapon != null)
            {
                var item = CreateWeaponItem(combatant.MainHandWeapon);
                inv.EquippedItems[EquipSlot.MainHand] = item;
            }
            if (combatant.OffHandWeapon != null)
            {
                var item = CreateWeaponItem(combatant.OffHandWeapon);
                inv.EquippedItems[EquipSlot.OffHand] = item;
            }
            if (combatant.HasShield && combatant.Equipment?.ShieldId != null)
            {
                var shieldDef = _charRegistry?.GetArmor(combatant.Equipment.ShieldId);
                if (shieldDef != null)
                {
                    var item = CreateArmorItem(shieldDef, ItemCategory.Shield);
                    inv.EquippedItems[EquipSlot.OffHand] = item;
                }
            }
            if (combatant.EquippedArmor != null)
            {
                var item = CreateArmorItem(combatant.EquippedArmor, ItemCategory.Armor);
                inv.EquippedItems[EquipSlot.Armor] = item;
            }

            // Add some starter items to the bag based on class/role
            AddStarterBagItems(combatant, inv);
        }

        /// <summary>Equip an item and apply stat changes to the combatant.</summary>
        public bool EquipItem(Combatant combatant, string instanceId, EquipSlot slot)
        {
            if (combatant == null) return false;
            var inv = GetInventory(combatant.Id);
            var previous = inv.Equip(instanceId, slot);
            ApplyEquipment(combatant, inv);
            OnEquipmentChanged?.Invoke(combatant.Id, slot);
            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>Unequip a slot and revert stat changes.</summary>
        public bool UnequipItem(Combatant combatant, EquipSlot slot)
        {
            if (combatant == null) return false;
            var inv = GetInventory(combatant.Id);
            if (!inv.Unequip(slot)) return false;
            ApplyEquipment(combatant, inv);
            OnEquipmentChanged?.Invoke(combatant.Id, slot);
            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>Add an item to a combatant's bag.</summary>
        public bool AddItemToBag(Combatant combatant, InventoryItem item)
        {
            if (combatant == null) return false;
            var inv = GetInventory(combatant.Id);
            if (!inv.AddItem(item)) return false;
            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>
        /// Apply current equipped items to the combatant's stats.
        /// Updates weapon, armor, shield, and AC.
        /// </summary>
        public void ApplyEquipment(Combatant combatant, Inventory inv)
        {
            // Main hand
            var mainHand = inv.GetEquipped(EquipSlot.MainHand);
            combatant.MainHandWeapon = mainHand?.WeaponDef;

            // Off hand - weapon OR shield
            var offHand = inv.GetEquipped(EquipSlot.OffHand);
            if (offHand?.WeaponDef != null)
            {
                combatant.OffHandWeapon = offHand.WeaponDef;
                combatant.HasShield = false;
            }
            else if (offHand?.ArmorDef != null && offHand.Category == ItemCategory.Shield)
            {
                combatant.OffHandWeapon = null;
                combatant.HasShield = true;
            }
            else
            {
                combatant.OffHandWeapon = null;
                combatant.HasShield = false;
            }

            // Armor
            var armor = inv.GetEquipped(EquipSlot.Armor);
            combatant.EquippedArmor = armor?.ArmorDef;

            // Recalculate AC
            RecalculateAC(combatant);
        }

        /// <summary>Recalculate AC from equipped armor + shield + DEX.</summary>
        private void RecalculateAC(Combatant combatant)
        {
            if (combatant.Stats == null) return;
            int dexMod = (combatant.Stats.Dexterity - 10) / 2;
            int ac;

            if (combatant.EquippedArmor != null)
            {
                var armor = combatant.EquippedArmor;
                int dexBonus = armor.MaxDexBonus.HasValue
                    ? Math.Min(dexMod, armor.MaxDexBonus.Value)
                    : dexMod;
                ac = armor.BaseAC + dexBonus;
            }
            else
            {
                ac = 10 + dexMod; // Unarmored
            }

            if (combatant.HasShield)
                ac += 2;

            combatant.Stats.BaseAC = ac;
        }

        // ── Item Creation Helpers ──────────────────────────────────

        public static InventoryItem CreateWeaponItem(WeaponDefinition wep)
        {
            return new InventoryItem
            {
                DefinitionId = wep.Id,
                Name = wep.Name,
                Category = ItemCategory.Weapon,
                Weight = wep.Weight,
                WeaponDef = wep,
                Description = $"{wep.DamageDice} {wep.DamageType} - {wep.Category} {wep.WeaponType}"
            };
        }

        public static InventoryItem CreateArmorItem(ArmorDefinition armor, ItemCategory category)
        {
            return new InventoryItem
            {
                DefinitionId = armor.Id,
                Name = armor.Name,
                Category = category,
                Weight = armor.Weight,
                ArmorDef = armor,
                Description = $"AC {armor.BaseAC} - {armor.Category}"
            };
        }

        public static InventoryItem CreateConsumableItem(string id, string name, ItemCategory category,
            string description, int quantity = 1, string useActionId = null, bool isConsumable = true, int maxStackSize = 20)
        {
            return new InventoryItem
            {
                DefinitionId = id,
                Name = name,
                Category = category,
                Quantity = quantity,
                Description = description,
                UseActionId = useActionId,
                IsConsumable = isConsumable,
                MaxStackSize = maxStackSize
            };
        }

        /// <summary>
        /// Check if an item can be used in combat.
        /// </summary>
        public (bool canUse, string reason) CanUseItem(Combatant combatant, string instanceId)
        {
            if (combatant == null)
                return (false, "Combatant does not exist.");

            var inv = GetInventory(combatant.Id);
            var item = inv.GetItem(instanceId);

            if (item == null)
                return (false, "Item not found in inventory.");
            if (string.IsNullOrEmpty(item.UseActionId))
                return (false, "This item cannot be used in combat.");
            if (item.Quantity <= 0)
                return (false, "No charges remaining.");

            return (true, null);
        }

        /// <summary>
        /// Consume one charge of an item. Decrements quantity, removes if 0.
        /// Returns the UseActionId for the consumed item.
        /// </summary>
        public string ConsumeItem(Combatant combatant, string instanceId)
        {
            if (combatant == null) return null;

            var inv = GetInventory(combatant.Id);
            var item = inv.GetItem(instanceId);
            if (item == null || string.IsNullOrEmpty(item.UseActionId) || item.Quantity <= 0)
                return null;

            string actionId = item.UseActionId;

            if (item.IsConsumable)
            {
                item.Quantity--;
                if (item.Quantity <= 0)
                    inv.RemoveItem(instanceId);
            }

            OnInventoryChanged?.Invoke(combatant.Id);
            return actionId;
        }

        /// <summary>
        /// Get all usable items for a combatant (items with UseActionId and quantity > 0).
        /// </summary>
        public List<InventoryItem> GetUsableItems(string combatantId)
        {
            var inv = GetInventory(combatantId);
            return inv.BagItems
                .Where(i => !string.IsNullOrEmpty(i.UseActionId) && i.Quantity > 0)
                .ToList();
        }

        /// <summary>Add some default items to a combatant's bag based on their class/tags.</summary>
        private void AddStarterBagItems(Combatant combatant, Inventory inv)
        {
            // Everyone gets healing potions
            inv.AddItem(CreateConsumableItem("potion_healing", "Potion of Healing", ItemCategory.Potion,
                "Heal 2d4+2 HP", 2, useActionId: "use_potion_healing"));

            // Everyone gets an Alchemist's Fire
            inv.AddItem(CreateConsumableItem("alchemist_fire", "Alchemist's Fire", ItemCategory.Throwable,
                "1d4 fire damage, applies Burning", 1, useActionId: "use_alchemist_fire"));

            // Add a spare weapon
            if (_charRegistry != null)
            {
                var dagger = _charRegistry.GetWeapon("dagger");
                if (dagger != null)
                    inv.AddItem(CreateWeaponItem(dagger));

                // Martial characters get a ranged backup
                bool isMartial = combatant.Tags?.Contains("martial") == true
                    || combatant.Tags?.Contains("melee") == true;
                if (isMartial)
                {
                    var javelin = _charRegistry.GetWeapon("javelin");
                    if (javelin != null)
                        inv.AddItem(CreateWeaponItem(javelin));
                }

                // Casters get a scroll
                bool isCaster = combatant.Tags?.Contains("caster") == true;
                if (isCaster)
                {
                    inv.AddItem(CreateConsumableItem("scroll_revivify", "Scroll of Revivify",
                        ItemCategory.Scroll, "Revive a downed ally with 1 HP", 1,
                        useActionId: "use_scroll_revivify"));
                }
            }
        }
    }
}
