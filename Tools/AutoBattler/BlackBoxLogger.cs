using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;
using QDND.Combat.Statuses;
using QDND.Data.CharacterModel;

namespace QDND.Tools.AutoBattler
{
    /// <summary>
    /// Record of an executed action (for logging purposes).
    /// </summary>
    public class ActionRecord
    {
        public AIActionType Type { get; set; }
        public string TargetId { get; set; }
        public string ActionId { get; set; }
        public bool Success { get; set; }
        public string Description { get; set; }
        public float Score { get; set; }
    }

    /// <summary>
    /// Event types for the black-box combat log.
    /// </summary>
    public enum LogEventType
    {
        BATTLE_START,
        TURN_START,
        DECISION,
        ACTION_RESULT,
        STATE_SNAPSHOT,
        UNIT_DIED,
        ROUND_END,
        BATTLE_END,
        WATCHDOG_ALERT,
        ERROR,
        STATE_CHANGE,
        ABILITY_COVERAGE,
        EFFECT_UNHANDLED,
        STATUS_APPLIED,
        STATUS_REMOVED,
        STATUS_NO_RUNTIME_BEHAVIOR,
        SURFACE_CREATED,
        DAMAGE_DEALT,
        PARITY_SUMMARY,
        ACTION_DETAIL,
        ACTION_BATCH_SUMMARY,
        SURFACE_DAMAGE,
        STATUS_TICK,
        PASSIVE_TRIGGERED,
        REACTION_TRIGGERED,  // TODO: wire from ReactionCoordinator when a reaction fires
        REACTION_USED        // TODO: wire from ReactionCoordinator when a reaction is consumed
    }

    /// <summary>
    /// A single log entry in JSON-Lines format.
    /// All fields are nullable to keep entries lean.
    /// </summary>
    public class LogEntry
    {
        [JsonPropertyName("ts")]
        public long Timestamp { get; set; }

        [JsonPropertyName("event")]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public LogEventType Event { get; set; }

