using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace QDND.Tools.Simulation
{
    /// <summary>
    /// JSON-serializable command representation.
    /// </summary>
    public class JsonCommand
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
        
        [JsonPropertyName("actorId")]
        public string ActorId { get; set; }
        
        [JsonPropertyName("actionId")]
        public string ActionId { get; set; }
        
        [JsonPropertyName("targetId")]
        public string TargetId { get; set; }
        
        [JsonPropertyName("position")]
        public float[] Position { get; set; }
        
        [JsonPropertyName("waitSeconds")]
        public float WaitSeconds { get; set; }
        
        /// <summary>
        /// Convert this JSON representation to a SimulationCommand.
        /// </summary>
        public SimulationCommand ToCommand()
        {
            var pos = Position != null && Position.Length >= 3 
                ? new Vector3(Position[0], Position[1], Position[2]) 
                : Vector3.Zero;
            
            return Type?.ToLowerInvariant() switch
            {
                "moveto" => SimulationCommand.MoveTo(ActorId, pos),
                "useability" => SimulationCommand.UseAbility(ActorId, ActionId, TargetId),
                "useabilityatposition" => SimulationCommand.UseAbilityAtPosition(ActorId, ActionId, pos),
                "endturn" => string.IsNullOrEmpty(ActorId) ? SimulationCommand.EndTurn() : SimulationCommand.EndTurn(ActorId),
                "wait" => SimulationCommand.Wait(WaitSeconds),
                "select" => SimulationCommand.Select(ActorId),
                "selectability" => SimulationCommand.SelectAction(ActionId),
                "clearselection" => SimulationCommand.ClearSelection(),
                _ => throw new ArgumentException($"Unknown command type: {Type}")
            };
        }
        
        /// <summary>
        /// Create from a SimulationCommand for serialization.
        /// </summary>
        public static JsonCommand FromCommand(SimulationCommand cmd)
        {
            return new JsonCommand
            {
                Type = cmd.Type.ToString(),
                ActorId = cmd.ActorId,
                ActionId = cmd.ActionId,
                TargetId = cmd.TargetId,
                Position = new[] { cmd.TargetPosition.X, cmd.TargetPosition.Y, cmd.TargetPosition.Z },
                WaitSeconds = cmd.WaitSeconds
            };
        }
    }
    
    /// <summary>
    /// JSON-serializable test case representation.
    /// </summary>
    public class JsonTestCase
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("scenarioPath")]
        public string ScenarioPath { get; set; }
        
        [JsonPropertyName("seed")]
        public int? Seed { get; set; }
        
        [JsonPropertyName("commands")]
        public List<JsonCommand> Commands { get; set; } = new();
        
        [JsonPropertyName("assertions")]
        public List<SimulationAssertion> Assertions { get; set; } = new();
        
        /// <summary>
        /// Convert to a SimulationTestCase.
        /// </summary>
        public SimulationTestCase ToTestCase()
        {
            return new SimulationTestCase
            {
                Name = Name,
                Description = Description,
                ScenarioPath = ScenarioPath,
                Seed = Seed,
                Commands = Commands?.Select(c => c.ToCommand()).ToList() ?? new List<SimulationCommand>(),
                Assertions = Assertions ?? new List<SimulationAssertion>()
            };
        }
        
        /// <summary>
        /// Create from a SimulationTestCase for serialization.
        /// </summary>
        public static JsonTestCase FromTestCase(SimulationTestCase tc)
        {
            return new JsonTestCase
            {
                Name = tc.Name,
                Description = tc.Description,
                ScenarioPath = tc.ScenarioPath,
                Seed = tc.Seed,
                Commands = tc.Commands?.Select(JsonCommand.FromCommand).ToList() ?? new List<JsonCommand>(),
                Assertions = tc.Assertions ?? new List<SimulationAssertion>()
            };
        }
    }
    
    /// <summary>
    /// Test manifest containing multiple test cases.
    /// </summary>
    public class TestManifest
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("defaultScenarioPath")]
        public string DefaultScenarioPath { get; set; }
        
        [JsonPropertyName("tests")]
        public List<JsonTestCase> Tests { get; set; } = new();
        
        /// <summary>
        /// Load a test manifest from a JSON file.
        /// </summary>
        public static TestManifest LoadFromFile(string path)
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TestManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        
        /// <summary>
        /// Load a test manifest from Godot resource path.
        /// </summary>
        public static TestManifest LoadFromGodotPath(string resPath)
        {
            string globalPath = ProjectSettings.GlobalizePath(resPath);
            return LoadFromFile(globalPath);
        }
        
        /// <summary>
        /// Convert all tests to SimulationTestCase instances.
        /// </summary>
        public List<SimulationTestCase> ToTestCases()
        {
            return Tests.Select(t =>
            {
                var tc = t.ToTestCase();
                // Apply default scenario if not specified
                if (string.IsNullOrEmpty(tc.ScenarioPath) && !string.IsNullOrEmpty(DefaultScenarioPath))
                {
                    tc.ScenarioPath = DefaultScenarioPath;
                }
                return tc;
            }).ToList();
        }
        
        /// <summary>
        /// Save this manifest to a JSON file.
        /// </summary>
        public void SaveToFile(string path)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(path, json);
        }
    }
    
    /// <summary>
    /// A single assertion to verify after test execution.
    /// </summary>
    public class SimulationAssertion
    {
        /// <summary>
        /// Which combatant to check (null for global state like CurrentCombatantId).
        /// </summary>
        [JsonPropertyName("combatantId")]
        public string CombatantId { get; set; }
        
        /// <summary>
        /// Field name to check (e.g., "CurrentHP", "PositionX", "CurrentCombatantId").
        /// </summary>
        [JsonPropertyName("field")]
        public string Field { get; set; }
        
        /// <summary>
        /// Comparison operator: "equals", "greaterThan", "lessThan", "changed", "unchanged", "contains", "notContains".
        /// </summary>
        [JsonPropertyName("operator")]
        public string Operator { get; set; }
        
        /// <summary>
        /// Expected value (as string, converted on comparison).
        /// </summary>
        [JsonPropertyName("expectedValue")]
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
        /// Creates an assertion that checks if a field has NOT changed between snapshots.
        /// </summary>
        public static SimulationAssertion Unchanged(string combatantId, string field)
        {
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = field,
                Operator = "unchanged",
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
        
        /// <summary>
        /// Creates an assertion that checks if a combatant has a specific status effect.
        /// </summary>
        public static SimulationAssertion HasStatus(string combatantId, string statusId)
        {
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = "ActiveStatuses",
                Operator = "contains",
                ExpectedValue = statusId
            };
        }
        
        /// <summary>
        /// Creates an assertion that checks if a combatant does not have a specific status effect.
        /// </summary>
        public static SimulationAssertion DoesNotHaveStatus(string combatantId, string statusId)
        {
            return new SimulationAssertion
            {
                CombatantId = combatantId,
                Field = "ActiveStatuses",
                Operator = "notContains",
                ExpectedValue = statusId
            };
        }
        
        /// <summary>
        /// Creates an assertion that checks if action was consumed (HasAction is false).
        /// </summary>
        public static SimulationAssertion ActionConsumed(string combatantId)
        {
            return Equals(combatantId, "HasAction", "False");
        }
        
        /// <summary>
        /// Creates an assertion that checks if action is available (HasAction is true).
        /// </summary>
        public static SimulationAssertion HasActionAvailable(string combatantId)
        {
            return Equals(combatantId, "HasAction", "True");
        }
        
        /// <summary>
        /// Creates an assertion that checks if attack bonus changed.
        /// </summary>
        public static SimulationAssertion AttackBonusChanged(string combatantId)
        {
            return Changed(combatantId, "AttackBonus");
        }
        
        /// <summary>
        /// Creates an assertion that checks attack bonus equals a value.
        /// </summary>
        public static SimulationAssertion AttackBonusEquals(string combatantId, int bonus)
        {
            return Equals(combatantId, "AttackBonus", bonus.ToString());
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
            var jsonCase = JsonTestCase.FromTestCase(this);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            return JsonSerializer.Serialize(jsonCase, options);
        }
        
        /// <summary>
        /// Deserializes a test case from JSON.
        /// </summary>
        public static SimulationTestCase FromJson(string json)
        {
            var jsonCase = JsonSerializer.Deserialize<JsonTestCase>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return jsonCase?.ToTestCase();
        }
        
        /// <summary>
        /// Load a test case from a JSON file.
        /// </summary>
        public static SimulationTestCase LoadFromFile(string path)
        {
            string json = File.ReadAllText(path);
            return FromJson(json);
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
        [JsonIgnore]
        public StateSnapshot PreSnapshot { get; set; }
        
        /// <summary>
        /// State snapshot after executing commands.
        /// </summary>
        [JsonIgnore]
        public StateSnapshot PostSnapshot { get; set; }
        
        /// <summary>
        /// Differences between pre and post snapshots.
        /// </summary>
        [JsonIgnore]
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
