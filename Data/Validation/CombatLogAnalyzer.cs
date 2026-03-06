using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QDND.Data.Validation
{
    /// <summary>
    /// Analyzes a combat_log.jsonl file and extracts spell verification data.
    /// Pure .NET — no Godot dependencies.
    /// </summary>
    public static class CombatLogAnalyzer
    {
        public class VerificationSummary
        {
            public string ActionId { get; set; }
            public bool ActionWasUsed { get; set; }
            public int TimesUsed { get; set; }
            public bool BattleCompleted { get; set; }
            public string Winner { get; set; }

            // Damage analysis
            public List<DamageInstance> DamageInstances { get; set; } = new();
            public int TotalDamageDealt { get; set; }
            public int MinDamageRoll { get; set; }
            public int MaxDamageRoll { get; set; }
            public List<string> DamageTypes { get; set; } = new();

            // Save analysis
            public List<SaveInstance> SaveInstances { get; set; } = new();
            public int SavesTriggered { get; set; }
            public int SavesPassed { get; set; }
            public int SavesFailed { get; set; }
            public string SaveAbility { get; set; }

            // Status analysis
            public List<StatusInstance> StatusesApplied { get; set; } = new();

            // Targeting analysis
            public int TargetsHit { get; set; }
            public List<string> TargetIds { get; set; } = new();

            // Attack roll analysis
            public List<AttackRollInstance> AttackRolls { get; set; } = new();
            public int AttacksMade { get; set; }
            public int AttacksHit { get; set; }
            public int AttacksMissed { get; set; }

            // Healing analysis
            public int TotalHealing { get; set; }
            public List<HealInstance> HealInstances { get; set; } = new();

            // Concentration
            public bool ConcentrationStarted { get; set; }

            // Raw events for agent inspection
            public List<string> ActionDetailEvents { get; set; } = new();
            public List<string> RelevantEvents { get; set; } = new();

            // Errors/warnings
            public List<string> Warnings { get; set; } = new();
        }

        public class DamageInstance
        {
            public string TargetId { get; set; }
            public int Amount { get; set; }
            public string DamageType { get; set; }
            public bool WasCrit { get; set; }
            public bool WasHalfDamage { get; set; }
        }

        public class SaveInstance
        {
            public string TargetId { get; set; }
            public string Ability { get; set; }
            public int DC { get; set; }
            public int Roll { get; set; }
            public bool Passed { get; set; }
        }

        public class StatusInstance
        {
            public string TargetId { get; set; }
            public string StatusId { get; set; }
            public string Source { get; set; }
            public int? Duration { get; set; }
        }

        public class AttackRollInstance
        {
            public string TargetId { get; set; }
            public int Roll { get; set; }
            public int Total { get; set; }
            public int TargetAC { get; set; }
            public bool Hit { get; set; }
            public bool WasCrit { get; set; }
        }

        public class HealInstance
        {
            public string TargetId { get; set; }
            public int Amount { get; set; }
        }

        /// <summary>
        /// Analyzes combat log for a specific action's verification data.
        /// </summary>
        /// <param name="logPath">Path to combat_log.jsonl</param>
        /// <param name="actionId">The action ID to analyze</param>
        /// <returns>Structured verification summary</returns>
        public static VerificationSummary Analyze(string logPath, string actionId)
        {
            var summary = new VerificationSummary { ActionId = actionId };

            if (!File.Exists(logPath))
            {
                summary.Warnings.Add($"Log file not found: {logPath}");
                return summary;
            }

            var lines = File.ReadAllLines(logPath);
            var options = new JsonDocumentOptions { AllowTrailingCommas = true };

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(line, options);
                    var root = doc.RootElement;

                    if (!root.TryGetProperty("event", out var eventProp)) continue;
                    string eventType = eventProp.GetString();

                    switch (eventType)
                    {
                        case "BATTLE_END":
                            summary.BattleCompleted = true;
                            if (root.TryGetProperty("winner", out var winnerProp))
                                summary.Winner = winnerProp.GetString();
                            break;

                        case "ACTION_RESULT":
                            ProcessActionResult(root, actionId, summary, line);
                            break;

                        case "ACTION_DETAIL":
                            ProcessActionDetail(root, actionId, summary, line);
                            break;

                        case "DAMAGE_DEALT":
                            ProcessDamageDealt(root, actionId, summary);
                            break;

                        case "STATUS_APPLIED":
                            ProcessStatusApplied(root, actionId, summary);
                            break;

                        case "DECISION":
                            ProcessDecision(root, actionId, summary);
                            break;
                    }
                }
                catch (JsonException)
                {
                    // Skip malformed lines
                }
            }

            // Compute aggregates
            if (summary.DamageInstances.Count > 0)
            {
                summary.TotalDamageDealt = summary.DamageInstances.Sum(d => d.Amount);
                summary.MinDamageRoll = summary.DamageInstances.Min(d => d.Amount);
                summary.MaxDamageRoll = summary.DamageInstances.Max(d => d.Amount);
                summary.DamageTypes = summary.DamageInstances.Select(d => d.DamageType).Where(t => t != null).Distinct().ToList();
                summary.TargetsHit = summary.DamageInstances.Select(d => d.TargetId).Distinct().Count();
                summary.TargetIds = summary.DamageInstances.Select(d => d.TargetId).Distinct().ToList();
            }

            if (summary.SaveInstances.Count > 0)
            {
                summary.SavesTriggered = summary.SaveInstances.Count;
                summary.SavesPassed = summary.SaveInstances.Count(s => s.Passed);
                summary.SavesFailed = summary.SaveInstances.Count(s => !s.Passed);
                summary.SaveAbility = summary.SaveInstances.FirstOrDefault()?.Ability;
            }

            if (summary.AttackRolls.Count > 0)
            {
                summary.AttacksMade = summary.AttackRolls.Count;
                summary.AttacksHit = summary.AttackRolls.Count(a => a.Hit);
                summary.AttacksMissed = summary.AttackRolls.Count(a => !a.Hit);
            }

            if (summary.HealInstances.Count > 0)
            {
                summary.TotalHealing = summary.HealInstances.Sum(h => h.Amount);
            }

            return summary;
        }

        private static void ProcessActionResult(JsonElement root, string actionId, VerificationSummary summary, string rawLine)
        {
            // Match by ability_id or action field
            string abilityId = GetStringProp(root, "ability_id");
            string action = GetStringProp(root, "action");

            if (string.Equals(abilityId, actionId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, actionId, StringComparison.OrdinalIgnoreCase) ||
                (action != null && action.Contains(actionId, StringComparison.OrdinalIgnoreCase)))
            {
                summary.ActionWasUsed = true;
                summary.TimesUsed++;
                summary.RelevantEvents.Add(rawLine);
            }
        }

        private static void ProcessActionDetail(JsonElement root, string actionId, VerificationSummary summary, string rawLine)
        {
            string abilityId = GetStringProp(root, "ability_id");
            string action = GetStringProp(root, "action");

            bool isRelevant = string.Equals(abilityId, actionId, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(action, actionId, StringComparison.OrdinalIgnoreCase) ||
                              (action != null && action.Contains(actionId, StringComparison.OrdinalIgnoreCase));

            if (!isRelevant) return;

            summary.ActionDetailEvents.Add(rawLine);

            // Extract details sub-object
            if (!root.TryGetProperty("details", out var details)) return;

            // Damage
            if (details.TryGetProperty("damage_dealt", out var damageProp))
            {
                // Could be a number or an array of damage entries
                if (damageProp.ValueKind == JsonValueKind.Number)
                {
                    int dmg = damageProp.GetInt32();
                    if (dmg > 0)
                    {
                        string targetId = GetStringProp(root, "target") ?? GetStringProp(root, "targets");
                        string dmgType = GetStringProp(details, "damage_type");
                        summary.DamageInstances.Add(new DamageInstance
                        {
                            TargetId = targetId,
                            Amount = dmg,
                            DamageType = dmgType
                        });
                    }
                }
                else if (damageProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in damageProp.EnumerateArray())
                    {
                        int dmg = entry.TryGetProperty("amount", out var amt) ? amt.GetInt32() : 0;
                        string targetId = GetStringProp(entry, "target");
                        string dmgType = GetStringProp(entry, "damage_type");
                        if (dmg > 0)
                        {
                            summary.DamageInstances.Add(new DamageInstance
                            {
                                TargetId = targetId,
                                Amount = dmg,
                                DamageType = dmgType
                            });
                        }
                    }
                }
            }

            // Saving throw
            if (details.TryGetProperty("saving_throw", out var saveProp))
            {
                if (saveProp.ValueKind == JsonValueKind.Object)
                {
                    ProcessSaveObject(saveProp, summary);
                }
                else if (saveProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var entry in saveProp.EnumerateArray())
                    {
                        ProcessSaveObject(entry, summary);
                    }
                }
            }

            // Attack roll
            if (details.TryGetProperty("attack_roll", out var atkProp) && atkProp.ValueKind == JsonValueKind.Object)
            {
                int roll = atkProp.TryGetProperty("natural_roll", out var nr) ? nr.GetInt32() : 0;
                int total = atkProp.TryGetProperty("total", out var totProp) ? totProp.GetInt32() : 0;
                int ac = atkProp.TryGetProperty("target_ac", out var acProp) ? acProp.GetInt32() : 0;
                bool hit = atkProp.TryGetProperty("hit", out var hitProp) && hitProp.GetBoolean();
                bool crit = atkProp.TryGetProperty("critical", out var critProp) && critProp.GetBoolean();
                string targetId = GetStringProp(root, "target");

                summary.AttackRolls.Add(new AttackRollInstance
                {
                    TargetId = targetId,
                    Roll = roll,
                    Total = total,
                    TargetAC = ac,
                    Hit = hit,
                    WasCrit = crit
                });
            }

            // Healing
            if (details.TryGetProperty("healing", out var healProp))
            {
                int heal = healProp.ValueKind == JsonValueKind.Number ? healProp.GetInt32() : 0;
                if (heal > 0)
                {
                    string targetId = GetStringProp(root, "target");
                    summary.HealInstances.Add(new HealInstance { TargetId = targetId, Amount = heal });
                }
            }

            // Concentration
            if (details.TryGetProperty("concentration", out var concProp) && concProp.GetBoolean())
            {
                summary.ConcentrationStarted = true;
            }

            // Statuses from detail
            if (details.TryGetProperty("statuses_applied", out var statusesProp) &&
                statusesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var status in statusesProp.EnumerateArray())
                {
                    string sid = status.ValueKind == JsonValueKind.String ? status.GetString() :
                                 GetStringProp(status, "status_id");
                    if (sid != null)
                    {
                        summary.StatusesApplied.Add(new StatusInstance
                        {
                            StatusId = sid,
                            Source = GetStringProp(root, "source")
                        });
                    }
                }
            }
        }

        private static void ProcessSaveObject(JsonElement saveProp, VerificationSummary summary)
        {
            string ability = GetStringProp(saveProp, "ability");
            int dc = saveProp.TryGetProperty("dc", out var dcProp) ? dcProp.GetInt32() : 0;
            int roll = saveProp.TryGetProperty("roll", out var rollProp) ? rollProp.GetInt32() : 0;
            bool passed = saveProp.TryGetProperty("passed", out var passedProp) && passedProp.GetBoolean();
            if (!saveProp.TryGetProperty("passed", out _))
            {
                // Some logs use "success" instead
                passed = saveProp.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
            }
            string targetId = GetStringProp(saveProp, "target");

            summary.SaveInstances.Add(new SaveInstance
            {
                TargetId = targetId,
                Ability = ability,
                DC = dc,
                Roll = roll,
                Passed = passed
            });
        }

        private static void ProcessDamageDealt(JsonElement root, string actionId, VerificationSummary summary)
        {
            // DAMAGE_DEALT events may reference the source ability
            string sourceAbility = GetStringProp(root, "ability_id") ?? GetStringProp(root, "source_ability");
            if (sourceAbility == null) return;

            if (!string.Equals(sourceAbility, actionId, StringComparison.OrdinalIgnoreCase)) return;

            int amount = root.TryGetProperty("amount", out var amtProp) ? amtProp.GetInt32() : 0;
            string targetId = GetStringProp(root, "target") ?? GetStringProp(root, "unit");
            string dmgType = GetStringProp(root, "damage_type");

            if (amount > 0)
            {
                summary.DamageInstances.Add(new DamageInstance
                {
                    TargetId = targetId,
                    Amount = amount,
                    DamageType = dmgType
                });
            }
        }

        private static void ProcessStatusApplied(JsonElement root, string actionId, VerificationSummary summary)
        {
            // STATUS_APPLIED events that were caused by our action
            string source = GetStringProp(root, "source");
            if (source == null || !source.Contains(actionId, StringComparison.OrdinalIgnoreCase)) return;

            string statusId = GetStringProp(root, "status_id");
            string target = GetStringProp(root, "unit") ?? GetStringProp(root, "target");
            int? duration = root.TryGetProperty("duration", out var durProp) ? durProp.GetInt32() : null;

            summary.StatusesApplied.Add(new StatusInstance
            {
                TargetId = target,
                StatusId = statusId,
                Source = source,
                Duration = duration
            });
        }

        private static void ProcessDecision(JsonElement root, string actionId, VerificationSummary summary)
        {
            string abilityId = GetStringProp(root, "ability_id");
            if (string.Equals(abilityId, actionId, StringComparison.OrdinalIgnoreCase))
            {
                summary.ActionWasUsed = true;
            }
        }

        private static string GetStringProp(JsonElement element, string propName)
        {
            if (element.TryGetProperty(propName, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
            return null;
        }

        /// <summary>
        /// Formats the verification summary as a human-readable report for agent consumption.
        /// </summary>
        public static string FormatReport(VerificationSummary summary)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== Spell Verification Report: {summary.ActionId} ===");
            sb.AppendLine();

            // Basic outcome
            sb.AppendLine($"Action Used:      {(summary.ActionWasUsed ? "YES" : "NO")}");
            sb.AppendLine($"Times Used:       {summary.TimesUsed}");
            sb.AppendLine($"Battle Completed: {(summary.BattleCompleted ? "YES" : "NO")}");
            if (summary.Winner != null)
                sb.AppendLine($"Winner:           {summary.Winner}");
            sb.AppendLine();

            // Damage
            if (summary.DamageInstances.Count > 0)
            {
                sb.AppendLine("-- Damage --");
                sb.AppendLine($"  Total:          {summary.TotalDamageDealt}");
                sb.AppendLine($"  Min Roll:       {summary.MinDamageRoll}");
                sb.AppendLine($"  Max Roll:       {summary.MaxDamageRoll}");
                sb.AppendLine($"  Damage Types:   {string.Join(", ", summary.DamageTypes)}");
                sb.AppendLine($"  Targets Hit:    {summary.TargetsHit} ({string.Join(", ", summary.TargetIds)})");
                foreach (var d in summary.DamageInstances)
                    sb.AppendLine($"    -> {d.TargetId}: {d.Amount} {d.DamageType}{(d.WasCrit ? " (CRIT)" : "")}{(d.WasHalfDamage ? " (half)" : "")}");
                sb.AppendLine();
            }

            // Saves
            if (summary.SaveInstances.Count > 0)
            {
                sb.AppendLine("-- Saving Throws --");
                sb.AppendLine($"  Save Ability:   {summary.SaveAbility}");
                sb.AppendLine($"  Total:          {summary.SavesTriggered}");
                sb.AppendLine($"  Passed:         {summary.SavesPassed}");
                sb.AppendLine($"  Failed:         {summary.SavesFailed}");
                foreach (var s in summary.SaveInstances)
                    sb.AppendLine($"    -> {s.TargetId}: DC {s.DC}, rolled {s.Roll} -> {(s.Passed ? "PASS" : "FAIL")}");
                sb.AppendLine();
            }

            // Attack rolls
            if (summary.AttackRolls.Count > 0)
            {
                sb.AppendLine("-- Attack Rolls --");
                sb.AppendLine($"  Total:          {summary.AttacksMade}");
                sb.AppendLine($"  Hits:           {summary.AttacksHit}");
                sb.AppendLine($"  Misses:         {summary.AttacksMissed}");
                foreach (var a in summary.AttackRolls)
                    sb.AppendLine($"    -> {a.TargetId}: rolled {a.Roll} (total {a.Total}) vs AC {a.TargetAC} -> {(a.Hit ? "HIT" : "MISS")}{(a.WasCrit ? " (CRIT)" : "")}");
                sb.AppendLine();
            }

            // Healing
            if (summary.HealInstances.Count > 0)
            {
                sb.AppendLine("-- Healing --");
                sb.AppendLine($"  Total:          {summary.TotalHealing}");
                foreach (var h in summary.HealInstances)
                    sb.AppendLine($"    -> {h.TargetId}: +{h.Amount} HP");
                sb.AppendLine();
            }

            // Statuses
            if (summary.StatusesApplied.Count > 0)
            {
                sb.AppendLine("-- Statuses Applied --");
                foreach (var s in summary.StatusesApplied)
                    sb.AppendLine($"    -> {s.TargetId}: {s.StatusId}{(s.Duration.HasValue ? $" ({s.Duration}t)" : "")}");
                sb.AppendLine();
            }

            // Concentration
            if (summary.ConcentrationStarted)
            {
                sb.AppendLine("-- Concentration: ACTIVE --");
                sb.AppendLine();
            }

            // Warnings
            if (summary.Warnings.Count > 0)
            {
                sb.AppendLine("-- Warnings --");
                foreach (var w in summary.Warnings)
                    sb.AppendLine($"  WARNING: {w}");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serializes the full summary to JSON for programmatic consumption.
        /// </summary>
        public static string ToJson(VerificationSummary summary)
        {
            return JsonSerializer.Serialize(summary, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }
    }
}
