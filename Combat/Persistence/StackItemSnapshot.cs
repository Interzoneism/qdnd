#nullable enable

namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of a resolution stack item (action, reaction, or effect in progress).
    /// Used for saving mid-reaction or mid-effect resolution.
    /// </summary>
    public class StackItemSnapshot
    {
        /// <summary>
        /// Unique ID for this stack item.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Type of action being resolved.
        /// </summary>
        public string ActionType { get; set; } = string.Empty;

        /// <summary>
        /// Who is performing this action.
        /// </summary>
        public string SourceCombatantId { get; set; } = string.Empty;

        /// <summary>
        /// Who is targeted (null if no target).
        /// </summary>
        public string? TargetCombatantId { get; set; }

        /// <summary>
        /// Is this item cancelled?
        /// </summary>
        public bool IsCancelled { get; set; }

        /// <summary>
        /// Depth in the stack (0 = top level action, 1+ = reactions/interrupts).
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Serialized payload data specific to the item type.
        /// This is typically a JSON string containing ability/effect specific data.
        /// </summary>
        public string PayloadData { get; set; } = string.Empty;
    }
}
