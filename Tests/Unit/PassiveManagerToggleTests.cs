using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Passives;
using QDND.Combat.Entities;
using QDND.Data.Passives;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for PassiveManager toggle functionality.
    /// </summary>
    public class PassiveManagerToggleTests
    {
        private PassiveRegistry CreateTestRegistry()
        {
            var registry = new PassiveRegistry();

            // Create test toggleable passives
            registry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "GreatWeaponMaster",
                DisplayName = "Great Weapon Master",
                Description = "-5 to attack, +10 to damage",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(GWM_ACTIVE,100,-1)",
                ToggleOffFunctors = "RemoveStatus(GWM_ACTIVE)",
                ToggleGroup = null
            });

            registry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "Sharpshooter",
                DisplayName = "Sharpshooter",
                Description = "-5 to ranged attack, +10 to damage",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(SHARPSHOOTER_ACTIVE,100,-1)",
                ToggleOffFunctors = "RemoveStatus(SHARPSHOOTER_ACTIVE)",
                ToggleGroup = null
            });

            // Toggles in a group (mutually exclusive)
            registry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "NonLethalAttacks",
                DisplayName = "Non-Lethal Attacks",
                Description = "Your attacks deal non-lethal damage",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(NON_LETHAL,100,-1)",
                ToggleOffFunctors = "RemoveStatus(NON_LETHAL)",
                ToggleGroup = "AttackMode"
            });

            registry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "RecklessAttack",
                DisplayName = "Reckless Attack",
                Description = "Advantage on attacks, enemies have advantage on you",
                Properties = "IsToggled",
                ToggleOnFunctors = "ApplyStatus(RECKLESS,100,-1)",
                ToggleOffFunctors = "RemoveStatus(RECKLESS)",
                ToggleGroup = "AttackMode"
            });

            // Non-toggleable passive for control
            registry.RegisterPassive(new BG3PassiveData
            {
                PassiveId = "Darkvision",
                DisplayName = "Darkvision",
                Description = "See in darkness",
                Properties = "Highlighted",
                Boosts = "DarkvisionRangeMin(12)"
            });

            return registry;
        }

        private Combatant CreateTestCombatant()
        {
            var combatant = new Combatant("test_id", "Test Combatant", Faction.Player, 50, 10);
            return combatant;
        }

        [Fact]
        public void GrantPassive_ToggleablePassive_InitializesToggleState()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;

            // Act
            bool granted = manager.GrantPassive(registry, "GreatWeaponMaster");

            // Assert
            Assert.True(granted);
            Assert.False(manager.IsToggled("GreatWeaponMaster")); // Default: off
        }

        [Fact]
        public void SetToggleState_EnableToggle_SetsStateToTrue()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "GreatWeaponMaster");

            // Act
            manager.SetToggleState(registry, "GreatWeaponMaster", true);

            // Assert
            Assert.True(manager.IsToggled("GreatWeaponMaster"));
        }

        [Fact]
        public void SetToggleState_DisableToggle_SetsStateToFalse()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "GreatWeaponMaster");
            manager.SetToggleState(registry, "GreatWeaponMaster", true);

            // Act
            manager.SetToggleState(registry, "GreatWeaponMaster", false);

            // Assert
            Assert.False(manager.IsToggled("GreatWeaponMaster"));
        }

        [Fact]
        public void IsToggled_NonToggleablePassive_ReturnsFalse()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "Darkvision");

            // Act
            bool toggled = manager.IsToggled("Darkvision");

            // Assert
            Assert.False(toggled);
        }

        [Fact]
        public void GetToggleablePassives_ReturnsOnlyToggleablePassives()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "GreatWeaponMaster");
            manager.GrantPassive(registry, "Darkvision");
            manager.GrantPassive(registry, "Sharpshooter");

            // Act
            var toggleables = manager.GetToggleablePassives();

            // Assert
            Assert.Equal(2, toggleables.Count);
            Assert.Contains("GreatWeaponMaster", toggleables);
            Assert.Contains("Sharpshooter", toggleables);
            Assert.DoesNotContain("Darkvision", toggleables);
        }

        [Fact]
        public void SetToggleState_EnablePassiveInToggleGroup_DisablesOthersInGroup()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "NonLethalAttacks");
            manager.GrantPassive(registry, "RecklessAttack");
            manager.SetToggleState(registry, "NonLethalAttacks", true);

            // Act
            manager.SetToggleState(registry, "RecklessAttack", true);

            // Assert
            Assert.True(manager.IsToggled("RecklessAttack"));
            Assert.False(manager.IsToggled("NonLethalAttacks")); // Should be disabled
        }

        [Fact]
        public void OnToggleChanged_Event_FiresWhenToggleChanges()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "GreatWeaponMaster");

            string firedPassiveId = null;
            bool firedState = false;
            int eventCount = 0;

            manager.OnToggleChanged += (passiveId, enabled) =>
            {
                firedPassiveId = passiveId;
                firedState = enabled;
                eventCount++;
            };

            // Act
            manager.SetToggleState(registry, "GreatWeaponMaster", true);

            // Assert
            Assert.Equal(1, eventCount);
            Assert.Equal("GreatWeaponMaster", firedPassiveId);
            Assert.True(firedState);
        }

        [Fact]
        public void RevokePassive_RemovesToggleState()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;
            manager.GrantPassive(registry, "GreatWeaponMaster");
            manager.SetToggleState(registry, "GreatWeaponMaster", true);

            // Act
            manager.RevokePassive("GreatWeaponMaster");

            // Assert
            Assert.False(manager.IsToggled("GreatWeaponMaster"));
            Assert.Empty(manager.GetToggleablePassives());
        }

        [Fact]
        public void SetToggleState_NonExistentPassive_DoesNotThrow()
        {
            // Arrange
            var registry = CreateTestRegistry();
            var manager = new PassiveManager();
            var combatant = CreateTestCombatant();
            manager.Owner = combatant;

            // Act & Assert (should not throw)
            manager.SetToggleState(registry, "NonExistent", true);
            Assert.False(manager.IsToggled("NonExistent"));
        }
    }
}
