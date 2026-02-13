using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;
using QDND.Combat.Abilities;
using QDND.Combat.Abilities.Effects;
using QDND.Combat.Statuses;
using QDND.Combat.Environment;

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

        private AbilityDefinition CreateReviveAbility(int reviveHp = 1)
        {
            return new AbilityDefinition
            {
                Id = "test_revive",
                Name = "Test Revive",
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "revive", Value = reviveHp }
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

        [Fact]
        public void ExecuteAbility_DamageBreaksAsleepAndHypnotised()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "asleep",
                Name = "Sleep",
                DurationType = DurationType.Turns,
                DefaultDuration = 2
            });
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "hypnotised",
                Name = "Hypnotised",
                DurationType = DurationType.Turns,
                DefaultDuration = 2
            });

            statuses.ApplyStatus("asleep", source.Id, target.Id);
            statuses.ApplyStatus("hypnotised", source.Id, target.Id);

            var ability = CreateDamageAbility(8, "physical");
            ability.Id = "wake_up_strike";
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("wake_up_strike", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.False(statuses.HasStatus(target.Id, "asleep"));
            Assert.False(statuses.HasStatus(target.Id, "hypnotised"));
        }

        [Fact]
        public void ExecuteAbility_MagicMissileBlockedByShieldSpellStatus()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("shielded", 100);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "shield_spell",
                Name = "Shield",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.ApplyStatus("shield_spell", target.Id, target.Id);

            var ability = CreateDamageAbility(12, "force");
            ability.Id = "magic_missile";
            ability.Name = "Magic Missile";
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("magic_missile", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(100, target.Resources.CurrentHP);
            Assert.Single(result.EffectResults);
            Assert.True(result.EffectResults[0].Data.TryGetValue("actualDamageDealt", out var dealtObj));
            Assert.Equal(0, Convert.ToInt32(dealtObj));
        }

        #endregion

        #region Concentration Tests

        [Fact]
        public void ExecuteAbility_ConcentrationSpellWithNoTargets_StartsConcentrationOnCaster()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var concentration = new ConcentrationSystem(statuses, rules);
            pipeline.Concentration = concentration;

            var source = CreateCombatant("caster", 100);
            var ability = new AbilityDefinition
            {
                Id = "zone_spell",
                Name = "Zone Spell",
                TargetType = TargetType.Point,
                RequiresConcentration = true,
                ConcentrationStatusId = "zone_status",
                Effects = new List<EffectDefinition>()
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("zone_spell", source, new List<Combatant>());

            // Assert
            Assert.True(result.Success);
            Assert.True(concentration.IsConcentrating(source.Id));
            var info = concentration.GetConcentratedEffect(source.Id);
            Assert.NotNull(info);
            Assert.Equal(source.Id, info.TargetId);
        }

        [Fact]
        public void BreakConcentration_RemovesMatchingStatusFromAllTargets()
        {
            // Arrange
            var (_, rules, statuses) = CreatePipeline();
            var concentration = new ConcentrationSystem(statuses, rules);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "blessed_bg3",
                Name = "Blessed",
                DurationType = DurationType.Turns,
                DefaultDuration = 10
            });

            statuses.ApplyStatus("blessed_bg3", "caster", "ally_one");
            statuses.ApplyStatus("blessed_bg3", "caster", "ally_two");
            concentration.StartConcentration("caster", "bless", "blessed_bg3", "ally_one");

            // Act
            var broke = concentration.BreakConcentration("caster", "test_break");

            // Assert
            Assert.True(broke);
            Assert.False(statuses.HasStatus("ally_one", "blessed_bg3"));
            Assert.False(statuses.HasStatus("ally_two", "blessed_bg3"));
        }

        [Fact]
        public void CheckConcentration_UsesResolvedCombatantConstitutionSaveBonus()
        {
            // Arrange
            var (_, rules, statuses) = CreatePipeline();
            var concentration = new ConcentrationSystem(statuses, rules);
            var caster = CreateCombatant("caster", 100);
            caster.Stats = new CombatantStats
            {
                Constitution = 16
            };

            concentration.ResolveCombatant = id => id == caster.Id ? caster : null;

            // Act
            var check = concentration.CheckConcentration(caster.Id, damageTaken: 12);

            // Assert
            Assert.NotNull(check.RollResult);
            Assert.Equal(3, check.RollResult.BaseValue);
            Assert.Equal(caster.Id, check.RollResult.Input.Target?.Id);
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

        [Fact]
        public void ExecuteAbility_Revive_BringsDeadTargetBackToLife()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            var source = CreateCombatant("cleric", 100);
            var target = CreateCombatant("downed", 100);
            target.LifeState = CombatantLifeState.Dead;
            target.Resources.CurrentHP = 0;

            var ability = CreateReviveAbility(5);
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("test_revive", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal(CombatantLifeState.Alive, target.LifeState);
            Assert.Equal(5, target.Resources.CurrentHP);
            Assert.Contains(result.EffectResults, r => r.EffectType == "revive" && r.Success);
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
            source.ActionBudget.ResetFull();
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
            source.ActionBudget.ResetForTurn();
            var (canUse2, _) = pipeline.CanUseAbility("limited_ability", source);
            Assert.False(canUse2); // Still on cooldown (1 turn remaining)

            pipeline.ProcessTurnStart(source.Id);
            source.ActionBudget.ResetForTurn();
            var (canUse3, _) = pipeline.CanUseAbility("limited_ability", source);
            Assert.True(canUse3); // Cooldown expired
        }

        #endregion

        #region Requirements Tests

        [Fact]
        public void CanUseAbility_WithResourceCost_RequiresMatchingResource()
        {
            // Arrange
            var (pipeline, _, _) = CreatePipeline();
            var source = CreateCombatant("monk", 100);

            var ability = new AbilityDefinition
            {
                Id = "ki_strike",
                Name = "Ki Strike",
                TargetType = TargetType.None,
                Cost = new AbilityCost
                {
                    ResourceCosts = new Dictionary<string, int>
                    {
                        { "ki_points", 1 }
                    }
                },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 1, DamageType = "physical" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var (canUse, reason) = pipeline.CanUseAbility("ki_strike", source);

            // Assert
            Assert.False(canUse);
            Assert.Contains("resource", reason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ExecuteAbility_WithResourceCost_ConsumesResourcePool()
        {
            // Arrange
            var (pipeline, _, _) = CreatePipeline();
            var source = CreateCombatant("monk", 100);
            source.ResourcePool.SetMax("ki_points", 2);

            var ability = new AbilityDefinition
            {
                Id = "flurry_like",
                Name = "Flurry-Like",
                TargetType = TargetType.Self,
                Cost = new AbilityCost
                {
                    ResourceCosts = new Dictionary<string, int>
                    {
                        { "ki_points", 1 }
                    }
                },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "apply_status",
                        StatusId = "temp_buff",
                        StatusDuration = 1
                    }
                }
            };

            pipeline.Statuses.RegisterStatus(new StatusDefinition
            {
                Id = "temp_buff",
                Name = "Temp Buff",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            pipeline.RegisterAbility(ability);

            // Act
            var first = pipeline.ExecuteAbility("flurry_like", source, new List<Combatant> { source });
            var second = pipeline.ExecuteAbility("flurry_like", source, new List<Combatant> { source });
            var third = pipeline.ExecuteAbility("flurry_like", source, new List<Combatant> { source });

            // Assert
            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.False(third.Success);
            Assert.Equal(0, source.ResourcePool.GetCurrent("ki_points"));
            Assert.Contains("resource", third.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

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
            source.LifeState = CombatantLifeState.Downed;
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
            source.ActionBudget.ResetFull();

            // Assert
            var (afterReset, _) = pipeline.CanUseAbility("once_per_encounter", source);
            Assert.True(afterReset);
        }

        #endregion

        #region Height Modifier Tests

        [Fact]
        public void ExecuteAbility_HighGround_GivesPlus2AttackBonus()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 123);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 10, 0); // High ground
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(0, 0, 0); // Low position

            var heightService = new HeightService { AdvantageThreshold = 3f };
            pipeline.Heights = heightService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            Assert.Equal(2f, result.AttackResult.BaseValue); // +2 from high ground
            Assert.Contains(result.AttackResult.AppliedModifiers, m => m.Source == "High Ground" && m.Value == 2);
        }

        [Fact]
        public void ExecuteAbility_LowGround_GivesMinus2AttackPenalty()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 456);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 0, 0); // Low ground
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(0, 10, 0); // High position

            var heightService = new HeightService { AdvantageThreshold = 3f };
            pipeline.Heights = heightService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            Assert.Equal(-2f, result.AttackResult.BaseValue); // -2 from low ground
            Assert.Contains(result.AttackResult.AppliedModifiers, m => m.Source == "Low Ground" && m.Value == -2);
        }

        [Fact]
        public void ExecuteAbility_LevelGround_NoModifier()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 789);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 5, 0);
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(0, 5, 0); // Same height

            var heightService = new HeightService { AdvantageThreshold = 3f };
            pipeline.Heights = heightService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            Assert.Equal(0f, result.AttackResult.BaseValue); // No height modifier
            Assert.DoesNotContain(result.AttackResult.AppliedModifiers, m => m.Source == "High Ground" || m.Source == "Low Ground");
        }

        #endregion

        #region Cover AC Bonus Tests

        [Fact]
        public void ExecuteAbility_HalfCover_GivesPlus2AC()
        {
            // Arrange - Use seed that produces a roll that would normally hit but misses with cover
            var (pipeline, rules, statuses) = CreatePipeline(seed: 12345);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(10, 0, 0);

            var losService = new LOSService();
            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);
            // Add obstacle providing half cover
            losService.RegisterObstacle(new Obstacle
            {
                Id = "wall1",
                Position = new Vector3(5, 0, 0),
                Width = 2f,
                Height = 2f,
                ProvidedCover = CoverLevel.Half,
                BlocksLOS = false
            });
            pipeline.LOS = losService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            // Cover AC modifier should be in the breakdown
            Assert.Contains(result.AttackResult.AppliedModifiers, m => m.Source == "Half Cover" && m.Value == 2);
        }

        [Fact]
        public void ExecuteAbility_ThreeQuartersCover_GivesPlus5AC()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 54321);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(10, 0, 0);

            var losService = new LOSService();
            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);
            // Add obstacle providing three-quarters cover
            losService.RegisterObstacle(new Obstacle
            {
                Id = "barrier1",
                Position = new Vector3(5, 0, 0),
                Width = 2f,
                Height = 2f,
                ProvidedCover = CoverLevel.ThreeQuarters,
                BlocksLOS = false
            });
            pipeline.LOS = losService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            // Three-quarters cover AC modifier (+5) should be in the breakdown
            Assert.Contains(result.AttackResult.AppliedModifiers, m => m.Source == "Three-Quarters Cover" && m.Value == 5);
        }

        [Fact]
        public void ExecuteAbility_NoCover_NoACBonus()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 99999);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(10, 0, 0);

            var losService = new LOSService();
            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);
            // No obstacles - clear LOS
            pipeline.LOS = losService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            // No cover modifiers in breakdown
            Assert.DoesNotContain(result.AttackResult.AppliedModifiers, m => m.Source.Contains("Cover"));
        }

        #endregion

        #region Combined Height and Cover Tests

        [Fact]
        public void ExecuteAbility_HighGroundWithCover_AppliesBothModifiers()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 11111);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 10, 0); // High ground
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(10, 0, 0); // Low, behind cover

            var heightService = new HeightService { AdvantageThreshold = 3f };
            pipeline.Heights = heightService;

            var losService = new LOSService();
            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);
            losService.RegisterObstacle(new Obstacle
            {
                Id = "cover1",
                Position = new Vector3(5, 0, 0),
                Width = 2f,
                Height = 8f,  // Tall enough to provide cover from high ground
                ProvidedCover = CoverLevel.Half,
                BlocksLOS = false
            });
            pipeline.LOS = losService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            Assert.Equal(2f, result.AttackResult.BaseValue); // +2 from high ground
            Assert.Contains(result.AttackResult.AppliedModifiers, m => m.Source == "High Ground" && m.Value == 2);
            Assert.Contains(result.AttackResult.AppliedModifiers, m => m.Source == "Half Cover" && m.Value == 2);
        }

        [Fact]
        public void HeightAndCover_AppearInBreakdown()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline(seed: 22222);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 10, 0); // High ground
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(10, 0, 0);

            var heightService = new HeightService { AdvantageThreshold = 3f };
            pipeline.Heights = heightService;

            var losService = new LOSService();
            losService.RegisterCombatant(source);
            losService.RegisterCombatant(target);
            losService.RegisterObstacle(new Obstacle
            {
                Id = "cover1",
                Position = new Vector3(5, 0, 0),
                Width = 2f,
                Height = 10f,  // Tall enough to provide cover from high ground
                ProvidedCover = CoverLevel.ThreeQuarters,
                BlocksLOS = false
            });
            pipeline.LOS = losService;

            var ability = new AbilityDefinition
            {
                Id = "attack",
                Name = "Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAbility(ability);

            // Act
            var result = pipeline.ExecuteAbility("attack", source, new List<Combatant> { target });
            var breakdown = result.AttackResult.GetBreakdown();

            // Assert
            Assert.Contains("High Ground", breakdown);
            Assert.Contains("Three-Quarters Cover", breakdown);
        }

        [Fact]
        public void ExecuteAbility_ThreatenedSource_RangedAttackHasDisadvantage()
        {
            var (pipeline, rules, statuses) = CreatePipeline(seed: 1234);
            var source = CreateCombatant("attacker", 100);
            var target = CreateCombatant("defender", 100);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "threatened",
                Name = "Threatened",
                DurationType = DurationType.Turns,
                DefaultDuration = 1,
                Modifiers = new List<StatusModifier>
                {
                    new StatusModifier
                    {
                        Target = ModifierTarget.AttackRoll,
                        Type = ModifierType.Disadvantage,
                        Value = 1
                    }
                }
            });

            statuses.ApplyStatus("threatened", target.Id, source.Id, duration: 1);

            var ability = new AbilityDefinition
            {
                Id = "ranged_attack_test",
                Name = "Ranged Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "1d8", DamageType = "piercing", Condition = "on_hit" }
                }
            };
            pipeline.RegisterAbility(ability);

            var result = pipeline.ExecuteAbility("ranged_attack_test", source, new List<Combatant> { target });

            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            Assert.Equal(-1, result.AttackResult.AdvantageState);
        }

        [Fact]
        public void ExecuteAbility_ParalyzedTarget_MeleeHitAutoCrits()
        {
            var (pipeline, rules, statuses) = CreatePipeline(seed: 77);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(1, 0, 0);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "paralyzed",
                Name = "Paralyzed",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.ApplyStatus("paralyzed", source.Id, target.Id, duration: 1);

            // Ensure the attack lands so auto-crit behavior can be verified.
            rules.AddModifier(source.Id, Modifier.Flat("Test Accuracy", ModifierTarget.AttackRoll, 20, "test"));

            var ability = new AbilityDefinition
            {
                Id = "melee_attack_test",
                Name = "Melee Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "1d8", DamageType = "slashing", Condition = "on_hit" }
                }
            };
            pipeline.RegisterAbility(ability);

            var result = pipeline.ExecuteAbility("melee_attack_test", source, new List<Combatant> { target });

            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);
            Assert.True(result.AttackResult.IsSuccess);
            Assert.True(result.AttackResult.IsCritical);
        }

        [Fact]
        public void ExecuteAbility_ParalyzedTarget_DexSaveAutoFails()
        {
            var (pipeline, rules, statuses) = CreatePipeline(seed: 9);
            var source = CreateCombatant("caster", 100);
            var target = CreateCombatant("target", 100);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "paralyzed",
                Name = "Paralyzed",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "test_locked",
                Name = "Locked",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.ApplyStatus("paralyzed", source.Id, target.Id, duration: 1);

            var ability = new AbilityDefinition
            {
                Id = "dex_save_spell",
                Name = "Dex Save Spell",
                TargetType = TargetType.SingleUnit,
                SaveType = "dexterity",
                SaveDC = 99,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "apply_status",
                        StatusId = "test_locked",
                        StatusDuration = 1,
                        Condition = "on_save_fail"
                    }
                }
            };
            pipeline.RegisterAbility(ability);

            var result = pipeline.ExecuteAbility("dex_save_spell", source, new List<Combatant> { target });

            Assert.True(result.Success);
            Assert.NotNull(result.SaveResult);
            Assert.False(result.SaveResult.IsSuccess);
            Assert.True(statuses.HasStatus(target.Id, "test_locked"));
        }

        [Fact]
        public void ExecuteAbility_BlindedSource_RangedBeyond3mFails()
        {
            var (pipeline, rules, statuses) = CreatePipeline(seed: 31);
            var source = CreateCombatant("attacker", 100);
            source.Position = new Vector3(0, 0, 0);
            var target = CreateCombatant("defender", 100);
            target.Position = new Vector3(10, 0, 0);

            statuses.RegisterStatus(new StatusDefinition
            {
                Id = "blinded",
                Name = "Blinded",
                DurationType = DurationType.Turns,
                DefaultDuration = 1
            });
            statuses.ApplyStatus("blinded", target.Id, source.Id, duration: 1);

            var ability = new AbilityDefinition
            {
                Id = "long_ranged_attack",
                Name = "Long Ranged Attack",
                TargetType = TargetType.SingleUnit,
                AttackType = AttackType.RangedSpell,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "1d10", DamageType = "force", Condition = "on_hit" }
                }
            };
            pipeline.RegisterAbility(ability);

            var result = pipeline.ExecuteAbility("long_ranged_attack", source, new List<Combatant> { target });

            Assert.False(result.Success);
            Assert.Contains("Blinded limits ranged attacks", result.ErrorMessage);
        }

        #endregion
    }
}
