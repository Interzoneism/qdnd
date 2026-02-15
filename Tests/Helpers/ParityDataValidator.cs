using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using QDND.Combat.Actions;
using QDND.Data;
using QDND.Data.CharacterModel;

namespace QDND.Tests.Helpers
{
    internal sealed class ParityValidationReport
    {
        private readonly List<string> _errors = new();
        private readonly List<string> _warnings = new();

        public IReadOnlyList<string> Errors => _errors;
        public IReadOnlyList<string> Warnings => _warnings;
        public bool HasErrors => _errors.Count > 0;
        
        /// <summary>
        /// Coverage inventory showing action registry coverage across scenarios.
        /// </summary>
        public CoverageInventory CoverageInventory { get; set; }

        public void AddError(string message) => _errors.Add(message);
        public void AddWarning(string message) => _warnings.Add(message);

        public string Format()
        {
            var lines = new List<string>();

            if (_errors.Count > 0)
            {
                lines.Add($"Parity validation errors: {_errors.Count}");
                lines.AddRange(_errors.Select(e => $"  - {e}"));
            }

            if (_warnings.Count > 0)
            {
                lines.Add($"Parity validation warnings: {_warnings.Count}");
                lines.AddRange(_warnings.Select(w => $"  - {w}"));
            }

            if (lines.Count == 0)
            {
                lines.Add("Parity validation passed with no issues.");
            }

            // Add coverage inventory if available
            if (CoverageInventory != null)
            {
                lines.Add("");
                lines.Add("=== Action Coverage Inventory ===");
                lines.Add($"Total actions granted across scenarios: {CoverageInventory.TotalGrantedActions}");
                lines.Add($"Actions available in Data/Actions: {CoverageInventory.ActionsInDataRegistry}");
                lines.Add($"Forbidden summon actions: {CoverageInventory.ForbiddenSummonActions}");
                lines.Add($"Missing actions (granted but not in registry): {CoverageInventory.MissingActions}");
                
                if (CoverageInventory.GrantedSummonActions > 0)
                {
                    lines.Add($"WARNING: Summon actions granted in scenarios: {CoverageInventory.GrantedSummonActions}");
                    lines.Add($"  IDs: {string.Join(", ", CoverageInventory.GrantedSummonActionIds)}");
                }
                
                if (CoverageInventory.MissingActions > 0)
                {
                    lines.Add($"Missing action IDs (sample): {string.Join(", ", CoverageInventory.MissingActionIds.Take(10))}");
                    if (CoverageInventory.MissingActionIds.Count > 10)
                    {
                        lines.Add($"  ... and {CoverageInventory.MissingActionIds.Count - 10} more");
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    internal sealed class ParityDataValidator
    {
        private readonly string _repoRoot;
        private readonly string _dataRoot;
        private readonly JsonSerializerOptions _jsonOptions;

        public ParityDataValidator()
        {
            _repoRoot = ResolveRepoRoot();
            _dataRoot = Path.Combine(_repoRoot, "Data");
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public ParityValidationReport Validate()
        {
            var report = new ParityValidationReport();
            var allowlist = LoadAllowlist(report);

            var actionEntries = LoadActionEntries(report);
            var statusEntries = LoadStatusEntries(report);
            var raceEntries = LoadRaceEntries(report);
            var classEntries = LoadClassEntries(report);
            var featEntries = LoadFeatEntries(report);
            var beastEntries = LoadBeastEntries(report);
            var scenarioEntries = LoadScenarioEntries(report);
            var equipmentEntries = LoadEquipmentEntries(report);

            ValidateDuplicateIds(
                "action",
                actionEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                allowlist.AllowDuplicateIds.GetValueOrDefault("action") ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "status",
                statusEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                allowlist.AllowDuplicateIds.GetValueOrDefault("status") ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "race",
                raceEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "class",
                classEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "feat",
                featEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "beast_form",
                beastEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "scenario",
                scenarioEntries.Select(e => new IdRef(e.Definition.Name, e.FilePath, e.Definition.Id)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "weapon",
                equipmentEntries.WeaponEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            ValidateDuplicateIds(
                "armor",
                equipmentEntries.ArmorEntries.Select(e => new IdRef(e.Definition.Id, e.FilePath, e.Definition.Name)),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                report);

            var actionIds = actionEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Definition.Id))
                .Select(e => e.Definition.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var statusIds = statusEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Definition.Id))
                .Select(e => e.Definition.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            ValidateMissingGrantedAbilities(
                actionIds,
                CollectGrantedAbilityRefs(raceEntries, classEntries, featEntries, beastEntries),
                allowlist.AllowMissingGrantedAbilities,
                report);

            ValidateActionStatusLinks(actionEntries, statusIds, allowlist.AllowMissingStatusIds, report);
            ValidateEffectHandlers(actionEntries, report);
            ValidateNoSummonActionsInScenarios(scenarioEntries, actionEntries, report);

            // Generate coverage inventory report
            GenerateCoverageInventory(scenarioEntries, actionEntries, report);

            return report;
        }

        /// <summary>
        /// Generate a coverage inventory showing what actions are granted across scenarios,
        /// which are available in registries, and which are missing or forbidden.
        /// </summary>
        private static void GenerateCoverageInventory(
            IReadOnlyCollection<ScenarioEntry> scenarioEntries,
            IReadOnlyCollection<ActionEntry> actionEntries,
            ParityValidationReport report)
        {
            var inventory = new CoverageInventory();

            // Collect all granted actions from scenarios
            var grantedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var scenarioEntry in scenarioEntries)
            {
                if (scenarioEntry.Definition.Units == null) continue;

                foreach (var unit in scenarioEntry.Definition.Units)
                {
                    if (unit.KnownActions != null)
                    {
                        foreach (var actionId in unit.KnownActions)
                        {
                            if (!string.IsNullOrWhiteSpace(actionId))
                            {
                                grantedActions.Add(actionId.Trim());
                            }
                        }
                    }
                }
            }

            inventory.TotalGrantedActions = grantedActions.Count;

            // Build action registry sets
            var dataRegistryActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var bg3RegistryActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var summonActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in actionEntries)
            {
                var actionId = entry.Definition.Id?.Trim();
                if (string.IsNullOrWhiteSpace(actionId)) continue;

                dataRegistryActions.Add(actionId);

                // Check if it's a summon action
                if (IsSummonAction(entry.Definition))
                {
                    summonActions.Add(actionId);
                }
            }

            // For BG3 registry actions, we'd need to load the ActionRegistry here
            // Since we don't have it available in this context, we'll note it as a limitation
            // For now, just report on Data/Actions coverage

            inventory.ActionsInDataRegistry = dataRegistryActions.Count;
            inventory.ForbiddenSummonActions = summonActions.Count;

            // Find missing actions (granted but not in any registry)
            var missingActions = new List<string>();
            foreach (var actionId in grantedActions)
            {
                if (!dataRegistryActions.Contains(actionId))
                {
                    missingActions.Add(actionId);
                }
            }

            inventory.MissingActions = missingActions.Count;
            inventory.MissingActionIds = missingActions;

            // Find granted summon actions (should be 0 if validation passes)
            var grantedSummons = new List<string>();
            foreach (var actionId in grantedActions)
            {
                if (summonActions.Contains(actionId))
                {
                    grantedSummons.Add(actionId);
                }
            }
            inventory.GrantedSummonActions = grantedSummons.Count;
            inventory.GrantedSummonActionIds = grantedSummons;

            report.CoverageInventory = inventory;
        }

        private static string ResolveRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current != null)
            {
                var dataDir = Path.Combine(current.FullName, "Data");
                var testsDir = Path.Combine(current.FullName, "Tests");

                if (Directory.Exists(dataDir) && Directory.Exists(testsDir))
                    return current.FullName;

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not resolve repository root for parity validation.");
        }

        private ParityAllowlist LoadAllowlist(ParityValidationReport report)
        {
            var path = Path.Combine(_dataRoot, "Validation", "parity_allowlist.json");
            if (!File.Exists(path))
            {
                report.AddError($"Missing allowlist file: {path}");
                return new ParityAllowlist();
            }

            try
            {
                var json = File.ReadAllText(path);
                var parsed = JsonSerializer.Deserialize<ParityAllowlistFile>(json, _jsonOptions);
                if (parsed == null)
                {
                    report.AddError($"Allowlist deserialized to null: {path}");
                    return new ParityAllowlist();
                }

                var result = new ParityAllowlist();

                if (parsed.AllowDuplicateIds != null)
                {
                    foreach (var (category, ids) in parsed.AllowDuplicateIds)
                    {
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (ids != null)
                        {
                            foreach (var id in ids.Where(i => !string.IsNullOrWhiteSpace(i)))
                                set.Add(id.Trim());
                        }

                        result.AllowDuplicateIds[category?.Trim() ?? string.Empty] = set;
                    }
                }

                if (parsed.AllowMissingGrantedAbilities != null)
                {
                    foreach (var id in parsed.AllowMissingGrantedAbilities.Where(i => !string.IsNullOrWhiteSpace(i)))
                        result.AllowMissingGrantedAbilities.Add(id.Trim());
                }

                if (parsed.AllowMissingStatusIds != null)
                {
                    foreach (var id in parsed.AllowMissingStatusIds.Where(i => !string.IsNullOrWhiteSpace(i)))
                        result.AllowMissingStatusIds.Add(id.Trim());
                }

                return result;
            }
            catch (Exception ex)
            {
                report.AddError($"Failed to parse allowlist {path}: {ex.Message}");
                return new ParityAllowlist();
            }
        }

        private List<ActionEntry> LoadActionEntries(ParityValidationReport report)
        {
            var dir = Path.Combine(_dataRoot, "Actions");
            var entries = new List<ActionEntry>();

            foreach (var file in GetJsonFiles(dir))
            {
                var pack = DeserializeFromFile<ActionPack>(file, report, "action_pack");
                if (pack?.Actions == null)
                {
                    report.AddError($"Schema mismatch in {file}: missing top-level actions array.");
                    continue;
                }

                entries.AddRange(pack.Actions.Select(def => new ActionEntry(file, def)));
            }

            return entries;
        }

        private List<StatusEntry> LoadStatusEntries(ParityValidationReport report)
        {
            var dir = Path.Combine(_dataRoot, "Statuses");
            var entries = new List<StatusEntry>();

            foreach (var file in GetJsonFiles(dir))
            {
                var pack = DeserializeFromFile<StatusPack>(file, report, "status_pack");
                if (pack?.Statuses == null)
                {
                    report.AddError($"Schema mismatch in {file}: missing top-level statuses array.");
                    continue;
                }

                entries.AddRange(pack.Statuses.Select(def => new StatusEntry(file, def)));
            }

            return entries;
        }

        private List<RaceEntry> LoadRaceEntries(ParityValidationReport report)
        {
            var dir = Path.Combine(_dataRoot, "Races");
            var entries = new List<RaceEntry>();

            foreach (var file in GetJsonFiles(dir))
            {
                var pack = DeserializeFromFile<RacePack>(file, report, "race_pack");
                if (pack?.Races == null)
                {
                    report.AddError($"Schema mismatch in {file}: missing top-level races array.");
                    continue;
                }

                entries.AddRange(pack.Races.Select(def => new RaceEntry(file, def)));
            }

            return entries;
        }

        private List<ClassEntry> LoadClassEntries(ParityValidationReport report)
        {
            var dir = Path.Combine(_dataRoot, "Classes");
            var entries = new List<ClassEntry>();

            foreach (var file in GetJsonFiles(dir))
            {
                var pack = DeserializeFromFile<ClassPack>(file, report, "class_pack");
                if (pack?.Classes == null)
                {
                    report.AddError($"Schema mismatch in {file}: missing top-level classes array.");
                    continue;
                }

                entries.AddRange(pack.Classes.Select(def => new ClassEntry(file, def)));
            }

            return entries;
        }

        private List<FeatEntry> LoadFeatEntries(ParityValidationReport report)
        {
            var dir = Path.Combine(_dataRoot, "Feats");
            var entries = new List<FeatEntry>();

            foreach (var file in GetJsonFiles(dir))
            {
                var pack = DeserializeFromFile<FeatPack>(file, report, "feat_pack");
                if (pack?.Feats == null)
                {
                    report.AddError($"Schema mismatch in {file}: missing top-level feats array.");
                    continue;
                }

                entries.AddRange(pack.Feats.Select(def => new FeatEntry(file, def)));
            }

            return entries;
        }

        private List<BeastEntry> LoadBeastEntries(ParityValidationReport report)
        {
            var file = Path.Combine(_dataRoot, "CharacterModel", "beast_forms.json");
            var entries = new List<BeastEntry>();

            if (!File.Exists(file))
            {
                report.AddError($"Missing required beast forms file: {file}");
                return entries;
            }

            var pack = DeserializeFromFile<BeastFormPack>(file, report, "beast_pack");
            if (pack?.BeastForms == null)
            {
                report.AddError($"Schema mismatch in {file}: missing top-level beastForms array.");
                return entries;
            }

            entries.AddRange(pack.BeastForms.Select(def => new BeastEntry(file, def)));
            return entries;
        }

        private List<ScenarioEntry> LoadScenarioEntries(ParityValidationReport report)
        {
            var dir = Path.Combine(_dataRoot, "Scenarios");
            var entries = new List<ScenarioEntry>();

            foreach (var file in GetJsonFiles(dir))
            {
                var def = DeserializeFromFile<ScenarioDefinition>(file, report, "scenario");
                if (def == null)
                    continue;

                entries.Add(new ScenarioEntry(file, def));
            }

            return entries;
        }

        private EquipmentEntries LoadEquipmentEntries(ParityValidationReport report)
        {
            var file = Path.Combine(_dataRoot, "CharacterModel", "equipment_data.json");
            var result = new EquipmentEntries();

            if (!File.Exists(file))
            {
                report.AddError($"Missing required equipment file: {file}");
                return result;
            }

            var pack = DeserializeFromFile<EquipmentPack>(file, report, "equipment_pack");
            if (pack == null)
                return result;

            if (pack.Weapons != null)
            {
                result.WeaponEntries.AddRange(pack.Weapons.Select(w => new WeaponEntry(file, w)));
            }
            else
            {
                report.AddError($"Schema mismatch in {file}: missing top-level weapons array.");
            }

            if (pack.Armors != null)
            {
                result.ArmorEntries.AddRange(pack.Armors.Select(a => new ArmorEntry(file, a)));
            }
            else
            {
                report.AddError($"Schema mismatch in {file}: missing top-level armors array.");
            }

            return result;
        }

        private void ValidateDuplicateIds(
            string category,
            IEnumerable<IdRef> refs,
            HashSet<string> allowlisted,
            ParityValidationReport report)
        {
            var grouped = refs
                .Where(r => !string.IsNullOrWhiteSpace(r.Id))
                .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var (id, idRefs) in grouped)
            {
                if (idRefs.Count <= 1)
                    continue;

                if (allowlisted.Contains(id))
                    continue;

                var locations = string.Join(", ", idRefs.Select(r => r.FilePath).Distinct(StringComparer.OrdinalIgnoreCase));
                report.AddError($"Duplicate {category} id '{id}' found in: {locations}");
            }

            foreach (var allowedId in allowlisted)
            {
                if (!grouped.TryGetValue(allowedId, out var matching) || matching.Count <= 1)
                {
                    report.AddWarning($"Allowlist entry '{allowedId}' in duplicate category '{category}' is stale.");
                }
            }
        }

        private static List<GrantedAbilityRef> CollectGrantedAbilityRefs(
            IReadOnlyCollection<RaceEntry> raceEntries,
            IReadOnlyCollection<ClassEntry> classEntries,
            IReadOnlyCollection<FeatEntry> featEntries,
            IReadOnlyCollection<BeastEntry> beastEntries)
        {
            var refs = new List<GrantedAbilityRef>();

            foreach (var raceEntry in raceEntries)
            {
                if (raceEntry.Definition.Features != null)
                {
                    refs.AddRange(CollectFeatureGrantedAbilities(
                        raceEntry.FilePath,
                        $"race:{raceEntry.Definition.Id}",
                        raceEntry.Definition.Features));
                }

                if (raceEntry.Definition.Subraces != null)
                {
                    foreach (var subrace in raceEntry.Definition.Subraces)
                    {
                        refs.AddRange(CollectFeatureGrantedAbilities(
                            raceEntry.FilePath,
                            $"subrace:{subrace.Id}",
                            subrace.Features));
                    }
                }
            }

            foreach (var classEntry in classEntries)
            {
                if (classEntry.Definition.LevelTable != null)
                {
                    foreach (var (level, progression) in classEntry.Definition.LevelTable)
                    {
                        refs.AddRange(CollectFeatureGrantedAbilities(
                            classEntry.FilePath,
                            $"class:{classEntry.Definition.Id}:level:{level}",
                            progression?.Features));
                    }
                }

                if (classEntry.Definition.Subclasses != null)
                {
                    foreach (var subclass in classEntry.Definition.Subclasses)
                    {
                        if (subclass?.LevelTable == null)
                            continue;

                        foreach (var (level, progression) in subclass.LevelTable)
                        {
                            refs.AddRange(CollectFeatureGrantedAbilities(
                                classEntry.FilePath,
                                $"subclass:{subclass.Id}:level:{level}",
                                progression?.Features));
                        }
                    }
                }
            }

            foreach (var featEntry in featEntries)
            {
                refs.AddRange(CollectFeatureGrantedAbilities(
                    featEntry.FilePath,
                    $"feat:{featEntry.Definition.Id}",
                    featEntry.Definition.Features));
            }

            foreach (var beastEntry in beastEntries)
            {
                if (beastEntry.Definition.GrantedAbilities == null)
                    continue;

                foreach (var granted in beastEntry.Definition.GrantedAbilities)
                {
                    if (string.IsNullOrWhiteSpace(granted))
                        continue;

                    refs.Add(new GrantedAbilityRef(
                        beastEntry.FilePath,
                        $"beast_form:{beastEntry.Definition.Id}",
                        "(beast-form)",
                        granted.Trim()));
                }
            }

            return refs;
        }

        private static IEnumerable<GrantedAbilityRef> CollectFeatureGrantedAbilities(
            string filePath,
            string owner,
            List<Feature> features)
        {
            if (features == null)
                yield break;

            foreach (var feature in features)
            {
                if (feature?.GrantedAbilities == null)
                    continue;

                foreach (var granted in feature.GrantedAbilities)
                {
                    if (string.IsNullOrWhiteSpace(granted))
                        continue;

                    yield return new GrantedAbilityRef(
                        filePath,
                        owner,
                        feature.Id ?? "(feature-without-id)",
                        granted.Trim());
                }
            }
        }

        private static void ValidateMissingGrantedAbilities(
            HashSet<string> actionIds,
            IReadOnlyCollection<GrantedAbilityRef> grantedRefs,
            HashSet<string> allowlistedMissing,
            ParityValidationReport report)
        {
            var missingById = grantedRefs
                .Where(gr => !actionIds.Contains(gr.ActionId))
                .GroupBy(gr => gr.ActionId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

            foreach (var (actionId, refs) in missingById)
            {
                if (allowlistedMissing.Contains(actionId))
                    continue;

                var first = refs[0];
                report.AddError(
                    $"Missing granted ability '{actionId}' referenced by {first.Owner} feature '{first.FeatureId}' ({first.FilePath}).");
            }

            foreach (var allowed in allowlistedMissing)
            {
                if (actionIds.Contains(allowed))
                {
                    report.AddWarning($"Allowlist entry '{allowed}' is stale: action now exists.");
                }
            }
        }

        private static void ValidateActionStatusLinks(
            IReadOnlyCollection<ActionEntry> actionEntries,
            HashSet<string> statusIds,
            HashSet<string> allowlistedMissingStatusIds,
            ParityValidationReport report)
        {
            foreach (var entry in actionEntries)
            {
                var action = entry.Definition;
                if (action == null)
                    continue;
                ValidateEffectStatusRefs(action.Id, entry.FilePath, action.Effects, statusIds, allowlistedMissingStatusIds, report);

                if (action.Variants != null)
                {
                    foreach (var variant in action.Variants)
                    {
                        ValidateEffectStatusRefs(
                            $"{action.Id}:{variant?.VariantId ?? "(variant)"}",
                            entry.FilePath,
                            variant?.AdditionalEffects,
                            statusIds,
                            allowlistedMissingStatusIds,
                            report);
                    }
                }
            }

            foreach (var allowlistedStatusId in allowlistedMissingStatusIds)
            {
                if (statusIds.Contains(allowlistedStatusId))
                {
                    report.AddWarning($"Allowlist entry '{allowlistedStatusId}' is stale: status now exists.");
                }
            }
        }

        private static void ValidateEffectStatusRefs(
            string ownerId,
            string filePath,
            List<EffectDefinition> effects,
            HashSet<string> statusIds,
            HashSet<string> allowlistedMissingStatusIds,
            ParityValidationReport report)
        {
            if (effects == null)
                return;

            foreach (var effect in effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Type))
                    continue;

                var effectType = effect.Type.Trim();
                if (!effectType.Equals("apply_status", StringComparison.OrdinalIgnoreCase) &&
                    !effectType.Equals("remove_status", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var statusId = effect.StatusId?.Trim();
                if (string.IsNullOrWhiteSpace(statusId))
                {
                    report.AddError($"{ownerId} has {effectType} effect without statusId ({filePath}).");
                    continue;
                }

                if (!statusIds.Contains(statusId))
                {
                    if (allowlistedMissingStatusIds.Contains(statusId))
                        continue;

                    report.AddError($"{ownerId} references unknown status '{statusId}' in {effectType} effect ({filePath}).");
                }
            }
        }

        private static void ValidateEffectHandlers(
            IReadOnlyCollection<ActionEntry> actionEntries,
            ParityValidationReport report)
        {
            var registered = new HashSet<string>(
                new EffectPipeline().GetRegisteredEffectTypes(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var entry in actionEntries)
            {
                var action = entry.Definition;
                if (action == null)
                    continue;

                ValidateEffectTypeList(action.Id, action.Effects, entry.FilePath, registered, report);

                if (action.Variants != null)
                {
                    foreach (var variant in action.Variants)
                    {
                        var variantId = variant?.VariantId ?? "(variant)";
                        ValidateEffectTypeList(
                            $"{action.Id}:{variantId}",
                            variant?.AdditionalEffects,
                            entry.FilePath,
                            registered,
                            report);
                    }
                }
            }
        }

        private static void ValidateEffectTypeList(
            string ownerId,
            List<EffectDefinition> effects,
            string filePath,
            HashSet<string> registered,
            ParityValidationReport report)
        {
            if (effects == null)
                return;

            foreach (var effect in effects)
            {
                if (effect == null || string.IsNullOrWhiteSpace(effect.Type))
                    continue;

                var effectType = effect.Type.Trim();
                if (!registered.Contains(effectType))
                {
                    report.AddError($"Effect type '{effectType}' used by '{ownerId}' is not registered in EffectPipeline ({filePath}).");
                }
            }
        }

        /// <summary>
        /// Validate that no canonical scenarios reference summon actions.
        /// Summon actions are identified by IsSummon flag or by having an effect with type "summon".
        /// </summary>
        private static void ValidateNoSummonActionsInScenarios(
            IReadOnlyCollection<ScenarioEntry> scenarioEntries,
            IReadOnlyCollection<ActionEntry> actionEntries,
            ParityValidationReport report)
        {
            // Build set of summon action IDs
            var summonActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in actionEntries)
            {
                if (IsSummonAction(entry.Definition))
                {
                    summonActionIds.Add(entry.Definition.Id);
                }
            }

            // Check each scenario
            foreach (var scenarioEntry in scenarioEntries)
            {
                var scenario = scenarioEntry.Definition;
                if (scenario.Units == null)
                    continue;

                foreach (var unit in scenario.Units)
                {
                    // Check KnownActions for summon actions
                    if (unit.KnownActions != null)
                    {
                        foreach (var actionId in unit.KnownActions)
                        {
                            if (summonActionIds.Contains(actionId))
                            {
                                report.AddError(
                                    $"Scenario '{scenario.Name}' unit '{unit.Name}' references forbidden summon action '{actionId}' ({scenarioEntry.FilePath}).");
                            }
                        }
                    }

                    // TODO: Also check class/race/feat progressions for summon actions
                    // This would require loading class/race/feat definitions and checking their granted abilities
                }
            }
        }

        /// <summary>
        /// Determines if an action is a summon action.
        /// An action is considered a summon if:
        /// 1. IsSummon flag is explicitly set to true, OR
        /// 2. It has an effect with type "summon"
        /// </summary>
        private static bool IsSummonAction(ActionDefinition action)
        {
            if (action == null)
                return false;

            // Check explicit flag
            if (action.IsSummon)
                return true;

            // Check for summon effect
            if (action.Effects != null)
            {
                foreach (var effect in action.Effects)
                {
                    if (effect?.Type?.Trim().Equals("summon", StringComparison.OrdinalIgnoreCase) == true)
                        return true;
                }
            }

            return false;
        }

        private T DeserializeFromFile<T>(string filePath, ParityValidationReport report, string schemaLabel) where T : class
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var result = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                if (result == null)
                {
                    report.AddError($"Schema mismatch ({schemaLabel}) in {filePath}: deserialized null.");
                }

                return result;
            }
            catch (Exception ex)
            {
                report.AddError($"Schema mismatch ({schemaLabel}) in {filePath}: {ex.Message}");
                return null;
            }
        }

        private static List<string> GetJsonFiles(string directory)
        {
            if (!Directory.Exists(directory))
                return new List<string>();

            return Directory
                .GetFiles(directory, "*.json")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed class ParityAllowlist
        {
            public Dictionary<string, HashSet<string>> AllowDuplicateIds { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> AllowMissingGrantedAbilities { get; } =
                new(StringComparer.OrdinalIgnoreCase);

            public HashSet<string> AllowMissingStatusIds { get; } =
                new(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ParityAllowlistFile
        {
            public Dictionary<string, List<string>> AllowDuplicateIds { get; set; }
            public List<string> AllowMissingGrantedAbilities { get; set; }
            public List<string> AllowMissingStatusIds { get; set; }
        }

        private sealed record IdRef(string Id, string FilePath, string Label);
        private sealed record GrantedAbilityRef(string FilePath, string Owner, string FeatureId, string ActionId);
        private sealed record ActionEntry(string FilePath, ActionDefinition Definition);
        private sealed record StatusEntry(string FilePath, QDND.Combat.Statuses.StatusDefinition Definition);
        private sealed record RaceEntry(string FilePath, RaceDefinition Definition);
        private sealed record ClassEntry(string FilePath, ClassDefinition Definition);
        private sealed record FeatEntry(string FilePath, FeatDefinition Definition);
        private sealed record BeastEntry(string FilePath, BeastForm Definition);
        private sealed record ScenarioEntry(string FilePath, ScenarioDefinition Definition);
        private sealed record WeaponEntry(string FilePath, WeaponDefinition Definition);
        private sealed record ArmorEntry(string FilePath, ArmorDefinition Definition);

        private sealed class EquipmentEntries
        {
            public List<WeaponEntry> WeaponEntries { get; } = new();
            public List<ArmorEntry> ArmorEntries { get; } = new();
        }
    }

    /// <summary>
    /// Coverage inventory showing action registry coverage across scenarios.
    /// </summary>
    internal sealed class CoverageInventory
    {
        /// <summary>
        /// Total unique actions granted across all scenarios.
        /// </summary>
        public int TotalGrantedActions { get; set; }

        /// <summary>
        /// Number of actions available in Data/Actions registry.
        /// </summary>
        public int ActionsInDataRegistry { get; set; }

        /// <summary>
        /// Number of actions marked as forbidden summons.
        /// </summary>
        public int ForbiddenSummonActions { get; set; }

        /// <summary>
        /// Number of actions granted in scenarios but missing from registries.
        /// </summary>
        public int MissingActions { get; set; }

        /// <summary>
        /// IDs of actions granted but missing from registries.
        /// </summary>
        public List<string> MissingActionIds { get; set; } = new();

        /// <summary>
        /// Number of summon actions granted in scenarios (should be 0).
        /// </summary>
        public int GrantedSummonActions { get; set; }

        /// <summary>
        /// IDs of summon actions granted in scenarios.
        /// </summary>
        public List<string> GrantedSummonActionIds { get; set; } = new();
    }
}
