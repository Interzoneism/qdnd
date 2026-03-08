using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using QDND.Combat.Actions;
using QDND.Data.Actions;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// Verifies that the equipment system correctly:
    /// 1. Loads all weapons and armors from equipment_data.json
    /// 2. Weapons have correct GrantedActionIds matching BG3 data
    /// 3. Weapon action definitions exist for all granted action IDs
    /// 4. AC calculation follows D&D 5e/BG3 rules
    /// 5. All 34 PHB weapons and 13 armor types are present
    /// </summary>
    public class EquipmentWeaponActionTests
    {
        private readonly ITestOutputHelper _output;
        private readonly CharacterDataRegistry _registry;
        private readonly ActionRegistry _actionRegistry;
        private readonly string _repoRoot;
        private readonly string _dataPath;

        public EquipmentWeaponActionTests(ITestOutputHelper output)
        {
            _output = output;
            _repoRoot = ResolveRepoRoot();
            _dataPath = Path.Combine(_repoRoot, "Data");
            _registry = new CharacterDataRegistry();
            _registry.LoadFromDirectory(_dataPath);

            _actionRegistry = new ActionRegistry();
            var init = ActionRegistryInitializer.Initialize(
                _actionRegistry,
                Path.Combine(_repoRoot, "BG3_Data"),
                verboseLogging: false);
            if (!init.Success)
            {
                throw new InvalidOperationException(init.ErrorMessage ?? "Failed to initialize action registry");
            }
        }

        private static string ResolveRepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                    return dir;

                dir = Directory.GetParent(dir)?.FullName;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        }

        // =================================================================
        //  Weapon Count & Coverage
        // =================================================================

        [Fact]
        public void Registry_Contains_All34_PHBWeapons()
        {
            var weapons = _registry.GetAllWeapons();
            Assert.True(weapons.Count >= 34, $"Expected at least 34 weapons, got {weapons.Count}");
            _output.WriteLine($"Total weapons in registry: {weapons.Count}");
        }

        [Fact]
        public void Registry_Contains_All13_ArmorTypes()
        {
            var armors = _registry.GetAllArmors();
            Assert.True(armors.Count >= 13, $"Expected at least 13 armor types, got {armors.Count}");
            _output.WriteLine($"Total armors in registry: {armors.Count}");
        }

        // =================================================================
        //  Specific Weapon Lookups
        // =================================================================

        [Theory]
        [InlineData("longsword", "Longsword", 1, 8, "Slashing")]
        [InlineData("greatsword", "Greatsword", 2, 6, "Slashing")]
        [InlineData("dagger", "Dagger", 1, 4, "Piercing")]
        [InlineData("greataxe", "Greataxe", 1, 12, "Slashing")]
        [InlineData("rapier", "Rapier", 1, 8, "Piercing")]
        [InlineData("shortbow", "Shortbow", 1, 6, "Piercing")]
        [InlineData("longbow", "Longbow", 1, 8, "Piercing")]
        [InlineData("hand_crossbow", "Hand Crossbow", 1, 6, "Piercing")]
        public void Weapon_HasCorrectDamageDice(string id, string expectedName, int diceCount, int diceFaces, string damageType)
        {
            var weapon = _registry.GetWeapon(id);
            Assert.NotNull(weapon);
            Assert.Equal(expectedName, weapon.Name);
            Assert.Equal(diceCount, weapon.DamageDiceCount);
            Assert.Equal(diceFaces, weapon.DamageDieFaces);
            Assert.Equal(Enum.Parse<DamageType>(damageType), weapon.DamageType);
        }

        [Theory]
        [InlineData("dagger", WeaponProperty.Finesse | WeaponProperty.Light | WeaponProperty.Thrown)]
        [InlineData("longsword", WeaponProperty.Versatile)]
        [InlineData("greatsword", WeaponProperty.Heavy | WeaponProperty.TwoHanded)]
        [InlineData("rapier", WeaponProperty.Finesse)]
        [InlineData("longbow", WeaponProperty.Heavy | WeaponProperty.TwoHanded | WeaponProperty.Ammunition)]
        public void Weapon_HasCorrectProperties(string id, WeaponProperty expectedProps)
        {
            var weapon = _registry.GetWeapon(id);
            Assert.NotNull(weapon);
            // Check that all expected flags are set
            Assert.True((weapon.Properties & expectedProps) == expectedProps,
                $"{weapon.Name} expected properties {expectedProps} but got {weapon.Properties}");
        }

        // =================================================================
        //  Weapon Action Grants
        // =================================================================

        [Theory]
        [InlineData("longsword", new[] { "pommel_strike", "lacerate", "spring_attack" })]
        [InlineData("greatsword", new[] { "pommel_strike", "lacerate", "cleave" })]
        [InlineData("battleaxe", new[] { "cleave", "lacerate", "crippling_strike" })]
        [InlineData("dagger", new[] { "piercing_thrust" })]
        [InlineData("quarterstaff", new[] { "topple" })]
        [InlineData("rapier", new[] { "opening_attack", "piercing_thrust", "hindering_smash" })]
        [InlineData("longbow", new[] { "hamstring_shot", "steady_ranged" })]
        [InlineData("hand_crossbow", new[] { "piercing_shot", "mobile_shooting" })]
        [InlineData("maul", new[] { "posture_breaker", "smash" })]
        [InlineData("morningstar", new[] { "heart_stopper", "smash" })]
        [InlineData("shortsword", new[] { "opening_attack", "piercing_thrust" })]
        [InlineData("trident", new[] { "spring_attack", "piercing_thrust", "disarming_strike" })]
        public void Weapon_GrantsCorrectActions(string weaponId, string[] expectedActions)
        {
            var weapon = _registry.GetWeapon(weaponId);
            Assert.NotNull(weapon);
            Assert.NotNull(weapon.GrantedActionIds);

            foreach (var expected in expectedActions)
            {
                Assert.Contains(expected, weapon.GrantedActionIds);
            }

            Assert.Equal(expectedActions.Length, weapon.GrantedActionIds.Count);
            _output.WriteLine($"{weapon.Name}: [{string.Join(", ", weapon.GrantedActionIds)}]");
        }

        [Theory]
        [InlineData("lance")]
        [InlineData("whip")]
        public void Weapon_WithNoActions_HasEmptyGrants(string weaponId)
        {
            var weapon = _registry.GetWeapon(weaponId);
            Assert.NotNull(weapon);
            Assert.True(weapon.GrantedActionIds == null || weapon.GrantedActionIds.Count == 0,
                $"{weapon.Name} should have no granted actions but has: [{string.Join(", ", weapon.GrantedActionIds ?? new List<string>())}]");
        }

        [Fact]
        public void AllWeapons_GrantedActionsExistInActionRegistry()
        {
            var actionIds = LoadAllActionIds();
            var weapons = _registry.GetAllWeapons();
            var missing = new List<string>();

            foreach (var weapon in weapons)
            {
                if (weapon.GrantedActionIds == null) continue;
                foreach (var actionId in weapon.GrantedActionIds)
                {
                    if (!actionIds.Contains(actionId))
                        missing.Add($"{weapon.Name} grants '{actionId}' but action not found");
                }
            }

            if (missing.Any())
            {
                _output.WriteLine("Missing action definitions:");
                foreach (var m in missing) _output.WriteLine($"  {m}");
            }

            Assert.Empty(missing);
        }

        // =================================================================
        //  Weapon Action Definitions Quality
        // =================================================================

        [Fact]
        public void AllWeaponActions_HaveRequiredFields()
        {
            var actions = LoadWeaponActions();
            Assert.True(actions.Count >= 20, $"Expected at least 20 weapon actions, got {actions.Count}");

            foreach (var action in actions)
            {
                Assert.False(string.IsNullOrEmpty(action.Id), "Weapon action missing ID");
                Assert.False(string.IsNullOrEmpty(action.Name), $"Weapon action {action.Id} missing Name");
                Assert.NotNull(action.Description);
                Assert.NotNull(action.ExtraDescription);
                Assert.NotNull(action.Tags);
                _output.WriteLine($"  {action.Id}: {action.Name}");
            }
        }

        [Theory]
        [InlineData("cleave")]
        [InlineData("lacerate")]
        [InlineData("smash")]
        [InlineData("topple")]
        [InlineData("pommel_strike")]
        [InlineData("piercing_shot")]
        [InlineData("hamstring_shot")]
        [InlineData("headcrack")]
        [InlineData("mobile_shooting")]
        [InlineData("steady_ranged")]
        [InlineData("steady")]
        [InlineData("spring_attack")]
        public void WeaponAction_ExistsInRegistry(string actionId)
        {
            var action = _actionRegistry.GetAction(actionId);
            Assert.NotNull(action);
        }

        // =================================================================
        //  Armor AC Calculation
        // =================================================================

        [Theory]
        [InlineData("padded", 11)]
        [InlineData("leather", 11)]
        [InlineData("studded_leather", 12)]
        [InlineData("hide", 12)]
        [InlineData("chain_shirt", 13)]
        [InlineData("scale_mail", 14)]
        [InlineData("breastplate", 14)]
        [InlineData("half_plate", 15)]
        [InlineData("ring_mail", 14)]
        [InlineData("chain_mail", 16)]
        [InlineData("splint", 17)]
        [InlineData("plate", 18)]
        public void Armor_HasCorrectBaseAC(string armorId, int expectedAC)
        {
            var armor = _registry.GetArmor(armorId);
            Assert.NotNull(armor);
            Assert.Equal(expectedAC, armor.BaseAC);
        }

        [Theory]
        [InlineData("padded", ArmorCategory.Light)]
        [InlineData("leather", ArmorCategory.Light)]
        [InlineData("studded_leather", ArmorCategory.Light)]
        [InlineData("hide", ArmorCategory.Medium)]
        [InlineData("chain_shirt", ArmorCategory.Medium)]
        [InlineData("scale_mail", ArmorCategory.Medium)]
        [InlineData("breastplate", ArmorCategory.Medium)]
        [InlineData("half_plate", ArmorCategory.Medium)]
        [InlineData("ring_mail", ArmorCategory.Heavy)]
        [InlineData("chain_mail", ArmorCategory.Heavy)]
        [InlineData("splint", ArmorCategory.Heavy)]
        [InlineData("plate", ArmorCategory.Heavy)]
        [InlineData("shield", ArmorCategory.Shield)]
        public void Armor_HasCorrectCategory(string armorId, ArmorCategory expectedCategory)
        {
            var armor = _registry.GetArmor(armorId);
            Assert.NotNull(armor);
            Assert.Equal(expectedCategory, armor.Category);
        }

        [Theory]
        [InlineData("hide", 2)]
        [InlineData("chain_shirt", 2)]
        [InlineData("scale_mail", 2)]
        [InlineData("breastplate", 2)]
        [InlineData("half_plate", 2)]
        public void MediumArmor_CapsMaxDexBonusAt2(string armorId, int expectedMaxDex)
        {
            var armor = _registry.GetArmor(armorId);
            Assert.NotNull(armor);
            Assert.True(armor.MaxDexBonus.HasValue);
            Assert.Equal(expectedMaxDex, armor.MaxDexBonus.Value);
        }

        [Theory]
        [InlineData("ring_mail", 0)]
        [InlineData("chain_mail", 0)]
        [InlineData("splint", 0)]
        [InlineData("plate", 0)]
        public void HeavyArmor_HasZeroMaxDexBonus(string armorId, int expectedMaxDex)
        {
            var armor = _registry.GetArmor(armorId);
            Assert.NotNull(armor);
            Assert.True(armor.MaxDexBonus.HasValue);
            Assert.Equal(expectedMaxDex, armor.MaxDexBonus.Value);
        }

        [Theory]
        [InlineData("padded")]
        [InlineData("leather")]
        [InlineData("studded_leather")]
        public void LightArmor_HasNoMaxDexCap(string armorId)
        {
            var armor = _registry.GetArmor(armorId);
            Assert.NotNull(armor);
            Assert.False(armor.MaxDexBonus.HasValue,
                $"Light armor {armorId} should not cap Dex bonus");
        }

        // =================================================================
        //  Weapon Type Coverage
        // =================================================================

        [Fact]
        public void AllSimpleWeapons_ArePresent()
        {
            var simpleWeapons = new[]
            {
                "club", "dagger", "greatclub", "handaxe", "javelin",
                "light_hammer", "mace", "quarterstaff", "sickle", "spear",
                "dart", "light_crossbow", "shortbow"
            };

            foreach (var id in simpleWeapons)
            {
                var weapon = _registry.GetWeapon(id);
                Assert.NotNull(weapon);
                Assert.Equal(WeaponCategory.Simple, weapon.Category);
            }
        }

        [Fact]
        public void AllMartialWeapons_ArePresent()
        {
            var martialWeapons = new[]
            {
                "battleaxe", "flail", "glaive", "greataxe", "greatsword",
                "halberd", "lance", "longsword", "maul", "morningstar",
                "pike", "rapier", "scimitar", "shortsword", "trident",
                "war_pick", "warhammer", "whip",
                "hand_crossbow", "heavy_crossbow", "longbow"
            };

            foreach (var id in martialWeapons)
            {
                var weapon = _registry.GetWeapon(id);
                Assert.NotNull(weapon);
                Assert.Equal(WeaponCategory.Martial, weapon.Category);
            }
        }

        // =================================================================
        //  Summary Report
        // =================================================================

        [Fact]
        public void Equipment_CoverageSummary()
        {
            var weapons = _registry.GetAllWeapons().ToList();
            var armors = _registry.GetAllArmors().ToList();

            int weaponsWithActions = weapons.Count(w => w.GrantedActionIds?.Count > 0);
            int totalActionGrants = weapons.Sum(w => w.GrantedActionIds?.Count ?? 0);
            var uniqueActions = weapons.SelectMany(w => w.GrantedActionIds ?? new List<string>()).Distinct().ToList();

            _output.WriteLine($"=== Equipment Coverage Summary ===");
            _output.WriteLine($"Weapons: {weapons.Count}");
            _output.WriteLine($"  With weapon actions: {weaponsWithActions}");
            _output.WriteLine($"  Without weapon actions: {weapons.Count - weaponsWithActions}");
            _output.WriteLine($"  Total action grants: {totalActionGrants}");
            _output.WriteLine($"  Unique weapon actions: {uniqueActions.Count}");
            _output.WriteLine($"Armors: {armors.Count}");
            _output.WriteLine($"  Light: {armors.Count(a => a.Category == ArmorCategory.Light)}");
            _output.WriteLine($"  Medium: {armors.Count(a => a.Category == ArmorCategory.Medium)}");
            _output.WriteLine($"  Heavy: {armors.Count(a => a.Category == ArmorCategory.Heavy)}");
            _output.WriteLine($"  Shield: {armors.Count(a => a.Category == ArmorCategory.Shield)}");

            _output.WriteLine($"\nWeapon action distribution:");
            foreach (var actionId in uniqueActions.OrderBy(a => a))
            {
                var count = weapons.Count(w => w.GrantedActionIds?.Contains(actionId) == true);
                _output.WriteLine($"  {actionId}: used by {count} weapons");
            }

            Assert.True(weapons.Count >= 34);
            Assert.True(uniqueActions.Count >= 20, 
                $"Expected at least 20 unique weapon actions across weapons, got {uniqueActions.Count}");
        }

        // =================================================================
        //  Helpers
        // =================================================================

        private HashSet<string> LoadAllActionIds()
        {
            return _actionRegistry.GetAllActionIds()
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private List<ActionRecord> LoadWeaponActions()
        {
            // BG3 actions don't carry a "weapon_action" tag. Look up the known
            // weapon-granted action IDs from the equipment registry instead.
            var weaponActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var weapon in _registry.GetAllWeapons())
            {
                if (weapon.GrantedActionIds == null) continue;
                foreach (var id in weapon.GrantedActionIds)
                    weaponActionIds.Add(id);
            }

            var results = new List<ActionRecord>();
            foreach (var id in weaponActionIds)
            {
                var action = _actionRegistry.GetAction(id);
                if (action != null)
                {
                    results.Add(new ActionRecord
                    {
                        Id = action.Id,
                        Name = action.Name,
                        Description = action.Description,
                        ExtraDescription = action.ExtraDescription,
                        Tags = action.Tags?.ToList() ?? new List<string>()
                    });
                }
            }
            return results;
        }

        private class ActionRecord
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string ExtraDescription { get; set; }
            public List<string> Tags { get; set; }
        }
    }
}
