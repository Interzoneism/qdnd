using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Abilities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using System.IO;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for the SleepPoolEffect - D&D/BG3 HP pool mechanic.
    /// </summary>
    public class SleepPoolEffectTests
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
                if (Directory.Exists(Path.Combine(path, "Abilities")) &&
                    Directory.Exists(Path.Combine(path, "Statuses")))
                {
                    return path;
                }
            }

            throw new DirectoryNotFoundException("Could not locate Data directory for SleepPoolEffectTests");
        }

        private EffectContext CreateContext(Combatant source, List<Combatant> targets, int seed = 42)
        {
            var rules = new RulesEngine();
            var statuses = new StatusManager(rules);
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "asleep",
                Name = "Asleep",
                DurationType = DurationType.Turns,
                DefaultDuration = 2,
                IsBuff = false,
                MaxStacks = 1
            });
            
            return new EffectContext
            {
                Source = source,
                Targets = targets,
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(seed)
            };
        }

        private Combatant CreateCombatant(string id, string name, int currentHP, int maxHP = 100)
        {
            var combatant = new Combatant(id, name, Faction.Player, maxHP, 10);
            combatant.Resources.CurrentHP = currentHP;
            return combatant;
        }

        [Fact]
        public void SleepPool_SingleTarget_WithinPool_AppliesSleep()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var target = CreateCombatant("target1", "Goblin", 15); // 15 HP
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "5d8", // Pool: 5-40 HP
                StatusId = "asleep",
                StatusDuration = 2
            };

            var context = CreateContext(caster, new List<Combatant> { target }, seed: 1);
            var effect = new SleepPoolEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success);
            Assert.Equal("target1", results[0].TargetId);
            Assert.True(context.Statuses.HasStatus("target1", "asleep"));
        }

        [Fact]
        public void SleepPool_SingleTarget_ExceedsPool_NoSleep()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var target = CreateCombatant("target1", "Ogre", 60); // 60 HP - too high
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "5d8", // Pool: 5-40 HP, average ~22
                StatusId = "asleep",
                StatusDuration = 2
            };

            var context = CreateContext(caster, new List<Combatant> { target }, seed: 1);
            var effect = new SleepPoolEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.False(results[0].Success); // Should fail - HP too high
            Assert.False(context.Statuses.HasStatus("target1", "asleep"));
        }

        [Fact]
        public void SleepPool_MultipleTargets_SortsByLowestHP()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var target1 = CreateCombatant("target1", "Goblin", 30); // 30 HP
            var target2 = CreateCombatant("target2", "Kobold", 10); // 10 HP - should be affected first
            var target3 = CreateCombatant("target3", "Orc", 25); // 25 HP
            
            // Targets intentionally out of order
            var targets = new List<Combatant> { target1, target2, target3 };
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "40", // Fixed pool of 40 HP
                StatusId = "asleep",
                StatusDuration = 2
            };

            var context = CreateContext(caster, targets, seed: 1);
            var effect = new SleepPoolEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            // Pool of 40: Kobold (10) + Orc (25) = 35 HP consumed (fits)
            // Goblin (30) would exceed pool (35 + 30 = 65 > 40), so not affected
            Assert.Equal(3, results.Count);
            
            // Kobold should be affected (lowest HP)
            Assert.True(context.Statuses.HasStatus("target2", "asleep"));
            
            // Orc should be affected (next lowest, still fits in pool)
            Assert.True(context.Statuses.HasStatus("target3", "asleep"));
            
            // Goblin should NOT be affected (would exceed pool)
            Assert.False(context.Statuses.HasStatus("target1", "asleep"));
        }

        [Fact]
        public void SleepPool_ExhaustsPool_StopsAtRemainingTargets()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var target1 = CreateCombatant("target1", "Goblin", 5);
            var target2 = CreateCombatant("target2", "Kobold", 8);
            var target3 = CreateCombatant("target3", "Orc", 12);
            var target4 = CreateCombatant("target4", "Troll", 15);
            
            var targets = new List<Combatant> { target1, target2, target3, target4 };
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "20", // Pool of 20 HP
                StatusId = "asleep",
                StatusDuration = 2
            };

            var context = CreateContext(caster, targets, seed: 1);
            var effect = new SleepPoolEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            // Pool of 20: Goblin (5) + Kobold (8) + Orc (12) = 25 exceeds
            // Should affect: Goblin (5) + Kobold (8) = 13 HP consumed
            // Orc (12) would push it to 25, exceeding pool
            Assert.True(context.Statuses.HasStatus("target1", "asleep"));
            Assert.True(context.Statuses.HasStatus("target2", "asleep"));
            Assert.False(context.Statuses.HasStatus("target3", "asleep"));
            Assert.False(context.Statuses.HasStatus("target4", "asleep"));
        }

        [Fact]
        public void SleepPool_EmptyTargetList_ReturnsEmpty()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var targets = new List<Combatant>();
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "5d8",
                StatusId = "asleep",
                StatusDuration = 2
            };

            var context = CreateContext(caster, targets, seed: 1);
            var effect = new SleepPoolEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Empty(results);
        }

        [Fact]
        public void SleepPool_RollsHPPoolDice_VariableResults()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var target1 = CreateCombatant("target1", "Goblin", 25);
            var target2 = CreateCombatant("target2", "Goblin", 25);
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "5d8", // Pool: 5-40 HP
                StatusId = "asleep",
                StatusDuration = 2
            };

            // Use different seeds to get different rolls
            var context1 = CreateContext(caster, new List<Combatant> { target1 }, seed: 1);
            var context2 = CreateContext(caster, new List<Combatant> { target2 }, seed: 999);
            
            var effect = new SleepPoolEffect();

            // Act
            var results1 = effect.Execute(definition, context1);
            var results2 = effect.Execute(definition, context2);

            // Assert
            // Both should complete and include rolled pool metadata.
            Assert.Single(results1);
            Assert.Single(results2);

            Assert.True(results1[0].Data.ContainsKey("hpPool"));
            Assert.True(results2[0].Data.ContainsKey("hpPool"));

            int hpPool1 = (int)results1[0].Data["hpPool"];
            int hpPool2 = (int)results2[0].Data["hpPool"];

            // 5d8 range sanity check
            Assert.InRange(hpPool1, 5, 40);
            Assert.InRange(hpPool2, 5, 40);

            // Different seeds should produce different rolls for this test setup.
            Assert.NotEqual(hpPool1, hpPool2);
        }

        [Fact]
        public void SleepPool_IncludesPoolValueInResultData()
        {
            // Arrange
            var caster = CreateCombatant("caster1", "Wizard", 50);
            var target = CreateCombatant("target1", "Goblin", 10);
            
            var definition = new EffectDefinition
            {
                Type = "sleep_pool",
                DiceFormula = "5d8",
                StatusId = "asleep",
                StatusDuration = 2
            };

            var context = CreateContext(caster, new List<Combatant> { target }, seed: 1);
            var effect = new SleepPoolEffect();

            // Act
            var results = effect.Execute(definition, context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Data.ContainsKey("hpPool"));
            Assert.True(results[0].Data.ContainsKey("hpConsumed"));
            
            var hpPool = (int)results[0].Data["hpPool"];
            var hpConsumed = (int)results[0].Data["hpConsumed"];
            
            Assert.True(hpPool >= 5 && hpPool <= 40); // 5d8 range
            Assert.True(hpConsumed >= 0);
        }

        [Fact]
        public void SleepSpell_LoadedFromJSON_UsesSleepPoolEffect()
        {
            // Arrange
            var dataRegistry = new QDND.Data.DataRegistry();
            dataRegistry.LoadAbilitiesFromFile(Path.Combine(ResolveDataPath(), "Abilities", "bg3_mechanics_abilities.json"));
            
            // Act
            var sleepAbility = dataRegistry.GetAbility("sleep");
            
            // Assert
            Assert.NotNull(sleepAbility);
            Assert.Single(sleepAbility.Effects);
            Assert.Equal("sleep_pool", sleepAbility.Effects[0].Type);
            Assert.Equal("5d8", sleepAbility.Effects[0].DiceFormula);
            Assert.Equal("asleep", sleepAbility.Effects[0].StatusId);
            Assert.Equal(2, sleepAbility.Effects[0].StatusDuration);
        }
    }
}
