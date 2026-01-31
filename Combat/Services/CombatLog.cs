using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using QDND.Combat.States;

namespace QDND.Combat.Services
{
    public enum LogEntryType
    {
        StateTransition, TurnChange, CommandExecuted, DamageDealt,HealApplied, StatusApplied, StatusRemoved, CombatStart, CombatEnd, Custom
    }

    public class CombatLog
    {
        private readonly List<CombatLogEntry> _entries = new();
        private int _currentRound = 0;
        private int _currentTurn = 0;
        public event Action<CombatLogEntry> OnEntryAdded;
        public IReadOnlyList<CombatLogEntry> Entries => _entries;

        public void SetContext(int round, int turn)
        {
            _currentRound = round;
            _currentTurn = turn;
        }

        public void LogEntry(CombatLogEntry entry)
        {
            entry.Round = _currentRound;
            entry.Turn = _currentTurn;
            _entries.Add(entry);
            OnEntryAdded?.Invoke(entry);
        }

        public void LogDamage(string sourceId, string sourceName, string targetId, string targetName, float damage, Dictionary<string, object> breakdown = null, bool isCritical = false)
        {
            var entry = new CombatLogEntry
            {
                Type = CombatLogEntryType.DamageDealt,
                SourceId = sourceId,
                SourceName = sourceName,
                TargetId = targetId,
                TargetName = targetName,
                Value = damage,
                IsCritical = isCritical,
                Severity = isCritical ? LogSeverity.Important : LogSeverity.Normal
            };
            if (breakdown != null)
                foreach (var kvp in breakdown)
                    entry.Breakdown[kvp.Key] = kvp.Value;
            LogEntry(entry);
        }

        public void LogHealing(string sourceId, string sourceName, string targetId, string targetName, float healing)
        {
            LogEntry(new CombatLogEntry { Type = CombatLogEntryType.HealingDone, SourceId = sourceId, SourceName = sourceName, TargetId = targetId, TargetName = targetName, Value = healing });
        }

        public void LogAttack(string sourceId, string sourceName, string targetId, string targetName, bool hit, Dictionary<string, object> breakdown = null)
        {
            var entry = new CombatLogEntry { Type = CombatLogEntryType.AttackDeclared, SourceId = sourceId, SourceName = sourceName, TargetId = targetId, TargetName = targetName, IsMiss = !hit };
            if (breakdown != null)
                foreach (var kvp in breakdown)
                    entry.Breakdown[kvp.Key] = kvp.Value;
            LogEntry(entry);
        }

        public void LogStatus(string targetId, string targetName, string statusId, bool applied)
        {
            var entry = new CombatLogEntry { Type = applied ? CombatLogEntryType.StatusApplied : CombatLogEntryType.StatusRemoved, TargetId = targetId, TargetName = targetName };
            entry.Data["statusId"] = statusId;
            LogEntry(entry);
        }

        public void LogTurnStart(string combatantId, string name, int round, int turn)
        {
            SetContext(round, turn);
            LogEntry(new CombatLogEntry { Type = CombatLogEntryType.TurnStarted, SourceId = combatantId, SourceName = name });
        }

        public void LogTurnEnd(string combatantId, string name)
        {
            LogEntry(new CombatLogEntry { Type = CombatLogEntryType.TurnEnded, SourceId = combatantId, SourceName = name });
        }

        public List<CombatLogEntry> GetEntries(CombatLogFilter filter = null)
        {
            if (filter == null) return new List<CombatLogEntry>(_entries);
            return _entries.Where(e => filter.Matches(e)).ToList();
        }

        public List<CombatLogEntry> GetRecentEntries(int count = 20)
        {
            return _entries.Skip(Math.Max(0, _entries.Count - count)).ToList();
        }

        public string ExportToJson()
        {
            var exportData = new List<Dictionary<string, object>>();
            foreach (var entry in _entries)
            {
                var dict = new Dictionary<string, object> { { "entryId", entry.EntryId }, { "type", entry.Type.ToString() }, { "severity", entry.Severity.ToString() }, { "timestamp", entry.Timestamp.ToString("O") }, { "round", entry.Round }, { "turn", entry.Turn }, { "message", entry.Format() } };
                if (!string.IsNullOrEmpty(entry.SourceId)) { dict["sourceId"] = entry.SourceId; dict["sourceName"] = entry.SourceName; }
                if (!string.IsNullOrEmpty(entry.TargetId)) { dict["targetId"] = entry.TargetId; dict[ "targetName"] = entry.TargetName; }
                if (entry.Value != 0) dict["value"] = entry.Value;
                if (entry.Breakdown.Count > 0) dict["breakdown"] = entry.Breakdown;
                if (entry.Data.Count > 0) dict["data"] = entry.Data;
                if (entry.IsCritical) dict["isCritical"] = true;
                if (entry.IsMiss) dict["isMiss"] = true;
                exportData.Add(dict);
            }
            return JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        }

