using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using QDND.Combat.Actions;
using QDND.Data.Actions;
using QDND.Data.Interrupts;
using QDND.Data.Passives;
using QDND.Data.Statuses;

namespace QDND.Data.Validation
{
    /// <summary>
    /// Cross-registry parity validator.
    /// Loads every registry from disk and checks for:
    ///   1. Internal registry errors (duplicate IDs, parse failures)
    ///   2. Scenario → action/passive cross-reference integrity
    ///   3. Status → status functor cross-references
    ///   4. DataRegistry action → status effect cross-references
    ///   5. Scenario JSON schema validity
    /// </summary>
    public static class ParityValidator
    {
        // Regex to extract the first argument of ApplyStatus(...) in functor strings
        private static readonly Regex ApplyStatusRegex =
            new(@"ApplyStatus\(\s*([A-Za-z0-9_]+)", RegexOptions.Compiled);

        /// <summary>
        /// Run the full parity validation suite, loading all registries from disk.
        /// NOTE: PassiveRegistry and InterruptRegistry use Godot.GD.Print internally,
        /// so this overload must be called from within the Godot engine. For unit tests
        /// outside Godot, use <see cref="ValidateWithRegistries"/> instead.
        /// </summary>
        /// <param name="dataDirectory">Path to the Data/ folder (contains Actions/, Statuses/, Scenarios/ sub-dirs).</param>
        /// <param name="bg3DataDirectory">Path to the BG3_Data/ folder (Spells, Statuses, Stats sub-dirs).</param>
        /// <param name="scenarioDirectory">Path to the scenario JSON directory (typically Data/Scenarios/).</param>
        /// <returns>Aggregate validation result.</returns>
        public static ParityValidationResult Validate(
            string dataDirectory,
            string bg3DataDirectory,
            string scenarioDirectory)
        {
            var result = new ParityValidationResult();

            // ------------------------------------------------------------------
            // 1. Load registries
            // ------------------------------------------------------------------

            // 1a. ActionRegistry (BG3 spells) — safe outside Godot
            ActionRegistry actionRegistry = null;
            try
            {
                actionRegistry = new ActionRegistry();
                var initResult = ActionRegistryInitializer.Initialize(
                    actionRegistry, bg3DataDirectory, verboseLogging: false);

                result.TotalChecks++;
                if (!initResult.Success)
                {
                    result.Errors.Add(new ParityError(
                        "ActionRegistry",
                        $"Initialization failed: {initResult.ErrorMessage}"));
                }
                else if (initResult.ErrorCount > 0)
                {
                    result.Warnings.Add(new ParityWarning(
                        "ActionRegistry",
                        $"{initResult.ErrorCount} parse error(s) during initialization"));
                }

                result.TotalChecks++;
                if (actionRegistry.Count == 0)
                {
                    result.Errors.Add(new ParityError(
                        "ActionRegistry",
                        "Registry is empty after initialization"));
                }
            }
            catch (Exception ex)
            {
                result.TotalChecks++;
                result.Errors.Add(new ParityError(
                    "ActionRegistry",
                    $"Exception during initialization: {ex.Message}"));
            }

            // 1b. DataRegistry (legacy actions / statuses) — safe outside Godot
            DataRegistry dataRegistry = null;
            try
            {
                dataRegistry = new DataRegistry();
                dataRegistry.LoadFromDirectory(dataDirectory);
                result.TotalChecks++;
            }
            catch (Exception ex)
            {
                result.TotalChecks++;
                result.Errors.Add(new ParityError(
                    "DataRegistry",
                    $"Failed to load: {ex.Message}"));
            }

            // 1c. StatusRegistry (BG3 statuses) — safe outside Godot
            StatusRegistry statusRegistry = null;
            try
            {
                string statusDir = Path.Combine(bg3DataDirectory, "Statuses");
                statusRegistry = new StatusRegistry();
                if (Directory.Exists(statusDir))
                {
                    statusRegistry.LoadStatuses(statusDir);
                }

                result.TotalChecks++;
                if (statusRegistry.Count == 0)
                {
                    result.Warnings.Add(new ParityWarning(
                        "StatusRegistry",
                        "No BG3 statuses loaded (directory may be missing)"));
                }

                if (statusRegistry.Errors.Count > 0)
                {
                    result.Warnings.Add(new ParityWarning(
                        "StatusRegistry",
                        $"{statusRegistry.Errors.Count} parse error(s)"));
                }
            }
            catch (Exception ex)
            {
                result.TotalChecks++;
                result.Errors.Add(new ParityError(
                    "StatusRegistry",
                    $"Failed to load: {ex.Message}"));
            }

            // 1d. PassiveRegistry — uses Godot.GD.Print, requires Godot engine
            PassiveRegistry passiveRegistry = null;
            try
            {
                string passiveFile = Path.Combine(bg3DataDirectory, "Stats", "Passive.txt");
                passiveRegistry = new PassiveRegistry();
                if (File.Exists(passiveFile))
                {
                    passiveRegistry.LoadPassives(passiveFile);
                }

                result.TotalChecks++;
                if (passiveRegistry.Count == 0)
                {
                    result.Warnings.Add(new ParityWarning(
                        "PassiveRegistry",
                        "No passives loaded (file may be missing)"));
                }
            }
            catch (Exception ex)
            {
                result.TotalChecks++;
                result.Errors.Add(new ParityError(
                    "PassiveRegistry",
                    $"Failed to load: {ex.Message}"));
            }

            // 1e. InterruptRegistry — uses Godot.GD.Print, requires Godot engine
            InterruptRegistry interruptRegistry = null;
            try
            {
                string interruptFile = Path.Combine(bg3DataDirectory, "Stats", "Interrupt.txt");
                interruptRegistry = new InterruptRegistry();
                if (File.Exists(interruptFile))
                {
                    interruptRegistry.LoadInterrupts(interruptFile);
                }

                result.TotalChecks++;
                if (interruptRegistry.Count == 0)
                {
                    result.Warnings.Add(new ParityWarning(
                        "InterruptRegistry",
                        "No interrupts loaded (file may be missing)"));
                }
            }
            catch (Exception ex)
            {
                result.TotalChecks++;
                result.Errors.Add(new ParityError(
                    "InterruptRegistry",
                    $"Failed to load: {ex.Message}"));
            }

            // Delegate to the registry-based overload
            return ValidateWithRegistries(
                scenarioDirectory,
                actionRegistry,
                dataRegistry,
                statusRegistry,
                passiveRegistry,
                interruptRegistry,
                result);
        }

