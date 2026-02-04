using System;
using System.Collections.Generic;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Type of log entry.
    /// </summary>
    public enum CombatLogEntryType
    {
        // Combat flow
        CombatStarted,
        CombatEnded,
        RoundStarted,
        RoundEnded,
        TurnStarted,
        TurnEnded,

        // Actions
        MovementStarted,
        MovementCompleted,
        AttackDeclared,
        AttackResolved,
        AbilityUsed,

        // Outcomes
        DamageDealt,
        HealingDone,
        StatusApplied,
        StatusRemoved,
        StatusTicked,

        // Reactions
        ReactionTriggered,
        ReactionUsed,
        ReactionDeclined,

        // Special
        SurfaceCreated,
        SurfaceEntered,
        SurfaceRemoved,
        ForcedMovement,
        FallDamage,

        // System
        Debug,
        Error
    }

    /// <summary>
    /// Severity level for filtering.
    /// </summary>
    public enum LogSeverity
    {
        Verbose,    // Everything including debug
        Normal,     // Standard gameplay events
        Important,  // Key events (kills, crits, reactions)
        Critical    // Only errors and critical events
    }

    /// <summary>
    /// A rich combat log entry with full context.
    /// </summary>
    public class CombatLogEntry
    {
        /// <summary>
        /// Unique entry ID.
        /// </summary>
        public string EntryId { get; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// Type of entry.
        /// </summary>
        public CombatLogEntryType Type { get; set; }

        /// <summary>
        /// Severity for filtering.
        /// </summary>
        public LogSeverity Severity { get; set; } = LogSeverity.Normal;

        /// <summary>
        /// Timestamp when this occurred.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Combat round when this occurred.
        /// </summary>
        public int Round { get; set; }

        /// <summary>
        /// Turn number within round.
        /// </summary>
        public int Turn { get; set; }

        /// <summary>
        /// Source combatant ID (who did it).
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// Source combatant name for display.
        /// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// Target combatant ID.
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Target combatant name for display.
        /// </summary>
        public string TargetName { get; set; }

        /// <summary>
        /// Primary numeric value (damage, healing, etc).
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// Secondary value if needed.
        /// </summary>
        public float SecondaryValue { get; set; }

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// Detailed breakdown of calculation.
        /// </summary>
        public Dictionary<string, object> Breakdown { get; set; } = new();

        /// <summary>
        /// Additional tags for filtering.
        /// </summary>
        public HashSet<string> Tags { get; set; } = new();

        /// <summary>
        /// Additional data.
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();

        /// <summary>
        /// Was this a critical hit/success?
        /// </summary>
        public bool IsCritical { get; set; }

        /// <summary>
        /// Was this a miss/failure?
        /// </summary>
        public bool IsMiss { get; set; }

        /// <summary>
        /// Add breakdown component.
        /// </summary>
        public void AddBreakdown(string key, object value)
        {
            Breakdown[key] = value;
        }

        /// <summary>
        /// Format for display.
        /// </summary>
        public string Format()
        {
            if (!string.IsNullOrEmpty(Message))
                return Message;

            return Type switch
            {
                CombatLogEntryType.DamageDealt => $"{SourceName} deals {Value} damage to {TargetName}",
                CombatLogEntryType.HealingDone => $"{SourceName} heals {TargetName} for {Value}",
                CombatLogEntryType.AttackDeclared => $"{SourceName} attacks {TargetName}",
                CombatLogEntryType.StatusApplied => $"{TargetName} is affected by {Data.GetValueOrDefault("statusId", "status")}",
                CombatLogEntryType.TurnStarted => $"{SourceName}'s turn begins",
                CombatLogEntryType.TurnEnded => $"{SourceName}'s turn ends",
                _ => $"[{Type}] {SourceName ?? ""} -> {TargetName ?? ""}"
            };
        }

        public override string ToString()
        {
            string crit = IsCritical ? " [CRIT]" : "";
            string miss = IsMiss ? " [MISS]" : "";
            return $"[R{Round}T{Turn}] {Format()}{crit}{miss}";
        }
    }
}
