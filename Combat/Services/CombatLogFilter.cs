using System;
using System.Collections.Generic;
using System.Linq;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Filter criteria for combat log entries.
    /// </summary>
    public class CombatLogFilter
    {
        /// <summary>
        /// Minimum severity to include.
        /// </summary>
        public LogSeverity MinSeverity { get; set; } = LogSeverity.Normal;

        /// <summary>
        /// Only include these entry types (null = all).
        /// </summary>
        public HashSet<CombatLogEntryType> IncludeTypes { get; set; }

        /// <summary>
        /// Exclude these entry types.
        /// </summary>
        public HashSet<CombatLogEntryType> ExcludeTypes { get; set; } = new();

        /// <summary>
        /// Only entries involving this combatant.
        /// </summary>
        public string CombatantId { get; set; }

        /// <summary>
        /// Only entries from this round.
        /// </summary>
        public int? Round { get; set; }

        /// <summary>
        /// Only entries with these tags.
        /// </summary>
        public HashSet<string> RequiredTags { get; set; }

        /// <summary>
        /// Only entries after this time.
        /// </summary>
        public DateTime? After { get; set; }

        /// <summary>
        /// Only entries before this time.
        /// </summary>
        public DateTime? Before { get; set; }

        /// <summary>
        /// Search text in message.
        /// </summary>
        public string SearchText { get; set; }

        /// <summary>
        /// Test if an entry matches this filter.
        /// </summary>
        public bool Matches(CombatLogEntry entry)
        {
            // Severity check
            if (entry.Severity < MinSeverity)
                return false;

            // Type include check
            if (IncludeTypes != null && IncludeTypes.Count > 0 && !IncludeTypes.Contains(entry.Type))
                return false;

            // Type exclude check
            if (ExcludeTypes.Contains(entry.Type))
                return false;

            // Combatant check
            if (!string.IsNullOrEmpty(CombatantId))
            {
                if (entry.SourceId != CombatantId && entry.TargetId != CombatantId)
                    return false;
            }

            // Round check
            if (Round.HasValue && entry.Round != Round.Value)
                return false;

            // Tag check
            if (RequiredTags != null && RequiredTags.Count > 0)
            {
                if (!RequiredTags.All(t => entry.Tags.Contains(t)))
                    return false;
            }

            // Time check
            if (After.HasValue && entry.Timestamp < After.Value)
                return false;
            if (Before.HasValue && entry.Timestamp > Before.Value)
                return false;

            // Search text
            if (!string.IsNullOrEmpty(SearchText))
            {
                string formatted = entry.Format().ToLowerInvariant();
                if (!formatted.Contains(SearchText.ToLowerInvariant()))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Create a filter for a specific combatant.
        /// </summary>
        public static CombatLogFilter ForCombatant(string combatantId)
        {
            return new CombatLogFilter { CombatantId = combatantId };
        }

        /// <summary>
        /// Create a filter for specific entry types.
        /// </summary>
        public static CombatLogFilter ForTypes(params CombatLogEntryType[] types)
        {
            return new CombatLogFilter { IncludeTypes = new HashSet<CombatLogEntryType>(types) };
        }

        /// <summary>
        /// Create a filter for important events only.
        /// </summary>
        public static CombatLogFilter ImportantOnly()
        {
            return new CombatLogFilter { MinSeverity = LogSeverity.Important };
        }
    }
}
