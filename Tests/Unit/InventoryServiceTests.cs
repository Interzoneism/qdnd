using System.Linq;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Data.CharacterModel;
using QDND.Data.Stats;
using Xunit;

namespace QDND.Tests.Unit
{
    public class InventoryServiceTests
    {
        [Fact]
        public void MoveBagItemToBagSlot_ReordersItems()
        {
            var service = new InventoryService(new CharacterDataRegistry());
            var actor = CreateCombatant();

            service.AddItemToBag(actor, InventoryService.CreateConsumableItem("a", "A", ItemCategory.Misc, ""));
            service.AddItemToBag(actor, InventoryService.CreateConsumableItem("b", "B", ItemCategory.Misc, ""));

            bool ok = service.MoveBagItemToBagSlot(actor, 0, 1, out var reason);

            Assert.True(ok, reason);
            var inv = service.GetInventory(actor.Id);
            Assert.Equal("b", inv.BagItems[0].DefinitionId);
            Assert.Equal("a", inv.BagItems[1].DefinitionId);
        }

        [Fact]
        public void MoveBagToEquip_AndBackToBag_Works()
        {
            var service = new InventoryService(new CharacterDataRegistry());
            var actor = CreateCombatant();

            var sword = new WeaponDefinition
            {
                Id = "test_sword",
                Name = "Test Sword",
                WeaponType = WeaponType.Longsword,
                Category = WeaponCategory.Martial,
                DamageType = DamageType.Slashing,
                DamageDiceCount = 1,
                DamageDieFaces = 8,
                NormalRange = 5,
                Weight = 3,
            };

            service.AddItemToBag(actor, InventoryService.CreateWeaponItem(sword));

            bool equipOk = service.MoveBagItemToEquipSlot(actor, 0, EquipSlot.MainHand, out var equipReason);
            Assert.True(equipOk, equipReason);

            var inv = service.GetInventory(actor.Id);
            Assert.NotNull(inv.GetEquipped(EquipSlot.MainHand));
            Assert.Empty(inv.BagItems);

            bool unequipOk = service.MoveEquippedItemToBagSlot(actor, EquipSlot.MainHand, 0, out var unequipReason);
            Assert.True(unequipOk, unequipReason);
            Assert.Null(inv.GetEquipped(EquipSlot.MainHand));
            Assert.Single(inv.BagItems);
        }

        [Fact]
        public void CannotEquipOffhand_WhenMainHandIsTwoHanded()
        {
            var service = new InventoryService(new CharacterDataRegistry());
            var actor = CreateCombatant();

            var greatsword = new WeaponDefinition
            {
                Id = "greatsword",
                Name = "Greatsword",
                WeaponType = WeaponType.Greatsword,
                Category = WeaponCategory.Martial,
                DamageType = DamageType.Slashing,
                DamageDiceCount = 2,
                DamageDieFaces = 6,
                Properties = WeaponProperty.TwoHanded,
                NormalRange = 5,
                Weight = 6,
            };

            var shield = new ArmorDefinition
            {
                Id = "shield",
                Name = "Shield",
                Category = ArmorCategory.Shield,
                BaseAC = 2,
                MaxDexBonus = 0,
                Weight = 6,
            };

            service.AddItemToBag(actor, InventoryService.CreateWeaponItem(greatsword));
            service.AddItemToBag(actor, InventoryService.CreateArmorItem(shield, ItemCategory.Shield));

            Assert.True(service.MoveBagItemToEquipSlot(actor, 0, EquipSlot.MainHand, out var mainReason), mainReason);
            Assert.False(service.MoveBagItemToEquipSlot(actor, 0, EquipSlot.OffHand, out var offReason));
            Assert.Contains("blocks off-hand", offReason);
        }

        [Fact]
        public void CreateWeaponItem_RangedWeaponsExposeRangedSlots()
        {
            var shortbow = new WeaponDefinition
            {
                Id = "shortbow",
                Name = "Shortbow",
                WeaponType = WeaponType.Shortbow,
                Category = WeaponCategory.Simple,
                DamageType = DamageType.Piercing,
                DamageDiceCount = 1,
                DamageDieFaces = 6,
                Properties = WeaponProperty.TwoHanded | WeaponProperty.Ammunition,
                NormalRange = 60,
                LongRange = 320,
                Weight = 2,
            };

            var handCrossbow = new WeaponDefinition
            {
                Id = "hand_crossbow",
                Name = "Hand Crossbow",
                WeaponType = WeaponType.HandCrossbow,
                Category = WeaponCategory.Martial,
                DamageType = DamageType.Piercing,
                DamageDiceCount = 1,
                DamageDieFaces = 6,
                Properties = WeaponProperty.Light | WeaponProperty.Ammunition,
                NormalRange = 30,
                LongRange = 120,
                Weight = 3,
            };

            var shortbowItem = InventoryService.CreateWeaponItem(shortbow);
            var handCrossbowItem = InventoryService.CreateWeaponItem(handCrossbow);

            Assert.Contains(EquipSlot.RangedMainHand, shortbowItem.AllowedEquipSlots);
            Assert.DoesNotContain(EquipSlot.RangedOffHand, shortbowItem.AllowedEquipSlots);
            Assert.Contains(EquipSlot.RangedMainHand, handCrossbowItem.AllowedEquipSlots);
            Assert.Contains(EquipSlot.RangedOffHand, handCrossbowItem.AllowedEquipSlots);
        }

