using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using QDND.Combat.Entities;
using QDND.Combat.Rules.Boosts;
using QDND.Data.CharacterModel;
using QDND.Data.Stats;

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
        Accessory,
        Potion,
        Scroll,
        Throwable,
        Misc,
        Clothing,
        Headwear,
        Cloak,
        Footwear,
        Handwear,
        Amulet,
        Ring,
        Consumable
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
        public string Description { get; set; } = string.Empty;

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

        /// <summary>Explicit slot compatibility. Empty means inferred from item data.</summary>
        public HashSet<EquipSlot> AllowedEquipSlots { get; set; } = new();

        /// <summary>BG3-style boost DSL string applied when equipped, e.g. "AC(1)".</summary>
        public string BoostString { get; set; }

        /// <summary>Build a short stat line for tooltips.</summary>
        public string GetStatLine()
        {
            if (WeaponDef != null)
            {
                string props = WeaponDef.Properties != WeaponProperty.None ? $" ({WeaponDef.Properties})" : string.Empty;
                string range = WeaponDef.IsRanged ? $" Range {WeaponDef.NormalRange}/{WeaponDef.LongRange}ft" : string.Empty;
                return $"{WeaponDef.DamageDice} {WeaponDef.DamageType}{props}{range}";
            }

            if (ArmorDef != null)
            {
                string dex = ArmorDef.MaxDexBonus.HasValue ? $" (max DEX +{ArmorDef.MaxDexBonus})" : " (+DEX)";
                string stealth = ArmorDef.StealthDisadvantage ? " Stealth Disadv." : string.Empty;
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
        RangedMainHand,
        RangedOffHand,
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
        /// <summary>Flat list of bag items (grid inventory order).</summary>
        public List<InventoryItem> BagItems { get; } = new();

        /// <summary>Equipped items by slot.</summary>
        public Dictionary<EquipSlot, InventoryItem> EquippedItems { get; } = new();

        /// <summary>Max bag slots (grid size).</summary>
        public int MaxBagSlots { get; set; } = 72;

        /// <summary>Per-category limits (0 = unlimited).</summary>
        public Dictionary<ItemCategory, int> CategoryLimits { get; set; } = new()
        {
            { ItemCategory.Weapon, 10 },
            { ItemCategory.Armor, 10 },
            { ItemCategory.Shield, 4 },
            { ItemCategory.Accessory, 12 },
            { ItemCategory.Potion, 20 },
            { ItemCategory.Scroll, 14 },
            { ItemCategory.Throwable, 14 },
            { ItemCategory.Misc, 40 },
            { ItemCategory.Clothing, 8 },
            { ItemCategory.Headwear, 6 },
            { ItemCategory.Cloak, 4 },
            { ItemCategory.Footwear, 6 },
            { ItemCategory.Handwear, 6 },
            { ItemCategory.Amulet, 4 },
            { ItemCategory.Ring, 8 },
            { ItemCategory.Consumable, 24 },
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
            if (item == null)
                return false;

            if (BagItems.Count >= MaxBagSlots)
                return false;

            if (CategoryLimits.TryGetValue(item.Category, out int limit) && limit > 0)
                return CountCategory(item.Category) + Math.Max(1, item.Quantity) <= limit;

            return true;
        }

        /// <summary>Add an item to the bag.</summary>
        public bool AddItem(InventoryItem item)
        {
            if (!CanAddItem(item))
                return false;

            BagItems.Add(item);
            return true;
        }

        /// <summary>Remove an item from the bag by instance ID.</summary>
        public bool RemoveItem(string instanceId)
        {
            var item = BagItems.FirstOrDefault(i => i.InstanceId == instanceId);
            if (item == null)
                return false;

            return BagItems.Remove(item);
        }

        /// <summary>Get an item from the bag by instance ID.</summary>
        public InventoryItem GetItem(string instanceId)
        {
            return BagItems.FirstOrDefault(i => i.InstanceId == instanceId);
        }

        /// <summary>Get bag item at slot index, or null.</summary>
        public InventoryItem GetBagItemAt(int index)
        {
            return index >= 0 && index < BagItems.Count ? BagItems[index] : null;
        }

        /// <summary>Find bag slot index by item instance ID.</summary>
        public int FindBagIndex(string instanceId)
        {
            return BagItems.FindIndex(i => i.InstanceId == instanceId);
        }

        /// <summary>Move an item inside the bag order.</summary>
        public bool MoveBagItem(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= BagItems.Count)
                return false;

            if (toIndex < 0 || toIndex >= MaxBagSlots)
                return false;

            if (fromIndex == toIndex)
                return true;

            var item = BagItems[fromIndex];
            BagItems.RemoveAt(fromIndex);

            int insertIndex = Math.Clamp(toIndex, 0, BagItems.Count);
            BagItems.Insert(insertIndex, item);
            return true;
        }

        /// <summary>
        /// Equip an item from the bag to the given slot. Returns any previously equipped item (returned to bag).
        /// </summary>
        public InventoryItem Equip(string instanceId, EquipSlot slot)
        {
            var index = FindBagIndex(instanceId);
            if (index < 0)
                return null;

            var item = BagItems[index];
            BagItems.RemoveAt(index);

            InventoryItem previous = null;
            if (EquippedItems.TryGetValue(slot, out var existing))
            {
                previous = existing;
                BagItems.Insert(Math.Min(index, BagItems.Count), previous);
            }

            EquippedItems[slot] = item;
            return previous;
        }

        /// <summary>Unequip an item from a slot back to the bag.</summary>
        public bool Unequip(EquipSlot slot)
        {
            if (!EquippedItems.TryGetValue(slot, out var item))
                return false;

            if (BagItems.Count >= MaxBagSlots)
                return false;

            EquippedItems.Remove(slot);
            BagItems.Add(item);
            return true;
        }

        /// <summary>Get the equipped item in a slot, or null.</summary>
        public InventoryItem GetEquipped(EquipSlot slot)
        {
            return EquippedItems.GetValueOrDefault(slot);
        }

        /// <summary>Whether the bag has no free slots left.</summary>
        public bool IsBagFull()
        {
            return BagItems.Count >= MaxBagSlots;
        }

        /// <summary>Insert an item into the bag at index (or end if index invalid).</summary>
        public bool TryInsertIntoBag(InventoryItem item, int index)
        {
            if (item == null || BagItems.Count >= MaxBagSlots)
                return false;

            int insertIndex = index < 0 || index > BagItems.Count ? BagItems.Count : index;
            BagItems.Insert(insertIndex, item);
            return true;
        }
    }

    /// <summary>
    /// Central inventory service managing inventories for all combatants.
    /// </summary>
    public class InventoryService
    {
        private const string IconGenericPhysical = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png";
        private const string IconGenericUtility = "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png";
        private const string IconGenericMagical = "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png";
        private const string IconGenericHealing = "res://assets/Images/Icons General/Generic_Healing_Unfaded_Icon.png";
        private const string IconMeleeWeapon = "res://assets/Images/Icons Weapon Actions/Main_Hand_Attack_Unfaded_Icon.png";
        private const string IconRangedWeapon = "res://assets/Images/Icons Weapon Actions/Ranged_Attack_Unfaded_Icon.png";
        private const string IconShield = "res://assets/Images/Icons Actions/Shield_Bash_Unfaded_Icon.png";
        private const string IconBoots = "res://assets/Images/Icons Actions/Boot_of_the_Giants_Unfaded_Icon.png";
        private const string IconCloak = "res://assets/Images/Icons Actions/Cloak_of_Shadows_Unfaded_Icon.png";
        private const string IconAmulet = "res://assets/Images/Icons Actions/Talk_to_the_Sentient_Amulet_Unfaded_Icon.png";
        private const string IconThrowable = "res://assets/Images/Icons Actions/Throw_Weapon_Unfaded_Icon.png";
        private const string IconWeaponDirectory = "assets/Images/Icons Weapons and Other";
        private const string IconArmorDirectory = "assets/Images/Icons Armour";

        private static readonly Lazy<string> ProjectRootPath = new(FindProjectRootPath);
        private static readonly Lazy<Dictionary<string, string>> SpecificIconLookup = new(BuildSpecificIconLookup);
        private static readonly IReadOnlyDictionary<WeaponType, string[]> WeaponTypeIconCandidates = new Dictionary<WeaponType, string[]>
        {
            [WeaponType.Club] = new[] { "Club" },
            [WeaponType.Dagger] = new[] { "Dagger" },
            [WeaponType.Greatclub] = new[] { "Greatclub", "Club" },
            [WeaponType.Handaxe] = new[] { "Handaxe" },
            [WeaponType.Javelin] = new[] { "Javelin" },
            [WeaponType.LightCrossbow] = new[] { "Light Crossbow" },
            [WeaponType.LightHammer] = new[] { "Light Hammer", "Warhammer" },
            [WeaponType.Mace] = new[] { "Mace" },
            [WeaponType.Quarterstaff] = new[] { "Quarterstaff", "Druid Quarterstaff" },
            [WeaponType.Shortbow] = new[] { "Shortbow", "Bow" },
            [WeaponType.Sickle] = new[] { "Sickle" },
            [WeaponType.Spear] = new[] { "Spear" },
            [WeaponType.Dart] = new[] { "Dart" },
            [WeaponType.Battleaxe] = new[] { "Battleaxe" },
            [WeaponType.Flail] = new[] { "Flail" },
            [WeaponType.Glaive] = new[] { "Glaive" },
            [WeaponType.Greataxe] = new[] { "Greataxe" },
            [WeaponType.Greatsword] = new[] { "Greatsword" },
            [WeaponType.Halberd] = new[] { "Halberd" },
            [WeaponType.HeavyCrossbow] = new[] { "Heavy Crossbow" },
            [WeaponType.Lance] = new[] { "Lance", "Pike" },
            [WeaponType.Longbow] = new[] { "Longbow", "Bow" },
            [WeaponType.Longsword] = new[] { "Longsword" },
            [WeaponType.Maul] = new[] { "Maul" },
            [WeaponType.Morningstar] = new[] { "Morningstar" },
            [WeaponType.Pike] = new[] { "Pike" },
            [WeaponType.Rapier] = new[] { "Rapier" },
            [WeaponType.Scimitar] = new[] { "Scimitar" },
            [WeaponType.Shortsword] = new[] { "Shortsword" },
            [WeaponType.Trident] = new[] { "Trident" },
            [WeaponType.WarPick] = new[] { "War Pick", "Warpick" },
            [WeaponType.Warhammer] = new[] { "Warhammer" },
            [WeaponType.Whip] = new[] { "Whip" },
            [WeaponType.HandCrossbow] = new[] { "Hand Crossbow" },
        };

        private readonly Dictionary<string, Inventory> _inventories = new();
        private readonly CharacterDataRegistry _charRegistry;
        private readonly StatsRegistry _statsRegistry;

        private readonly List<InventoryTemplate> _bg3WeaponTemplates = new();
        private readonly List<InventoryTemplate> _bg3ArmorTemplates = new();
        private readonly Dictionary<EquipSlot, List<InventoryTemplate>> _bg3TemplatesBySlot = new();
        private bool _bg3CatalogBuilt;

        /// <summary>Fired when an inventory changes (combatantId).</summary>
        public event Action<string> OnInventoryChanged;

        /// <summary>Fired when equipment changes (combatantId, slot).</summary>
        public event Action<string, EquipSlot> OnEquipmentChanged;

        public InventoryService(CharacterDataRegistry charRegistry, StatsRegistry statsRegistry = null)
        {
            _charRegistry = charRegistry;
            _statsRegistry = statsRegistry;
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
        /// Populates equipment slots and fills the bag with starter items from BG3 stats.
        /// </summary>
        public void InitializeFromCombatant(Combatant combatant)
        {
            if (combatant == null)
                return;

            var inv = GetInventory(combatant.Id);
            inv.BagItems.Clear();
            inv.EquippedItems.Clear();

            // Equip current weapons/armor into slots
            if (combatant.MainHandWeapon != null)
                inv.EquippedItems[EquipSlot.MainHand] = CreateWeaponItem(combatant.MainHandWeapon);

            if (combatant.OffHandWeapon != null)
                inv.EquippedItems[EquipSlot.OffHand] = CreateWeaponItem(combatant.OffHandWeapon);

            if (combatant.HasShield && combatant.EquippedShield != null)
            {
                inv.EquippedItems[EquipSlot.OffHand] = CreateArmorItem(combatant.EquippedShield, ItemCategory.Shield);
            }

            if (combatant.EquippedArmor != null)
                inv.EquippedItems[EquipSlot.Armor] = CreateArmorItem(combatant.EquippedArmor, ItemCategory.Armor);

            AddStarterBagItems(combatant, inv);
            ApplyEquipment(combatant, inv);
            OnInventoryChanged?.Invoke(combatant.Id);
        }

        /// <summary>Equip an item and apply stat changes to the combatant.</summary>
        public bool EquipItem(Combatant combatant, string instanceId, EquipSlot slot)
        {
            if (combatant == null)
                return false;

            var inv = GetInventory(combatant.Id);
            int bagIndex = inv.FindBagIndex(instanceId);
            if (bagIndex < 0)
                return false;

            if (!MoveBagItemToEquipSlot(combatant, bagIndex, slot, out _))
                return false;

            return true;
        }

        /// <summary>Unequip a slot and revert stat changes.</summary>
        public bool UnequipItem(Combatant combatant, EquipSlot slot)
        {
            if (combatant == null)
                return false;

            int bagIndex = GetInventory(combatant.Id).BagItems.Count;
            if (!MoveEquippedItemToBagSlot(combatant, slot, bagIndex, out _))
                return false;

            return true;
        }

        /// <summary>Add an item to a combatant's bag.</summary>
        public bool AddItemToBag(Combatant combatant, InventoryItem item)
        {
            if (combatant == null || item == null)
                return false;

            var inv = GetInventory(combatant.Id);
            if (!inv.AddItem(item))
                return false;

            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>
        /// Move an item between two bag slots.
        /// </summary>
        public bool MoveBagItemToBagSlot(Combatant combatant, int fromIndex, int toIndex, out string reason)
        {
            reason = null;
            if (combatant == null)
            {
                reason = "Combatant is null.";
                return false;
            }

            var inv = GetInventory(combatant.Id);
            if (!inv.MoveBagItem(fromIndex, toIndex))
            {
                reason = "Invalid bag slot move.";
                return false;
            }

            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>
        /// Move a bag item into an equipment slot (supports swapping).
        /// </summary>
        public bool MoveBagItemToEquipSlot(Combatant combatant, int fromBagIndex, EquipSlot targetSlot, out string reason)
        {
            reason = null;
            if (combatant == null)
            {
                reason = "Combatant is null.";
                return false;
            }

            var inv = GetInventory(combatant.Id);
            var item = inv.GetBagItemAt(fromBagIndex);
            if (item == null)
            {
                reason = "No item in source bag slot.";
                return false;
            }

            if (!CanEquipToSlot(inv, item, targetSlot, out reason, combatant.ResolvedCharacter?.Sheet?.FeatIds))
                return false;

            var displacedAtTarget = inv.GetEquipped(targetSlot);
            var linkedOffhandSlot = GetLinkedOffhandSlot(targetSlot);
            var displacedLinkedOffhand = linkedOffhandSlot.HasValue && item.WeaponDef?.IsTwoHanded == true
                ? inv.GetEquipped(linkedOffhandSlot.Value)
                : null;

            if (linkedOffhandSlot.HasValue && item.WeaponDef?.IsTwoHanded == true)
            {
                // If linked off-hand holds the same slot we're replacing, it will be handled once.
                if (ReferenceEquals(displacedLinkedOffhand, displacedAtTarget))
                    displacedLinkedOffhand = null;
            }

            int currentBagCount = inv.BagItems.Count;
            int bagSlotsFreed = 1; // removing source bag item
            int bagSlotsNeeded = 0;
            if (displacedAtTarget != null) bagSlotsNeeded++;
            if (displacedLinkedOffhand != null) bagSlotsNeeded++;

            if (currentBagCount - bagSlotsFreed + bagSlotsNeeded > inv.MaxBagSlots)
            {
                reason = "Bag is full.";
                return false;
            }

            inv.BagItems.RemoveAt(fromBagIndex);
            inv.EquippedItems[targetSlot] = item;

            int insertIndex = Math.Clamp(fromBagIndex, 0, inv.BagItems.Count);
            if (displacedAtTarget != null)
                inv.BagItems.Insert(insertIndex, displacedAtTarget);

            if (displacedLinkedOffhand != null && linkedOffhandSlot.HasValue)
            {
                inv.EquippedItems.Remove(linkedOffhandSlot.Value);
                inv.BagItems.Insert(Math.Min(insertIndex + 1, inv.BagItems.Count), displacedLinkedOffhand);
            }

            ApplyEquipment(combatant, inv);
            EmitEquipmentChanged(combatant.Id, targetSlot);
            if (displacedLinkedOffhand != null && linkedOffhandSlot.HasValue)
                EmitEquipmentChanged(combatant.Id, linkedOffhandSlot.Value);
            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>
        /// Move an equipped item back to a bag slot.
        /// </summary>
        public bool MoveEquippedItemToBagSlot(Combatant combatant, EquipSlot fromSlot, int toBagIndex, out string reason)
        {
            reason = null;
            if (combatant == null)
            {
                reason = "Combatant is null.";
                return false;
            }

            var inv = GetInventory(combatant.Id);
            if (!inv.EquippedItems.TryGetValue(fromSlot, out var item) || item == null)
            {
                reason = "No equipped item in source slot.";
                return false;
            }

            if (inv.IsBagFull())
            {
                reason = "Bag is full.";
                return false;
            }

            inv.EquippedItems.Remove(fromSlot);
            inv.TryInsertIntoBag(item, toBagIndex);

            ApplyEquipment(combatant, inv);
            EmitEquipmentChanged(combatant.Id, fromSlot);
            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>
        /// Move/swap between equipped slots.
        /// </summary>
        public bool MoveEquippedItemToEquipSlot(Combatant combatant, EquipSlot fromSlot, EquipSlot toSlot, out string reason)
        {
            reason = null;
            if (combatant == null)
            {
                reason = "Combatant is null.";
                return false;
            }

            if (fromSlot == toSlot)
                return true;

            var inv = GetInventory(combatant.Id);
            if (!inv.EquippedItems.TryGetValue(fromSlot, out var sourceItem) || sourceItem == null)
            {
                reason = "No equipped item in source slot.";
                return false;
            }

            if (!CanEquipToSlot(inv, sourceItem, toSlot, out reason, combatant.ResolvedCharacter?.Sheet?.FeatIds))
                return false;

            inv.EquippedItems.TryGetValue(toSlot, out var targetItem);
            if (targetItem != null && !CanEquipToSlot(inv, targetItem, fromSlot, out reason, combatant.ResolvedCharacter?.Sheet?.FeatIds))
                return false;

            // Avoid implicit off-hand displacement in equip-to-equip moves.
            if (sourceItem.WeaponDef?.IsTwoHanded == true)
            {
                var linkedOffhandSlot = GetLinkedOffhandSlot(toSlot);
                var linkedOffhand = linkedOffhandSlot.HasValue ? inv.GetEquipped(linkedOffhandSlot.Value) : null;
                if (linkedOffhand != null && linkedOffhandSlot.HasValue && fromSlot != linkedOffhandSlot.Value)
                {
                    reason = $"Unequip {linkedOffhandSlot.Value} first to equip a two-handed weapon.";
                    return false;
                }
            }

            inv.EquippedItems.Remove(fromSlot);
            inv.EquippedItems[toSlot] = sourceItem;

            if (targetItem != null)
                inv.EquippedItems[fromSlot] = targetItem;
            else
                inv.EquippedItems.Remove(fromSlot);

            ApplyEquipment(combatant, inv);
            EmitEquipmentChanged(combatant.Id, fromSlot);
            EmitEquipmentChanged(combatant.Id, toSlot);
            OnInventoryChanged?.Invoke(combatant.Id);
            return true;
        }

        /// <summary>
        /// Check if an item is valid for a specific equipment slot.
        /// </summary>
        public bool CanEquipToSlot(Combatant combatant, InventoryItem item, EquipSlot slot, out string reason)
        {
            var inv = combatant != null ? GetInventory(combatant.Id) : null;
            return CanEquipToSlot(inv, item, slot, out reason, combatant?.ResolvedCharacter?.Sheet?.FeatIds);
        }

        /// <summary>
        /// Toggles the active weapon set (melee ↔ ranged) and re-applies equipment.
        /// </summary>
        public void SwitchWeaponSet(Combatant combatant)
        {
            if (combatant == null) return;
            combatant.ActiveWeaponSet = combatant.ActiveWeaponSet == 0 ? 1 : 0;
            var inv = GetInventory(combatant.Id);
            if (inv != null)
            {
                ApplyEquipment(combatant, inv);
                OnEquipmentChanged?.Invoke(combatant.Id, EquipSlot.MainHand);
            }
        }

        /// <summary>
        /// Apply current equipped items to the combatant's stats.
        /// Updates weapon, armor, shield, and AC.
        /// Respects combatant.ActiveWeaponSet: 0 = melee (MainHand/OffHand), 1 = ranged (RangedMainHand/RangedOffHand).
        /// </summary>
        public void ApplyEquipment(Combatant combatant, Inventory inv)
        {
            if (combatant == null || inv == null)
                return;

            // Remove all equipment-sourced boosts before re-evaluating
            QDND.Combat.Rules.Boosts.BoostApplicator.RemoveBoostsBySource(combatant, "Equipment");

            var prevMain = combatant.MainHandWeapon;
            var prevOff = combatant.OffHandWeapon;

            var mainSlot = combatant.ActiveWeaponSet == 0 ? EquipSlot.MainHand : EquipSlot.RangedMainHand;
            var offSlot = combatant.ActiveWeaponSet == 0 ? EquipSlot.OffHand : EquipSlot.RangedOffHand;

            // Main hand (from active weapon set)
            var mainHand = inv.GetEquipped(mainSlot);
            combatant.MainHandWeapon = mainHand?.WeaponDef;

            // Off hand - weapon OR shield (from active weapon set)
            var offHand = inv.GetEquipped(offSlot);
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

            // Sync weapon-granted actions
            SyncGrantedActions(combatant, prevMain, combatant.MainHandWeapon);
            SyncGrantedActions(combatant, prevOff, combatant.OffHandWeapon);

            RecalculateAC(combatant);

            // Re-apply boosts from all equipped items
            foreach (var kvp in inv.EquippedItems)
            {
                if (kvp.Value?.BoostString != null && kvp.Value.BoostString.Length > 0)
                    QDND.Combat.Rules.Boosts.BoostApplicator.ApplyBoosts(
                        combatant, kvp.Value.BoostString, "Equipment", kvp.Value.InstanceId);
            }
        }

        private static void SyncGrantedActions(Combatant combatant, WeaponDefinition previous, WeaponDefinition current)
        {
            bool changed = false;

            if (previous?.GrantedActionIds != null)
                foreach (var id in previous.GrantedActionIds)
                    changed |= combatant.KnownActions.Remove(id);

            if (current?.GrantedActionIds != null)
                foreach (var id in current.GrantedActionIds)
                    if (!combatant.KnownActions.Contains(id))
                    {
                        combatant.KnownActions.Add(id);
                        changed = true;
                    }

            if (changed)
                combatant.NotifyKnownActionsChanged();
        }

        /// <summary>Recalculate AC from equipped armor + shield + DEX.</summary>
        private static void RecalculateAC(Combatant combatant)
        {
            if (combatant.ResolvedCharacter == null)
                return;

            int dexMod = combatant.GetAbilityModifier(AbilityType.Dexterity);
            int ac;

            if (combatant.EquippedArmor != null)
            {
                var armor = combatant.EquippedArmor;
                int maxDex = armor.MaxDexBonus ?? int.MaxValue;
                // Medium Armor Master feat: DEX cap +3 instead of +2
                if (armor.MaxDexBonus == 2 && combatant.ResolvedCharacter?.Sheet?.FeatIds != null)
                {
                    bool hasMam = combatant.ResolvedCharacter.Sheet.FeatIds.Any(f =>
                        string.Equals(f, "medium_armor_master", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f, "medium_armour_master", StringComparison.OrdinalIgnoreCase));
                    if (hasMam) maxDex = 3;
                }
                int dexBonus = Math.Clamp(dexMod, 0, maxDex);
                ac = armor.BaseAC + dexBonus;
            }
            else
            {
                // Unarmored: check for AC override formula (Unarmored Defense, Draconic Resilience)
                ac = 10 + dexMod;
                var (hasOverride, overrideAC) = BoostEvaluator.GetACOverride(combatant);
                if (hasOverride && overrideAC > ac)
                    ac = overrideAC;
            }

            if (combatant.HasShield)
                ac += 2;

            // Dual Wielder feat: +1 AC when wielding a melee weapon in each hand
            if (combatant.MainHandWeapon != null && combatant.OffHandWeapon != null &&
                !combatant.MainHandWeapon.IsRanged && !combatant.OffHandWeapon.IsRanged &&
                combatant.ResolvedCharacter?.Sheet?.FeatIds?.Any(f =>
                    string.Equals(f, "dual_wielder", StringComparison.OrdinalIgnoreCase)) == true)
            {
                ac += 1;
            }

            combatant.CurrentAC = ac;

            // Heavy armor STR requirement: -10ft speed penalty if STR is below minimum
            if (combatant.EquippedArmor != null && combatant.EquippedArmor.StrengthRequirement > 0)
            {
                int strScore = combatant.GetAbilityScore(AbilityType.Strength);
                combatant.ArmorSpeedPenalty = strScore < combatant.EquippedArmor.StrengthRequirement ? 10f : 0f;
            }
            else
            {
                combatant.ArmorSpeedPenalty = 0f;
            }
        }

        // -- Item creation helpers ---------------------------------------------

        public static InventoryItem CreateWeaponItem(WeaponDefinition wep)
        {
            if (wep == null)
                return null;

            var allowed = BuildAllowedWeaponSlots(wep);

            return new InventoryItem
            {
                DefinitionId = wep.Id,
                Name = wep.Name,
                Category = ItemCategory.Weapon,
                Weight = wep.Weight,
                WeaponDef = wep,
                Description = $"{wep.DamageDice} {wep.DamageType} - {wep.Category} {wep.WeaponType}",
                AllowedEquipSlots = allowed,
                IconPath = ResolveWeaponIconPath(wep, wep.Id),
            };
        }

        public static InventoryItem CreateArmorItem(ArmorDefinition armor, ItemCategory category)
        {
            if (armor == null)
                return null;

            var allowed = BuildAllowedWearableSlots(category);
            if (allowed.Count == 0)
                allowed.Add(EquipSlot.Armor);

            return new InventoryItem
            {
                DefinitionId = armor.Id,
                Name = armor.Name,
                Category = category,
                Weight = armor.Weight,
                ArmorDef = armor,
                Description = $"AC {armor.BaseAC} - {armor.Category}",
                AllowedEquipSlots = allowed,
                IconPath = ResolveArmorIconPath(category, allowed.FirstOrDefault(), armor, rawName: armor.Id, rawArmorType: null),
            };
        }

        public static InventoryItem CreateConsumableItem(
            string id,
            string name,
            ItemCategory category,
            string description,
            int quantity = 1,
            string useActionId = null,
            bool isConsumable = true,
            int maxStackSize = 20,
            string iconPath = null)
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
                MaxStackSize = maxStackSize,
                IconPath = iconPath,
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
            if (combatant == null)
                return null;

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

        /// <summary>
        /// Add starter items for a combatant. Prioritizes parsed BG3 items if available.
        /// </summary>
        private void AddStarterBagItems(Combatant combatant, Inventory inv)
        {
            bool addedBg3 = AddStarterBagItemsFromBG3(combatant, inv);

            // Core consumables are guaranteed regardless of data source.
            inv.AddItem(CreateConsumableItem(
                "potion_healing",
                "Potion of Healing",
                ItemCategory.Potion,
                "Heal 2d4+2 HP",
                2,
                useActionId: "use_potion_healing",
                iconPath: IconGenericHealing));

            inv.AddItem(CreateConsumableItem(
                "alchemist_fire",
                "Alchemist's Fire",
                ItemCategory.Throwable,
                "1d4 fire damage, applies Burning",
                1,
                useActionId: "use_alchemist_fire",
                iconPath: IconThrowable));

            bool isCaster = combatant.Tags?.Contains("caster") == true;
            if (isCaster)
            {
                inv.AddItem(CreateConsumableItem(
                    "scroll_revivify",
                    "Scroll of Revivify",
                    ItemCategory.Scroll,
                    "Revive a downed ally with 1 HP",
                    1,
                    useActionId: "use_scroll_revivify",
                    iconPath: IconGenericMagical));
            }

            // Legacy fallback if BG3 catalog isn't available.
            if (!addedBg3 && _charRegistry != null)
            {
                var dagger = _charRegistry.GetWeapon("dagger");
                if (dagger != null)
                    inv.AddItem(CreateWeaponItem(dagger));

                bool isMartial = combatant.Tags?.Contains("martial") == true || combatant.Tags?.Contains("melee") == true;
                if (isMartial)
                {
                    var javelin = _charRegistry.GetWeapon("javelin");
                    if (javelin != null)
                        inv.AddItem(CreateWeaponItem(javelin));
                }
            }
        }

        private bool AddStarterBagItemsFromBG3(Combatant combatant, Inventory inv)
        {
            EnsureBg3Catalog();
            if (!_bg3CatalogBuilt)
                return false;

            int seed = ComputeDeterministicSeed(combatant.Id + ":" + combatant.Name);

            // Weapons
            AddTemplateFromPool(inv, _bg3WeaponTemplates, seed, 0);
            AddTemplateFromPool(inv, _bg3WeaponTemplates, seed, 1);

            // Equipment pieces around BG3-style slots
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Armor), seed, 2);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Helmet), seed, 3);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Gloves), seed, 4);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Boots), seed, 5);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Cloak), seed, 6);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Amulet), seed, 7);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.Ring1), seed, 8);
            AddTemplateFromPool(inv, GetSlotPool(EquipSlot.OffHand), seed, 9);

            return _bg3WeaponTemplates.Count > 0 || _bg3ArmorTemplates.Count > 0;
        }

        private List<InventoryTemplate> GetSlotPool(EquipSlot slot)
        {
            return _bg3TemplatesBySlot.TryGetValue(slot, out var pool) ? pool : null;
        }

        private static void AddTemplateFromPool(Inventory inv, List<InventoryTemplate> pool, int seed, int salt)
        {
            if (inv == null || pool == null || pool.Count == 0)
                return;

            int index = Math.Abs(seed + salt * 7919) % pool.Count;
            var template = pool[index];
            var item = template.ToInventoryItem();
            inv.AddItem(item);
        }

        private void EnsureBg3Catalog()
        {
            if (_bg3CatalogBuilt)
                return;

            _bg3CatalogBuilt = true;

            if (_statsRegistry == null)
                return;

            BuildBg3WeaponCatalog();
            BuildBg3ArmorCatalog();
        }

        private void BuildBg3WeaponCatalog()
        {
            foreach (var entry in _statsRegistry.GetAllWeapons().Values)
            {
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.StartsWith("_", StringComparison.Ordinal))
                    continue;

                if (string.Equals(entry.Name, "NoWeapon", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(entry.Name, "WPN_DummyForEquipment", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(entry.InventoryTab, "Hidden", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!TryMapWeapon(entry, out var weaponDef))
                    continue;

                var item = new InventoryTemplate
                {
                    DefinitionId = NormalizeDefinitionId(entry.Name),
                    Name = HumanizeStatName(entry.Name),
                    Category = ItemCategory.Weapon,
                    Weight = KgToLbsRounded(entry.Weight),
                    Description = $"{weaponDef.DamageDice} {weaponDef.DamageType} - {weaponDef.Category} {weaponDef.WeaponType}",
                    Rarity = ParseRarity(entry.Rarity),
                    WeaponDef = weaponDef,
                    AllowedSlots = BuildAllowedWeaponSlots(weaponDef, entry.Slot),
                    IconPath = ResolveWeaponIconPath(weaponDef, entry.Name),
                };

                _bg3WeaponTemplates.Add(item);
                IndexTemplateSlots(item);
            }
        }

        private void BuildBg3ArmorCatalog()
        {
            foreach (var entry in _statsRegistry.GetAllArmors().Values)
            {
                if (entry == null)
                    continue;

                if (string.IsNullOrWhiteSpace(entry.Name) || entry.Name.StartsWith("_", StringComparison.Ordinal))
                    continue;

                if (!TryMapArmorSlots(entry, out var slots, out var category))
                    continue;

                ArmorDefinition armorDef = null;
                if (category == ItemCategory.Armor || category == ItemCategory.Shield)
                    armorDef = BuildArmorDefinition(entry, category);

                string icon = ResolveArmorIconPath(category, slots.FirstOrDefault(), armorDef, entry.Name, entry.ArmorType);
                string slotLabel = string.Join(", ", slots.Select(s => s.ToString()));
                string desc = armorDef != null
                    ? $"AC {armorDef.BaseAC} - {slotLabel}"
                    : $"Equippable in: {slotLabel}";

                var item = new InventoryTemplate
                {
                    DefinitionId = NormalizeDefinitionId(entry.Name),
                    Name = HumanizeStatName(entry.Name),
                    Category = category,
                    Weight = KgToLbsRounded(entry.Weight),
                    Description = desc,
                    Rarity = ParseRarity(entry.Rarity),
                    ArmorDef = armorDef,
                    AllowedSlots = new HashSet<EquipSlot>(slots),
                    IconPath = icon,
                };

                _bg3ArmorTemplates.Add(item);
                IndexTemplateSlots(item);
            }
        }

        private void IndexTemplateSlots(InventoryTemplate template)
        {
            if (template?.AllowedSlots == null)
                return;

            foreach (var slot in template.AllowedSlots)
            {
                if (!_bg3TemplatesBySlot.TryGetValue(slot, out var list))
                {
                    list = new List<InventoryTemplate>();
                    _bg3TemplatesBySlot[slot] = list;
                }

                list.Add(template);
            }
        }

        private static bool CanEquipToSlot(Inventory inv, InventoryItem item, EquipSlot slot, out string reason, IReadOnlyCollection<string> featIds = null)
        {
            reason = null;

            if (item == null)
            {
                reason = "No item selected.";
                return false;
            }

            var allowedSlots = ResolveAllowedSlots(item);
            if (!allowedSlots.Contains(slot))
            {
                reason = $"{item.Name} cannot be equipped in {slot}.";
                return false;
            }

            if (slot == EquipSlot.OffHand)
            {
                if (item.WeaponDef?.IsTwoHanded == true)
                {
                    reason = "Two-handed weapons cannot go in off-hand.";
                    return false;
                }

                if (inv?.GetEquipped(EquipSlot.MainHand)?.WeaponDef?.IsTwoHanded == true)
                {
                    reason = "Main-hand two-handed weapon blocks off-hand slot.";
                    return false;
                }

                // Dual-wield Light requirement: non-Light weapons require the Dual Wielder feat
                if (item.WeaponDef != null && !item.WeaponDef.IsLight)
                {
                    bool hasDualWielderFeat = featIds != null && featIds.Any(f =>
                        string.Equals(f, "DualWielder", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f, "dual_wielder", StringComparison.OrdinalIgnoreCase));
                    if (!hasDualWielderFeat)
                    {
                        reason = $"{item.Name} is not a Light weapon. The Dual Wielder feat is required to wield it in the off-hand.";
                        return false;
                    }
                }
            }

            if (slot == EquipSlot.RangedOffHand)
            {
                if (item.WeaponDef?.IsTwoHanded == true)
                {
                    reason = "Two-handed weapons cannot go in ranged off-hand.";
                    return false;
                }

                if (inv?.GetEquipped(EquipSlot.RangedMainHand)?.WeaponDef?.IsTwoHanded == true)
                {
                    reason = "Ranged main-hand two-handed weapon blocks ranged off-hand slot.";
                    return false;
                }
            }

            if ((slot == EquipSlot.MainHand || slot == EquipSlot.RangedMainHand) && item.WeaponDef?.IsTwoHanded == true)
            {
                // Allowed; UI/service may displace off-hand depending on operation.
                return true;
            }

            return true;
        }

        private static HashSet<EquipSlot> ResolveAllowedSlots(InventoryItem item)
        {
            if (item.AllowedEquipSlots != null && item.AllowedEquipSlots.Count > 0)
                return new HashSet<EquipSlot>(item.AllowedEquipSlots);

            if (item.WeaponDef != null)
                return BuildAllowedWeaponSlots(item.WeaponDef);

            if (item.ArmorDef != null)
            {
                return BuildAllowedWearableSlots(item.Category);
            }

            return new HashSet<EquipSlot>();
        }

        private static HashSet<EquipSlot> BuildAllowedWeaponSlots(WeaponDefinition weaponDef, string bg3Slot = null)
        {
            var allowed = new HashSet<EquipSlot> { EquipSlot.MainHand };

            if (weaponDef == null)
                return allowed;

            bool isRangedLoadoutWeapon = IsRangedLoadoutWeapon(weaponDef, bg3Slot);
            if (isRangedLoadoutWeapon)
                allowed.Add(EquipSlot.RangedMainHand);

            bool offhandAllowed = !weaponDef.IsTwoHanded && (weaponDef.IsLight || weaponDef.WeaponType == WeaponType.HandCrossbow || !weaponDef.IsRanged);
            if (offhandAllowed)
            {
                allowed.Add(EquipSlot.OffHand);
                if (isRangedLoadoutWeapon)
                    allowed.Add(EquipSlot.RangedOffHand);
            }

            return allowed;
        }

        private static bool IsRangedLoadoutWeapon(WeaponDefinition weaponDef, string bg3Slot = null)
        {
            if (!string.IsNullOrWhiteSpace(bg3Slot) && bg3Slot.Contains("Ranged", StringComparison.OrdinalIgnoreCase))
                return true;

            if (weaponDef == null)
                return false;

            return weaponDef.WeaponType is WeaponType.Shortbow
                or WeaponType.Longbow
                or WeaponType.LightCrossbow
                or WeaponType.HeavyCrossbow
                or WeaponType.HandCrossbow;
        }

        private static EquipSlot? GetLinkedOffhandSlot(EquipSlot mainOrRangedMainSlot)
        {
            return mainOrRangedMainSlot switch
            {
                EquipSlot.MainHand => EquipSlot.OffHand,
                EquipSlot.RangedMainHand => EquipSlot.RangedOffHand,
                _ => null,
            };
        }

        private static bool TryMapArmorSlots(BG3ArmorData entry, out HashSet<EquipSlot> slots, out ItemCategory category)
        {
            slots = new HashSet<EquipSlot>();
            category = ItemCategory.Misc;

            string slot = entry.Slot?.Trim() ?? string.Empty;
            if (slot.Length == 0)
                return false;

            switch (slot)
            {
                case "Breast":
                    slots.Add(EquipSlot.Armor);
                    category = IsClothingItem(entry) ? ItemCategory.Clothing : ItemCategory.Armor;
                    return true;
                case "Helmet":
                    slots.Add(EquipSlot.Helmet);
                    category = ItemCategory.Headwear;
                    return true;
                case "Gloves":
                    slots.Add(EquipSlot.Gloves);
                    category = ItemCategory.Handwear;
                    return true;
                case "Boots":
                    slots.Add(EquipSlot.Boots);
                    category = ItemCategory.Footwear;
                    return true;
                case "Cloak":
                    slots.Add(EquipSlot.Cloak);
                    category = ItemCategory.Cloak;
                    return true;
                case "Amulet":
                    slots.Add(EquipSlot.Amulet);
                    category = ItemCategory.Amulet;
                    return true;
                case "Ring":
                    slots.Add(EquipSlot.Ring1);
                    slots.Add(EquipSlot.Ring2);
                    category = ItemCategory.Ring;
                    return true;
                case "Melee Offhand Weapon":
                case "Offhand":
                    slots.Add(EquipSlot.OffHand);
                    category = ItemCategory.Shield;
                    return true;
                case "VanityBody":
                case "Underwear":
                    slots.Add(EquipSlot.Armor);
                    category = ItemCategory.Clothing;
                    return true;
                case "VanityBoots":
                    slots.Add(EquipSlot.Boots);
                    category = ItemCategory.Footwear;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsClothingItem(BG3ArmorData entry)
        {
            if (entry == null)
                return false;

            if (string.Equals(entry.Shield, "Yes", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(entry.ProficiencyGroup))
                return false;

            string armorType = entry.ArmorType?.Trim() ?? string.Empty;
            if (armorType.Length == 0)
                return true;

            if (string.Equals(armorType, "None", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(armorType, "Cloth", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return armorType.Contains("Robe", StringComparison.OrdinalIgnoreCase);
        }

        private static HashSet<EquipSlot> BuildAllowedWearableSlots(ItemCategory category)
        {
            var slots = new HashSet<EquipSlot>();
            switch (category)
            {
                case ItemCategory.Shield:
                    slots.Add(EquipSlot.OffHand);
                    break;
                case ItemCategory.Armor:
                case ItemCategory.Clothing:
                    slots.Add(EquipSlot.Armor);
                    break;
                case ItemCategory.Headwear:
                    slots.Add(EquipSlot.Helmet);
                    break;
                case ItemCategory.Handwear:
                    slots.Add(EquipSlot.Gloves);
                    break;
                case ItemCategory.Footwear:
                    slots.Add(EquipSlot.Boots);
                    break;
                case ItemCategory.Cloak:
                    slots.Add(EquipSlot.Cloak);
                    break;
                case ItemCategory.Amulet:
                    slots.Add(EquipSlot.Amulet);
                    break;
                case ItemCategory.Ring:
                    slots.Add(EquipSlot.Ring1);
                    slots.Add(EquipSlot.Ring2);
                    break;
            }

            return slots;
        }

        private static bool TryMapWeapon(BG3WeaponData entry, out WeaponDefinition weapon)
        {
            weapon = null;

            if (!TryMapWeaponType(entry.Name, out var weaponType))
                return false;

            if (!TryParseDamageDice(entry.Damage, out int diceCount, out int dieFaces))
                return false;

            var category = entry.WeaponGroup != null && entry.WeaponGroup.Contains("Martial", StringComparison.OrdinalIgnoreCase)
                ? WeaponCategory.Martial
                : WeaponCategory.Simple;

            var damageType = TryParseDamageType(entry.DamageType, out var parsedDamage)
                ? parsedDamage
                : DamageType.Bludgeoning;

            int normalRange = CmToFeet(entry.WeaponRange);
            if (normalRange <= 0)
                normalRange = entry.WeaponProperties?.Contains("Ranged", StringComparison.OrdinalIgnoreCase) == true ? 60 : 5;

            int longRange = CmToFeet(entry.DamageRange);
            if (longRange < normalRange)
                longRange = normalRange;

            int versatileFaces = 0;
            if (TryParseDamageDice(entry.VersatileDamage, out _, out int parsedVersatileFaces))
                versatileFaces = parsedVersatileFaces;

            weapon = new WeaponDefinition
            {
                Id = NormalizeDefinitionId(entry.Name),
                Name = HumanizeStatName(entry.Name),
                WeaponType = weaponType,
                Category = category,
                DamageType = damageType,
                DamageDiceCount = Math.Max(1, diceCount),
                DamageDieFaces = Math.Max(4, dieFaces),
                VersatileDieFaces = versatileFaces,
                NormalRange = Math.Max(5, normalRange),
                LongRange = Math.Max(0, longRange),
                Properties = ParseWeaponProperties(entry.WeaponProperties),
                Weight = KgToLbsRounded(entry.Weight),
            };

            return true;
        }

        private static ArmorDefinition BuildArmorDefinition(BG3ArmorData entry, ItemCategory category)
        {
            int baseAc = Math.Max(10, entry.ArmorClass);
            int? maxDex = null;
            bool usesDex = string.Equals(entry.ArmorClassAbility, "Dexterity", StringComparison.OrdinalIgnoreCase);
            if (usesDex)
            {
                maxDex = entry.AbilityModifierCap > 0 ? entry.AbilityModifierCap : null;
            }
            else if (entry.AbilityModifierCap > 0)
            {
                maxDex = entry.AbilityModifierCap;
            }
            else
            {
                maxDex = 0;
            }

            bool stealthDisadv = (entry.Boosts ?? string.Empty).Contains("Disadvantage(Skill,Stealth)", StringComparison.OrdinalIgnoreCase);
            int strengthReq = 0;
            if (entry.RawProperties != null && entry.RawProperties.TryGetValue("MinimumStrength", out var minStr) && int.TryParse(minStr, out var parsedStr))
                strengthReq = parsedStr;

            ArmorCategory armorCategory = category switch
            {
                ItemCategory.Shield => ArmorCategory.Shield,
                _ => ParseArmorCategory(entry.ProficiencyGroup, entry.ArmorType)
            };

            return new ArmorDefinition
            {
                Id = NormalizeDefinitionId(entry.Name),
                Name = HumanizeStatName(entry.Name),
                Category = armorCategory,
                BaseAC = baseAc,
                MaxDexBonus = maxDex,
                StealthDisadvantage = stealthDisadv,
                StrengthRequirement = strengthReq,
                Weight = KgToLbsRounded(entry.Weight),
            };
        }

        private static ArmorCategory ParseArmorCategory(string proficiencyGroup, string armorType)
        {
            if (!string.IsNullOrWhiteSpace(proficiencyGroup))
            {
                if (proficiencyGroup.Contains("Heavy", StringComparison.OrdinalIgnoreCase))
                    return ArmorCategory.Heavy;
                if (proficiencyGroup.Contains("Medium", StringComparison.OrdinalIgnoreCase))
                    return ArmorCategory.Medium;
                if (proficiencyGroup.Contains("Light", StringComparison.OrdinalIgnoreCase))
                    return ArmorCategory.Light;
                if (proficiencyGroup.Contains("Shield", StringComparison.OrdinalIgnoreCase))
                    return ArmorCategory.Shield;
            }

            if (!string.IsNullOrWhiteSpace(armorType))
            {
                if (armorType.Contains("Plate", StringComparison.OrdinalIgnoreCase) ||
                    armorType.Contains("Chain", StringComparison.OrdinalIgnoreCase) ||
                    armorType.Contains("Splint", StringComparison.OrdinalIgnoreCase) ||
                    armorType.Contains("RingMail", StringComparison.OrdinalIgnoreCase))
                {
                    return ArmorCategory.Heavy;
                }

                if (armorType.Contains("HalfPlate", StringComparison.OrdinalIgnoreCase) ||
                    armorType.Contains("ScaleMail", StringComparison.OrdinalIgnoreCase) ||
                    armorType.Contains("Breast", StringComparison.OrdinalIgnoreCase) ||
                    armorType.Contains("Hide", StringComparison.OrdinalIgnoreCase))
                {
                    return ArmorCategory.Medium;
                }
            }

            return ArmorCategory.Light;
        }

        private static WeaponProperty ParseWeaponProperties(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return WeaponProperty.None;

            WeaponProperty props = WeaponProperty.None;
            foreach (var token in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (token)
                {
                    case "Finesse": props |= WeaponProperty.Finesse; break;
                    case "Light": props |= WeaponProperty.Light; break;
                    case "Heavy": props |= WeaponProperty.Heavy; break;
                    case "Twohanded":
                    case "TwoHanded": props |= WeaponProperty.TwoHanded; break;
                    case "Versatile": props |= WeaponProperty.Versatile; break;
                    case "Thrown": props |= WeaponProperty.Thrown; break;
                    case "Ammunition": props |= WeaponProperty.Ammunition; break;
                    case "Loading": props |= WeaponProperty.Loading; break;
                    case "Reach": props |= WeaponProperty.Reach; break;
                    case "Special": props |= WeaponProperty.Special; break;
                }
            }

            return props;
        }

        private static bool TryMapWeaponType(string rawName, out WeaponType weaponType)
        {
            weaponType = WeaponType.Mace;
            string normalized = (rawName ?? string.Empty).ToLowerInvariant();

            // Order matters for ambiguous names (hand crossbow before crossbow, war pick before pick).
            var map = new (string token, WeaponType type)[]
            {
                ("handcrossbow", WeaponType.HandCrossbow),
                ("hand_crossbow", WeaponType.HandCrossbow),
                ("heavycrossbow", WeaponType.HeavyCrossbow),
                ("lightcrossbow", WeaponType.LightCrossbow),
                ("crossbow", WeaponType.LightCrossbow),
                ("longbow", WeaponType.Longbow),
                ("shortbow", WeaponType.Shortbow),
                ("greatsword", WeaponType.Greatsword),
                ("longsword", WeaponType.Longsword),
                ("shortsword", WeaponType.Shortsword),
                ("quarterstaff", WeaponType.Quarterstaff),
                ("warpick", WeaponType.WarPick),
                ("war_pick", WeaponType.WarPick),
                ("warhammer", WeaponType.Warhammer),
                ("battleaxe", WeaponType.Battleaxe),
                ("greataxe", WeaponType.Greataxe),
                ("halberd", WeaponType.Halberd),
                ("morningstar", WeaponType.Morningstar),
                ("greatclub", WeaponType.Greatclub),
                ("light hammer", WeaponType.LightHammer),
                ("lighthammer", WeaponType.LightHammer),
                ("handaxe", WeaponType.Handaxe),
                ("javelin", WeaponType.Javelin),
                ("trident", WeaponType.Trident),
                ("rapier", WeaponType.Rapier),
                ("scimitar", WeaponType.Scimitar),
                ("sickle", WeaponType.Sickle),
                ("spear", WeaponType.Spear),
                ("glaive", WeaponType.Glaive),
                ("maul", WeaponType.Maul),
                ("pike", WeaponType.Pike),
                ("flail", WeaponType.Flail),
                ("club", WeaponType.Club),
                ("dagger", WeaponType.Dagger),
                ("dart", WeaponType.Dart),
                ("mace", WeaponType.Mace),
                ("lance", WeaponType.Lance),
                ("whip", WeaponType.Whip),
            };

            foreach (var (token, mappedType) in map)
            {
                if (normalized.Contains(token, StringComparison.Ordinal))
                {
                    weaponType = mappedType;
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseDamageType(string raw, out DamageType damageType)
        {
            damageType = DamageType.Bludgeoning;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return Enum.TryParse(raw, ignoreCase: true, out damageType);
        }

        private static bool TryParseDamageDice(string raw, out int count, out int faces)
        {
            count = 1;
            faces = 4;

            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var match = Regex.Match(raw, @"(\d+)d(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            if (!int.TryParse(match.Groups[1].Value, out count))
                count = 1;
            if (!int.TryParse(match.Groups[2].Value, out faces))
                faces = 4;

            return true;
        }

        private static int KgToLbsRounded(float kg)
        {
            if (kg <= 0f)
                return 0;

            return (int)Math.Round(kg * 2.20462f);
        }

        private static int CmToFeet(int centimeters)
        {
            if (centimeters <= 0)
                return 0;

            return Math.Max(1, (int)Math.Round(centimeters / 30.48f));
        }

        private static ItemRarity ParseRarity(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return ItemRarity.Common;

            return raw.Trim().ToLowerInvariant() switch
            {
                "uncommon" => ItemRarity.Uncommon,
                "rare" => ItemRarity.Rare,
                "veryrare" => ItemRarity.VeryRare,
                "legendary" => ItemRarity.Legendary,
                _ => ItemRarity.Common,
            };
        }

        private static string HumanizeStatName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Unknown Item";

            string value = raw.Trim();
            foreach (var prefix in new[] { "WPN_", "ARM_", "UNI_", "OBJ_", "MAG_" })
            {
                if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value[prefix.Length..];
                    break;
                }
            }

            value = Regex.Replace(value, @"_(Body|Boots|Gloves|Helmet|Cloak|Ring|Amulet)$", string.Empty, RegexOptions.IgnoreCase);
            value = Regex.Replace(value, @"_\d+$", string.Empty, RegexOptions.IgnoreCase);
            value = value.Replace("_", " ");
            value = Regex.Replace(value, @"\s+", " ").Trim();

            if (value.Length == 0)
                return "Unknown Item";

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }

        private static int ComputeDeterministicSeed(string input)
        {
            if (string.IsNullOrEmpty(input))
                return 17;

            unchecked
            {
                int hash = 23;
                foreach (char c in input)
                    hash = (hash * 31) + c;
                return hash;
            }
        }

        private static string NormalizeDefinitionId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "item_unknown";

            return raw.Trim().ToLowerInvariant();
        }

        private static string ResolveWeaponIconPath(WeaponDefinition weaponDef, string rawName)
        {
            var candidates = new List<string>();
            AddIconNameCandidates(candidates, rawName);
            AddIconNameCandidates(candidates, weaponDef?.Id);
            AddIconNameCandidates(candidates, weaponDef?.Name);

            if (weaponDef != null && WeaponTypeIconCandidates.TryGetValue(weaponDef.WeaponType, out var mapped))
            {
                foreach (var candidate in mapped)
                    AddIconNameCandidates(candidates, candidate);
            }

            if (TryResolveSpecificIcon(candidates, out var iconPath))
                return iconPath;

            return weaponDef?.IsRanged == true ? IconRangedWeapon : IconMeleeWeapon;
        }

        private static string ResolveArmorIconPath(
            ItemCategory category,
            EquipSlot slot,
            ArmorDefinition armorDef,
            string rawName,
            string rawArmorType)
        {
            var candidates = new List<string>();
            AddIconNameCandidates(candidates, rawName);
            AddIconNameCandidates(candidates, armorDef?.Id);
            AddIconNameCandidates(candidates, armorDef?.Name);
            AddArmorTypeIconCandidates(candidates, rawArmorType);

            if (armorDef != null)
            {
                switch (armorDef.Category)
                {
                    case ArmorCategory.Light:
                        AddIconNameCandidates(candidates, "Leather Armour");
                        break;
                    case ArmorCategory.Medium:
                        AddIconNameCandidates(candidates, "Scale Mail");
                        break;
                    case ArmorCategory.Heavy:
                        AddIconNameCandidates(candidates, "Plate Armour");
                        break;
                    case ArmorCategory.Shield:
                        AddIconNameCandidates(candidates, "Wooden Shield");
                        break;
                }
            }

            switch (category)
            {
                case ItemCategory.Shield:
                    AddIconNameCandidates(candidates, "Shield");
                    AddIconNameCandidates(candidates, "Wooden Shield");
                    AddIconNameCandidates(candidates, "Studded Shield");
                    break;
                case ItemCategory.Clothing:
                    AddIconNameCandidates(candidates, "Robe");
                    AddIconNameCandidates(candidates, "Selunite Robe");
                    break;
                case ItemCategory.Headwear:
                    AddIconNameCandidates(candidates, "Helmet");
                    break;
                case ItemCategory.Handwear:
                    AddIconNameCandidates(candidates, "Gloves");
                    break;
                case ItemCategory.Footwear:
                    AddIconNameCandidates(candidates, "Boots");
                    break;
            }

            if (TryResolveSpecificIcon(candidates, out var iconPath))
                return iconPath;

            return GetEquipmentIcon(slot, category);
        }

        private static bool TryResolveSpecificIcon(IEnumerable<string> candidates, out string iconPath)
        {
            iconPath = null;
            var lookup = SpecificIconLookup.Value;
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                string key = NormalizeIconKey(candidate);
                if (key.Length == 0)
                    continue;

                if (lookup.TryGetValue(key, out var match))
                {
                    iconPath = match;
                    return true;
                }
            }

            return false;
        }

        private static void AddArmorTypeIconCandidates(List<string> candidates, string rawArmorType)
        {
            if (string.IsNullOrWhiteSpace(rawArmorType))
                return;

            AddIconNameCandidates(candidates, rawArmorType);
            string normalized = NormalizeIconKey(rawArmorType);
            switch (normalized)
            {
                case "padded":
                    AddIconNameCandidates(candidates, "Padded Armour");
                    break;
                case "leather":
                    AddIconNameCandidates(candidates, "Leather Armour");
                    break;
                case "studdedleather":
                    AddIconNameCandidates(candidates, "Studded Leather");
                    AddIconNameCandidates(candidates, "Studded Leather Armour");
                    break;
                case "hide":
                    AddIconNameCandidates(candidates, "Hide Armour");
                    break;
                case "chainshirt":
                    AddIconNameCandidates(candidates, "Chain Shirt");
                    break;
                case "scalemail":
                    AddIconNameCandidates(candidates, "Scale Mail");
                    break;
                case "breastplate":
                    AddIconNameCandidates(candidates, "Breastplate");
                    break;
                case "halfplate":
                    AddIconNameCandidates(candidates, "Half Plate");
                    AddIconNameCandidates(candidates, "Half Plate Armour");
                    break;
                case "ringmail":
                    AddIconNameCandidates(candidates, "Ring Mail");
                    break;
                case "chainmail":
                    AddIconNameCandidates(candidates, "Chain Mail");
                    break;
                case "splint":
                    AddIconNameCandidates(candidates, "Splint Mail");
                    break;
                case "plate":
                    AddIconNameCandidates(candidates, "Plate Armour");
                    break;
                case "cloth":
                case "none":
                    AddIconNameCandidates(candidates, "Robe");
                    break;
            }
        }

        private static void AddIconNameCandidates(List<string> candidates, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return;

            string value = raw.Trim();
            candidates.Add(value);
            candidates.Add(value.Replace('_', ' '));
            candidates.Add(HumanizeStatName(value));

            string trimmed = Regex.Replace(value, @"^(WPN|ARM|UNI|OBJ|MAG)_", string.Empty, RegexOptions.IgnoreCase);
            trimmed = Regex.Replace(trimmed, @"_(Body|Boots|Gloves|Helmet|Cloak|Ring|Amulet|Shield|Offhand)$", string.Empty, RegexOptions.IgnoreCase);
            trimmed = Regex.Replace(trimmed, @"_\d+$", string.Empty, RegexOptions.IgnoreCase);
            candidates.Add(trimmed);
            candidates.Add(trimmed.Replace('_', ' '));
        }

        private static string NormalizeIconKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (char ch in value)
            {
                if (char.IsLetterOrDigit(ch))
                    builder.Append(char.ToLowerInvariant(ch));
            }

            return builder.ToString();
        }

        private static Dictionary<string, string> BuildSpecificIconLookup()
        {
            var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string root = ProjectRootPath.Value;
            foreach (var relativeDirectory in new[] { IconWeaponDirectory, IconArmorDirectory })
            {
                string absoluteDir = Path.Combine(root, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteDir))
                    continue;

                foreach (string pattern in new[] { "*.png", "*.webp", "*.jpg", "*.jpeg" })
                {
                    foreach (var file in Directory.EnumerateFiles(absoluteDir, pattern, SearchOption.TopDirectoryOnly))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);
                        string normalizedName = Regex.Replace(fileName, @"_Unfaded_Icon$", string.Empty, RegexOptions.IgnoreCase);
                        normalizedName = Regex.Replace(normalizedName, @"_Icon$", string.Empty, RegexOptions.IgnoreCase);
                        string key = NormalizeIconKey(normalizedName);
                        if (key.Length == 0 || lookup.ContainsKey(key))
                            continue;

                        string relativePath = Path.GetRelativePath(root, file).Replace('\\', '/');
                        lookup[key] = $"res://{relativePath}";
                    }
                }
            }

            return lookup;
        }

        private static string FindProjectRootPath()
        {
            string root = TryFindProjectRootPath(Directory.GetCurrentDirectory());
            if (!string.IsNullOrEmpty(root))
                return root;

            root = TryFindProjectRootPath(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(root))
                return root;

            return Directory.GetCurrentDirectory();
        }

        private static string TryFindProjectRootPath(string startPath)
        {
            if (string.IsNullOrWhiteSpace(startPath))
                return null;

            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "project.godot")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }

        private static string GetEquipmentIcon(EquipSlot slot, ItemCategory category)
        {
            if (category == ItemCategory.Shield)
                return IconShield;

            return slot switch
            {
                EquipSlot.Boots => IconBoots,
                EquipSlot.Cloak => IconCloak,
                EquipSlot.Amulet => IconAmulet,
                EquipSlot.MainHand => IconMeleeWeapon,
                EquipSlot.OffHand => IconShield,
                _ => category switch
                {
                    ItemCategory.Armor => IconGenericPhysical,
                    ItemCategory.Clothing => IconGenericUtility,
                    ItemCategory.Headwear => IconGenericUtility,
                    ItemCategory.Handwear => IconGenericUtility,
                    ItemCategory.Footwear => IconBoots,
                    ItemCategory.Cloak => IconCloak,
                    ItemCategory.Amulet => IconAmulet,
                    ItemCategory.Ring => IconGenericMagical,
                    ItemCategory.Accessory => IconGenericMagical,
                    _ => IconGenericUtility,
                },
            };
        }

        private void EmitEquipmentChanged(string combatantId, EquipSlot slot)
        {
            OnEquipmentChanged?.Invoke(combatantId, slot);
        }

        private sealed class InventoryTemplate
        {
            public string DefinitionId { get; init; }
            public string Name { get; init; }
            public ItemCategory Category { get; init; }
            public int Quantity { get; init; } = 1;
            public string Description { get; init; }
            public int Weight { get; init; }
            public string IconPath { get; init; }
            public ItemRarity Rarity { get; init; }
            public WeaponDefinition WeaponDef { get; init; }
            public ArmorDefinition ArmorDef { get; init; }
            public HashSet<EquipSlot> AllowedSlots { get; init; } = new();

            public InventoryItem ToInventoryItem()
            {
                return new InventoryItem
                {
                    DefinitionId = DefinitionId,
                    Name = Name,
                    Category = Category,
                    Quantity = Quantity,
                    Description = Description,
                    Weight = Weight,
                    IconPath = IconPath,
                    WeaponDef = WeaponDef,
                    ArmorDef = ArmorDef,
                    Rarity = Rarity,
                    AllowedEquipSlots = new HashSet<EquipSlot>(AllowedSlots),
                };
            }
        }
    }
}
