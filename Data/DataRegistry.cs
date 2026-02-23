using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using QDND.Combat.Actions;
using QDND.Combat.Statuses;
using QDND.Data.Passives;

namespace QDND.Data
{
    /// <summary>
    /// Parity allowlist - known acceptable gaps in BG3 parity.
    /// </summary>
    internal class ParityAllowlist
    {
        public HashSet<string> AllowMissingStatusIds { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> AllowMissingGrantedAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Allowlist file format.
    /// </summary>
    internal class ParityAllowlistFile
    {
        public List<string> AllowMissingStatusIds { get; set; }
        public List<string> AllowMissingGrantedAbilities { get; set; }
    }
    /// <summary>
    /// Validation severity levels.
    /// </summary>
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// A single validation error/warning.
    /// </summary>
    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Category { get; set; }
        public string ItemId { get; set; }
        public string Message { get; set; }
        public string FilePath { get; set; }

        public override string ToString()
        {
            string prefix = Severity switch
            {
                ValidationSeverity.Error => "[ERROR]",
                ValidationSeverity.Warning => "[WARN]",
                _ => "[INFO]"
            };
            string location = string.IsNullOrEmpty(FilePath) ? "" : $" ({FilePath})";
            return $"{prefix} [{Category}] {ItemId}{location}: {Message}";
        }
    }

    /// <summary>
    /// Result of a validation run.
    /// </summary>
    public class ValidationResult
    {
        public List<ValidationIssue> Issues { get; } = new();
        public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error);
        public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);
        public int ErrorCount => Issues.Count(i => i.Severity == ValidationSeverity.Error);
        public int WarningCount => Issues.Count(i => i.Severity == ValidationSeverity.Warning);

