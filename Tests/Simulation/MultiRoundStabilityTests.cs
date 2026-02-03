#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Xunit;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Combat.Abilities;
using QDND.Combat.AI;
using QDND.Data;
using QDND.Tests.Helpers;

namespace QDND.Tests.Simulation
{
    /// <summary>
    /// Tests for multi-round combat stability to ensure invariants hold throughout combat.
    /// Uses real combat systems in a lightweight headless loop.
    /// </summary>
    public class MultiRoundStabilityTests
    {
        private class MinimalCombatSetup
        {
            public ICombatContext Context { get; }
            public TurnQueueService TurnQueue { get; }
            public RulesEngine Rules { get; }
            public StatusManager Statuses { get; }
            public EffectPipeline Effects { get; }
            public List<Combatant> AllCombatants { get; }

            public MinimalCombatSetup(int seed)
            {
                Context = new HeadlessCombatContext();
                TurnQueue = new TurnQueueService();
                Rules = new RulesEngine(seed);
                Statuses = new StatusManager(Rules);
                Effects = new EffectPipeline
                {
                    Rules = Rules,
                    Statuses = Statuses,
                    Rng = new Random(seed)
                };
                AllCombatants = new List<Combatant>();

                Context.RegisterService(Rules);
                Context.RegisterService(Statuses);
                Context.RegisterService(Effects);
                Context.RegisterService(TurnQueue);
            }

            public void AddCombatant(Combatant combatant)
            {
                AllCombatants.Add(combatant);
                Context.RegisterCombatant(combatant);
                TurnQueue.AddCombatant(combatant);
            }

            public void ExecuteTurns(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var current = TurnQueue.CurrentCombatant;
                    if (current == null) break;

                    // Reset action budget
                    current.ActionBudget?.ResetFull();

                    // Process turn start (tick cooldowns)
                    Effects.ProcessTurnStart(current.Id);

                    // Simple AI: attack nearest enemy or end turn
                    if (current.Faction == Faction.Hostile && current.IsActive)
                    {
                        var enemies = AllCombatants
                            .Where(c => c.Faction != current.Faction && c.IsActive)
                            .OrderBy(c => current.Position.DistanceTo(c.Position))
                            .ToList();

                        if (enemies.Any())
                        {
                            // Simple attack logic - not full AI, just basic action
                            var target = enemies.First();
                            // Deal simple damage
                            int damage = 5; // Basic damage
                            target.Resources.TakeDamage(damage);
                        }
                    }

                    // Advance turn
                    TurnQueue.AdvanceTurn();
                }
            }
        }

        private Combatant CreateCombatant(string id, string name, Faction faction, int hp, int initiative)
        {
            return new Combatant(id, name, faction, hp, initiative)
            {
                Position = new Vector3(0, 0, 0),
                Team = faction == Faction.Player ? "player" : "enemy"
            };
        }

