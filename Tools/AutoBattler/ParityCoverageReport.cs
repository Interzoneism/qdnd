using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Parity coverage report generated from combat log analysis.
    /// Tracks which abilities were used, which effects were handled, and parity metrics.
    /// </summary>
    public class ParityCoverageReport
    {
        [JsonPropertyName("total_abilities_granted")]
        public int TotalAbilitiesGranted { get; set; }

        [JsonPropertyName("total_abilities_attempted")]
        public int TotalAbilitiesAttempted { get; set; }

        [JsonPropertyName("total_abilities_succeeded")]
        public int TotalAbilitiesSucceeded { get; set; }

        [JsonPropertyName("total_abilities_with_unhandled_effects")]
        public int TotalAbilitiesWithUnhandledEffects { get; set; }

        [JsonPropertyName("total_statuses_applied")]
        public int TotalStatusesApplied { get; set; }

        [JsonPropertyName("total_statuses_with_no_runtime_behavior")]
        public int TotalStatusesWithNoRuntimeBehavior { get; set; }

        [JsonPropertyName("total_surfaces_created")]
        public int TotalSurfacesCreated { get; set; }

        [JsonPropertyName("total_damage_events")]
        public int TotalDamageEvents { get; set; }

        [JsonPropertyName("ability_coverage_pct")]
        public double AbilityCoveragePct { get; set; }

        [JsonPropertyName("effect_handling_pct")]
        public double EffectHandlingPct { get; set; }

        [JsonPropertyName("unhandled_effect_types")]
        public List<string> UnhandledEffectTypes { get; set; } = new();

        [JsonPropertyName("abilities_never_attempted")]
        public List<string> AbilitiesNeverAttempted { get; set; } = new();

        [JsonPropertyName("abilities_always_failed")]
        public List<string> AbilitiesAlwaysFailed { get; set; } = new();

        /// <summary>
        /// Generate a parity coverage report from a combat log file.
        /// </summary>
        public static ParityCoverageReport GenerateFromLog(string logFilePath)
        {
            var report = new ParityCoverageReport();

            if (!File.Exists(logFilePath))
            {
                return report;
            }

            var grantedAbilities = new HashSet<string>();
            var attemptedAbilities = new HashSet<string>();
            var succeededAbilities = new HashSet<string>();
            var failedAbilities = new HashSet<string>();
            var abilitiesWithUnhandledEffects = new HashSet<string>();
            var unhandledEffectTypes = new HashSet<string>();
            var abilitySuccessCounts = new Dictionary<string, int>();
            var abilityFailureCounts = new Dictionary<string, int>();

            int statusesApplied = 0;
            int statusesWithNoRuntimeBehavior = 0;
            int surfacesCreated = 0;
            int damageEvents = 0;

            // Parse JSONL file line by line
            foreach (var line in File.ReadLines(logFilePath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("event", out var eventProp))
                        continue;

                    string eventType = eventProp.GetString();

                    switch (eventType)
                    {
                        case "BATTLE_START":
                            // Extract granted abilities from units
                            if (root.TryGetProperty("units", out var units) && units.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var unit in units.EnumerateArray())
                                {
                                    if (unit.TryGetProperty("abilities", out var abilities) && 
                                        abilities.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var ability in abilities.EnumerateArray())
                                        {
                                            string abilityId = ability.GetString();
                                            if (!string.IsNullOrEmpty(abilityId))
                                            {
                                                grantedAbilities.Add(abilityId);
                                            }
                                        }
                                    }
                                }
                            }
                            break;

                        case "ACTION_RESULT":
                            // Track attempted and succeeded abilities
                            if (root.TryGetProperty("ability_id", out var abilityIdProp))
                            {
                                string abilityId = abilityIdProp.GetString();
                                if (!string.IsNullOrEmpty(abilityId))
                                {
                                    attemptedAbilities.Add(abilityId);

                                    bool success = root.TryGetProperty("success", out var successProp) && 
                                                   successProp.GetBoolean();

                                    if (success)
                                    {
                                        succeededAbilities.Add(abilityId);
                                        abilitySuccessCounts[abilityId] = abilitySuccessCounts.GetValueOrDefault(abilityId) + 1;
                                    }
                                    else
                                    {
                                        failedAbilities.Add(abilityId);
                                        abilityFailureCounts[abilityId] = abilityFailureCounts.GetValueOrDefault(abilityId) + 1;
                                    }
                                }
                            }
                            break;

                        case "EFFECT_UNHANDLED":
                            // Track unhandled effects
                            if (root.TryGetProperty("ability_id", out var effectAbilityProp))
                            {
                                string abilityId = effectAbilityProp.GetString();
                                if (!string.IsNullOrEmpty(abilityId))
                                {
                                    abilitiesWithUnhandledEffects.Add(abilityId);
                                }
                            }

                            if (root.TryGetProperty("effect_type", out var effectTypeProp))
                            {
                                string effectType = effectTypeProp.GetString();
                                if (!string.IsNullOrEmpty(effectType))
                                {
                                    unhandledEffectTypes.Add(effectType);
                                }
                            }
                            break;

                        case "STATUS_APPLIED":
                            statusesApplied++;
                            break;

                        case "STATUS_NO_RUNTIME_BEHAVIOR":
                            statusesWithNoRuntimeBehavior++;
                            break;

                        case "SURFACE_CREATED":
                            surfacesCreated++;
                            break;

                        case "DAMAGE_DEALT":
                            damageEvents++;
                            break;
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                    continue;
                }
            }

            // Identify abilities that were attempted but always failed
            var alwaysFailed = attemptedAbilities
                .Where(a => !succeededAbilities.Contains(a))
                .ToList();

            // Identify abilities that were granted but never attempted
            var neverAttempted = grantedAbilities
                .Except(attemptedAbilities)
                .ToList();

            // Calculate percentages
            double coveragePct = grantedAbilities.Count > 0
                ? (double)attemptedAbilities.Count / grantedAbilities.Count
                : 0.0;

            double effectHandlingPct = attemptedAbilities.Count > 0
                ? 1.0 - ((double)abilitiesWithUnhandledEffects.Count / attemptedAbilities.Count)
                : 0.0;

            // Build report
            report.TotalAbilitiesGranted = grantedAbilities.Count;
            report.TotalAbilitiesAttempted = attemptedAbilities.Count;
            report.TotalAbilitiesSucceeded = succeededAbilities.Count;
            report.TotalAbilitiesWithUnhandledEffects = abilitiesWithUnhandledEffects.Count;
            report.TotalStatusesApplied = statusesApplied;
            report.TotalStatusesWithNoRuntimeBehavior = statusesWithNoRuntimeBehavior;
            report.TotalSurfacesCreated = surfacesCreated;
            report.TotalDamageEvents = damageEvents;
            report.AbilityCoveragePct = coveragePct;
            report.EffectHandlingPct = effectHandlingPct;
            report.UnhandledEffectTypes = unhandledEffectTypes.OrderBy(x => x).ToList();
            report.AbilitiesNeverAttempted = neverAttempted.OrderBy(x => x).ToList();
            report.AbilitiesAlwaysFailed = alwaysFailed.OrderBy(x => x).ToList();

            return report;
        }

        /// <summary>
        /// Serialize report to JSON string.
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
        /// Print a human-readable summary to console (safe for testhost).
        /// </summary>
        public void PrintSummary()
        {
            Console.WriteLine("");
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           PARITY COVERAGE REPORT                       ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine("");
            Console.WriteLine($"  Abilities Granted:      {TotalAbilitiesGranted}");
            Console.WriteLine($"  Abilities Attempted:    {TotalAbilitiesAttempted}");
            Console.WriteLine($"  Abilities Succeeded:    {TotalAbilitiesSucceeded}");
            Console.WriteLine($"  Coverage:               {AbilityCoveragePct:P1}");
            Console.WriteLine("");
            Console.WriteLine($"  Unhandled Effects:      {TotalAbilitiesWithUnhandledEffects} abilities");
            Console.WriteLine($"  Effect Handling:        {EffectHandlingPct:P1}");
            Console.WriteLine($"  Statuses Applied:       {TotalStatusesApplied}");
            Console.WriteLine($"  Surfaces Created:       {TotalSurfacesCreated}");
            Console.WriteLine($"  Damage Events:          {TotalDamageEvents}");
            Console.WriteLine("");

            if (UnhandledEffectTypes.Count > 0)
            {
                Console.WriteLine("  Unhandled Effect Types:");
                foreach (var effect in UnhandledEffectTypes.Take(10))
                {
                    Console.WriteLine($"    - {effect}");
                }
                if (UnhandledEffectTypes.Count > 10)
                {
                    Console.WriteLine($"    ... and {UnhandledEffectTypes.Count - 10} more");
                }
                Console.WriteLine("");
            }

            if (AbilitiesNeverAttempted.Count > 0)
            {
                Console.WriteLine($"  Abilities Never Attempted: {AbilitiesNeverAttempted.Count}");
                foreach (var ability in AbilitiesNeverAttempted.Take(10))
                {
                    Console.WriteLine($"    - {ability}");
                }
                if (AbilitiesNeverAttempted.Count > 10)
                {
                    Console.WriteLine($"    ... and {AbilitiesNeverAttempted.Count - 10} more");
                }
                Console.WriteLine("");
            }

            if (AbilitiesAlwaysFailed.Count > 0)
            {
                Console.WriteLine($"  Abilities Always Failed: {AbilitiesAlwaysFailed.Count}");
                foreach (var ability in AbilitiesAlwaysFailed.Take(10))
                {
                    Console.WriteLine($"    - {ability}");
                }
                if (AbilitiesAlwaysFailed.Count > 10)
                {
                    Console.WriteLine($"    ... and {AbilitiesAlwaysFailed.Count - 10} more");
                }
            }

            Console.WriteLine("");
        }
    }
}