        /// <summary>
        /// Run the parity validation suite using pre-loaded registries.
        /// This overload is safe to call from unit tests outside the Godot engine
        /// because it does not load PassiveRegistry/InterruptRegistry from disk
        /// (those use Godot native APIs). Pass null for any registry you cannot load.
        /// </summary>
        /// <param name="scenarioDirectory">Path to scenario JSON files.</param>
        /// <param name="actionRegistry">BG3 spell registry (may be null).</param>
        /// <param name="dataRegistry">Legacy action/status definitions (may be null).</param>
        /// <param name="statusRegistry">BG3 status registry (may be null).</param>
        /// <param name="passiveRegistry">BG3 passive registry (may be null).</param>
        /// <param name="interruptRegistry">BG3 interrupt registry (may be null — checked for count only).</param>
        /// <param name="existing">Append to an existing result; if null a new one is created.</param>
        /// <returns>Aggregate validation result.</returns>
        public static ParityValidationResult ValidateWithRegistries(
            string scenarioDirectory,
            ActionRegistry actionRegistry,
            DataRegistry dataRegistry,
            StatusRegistry statusRegistry,
            PassiveRegistry passiveRegistry,
            InterruptRegistry interruptRegistry,
            ParityValidationResult existing = null)
        {
            var result = existing ?? new ParityValidationResult();

            // Scenario cross-reference checks
            ValidateScenarios(
                scenarioDirectory,
                actionRegistry,
                dataRegistry,
                passiveRegistry,
                result);

            // Status cross-references (functor → status)
            ValidateStatusCrossReferences(statusRegistry, result);

            // DataRegistry action → status effect cross-references
            ValidateActionEffectStatuses(dataRegistry, statusRegistry, result);

            return result;
        }

        // =================================================================
        //  Scenario cross-reference validation
        // =================================================================

