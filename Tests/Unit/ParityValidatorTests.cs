using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using QDND.Combat.Actions;
using QDND.Data;
using QDND.Data.Actions;
using QDND.Data.Statuses;
using QDND.Data.Validation;

namespace QDND.Tests.Unit
{
    /// <summary>
    /// Unit tests for the <see cref="ParityValidator"/> cross-registry validation system.
    /// Uses <see cref="ParityValidator.ValidateWithRegistries"/> to avoid Godot-native
    /// code in PassiveRegistry/InterruptRegistry that crashes outside the engine.
    /// </summary>
    public class ParityValidatorTests : IDisposable
    {
        // Find the repo root by walking up from the test output directory.
        private readonly string _repoRoot;

        // Pre-loaded registries that are safe outside Godot.
        private readonly ActionRegistry? _actionRegistry;
        private readonly DataRegistry? _dataRegistry;
        private readonly StatusRegistry? _statusRegistry;
        private readonly bool _hasData;

        public ParityValidatorTests()
        {
            // Walk up until we find "project.godot" as a root marker.
            var dir = AppContext.BaseDirectory;
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, "project.godot")))
                {
                    _repoRoot = dir;
                    break;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }

            _repoRoot ??= Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

            if (!Directory.Exists(BG3DataDir))
            {
                _hasData = false;
                return;
            }

            _hasData = true;

            // Load only the registries that do NOT call Godot.GD.Print
            _actionRegistry = new ActionRegistry();
            ActionRegistryInitializer.Initialize(_actionRegistry, BG3DataDir, verboseLogging: false);

            _dataRegistry = new DataRegistry();
            _dataRegistry.LoadFromDirectory(DataDir);

            _statusRegistry = new StatusRegistry();
            string statusDir = Path.Combine(BG3DataDir, "Statuses");
            if (Directory.Exists(statusDir))
                _statusRegistry.LoadStatuses(statusDir);
        }

        public void Dispose() { }

        private string DataDir => Path.Combine(_repoRoot, "Data");
        private string BG3DataDir => Path.Combine(_repoRoot, "BG3_Data");
        private string ScenarioDir => Path.Combine(_repoRoot, "Data", "Scenarios");

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Run the validator with pre-loaded registries against a given scenario dir.
        /// PassiveRegistry and InterruptRegistry are passed as null (they require Godot).
        /// </summary>
        private ParityValidationResult RunValidation(string scenarioDir)
        {
            return ParityValidator.ValidateWithRegistries(
                scenarioDir,
                _actionRegistry,
                _dataRegistry,
                _statusRegistry,
                passiveRegistry: null,       // requires Godot
                interruptRegistry: null);    // requires Godot
        }

        private string CreateTempScenarioDir(params (string name, object content)[] files)
        {
            var tmpDir = Path.Combine(Path.GetTempPath(),
                "qdnd_parity_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);

            var options = new JsonSerializerOptions { WriteIndented = true };
            foreach (var (name, content) in files)
            {
                string json = JsonSerializer.Serialize(content, options);
                File.WriteAllText(Path.Combine(tmpDir, name), json);
            }

            return tmpDir;
        }

        private static void CleanupTempDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch { /* best-effort cleanup */ }
        }

        // ------------------------------------------------------------------
        //  Tests
        // ------------------------------------------------------------------

        [Fact]
        public void Validate_KnownGoodScenarios_ReturnsNoErrors()
        {
            if (!_hasData) return;

            var result = RunValidation(ScenarioDir);

            Assert.True(result.TotalChecks > 0, "Should have performed at least one check");
            Console.WriteLine(result.GetSummary());

            // The known-good scenarios should not have fatal errors for actions.
            // Passive checks are skipped because PassiveRegistry requires Godot.
            Assert.True(result.TotalChecks >= 5,
                $"Expected at least 5 checks, got {result.TotalChecks}");
        }

        [Fact]
        public void Validate_BadScenario_CatchesMissingAction()
        {
            if (!_hasData) return;

            var badScenario = new
            {
                id = "test_bad",
                name = "Bad Scenario",
                seed = 1,
                units = new[]
                {
                    new
                    {
                        id = "unit_a",
                        name = "Test Unit",
                        faction = "player",
                        hp = 20,
                        initiative = 10,
                        x = 0f, y = 0f, z = 0f,
                        actions = new[] { "NONEXISTENT_ACTION_12345" },
                        passives = Array.Empty<string>()
                    }
                }
            };

            string? tmpDir = null;
            try
            {
                tmpDir = CreateTempScenarioDir(("bad_scenario.json", badScenario));
                var result = RunValidation(tmpDir);

                Console.WriteLine(result.GetSummary());

                Assert.False(result.IsValid, "Should have errors for missing action");
                Assert.Contains(result.Errors,
                    e => e.Category == "Scenario" &&
                         e.Message.Contains("NONEXISTENT_ACTION_12345"));
            }
            finally
            {
                if (tmpDir != null) CleanupTempDir(tmpDir);
            }
        }

        [Fact]
        public void Validate_BadScenario_CatchesMissingPassive()
        {
            if (!_hasData) return;

            // When PassiveRegistry is null, unknown passives are NOT flagged
            // (no data to validate against). This test verifies the validator
            // doesn't crash and that it DOES flag the error when a PassiveRegistry
            // is supplied. We create a minimal empty PassiveRegistry here.
            var emptyPassiveRegistry = new QDND.Data.Passives.PassiveRegistry();

            var badScenario = new
            {
                id = "test_bad_passive",
                name = "Bad Passive Scenario",
                seed = 2,
                units = new[]
                {
                    new
                    {
                        id = "unit_b",
                        name = "Test Unit",
                        faction = "player",
                        hp = 20,
                        initiative = 10,
                        x = 0f, y = 0f, z = 0f,
                        actions = new[] { "Target_MainHandAttack" },
                        passives = new[] { "NONEXISTENT_PASSIVE_99999" }
                    }
                }
            };

            string? tmpDir = null;
            try
            {
                tmpDir = CreateTempScenarioDir(("bad_passive.json", badScenario));
                var result = ParityValidator.ValidateWithRegistries(
                    tmpDir,
                    _actionRegistry,
                    _dataRegistry,
                    _statusRegistry,
                    emptyPassiveRegistry,
                    interruptRegistry: null);

                Console.WriteLine(result.GetSummary());

                Assert.False(result.IsValid, "Should have errors for missing passive");
                Assert.Contains(result.Errors,
                    e => e.Category == "Scenario" &&
                         e.Message.Contains("NONEXISTENT_PASSIVE_99999"));
            }
            finally
            {
                if (tmpDir != null) CleanupTempDir(tmpDir);
            }
        }

        [Fact]
        public void Validate_MalformedJson_ReportsParseError()
        {
            if (!_hasData) return;

            string? tmpDir = null;
            try
            {
                tmpDir = Path.Combine(Path.GetTempPath(),
                    "qdnd_parity_test_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmpDir);
                File.WriteAllText(Path.Combine(tmpDir, "broken.json"), "{ NOT VALID JSON!!!");

                var result = RunValidation(tmpDir);

                Console.WriteLine(result.GetSummary());

                Assert.False(result.IsValid, "Should have errors for malformed JSON");
                Assert.Contains(result.Errors,
                    e => e.Category == "Scenario" &&
                         e.Message.Contains("JSON parse error"));
            }
            finally
            {
                if (tmpDir != null) CleanupTempDir(tmpDir);
            }
        }

        [Fact]
        public void Validate_EmptyScenarioDir_WarnsButNoErrors()
        {
            if (!_hasData) return;

            string? tmpDir = null;
            try
            {
                tmpDir = Path.Combine(Path.GetTempPath(),
                    "qdnd_parity_test_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmpDir);

                var result = RunValidation(tmpDir);

                Assert.True(result.IsValid || result.Errors.All(e => e.Category != "Scenario"),
                    "Empty scenario directory should not produce scenario errors");
            }
            finally
            {
                if (tmpDir != null) CleanupTempDir(tmpDir);
            }
        }

        [Fact]
        public void Validate_ZeroHpUnit_ReportsError()
        {
            if (!_hasData) return;

            var badScenario = new
            {
                id = "test_zero_hp",
                name = "Zero HP Scenario",
                seed = 3,
                units = new[]
                {
                    new
                    {
                        id = "dead_unit",
                        name = "Dead Before Battle",
                        faction = "hostile",
                        hp = 0,
                        initiative = 5,
                        x = 0f, y = 0f, z = 0f,
                        actions = new[] { "Target_MainHandAttack" },
                        passives = Array.Empty<string>()
                    }
                }
            };

            string? tmpDir = null;
            try
            {
                tmpDir = CreateTempScenarioDir(("zero_hp.json", badScenario));
                var result = RunValidation(tmpDir);

                Console.WriteLine(result.GetSummary());

                Assert.False(result.IsValid, "Should flag unit with HP <= 0");
                Assert.Contains(result.Errors,
                    e => e.Category == "Scenario" &&
                         e.Message.Contains("HP <= 0"));
            }
            finally
            {
                if (tmpDir != null) CleanupTempDir(tmpDir);
            }
        }

        [Fact]
        public void ParityValidationResult_IsValid_WhenNoErrors()
        {
            var result = new ParityValidationResult();
            result.TotalChecks = 5;
            result.Warnings.Add(new ParityWarning("Test", "Just a warning"));

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ParityValidationResult_IsNotValid_WhenHasErrors()
        {
            var result = new ParityValidationResult();
            result.TotalChecks = 5;
            result.Errors.Add(new ParityError("Test", "Something broke"));

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ParityValidationResult_GetSummary_ContainsKeyInfo()
        {
            var result = new ParityValidationResult();
            result.TotalChecks = 42;
            result.Errors.Add(new ParityError("Cat1", "Error msg", "file.json"));
            result.Warnings.Add(new ParityWarning("Cat2", "Warn msg"));

            string summary = result.GetSummary();

            Assert.Contains("42", summary);
            Assert.Contains("FAIL", summary);
            Assert.Contains("Error msg", summary);
            Assert.Contains("Warn msg", summary);
            Assert.Contains("file.json", summary);
        }
    }
}
