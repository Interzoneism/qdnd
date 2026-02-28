using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using QDND.Combat.Arena;

namespace QDND.Tools.Simulation
{
    /// <summary>
    /// Runs simulation tests in headless mode.
    /// Orchestrates the Snapshot -> Act -> Verify loop.
    /// </summary>
    public class SimulationTestRunner
    {
        private List<SimulationTestCase> _testCases = new();
        private List<SimulationTestResult> _results = new();
        private bool _verboseDeltas = true;
        
        /// <summary>
        /// Set whether to print detailed delta logs for each test.
        /// </summary>
        public bool VerboseDeltas
        {
            get => _verboseDeltas;
            set => _verboseDeltas = value;
        }
        
        /// <summary>
        /// Add a test case to the runner.
        /// </summary>
        public void AddTestCase(SimulationTestCase testCase)
        {
            if (testCase == null)
            {
                throw new ArgumentNullException(nameof(testCase));
            }
            
            _testCases.Add(testCase);
        }
        
        /// <summary>
        /// Load test cases from a test manifest JSON file.
        /// </summary>
        public void LoadFromManifest(string manifestPath)
        {
            GD.Print($"[SimulationTestRunner] Loading manifest from: {manifestPath}");
            var manifest = TestManifest.LoadFromGodotPath(manifestPath);
            var tests = manifest.ToTestCases();
            GD.Print($"[SimulationTestRunner] Loaded {tests.Count} test(s) from manifest '{manifest.Name}'");
            foreach (var test in tests)
            {
                AddTestCase(test);
            }
        }
        
