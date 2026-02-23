using Godot;
using System;
using System.Collections.Generic;
using QDND.Combat.Entities;
using QDND.Combat.Services;
using QDND.Combat.States;
using QDND.Combat.Rules;
using QDND.Data;

namespace QDND.Tools
{
    /// <summary>
    /// Headless test runner for Godot. Runs deterministic validation tests
    /// that don't require rendering.
    /// 
    /// This complements the dotnet test suite by validating that:
    /// - Core services initialize correctly
    /// - Combat scenarios load without errors
    /// - Deterministic simulation produces expected hashes
    /// - Data registry validation passes
    /// </summary>
    public class HeadlessTestRunner
    {
        private List<string> _failures = new();
        private int _passed = 0;
        private int _failed = 0;
        private int _skipped = 0;

        public TestResult RunAllTests()
        {
            GD.Print("[HeadlessTestRunner] Starting test suite...");

            // Core initialization tests
            RunTest("DataRegistry_LoadsSuccessfully", TestDataRegistryLoads);
            RunTest("DataRegistry_ValidationPasses", TestDataRegistryValidation);
            RunTest("CombatContext_Initializes", TestCombatContextInitializes);
            RunTest("TurnQueue_InitialState", TestTurnQueueInitialState);
            RunTest("CombatStateMachine_InitialState", TestStateMachineInitialState);
            RunTest("RulesEngine_Initializes", TestRulesEngineInitializes);
            
            // Combatant tests
            RunTest("Combatant_Creation", TestCombatantCreation);
            RunTest("Combatant_StateHash", TestCombatantStateHash);
            
            // Scenario loading tests
            RunTest("ScenarioLoader_LoadsMinimalCombat", TestScenarioLoaderMinimal);
            
            // Deterministic simulation tests
            RunTest("DeterministicRNG_ProducesConsistentResults", TestDeterministicRNG);
            RunTest("TurnOrder_IsDeterministic", TestTurnOrderDeterministic);

            return new TestResult
            {
                Total = _passed + _failed + _skipped,
                Passed = _passed,
                Failed = _failed,
                Skipped = _skipped,
                Failures = _failures
            };
        }

        private void RunTest(string name, Action test)
        {
            try
            {
                GD.Print($"  Running: {name}...");
                test();
                _passed++;
                GD.Print($"    PASS");
            }
            catch (SkipTestException ex)
            {
                _skipped++;
                GD.Print($"    SKIP: {ex.Message}");
            }
            catch (Exception ex)
            {
                _failed++;
                string failure = $"{name}: {ex.Message}";
                _failures.Add(failure);
                GD.PrintErr($"    FAIL: {ex.Message}");
            }
        }

        // === TEST METHODS ===

        private void TestDataRegistryLoads()
        {
            var registry = new DataRegistry();
            string dataPath = ProjectSettings.GlobalizePath("res://Data");
            registry.LoadFromDirectory(dataPath);
            
            Assert(registry.GetAllStatuses().Count > 0, "Expected statuses to be loaded");
        }

        private void TestDataRegistryValidation()
        {
            var registry = new DataRegistry();
            string dataPath = ProjectSettings.GlobalizePath("res://Data");
            registry.LoadFromDirectory(dataPath);
            
            // Should not throw
            registry.ValidateOrThrow();
        }

        private void TestCombatContextInitializes()
        {
            var context = new CombatContext();
            Assert(context != null, "CombatContext should initialize");
            Assert(context.GetRegisteredServices().Count == 0, "New context should have no services");
        }

        private void TestTurnQueueInitialState()
        {
            var queue = new TurnQueueService();
            Assert(queue.Combatants.Count == 0, "Initial queue should be empty");
            Assert(queue.CurrentCombatant == null, "No current combatant initially");
            Assert(queue.CurrentRound == 0, "Initial round should be 0");
        }

        private void TestStateMachineInitialState()
        {
            var sm = new CombatStateMachine();
            Assert(sm.CurrentState == CombatState.NotInCombat, "Initial state should be NotInCombat");
        }

        private void TestRulesEngineInitializes()
        {
            var engine = new RulesEngine(42);
            Assert(engine != null, "RulesEngine should initialize");
            Assert(engine.Events != null, "RulesEngine.Events should be available");
        }

