using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;
using QDND.Data;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for Ranger class mechanics.
    /// </summary>
    public class RangerMechanicsTests
    {
        private Combatant CreateCombatant(string id, int hp = 100, int initiative = 10, string team = "player")
        {
            return new Combatant(id, id, Faction.Player, hp, initiative)
            {
                Team = team,
                Stats = new Dictionary<string, int>
                {
                    ["Strength"] = 14,
                    ["Dexterity"] = 16,
                    ["Constitution"] = 12,
                    ["Intelligence"] = 10,
                    ["Wisdom"] = 14,
                    ["Charisma"] = 8
                }
            };
        }

        [Fact]
        public void FavouredEnemy_Humanoids_GrantsDamageBonus()
        {
            // Arrange - Ranger with Favoured Enemy: Humanoids
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            var statuses = new StatusService(new RulesEngine(42));
            var ranger = CreateCombatant("ranger");
            var humanoidEnemy = CreateCombatant("bandit", team: "enemy");
            humanoidEnemy.Tags.Add("humanoid");
            
            // Apply Favoured Enemy status
            statuses.ApplyStatus("favoured_enemy_humanoids", ranger.Id, ranger.Id, duration: 100);
            
            // Act - Get damage modifier
            int baseDamage = 10;
            var modifiers = statuses.GetDamageModifiers(ranger.Id, humanoidEnemy.Id);
            int totalDamage = baseDamage;
            foreach (var mod in modifiers)
            {
                if (mod.ModifierType == StatusModifierType.Flat)
                    totalDamage += mod.Value;
            }
            
            // Assert - Should have +2 bonus vs humanoids
            Assert.Equal(12, totalDamage);
        }

        [Fact]
        public void FavouredEnemy_DoesNotApply_ToNonFavouredType()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            var statuses = new StatusService(new RulesEngine(42));
            var ranger = CreateCombatant("ranger");
            var beastEnemy = CreateCombatant("wolf", team: "enemy");
            beastEnemy.Tags.Add("beast");
            
            // Apply Favoured Enemy: Humanoids
            statuses.ApplyStatus("favoured_enemy_humanoids", ranger.Id, ranger.Id, duration: 100);
            
            // Act
            int baseDamage = 10;
            var modifiers = statuses.GetDamageModifiers(ranger.Id, beastEnemy.Id);
            int totalDamage = baseDamage;
            foreach (var mod in modifiers)
            {
                if (mod.ModifierType == StatusModifierType.Flat)
                    totalDamage += mod.Value;
            }
            
            // Assert - No bonus vs beasts when favoured enemy is humanoids
            Assert.Equal(10, totalDamage);
        }

        [Fact]
        public void NaturalExplorer_GrantsInitiativeAdvantage()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            var statuses = new StatusService(new RulesEngine(42));
            var ranger = CreateCombatant("ranger");
            
            // Apply Natural Explorer status
            statuses.ApplyStatus("natural_explorer", ranger.Id, ranger.Id, duration: 100);
            
            // Act - Check for initiative advantage
            var initiativeMods = statuses.GetInitiativeModifiers(ranger.Id);
            
            // Assert - Should have advantage on initiative
            Assert.Contains(initiativeMods, m => m.ModifierType == StatusModifierType.Advantage);
        }

        [Fact]
        public void EnsnaringStrike_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            // Act
            var ability = registry.GetAbility("ensnaring_strike");
            
            // Assert
            Assert.NotNull(ability);
            Assert.Equal("Ensnaring Strike", ability.Name);
            Assert.True(ability.RequiresConcentration);
            Assert.Contains("ranger", ability.Tags);
        }

        [Fact]
        public void HailOfThorns_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            // Act
            var ability = registry.GetAbility("hail_of_thorns");
            
            // Assert
            Assert.NotNull(ability);
            Assert.Equal("Hail of Thorns", ability.Name);
            Assert.True(ability.RequiresConcentration);
            Assert.Contains("ranger", ability.Tags);
        }

        [Fact]
        public void EnsnaringStrike_Status_CausesRestrained()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            var statuses = new StatusService(new RulesEngine(42));
            var enemy = CreateCombatant("enemy");
            
            // Act - Apply ensnared vines status
            statuses.ApplyStatus("ensnared_vines", "ranger", enemy.Id, duration: 3);
            var activeStatuses = statuses.GetActiveStatusesByTarget(enemy.Id);
            
            // Assert - Enemy is restrained
            Assert.Contains(activeStatuses, s => s.StatusId == "ensnared_vines");
            var ensnaringStatus = registry.GetStatus("ensnared_vines");
            Assert.NotNull(ensnaringStatus);
            Assert.Contains("restrained", ensnaringStatus.Tags);
        }

        [Fact]
        public void ColossusSlayer_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            // Act
            var ability = registry.GetAbility("colossus_slayer");
            
            // Assert
            Assert.NotNull(ability);
            Assert.Equal("Colossus Slayer", ability.Name);
            Assert.Contains("hunter", ability.Tags);
            Assert.Contains("ranger", ability.Tags);
        }

        [Fact]
        public void HideInPlainSight_GrantsStealthBonus()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            var statuses = new StatusService(new RulesEngine(42));
            var ranger = CreateCombatant("ranger");
            
            // Apply Hide in Plain Sight status
            statuses.ApplyStatus("hide_in_plain_sight_active", ranger.Id, ranger.Id, duration: 10);
            
            // Act - Get skill check modifiers
            var skillMods = statuses.GetSkillCheckModifiers(ranger.Id, "Stealth");
            
            // Assert - Should have +10 to Stealth
            int totalBonus = 0;
            foreach (var mod in skillMods)
            {
                if (mod.ModifierType == StatusModifierType.Flat)
                    totalBonus += mod.Value;
            }
            Assert.Equal(10, totalBonus);
        }

        [Fact]
        public void PrimevalAwareness_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            // Act
            var ability = registry.GetAbility("primeval_awareness");
            
            // Assert
            Assert.NotNull(ability);
            Assert.Equal("Primeval Awareness", ability.Name);
            Assert.Contains("ranger", ability.Tags);
        }

        [Fact]
        public void HuntersMark_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = new DataRegistry();
            registry.LoadFromDirectory("Data");
            
            // Act
            var ability = registry.GetAbility("hunters_mark");
            
            // Assert
            Assert.NotNull(ability);
            Assert.Equal("Hunter's Mark", ability.Name);
            Assert.True(ability.RequiresConcentration);
        }

        [Fact]
        public void AllRangerAbilities_LoadSuccessfully()
        {
            // Arrange
            var registry = new DataRegistry();
            
            // Act
            registry.LoadFromDirectory("Data");
            
            // Assert - All Ranger abilities should be present
            var rangerAbilities = new[]
            {
                "hunters_mark",
                "ensnaring_strike",
                "hail_of_thorns",
                "colossus_slayer",
                "primeval_awareness",
                "hide_in_plain_sight"
            };
            
            foreach (var abilityId in rangerAbilities)
            {
                var ability = registry.GetAbility(abilityId);
                Assert.NotNull(ability);
            }
        }

        [Fact]
        public void AllRangerStatuses_LoadSuccessfully()
        {
            // Arrange
            var registry = new DataRegistry();
            
            // Act
            registry.LoadFromDirectory("Data");
            
            // Assert - All Ranger statuses should be present
            var rangerStatuses = new[]
            {
                "favoured_enemy_humanoids",
                "favoured_enemy_beasts",
                "favoured_enemy_undead",
                "favoured_enemy_aberrations",
                "natural_explorer",
                "ensnaring_strike_active",
                "ensnared_vines",
                "hail_of_thorns_active",
                "hide_in_plain_sight_active",
                "primeval_awareness_active",
                "colossus_slayer_active"
            };
            
            foreach (var statusId in rangerStatuses)
            {
                var status = registry.GetStatus(statusId);
                Assert.NotNull(status);
            }
        }
    }
}
