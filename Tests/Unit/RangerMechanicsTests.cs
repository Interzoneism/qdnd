using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;
using QDND.Combat.Rules;
using QDND.Data;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for Ranger class mechanics.
    /// </summary>
    public class RangerMechanicsTests
    {
        private static string ResolveDataPath()
        {
            var candidates = new[]
            {
                "Data",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Data"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "Data")
            };

            foreach (var path in candidates)
            {
                if (Directory.Exists(Path.Combine(path, "Actions")) &&
                    Directory.Exists(Path.Combine(path, "Statuses")))
                {
                    return path;
                }
            }

            throw new DirectoryNotFoundException("Could not locate Data directory for RangerMechanicsTests");
        }

        private static DataRegistry CreateLoadedRegistry()
        {
            var registry = new DataRegistry();
            registry.LoadFromDirectory(ResolveDataPath());
            return registry;
        }

        private static StatusManager CreateStatusManager(RulesEngine rulesEngine, DataRegistry registry)
        {
            var statuses = new StatusManager(rulesEngine);
            foreach (var status in registry.GetAllStatuses())
            {
                statuses.RegisterStatus(status);
            }

            return statuses;
        }

        private Combatant CreateCombatant(string id, int hp = 100, int initiative = 10, string team = "player")
        {
            return new Combatant(id, id, Faction.Player, hp, initiative)
            {
                Team = team,
                Stats = new CombatantStats
                {
                    Strength = 14,
                    Dexterity = 16,
                    Constitution = 12,
                    Intelligence = 10,
                    Wisdom = 14,
                    Charisma = 8
                }
            };
        }

        [Fact]
        public void FavouredEnemy_Humanoids_GrantsDamageBonus()
        {
            // Arrange - Ranger with Favoured Enemy: Humanoids
            var registry = CreateLoadedRegistry();
            
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateStatusManager(rulesEngine, registry);
            var ranger = CreateCombatant("ranger");
            var humanoidEnemy = CreateCombatant("bandit", team: "enemy");
            humanoidEnemy.Tags.Add("humanoid");
            
            // Apply Favoured Enemy status
            statuses.ApplyStatus("favoured_enemy_humanoids", ranger.Id, ranger.Id, duration: 100);
            
            // Act - Get damage modifier from RulesEngine
            int baseDamage = 10;
            var modStack = rulesEngine.GetModifiers(ranger.Id);
            var damageContext = new ModifierContext
            {
                Tags = new HashSet<string>(humanoidEnemy.Tags.Select(t => $"target:{t.ToLowerInvariant()}"))
            };
            var (modifiedDamage, applied) = modStack.Apply(baseDamage, ModifierTarget.DamageDealt, damageContext, rulesEngine.Dice);
            int totalDamage = (int)modifiedDamage;
            
            // Assert - Should have +2 bonus vs humanoids
            Assert.Equal(12, totalDamage);
        }

        [Fact]
        public void FavouredEnemy_DoesNotApply_ToNonFavouredType()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateStatusManager(rulesEngine, registry);
            var ranger = CreateCombatant("ranger");
            var beastEnemy = CreateCombatant("wolf", team: "enemy");
            beastEnemy.Tags.Add("beast");
            
            // Apply Favoured Enemy: Humanoids
            statuses.ApplyStatus("favoured_enemy_humanoids", ranger.Id, ranger.Id, duration: 100);
            
            // Act - Get damage modifier from RulesEngine
            int baseDamage = 10;
            var modStack = rulesEngine.GetModifiers(ranger.Id);
            var damageContext = new ModifierContext
            {
                Tags = new HashSet<string>(beastEnemy.Tags.Select(t => $"target:{t.ToLowerInvariant()}"))
            };
            var (modifiedDamage, applied) = modStack.Apply(baseDamage, ModifierTarget.DamageDealt, damageContext, rulesEngine.Dice);
            int totalDamage = (int)modifiedDamage;
            
            // Assert - No bonus vs beasts when favoured enemy is humanoids
            Assert.Equal(10, totalDamage);
        }

        [Fact]
        public void NaturalExplorer_GrantsInitiativeAdvantage()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateStatusManager(rulesEngine, registry);
            var ranger = CreateCombatant("ranger");
            
            // Apply Natural Explorer status
            statuses.ApplyStatus("natural_explorer", ranger.Id, ranger.Id, duration: 100);
            
            // Act - Check for initiative advantage from RulesEngine
            var modStack = rulesEngine.GetModifiers(ranger.Id);
            var advantageResolution = modStack.ResolveAdvantage(ModifierTarget.Initiative, null);
            
            // Assert - Should have advantage on initiative
            Assert.Equal(Combat.Rules.AdvantageState.Advantage, advantageResolution.ResolvedState);
        }

        [Fact]
        public void EnsnaringStrike_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            // Act
            var action = registry.GetAction("ensnaring_strike");
            
            // Assert
            Assert.NotNull(action);
            Assert.Equal("Ensnaring Strike", action.Name);
            Assert.True(action.RequiresConcentration);
            Assert.Contains("ranger", action.Tags);
        }

        [Fact]
        public void HailOfThorns_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            // Act
            var action = registry.GetAction("hail_of_thorns");
            
            // Assert
            Assert.NotNull(action);
            Assert.Equal("Hail of Thorns", action.Name);
            Assert.True(action.RequiresConcentration);
            Assert.Contains("ranger", action.Tags);
        }

        [Fact]
        public void EnsnaringStrike_Status_CausesRestrained()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateStatusManager(rulesEngine, registry);
            var enemy = CreateCombatant("enemy");
            
            // Act - Apply ensnared vines status
            statuses.ApplyStatus("ensnared_vines", "ranger", enemy.Id, duration: 3);
            var activeStatuses = statuses.GetStatuses(enemy.Id);
            
            // Assert - Enemy has ensnared_vines status
            Assert.Contains(activeStatuses, s => s.Definition.Id == "ensnared_vines");
            var ensnaringStatus = registry.GetStatus("ensnared_vines");
            Assert.NotNull(ensnaringStatus);
            Assert.Contains("restrained", ensnaringStatus.Tags);
        }

        [Fact]
        public void ColossusSlayer_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            // Act
            var action = registry.GetAction("colossus_slayer");
            
            // Assert
            Assert.NotNull(action);
            Assert.Equal("Colossus Slayer", action.Name);
            Assert.Contains("hunter", action.Tags);
            Assert.Contains("ranger", action.Tags);
        }

        [Fact]
        public void HideInPlainSight_GrantsStealthBonus()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            var rulesEngine = new RulesEngine(42);
            var statuses = CreateStatusManager(rulesEngine, registry);
            var ranger = CreateCombatant("ranger");
            
            // Apply Hide in Plain Sight status
            statuses.ApplyStatus("hide_in_plain_sight_active", ranger.Id, ranger.Id, duration: 10);
            
            // Act - Get skill check modifiers from RulesEngine
            var modStack = rulesEngine.GetModifiers(ranger.Id);
            var (modifiedValue, applied) = modStack.Apply(0, ModifierTarget.SkillCheck, null, rulesEngine.Dice);
            int totalBonus = (int)modifiedValue;
            
            // Assert - Should have +10 to Stealth
            Assert.Equal(10, totalBonus);
        }

        [Fact]
        public void PrimevalAwareness_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            // Act
            var action = registry.GetAction("primeval_awareness");
            
            // Assert
            Assert.NotNull(action);
            Assert.Equal("Primeval Awareness", action.Name);
            Assert.Contains("ranger", action.Tags);
        }

        [Fact]
        public void HuntersMark_Exists_InAbilityRegistry()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
            // Act
            var action = registry.GetAction("hunters_mark");
            
            // Assert
            Assert.NotNull(action);
            Assert.Equal("Hunter's Mark", action.Name);
            Assert.True(action.RequiresConcentration);
        }

        [Fact]
        public void AllRangerAbilities_LoadSuccessfully()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
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
            
            foreach (var actionId in rangerAbilities)
            {
                var action = registry.GetAction(actionId);
                Assert.NotNull(action);
            }
        }

        [Fact]
        public void AllRangerStatuses_LoadSuccessfully()
        {
            // Arrange
            var registry = CreateLoadedRegistry();
            
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