        public void AddError(string category, string itemId, string message)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Error,
                Category = category,
                ItemId = itemId,
                Message = message
            });
        }

        public void AddWarning(string category, string itemId, string message)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Category = category,
                ItemId = itemId,
                Message = message
            });
        }

        public void AddInfo(string category, string itemId, string message)
        {
            Issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Info,
                Category = category,
                ItemId = itemId,
                Message = message
            });
        }

        public void PrintSummary()
        {
            if (Issues.Count == 0)
            {
                Console.WriteLine("[Registry] Validation passed with no issues");
                return;
            }

            foreach (var issue in Issues)
            {
                Console.WriteLine(issue.ToString());
            }

            Console.WriteLine($"[Registry] Validation complete: {ErrorCount} errors, {WarningCount} warnings");
        }
    }

    /// <summary>
    /// Central data registry for game content.
    /// Handles loading, validation, and access to abilities, statuses, and scenarios.
    /// </summary>
    public class DataRegistry
    {
        private readonly Dictionary<string, StatusDefinition> _statuses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScenarioDefinition> _scenarios = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CharacterModel.BeastForm> _beastForms = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> _loadedFiles = new();

        /// <summary>
        /// Registry for passive abilities (loaded from BG3_Data).
        /// </summary>
        public PassiveRegistry PassiveRegistry { get; } = new PassiveRegistry();

        // --- Registration ---

        public void RegisterStatus(StatusDefinition status)
        {
            if (status == null) throw new ArgumentNullException(nameof(status));
            if (string.IsNullOrEmpty(status.Id))
                throw new ArgumentException("Status must have an Id");

            _statuses[status.Id] = status;
        }

        public void RegisterScenario(ScenarioDefinition scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            if (string.IsNullOrEmpty(scenario.Name))
                throw new ArgumentException("Scenario must have a Name");

            _scenarios[scenario.Name] = scenario;
        }

        public void RegisterBeastForm(CharacterModel.BeastForm beastForm)
        {
            if (beastForm == null) throw new ArgumentNullException(nameof(beastForm));
            if (string.IsNullOrEmpty(beastForm.Id))
                throw new ArgumentException("Beast form must have an Id");

            _beastForms[beastForm.Id] = beastForm;
        }

        // --- Lookup ---

        public StatusDefinition GetStatus(string id)
        {
            return _statuses.TryGetValue(id, out var status) ? status : null;
        }

        public ScenarioDefinition GetScenario(string name)
        {
            return _scenarios.TryGetValue(name, out var scenario) ? scenario : null;
        }

        public CharacterModel.BeastForm GetBeastForm(string id)
        {
            return _beastForms.TryGetValue(id, out var beastForm) ? beastForm : null;
        }

        public IReadOnlyCollection<StatusDefinition> GetAllStatuses() => _statuses.Values;
        public IReadOnlyCollection<ScenarioDefinition> GetAllScenarios() => _scenarios.Values;
        public IReadOnlyCollection<CharacterModel.BeastForm> GetAllBeastForms() => _beastForms.Values;

        // --- Loading ---

        public int LoadStatusesFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[Registry] Status file not found: {path}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path);
                var pack = JsonSerializer.Deserialize<StatusPack>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });

                if (pack?.Statuses == null)
                    return 0;

                foreach (var status in pack.Statuses)
                {
                    RegisterStatus(status);
                }

                _loadedFiles.Add(path);
                return pack.Statuses.Count;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Registry] Failed to load statuses from {path}: {ex.Message}");
                return 0;
            }
        }

        public int LoadScenarioFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[Registry] Scenario file not found: {path}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path);
                var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });

                if (scenario == null || string.IsNullOrEmpty(scenario.Name))
                    return 0;

                RegisterScenario(scenario);
                _loadedFiles.Add(path);
                return 1;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Registry] Failed to load scenario from {path}: {ex.Message}");
                return 0;
            }
        }

        public int LoadBeastFormsFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"[Registry] Beast forms file not found: {path}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path);
                var pack = JsonSerializer.Deserialize<CharacterModel.BeastFormPack>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });

                if (pack?.BeastForms == null)
                    return 0;

                foreach (var beastForm in pack.BeastForms)
                {
                    RegisterBeastForm(beastForm);
                }

                _loadedFiles.Add(path);
                return pack.BeastForms.Count;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Registry] Failed to load beast forms from {path}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Load all data files from a directory structure.
        /// Expects: Abilities/, Statuses/, Scenarios/, CharacterModel/ subdirectories.
        /// </summary>
        public void LoadFromDirectory(string basePath)
        {
            string statusesPath = Path.Combine(basePath, "Statuses");
            string scenariosPath = Path.Combine(basePath, "Scenarios");
            string characterModelPath = Path.Combine(basePath, "CharacterModel");

            int totalStatuses = 0;
            int totalScenarios = 0;
            int totalBeastForms = 0;

            if (Directory.Exists(statusesPath))
            {
                foreach (var file in Directory.GetFiles(statusesPath, "*.json")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    totalStatuses += LoadStatusesFromFile(file);
                }
            }

            if (Directory.Exists(scenariosPath))
            {
                foreach (var file in Directory.GetFiles(scenariosPath, "*.json")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    totalScenarios += LoadScenarioFromFile(file);
                }
            }

            if (Directory.Exists(characterModelPath))
            {
                foreach (var file in Directory.GetFiles(characterModelPath, "beast_forms.json")
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    totalBeastForms += LoadBeastFormsFromFile(file);
                }
            }

            Console.WriteLine($"[Registry] Loaded from {basePath}:");
            Console.WriteLine($"  - {totalStatuses} statuses");
            Console.WriteLine($"  - {totalScenarios} scenarios");
            Console.WriteLine($"  - {totalBeastForms} beast forms");
        }

        // --- Validation ---

        /// <summary>
        /// Validate all registered data for consistency.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            ValidateStatuses(result);
            ValidateScenarios(result);
            CheckDependencies(result);

            return result;
        }

        private void ValidateStatuses(ValidationResult result)
        {
            foreach (var status in _statuses.Values)
            {
                // Required fields
                if (string.IsNullOrEmpty(status.Name))
                {
                    result.AddError("Status", status.Id, "Missing Name");
                }

                // Duration validation
                if (status.DurationType != DurationType.Permanent && status.DefaultDuration <= 0)
                {
                    result.AddWarning("Status", status.Id,
                        "Non-permanent status with duration <= 0 will expire immediately");
                }

                // Stack validation
                if (status.MaxStacks < 1)
                {
                    result.AddError("Status", status.Id, "MaxStacks must be at least 1");
                }

                // Modifier validation
                if (status.Modifiers != null)
                {
                    foreach (var mod in status.Modifiers)
                    {
                        if (string.IsNullOrEmpty(mod.Target.ToString()))
                        {
                            result.AddWarning("Status", status.Id, "Modifier has no target");
                        }
                    }
                }
            }
        }

        private void ValidateScenarios(ValidationResult result)
        {
            foreach (var scenario in _scenarios.Values)
            {
                // Required fields
                if (scenario.Units == null || scenario.Units.Count == 0)
                {
                    result.AddError("Scenario", scenario.Name, "No units defined");
                }
                else
                {
                    var ids = new HashSet<string>();
                    foreach (var unit in scenario.Units)
                    {
                        if (string.IsNullOrEmpty(unit.Id))
                        {
                            result.AddError("Scenario", scenario.Name, "Unit missing Id");
                        }
                        else if (!ids.Add(unit.Id))
                        {
                            result.AddError("Scenario", scenario.Name,
                                $"Duplicate unit Id: {unit.Id}");
                        }

                        int effectiveHp = unit.HP ?? unit.MaxHp ?? 0;
                        bool hasCharacterBuild = unit.ClassLevels != null && unit.ClassLevels.Count > 0;
                        if (effectiveHp <= 0 && !hasCharacterBuild)
                        {
                            result.AddError("Scenario", scenario.Name,
                                $"Unit {unit.Id} has invalid HP: {effectiveHp}");
                        }
                    }
                }
            }
        }

        private void CheckDependencies(ValidationResult result)
        {
            // Check for abilities referencing abilities (for future chaining)
            // Check for circular status dependencies (status A applies B, B applies A)
            // This is a stub for more complex dependency checking

            result.AddInfo("Registry", "Dependencies",
                $"Checked {_statuses.Count} statuses, {_scenarios.Count} scenarios");
        }

        /// <summary>
        /// Load parity allowlist from res://Data/Validation/parity_allowlist.json.
        /// Returns null if file doesn't exist (graceful degradation).
        /// </summary>
        private ParityAllowlist LoadAllowlist()
        {
            const string allowlistPath = "res://Data/Validation/parity_allowlist.json";
            
            if (!RuntimeSafety.TryReadText(allowlistPath, out var json))
            {
                // Allowlist is optional - if missing, treat all errors as errors
                return null;
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<ParityAllowlistFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (parsed == null)
                    return null;

                var result = new ParityAllowlist();

                if (parsed.AllowMissingStatusIds != null)
                {
                    foreach (var id in parsed.AllowMissingStatusIds.Where(i => !string.IsNullOrWhiteSpace(i)))
                        result.AllowMissingStatusIds.Add(id.Trim());
                }

                if (parsed.AllowMissingGrantedAbilities != null)
                {
                    foreach (var id in parsed.AllowMissingGrantedAbilities.Where(i => !string.IsNullOrWhiteSpace(i)))
                        result.AllowMissingGrantedAbilities.Add(id.Trim());
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Registry] Failed to parse allowlist: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if a validation error should be suppressed based on the allowlist.
        /// </summary>
        private bool IsAllowlisted(ValidationIssue issue, ParityAllowlist allowlist)
        {
            if (allowlist == null)
                return false;

            // Check for "References unknown status: X" pattern
            const string statusRefPrefix = "References unknown status: ";
            if (issue.Message.StartsWith(statusRefPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var statusId = issue.Message.Substring(statusRefPrefix.Length).Trim();
                if (allowlist.AllowMissingStatusIds.Contains(statusId))
                    return true;
            }

            // Check for missing granted ability pattern
            // Format: "missing granted ability" or similar
            if (issue.Message.Contains("missing granted ability", StringComparison.OrdinalIgnoreCase))
            {
                // Try to extract ability ID from message
                foreach (var allowedId in allowlist.AllowMissingGrantedAbilities)
                {
                    if (issue.Message.Contains(allowedId, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Validate and fail fast if there are errors.
        /// Respects parity_allowlist.json - allowlisted errors become warnings.
        /// </summary>
        public void ValidateOrThrow()
        {
            var result = Validate();
            var allowlist = LoadAllowlist();

            if (allowlist != null)
            {
                // Downgrade allowlisted errors to warnings
                int suppressedCount = 0;
                foreach (var issue in result.Issues.ToList())
                {
                    if (issue.Severity == ValidationSeverity.Error && IsAllowlisted(issue, allowlist))
                    {
                        issue.Severity = ValidationSeverity.Warning;
                        suppressedCount++;
                    }
                }

                if (suppressedCount > 0)
                {
                    Console.WriteLine($"[Registry] Allowlist suppressed {suppressedCount} known BG3 parity gaps (now warnings)");
                }
            }

            result.PrintSummary();

            if (result.HasErrors)
            {
                throw new InvalidOperationException(
                    $"Registry validation failed with {result.ErrorCount} errors. See log for details.");
            }
        }

        // --- Stats ---

        public void PrintStats()
        {
            Console.WriteLine("[Registry] Statistics:");
            Console.WriteLine($"  - Statuses: {_statuses.Count}");
            Console.WriteLine($"  - Scenarios: {_scenarios.Count}");
            Console.WriteLine($"  - Loaded files: {_loadedFiles.Count}");
        }
    }

    // --- Helper classes for JSON deserialization ---

    public class ActionPack
    {
        public string PackId { get; set; }
        public string Version { get; set; }
        
        [JsonPropertyName("actions")]
        public List<ActionDefinition> Actions { get; set; }
    }

    public class StatusPack
    {
        public string PackId { get; set; }
        public string Version { get; set; }
        public List<StatusDefinition> Statuses { get; set; }
    }
}
