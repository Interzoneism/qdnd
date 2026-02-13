using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Rules;
using QDND.Combat.Entities;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Tests for the Dodge (Patient Defence) and Threatened mechanics.
    /// </summary>
    public class DodgeAndThreatenedTests
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

        private Combatant CreateCombatant(string id, Faction faction = Faction.Player, Vector3? position = null)
        {
            var combatant = new Combatant(id, $"Test_{id}", faction, 100, 10);
            if (position.HasValue)
            {
                combatant.Position = position.Value;
            }
            return combatant;
        }

        private void RegisterDodgingStatus(StatusManager statuses)
        {
            var dodgingStatus = new StatusDefinition
            {
                Id = "dodging",
                Name = "Dodging",
                Description = "Focused on defense, harder to hit.",
                DurationType = DurationType.Turns,
                DefaultDuration = 1,
                IsBuff = true,
                Tags = new HashSet<string> { "buff", "defensive" }
            };
            statuses.RegisterStatus(dodgingStatus);
        }

        [Fact]
        public void Dodge_TargetDodging_AttackerHasDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            RegisterDodgingStatus(statuses);

            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var defender = CreateCombatant("defender", Faction.Hostile, new Vector3(3, 0, 0));

            // Apply dodging status to defender
            statuses.ApplyStatus("dodging", defender.Id, defender.Id, 1);

            var meleeAttack = new ActionDefinition
            {
                Id = "melee_attack",
                Name = "Melee Attack",
                AttackType = AttackType.MeleeWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAction(meleeAttack);

            // Act
            var result = pipeline.ExecuteAction("melee_attack", attacker, new List<Combatant> { defender });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Check that disadvantage was applied
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            Assert.NotNull(disadvantageSources);
            Assert.Contains("Target Dodging", disadvantageSources);
        }

        [Fact]
        public void Dodge_TargetNotDodging_NoDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            RegisterDodgingStatus(statuses);

            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var defender = CreateCombatant("defender", Faction.Hostile, new Vector3(3, 0, 0));

            // Don't apply dodging status

            var meleeAttack = new ActionDefinition
            {
                Id = "melee_attack",
                Name = "Melee Attack",
                AttackType = AttackType.MeleeWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAction(meleeAttack);

            // Act
            var result = pipeline.ExecuteAction("melee_attack", attacker, new List<Combatant> { defender });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Check that there's no disadvantage from dodging
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            if (disadvantageSources != null)
            {
                Assert.DoesNotContain("Target Dodging", disadvantageSources);
            }
        }

        [Fact]
        public void Threatened_RangedAttackNearHostile_HasDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();

            // Attacker at (0,0,0), target at (10,0,0), hostile nearby at (1,0,0) - within 1.5m
            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", Faction.Hostile, new Vector3(10, 0, 0));
            var nearbyHostile = CreateCombatant("nearby_hostile", Faction.Hostile, new Vector3(1, 0, 0));

            // Setup GetCombatants to return all combatants
            var allCombatants = new List<Combatant> { attacker, target, nearbyHostile };
            pipeline.GetCombatants = () => allCombatants;

            var rangedAttack = new ActionDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                AttackType = AttackType.RangedWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAction(rangedAttack);

            // Act
            var result = pipeline.ExecuteAction("ranged_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Check that disadvantage was applied due to threatened
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            Assert.NotNull(disadvantageSources);
            Assert.Contains("Threatened", disadvantageSources);
        }

        [Fact]
        public void Threatened_RangedAttackFarFromHostile_NoDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();

            // Attacker at (0,0,0), target at (10,0,0), hostile nearby at (3,0,0) - outside 1.5m range
            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", Faction.Hostile, new Vector3(10, 0, 0));
            var distantHostile = CreateCombatant("distant_hostile", Faction.Hostile, new Vector3(3, 0, 0));

            // Setup GetCombatants to return all combatants
            var allCombatants = new List<Combatant> { attacker, target, distantHostile };
            pipeline.GetCombatants = () => allCombatants;

            var rangedAttack = new ActionDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                AttackType = AttackType.RangedWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAction(rangedAttack);

            // Act
            var result = pipeline.ExecuteAction("ranged_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Check that there's no disadvantage from threatened
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            if (disadvantageSources != null)
            {
                Assert.DoesNotContain("Threatened", disadvantageSources);
            }
        }

        [Fact]
        public void Threatened_SpellAttackNearHostile_HasDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();

            // Attacker at (0,0,0), target at (10,0,0), hostile nearby at (1.2,0,0) - within 1.5m
            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", Faction.Hostile, new Vector3(10, 0, 0));
            var nearbyHostile = CreateCombatant("nearby_hostile", Faction.Hostile, new Vector3(1.2f, 0, 0));

            // Setup GetCombatants to return all combatants
            var allCombatants = new List<Combatant> { attacker, target, nearbyHostile };
            pipeline.GetCombatants = () => allCombatants;

            var spellAttack = new ActionDefinition
            {
                Id = "spell_attack",
                Name = "Spell Attack",
                AttackType = AttackType.RangedSpell,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "fire" }
                }
            };
            pipeline.RegisterAction(spellAttack);

            // Act
            var result = pipeline.ExecuteAction("spell_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Check that disadvantage was applied due to threatened
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            Assert.NotNull(disadvantageSources);
            Assert.Contains("Threatened", disadvantageSources);
        }

        [Fact]
        public void Threatened_MeleeAttackNearHostile_NoDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();

            // Attacker at (0,0,0), target at (1,0,0), hostile nearby at (1.2,0,0) - within 1.5m
            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", Faction.Hostile, new Vector3(1, 0, 0));
            var nearbyHostile = CreateCombatant("nearby_hostile", Faction.Hostile, new Vector3(1.2f, 0, 0));

            // Setup GetCombatants to return all combatants
            var allCombatants = new List<Combatant> { attacker, target, nearbyHostile };
            pipeline.GetCombatants = () => allCombatants;

            var meleeAttack = new ActionDefinition
            {
                Id = "melee_attack",
                Name = "Melee Attack",
                AttackType = AttackType.MeleeWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "slashing" }
                }
            };
            pipeline.RegisterAction(meleeAttack);

            // Act
            var result = pipeline.ExecuteAction("melee_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Melee attacks should NOT be affected by threatened
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            if (disadvantageSources != null)
            {
                Assert.DoesNotContain("Threatened", disadvantageSources);
            }
        }

        [Fact]
        public void Threatened_IgnoresAllies_OnlyHostilesMatter()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();

            // Attacker at (0,0,0), target at (10,0,0), allied unit nearby at (1,0,0) - within 1.5m but same faction
            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", Faction.Hostile, new Vector3(10, 0, 0));
            var nearbyAlly = CreateCombatant("nearby_ally", Faction.Player, new Vector3(1, 0, 0));

            // Setup GetCombatants to return all combatants
            var allCombatants = new List<Combatant> { attacker, target, nearbyAlly };
            pipeline.GetCombatants = () => allCombatants;

            var rangedAttack = new ActionDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                AttackType = AttackType.RangedWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAction(rangedAttack);

            // Act
            var result = pipeline.ExecuteAction("ranged_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Allies should not cause threatened
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            if (disadvantageSources != null)
            {
                Assert.DoesNotContain("Threatened", disadvantageSources);
            }
        }

        [Fact]
        public void Combined_DodgingAndThreatened_BothApplyDisadvantage()
        {
            // Arrange
            var (pipeline, rules, statuses) = CreatePipeline();
            RegisterDodgingStatus(statuses);

            // Attacker at (0,0,0), target at (10,0,0), hostile nearby at (1,0,0)
            var attacker = CreateCombatant("attacker", Faction.Player, new Vector3(0, 0, 0));
            var target = CreateCombatant("target", Faction.Hostile, new Vector3(10, 0, 0));
            var nearbyHostile = CreateCombatant("nearby_hostile", Faction.Hostile, new Vector3(1, 0, 0));

            // Apply dodging status to target
            statuses.ApplyStatus("dodging", target.Id, target.Id, 1);

            // Setup GetCombatants to return all combatants
            var allCombatants = new List<Combatant> { attacker, target, nearbyHostile };
            pipeline.GetCombatants = () => allCombatants;

            var rangedAttack = new ActionDefinition
            {
                Id = "ranged_attack",
                Name = "Ranged Attack",
                AttackType = AttackType.RangedWeapon,
                TargetType = TargetType.SingleUnit,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", Value = 10, DamageType = "piercing" }
                }
            };
            pipeline.RegisterAction(rangedAttack);

            // Act
            var result = pipeline.ExecuteAction("ranged_attack", attacker, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.AttackResult);

            // Both disadvantage sources should be present
            var disadvantageSources = result.AttackResult.Input.Parameters.TryGetValue("statusDisadvantageSources", out var disObj)
                ? disObj as List<string>
                : null;

            Assert.NotNull(disadvantageSources);
            Assert.Contains("Target Dodging", disadvantageSources);
            Assert.Contains("Threatened", disadvantageSources);
            Assert.Equal(2, disadvantageSources.Count);
        }
    }
}
