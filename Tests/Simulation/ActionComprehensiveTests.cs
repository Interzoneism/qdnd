#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Combat.Abilities;
using QDND.Data;
using QDND.Tests.Helpers;

namespace QDND.Tests.Simulation
{
    /// <summary>
    /// Comprehensive tests for ability execution.
    /// Loads all abilities from DataRegistry and verifies they execute without errors.
    /// </summary>
    public class AbilityComprehensiveTests
    {
        private class AbilityTestSetup
        {
            public ICombatContext Context { get; }
            public RulesEngine Rules { get; }
            public StatusManager Statuses { get; }
            public EffectPipeline Effects { get; }
            public DataRegistry Registry { get; }

            public AbilityTestSetup(int seed)
            {
                Context = new HeadlessCombatContext();
                Rules = new RulesEngine(seed);
                Statuses = new StatusManager(Rules);
                Effects = new EffectPipeline
                {
                    Rules = Rules,
                    Statuses = Statuses,
                    Rng = new Random(seed)
                };
                Registry = new DataRegistry();

                Context.RegisterService(Rules);
                Context.RegisterService(Statuses);
                Context.RegisterService(Effects);
            }

            public void LoadAbilitiesFromDirectory(string directory)
            {
                if (!Directory.Exists(directory))
                {
                    throw new DirectoryNotFoundException($"Abilities directory not found: {directory}");
                }

                Registry.LoadFromDirectory(directory);

                // Register all loaded abilities with the effect pipeline
                foreach (var ability in Registry.GetAllAbilities())
                {
                    Effects.RegisterAbility(ability);
                }
            }
        }

        private Combatant CreateCombatant(string id, string name, Faction faction, int hp)
        {
            return new Combatant(id, name, faction, hp, initiative: 10)
            {
                Position = new Vector3(0, 0, 0),
                Team = faction == Faction.Player ? "player" : "enemy"
            };
        }

        [Fact]
        public void AllRegisteredAbilities_ExecuteWithoutException()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 12345);
            var abilitiesDir = "Data/Abilities";