        [Fact]
        public void CreateWeaponItem_UsesSpecificWeaponIcon_WhenAvailable()
        {
            var longsword = new WeaponDefinition
            {
                Id = "wpn_longsword",
                Name = "Longsword",
                WeaponType = WeaponType.Longsword,
                Category = WeaponCategory.Martial,
                DamageType = DamageType.Slashing,
                DamageDiceCount = 1,
                DamageDieFaces = 8,
                NormalRange = 5,
                Weight = 3,
            };

            var item = InventoryService.CreateWeaponItem(longsword);

            Assert.NotNull(item);
            Assert.Contains("Icons Weapons and Other/Longsword_Unfaded_Icon", item.IconPath);
        }

        [Fact]
        public void EquippingTwoHandedRangedMain_DisplacesRangedOffHand()
        {
            var service = new InventoryService(new CharacterDataRegistry());
            var actor = CreateCombatant();

            var handCrossbow = new WeaponDefinition
            {
                Id = "hand_crossbow",
                Name = "Hand Crossbow",
                WeaponType = WeaponType.HandCrossbow,
                Category = WeaponCategory.Martial,
                DamageType = DamageType.Piercing,
                DamageDiceCount = 1,
                DamageDieFaces = 6,
                Properties = WeaponProperty.Light | WeaponProperty.Ammunition,
                NormalRange = 30,
                LongRange = 120,
                Weight = 3,
            };

            var longbow = new WeaponDefinition
            {
                Id = "longbow",
                Name = "Longbow",
                WeaponType = WeaponType.Longbow,
                Category = WeaponCategory.Martial,
                DamageType = DamageType.Piercing,
                DamageDiceCount = 1,
                DamageDieFaces = 8,
                Properties = WeaponProperty.TwoHanded | WeaponProperty.Ammunition,
                NormalRange = 60,
                LongRange = 320,
                Weight = 2,
            };

            service.AddItemToBag(actor, InventoryService.CreateWeaponItem(handCrossbow));
            service.AddItemToBag(actor, InventoryService.CreateWeaponItem(longbow));

            Assert.True(service.MoveBagItemToEquipSlot(actor, 0, EquipSlot.RangedOffHand, out var offhandReason), offhandReason);
            Assert.True(service.MoveBagItemToEquipSlot(actor, 0, EquipSlot.RangedMainHand, out var rangedMainReason), rangedMainReason);

            var inv = service.GetInventory(actor.Id);
            Assert.NotNull(inv.GetEquipped(EquipSlot.RangedMainHand));
            Assert.Null(inv.GetEquipped(EquipSlot.RangedOffHand));
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "hand_crossbow");
        }

        [Fact]
        public void InitializeFromCombatant_SeedsItemsFromStatsRegistry()
        {
            var stats = new StatsRegistry();
            stats.RegisterWeapon(new BG3WeaponData
            {
                Name = "WPN_Longsword",
                Damage = "1d8",
                DamageType = "Slashing",
                WeaponGroup = "MartialMeleeWeapon",
                WeaponProperties = "Versatile;Melee",
                WeaponRange = 150,
                DamageRange = 300,
                Weight = 1.3f,
                InventoryTab = "Equipment",
                Rarity = "Rare",
            });
            stats.RegisterArmor(new BG3ArmorData
            {
                Name = "ARM_Leather_Body",
                Slot = "Breast",
                ArmorClass = 11,
                ArmorClassAbility = "Dexterity",
                AbilityModifierCap = 0,
                ProficiencyGroup = "LightArmor",
                InventoryTab = "Equipment",
                Weight = 2.5f,
                Rarity = "Uncommon",
            });
            stats.RegisterArmor(new BG3ArmorData
            {
                Name = "UNI_TestRing",
                Slot = "Ring",
                InventoryTab = "Equipment",
                Weight = 0.1f,
                Rarity = "Uncommon",
            });

            var service = new InventoryService(new CharacterDataRegistry(), stats);
            var actor = CreateCombatant();

            service.InitializeFromCombatant(actor);
            var inv = service.GetInventory(actor.Id);

            Assert.NotEmpty(inv.BagItems);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "wpn_longsword");
            Assert.Contains(inv.BagItems, i => i.AllowedEquipSlots.Contains(EquipSlot.Ring1));

