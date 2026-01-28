using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using QDND.Combat.Abilities;
using QDND.Combat.Statuses;

namespace QDND.Data
{
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

        public override string ToString()
        {
            string prefix = Severity switch
            {
                ValidationSeverity.Error => "[ERROR]",
                ValidationSeverity.Warning => "[WARN]",
                _ => "[INFO]"
            };
            return $"{prefix} [{Category}] {ItemId}: {Message}";
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
                Godot.GD.Print("[Registry] Validation passed with no issues");
                return;
            }

            foreach (var issue in Issues)
            {
                Godot.GD.Print(issue.ToString());
            }

            Godot.GD.Print($"[Registry] Validation complete: {ErrorCount} errors, {WarningCount} warnings");
        }
    }

    /// <summary>
    /// Central data registry for game content.
    /// Handles loading, validation, and access to abilities, statuses, and scenarios.
    /// </summary>
    public class DataRegistry
    {
        private readonly Dictionary<string, AbilityDefinition> _abilities = new();
        private readonly Dictionary<string, StatusDefinition> _statuses = new();
        private readonly Dictionary<string, ScenarioDefinition> _scenarios = new();
        
        private readonly List<string> _loadedFiles = new();

        // --- Registration ---

        public void RegisterAbility(AbilityDefinition ability)
        {
            if (ability == null) throw new ArgumentNullException(nameof(ability));
            if (string.IsNullOrEmpty(ability.Id))
                throw new ArgumentException("Ability must have an Id");

            _abilities[ability.Id] = ability;
        }

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

        // --- Lookup ---

        public AbilityDefinition GetAbility(string id)
        {
            return _abilities.TryGetValue(id, out var ability) ? ability : null;
        }

        public StatusDefinition GetStatus(string id)
        {
            return _statuses.TryGetValue(id, out var status) ? status : null;
        }

        public ScenarioDefinition GetScenario(string name)
        {
            return _scenarios.TryGetValue(name, out var scenario) ? scenario : null;
        }

        public IReadOnlyCollection<AbilityDefinition> GetAllAbilities() => _abilities.Values;
        public IReadOnlyCollection<StatusDefinition> GetAllStatuses() => _statuses.Values;
        public IReadOnlyCollection<ScenarioDefinition> GetAllScenarios() => _scenarios.Values;

        // --- Loading ---

        public int LoadAbilitiesFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Godot.GD.PrintErr($"[Registry] Ability file not found: {path}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path);
                var pack = JsonSerializer.Deserialize<AbilityPack>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (pack?.Abilities == null)
                    return 0;

                foreach (var ability in pack.Abilities)
                {
                    RegisterAbility(ability);
                }

                _loadedFiles.Add(path);
                return pack.Abilities.Count;
            }
            catch (Exception ex)
            {
                Godot.GD.PrintErr($"[Registry] Failed to load abilities from {path}: {ex.Message}");
                return 0;
            }
        }

        public int LoadStatusesFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Godot.GD.PrintErr($"[Registry] Status file not found: {path}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path);
                var pack = JsonSerializer.Deserialize<StatusPack>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
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
                Godot.GD.PrintErr($"[Registry] Failed to load statuses from {path}: {ex.Message}");
                return 0;
            }
        }

        public int LoadScenarioFromFile(string path)
        {
            if (!File.Exists(path))
            {
                Godot.GD.PrintErr($"[Registry] Scenario file not found: {path}");
                return 0;
            }

            try
            {
                string json = File.ReadAllText(path);
                var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (scenario == null)
                    return 0;

                RegisterScenario(scenario);
                _loadedFiles.Add(path);
                return 1;
            }
            catch (Exception ex)
            {
                Godot.GD.PrintErr($"[Registry] Failed to load scenario from {path}: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Load all data files from a directory structure.
        /// Expects: Abilities/, Statuses/, Scenarios/ subdirectories.
        /// </summary>
        public void LoadFromDirectory(string basePath)
        {
            string abilitiesPath = Path.Combine(basePath, "Abilities");
            string statusesPath = Path.Combine(basePath, "Statuses");
            string scenariosPath = Path.Combine(basePath, "Scenarios");

            int totalAbilities = 0;
            int totalStatuses = 0;
            int totalScenarios = 0;

            if (Directory.Exists(abilitiesPath))
            {
                foreach (var file in Directory.GetFiles(abilitiesPath, "*.json"))
                {
                    totalAbilities += LoadAbilitiesFromFile(file);
                }
            }

            if (Directory.Exists(statusesPath))
            {
                foreach (var file in Directory.GetFiles(statusesPath, "*.json"))
                {
                    totalStatuses += LoadStatusesFromFile(file);
                }
            }

            if (Directory.Exists(scenariosPath))
            {
                foreach (var file in Directory.GetFiles(scenariosPath, "*.json"))
                {
                    totalScenarios += LoadScenarioFromFile(file);
                }
            }

            Godot.GD.Print($"[Registry] Loaded from {basePath}:");
            Godot.GD.Print($"  - {totalAbilities} abilities");
            Godot.GD.Print($"  - {totalStatuses} statuses");
            Godot.GD.Print($"  - {totalScenarios} scenarios");
        }

        // --- Validation ---

        /// <summary>
        /// Validate all registered data for consistency.
        /// </summary>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();

            ValidateAbilities(result);
            ValidateStatuses(result);
            ValidateScenarios(result);
            CheckDependencies(result);

            return result;
        }

        private void ValidateAbilities(ValidationResult result)
        {
            foreach (var ability in _abilities.Values)
            {
                // Required fields
                if (string.IsNullOrEmpty(ability.Name))
                {
                    result.AddError("Ability", ability.Id, "Missing Name");
                }

                // Targeting validation
                if (ability.TargetType == TargetType.None)
                {
                    result.AddWarning("Ability", ability.Id, "TargetType is None");
                }

                // Effects validation
                if (ability.Effects == null || ability.Effects.Count == 0)
                {
                    result.AddWarning("Ability", ability.Id, "No effects defined");
                }
                else
                {
                    foreach (var effect in ability.Effects)
                    {
                        if (string.IsNullOrEmpty(effect.Type))
                        {
                            result.AddError("Ability", ability.Id, "Effect missing Type");
                        }

                        // Check status references
                        if (effect.Type == "apply_status" && !string.IsNullOrEmpty(effect.StatusId))
                        {
                            if (!_statuses.ContainsKey(effect.StatusId))
                            {
                                result.AddError("Ability", ability.Id, 
                                    $"References unknown status: {effect.StatusId}");
                            }
                        }
                    }
                }

                // Cooldown validation
                if (ability.Cooldown != null && ability.Cooldown.TurnCooldown < 0)
                {
                    result.AddError("Ability", ability.Id, "Cooldown cannot be negative");
                }

                // Range validation
                if (ability.Range < 0)
                {
                    result.AddError("Ability", ability.Id, "Range cannot be negative");
                }
            }
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

                        if (unit.HP <= 0)
                        {
                            result.AddError("Scenario", scenario.Name, 
                                $"Unit {unit.Id} has invalid HP: {unit.HP}");
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
                $"Checked {_abilities.Count} abilities, {_statuses.Count} statuses, {_scenarios.Count} scenarios");
        }

        /// <summary>
        /// Validate and fail fast if there are errors.
        /// </summary>
        public void ValidateOrThrow()
        {
            var result = Validate();
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
            Godot.GD.Print("[Registry] Statistics:");
            Godot.GD.Print($"  - Abilities: {_abilities.Count}");
            Godot.GD.Print($"  - Statuses: {_statuses.Count}");
            Godot.GD.Print($"  - Scenarios: {_scenarios.Count}");
            Godot.GD.Print($"  - Loaded files: {_loadedFiles.Count}");
        }
    }

    // --- Helper classes for JSON deserialization ---

    public class AbilityPack
    {
        public string PackId { get; set; }
        public string Version { get; set; }
        public List<AbilityDefinition> Abilities { get; set; }
    }

    public class StatusPack
    {
        public string PackId { get; set; }
        public string Version { get; set; }
        public List<StatusDefinition> Statuses { get; set; }
    }
}
