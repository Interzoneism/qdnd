using System;
using System.Collections.Generic;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for Toll the Dead cantrip's conditional damage mechanic.
    /// Should deal 1d8 normally, 1d12 if target has taken damage.
    /// </summary>
    public class TollTheDeadTests
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
            return (pipeline, rules, statuses);
        }

        private Combatant CreateCombatant(string id,int hp = 100, int initiative = 10)
        {
            var combatant = new Combatant(id, $"Test_{id}", Faction.Player, hp, initiative);
            // Set wis save to auto-fail for testing
            combatant.Stats = new CombatantStats { Wisdom = 1 };
            return combatant;
        }

        private AbilityDefinition CreateTollTheDeadAbility(int saveDc = 30)
        {
            return new AbilityDefinition
            {
                Id = "toll_the_dead",
                Name = "Toll the Dead",
                TargetType = TargetType.SingleUnit,
                SaveType = "wisdom",
            SaveDC = saveDc,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d8",
                        DamageType = "necrotic",
                        Condition = "on_save_fail"
                    }
                },
                Tags = new HashSet<string> { "spell", "cantrip", "necrotic" }
            };
        }

        [Fact]
        public void TollTheDead_AgainstFullHealthTarget_Deals1d8Damage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 100);
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100);

            var ability = CreateTollTheDeadAbility();
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("toll_the_dead", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            var damageDealt = 100 - target.Resources.CurrentHP;
            
            // d8 damage should be between 0-8 (0 when save succeeds)
            Assert.InRange(damageDealt, 0, 8);
            
            // With seed=100, verify we're using d8 not d12 (no damage >8)
            Assert.True(damageDealt <= 8, "Full health target should take 1d8 (max 8) damage, not 1d12");
        }

        [Fact]
        public void TollTheDead_AgainstInjuredTarget_Deals1d12Damage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 100);
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100);

            // Injure the target (currentHP < maxHP)
            target.Resources.TakeDamage(30);
            Assert.Equal(70, target.Resources.CurrentHP);

            var ability = CreateTollTheDeadAbility();
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("toll_the_dead", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            var hpBefore = 70;
            var hpAfter = target.Resources.CurrentHP;
            var damageDealt = hpBefore - hpAfter;
            
            // d12 damage should be between 1-12
            Assert.InRange(damageDealt, 1, 12);
        }

        [Fact]
        public void TollTheDead_VerifyDiceRange_FullHealthUses1d8()
        {
            // Run multiple times to verify dice range statistically
            var damages = new List<int>();
            
            for (int i = 0; i < 100; i++)
            {
                var (pipeline, rules, statuses) = CreatePipeline(seed: i);
                var source = CreateCombatant("caster", 100);
                var target = CreateCombatant("target", 100);

                var ability = CreateTollTheDeadAbility();
                pipeline.RegisterAbility(ability);

                pipeline.ExecuteAbility("toll_the_dead", source, new List<Combatant> { target });
                damages.Add(100 - target.Resources.CurrentHP);
            }

            // Verify all damages are in d8 range (1-8)
            Assert.All(damages, d => Assert.InRange(d, 1, 8));
            
            // Verify we never exceeded 8 (would indicate d12)
            Assert.DoesNotContain(damages, d => d > 8);
        }

        [Fact]
        public void TollTheDead_VerifyDiceRange_InjuredTargetUses1d12()
        {
            // Run multiple times to verify dice range statistically
            var damages = new List<int>();
            
            for (int i = 0; i < 200; i++)
            {
                var (pipeline, rules, statuses) = CreatePipeline(seed: i);
                var source = CreateCombatant("caster", 100);
                var target = CreateCombatant("target", 100);
                
                // Injure target
                target.Resources.TakeDamage(10);

                var ability = CreateTollTheDeadAbility();
                pipeline.RegisterAbility(ability);

                pipeline.ExecuteAbility("toll_the_dead", source, new List<Combatant> { target });
                damages.Add(90 - target.Resources.CurrentHP);
            }

            // Verify all damages are in d12 range (1-12)
            Assert.All(damages, d => Assert.InRange(d, 1, 12));
            
            // With 200 rolls, we should statistically see at least one value > 8
            // (probability of never rolling 9-12 in 200 d12 rolls is astronomically low)
            Assert.Contains(damages, d => d > 8);
        }

        [Fact]
        public void TollTheDead_SaveSucceeds_NoDamage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 42);
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100);
            // Set target wis save very high to auto-succeed
            target.Stats = new CombatantStats { Wisdom = 30 };

            var ability = CreateTollTheDeadAbility(saveDc: 5);
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("toll_the_dead", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.SaveResult);
            Assert.True(result.SaveResult.IsSuccess);
        }

        [Theory]
        [InlineData(1, 1, 1)]  // Level 1: 1d8 or 1d12
        [InlineData(5, 2, 2)]  // Level 5: 2d8 or 2d12
        [InlineData(11, 3, 3)] // Level 11: 3d8 or 3d12
        public void TollTheDead_ScaledVersions_UseCorrectDiceCount(int level, int expectedDiceCount, int seed)
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: seed);
            var source = CreateCombatant("caster", 100);
            var targetHealthy = CreateCombatant("target_healthy", 100);
            var targetInjured = CreateCombatant("target_injured", 100);
            targetInjured.Resources.TakeDamage(10);

            string abilityId = level == 1 ? "toll_the_dead" :
                               level == 5 ? "toll_the_dead_5" :
                               "toll_the_dead_11";

            var ability = new AbilityDefinition
            {
                Id = abilityId,
                Name = "Toll the Dead",
                TargetType = TargetType.SingleUnit,
                SaveType = "wisdom",
                SaveDC = 30,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = $"{expectedDiceCount}d8",
                        DamageType = "necrotic",
                        Condition = "on_save_fail"
                    }
                },
                Tags = new HashSet<string> { "spell", "cantrip", "necrotic", "scaled" }
            };
            pipeline.RegisterAbility(ability);

            // Act - Test against healthy target
            var resultHealthy = pipeline.ExecuteAbility(abilityId, source, new List<Combatant> { targetHealthy });
            var damageHealthy = 100 - targetHealthy.Resources.CurrentHP;

            // Assert - Healthy target uses d8
            Assert.InRange(damageHealthy, expectedDiceCount, expectedDiceCount * 8);

            // Act - Test against injured target
            var resultInjured = pipeline.ExecuteAbility(abilityId, source, new List<Combatant> { targetInjured });
            var damageInjured = (100 - 10) - targetInjured.Resources.CurrentHP;

            // Assert - Injured target can exceed d8 max (uses d12)
            Assert.InRange(damageInjured, expectedDiceCount, expectedDiceCount * 12);
        }
    }
}