            int ringIndex = inv.BagItems.FindIndex(i => i.AllowedEquipSlots.Contains(EquipSlot.Ring1));
            Assert.True(ringIndex >= 0);
            Assert.True(service.MoveBagItemToEquipSlot(actor, ringIndex, EquipSlot.Ring1, out var reason), reason);
            Assert.NotNull(inv.GetEquipped(EquipSlot.Ring1));
        }

        [Fact]
        public void InitializeFromCombatant_BreastWithoutProficiency_IsClothingCategory()
        {
            var stats = new StatsRegistry();
            stats.RegisterArmor(new BG3ArmorData
            {
                Name = "ARM_Wizard_Robe_Body",
                Slot = "Breast",
                ArmorClass = 10,
                ArmorClassAbility = "Dexterity",
                ArmorType = "None",
                ProficiencyGroup = "",
                InventoryTab = "Equipment",
                Weight = 1.5f,
            });

            var service = new InventoryService(new CharacterDataRegistry(), stats);
            var actor = CreateCombatant();

            service.InitializeFromCombatant(actor);
            var inv = service.GetInventory(actor.Id);
            var robe = inv.BagItems.FirstOrDefault(i => i.DefinitionId == "arm_wizard_robe_body");

            Assert.NotNull(robe);
            Assert.Equal(ItemCategory.Clothing, robe.Category);
            Assert.Contains(EquipSlot.Armor, robe.AllowedEquipSlots);
        }

        [Fact]
        public void InitializeFromCombatant_MapsWearableSlotsToSpecificCategories()
        {
            var stats = new StatsRegistry();
            stats.RegisterArmor(new BG3ArmorData
            {
                Name = "ARM_Leather_Body",
                Slot = "Breast",
                ArmorClass = 11,
                ArmorClassAbility = "Dexterity",
                ProficiencyGroup = "LightArmor",
                ArmorType = "Leather",
                InventoryTab = "Equipment",
            });
            stats.RegisterArmor(new BG3ArmorData { Name = "UNI_Test_Helmet", Slot = "Helmet", InventoryTab = "Equipment" });
            stats.RegisterArmor(new BG3ArmorData { Name = "UNI_Test_Gloves", Slot = "Gloves", InventoryTab = "Equipment" });
            stats.RegisterArmor(new BG3ArmorData { Name = "UNI_Test_Boots", Slot = "Boots", InventoryTab = "Equipment" });
            stats.RegisterArmor(new BG3ArmorData { Name = "UNI_Test_Cloak", Slot = "Cloak", InventoryTab = "Equipment" });
            stats.RegisterArmor(new BG3ArmorData { Name = "UNI_Test_Amulet", Slot = "Amulet", InventoryTab = "Equipment" });
            stats.RegisterArmor(new BG3ArmorData { Name = "UNI_Test_Ring", Slot = "Ring", InventoryTab = "Equipment" });
            stats.RegisterArmor(new BG3ArmorData
            {
                Name = "ARM_Test_Shield",
                Slot = "Melee Offhand Weapon",
                Shield = "Yes",
                ProficiencyGroup = "Shields",
                ArmorClass = 2,
                InventoryTab = "Equipment",
            });

            var service = new InventoryService(new CharacterDataRegistry(), stats);
            var actor = CreateCombatant();

            service.InitializeFromCombatant(actor);
            var inv = service.GetInventory(actor.Id);

            Assert.Contains(inv.BagItems, i => i.DefinitionId == "arm_leather_body" && i.Category == ItemCategory.Armor);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_helmet" && i.Category == ItemCategory.Headwear);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_gloves" && i.Category == ItemCategory.Handwear);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_boots" && i.Category == ItemCategory.Footwear);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_cloak" && i.Category == ItemCategory.Cloak);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_amulet" && i.Category == ItemCategory.Amulet);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_ring" && i.Category == ItemCategory.Ring);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "arm_test_shield" && i.Category == ItemCategory.Shield);
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "uni_test_ring" && i.AllowedEquipSlots.Contains(EquipSlot.Ring1) && i.AllowedEquipSlots.Contains(EquipSlot.Ring2));
            Assert.Contains(inv.BagItems, i => i.DefinitionId == "arm_test_shield" && i.AllowedEquipSlots.SetEquals(new[] { EquipSlot.OffHand }));
        }

        [Fact]
        public void InitializeFromCombatant_UsesSpecificArmorIcon_WhenAvailable()
        {
            var stats = new StatsRegistry();
            stats.RegisterArmor(new BG3ArmorData
            {
                Name = "ARM_Leather_Body",
                Slot = "Breast",
                ArmorClass = 11,
                ArmorClassAbility = "Dexterity",
                ProficiencyGroup = "LightArmor",
                ArmorType = "Leather",
                InventoryTab = "Equipment",
            });

            var service = new InventoryService(new CharacterDataRegistry(), stats);
            var actor = CreateCombatant();

            service.InitializeFromCombatant(actor);
            var inv = service.GetInventory(actor.Id);
            var item = inv.BagItems.First(i => i.DefinitionId == "arm_leather_body");

            Assert.Contains("Icons Armour/Leather_Armour_Unfaded_Icon", item.IconPath);
        }

        private static Combatant CreateCombatant()
        {
            return new Combatant("test_actor", "Test Actor", Faction.Player, 20, 10)
            {
                Stats = new CombatantStats
                {
                    Strength = 16,
                    Dexterity = 14,
                    Constitution = 14,
                    Intelligence = 10,
                    Wisdom = 10,
                    Charisma = 10,
                    BaseAC = 10,
                }
            };
        }
    }
}