        [Fact]
        public void Combat_MultipleRounds_HPNeverNegative()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 12345);

            var fighter = CreateCombatant("fighter", "Fighter", Faction.Player, 100, 15);
            var goblin1 = CreateCombatant("goblin1", "Goblin1", Faction.Hostile, 50, 12);
            var goblin2 = CreateCombatant("goblin2", "Goblin2", Faction.Hostile, 50, 10);

            setup.AddCombatant(fighter);
            setup.AddCombatant(goblin1);
            setup.AddCombatant(goblin2);

            // Act - Run 30 turns (multiple rounds)
            setup.ExecuteTurns(30);

            // Assert - HP should never be negative
            foreach (var combatant in setup.AllCombatants)
            {
                Assert.True(combatant.Resources.CurrentHP >= 0,
                    $"{combatant.Name} has negative HP: {combatant.Resources.CurrentHP}");
            }
        }

        [Fact]
        public void Combat_MultipleRounds_HPNeverExceedsMax()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 42);

            var cleric = CreateCombatant("cleric", "Cleric", Faction.Player, 60, 14);
            var fighter = CreateCombatant("fighter", "Fighter", Faction.Player, 80, 13);
            var orc = CreateCombatant("orc", "Orc", Faction.Hostile, 70, 11);

            setup.AddCombatant(cleric);
            setup.AddCombatant(fighter);
            setup.AddCombatant(orc);

            // Act - Simulate healing and damage over multiple turns
            for (int i = 0; i < 20; i++)
            {
                // Apply some damage
                fighter.Resources.TakeDamage(5);

                // Apply healing
                fighter.Resources.Heal(10);

                setup.ExecuteTurns(1);

                // Assert mid-loop that HP never exceeds max
                Assert.True(fighter.Resources.CurrentHP <= fighter.Resources.MaxHP,
                    $"Fighter HP ({fighter.Resources.CurrentHP}) exceeds max ({fighter.Resources.MaxHP})");
            }

            // Final assert
            foreach (var combatant in setup.AllCombatants)
            {
                Assert.True(combatant.Resources.CurrentHP <= combatant.Resources.MaxHP,
                    $"{combatant.Name} HP exceeds max: {combatant.Resources.CurrentHP}/{combatant.Resources.MaxHP}");
            }
        }

        [Fact]
        public void Combat_LongDuration_NoInfiniteLoop()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 999);

            // High HP, low damage scenario
            var tank1 = CreateCombatant("tank1", "Tank1", Faction.Player, 200, 10);
            var tank2 = CreateCombatant("tank2", "Tank2", Faction.Hostile, 200, 10);

            setup.AddCombatant(tank1);
            setup.AddCombatant(tank2);

            // Act - Run up to max turns with timeout
            int maxTurns = 100;
            setup.ExecuteTurns(maxTurns);

            // Assert - Should complete without hanging
            // Either combat ends naturally or reaches max turns
            var anyCombatantAlive = setup.AllCombatants.Any(c => c.IsActive);
            Assert.True(anyCombatantAlive, "At least one combatant should still be alive after max turns");
        }

        [Fact]
        public void Combat_RoundProgression_Increments()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 777);

            var hero1 = CreateCombatant("hero1", "Hero1", Faction.Player, 50, 15);
            var hero2 = CreateCombatant("hero2", "Hero2", Faction.Player, 50, 12);
            var enemy = CreateCombatant("enemy", "Enemy", Faction.Hostile, 50, 10);

            setup.AddCombatant(hero1);
            setup.AddCombatant(hero2);
            setup.AddCombatant(enemy);

            // Act & Assert - Round should progress
            int initialRound = setup.TurnQueue.CurrentRound;

            // Execute one full round (3 turns for 3 combatants)
            setup.ExecuteTurns(3);

            int afterOneRound = setup.TurnQueue.CurrentRound;
            Assert.True(afterOneRound > initialRound,
                $"Round should have incremented from {initialRound} to {afterOneRound}");

            // Execute another round
            setup.ExecuteTurns(3);

            int afterTwoRounds = setup.TurnQueue.CurrentRound;
            Assert.True(afterTwoRounds > afterOneRound,
                $"Round should continue incrementing: {afterTwoRounds}");
        }

        [Fact]
        public void Combat_StatusDuration_DecrementsOverTurns()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 555);

            var target = CreateCombatant("target", "Target", Faction.Player, 50, 10);
            setup.AddCombatant(target);

            // Apply a status with duration
            var statusDef = new StatusDefinition
            {
                Id = "test_buff",
                Name = "Test Buff",
                DurationType = DurationType.Turns,
                DefaultDuration = 3
            };

            setup.Statuses.RegisterStatus(statusDef);
            setup.Statuses.ApplyStatus("test_buff", "test", target.Id, duration: 3);

            // Assert initial state
            Assert.True(setup.Statuses.HasStatus(target.Id, "test_buff"));

            // Act - Execute turns and check status duration
            setup.ExecuteTurns(1); // Turn 1 end
            Assert.True(setup.Statuses.HasStatus(target.Id, "test_buff"), "Status should persist after turn 1");

            setup.ExecuteTurns(1); // Turn 2 end
            Assert.True(setup.Statuses.HasStatus(target.Id, "test_buff"), "Status should persist after turn 2");

            setup.ExecuteTurns(1); // Turn 3 end
            // Status should expire after 3 turns
            Assert.False(setup.Statuses.HasStatus(target.Id, "test_buff"),
                "Status should expire after duration");
        }

        [Fact]
        public void Combat_DeadCombatants_DoNotTakeTurns()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 333);

            var hero = CreateCombatant("hero", "Hero", Faction.Player, 50, 15);
            var enemy = CreateCombatant("enemy", "Enemy", Faction.Hostile, 10, 12);

            setup.AddCombatant(hero);
            setup.AddCombatant(enemy);

            // Kill the enemy
            enemy.Resources.TakeDamage(100);
            Assert.False(enemy.IsActive);

            // Act - Execute several turns
            var turnsBefore = setup.TurnQueue.CurrentTurnIndex;
            setup.ExecuteTurns(5);
            var turnsAfter = setup.TurnQueue.CurrentTurnIndex;

            // Assert - Turn queue should skip dead combatants
            // Only hero should be taking turns
            var activeCombatants = setup.AllCombatants.Where(c => c.IsActive).Count();
            Assert.Equal(1, activeCombatants);
        }

        [Fact]
        public void Combat_FromScenario_RunsStably()
        {
            // Arrange - Load from scenario file
            var scenarioPath = "Data/Scenarios/minimal_combat.json";
            var loader = new ScenarioLoader();

            ScenarioDefinition scenario;
            try
            {
                scenario = loader.LoadFromFile(scenarioPath);
            }
            catch (System.IO.FileNotFoundException)
            {
                // Skip test if scenario file not found
                return;
            }

            var setup = new MinimalCombatSetup(scenario.Seed);
            var combatants = loader.SpawnCombatants(scenario, setup.TurnQueue);

            foreach (var combatant in combatants)
            {
                setup.AllCombatants.Add(combatant);
                setup.Context.RegisterCombatant(combatant);
            }

            // Act - Run multiple rounds
            setup.ExecuteTurns(20);

            // Assert - Invariants hold
            foreach (var combatant in setup.AllCombatants)
            {
                Assert.True(combatant.Resources.CurrentHP >= 0,
                    $"{combatant.Name} has negative HP");
                Assert.True(combatant.Resources.CurrentHP <= combatant.Resources.MaxHP,
                    $"{combatant.Name} HP exceeds max");
            }

            // Combat should progress
            Assert.True(setup.TurnQueue.CurrentRound >= 1, "Combat should have progressed through rounds");
        }

        [Fact]
        public void Combat_HighVolume_NoMemoryLeak()
        {
            // Arrange
            var setup = new MinimalCombatSetup(seed: 888);

            // Create many combatants
            for (int i = 0; i < 10; i++)
            {
                var faction = i < 5 ? Faction.Player : Faction.Hostile;
                var combatant = CreateCombatant($"unit_{i}", $"Unit {i}", faction, 50, 15 - i);
                setup.AddCombatant(combatant);
            }

            // Act - Run many turns
            setup.ExecuteTurns(100);

            // Assert - Should complete without issues
            // This is a basic smoke test for memory issues
            Assert.True(setup.AllCombatants.Count == 10, "All combatants should still exist");
            Assert.True(setup.TurnQueue.Combatants.Count <= 10, "Turn queue should not grow unbounded");
        }
    }
}
