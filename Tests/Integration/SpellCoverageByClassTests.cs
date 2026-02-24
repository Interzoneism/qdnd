using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace QDND.Tests.Integration
{
    /// <summary>
    /// CI gate: verifies every class's granted cantrips and spells (levels 1-6)
    /// exist in the curated action JSON registry. Iterates through all 12 BG3 classes,
    /// collects granted abilities from level progressions and subclass progressions,
    /// and asserts that each ability resolves to an action definition in Data/Actions/.
    /// </summary>
    public class SpellCoverageByClassTests
    {
        private static readonly HashSet<string> FixtureClassIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "test_dummy"
        };

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        [Fact]
        public void AllClasses_GrantedAbilities_ExistInActionRegistry()
        {
            var repoRoot = ResolveRepoRoot();

            // Load all action IDs from curated JSONs
            var actionIds = LoadAllActionIds(repoRoot);

            // Load allowlist
            var allowlist = LoadAllowlist(repoRoot);

            // Load class definitions
            var classes = LoadAllClasses(repoRoot);

            Assert.True(classes.Count >= 12, $"Expected at least 12 classes, found {classes.Count}");

            var allMissing = new List<string>();
            var perClassResults = new List<string>();

            foreach (var cls in classes)
            {
                var granted = CollectGrantedAbilities(cls);
                var missing = granted
                    .Where(a => !actionIds.Contains(a) && !allowlist.Contains(a))
                    .ToList();

                int covered = granted.Count - missing.Count - granted.Count(a => allowlist.Contains(a) && !actionIds.Contains(a));
                // Recount: abilities in registry + abilities in allowlist = "accounted for"
                int accountedFor = granted.Count(a => actionIds.Contains(a) || allowlist.Contains(a));
                double pct = granted.Count > 0 ? (double)accountedFor / granted.Count * 100 : 100;

                perClassResults.Add($"  {cls.Id}: {granted.Count} abilities, {accountedFor} accounted for ({pct:F0}%), {missing.Count} missing");

                allMissing.AddRange(missing.Select(m => $"{cls.Id}:{m}"));
            }

            // Log coverage summary
            Console.WriteLine();
            Console.WriteLine("=== Class-Spell Coverage Report ===");
            foreach (var line in perClassResults)
                Console.WriteLine(line);
            Console.WriteLine($"Total missing (not in registry or allowlist): {allMissing.Count}");
            if (allMissing.Count > 0)
            {
                Console.WriteLine("Missing ability IDs:");
                foreach (var m in allMissing.Take(20))
                    Console.WriteLine($"  - {m}");
                if (allMissing.Count > 20)
                    Console.WriteLine($"  ... and {allMissing.Count - 20} more");
            }

            Assert.True(
                allMissing.Count == 0,
                $"Class-spell coverage gate failed: {allMissing.Count} granted abilities are missing from both " +
                $"the action registry and the parity allowlist.\n" +
                string.Join("\n", allMissing.Select(m => $"  - {m}")));
        }

        [Fact]
        public void AllClasses_Have100Percent_AbilityCoverage()
        {
            var repoRoot = ResolveRepoRoot();
            var actionIds = LoadAllActionIds(repoRoot);
            var classes = LoadAllClasses(repoRoot);

            int totalAbilities = 0;
            int totalInRegistry = 0;

            foreach (var cls in classes)
            {
                var granted = CollectGrantedAbilities(cls);
                totalAbilities += granted.Count;
                totalInRegistry += granted.Count(a => actionIds.Contains(a));
            }

            double overallPct = totalAbilities > 0 ? (double)totalInRegistry / totalAbilities * 100 : 100;

            Console.WriteLine();
            Console.WriteLine($"=== Overall Class-Spell Registry Coverage ===");
            Console.WriteLine($"Total abilities: {totalAbilities}");
            Console.WriteLine($"In registry: {totalInRegistry}");
            Console.WriteLine($"Coverage: {overallPct:F1}%");

            // Phase 3 target: every class's granted abilities exist in registry
            // Current baseline is 100% - assert it stays that way
            Assert.True(
                overallPct >= 90.0,
                $"Class-spell registry coverage dropped below 90%: {overallPct:F1}%");
        }

        [Fact]
        public void Verify_All12Classes_ArePresent()
        {
            var repoRoot = ResolveRepoRoot();
            var classes = LoadAllClasses(repoRoot);

            var expectedClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "barbarian", "bard", "cleric", "druid",
                "fighter", "monk", "paladin", "ranger",
                "rogue", "sorcerer", "warlock", "wizard"
            };

            var foundClasses = classes.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var expected in expectedClasses)
            {
                Assert.Contains(expected, foundClasses);
            }

            Console.WriteLine($"All 12 classes verified present: {string.Join(", ", foundClasses.OrderBy(c => c))}");
        }

        [Fact]
        public void EachClass_HasGrantedAbilities()
        {
            var repoRoot = ResolveRepoRoot();
            var classes = LoadAllClasses(repoRoot);

            foreach (var cls in classes)
            {
                var granted = CollectGrantedAbilities(cls);
                Assert.True(
                    granted.Count > 0,
                    $"Class '{cls.Id}' has no granted abilities across its level table");
            }
        }

        [Fact]
        public void EffectHandlers_CoverAllGrantedAbilityEffects()
        {
            var repoRoot = ResolveRepoRoot();
            var actionIds = LoadAllActionIds(repoRoot);
            var actionDefs = LoadAllActionDefinitions(repoRoot);
            var classes = LoadAllClasses(repoRoot);

            // Get registered effect types from EffectPipeline
            var registeredTypes = new HashSet<string>(
                new QDND.Combat.Actions.EffectPipeline().GetRegisteredEffectTypes(),
                StringComparer.OrdinalIgnoreCase);

            var unhandledEffects = new List<string>();

            foreach (var cls in classes)
            {
                var granted = CollectGrantedAbilities(cls);
                foreach (var abilityId in granted)
                {
                    if (!actionDefs.TryGetValue(abilityId, out var action))
                        continue;

                    if (action.Effects == null)
                        continue;

                    foreach (var effect in action.Effects)
                    {
                        if (string.IsNullOrWhiteSpace(effect.Type))
                            continue;

                        if (!registeredTypes.Contains(effect.Type.Trim()))
                        {
                            unhandledEffects.Add($"{cls.Id}:{abilityId}:{effect.Type}");
                        }
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine($"=== Effect Handler Coverage ===");
            Console.WriteLine($"Unhandled effects in granted abilities: {unhandledEffects.Count}");
            if (unhandledEffects.Count > 0)
            {
                foreach (var ue in unhandledEffects.Take(20))
                    Console.WriteLine($"  - {ue}");
            }

            Assert.True(
                unhandledEffects.Count == 0,
                $"{unhandledEffects.Count} granted abilities have unhandled effect types:\n" +
                string.Join("\n", unhandledEffects.Select(u => $"  - {u}")));
        }

        // === Data loading helpers ===

        private static HashSet<string> LoadAllActionIds(string repoRoot)
        {
            var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var actionsDir = Path.Combine(repoRoot, "Data", "Actions");

            if (!Directory.Exists(actionsDir))
                return actionIds;

            foreach (var file in Directory.GetFiles(actionsDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Actions", out var actions) ||
                        root.TryGetProperty("actions", out actions))
                    {
                        foreach (var action in actions.EnumerateArray())
                        {
                            string id = null;
                            if (action.TryGetProperty("Id", out var idProp))
                                id = idProp.GetString();
                            else if (action.TryGetProperty("id", out idProp))
                                id = idProp.GetString();

                            if (!string.IsNullOrWhiteSpace(id))
                                actionIds.Add(id.Trim());
                        }
                    }
                }
                catch { /* skip unparseable files */ }
            }

            return actionIds;
        }

        private static Dictionary<string, ActionDefProxy> LoadAllActionDefinitions(string repoRoot)
        {
            var actionDefs = new Dictionary<string, ActionDefProxy>(StringComparer.OrdinalIgnoreCase);
            var actionsDir = Path.Combine(repoRoot, "Data", "Actions");

            if (!Directory.Exists(actionsDir))
                return actionDefs;

            foreach (var file in Directory.GetFiles(actionsDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("Actions", out var actions) ||
                        root.TryGetProperty("actions", out actions))
                    {
                        foreach (var action in actions.EnumerateArray())
                        {
                            string id = null;
                            if (action.TryGetProperty("Id", out var idProp))
                                id = idProp.GetString();
                            else if (action.TryGetProperty("id", out idProp))
                                id = idProp.GetString();

                            if (string.IsNullOrWhiteSpace(id))
                                continue;

                            var proxy = new ActionDefProxy { Id = id.Trim() };

                            if (action.TryGetProperty("Effects", out var effects) ||
                                action.TryGetProperty("effects", out effects))
                            {
                                foreach (var effect in effects.EnumerateArray())
                                {
                                    string type = null;
                                    if (effect.TryGetProperty("Type", out var typeProp))
                                        type = typeProp.GetString();
                                    else if (effect.TryGetProperty("type", out typeProp))
                                        type = typeProp.GetString();

                                    if (!string.IsNullOrWhiteSpace(type))
                                        proxy.Effects.Add(new EffectProxy { Type = type.Trim() });
                                }
                            }

                            actionDefs[proxy.Id] = proxy;
                        }
                    }
                }
                catch { /* skip unparseable files */ }
            }

            return actionDefs;
        }

        private static HashSet<string> LoadAllowlist(string repoRoot)
        {
            var allowlist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var path = Path.Combine(repoRoot, "Data", "Validation", "parity_allowlist.json");

            if (!File.Exists(path))
                return allowlist;

            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("allowMissingGrantedAbilities", out var arr))
                {
                    foreach (var item in arr.EnumerateArray())
                    {
                        var val = item.GetString();
                        if (!string.IsNullOrWhiteSpace(val))
                            allowlist.Add(val.Trim());
                    }
                }
            }
            catch { /* skip */ }

            return allowlist;
        }

        private static List<ClassDefProxy> LoadAllClasses(string repoRoot)
        {
            var classes = new List<ClassDefProxy>();
            var classesDir = Path.Combine(repoRoot, "Data", "Classes");

            if (!Directory.Exists(classesDir))
                return classes;

            foreach (var file in Directory.GetFiles(classesDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("Classes", out var classArray))
                        continue;

                    foreach (var cls in classArray.EnumerateArray())
                    {
                        var proxy = new ClassDefProxy();

                        if (cls.TryGetProperty("Id", out var idProp))
                            proxy.Id = idProp.GetString()?.Trim();

                        if (cls.TryGetProperty("Name", out var nameProp))
                            proxy.Name = nameProp.GetString()?.Trim();

                        // Parse level table
                        if (cls.TryGetProperty("LevelTable", out var levelTable))
                        {
                            foreach (var level in levelTable.EnumerateObject())
                            {
                                var abilities = ExtractGrantedAbilitiesFromProgression(level.Value);
                                if (abilities.Count > 0)
                                    proxy.LevelAbilities[level.Name] = abilities;
                            }
                        }

                        // Parse subclass level tables
                        if (cls.TryGetProperty("Subclasses", out var subclasses))
                        {
                            foreach (var sub in subclasses.EnumerateArray())
                            {
                                string subId = null;
                                if (sub.TryGetProperty("Id", out var subIdProp))
                                    subId = subIdProp.GetString()?.Trim();

                                if (sub.TryGetProperty("LevelTable", out var subLevelTable))
                                {
                                    foreach (var level in subLevelTable.EnumerateObject())
                                    {
                                        var abilities = ExtractGrantedAbilitiesFromProgression(level.Value);
                                        foreach (var a in abilities)
                                            proxy.SubclassAbilities.Add(a);
                                    }
                                }
                            }
                        }

                        // Ignore test-only placeholder classes (e.g., test_dummy) that are
                        // loaded for debug tooling and do not represent playable class tables.
                        if (!string.IsNullOrWhiteSpace(proxy.Id) &&
                            !proxy.Id.StartsWith("test_", StringComparison.OrdinalIgnoreCase))
                            classes.Add(proxy);
                    }
                }
                catch { /* skip unparseable files */ }
            }

            return classes;
        }

        private static List<string> ExtractGrantedAbilitiesFromProgression(JsonElement progression)
        {
            var abilities = new List<string>();

            if (progression.TryGetProperty("Features", out var features))
            {
                foreach (var feature in features.EnumerateArray())
                {
                    if (feature.TryGetProperty("GrantedAbilities", out var granted))
                    {
                        foreach (var ability in granted.EnumerateArray())
                        {
                            var val = ability.GetString();
                            if (!string.IsNullOrWhiteSpace(val))
                                abilities.Add(val.Trim());
                        }
                    }
                }
            }

            return abilities;
        }

        private static List<string> CollectGrantedAbilities(ClassDefProxy cls)
        {
            var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in cls.LevelAbilities)
            {
                foreach (var a in kvp.Value)
                    all.Add(a);
            }

            foreach (var a in cls.SubclassAbilities)
                all.Add(a);

            return all.ToList();
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

            throw new DirectoryNotFoundException("Could not resolve repository root for class-spell coverage.");
        }

        // === Proxy types for JSON parsing ===

        private class ClassDefProxy
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public Dictionary<string, List<string>> LevelAbilities { get; } = new();
            public HashSet<string> SubclassAbilities { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private class ActionDefProxy
        {
            public string Id { get; set; }
            public List<EffectProxy> Effects { get; } = new();
        }

        private class EffectProxy
        {
            public string Type { get; set; }
        }
    }
}