        private void TestCombatantCreation()
        {
            var combatant = new Combatant("test_id", "Test Fighter", Faction.Player, 50, 15);
            Assert(combatant.Id == "test_id", "ID should match");
            Assert(combatant.Name == "Test Fighter", "Name should match");
            Assert(combatant.Faction == Faction.Player, "Faction should match");
            Assert(combatant.Resources.CurrentHP == 50, "HP should match");
            Assert(combatant.Resources.MaxHP == 50, "MaxHP should match");
            Assert(combatant.Initiative == 15, "Initiative should match");
            Assert(combatant.IsActive, "Combatant should be active");
        }

        private void TestCombatantStateHash()
        {
            var c1 = new Combatant("test", "Fighter", Faction.Player, 50, 15);
            var c2 = new Combatant("test", "Fighter", Faction.Player, 50, 15);
            
            int hash1 = c1.GetStateHash();
            int hash2 = c2.GetStateHash();
            
            Assert(hash1 != 0, "State hash should not be zero");
            Assert(hash1 == hash2, "Identical combatants should have same hash");
            
            // Modify one and verify hash changes
            c2.Resources.TakeDamage(10);
            int hash2Modified = c2.GetStateHash();
            Assert(hash1 != hash2Modified, "Modified combatant should have different hash");
        }

        private void TestScenarioLoaderMinimal()
        {
            var loader = new ScenarioLoader();
            string scenarioPath = ProjectSettings.GlobalizePath("res://Data/Scenarios/minimal_combat.json");
            
            if (!FileAccess.FileExists(scenarioPath))
            {
                throw new SkipTestException("minimal_combat.json not found");
            }

            var scenario = loader.LoadFromFile(scenarioPath);
            Assert(scenario != null, "Scenario should load");
            Assert(scenario.Name != null, "Scenario should have name");
        }

        private void TestDeterministicRNG()
        {
            var rng1 = new Random(42);
            var rng2 = new Random(42);

            for (int i = 0; i < 100; i++)
            {
                int v1 = rng1.Next(1, 21); // d20
                int v2 = rng2.Next(1, 21);
                Assert(v1 == v2, $"RNG should be deterministic: {v1} != {v2} at iteration {i}");
            }
        }

        private void TestTurnOrderDeterministic()
        {
            var queue1 = new TurnQueueService();
            var queue2 = new TurnQueueService();

            // Add same combatants in same order
            var combatants = new[]
            {
                new Combatant("c1", "Fighter", Faction.Player, 50, 15),
                new Combatant("c2", "Mage", Faction.Player, 30, 12),
                new Combatant("c3", "Goblin", Faction.Hostile, 20, 15), // Same initiative as Fighter
                new Combatant("c4", "Orc", Faction.Hostile, 40, 10)
            };

            foreach (var c in combatants)
            {
                queue1.AddCombatant(c);
            }

            // Create identical combatants for queue2
            var combatants2 = new[]
            {
                new Combatant("c1", "Fighter", Faction.Player, 50, 15),
                new Combatant("c2", "Mage", Faction.Player, 30, 12),
                new Combatant("c3", "Goblin", Faction.Hostile, 20, 15),
                new Combatant("c4", "Orc", Faction.Hostile, 40, 10)
            };

            foreach (var c in combatants2)
            {
                queue2.AddCombatant(c);
            }

            queue1.StartCombat();
            queue2.StartCombat();

            // Verify same turn order
            for (int i = 0; i < 4; i++)
            {
                string id1 = queue1.CurrentCombatant?.Id;
                string id2 = queue2.CurrentCombatant?.Id;
                Assert(id1 == id2, $"Turn {i}: Expected {id1} == {id2}");
                queue1.AdvanceTurn();
                queue2.AdvanceTurn();
            }
        }

        // === HELPERS ===

        private void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new AssertionException(message);
            }
        }
    }

    public class TestResult
    {
        public int Total { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public List<string> Failures { get; set; } = new();
    }

    public class AssertionException : Exception
    {
        public AssertionException(string message) : base(message) { }
    }

    public class SkipTestException : Exception
    {
        public SkipTestException(string message) : base(message) { }
    }
}
