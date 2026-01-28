using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using QDND.Combat.States;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Types of combat log entries.
    /// </summary>
    public enum LogEntryType
    {
        StateTransition,
        TurnChange,
        CommandExecuted,
        DamageDealt,
        HealApplied,
        StatusApplied,
        StatusRemoved,
        CombatStart,
        CombatEnd,
        Custom
    }

    /// <summary>
    /// A single entry in the combat log.
    /// </summary>
    public class CombatLogEntry
    {
        public string EntryId { get; } = Guid.NewGuid().ToString("N")[..8];
        public LogEntryType Type { get; }
        public long Timestamp { get; }
        public int Round { get; set; }
        public int Turn { get; set; }
        public string Message { get; }
        public Dictionary<string, object> Data { get; }

        public CombatLogEntry(LogEntryType type, string message, Dictionary<string, object> data = null)
        {
            Type = type;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            Message = message;
            Data = data ?? new Dictionary<string, object>();
        }

        public override string ToString()
        {
            return $"[{Type}] R{Round}T{Turn}: {Message}";
        }
    }

    /// <summary>
    /// Records all combat events for replay, debugging, and determinism verification.
    /// </summary>
    public class CombatLog
    {
        private readonly List<CombatLogEntry> _entries = new();
        private int _currentRound = 0;
        private int _currentTurn = 0;

        /// <summary>
        /// Fired when a new entry is added.
        /// </summary>
        public event Action<CombatLogEntry> OnEntryAdded;

        /// <summary>
        /// All log entries.
        /// </summary>
        public IReadOnlyList<CombatLogEntry> Entries => _entries;

        /// <summary>
        /// Update the current round/turn context for new entries.
        /// </summary>
        public void SetContext(int round, int turn)
        {
            _currentRound = round;
            _currentTurn = turn;
        }

        /// <summary>
        /// Log a state transition.
        /// </summary>
        public void LogStateTransition(StateTransitionEvent evt)
        {
            var entry = new CombatLogEntry(
                LogEntryType.StateTransition,
                $"{evt.FromState} -> {evt.ToState}" + (string.IsNullOrEmpty(evt.Reason) ? "" : $" ({evt.Reason})"),
                new Dictionary<string, object>
                {
                    { "fromState", evt.FromState.ToString() },
                    { "toState", evt.ToState.ToString() },
                    { "reason", evt.Reason }
                }
            );
            AddEntry(entry);
        }

        /// <summary>
        /// Log a turn change.
        /// </summary>
        public void LogTurnChange(TurnChangeEvent evt)
        {
            _currentRound = evt.Round;
            _currentTurn = evt.TurnIndex;

            var entry = new CombatLogEntry(
                LogEntryType.TurnChange,
                $"Round {evt.Round}, Turn {evt.TurnIndex}: {evt.CurrentCombatant?.Name ?? "None"}",
                new Dictionary<string, object>
                {
                    { "round", evt.Round },
                    { "turnIndex", evt.TurnIndex },
                    { "previousCombatant", evt.PreviousCombatant?.Id },
                    { "currentCombatant", evt.CurrentCombatant?.Id }
                }
            );
            AddEntry(entry);
        }

        /// <summary>
        /// Log a command execution.
        /// </summary>
        public void LogCommand(CommandExecutedEvent evt)
        {
            var entry = new CombatLogEntry(
                LogEntryType.CommandExecuted,
                $"{evt.Command.Type} by {evt.Command.CombatantId}: {(evt.Success ? "Success" : "Failed")} - {evt.Result}",
                new Dictionary<string, object>
                {
                    { "commandId", evt.Command.CommandId },
                    { "commandType", evt.Command.Type.ToString() },
                    { "combatantId", evt.Command.CombatantId },
                    { "success", evt.Success },
                    { "result", evt.Result },
                    { "commandData", evt.Command.ToEventData() }
                }
            );
            AddEntry(entry);
        }

        /// <summary>
        /// Log a custom message.
        /// </summary>
        public void Log(string message, Dictionary<string, object> data = null)
        {
            var entry = new CombatLogEntry(LogEntryType.Custom, message, data);
            AddEntry(entry);
        }

        /// <summary>
        /// Log combat start.
        /// </summary>
        public void LogCombatStart(int combatantCount, int seed)
        {
            var entry = new CombatLogEntry(
                LogEntryType.CombatStart,
                $"Combat started with {combatantCount} combatants, seed {seed}",
                new Dictionary<string, object>
                {
                    { "combatantCount", combatantCount },
                    { "seed", seed }
                }
            );
            AddEntry(entry);
        }

        /// <summary>
        /// Log combat end.
        /// </summary>
        public void LogCombatEnd(string result)
        {
            var entry = new CombatLogEntry(
                LogEntryType.CombatEnd,
                $"Combat ended: {result}",
                new Dictionary<string, object>
                {
                    { "result", result }
                }
            );
            AddEntry(entry);
        }

        private void AddEntry(CombatLogEntry entry)
        {
            entry.Round = _currentRound;
            entry.Turn = _currentTurn;
            _entries.Add(entry);
            OnEntryAdded?.Invoke(entry);
        }

        /// <summary>
        /// Get entries by type.
        /// </summary>
        public List<CombatLogEntry> GetEntriesByType(LogEntryType type)
        {
            return _entries.FindAll(e => e.Type == type);
        }

        /// <summary>
        /// Get entries for a specific round.
        /// </summary>
        public List<CombatLogEntry> GetEntriesByRound(int round)
        {
            return _entries.FindAll(e => e.Round == round);
        }

        /// <summary>
        /// Export log to JSON.
        /// </summary>
        public string ExportToJson()
        {
            var exportData = new List<Dictionary<string, object>>();
            foreach (var entry in _entries)
            {
                exportData.Add(new Dictionary<string, object>
                {
                    { "entryId", entry.EntryId },
                    { "type", entry.Type.ToString() },
                    { "timestamp", entry.Timestamp },
                    { "round", entry.Round },
                    { "turn", entry.Turn },
                    { "message", entry.Message },
                    { "data", entry.Data }
                });
            }
            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Calculate a deterministic hash of the log for replay verification.
        /// </summary>
        public int CalculateHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (var entry in _entries)
                {
                    hash = hash * 31 + entry.Type.GetHashCode();
                    hash = hash * 31 + entry.Message.GetHashCode();
                    hash = hash * 31 + entry.Round;
                    hash = hash * 31 + entry.Turn;
                }
                return hash;
            }
        }

        /// <summary>
        /// Get formatted log as string.
        /// </summary>
        public string GetFormattedLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== COMBAT LOG ===");
            foreach (var entry in _entries)
            {
                sb.AppendLine(entry.ToString());
            }
            sb.AppendLine($"=== END LOG ({_entries.Count} entries, hash: {CalculateHash():X8}) ===");
            return sb.ToString();
        }

        /// <summary>
        /// Clear all entries.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _currentRound = 0;
            _currentTurn = 0;
        }
    }
}
