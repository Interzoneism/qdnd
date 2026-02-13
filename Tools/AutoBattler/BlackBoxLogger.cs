using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using QDND.Combat.AI;
using QDND.Combat.Entities;

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
        STATE_CHANGE
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

        public void LogTurnStart(Combatant actor, int turnNumber, int round)
        {
            Write(new LogEntry
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
            });
        }

        public void LogDecision(string actorId, AIDecisionResult decision)
        {
            var chosen = decision?.ChosenAction;
            if (chosen == null) return;

            string target = chosen.TargetId ?? chosen.TargetPosition?.ToString();
            string topScore = chosen.ScoreBreakdown.Count > 0
                ? chosen.ScoreBreakdown.OrderByDescending(kv => kv.Value).First().Key
                : null;

            Write(new LogEntry
            {
                Event = LogEventType.DECISION,
                UnitId = actorId,
                Action = chosen.ActionType.ToString(),
                Target = target,
                Score = chosen.Score,
                Reason = topScore,
                ScoreBreakdown = chosen.ScoreBreakdown.Count > 0
                    ? new Dictionary<string, float>(chosen.ScoreBreakdown
                        .OrderByDescending(kv => kv.Value)
                        .Take(5)
                        .ToDictionary(kv => kv.Key, kv => (float)Math.Round(kv.Value, 2)))
                    : null
            });
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

        private UnitSnapshot SnapshotUnit(Combatant c)
        {
            return new UnitSnapshot
            {
                Id = c.Id,
                Name = c.Name,
                Faction = c.Faction.ToString(),
                HP = c.Resources.CurrentHP,
                MaxHP = c.Resources.MaxHP,
                Position = new[] { c.Position.X, c.Position.Y, c.Position.Z },
                Alive = c.IsActive && c.Resources.CurrentHP > 0
            };
        }

        private static bool ShouldEchoToStdout(LogEntry entry)
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
