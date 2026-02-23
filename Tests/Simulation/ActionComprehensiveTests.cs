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
using QDND.Combat.Actions;
using QDND.Data;
using QDND.Tests.Helpers;

namespace QDND.Tests.Simulation
{
    /// <summary>
    /// Comprehensive tests for ability execution.
    /// Loads all abilities from DataRegistry and verifies they execute without errors.
    /// </summary>
    public class ActionComprehensiveTests
    {
        private class AbilityTestSetup
        {
            public ICombatContext Context { get; }
            public RulesEngine Rules { get; }
            public StatusManager Statuses { get; }
            public EffectPipeline Effects { get; }
            public ActionRegistry Registry { get; }

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
                Registry = new ActionRegistry();

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

                QDND.Data.Actions.ActionRegistryInitializer.LoadJsonActions(directory, Registry);

                // Register all loaded actions with the effect pipeline
                foreach (var action in Registry.GetAllActions())
                {
                    Effects.RegisterAction(action);
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
            var abilitiesDir = "Data/Actions";

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

            var allAbilities = setup.Registry.GetAllActions();
            Assert.NotEmpty(allAbilities);

            var failedAbilities = new List<(string actionId, Exception ex)>();

            // Act - Execute each ability
            foreach (var action in allAbilities)
            {
                try
                {
                    // Reset state
                    actor.ActionBudget?.ResetFull();
                    target.Resources.CurrentHP = target.Resources.MaxHP;

                    var targets = new List<Combatant> { target };
                    var result = setup.Effects.ExecuteAction(action.Id, actor, targets);

                    // Assert basic success indicators
                    Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage),
                        $"Ability {action.Id} should either succeed or provide error message");
                }
                catch (Exception ex)
                {
                    failedAbilities.Add((action.Id, ex));
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

            var basicAttack = new ActionDefinition
            {
                Id = "Target_MainHandAttack",
                Name = "Basic Attack",
                Cost = new ActionCost { UsesAction = true },
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

            setup.Effects.RegisterAction(basicAttack);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            int initialHP = target.Resources.CurrentHP;

            // Act
            var result = setup.Effects.ExecuteAction("Target_MainHandAttack", actor, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Target_MainHandAttack", result.ActionId);

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

            var healSpell = new ActionDefinition
            {
                Id = "heal",
                Name = "Heal",
                Cost = new ActionCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition
                    {
                        Type = "heal",
                        DiceFormula = "2d8+5"
                    }
                }
            };

            setup.Effects.RegisterAction(healSpell);

            var cleric = CreateCombatant("cleric", "Cleric", Faction.Player, 60);
            var wounded = CreateCombatant("wounded", "Wounded", Faction.Player, 60);

            // Wound the target
            wounded.Resources.TakeDamage(30);
            int damagedHP = wounded.Resources.CurrentHP;

            cleric.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(cleric);
            setup.Context.RegisterCombatant(wounded);

            // Act
            var result = setup.Effects.ExecuteAction("heal", cleric, new List<Combatant> { wounded });

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

            var poisonAttack = new ActionDefinition
            {
                Id = "poison_attack",
                Name = "Poison Attack",
                Cost = new ActionCost { UsesAction = true },
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

            setup.Effects.RegisterAction(poisonAttack);

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
            var result = setup.Effects.ExecuteAction("poison_attack", actor, new List<Combatant> { target });

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

            var fireball = new ActionDefinition
            {
                Id = "Projectile_Fireball",
                Name = "Fireball",
                Cost = new ActionCost { UsesAction = true },
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

            setup.Effects.RegisterAction(fireball);

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
            var result = setup.Effects.ExecuteAction("Projectile_Fireball", wizard, targets);

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

            var action = new ActionDefinition
            {
                Id = "test_ability",
                Name = "Test Ability",
                Cost = new ActionCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "1d6" }
                }
            };

            setup.Effects.RegisterAction(action);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);

            // Act - Execute with empty targets
            var result = setup.Effects.ExecuteAction("test_ability", actor, new List<Combatant>());

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

            var dashAbility = new ActionDefinition
            {
                Id = "dash",
                Name = "Dash",
                Cost = new ActionCost { UsesAction = true },
                Cooldown = new ActionCooldown
                {
                    TurnCooldown = 2,
                    MaxCharges = 1
                },
                Effects = new List<EffectDefinition>()
            };

            setup.Effects.RegisterAction(dashAbility);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            setup.Context.RegisterCombatant(actor);

            // Act - Use ability
            actor.ActionBudget?.ResetFull();
            var result1 = setup.Effects.ExecuteAction("dash", actor, new List<Combatant>());
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

            var action = new ActionDefinition
            {
                Id = "power_attack",
                Name = "Power Attack",
                Cost = new ActionCost { UsesAction = true },
                Effects = new List<EffectDefinition>
                {
                    new EffectDefinition { Type = "damage", DiceFormula = "2d10" }
                }
            };

            setup.Effects.RegisterAction(action);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            // Reset then consume the action
            actor.ActionBudget?.ResetFull();
            var (canPay, _) = actor.ActionBudget?.CanPayCost(new ActionCost { UsesAction = true }) ?? (false, null);
            if (canPay)
            {
                actor.ActionBudget?.ConsumeCost(new ActionCost { UsesAction = true });
            }

            // Act - Try to use ability without action available
            var (canUse, reason) = setup.Effects.CanUseAbility("power_attack", actor);

            // Assert - Should fail due to no action available
            Assert.False(canUse);
            Assert.NotNull(reason);
        }

        [Theory]
        [InlineData("Target_MainHandAttack")]
        [InlineData("dash")]
        [InlineData("heal")]
        public void Ability_CommonAbilities_ExecuteSuccessfully(string actionId)
        {
            // Arrange
            var setup = new AbilityTestSetup(seed: 444);

            // Define common abilities
            var abilities = new Dictionary<string, ActionDefinition>
            {
                ["Target_MainHandAttack"] = new ActionDefinition
                {
                    Id = "Target_MainHandAttack",
                    Name = "Basic Attack",
                    Cost = new ActionCost { UsesAction = true },
                    AttackType = AttackType.MeleeWeapon,
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "damage", DiceFormula = "1d8" }
                    }
                },
                ["dash"] = new ActionDefinition
                {
                    Id = "dash",
                    Name = "Dash",
                    Cost = new ActionCost { UsesAction = true },
                    Effects = new List<EffectDefinition>()
                },
                ["heal"] = new ActionDefinition
                {
                    Id = "heal",
                    Name = "Heal",
                    Cost = new ActionCost { UsesAction = true },
                    Effects = new List<EffectDefinition>
                    {
                        new EffectDefinition { Type = "heal", DiceFormula = "1d8+3" }
                    }
                }
            };

            var action = abilities[actionId];
            setup.Effects.RegisterAction(action);

            var actor = CreateCombatant("actor", "Actor", Faction.Player, 50);
            var target = CreateCombatant("target", "Target", Faction.Hostile, 50);

            actor.ActionBudget?.ResetFull();
            setup.Context.RegisterCombatant(actor);
            setup.Context.RegisterCombatant(target);

            // Act
            var result = setup.Effects.ExecuteAction(actionId, actor, new List<Combatant> { target });

            // Assert
            Assert.True(result.Success || !string.IsNullOrEmpty(result.ErrorMessage));
            Assert.Equal(actionId, result.ActionId);
        }
    }
}