        /// <summary>
        /// Load all test manifests from a directory (recursively).
        /// </summary>
        public void LoadFromDirectory(string directoryPath)
        {
            string globalPath = ProjectSettings.GlobalizePath(directoryPath);
            if (!Directory.Exists(globalPath))
            {
                GD.PrintErr($"[SimulationTestRunner] Directory not found: {globalPath}");
                return;
            }
            
            var files = Directory.GetFiles(globalPath, "*.manifest.json", SearchOption.AllDirectories);
            GD.Print($"[SimulationTestRunner] Found {files.Length} manifest file(s) in {directoryPath}");
            
            foreach (var file in files)
            {
                try
                {
                    var manifest = TestManifest.LoadFromFile(file);
                    var tests = manifest.ToTestCases();
                    foreach (var test in tests)
                    {
                        AddTestCase(test);
                    }
                    GD.Print($"  - Loaded {tests.Count} test(s) from {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    GD.PrintErr($"  - Failed to load {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Create standard test cases for smoke testing.
        /// Uses combatant IDs from minimal_combat.json scenario.
        /// </summary>
        public void AddSmokeTests()
        {
            // Test 1: Move Fighter One Tile (ally_1 starts at 0,0,0 - move to 2,0,0 which is unoccupied)
            var moveTest = new SimulationTestCase
            {
                Name = "move_fighter_one_tile",
                Description = "Move the Fighter (ally_1) from (0,0,0) to (2,0,0)",
                Commands = new List<SimulationCommand>
                {
                    SimulationCommand.MoveTo("ally_1", 2f, 0f, 0f)
                },
                Assertions = new List<SimulationAssertion>
                {
                    SimulationAssertion.PositionEquals("ally_1", 2f, 0f, 0f),
                    SimulationAssertion.Changed("ally_1", "RemainingMovement")
                }
            };
            AddTestCase(moveTest);
            
            // Test 2: End Turn Changes Active Combatant
            var endTurnTest = new SimulationTestCase
            {
                Name = "end_turn_changes_combatant",
                Description = "Ending turn should advance to next combatant",
                Commands = new List<SimulationCommand>
                {
                    SimulationCommand.EndTurn()
                },
                Assertions = new List<SimulationAssertion>
                {
                    SimulationAssertion.Changed(null, "CurrentCombatantId") // null means global
                }
            };
            AddTestCase(endTurnTest);
        }
        
        /// <summary>
        /// Add the "Golden Path" test scenarios for full-spectrum combat verification.
        /// These tests validate:
        /// - Scenario A (Melee): Attack execution, HP decrease, action consumption
        /// - Scenario B (Buff): Status application, derived stat changes
        /// - Scenario C (Turn Cycle): Turn handover, action reset
        /// </summary>
        public void AddGoldenPathTests()
        {
            // Scenario A: Melee Attack
            // Fighter attacks enemy -> Enemy HP decreased -> Action consumed
            var meleeTest = new SimulationTestCase
            {
                Name = "golden_path_melee_attack",
                Description = "Fighter (ally_1) attacks Goblin (enemy_1) with main_hand_attack",
                Commands = new List<SimulationCommand>
                {
                    SimulationCommand.UseAbility("ally_1", "main_hand_attack", "enemy_1")
                },
                Assertions = new List<SimulationAssertion>
                {
                    SimulationAssertion.Changed("enemy_1", "CurrentHP"),          // Enemy took damage
                    SimulationAssertion.ActionConsumed("ally_1")                    // Fighter used action
                }
            };
            AddTestCase(meleeTest);
            
            // Scenario B: Buff Application
            // Ally uses bardic_inspiration (bonus action buff) -> Status applied -> Attack bonus changes
            var buffTest = new SimulationTestCase
            {
                Name = "golden_path_buff_status",
                Description = "Bard (ally_1) uses bardic_inspiration -> ally_1 gets 'inspired' status",
                Commands = new List<SimulationCommand>
                {
                    SimulationCommand.UseAbility("ally_1", "bardic_inspiration", "ally_1")
                },
                Assertions = new List<SimulationAssertion>
                {
                    SimulationAssertion.HasStatus("ally_1", "inspired"),           // Status applied
                    SimulationAssertion.Changed("ally_1", "AttackBonus")           // Attack bonus changed
                }
            };
            AddTestCase(buffTest);
            
            // Scenario C: Turn Cycle
            // End turns for ally_1 and wait for it to cycle back, verify action reset
            var turnCycleTest = new SimulationTestCase
            {
                Name = "golden_path_turn_cycle",
                Description = "Ending turn advances to next combatant",
                Commands = new List<SimulationCommand>
                {
                    // First use the fighter's action so we can verify reset
                    SimulationCommand.MoveTo("ally_1", 1f, 0f, 0f),  // Move a bit to consume movement
                    SimulationCommand.EndTurn()
                },
                Assertions = new List<SimulationAssertion>
                {
                    SimulationAssertion.Changed(null, "CurrentCombatantId")        // Turn changed
                }
            };
            AddTestCase(turnCycleTest);
        }
        
        /// <summary>
        /// Run all registered test cases.
        /// Requires being called from within the Godot scene tree.
        /// Returns overall pass/fail and individual results.
        /// </summary>
        /// <param name="parent">Parent node to add arena instances to (must be in scene tree).</param>
        /// <returns>Tuple of (all tests passed, list of results).</returns>
        public (bool AllPassed, List<SimulationTestResult> Results) RunAllTests(Node parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            
            _results.Clear();
            
            foreach (var testCase in _testCases)
            {
                var result = RunTest(testCase, parent);
                _results.Add(result);
            }
            
            bool allPassed = _results.All(r => r.Passed);
            return (allPassed, _results);
        }
        
        /// <summary>
        /// Run a single test case.
        /// </summary>
        /// <param name="testCase">The test case to run.</param>
        /// <param name="parent">Parent node to add the arena to (must be in scene tree).</param>
        /// <returns>Test result with snapshots and assertion status.</returns>
        public SimulationTestResult RunTest(SimulationTestCase testCase, Node parent)
        {
            if (testCase == null)
            {
                throw new ArgumentNullException(nameof(testCase));
            }
            
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }
            
            var stopwatch = Stopwatch.StartNew();
            var result = new SimulationTestResult
            {
                TestName = testCase.Name
            };
            
            CombatArena arena = null;
            
            try
            {
                // 1. Instantiate CombatArena scene
                var arenaScene = ResourceLoader.Load<PackedScene>("res://Combat/Arena/CombatArena.tscn");
                if (arenaScene == null)
                {
                    throw new InvalidOperationException("Failed to load CombatArena.tscn");
                }
                
                arena = arenaScene.Instantiate<CombatArena>();
                if (arena == null)
                {
                    throw new InvalidOperationException("Failed to instantiate CombatArena");
                }
                
                // Set scenario path if specified
                if (!string.IsNullOrEmpty(testCase.ScenarioPath))
                {
                    arena.ScenarioPath = testCase.ScenarioPath;
                }
                
                // 2. Add arena to parent (required for _Ready to be called)
                parent.AddChild(arena);
                
                // 3. Wait for arena to initialize
                // In Godot 4, _Ready is called immediately after AddChild in the same frame
                // But we need to process at least one frame to ensure everything is initialized
                // Since we're in synchronous mode, the arena's _Ready should have been called
                
                // Force arena to be in a valid state by checking Context
                if (arena.Context == null)
                {
                    throw new InvalidOperationException("Arena failed to initialize - Context is null");
                }
                
                // 4. Take pre-snapshot
                result.PreSnapshot = StateSnapshot.Capture(arena);
                
                // 5. Create injector and execute commands
                var injector = new SimulationCommandInjector(arena);
                
                foreach (var command in testCase.Commands)
                {
                    var (success, error) = injector.Execute(command);
                    
                    if (!success)
                    {
                        result.CommandErrors.Add($"Command {command.Type} failed: {error}");
                    }
                }
                
                // 6. Take post-snapshot
                result.PostSnapshot = StateSnapshot.Capture(arena);
                
                // 7. Compute delta
                result.Delta = SnapshotDelta.Compare(result.PreSnapshot, result.PostSnapshot);
                
                // 8. Verify assertions
                result.FailedAssertions = VerifyAssertions(
                    testCase.Assertions, 
                    result.PreSnapshot, 
                    result.PostSnapshot
                );
                
                // 9. Determine pass/fail
                result.Passed = result.CommandErrors.Count == 0 && result.FailedAssertions.Count == 0;
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.CommandErrors.Add($"Exception during test execution: {ex.Message}");
                GD.PushError($"[SimulationTestRunner] Test '{testCase.Name}' threw exception: {ex}");
            }
            finally
            {
                // 10. Cleanup: Remove and free arena
                if (arena != null)
                {
                    arena.QueueFree();
                }
                
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            }
            
            return result;
        }
        
        /// <summary>
        /// Verify assertions against pre/post snapshots.
        /// Returns a list of failure messages (empty if all passed).
        /// </summary>
        private List<string> VerifyAssertions(
            List<SimulationAssertion> assertions, 
            StateSnapshot pre, 
            StateSnapshot post)
        {
            var failures = new List<string>();
            
            if (assertions == null || assertions.Count == 0)
            {
                return failures;
            }
            
            if (pre == null || post == null)
            {
                failures.Add("Cannot verify assertions: snapshots are null");
                return failures;
            }
            
            foreach (var assertion in assertions)
            {
                string failure = VerifyAssertion(assertion, pre, post);
                if (failure != null)
                {
                    failures.Add(failure);
                }
            }
            
            return failures;
        }
        
        /// <summary>
        /// Verify a single assertion.
        /// Returns null if passed, or error message if failed.
        /// </summary>
        private string VerifyAssertion(SimulationAssertion assertion, StateSnapshot pre, StateSnapshot post)
        {
            try
            {
                // Handle global state (null combatantId)
                if (assertion.CombatantId == null)
                {
                    return VerifyGlobalAssertion(assertion, pre, post);
                }
                
                // Find combatant in post-snapshot
                var combatant = post.Combatants?.FirstOrDefault(c => c.Id == assertion.CombatantId);
                if (combatant == null)
                {
                    return $"Combatant '{assertion.CombatantId}' not found in post-snapshot";
                }
                
                // Special handling for Position field (checks X, Y, Z)
                if (assertion.Field == "Position")
                {
                    return VerifyPositionAssertion(assertion, combatant);
                }
                
                // Get field value from combatant snapshot
                string actualValue = GetCombatantFieldValue(combatant, assertion.Field);
                
                // Handle "changed" and "unchanged" operators
                if (assertion.Operator == "changed" || assertion.Operator == "unchanged")
                {
                    var preCombatant = pre.Combatants?.FirstOrDefault(c => c.Id == assertion.CombatantId);
                    if (preCombatant == null)
                    {
                        return $"Combatant '{assertion.CombatantId}' not found in pre-snapshot";
                    }
                    
                    string preValue = GetCombatantFieldValue(preCombatant, assertion.Field);
                    bool hasChanged = preValue != actualValue;
                    
                    if (assertion.Operator == "changed" && !hasChanged)
                    {
                        return $"{assertion.CombatantId}.{assertion.Field} did not change (value: {actualValue})";
                    }
                    
                    if (assertion.Operator == "unchanged" && hasChanged)
                    {
                        return $"{assertion.CombatantId}.{assertion.Field} changed from {preValue} to {actualValue}";
                    }
                    
                    return null; // Passed
                }
                
                // Handle comparison operators
                return VerifyComparison(assertion, actualValue);
            }
            catch (Exception ex)
            {
                return $"Error verifying assertion for {assertion.CombatantId}.{assertion.Field}: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Verify assertion against global state fields.
        /// </summary>
        private string VerifyGlobalAssertion(SimulationAssertion assertion, StateSnapshot pre, StateSnapshot post)
        {
            string actualValue = GetGlobalFieldValue(post, assertion.Field);
            
            if (assertion.Operator == "changed" || assertion.Operator == "unchanged")
            {
                string preValue = GetGlobalFieldValue(pre, assertion.Field);
                bool hasChanged = preValue != actualValue;
                
                if (assertion.Operator == "changed" && !hasChanged)
                {
                    return $"Global.{assertion.Field} did not change (value: {actualValue})";
                }
                
                if (assertion.Operator == "unchanged" && hasChanged)
                {
                    return $"Global.{assertion.Field} changed from {preValue} to {actualValue}";
                }
                
                return null;
            }
            
            return VerifyComparison(assertion, actualValue);
        }
        
        /// <summary>
        /// Verify position assertion (checks all three coordinates).
        /// </summary>
        private string VerifyPositionAssertion(SimulationAssertion assertion, CombatantSnapshot combatant)
        {
            if (assertion.Operator != "equals")
            {
                return $"Position assertions only support 'equals' operator, got '{assertion.Operator}'";
            }
            
            // Parse expected position from "x,y,z" format
            var parts = assertion.ExpectedValue?.Split(',');
            if (parts == null || parts.Length != 3)
            {
                return $"Invalid position format: '{assertion.ExpectedValue}' (expected 'x,y,z')";
            }
            
            if (!float.TryParse(parts[0], out float expectedX) ||
                !float.TryParse(parts[1], out float expectedY) ||
                !float.TryParse(parts[2], out float expectedZ))
            {
                return $"Failed to parse position values: '{assertion.ExpectedValue}'";
            }
            
            // Compare with tolerance for floating point
            const float tolerance = 0.001f;
            
            if (Math.Abs(combatant.PositionX - expectedX) > tolerance ||
                Math.Abs(combatant.PositionY - expectedY) > tolerance ||
                Math.Abs(combatant.PositionZ - expectedZ) > tolerance)
            {
                return $"{assertion.CombatantId}.Position: expected ({expectedX},{expectedY},{expectedZ}), " +
                       $"got ({combatant.PositionX},{combatant.PositionY},{combatant.PositionZ})";
            }
            
            return null; // Passed
        }
        
        /// <summary>
        /// Verify comparison operators (equals, greaterThan, lessThan, contains, notContains).
        /// </summary>
        private string VerifyComparison(SimulationAssertion assertion, string actualValue)
        {
            switch (assertion.Operator)
            {
                case "equals":
                    // Case-insensitive comparison for boolean values
                    if (string.Equals(actualValue, assertion.ExpectedValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return null; // Passed
                    }
                    return $"{assertion.CombatantId}.{assertion.Field}: expected '{assertion.ExpectedValue}', got '{actualValue}'";
                    
                case "greaterThan":
                    if (!TryParseNumeric(actualValue, out double actual) || 
                        !TryParseNumeric(assertion.ExpectedValue, out double expectedGt))
                    {
                        return $"{assertion.CombatantId}.{assertion.Field}: cannot compare non-numeric values with greaterThan";
                    }
                    if (actual <= expectedGt)
                    {
                        return $"{assertion.CombatantId}.{assertion.Field}: expected > {expectedGt}, got {actual}";
                    }
                    break;
                    
                case "lessThan":
                    if (!TryParseNumeric(actualValue, out double actualLt) || 
                        !TryParseNumeric(assertion.ExpectedValue, out double expectedLt))
                    {
                        return $"{assertion.CombatantId}.{assertion.Field}: cannot compare non-numeric values with lessThan";
                    }
                    if (actualLt >= expectedLt)
                    {
                        return $"{assertion.CombatantId}.{assertion.Field}: expected < {expectedLt}, got {actualLt}";
                    }
                    break;
                    
                case "contains":
                    // Check if the value (typically a list or string) contains the expected item
                    if (actualValue == null || !actualValue.Contains(assertion.ExpectedValue ?? ""))
                    {
                        return $"{assertion.CombatantId}.{assertion.Field}: expected to contain '{assertion.ExpectedValue}', got '{actualValue}'";
                    }
                    break;
                    
                case "notContains":
                    // Check if the value does NOT contain the expected item
                    if (actualValue != null && actualValue.Contains(assertion.ExpectedValue ?? ""))
                    {
                        return $"{assertion.CombatantId}.{assertion.Field}: expected to NOT contain '{assertion.ExpectedValue}', but found it in '{actualValue}'";
                    }
                    break;
                    
                default:
                    return $"Unknown operator: {assertion.Operator}";
            }
            
            return null; // Passed
        }
        
        /// <summary>
        /// Get field value from a combatant snapshot.
        /// </summary>
        private string GetCombatantFieldValue(CombatantSnapshot combatant, string fieldName)
        {
            return fieldName switch
            {
                "Id" => combatant.Id,
                "Name" => combatant.Name,
                "PositionX" => combatant.PositionX.ToString(),
                "PositionY" => combatant.PositionY.ToString(),
                "PositionZ" => combatant.PositionZ.ToString(),
                "CurrentHP" => combatant.CurrentHP.ToString(),
                "MaxHP" => combatant.MaxHP.ToString(),
                "TempHP" => combatant.TempHP.ToString(),
                "Faction" => combatant.Faction,
                "IsActive" => combatant.IsActive.ToString(),
                "RemainingMovement" => combatant.RemainingMovement.ToString(),
                "MaxMovement" => combatant.MaxMovement.ToString(),
                "HasAction" => combatant.HasAction.ToString(),
                "HasBonusAction" => combatant.HasBonusAction.ToString(),
                "HasReaction" => combatant.HasReaction.ToString(),
                "ActiveStatuses" => string.Join(",", combatant.ActiveStatuses ?? new List<string>()),
                // Derived stats
                "EffectiveAC" => combatant.DerivedStats?.EffectiveAC.ToString() ?? "0",
                "AttackBonus" => combatant.DerivedStats?.AttackBonus.ToString() ?? "0",
                "DamageBonus" => combatant.DerivedStats?.DamageBonus.ToString() ?? "0",
                "SaveBonus" => combatant.DerivedStats?.SaveBonus.ToString() ?? "0",
                "HasAdvantageOnAttacks" => combatant.DerivedStats?.HasAdvantageOnAttacks.ToString() ?? "False",
                "HasDisadvantageOnAttacks" => combatant.DerivedStats?.HasDisadvantageOnAttacks.ToString() ?? "False",
                "HasAdvantageOnSaves" => combatant.DerivedStats?.HasAdvantageOnSaves.ToString() ?? "False",
                "HasDisadvantageOnSaves" => combatant.DerivedStats?.HasDisadvantageOnSaves.ToString() ?? "False",
                _ => throw new ArgumentException($"Unknown combatant field: {fieldName}")
            };
        }
        
        /// <summary>
        /// Get field value from global snapshot state.
        /// </summary>
        private string GetGlobalFieldValue(StateSnapshot snapshot, string fieldName)
        {
            return fieldName switch
            {
                "CombatState" => snapshot.CombatState,
                "CurrentCombatantId" => snapshot.CurrentCombatantId,
                "CurrentRound" => snapshot.CurrentRound.ToString(),
                _ => throw new ArgumentException($"Unknown global field: {fieldName}")
            };
        }
        
        /// <summary>
        /// Try to parse a string as a numeric value.
        /// </summary>
        private bool TryParseNumeric(string value, out double result)
        {
            return double.TryParse(value, out result);
        }
        
        /// <summary>
        /// Print results to stdout in machine-readable JSON format.
        /// </summary>
        public void PrintResults()
        {
            var summary = new
            {
                total = _results.Count,
                passed = _results.Count(r => r.Passed),
                failed = _results.Count(r => !r.Passed),
                executionTimeMs = _results.Sum(r => r.ExecutionTimeMs)
            };
            
            // Build test results with optional delta info
            var testsOutput = new List<object>();
            foreach (var r in _results)
            {
                var testOutput = new Dictionary<string, object>
                {
                    ["name"] = r.TestName,
                    ["passed"] = r.Passed,
                    ["executionTimeMs"] = r.ExecutionTimeMs
                };
                
                if (r.FailedAssertions.Count > 0)
                {
                    testOutput["failedAssertions"] = r.FailedAssertions.ToArray();
                }
                
                if (r.CommandErrors.Count > 0)
                {
                    testOutput["commandErrors"] = r.CommandErrors.ToArray();
                }
                
                // Add delta info if verbose mode is enabled
                if (_verboseDeltas && r.Delta?.Changes != null && r.Delta.Changes.Count > 0)
                {
                    testOutput["deltas"] = r.Delta.Changes.Select(c => new
                    {
                        combatantId = c.CombatantId ?? "global",
                        field = c.FieldName,
                        oldValue = c.OldValue,
                        newValue = c.NewValue
                    }).ToArray();
                }
                
                testsOutput.Add(testOutput);
            }
            
            var output = new
            {
                summary,
                tests = testsOutput
            };
            
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            string json = JsonSerializer.Serialize(output, options);
            GD.Print(json);
        }
        
        /// <summary>
        /// Print a human-readable summary of the test results.
        /// </summary>
        public void PrintHumanReadableSummary()
        {
            GD.Print("");
            GD.Print("╔═══════════════════════════════════════════════════╗");
            GD.Print("║            SIMULATION TEST RESULTS                ║");
            GD.Print("╚═══════════════════════════════════════════════════╝");
            GD.Print("");
            
            int passCount = _results.Count(r => r.Passed);
            int failCount = _results.Count(r => !r.Passed);
            long totalTimeMs = _results.Sum(r => r.ExecutionTimeMs);
            
            foreach (var result in _results)
            {
                string status = result.Passed ? "✓ PASS" : "✗ FAIL";
                GD.Print($"  [{status}] {result.TestName} ({result.ExecutionTimeMs}ms)");
                
                // Print delta changes if verbose
                if (_verboseDeltas && result.Delta?.Changes?.Count > 0)
                {
                    GD.Print("    Deltas:");
                    foreach (var change in result.Delta.Changes.Take(10))
                    {
                        string scope = string.IsNullOrEmpty(change.CombatantId) ? "global" : change.CombatantId;
                        GD.Print($"      {scope}.{change.FieldName}: {change.OldValue} -> {change.NewValue}");
                    }
                    if (result.Delta.Changes.Count > 10)
                    {
                        GD.Print($"      ... and {result.Delta.Changes.Count - 10} more changes");
                    }
                }
                
                // Print failures
                if (!result.Passed)
                {
                    foreach (var error in result.CommandErrors)
                    {
                        GD.Print($"      ! Command Error: {error}");
                    }
                    foreach (var assertion in result.FailedAssertions)
                    {
                        GD.Print($"      ! Assertion Failed: {assertion}");
                    }
                }
            }
            
            GD.Print("");
            GD.Print("───────────────────────────────────────────────────");
            GD.Print($"  Total: {_results.Count} | Passed: {passCount} | Failed: {failCount} | Time: {totalTimeMs}ms");
            GD.Print("───────────────────────────────────────────────────");
            GD.Print("");
        }
    }
}
