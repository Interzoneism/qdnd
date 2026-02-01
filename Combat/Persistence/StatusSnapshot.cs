using System.Collections.Generic;

namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of an active status effect.
    /// </summary>
    public class StatusSnapshot
    {
        /// <summary>
        /// Unique instance ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Status definition ID (e.g., "poisoned", "blessed").
        /// </summary>
        public string StatusDefinitionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Combatant affected by this status.
        /// </summary>
        public string TargetCombatantId { get; set; } = string.Empty;
        
        /// <summary>
        /// Combatant who applied this status.
        /// </summary>
        public string SourceCombatantId { get; set; } = string.Empty;
        
        /// <summary>
        /// Number of stacks of this status.
        /// </summary>
        public int StackCount { get; set; }
        
        /// <summary>
        /// Remaining duration in rounds/turns.
        /// </summary>
        public int RemainingDuration { get; set; }
        
        /// <summary>
        /// Custom status-specific data (e.g., damage type, intensity).
        /// </summary>
        public Dictionary<string, string> CustomData { get; set; } = new();
    }
}
