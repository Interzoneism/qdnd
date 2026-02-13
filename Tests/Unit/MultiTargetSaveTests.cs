using System.Collections.Generic;
using Xunit;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Entities;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for per-target saving throw handling in multi-target abilities.
    /// </summary>
    public class MultiTargetSaveTests
    {
        private (EffectPipeline Pipeline, RulesEngine Rules, StatusManager Statuses) CreatePipeline(int seed = 42)
        {
            var rules = new RulesEngine(seed);
            var statuses = new StatusManager(rules);
            var pipeline = new EffectPipeline
            {
                Rules = rules,
                Statuses = statuses,
                Rng = new System.Random(seed)
            };
            
            // Register effect handlers
            pipeline.RegisterEffect(new DealDamageEffect());
            pipeline.RegisterEffect(new ApplyStatusEffect());
            
            return (pipeline, rules, statuses);
        }

        private Combatant CreateCombatant(string id, int hp, int wisdomScore = 10)
        {
            return new Combatant(id, id, Faction.Player, hp, initiative: 10)
            {
                Stats = new CombatantStats { Wisdom = wisdomScore }
            };
        }

        [Fact]
        public void MultiTarget_DifferentSaves_AppliesDamageCorrectly()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 42);
            var source = CreateCombatant("caster", 100);
            
            // Target 1: Low wisdom (will fail save)
            var target1 = CreateCombatant("target1", 100, wisdomScore: 1);
            
            // Target 2: Extreme wisdom (will always pass save)
            var target2 = CreateCombatant("target2", 100, wisdomScore: 200);

            var ability = new AbilityDefinition
            {
                Id = "test_aoe_save",
                Name = "Test AoE Save",
                TargetType = TargetType.MultiUnit,
                SaveType = "wisdom",
                SaveDC = 50,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "2d6",
                        DamageType = "fire",
                        Condition = "on_save_fail"
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_aoe_save", source, new List<Combatant> { target1, target2 });

            // Assert
            Assert.True(result.Success);
            
            // Target 1 (low wisdom) should have taken damage
            Assert.True(target1.Resources.CurrentHP < 100, 
                $"Target1 should have taken damage. HP: {target1.Resources.CurrentHP}");
            
            // Target 2 (high wisdom) should NOT have taken damage
            Assert.Equal(100, target2.Resources.CurrentHP);
        }

        [Fact]
        public void MultiTarget_BothFailSave_BothTakeDamage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 100);
            var source = CreateCombatant("caster", 100);
            
            // Both targets have low wisdom
            var target1 = CreateCombatant("target1", 100, wisdomScore: 1);
            var target2 = CreateCombatant("target2", 100, wisdomScore: 1);

            var ability = new AbilityDefinition
            {
                Id = "test_aoe_save2",
                Name = "Test AoE Save 2",
                TargetType = TargetType.MultiUnit,
                SaveType = "wisdom",
                SaveDC = 50,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        Value = 10,
                        DamageType = "fire",
                        Condition = "on_save_fail"
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_aoe_save2", source, new List<Combatant> { target1, target2 });

            // Assert            Assert.True(result.Success);
            
            // Both should have taken damage
            Assert.True(target1.Resources.CurrentHP < 100);
            Assert.True(target2.Resources.CurrentHP < 100);
        }

        [Fact]
        public void MultiTarget_StatusOnSaveFail_AppliesOnlyToFailedSaves()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 50);
            var source = CreateCombatant("caster", 100);
            
            // Mixed saves
            var target1 = CreateCombatant("target1", 100, wisdomScore: 1);  // Will fail
            var target2 = CreateCombatant("target2", 100, wisdomScore: 200); // Will always pass

            // Register a test status
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "test_debuff",
                Name = "Test Debuff"
            });

            var ability = new AbilityDefinition
            {
                Id = "test_status_save",
                Name = "Test Status Save",
                TargetType = TargetType.MultiUnit,
                SaveType = "wisdom",
                SaveDC = 50,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "apply_status",
                        StatusId = "test_debuff",
                        Condition = "on_save_fail"
                    }
                },
                Tags = new HashSet<string> { "spell" }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_status_save", source, new List<Combatant> { target1, target2 });

            // Assert
            Assert.True(result.Success);
            
            // Target 1 (failed save) should have the status
            Assert.True(statuses.HasStatus("target1", "test_debuff"), 
                "Target1 should have the debuff after failing save");
            
            // Target 2 (passed save) should NOT have the status
            Assert.False(statuses.HasStatus("target2", "test_debuff"),
                "Target2 should not have the debuff after passing save");
        }
    }
}
