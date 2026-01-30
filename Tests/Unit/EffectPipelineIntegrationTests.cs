using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Integration tests for EffectPipeline using REAL implementations.
    /// These tests exercise the actual code, not mocks.
    /// </summary>
    public class EffectPipelineIntegrationTests
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

        private Combatant CreateCombatant(string id, int hp = 100, int initiative = 10)
        {
            return new Combatant(id, $"Test_{id}", Faction.Player, hp, initiative);
        }

        private AbilityDefinition CreateDamageAbility(int damage = 10, string damageType = "physical")
        {
            return new AbilityDefinition
            {
                Id = "test_damage",
                Name = "Test Damage",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = damage, DamageType = damageType }
                }
            };
        }

        private AbilityDefinition CreateHealAbility(int healAmount = 20)
        {
            return new AbilityDefinition
            {
                Id = "test_heal",
                Name = "Test Heal",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "heal", Value = healAmount }
                }
            };
        }

        private AbilityDefinition CreateApplyStatusAbility(string statusId, int duration = 3, int stacks = 1)
        {
            return new AbilityDefinition
            {
                Id = "test_apply_status",
                Name = "Test Apply Status",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "apply_status",
                        StatusId = statusId,
                        StatusDuration = duration,
                        StatusStacks = stacks
                    }
                }
            };
        }

        #region Damage Tests

        [Fact]
        public void ExecuteAbility_DealsDamage_ReducesHP()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            var ability = CreateDamageAbility(25, "slashing");
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_damage", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(75, target.Resources.CurrentHP);
            Assert.Single(result.EffectResults);
            Assert.Equal("damage", result.EffectResults[0].EffectType);
            Assert.True(result.EffectResults[0].Success);
        }

        [Fact]
        public void ExecuteAbility_OverkillDamage_DownsTarget()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 30);

            var ability = CreateDamageAbility(50, "fire");
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_damage", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(0, target.Resources.CurrentHP);
            Assert.True(target.Resources.IsDowned);
            Assert.False(target.IsActive);
        }

        #endregion

        #region Heal Tests

        [Fact]
        public void ExecuteAbility_Heal_RestoresHP()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("healer", 100);
            var target = CreateCombatant("wounded", 100);
            target.Resources.TakeDamage(50); // Reduce to 50 HP

            var ability = CreateHealAbility(30);
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_heal", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(80, target.Resources.CurrentHP);
            Assert.Single(result.EffectResults);
            Assert.Equal("heal", result.EffectResults[0].EffectType);
        }

        [Fact]
        public void ExecuteAbility_Heal_ClampsToMaxHP()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("healer", 100);
            var target = CreateCombatant("wounded", 100);
            target.Resources.TakeDamage(10); // Reduce to 90 HP

            var ability = CreateHealAbility(50);
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_heal", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(100, target.Resources.CurrentHP); // Clamped to max
        }

        #endregion

        #region Status Effect Tests

        [Fact]
        public void ExecuteAbility_ApplyStatus_AddsToTarget()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("victim", 100);

            // Register status definition FIRST
            var statusDef = new StatusDefinition
            {
                Id = "burning",
                Name = "Burning",
                DurationType = DurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 5,
                Stacking = StackingBehavior.Stack
            };
            statuses.RegisterStatus(statusDef);

            var ability = CreateApplyStatusAbility("burning", 3, 1);
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_apply_status", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.True(statuses.HasStatus(target.Id, "burning"));
            var activeStatuses = statuses.GetStatuses(target.Id);
            Assert.Single(activeStatuses);
            Assert.Equal("burning", activeStatuses[0].Definition.Id);
        }

        [Fact]
        public void ExecuteAbility_ApplyStatus_StacksExisting()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("victim", 100);

            var statusDef = new StatusDefinition
            {
                Id = "poison",
                Name = "Poison",
                DurationType = DurationType.Turns,
                DefaultDuration = 3,
                MaxStacks = 10,
                Stacking = StackingBehavior.Stack
            };
            statuses.RegisterStatus(statusDef);

            var ability = CreateApplyStatusAbility("poison", 3, 2);
            pipeline.RegisterAbility(ability);

            // Act - Apply twice
            pipeline.ExecuteAbility("test_apply_status", source, new List<Combatant> { target });
            pipeline.ExecuteAbility("test_apply_status", source, new List<Combatant> { target });

            // Assert
            var activeStatuses = statuses.GetStatuses(target.Id);
            Assert.Single(activeStatuses); // Still one status (stacked)
            Assert.Equal(4, activeStatuses[0].Stacks); // 2 + 2 stacks
        }

        #endregion

        #region Cooldown Tests

        [Fact]
        public void ExecuteAbility_WithCooldown_TracksCharges()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("victim", 100);

            var ability = new AbilityDefinition
            {
                Id = "limited_ability",
                Name = "Limited Ability",
                TargetType = TargetType.SingleUnit,
                Cooldown = new AbilityCooldown
                {
                    TurnCooldown = 2,
                    MaxCharges = 1
                },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "arcane" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act & Assert - First use should succeed
            var result1 = pipeline.ExecuteAbility("limited_ability", source, new List<Combatant> { target });
            Assert.True(result1.Success);

            // Second use should fail (on cooldown)
            var (canUse1, reason1) = pipeline.CanUseAbility("limited_ability", source);
            Assert.False(canUse1);
            Assert.Contains("cooldown", reason1.ToLower());

            // Process turns to tick cooldown
            pipeline.ProcessTurnStart(source.Id);
            var (canUse2, _) = pipeline.CanUseAbility("limited_ability", source);
            Assert.False(canUse2); // Still on cooldown (1 turn remaining)

            pipeline.ProcessTurnStart(source.Id);
            var (canUse3, _) = pipeline.CanUseAbility("limited_ability", source);
            Assert.True(canUse3); // Cooldown expired
        }

        #endregion

        #region Requirements Tests

        [Fact]
        public void ExecuteAbility_Requirements_ValidatesCorrectly()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var healthySource = CreateCombatant("healthy", 100);
            var weakSource = CreateCombatant("weak", 100);
            weakSource.Resources.TakeDamage(80); // 20 HP remaining
            var target = CreateCombatant("target", 100);

            var ability = new AbilityDefinition
            {
                Id = "power_attack",
                Name = "Power Attack",
                TargetType = TargetType.SingleUnit,
                Requirements = new List<AbilityRequirement>
                {
                    new AbilityRequirement { Type = "hp_above", Value = "50" }
                },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 30, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act & Assert - Healthy source can use
            var (canUseHealthy, _) = pipeline.CanUseAbility("power_attack", healthySource);
            Assert.True(canUseHealthy);

            // Weak source cannot use
            var (canUseWeak, reason) = pipeline.CanUseAbility("power_attack", weakSource);
            Assert.False(canUseWeak);
            Assert.Contains("hp_above", reason);
        }

        #endregion

        #region Multiple Effects Tests

        [Fact]
        public void ExecuteAbility_MultipleEffects_AllExecute()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            // Register status for apply_status effect
            var statusDef = new StatusDefinition
            {
                Id = "bleeding",
                Name = "Bleeding",
                DurationType = DurationType.Turns,
                DefaultDuration = 2
            };
            statuses.RegisterStatus(statusDef);

            var ability = new AbilityDefinition
            {
                Id = "bleeding_strike",
                Name = "Bleeding Strike",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 20, DamageType = "slashing" },
                    new EffectDefinition { Type = "apply_status", StatusId = "bleeding", StatusDuration = 2 }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("bleeding_strike", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(2, result.EffectResults.Count);
            Assert.Equal(80, target.Resources.CurrentHP); // 100 - 20 damage
            Assert.True(statuses.HasStatus(target.Id, "bleeding"));
        }

        #endregion

        #region Preview Tests

        [Fact]
        public void PreviewAbility_ReturnsExpectedRange()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            var ability = new AbilityDefinition
            {
                Id = "dice_damage",
                Name = "Dice Damage",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "2d6", DamageType = "fire" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var preview = pipeline.PreviewAbility("dice_damage", source, new List<Combatant> { target });

            // Assert
            Assert.True(preview.ContainsKey("damage"));
            var (min, max, avg) = preview["damage"];
            Assert.Equal(2f, min);   // 2d6 min = 2
            Assert.Equal(12f, max);  // 2d6 max = 12
            Assert.Equal(7f, avg);   // 2d6 avg = 7
        }

        [Fact]
        public void PreviewAbility_WithBonus_IncludesBonus()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            var ability = new AbilityDefinition
            {
                Id = "bonus_damage",
                Name = "Bonus Damage",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "1d8+3", DamageType = "cold" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var preview = pipeline.PreviewAbility("bonus_damage", source, new List<Combatant> { target });

            // Assert
            Assert.True(preview.ContainsKey("damage"));
            var (min, max, avg) = preview["damage"];
            Assert.Equal(4f, min);    // 1d8+3 min = 4
            Assert.Equal(11f, max);   // 1d8+3 max = 11
            Assert.Equal(7.5f, avg);  // 1d8+3 avg = 7.5
        }

        #endregion

        #region Event Emission Tests

        [Fact]
        public void Execute_EmitsEvents_ToRulesEngine()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            var ability = CreateDamageAbility(15, "lightning");
            pipeline.RegisterAbility(ability);

            var receivedEvents = new List<RuleEvent>();
            rules.Events.Subscribe(RuleEventType.DamageTaken, evt => receivedEvents.Add(evt));
            rules.Events.Subscribe(RuleEventType.AbilityDeclared, evt => receivedEvents.Add(evt));
            rules.Events.Subscribe(RuleEventType.AbilityResolved, evt => receivedEvents.Add(evt));

            // Act
            pipeline.ExecuteAbility("test_damage", source, new List<Combatant> { target });

            // Assert
            Assert.True(receivedEvents.Count >= 3);
            Assert.Contains(receivedEvents, e => e.Type == RuleEventType.AbilityDeclared);
            Assert.Contains(receivedEvents, e => e.Type == RuleEventType.DamageTaken);
            Assert.Contains(receivedEvents, e => e.Type == RuleEventType.AbilityResolved);

            // Check damage event details
            var damageEvent = receivedEvents.First(e => e.Type == RuleEventType.DamageTaken);
            Assert.Equal(source.Id, damageEvent.SourceId);
            Assert.Equal(target.Id, damageEvent.TargetId);
            Assert.True(damageEvent.Value > 0);
        }

        [Fact]
        public void Execute_EmitsHealingEvent_ForHealAbility()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("healer", 100);
            var target = CreateCombatant("wounded", 100);
            target.Resources.TakeDamage(40);

            var ability = CreateHealAbility(25);
            pipeline.RegisterAbility(ability);

            var receivedEvents = new List<RuleEvent>();
            rules.Events.Subscribe(RuleEventType.HealingReceived, evt => receivedEvents.Add(evt));

            // Act
            pipeline.ExecuteAbility("test_heal", source, new List<Combatant> { target });

            // Assert
            Assert.Single(receivedEvents);
            var healEvent = receivedEvents[0];
            Assert.Equal(RuleEventType.HealingReceived, healEvent.Type);
            Assert.Equal(source.Id, healEvent.SourceId);
            Assert.Equal(target.Id, healEvent.TargetId);
            Assert.Equal(25, healEvent.Value);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void ExecuteAbility_UnknownAbility_ReturnsFailure()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            // Act - Don't register any ability
            var result = pipeline.ExecuteAbility("nonexistent", source, new List<Combatant> { target });

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unknown", result.ErrorMessage);
        }

        [Fact]
        public void ExecuteAbility_DownedSource_ReturnsFailure()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            source.Resources.TakeDamage(100); // Down the source
            var target = CreateCombatant("defender", 100);

            var ability = CreateDamageAbility(10);
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_damage", source, new List<Combatant> { target });

            // Assert
            Assert.False(result.Success);
            Assert.Contains("incapacitated", result.ErrorMessage.ToLower());
        }

        [Fact]
        public void Reset_ClearsCooldowns()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100);

            var ability = new AbilityDefinition
            {
                Id = "once_per_encounter",
                Name = "Once Per Encounter",
                TargetType = TargetType.SingleUnit,
                Cooldown = new AbilityCooldown { TurnCooldown = 10, MaxCharges = 1 },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 50, DamageType = "radiant" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Use ability
            pipeline.ExecuteAbility("once_per_encounter", source, new List<Combatant> { target });
            var (beforeReset, _) = pipeline.CanUseAbility("once_per_encounter", source);
            Assert.False(beforeReset);

            // Act
            pipeline.Reset();

            // Assert
            var (afterReset, _) = pipeline.CanUseAbility("once_per_encounter", source);
            Assert.True(afterReset);
        }

        #endregion
    }
}
