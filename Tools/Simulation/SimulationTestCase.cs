using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace QDND.Tools.Simulation
{
    /// <summary>
    /// A single assertion to verify after test execution.
    /// </summary>
    public class SimulationAssertion
    {
        /// <summary>
        /// Which combatant to check (null for global state like CurrentCombatantId).
        /// </summary>
        public string CombatantId { get; set; }
        
        /// <summary>
        /// Field name to check (e.g., "CurrentHP", "PositionX", "CurrentCombatantId").
        /// </summary>
        public string Field { get; set; }
        
        /// <summary>
        /// Comparison operator: "equals", "greaterThan", "lessThan", "changed", "unchanged".
        /// </summary>
        public string Operator { get; set; }
        
        /// <summary>
        /// Expected value (as string, converted on comparison).
        /// </summary>
        public string ExpectedValue { get; set; }
        
        // Factory methods
        
        /// <summary>
        /// Creates an assertion that checks if a field equals a specific value.
        /// </summary>
        public static SimulationAssertion Equals(string combatantId, string field, string value)
        {
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = field,
                Operator = "equals",
                ExpectedValue = value
            };
        }
        
        /// <summary>
        /// Creates an assertion that checks if a field has changed between snapshots.
        /// </summary>
        public static SimulationAssertion Changed(string combatantId, string field)
        {
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = field,
                Operator = "changed",
                ExpectedValue = null
            };
        }
        
        /// <summary>
        /// Creates an assertion that checks if a combatant's position equals specific coordinates.
        /// </summary>
        public static SimulationAssertion PositionEquals(string combatantId, float x, float y, float z)
        {
            // We'll check all three position components
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = "Position",
                Operator = "equals",
                ExpectedValue = $"{x},{y},{z}"
            };
        }
        
        /// <summary>
        /// Creates an assertion that checks if a combatant's HP equals a specific value.
        /// </summary>
        public static SimulationAssertion HpEquals(string combatantId, int hp)
        {
            return Equals(combatantId, "CurrentHP", hp.ToString());
        }
        
        /// <summary>
        /// Creates an assertion that checks if a combatant's HP is less than a specific value.
        /// </summary>
        public static SimulationAssertion HpLessThan(string combatantId, int hp)
        {
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = "CurrentHP",
                Operator = "lessThan",
                ExpectedValue = hp.ToString()
            };
        }
    }
    
    /// <summary>
    /// A test case that defines a sequence of commands and expected outcomes.
    /// </summary>
    public class SimulationTestCase
    {
        /// <summary>
        /// Unique name for this test case.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Human-readable description of what this test verifies.
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Optional: Specific scenario to load (if null, uses arena's default).
        /// </summary>
        public string ScenarioPath { get; set; }
        
        /// <summary>
        /// Optional: RNG seed override for deterministic testing.
        /// </summary>
        public int? Seed { get; set; }
        
        /// <summary>
        /// Sequence of commands to execute during the test.
        /// </summary>
        public List<SimulationCommand> Commands { get; set; } = new();
        
        /// <summary>
        /// Assertions to verify after executing all commands.
        /// </summary>
        public List<SimulationAssertion> Assertions { get; set; } = new();
        
        /// <summary>
        /// Serializes this test case to JSON.
        /// </summary>
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(this, options);
        }
        
        /// <summary>
        /// Deserializes a test case from JSON.
        /// </summary>
        public static SimulationTestCase FromJson(string json)
        {
            return JsonSerializer.Deserialize<SimulationTestCase>(json);
        }
        
        // Fluent builder pattern
        
        /// <summary>
        /// Adds a command to this test case and returns the test case for chaining.
        /// </summary>
        public SimulationTestCase WithCommand(SimulationCommand cmd)
        {
            Commands.Add(cmd);
            return this;
        }
        
        /// <summary>
        /// Adds an assertion to this test case and returns the test case for chaining.
        /// </summary>
        public SimulationTestCase WithAssertion(SimulationAssertion assertion)
        {
            Assertions.Add(assertion);
            return this;
        }
    }
    
    /// <summary>
    /// Result of executing a simulation test case.
    /// </summary>
    public class SimulationTestResult
    {
        /// <summary>
        /// Name of the test that was executed.
        /// </summary>
        public string TestName { get; set; }
        
        /// <summary>
        /// Whether all assertions passed and no command errors occurred.
        /// </summary>
        public bool Passed { get; set; }
        
        /// <summary>
        /// State snapshot before executing commands.
        /// </summary>
        public StateSnapshot PreSnapshot { get; set; }
        
        /// <summary>
        /// State snapshot after executing commands.
        /// </summary>
        public StateSnapshot PostSnapshot { get; set; }
        
        /// <summary>
        /// Differences between pre and post snapshots.
        /// </summary>
        public SnapshotDelta Delta { get; set; }
        
        /// <summary>
        /// List of assertions that failed (empty if all passed).
        /// </summary>
        public List<string> FailedAssertions { get; set; } = new();
        
        /// <summary>
        /// Errors encountered during command execution.
        /// </summary>
        public List<string> CommandErrors { get; set; } = new();
        
        /// <summary>
        /// Time taken to execute the test in milliseconds.
        /// </summary>
        public long ExecutionTimeMs { get; set; }
        
        /// <summary>
        /// Serializes this result to JSON for reporting.
        /// </summary>
        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(this, options);
        }
    }
}