        private static void ValidateScenarios(
            string scenarioDirectory,
            ActionRegistry actionRegistry,
            DataRegistry dataRegistry,
            PassiveRegistry passiveRegistry,
            ParityValidationResult result)
        {
            if (string.IsNullOrEmpty(scenarioDirectory) || !Directory.Exists(scenarioDirectory))
            {
                result.TotalChecks++;
                result.Warnings.Add(new ParityWarning(
                    "Scenario",
                    $"Scenario directory not found: {scenarioDirectory}"));
                return;
            }

            var jsonFiles = Directory.GetFiles(scenarioDirectory, "*.json");
            if (jsonFiles.Length == 0)
            {
                result.TotalChecks++;
                result.Warnings.Add(new ParityWarning("Scenario", "No scenario JSON files found"));
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            foreach (var file in jsonFiles)
            {
                string fileName = Path.GetFileName(file);

                // --- Schema / parse check ---
                result.TotalChecks++;
                ScenarioDefinition scenario;
                try
                {
                    string json = File.ReadAllText(file);
                    scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, jsonOptions);
                    if (scenario == null)
                    {
                        result.Errors.Add(new ParityError(
                            "Scenario", $"Deserialized to null", fileName));
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ParityError(
                        "Scenario", $"JSON parse error: {ex.Message}", fileName));
                    continue;
                }

                // --- Per-unit checks ---
                if (scenario.Units == null || scenario.Units.Count == 0)
                {
                    result.TotalChecks++;
                    result.Errors.Add(new ParityError(
                        "Scenario", "No units defined", fileName));
                    continue;
                }

                foreach (var unit in scenario.Units)
                {
                    // HP check
                    result.TotalChecks++;
                    int effectiveHp = unit.HP ?? unit.MaxHp ?? 0;
                    bool hasCharacterBuild = unit.ClassLevels != null && unit.ClassLevels.Count > 0;
                    if (effectiveHp <= 0 && !hasCharacterBuild)
                    {
                        result.Errors.Add(new ParityError(
                            "Scenario",
                            $"Unit '{unit.Id}' has HP <= 0 and no class build",
                            fileName));
                    }

                    // At least one action
                    result.TotalChecks++;
                    if (unit.KnownActions == null || unit.KnownActions.Count == 0)
                    {
                        // Not an error if unit has class levels (actions come from class)
                        if (!hasCharacterBuild)
                        {
                            result.Warnings.Add(new ParityWarning(
                                "Scenario",
                                $"Unit '{unit.Id}' in {fileName} has no actions and no class build"));
                        }
                    }
                    else
                    {
                        // Validate each action reference
                        foreach (string actionId in unit.KnownActions)
                        {
                            result.TotalChecks++;
                            bool found = false;

                            // Check ActionRegistry (BG3 spells)
                            if (actionRegistry != null && actionRegistry.HasAction(actionId))
                                found = true;

                            // Check DataRegistry (legacy)
                            if (!found && dataRegistry != null && dataRegistry.GetAction(actionId) != null)
                                found = true;

                            if (!found)
                            {
                                result.Errors.Add(new ParityError(
                                    "Scenario",
                                    $"Unit '{unit.Id}' references unknown action '{actionId}'",
                                    fileName));
                            }
                        }
                    }

                    // Validate passive references
                    if (unit.Passives != null)
                    {
                        foreach (string passiveId in unit.Passives)
                        {
                            result.TotalChecks++;
                            if (passiveRegistry != null && !passiveRegistry.HasPassive(passiveId))
                            {
                                result.Errors.Add(new ParityError(
                                    "Scenario",
                                    $"Unit '{unit.Id}' references unknown passive '{passiveId}'",
                                    fileName));
                            }
                        }
                    }
                }
            }
        }

        // =================================================================
        //  Status cross-reference validation (ApplyStatus functors)
        // =================================================================

        private static void ValidateStatusCrossReferences(
            StatusRegistry statusRegistry,
            ParityValidationResult result)
        {
            if (statusRegistry == null || statusRegistry.Count == 0)
                return;

            foreach (var status in statusRegistry.GetAllStatuses())
            {
                CheckFunctorStatusRefs(status.StatusId, status.OnApplyFunctors, "OnApplyFunctors", statusRegistry, result);
                CheckFunctorStatusRefs(status.StatusId, status.OnTickFunctors, "OnTickFunctors", statusRegistry, result);
                CheckFunctorStatusRefs(status.StatusId, status.OnRemoveFunctors, "OnRemoveFunctors", statusRegistry, result);
            }
        }

        private static void CheckFunctorStatusRefs(
            string ownerStatusId,
            string functorString,
            string functorField,
            StatusRegistry registry,
            ParityValidationResult result)
        {
            if (string.IsNullOrEmpty(functorString))
                return;

            var matches = ApplyStatusRegex.Matches(functorString);
            foreach (Match m in matches)
            {
                result.TotalChecks++;
                string referencedId = m.Groups[1].Value;
                if (!registry.HasStatus(referencedId))
                {
                    result.Warnings.Add(new ParityWarning(
                        "StatusCrossRef",
                        $"Status '{ownerStatusId}' {functorField} references unknown status '{referencedId}'"));
                }
            }
        }

        // =================================================================
        //  DataRegistry action effect → status validation
        // =================================================================

        private static void ValidateActionEffectStatuses(
            DataRegistry dataRegistry,
            StatusRegistry statusRegistry,
            ParityValidationResult result)
        {
            if (dataRegistry == null)
                return;

            foreach (var action in dataRegistry.GetAllActions())
            {
                if (action.Effects == null) continue;

                foreach (var effect in action.Effects)
                {
                    if (effect.Type == "apply_status" && !string.IsNullOrEmpty(effect.StatusId))
                    {
                        result.TotalChecks++;
                        bool found = false;

                        // Check legacy DataRegistry statuses
                        if (dataRegistry.GetStatus(effect.StatusId) != null)
                            found = true;

                        // Check BG3 StatusRegistry
                        if (!found && statusRegistry != null && statusRegistry.HasStatus(effect.StatusId))
                            found = true;

                        if (!found)
                        {
                            result.Warnings.Add(new ParityWarning(
                                "ActionEffect",
                                $"Action '{action.Id}' effect references unknown status '{effect.StatusId}'"));
                        }
                    }
                }
            }
        }
    }
}
