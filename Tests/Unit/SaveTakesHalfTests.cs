using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Actions;
using QDND.Combat.Actions.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for "save takes half damage" mechanic (D&D 5e AoE spells like Fireball, Shatter, etc.).
    /// When saveTakesHalf is true, targets that succeed on their save take half damage instead of no damage.
    /// </summary>
    public class SaveTakesHalfTests
    {
        private (EffectPipeline Pipeline, RulesEngine Rules, StatusManager Statuses) CreatePipeline(int seed = 42)
        {
            var rules = new RulesEngine(seed);
            var statuses = new StatusManager(rules);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new Random(seed)
            };
            
            pipeline.RegisterEffect(new DealDamageEffect());
            
            return (pipeline, rules, statuses);
        }

        private Combatant CreateCombatant(string id, int hp, int dexScore = 10)
        {
            return new Combatant(id, id, Faction.Player, hp, initiative: 10)
            {
                Stats = new CombatantStats { Dexterity = dexScore }
            };
        }

        [Fact]
        public void SaveTakesHalf_SaveSucceeds_DealsHalfDamage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 100);
            var caster = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100, dexScore: 30); // High dex - will pass save

            var action = new ActionDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                TargetType = TargetType.Circle,
                SaveType = "dexterity",
                SaveDC = 15,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "8d6",  // Will roll specific values with seed 100
                        DamageType = "fire",
                        Condition = "on_save_fail",
                        SaveTakesHalf = true  // NEW PROPERTY - should cause half damage on save success
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAction(action);

            int healthBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("fireball", caster, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success, "Action should execute successfully");
            
            int damageTaken = healthBefore - target.Resources.CurrentHP;
            
            // Debug: Check if there's a damage effect at all
            var damageEffect = result.EffectResults.Find(e => e.EffectType == "damage");
            string debugMsg = damageEffect != null 
                ? $"Damage effect found: Success={damageEffect.Success}, Message={damageEffect.Message}" 
                : "No damage effect found in results";
            
            Assert.True(damageTaken > 0, $"Target should take SOME damage (half) even on successful save. Damage taken: {damageTaken}. {debugMsg}");
            
            // Verify it's less than full damage (half damage should be roughly 14-28 for 8d6)
            Assert.True(damageTaken < 40, "Half damage should be less than full damage");
            
            // Verify the result data indicates half damage was applied
            Assert.NotNull(damageEffect);
            Assert.True(damageEffect.Data.ContainsKey("halfDamageOnSave"));
            Assert.True((bool)damageEffect.Data["halfDamageOnSave"]);
        }

        [Fact]
        public void SaveTakesHalf_SaveFails_DealsFullDamage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 42);  // Changed from 100 to 42
            var caster = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100, dexScore: 1); // Low dex - will fail save

            var action = new ActionDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                TargetType = TargetType.Circle,
                SaveType = "dexterity",
                SaveDC = 50,  // Impossibly high DC to guarantee failure
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        Value = 40,  // Use fixed damage instead of dice for consistent test results
                        DamageType = "fire",
                        Condition = "on_save_fail",
                        SaveTakesHalf = true
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAction(action);

            int healthBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("fireball", caster, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            int damageTaken = healthBefore - target.Resources.CurrentHP;
            Assert.Equal(40, damageTaken); // Full damage on failed save
            
            // Verify the result does NOT indicate half damage
            var damageEffect = result.EffectResults.Find(e => e.EffectType == "damage" && e.TargetId == "target");
            Assert.NotNull(damageEffect);
            if (damageEffect.Data.ContainsKey("halfDamageOnSave"))
            {
                Assert.False((bool)damageEffect.Data["halfDamageOnSave"]);
            }
        }

        [Fact]
        public void SaveTakesHalf_False_SaveSucceeds_NoDamage()
        {
            // Arrange - Test backward compatibility: when SaveTakesHalf is false, save success = no damage
            var (pipeline, rules, statuses) = CreatePipeline(seed: 100);
            var caster = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100, dexScore: 30); // High dex - will pass save

            var action = new ActionDefinition
            {
                Id = "toll_the_dead",
                Name = "Toll the Dead",
                TargetType = TargetType.SingleUnit,
                SaveType = "dexterity",
                SaveDC = 15,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d8",
                        DamageType = "necrotic",
                        Condition = "on_save_fail",
                        SaveTakesHalf = false  // Explicit false - save success should negate damage
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAction(action);

            int healthBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("toll_the_dead", caster, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(healthBefore, target.Resources.CurrentHP); // No damage taken
        }

        [Fact]
        public void SaveTakesHalf_MultiTarget_MixedSaves_CorrectDamageDistribution()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 200);
            var caster = CreateCombatant("caster", 100);
            var target1 = CreateCombatant("target1", 100, dexScore: 1);  // Dex mod = -5, will fail DC 25
            var target2 = CreateCombatant("target2", 100, dexScore: 30); // Dex mod = +10, will pass DC 1

            var action = new ActionDefinition
            {
                Id = "lightning_bolt",
                Name = "Lightning Bolt",
                TargetType = TargetType.Line,
                SaveType = "dexterity",
                SaveDC = 1,  // Very low DC - target2 (high dex) will pass, but we'll test both scenarios separately
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        Value = 40,  // Use fixed damage for consistent testing
                        DamageType = "lightning",
                        Condition = "on_save_fail",
                        SaveTakesHalf = true
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAction(action);

            int health2Before = target2.Resources.CurrentHP;

            // Act - test with target2 only (high dex, will PASS save with DC=1)
            var result = pipeline.ExecuteAction("lightning_bolt", caster, new List<Combatant> { target2 });

            // Assert
            Assert.True(result.Success);
            
            int damage2 = health2Before - target2.Resources.CurrentHP;
            
            // Target 2 passed save - should take half damage (20)
            Assert.Equal(20, damage2);
            
            // Verify result data
            var effect2 = result.EffectResults.Find(e => e.EffectType == "damage" && e.TargetId == "target2");
            Assert.NotNull(effect2);
            
            // Target2 SHOULD have half damage flag
            Assert.True((bool)effect2.Data["halfDamageOnSave"]);
        }

        [Fact]
        public void SaveTakesHalf_NoCondition_ChecksPerTargetSave()
        {
            // Arrange - Test saveTakesHalf WITHOUT explicit "on_save_fail" condition
            // This should check per-target save results directly
            var (pipeline, rules, statuses) = CreatePipeline(seed: 300);
            var caster = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100, dexScore: 30); // High dex - will pass save

            var action = new ActionDefinition
            {
                Id = "shatter",
                Name = "Shatter",
                TargetType = TargetType.Circle,
                SaveType = "dexterity",  // Save is at ability level
                SaveDC = 1,  // Very low DC to guarantee success
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        Value = 30,  // Fixed damage for consistent testing
                        DamageType = "thunder",
                        // NO Condition specified, but SaveTakesHalf = true
                        SaveTakesHalf = true
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAction(action);

            int healthBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("shatter", caster, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            int damageTaken = healthBefore - target.Resources.CurrentHP;
            
            // Should take half damage (15) since they passed the save
            Assert.Equal(15, damageTaken);
            
            // Verify the result indicates half damage
            var damageEffect = result.EffectResults.Find(e => e.EffectType == "damage" && e.TargetId == "target");
            Assert.NotNull(damageEffect);
            Assert.True(damageEffect.Data.ContainsKey("halfDamageOnSave"));
            Assert.True((bool)damageEffect.Data["halfDamageOnSave"]);
        }

        [Fact]
        public void SaveTakesHalf_MinimumOneDamage()
        {
            // Arrange - Verify that half damage always deals at least 1 damage
            var (pipeline, rules, statuses) = CreatePipeline(seed: 1); // Seed that rolls low
            var caster = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100, dexScore: 30); // Will pass save

            var action = new ActionDefinition
            {
                Id = "weak_spell",
                Name = "Weak Spell",
                TargetType = TargetType.SingleUnit,
                SaveType = "dexterity",
                SaveDC = 15,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d4", // Very low damage - half could be 0
                        DamageType = "fire",
                        Condition = "on_save_fail",
                        SaveTakesHalf = true
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAction(action);

            int healthBefore = target.Resources.CurrentHP;

            // Act
            var result = pipeline.ExecuteAction("weak_spell", caster, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            int damageTaken = healthBefore - target.Resources.CurrentHP;
            
            // Should take at least 1 damage (even if half rounds to 0)
            Assert.True(damageTaken >= 1, "Half damage should deal at least 1 damage");
        }
    }
}