        [JsonPropertyName("turn")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TurnNumber { get; set; }

        [JsonPropertyName("round")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Round { get; set; }

        [JsonPropertyName("unit")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UnitId { get; set; }

        [JsonPropertyName("unit_name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string UnitName { get; set; }

        [JsonPropertyName("faction")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Faction { get; set; }

        [JsonPropertyName("hp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? HP { get; set; }

        [JsonPropertyName("max_hp")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? MaxHP { get; set; }

        [JsonPropertyName("ap")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ActionPoints { get; set; }

        [JsonPropertyName("pos")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float[] Position { get; set; }

        [JsonPropertyName("action")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Action { get; set; }

        [JsonPropertyName("target")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Target { get; set; }

        [JsonPropertyName("score")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Score { get; set; }

        [JsonPropertyName("reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Reason { get; set; }

        [JsonPropertyName("score_breakdown")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, float> ScoreBreakdown { get; set; }

        [JsonPropertyName("success")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Success { get; set; }

        [JsonPropertyName("description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Description { get; set; }

        [JsonPropertyName("damage")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Damage { get; set; }

        [JsonPropertyName("result")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Result { get; set; }

        [JsonPropertyName("seed")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Seed { get; set; }

        [JsonPropertyName("units")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<UnitSnapshot> Units { get; set; }

        [JsonPropertyName("winner")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Winner { get; set; }

        [JsonPropertyName("total_turns")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TotalTurns { get; set; }

        [JsonPropertyName("total_rounds")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TotalRounds { get; set; }

        [JsonPropertyName("duration_ms")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? DurationMs { get; set; }

        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Error { get; set; }

        [JsonPropertyName("from_state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string FromState { get; set; }

        [JsonPropertyName("to_state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ToState { get; set; }

        [JsonPropertyName("state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string State { get; set; }

        [JsonPropertyName("active_timelines")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ActiveTimelines { get; set; }

        [JsonPropertyName("ai_wait_reason")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AIWaitReason { get; set; }

        [JsonPropertyName("ability_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ActionId { get; set; }

        [JsonPropertyName("effect_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string EffectType { get; set; }

        [JsonPropertyName("status_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string StatusId { get; set; }

        [JsonPropertyName("damage_amount")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? DamageAmount { get; set; }

        [JsonPropertyName("damage_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DamageType { get; set; }

        [JsonPropertyName("surface_type")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SurfaceType { get; set; }

        [JsonPropertyName("metrics")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object> Metrics { get; set; }

        [JsonPropertyName("source")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Source { get; set; }

        [JsonPropertyName("duration")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Duration { get; set; }

        [JsonPropertyName("radius")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public float? Radius { get; set; }

        [JsonPropertyName("targets")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Targets { get; set; }

        [JsonPropertyName("details")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, object> Details { get; set; }

        [JsonPropertyName("active_statuses")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Dictionary<string, object>> ActiveStatuses { get; set; }

        [JsonPropertyName("spell_slots")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int> SpellSlots { get; set; }

        [JsonPropertyName("conditions")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Conditions { get; set; }
    }

    /// <summary>
    /// Lightweight unit state for STATE_SNAPSHOT events.
    /// </summary>
    public class UnitSnapshot
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("faction")]
        public string Faction { get; set; }

        [JsonPropertyName("hp")]
        public int HP { get; set; }

        [JsonPropertyName("max_hp")]
        public int MaxHP { get; set; }

        [JsonPropertyName("pos")]
        public float[] Position { get; set; }

        [JsonPropertyName("alive")]
        public bool Alive { get; set; }

        [JsonPropertyName("abilities")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Abilities { get; set; }

        [JsonPropertyName("race")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Race { get; set; }

        [JsonPropertyName("class")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Class { get; set; }

        [JsonPropertyName("subclass")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Subclass { get; set; }

        [JsonPropertyName("level")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Level { get; set; }

        [JsonPropertyName("ac")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? AC { get; set; }

        [JsonPropertyName("main_hand")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string MainHand { get; set; }

        [JsonPropertyName("off_hand")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string OffHand { get; set; }

        [JsonPropertyName("armor")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Armor { get; set; }

        [JsonPropertyName("shield")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Shield { get; set; }

        [JsonPropertyName("feats")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Feats { get; set; }

        [JsonPropertyName("passives")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Passives { get; set; }

        [JsonPropertyName("spell_slots")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int> SpellSlots { get; set; }

        [JsonPropertyName("ability_scores")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, int> AbilityScores { get; set; }

        [JsonPropertyName("tags")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> Tags { get; set; }
    }

    /// <summary>
    /// Forensic-level combat logger that writes structured JSON-Lines (.jsonl).
    /// Streams log entries to a file and/or stdout for post-mortem analysis.
    /// </summary>
    public class BlackBoxLogger : IDisposable
    {
        private StreamWriter _fileWriter;
        private readonly bool _writeToStdout;
        private readonly JsonSerializerOptions _jsonOptions;
        private int _entryCount;
        private long _lastWriteTimestamp;
        private bool _disposed;
        private string _lastStateChangeKey;
        private long _lastStateChangeTs;

        /// <summary>
        /// Timestamp of the last write operation (Unix ms).
        /// Used by the Watchdog to detect freezes.
        /// </summary>
        public long LastWriteTimestamp => _lastWriteTimestamp;

        /// <summary>
        /// Total entries written.
        /// </summary>
        public int EntryCount => _entryCount;

        /// <summary>
        /// When true, ACTION_DETAIL events are echoed to stdout.
        /// Off by default to keep stdout concise.
        /// </summary>
        public bool VerboseDetailLogging { get; set; } = false;

        /// <summary>
        /// Create a BlackBoxLogger.
        /// </summary>
        /// <param name="filePath">Path to .jsonl file. Null to skip file output.</param>
        /// <param name="writeToStdout">Also print to stdout via GD.Print.</param>
        public BlackBoxLogger(string filePath = null, bool writeToStdout = true)
        {
            _writeToStdout = writeToStdout;
            _lastWriteTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            _jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            if (!string.IsNullOrEmpty(filePath))
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                _fileWriter = new StreamWriter(filePath, append: false, encoding: System.Text.Encoding.UTF8)
                {
                    AutoFlush = true
                };
            }
        }

        /// <summary>
        /// Write a log entry.
        /// </summary>
        public void Write(LogEntry entry)
        {
            if (_disposed) return;

            entry.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string line = JsonSerializer.Serialize(entry, _jsonOptions);

            _fileWriter?.WriteLine(line);

            if (_writeToStdout && ShouldEchoToStdout(entry))
            {
                GD.Print($"[COMBAT_LOG] {line}");
            }

            _entryCount++;
            _lastWriteTimestamp = entry.Timestamp;
        }

        // === Convenience methods ===

        public void LogBattleStart(int seed, List<Combatant> combatants)
        {
            Write(new LogEntry
            {
                Event = LogEventType.BATTLE_START,
                Seed = seed,
                Units = combatants.Select(c => SnapshotUnit(c)).ToList()
            });
        }

        public void LogTurnStart(Combatant actor, int turnNumber, int round,
            List<StatusInstance> activeStatuses = null,
            Dictionary<string, int> spellSlots = null)
        {
            var entry = new LogEntry
            {
                Event = LogEventType.TURN_START,
                UnitId = actor.Id,
                UnitName = actor.Name,
                Faction = actor.Faction.ToString(),
                TurnNumber = turnNumber,
                Round = round,
                HP = actor.Resources.CurrentHP,
                MaxHP = actor.Resources.MaxHP,
                ActionPoints = actor.ActionBudget?.ToString(),
                Position = new[] { actor.Position.X, actor.Position.Y, actor.Position.Z }
            };

            if (activeStatuses?.Count > 0)
            {
                entry.ActiveStatuses = activeStatuses.Select(s => new Dictionary<string, object>
                {
                    ["id"] = s.Definition.Id,
                    ["source"] = s.SourceId ?? "unknown",
                    ["remaining_duration"] = s.RemainingDuration
                }).ToList();
            }

            if (spellSlots?.Count > 0)
                entry.SpellSlots = spellSlots;

            Write(entry);
        }

        public void LogDecision(string actorId, AIDecisionResult decision)
        {
            var chosen = decision?.ChosenAction;
            if (chosen == null) return;

            string target = chosen.TargetId ?? chosen.TargetPosition?.ToString();
            string topScore = chosen.ScoreBreakdown.Count > 0
                ? chosen.ScoreBreakdown.OrderByDescending(kv => kv.Value).First().Key
                : null;

            List<Dictionary<string, object>> candidateSummary = null;
            if (decision.AllCandidates?.Count > 0)
            {
                candidateSummary = decision.AllCandidates
                    .OrderByDescending(c => c.Score)
                    .Take(10)
                    .Select(c => new Dictionary<string, object>
                    {
                        ["action"] = c.ActionType.ToString(),
                        ["ability_id"] = c.ActionId ?? "",
                        ["target"] = c.TargetId ?? "",
                        ["score"] = Math.Round(c.Score, 2),
                        ["valid"] = c.IsValid
                    }).ToList();
            }

            var entry = new LogEntry
            {
                Event = LogEventType.DECISION,
                UnitId = actorId,
                Action = chosen.ActionType.ToString(),
                ActionId = chosen.ActionId,
                Target = target,
                Score = chosen.Score,
                Reason = topScore,
                ScoreBreakdown = chosen.ScoreBreakdown.Count > 0
                    ? new Dictionary<string, float>(chosen.ScoreBreakdown
                        .OrderByDescending(kv => kv.Value)
                        .Take(5)
                        .ToDictionary(kv => kv.Key, kv => (float)Math.Round(kv.Value, 2)))
                    : null
            };

            if (candidateSummary?.Count > 0)
            {
                entry.Details = new Dictionary<string, object>
                {
                    ["candidates"] = candidateSummary,
                    ["total_candidates"] = decision.AllCandidates.Count,
                    ["is_forced"] = decision.IsForcedByTest,
                    ["is_part_of_plan"] = decision.IsPartOfPlan
                };

                if (decision.TurnPlan != null)
                    entry.Details["turn_plan_steps"] = decision.TurnPlan.PlannedActions?.Count ?? 0;
            }

            Write(entry);
        }

        public void LogActionResult(string actorId, ActionRecord action)
        {
            Write(new LogEntry
            {
                Event = LogEventType.ACTION_RESULT,
                UnitId = actorId,
                Action = action.Type.ToString(),
                Target = action.TargetId,
                Success = action.Success,
                Description = action.Description,
                Score = action.Score,
                ActionId = action.ActionId
            });
        }

        public void LogStateChange(string fromState, string toState, string reason)
        {
            // Suppress rapid duplicate transitions that flood logs during stalls/retries.
            string stateKey = $"{fromState}|{toState}|{reason}";
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (string.Equals(_lastStateChangeKey, stateKey, StringComparison.Ordinal) &&
                (now - _lastStateChangeTs) < 250)
            {
                return;
            }

            _lastStateChangeKey = stateKey;
            _lastStateChangeTs = now;

            Write(new LogEntry
            {
                Event = LogEventType.STATE_CHANGE,
                FromState = fromState,
                ToState = toState,
                Reason = reason
            });
        }

        public void LogUnitDied(Combatant unit, string killedBy)
        {
            Write(new LogEntry
            {
                Event = LogEventType.UNIT_DIED,
                UnitId = unit.Id,
                UnitName = unit.Name,
                Faction = unit.Faction.ToString(),
                Reason = $"Killed by {killedBy}",
                Position = new[] { unit.Position.X, unit.Position.Y, unit.Position.Z }
            });
        }

        public void LogStateSnapshot(IEnumerable<Combatant> combatants, int round, int turnNumber)
        {
            Write(new LogEntry
            {
                Event = LogEventType.STATE_SNAPSHOT,
                Round = round,
                TurnNumber = turnNumber,
                Units = combatants.Select(c => SnapshotUnit(c)).ToList()
            });
        }

        public void LogRoundEnd(int round)
        {
            Write(new LogEntry
            {
                Event = LogEventType.ROUND_END,
                Round = round
            });
        }

        public void LogBattleEnd(string winner, int totalTurns, int totalRounds, long durationMs)
        {
            Write(new LogEntry
            {
                Event = LogEventType.BATTLE_END,
                Winner = winner,
                TotalTurns = totalTurns,
                TotalRounds = totalRounds,
                DurationMs = durationMs
            });
        }

        public void LogWatchdogAlert(string alertType, string message, string currentState = null, int? activeTimelines = null, string aiWaitReason = null)
        {
            Write(new LogEntry
            {
                Event = LogEventType.WATCHDOG_ALERT,
                Action = alertType,
                Error = message,
                State = currentState,
                ActiveTimelines = activeTimelines,
                AIWaitReason = aiWaitReason
            });
        }

        public void LogError(string message, string context = null)
        {
            Write(new LogEntry
            {
                Event = LogEventType.ERROR,
                Error = message,
                Description = context
            });
        }

        public void LogEffectUnhandled(string unitId, string abilityId, string effectType)
        {
            Write(new LogEntry
            {
                Event = LogEventType.EFFECT_UNHANDLED,
                UnitId = unitId,
                ActionId = abilityId,
                EffectType = effectType
            });
        }

        public void LogStatusApplied(string unitId, string statusId, string sourceId, int? duration)
        {
            Write(new LogEntry
            {
                Event = LogEventType.STATUS_APPLIED,
                UnitId = unitId,
                Target = unitId,
                StatusId = statusId,
                Source = sourceId,
                Duration = duration
            });
        }

        public void LogStatusRemoved(string unitId, string statusId, string reason)
        {
            Write(new LogEntry
            {
                Event = LogEventType.STATUS_REMOVED,
                UnitId = unitId,
                Target = unitId,
                StatusId = statusId,
                Reason = reason
            });
        }

        public void LogStatusTick(string unitId, string statusId, int remainingDuration)
        {
            Write(new LogEntry
            {
                Event = LogEventType.STATUS_TICK,
                UnitId = unitId,
                Target = unitId,
                StatusId = statusId,
                Duration = remainingDuration
            });
        }

        public void LogSurfaceCreated(string surfaceType, float radius)
        {
            Write(new LogEntry
            {
                Event = LogEventType.SURFACE_CREATED,
                SurfaceType = surfaceType,
                Radius = radius
            });
        }

        public void LogSurfaceDamage(string surfaceId, string targetId, int damage, string damageType)
        {
            Write(new LogEntry
            {
                Event = LogEventType.SURFACE_DAMAGE,
                Source = surfaceId,
                Target = targetId,
                DamageAmount = damage,
                DamageType = damageType
            });
        }

        public void LogDamageDealt(string sourceId, string targetId, int amount, string damageType, string abilityId)
        {
            Write(new LogEntry
            {
                Event = LogEventType.DAMAGE_DEALT,
                Source = sourceId,
                Target = targetId,
                DamageAmount = amount,
                DamageType = damageType,
                ActionId = abilityId
            });
        }

        public void LogAbilityCoverage(Dictionary<string, object> coverageData)
        {
            Write(new LogEntry
            {
                Event = LogEventType.ABILITY_COVERAGE,
                Metrics = coverageData
            });
        }

        public void LogParitySummary(Dictionary<string, object> metrics)
        {
            Write(new LogEntry
            {
                Event = LogEventType.PARITY_SUMMARY,
                Metrics = metrics
            });
        }

        public void LogActionDetail(string sourceId, string actionId, string actionName, List<string> targetIds, Dictionary<string, object> details)
        {
            Write(new LogEntry
            {
                Event = LogEventType.ACTION_DETAIL,
                Source = sourceId,
                ActionId = actionId,
                Action = actionName,
                Targets = targetIds?.Count > 0 ? targetIds : null,
                Target = targetIds?.Count == 1 ? targetIds[0] : null,
                Details = details?.Count > 0 ? details : null
            });
        }

        public void LogActionBatchSummary(Dictionary<string, object> batchMetrics)
        {
            Write(new LogEntry
            {
                Event = LogEventType.ACTION_BATCH_SUMMARY,
                Metrics = batchMetrics
            });
        }

        public void LogPassiveTriggered(string unitId, string passiveId, string trigger, string description = null)
        {
            Write(new LogEntry
            {
                Event = LogEventType.PASSIVE_TRIGGERED,
                UnitId = unitId,
                ActionId = passiveId,
                Action = trigger,
                Description = description
            });
        }

        private UnitSnapshot SnapshotUnit(Combatant c)
        {
            var snapshot = new UnitSnapshot
            {
                Id = c.Id,
                Name = c.Name,
                Faction = c.Faction.ToString(),
                HP = c.Resources.CurrentHP,
                MaxHP = c.Resources.MaxHP,
                Position = new[] { c.Position.X, c.Position.Y, c.Position.Z },
                Alive = c.IsActive && c.Resources.CurrentHP > 0,
                Abilities = c.KnownActions?.ToList(),
                AC = c.GetArmorClass(),
                MainHand = c.MainHandWeapon?.Id,
                OffHand = c.OffHandWeapon?.Id,
                Armor = c.EquippedArmor?.Id,
                Shield = c.EquippedShield?.Id,
                Passives = c.PassiveIds?.Count > 0 ? c.PassiveIds.ToList() : null,
                Tags = c.Tags?.Count > 0 ? c.Tags.ToList() : null
            };

            var rc = c.ResolvedCharacter;
            if (rc?.Sheet != null)
            {
                snapshot.Race = rc.Sheet.RaceId;
                snapshot.Level = rc.Sheet.TotalLevel;
                snapshot.Feats = rc.Sheet.FeatIds?.Count > 0 ? rc.Sheet.FeatIds.ToList() : null;

                var firstClass = rc.Sheet.ClassLevels?.FirstOrDefault();
                if (firstClass != null)
                {
                    snapshot.Class = firstClass.ClassId;
                    snapshot.Subclass = firstClass.SubclassId;
                }

                if (rc.AbilityScores?.Count > 0)
                {
                    snapshot.AbilityScores = rc.AbilityScores.ToDictionary(
                        kv => kv.Key.ToString(), kv => kv.Value);
                }
            }

            var slotSnapshot = CollectSpellSlots(c);
            if (slotSnapshot?.Count > 0)
                snapshot.SpellSlots = slotSnapshot;

            return snapshot;
        }

        public static Dictionary<string, int> CollectSpellSlots(Combatant c)
        {
            if (c.ActionResources == null) return null;
            var slots = new Dictionary<string, int>();
            foreach (var kv in c.ActionResources.Resources)
            {
                var resource = kv.Value;
                if (resource.IsLeveled)
                {
                    // Iterate MaxByLevel so exhausted (current = 0) slots are still reported.
                    foreach (var levelKv in resource.MaxByLevel)
                    {
                        int level = levelKv.Key;
                        int current = resource.CurrentByLevel.TryGetValue(level, out var cur) ? cur : 0;
                        slots[$"{kv.Key}_L{level}"] = current;
                    }
                }
                else
                {
                    var name = kv.Key;
                    if (!IsTurnEconomyResource(name))
                        slots[name] = resource.Current;
                }
            }
            return slots.Count > 0 ? slots : null;
        }

        private static bool IsTurnEconomyResource(string id)
        {
            return id != null && (
                id.Equals("ActionPoint", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("BonusActionPoint", StringComparison.OrdinalIgnoreCase) ||
                id.Equals("ReactionActionPoint", StringComparison.OrdinalIgnoreCase));
        }

        private bool ShouldEchoToStdout(LogEntry entry)
        {
            // Keep stdout concise; forensic detail remains in the JSONL file.
            return entry.Event switch
            {
                LogEventType.BATTLE_START => true,
                LogEventType.TURN_START => true,
                LogEventType.ROUND_END => true,
                LogEventType.UNIT_DIED => true,
                LogEventType.BATTLE_END => true,
                LogEventType.WATCHDOG_ALERT => true,
                LogEventType.ERROR => true,
                LogEventType.ABILITY_COVERAGE => true,
                LogEventType.PARITY_SUMMARY => true,
                LogEventType.ACTION_BATCH_SUMMARY => true,
                LogEventType.ACTION_DETAIL => VerboseDetailLogging,
                _ => false
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _fileWriter?.Flush();
            _fileWriter?.Dispose();
            _fileWriter = null;
        }
    }
}