        public string ExportToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== COMBAT LOG ===");
            foreach (var entry in _entries) sb.AppendLine(entry.ToString());
            sb.AppendLine($"===END ({_entries.Count} entries) ===");
            return sb.ToString();
        }

        public void Clear()
        {
            _entries.Clear();
            _currentRound = 0;
            _currentTurn = 0;
        }

        public void LogStateTransition(StateTransitionEvent evt)
        {
            var entry = new CombatLogEntry { Type = CombatLogEntryType.Debug, Severity = LogSeverity.Verbose, Message = $"{evt.FromState} -> {evt.ToState}" + (string.IsNullOrEmpty(evt.Reason) ? "" : $" ({evt.Reason})") };
            entry.Data["fromState"] = evt.FromState.ToString();
            entry.Data["toState"] = evt.ToState.ToString();
            entry.Data["reason"] = evt.Reason;
            LogEntry(entry);
        }

        public void LogTurnChange(TurnChangeEvent evt)
        {
            _currentRound = evt.Round;
            _currentTurn = evt.TurnIndex;
            var entry = new CombatLogEntry { Type = CombatLogEntryType.TurnStarted, SourceId = evt.CurrentCombatant?.Id, SourceName = evt.CurrentCombatant?.Name, Message = $"Round {evt.Round}, Turn {evt.TurnIndex}: {evt.CurrentCombatant?.Name ?? "None"}" };
            entry.Data["previousCombatant"] = evt.PreviousCombatant?.Id;
            LogEntry(entry);
        }

        public void LogCommand(CommandExecutedEvent evt)
        {
            var entry = new CombatLogEntry { Type = CombatLogEntryType.Debug, Severity = LogSeverity.Verbose, SourceId = evt.Command.CombatantId, Message = $"{evt.Command.Type} by {evt.Command.CombatantId}: {(evt.Success ? "Success" : "Failed")} - {evt.Result}" };
            entry.Data["commandId"] = evt.Command.CommandId;
            entry.Data["commandType"] = evt.Command.Type.ToString();
            entry.Data["success"] = evt.Success;
            entry.Data["result"] = evt.Result;
            entry.Data["commandData"] = evt.Command.ToEventData();
            LogEntry(entry);
        }

        public void Log(string message, Dictionary<string, object> data = null)
        {
            var entry = new CombatLogEntry { Type = CombatLogEntryType.Debug, Message = message };
            if (data != null)
                foreach (var kvp in data)
                    entry.Data[kvp.Key] = kvp.Value;
            LogEntry(entry);
        }

        public void LogCombatStart(int combatantCount, int seed)
        {
            var entry = new CombatLogEntry { Type = CombatLogEntryType.CombatStarted, Severity = LogSeverity.Important, Message = $"Combat started with {combatantCount} combatants, seed {seed}" };
            entry.Data["combatantCount"] = combatantCount;
            entry.Data["seed"] = seed;
            LogEntry(entry);
        }

        public void LogCombatEnd(string result)
        {
            var entry = new CombatLogEntry { Type = CombatLogEntryType.CombatEnded, Severity = LogSeverity.Important, Message = $"Combat ended: {result}" };
            entry.Data["result"] = result;
            LogEntry(entry);
        }

        public List<CombatLogEntry> GetEntriesByType(LogEntryType legacyType)
        {
            var newType = legacyType switch {
                LogEntryType.StateTransition => CombatLogEntryType.Debug,
                LogEntryType.TurnChange => CombatLogEntryType.TurnStarted,
                LogEntryType.DamageDealt => CombatLogEntryType.DamageDealt,
                LogEntryType.HealApplied => CombatLogEntryType.HealingDone,
                LogEntryType.StatusApplied => CombatLogEntryType.StatusApplied,
                LogEntryType.StatusRemoved => CombatLogEntryType.StatusRemoved,
                LogEntryType.CombatStart => CombatLogEntryType.CombatStarted,
                LogEntryType.CombatEnd => CombatLogEntryType.CombatEnded,
                _ => CombatLogEntryType.Debug
            };
            return _entries.Where(e => e.Type == newType).ToList();
        }

        public List<CombatLogEntry> GetEntriesByRound(int round) => _entries.Where(e => e.Round == round).ToList();

        public int CalculateHash()
        {
            unchecked
            {
                int hash = 17;
                foreach (var entry in _entries)
                {
                    hash = hash * 31 + entry.Type.GetHashCode();
                    hash = hash * 31 + (entry.Message?.GetHashCode() ?? 0);
                    hash = hash * 31 + entry.Round;
                    hash = hash * 31 + entry.Turn;
                }
                return hash;
            }
        }

        public string GetFormattedLog() => ExportToText();
    }
}