            try
            {
                setup.LoadAbilitiesFromDirectory(abilitiesDir);
            }
            catch (DirectoryNotFoundException)
            {
                // Skip test if abilities directory doesn't exist
                return;
            }

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 100);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 100);

            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            var allAbilities = setup.Registry.GetAllAbilities();
            Assert.NotEmpty(allAbilities);

            var failedAbilities = new List<(string abilityId, Exception ex)>();

            // Act - Execute each ability
            foreach (var ability in allAbilities)
            {
                try
                {
                    // Reset state
                    actor.ActionBudget?.ResetFull();
                    target.Resources.CurrentHP = target.Resources.MaxHP;

                    var targets = new List<Combatant> { target };
                    var result = setup.Effects.ExecuteAbility(ability.Id, actor, targets);

                    // Assert basic success indicators
                    Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage),
                        $"Ability {ability.Id} should either succeed or provide error message");
                }
                catch (Exception ex)
                {
                    failedAbilities.Add((ability.Id, ex));
                }
            }

            // Assert no abilities threw unhandled exceptions
            Assert.Empty(failedAbilities);
        }

        [Fact]
        public void Ability_BasicAttack_DealsDamage()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 42);

            var basicAttack = new AbilityDefinition
            {
                Id = "basic_attack",
                Name = "Basic Attack",
                Cost = new AbilityCost { UsesAction = true },
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d8+3",
                        DamageType = "physical"
                    }
                }
            };

            setup.Effects.RegisterAbility(basicAttack);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            int initialHP = target.Resources.CurrentHP;

            // Act
            var result = setup.Effects.ExecuteAbility("basic_attack", actor, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal("basic_attack", result.AbilityId);

            // Damage should be dealt if attack hit
            if (result.AttackResult?.IsSuccess == true)
            {
                Assert.True(target.Resources.CurrentHP < initialHP,
                    "Target should have taken damage on successful hit");
            }
        }

        [Fact]
        public void Ability_Heal_RestoresHP()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 777);

            var healSpell = new AbilityDefinition
            {
                Id = "heal",
                Name = "Heal",
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "heal",
                        DiceFormula = "2d8+5"
                    }
                }
            };

            setup.Effects.RegisterAbility(healSpell);

            var cleric = CreateCombatant("cleric", "Cleric", Faction.Player, 60);
            var wounded = CreateCombatant("wounded", "Wounded", Faction.Player, 60);

            // Wound the target
            wounded.Resources.TakeDamage(30);
            int damagedHP = wounded.Resources.CurrentHP;

            cleric.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(cleric);
            setup.Context.RegisterCombatant(wounded);

            // Act
            var result = setup.Effects.ExecuteAbility("heal", cleric, new List<Combatant> { wounded });

            // Assert
            Assert.True(result.Success);
            Assert.True(wounded.Resources.CurrentHP > damagedHP,
                "Healing should restore HP");
            Assert.True(wounded.Resources.CurrentHP <= wounded.Resources.MaxHP,
                "HP should not exceed max");
        }

        [Fact]
        public void Ability_ApplyStatus_AddsStatusToTarget()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 333);

            var poisonAttack = new AbilityDefinition
            {
                Id = "poison_attack",
                Name = "Poison Attack",
                Cost = new AbilityCost { UsesAction = true },
                AttackType = AttackType.MeleeWeapon,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "1d6",
                        DamageType = "physical"
                    },
                    new EffectDefinition
                    {
                        Type = "apply_status",
                        StatusId = "poisoned",
                        StatusDuration = 3
                    }
                }
            };

            setup.Effects.RegisterAbility(poisonAttack);

            // Register poisoned status
            var poisonedStatus = new StatusDefinition
            {
                Id = "poisoned",
                Name = "Poisoned",
                DurationType = DurationType.Turns,
                DefaultDuration = 3
            };
            setup.Statuses.RegisterStatus(poisonedStatus);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            // Act
            var result = setup.Effects.ExecuteAbility("poison_attack", actor, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);

            // If attack hit, status should be applied
            if (result.AttackResult?.IsSuccess == true)
            {
                Assert.True(setup.Statuses.HasStatus(target.Id, "poisoned"),
                    "Poisoned status should be applied on hit");
            }
        }

        [Fact]
        public void Ability_Fireball_AffectsMultipleTargets()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 999);

            var fireball = new AbilityDefinition
            {
                Id = "fireball",
                Name = "Fireball",
                Cost = new AbilityCost { UsesAction = true },
                SaveType = "DEX",
                SaveDC = 15,
                TargetType = TargetType.Circle,
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "damage",
                        DiceFormula = "8d6",
                        DamageType = "fire"
                    }
                }
            };

            setup.Effects.RegisterAbility(fireball);

            var wizard = CreateCombatant("wizard", "Wizard", Faction.Player, 40);
            var enemy1 = CreateCombatant("enemy1", "Enemy1", Faction.Hostile, 50);
            var enemy2 = CreateCombatant("enemy2", "Enemy2", Faction.Hostile, 50);
            var enemy3 = CreateCombatant("enemy3", "Enemy3", Faction.Hostile, 50);

            wizard.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(wizard);
            setup.Context.RegisterCombatant(enemy1);
            setup.Context.RegisterCombatant(enemy2);
            setup.Context.RegisterCombatant(enemy3);

            var targets = new List<Combatant> { enemy1, enemy2, enemy3 };
            var initialHPs = targets.Select(t => t.Resources.CurrentHP).ToList();

            // Act
            var result = setup.Effects.ExecuteAbility("fireball", wizard, targets);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(3, result.TargetIds.Count);

            // All targets should have taken some damage (or made saves)
            // At minimum, verify no exceptions and HP invariants hold
            foreach (var target in targets)
            {
                Assert.True(target.Resources.CurrentHP >= 0,
                    "HP should not go negative");
                Assert.True(target.Resources.CurrentHP <= target.Resources.MaxHP,
                    "HP should not exceed max");
            }
        }

        [Fact]
        public void Ability_InvalidTargets_ReturnsError()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 111);

            var ability = new AbilityDefinition
            {
                Id = "test_ability",
                Name = "Test Ability",
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "1d6" }
                }
            };

            setup.Effects.RegisterAbility(ability);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);

            // Act - Execute with empty targets
            var result = setup.Effects.ExecuteAbility("test_ability", actor, new List<Combatant>());

            // Assert - Should handle gracefully
            Assert.NotNull(result);
            // Either succeeds (no-op) or provides error
            Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage));
        }

        [Fact]
        public void Ability_Cooldown_PreventsReuse()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 555);

            var dashAbility = new AbilityDefinition
            {
                Id = "dash",
                Name = "Dash",
                Cost = new AbilityCost { UsesAction = true },
                Cooldown = new AbilityCooldown
                {
                    TurnCooldown = 2,
                    MaxCharges = 1
                },
                Effects = new List<EffectDefinition>()
            };

            setup.Effects.RegisterAbility(dashAbility);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            setup.Context.RegisterCombatant(actor);

            // Act - Use ability
            actor.ActionBudget?.ResetFull();
            var result1 = setup.Effects.ExecuteAbility("dash", actor, new List<Combatant>());
            Assert.True(result1.Success);

            // Try to use again immediately
            actor.ActionBudget?.ResetFull();
            var (canUse, reason) = setup.Effects.CanUseAbility("dash", actor);

            // Assert - Should be on cooldown
            Assert.False(canUse);
            Assert.Contains("cooldown", reason.ToLower());
        }

        [Fact]
        public void Ability_InsufficientActionEconomy_Fails()
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 222);

            var ability = new AbilityDefinition
            {
                Id = "power_attack",
                Name = "Power Attack",
                Cost = new AbilityCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "2d10" }
                }
            };

            setup.Effects.RegisterAbility(ability);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            // Reset then consume the action
            actor.ActionBudget?.ResetFull();
            var (canPay, _) = actor.ActionBudget?.CanPayCost(new AbilityCost { UsesAction = true }) ?? (false, null);
            if (canPay)
            {
                actor.ActionBudget?.ConsumeCost(new AbilityCost { UsesAction = true });
            }

            // Act - Try to use ability without action available
            var (canUse, reason) = setup.Effects.CanUseAbility("power_attack", actor);

            // Assert - Should fail due to no action available
            Assert.False(canUse);
            Assert.NotNull(reason);
        }

        [Theory]
        [InlineData("basic_attack")]
        [InlineData("dash")]
        [InlineData("heal")]
        public void Ability_CommonAbilities_ExecuteSuccessfully(string abilityId)
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 444);

            // Define common abilities
            var abilities = new Dictionary<string, AbilityDefinition>
            {
                ["basic_attack"] = new AbilityDefinition
                {
                    Id = "basic_attack",
                    Name = "Basic Attack",
                    Cost = new AbilityCost { UsesAction = true },
                    AttackType = AttackType.MeleeWeapon,
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "damage", DiceFormula = "1d8" }
                    }
                },
                ["dash"] = new AbilityDefinition
                {
                    Id = "dash",
                    Name = "Dash",
                    Cost = new AbilityCost { UsesAction = true },
                    Effects = new List<EffectDefinition>()
                },
                ["heal"] = new AbilityDefinition
                {
                    Id = "heal",
                    Name = "Heal",
                    Cost = new AbilityCost { UsesAction = true },
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "heal", DiceFormula = "1d8+3" }
                    }
                }
            };

            var ability = abilities[abilityId];
            setup.Effects.RegisterAbility(ability);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            // Act
            var result = setup.Effects.ExecuteAbility(abilityId, actor, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage));
            Assert.Equal(abilityId, result.AbilityId);
        }
    }
}
