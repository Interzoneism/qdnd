namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of a pending reaction prompt awaiting player decision.
    /// </summary>
    public class ReactionPromptSnapshot
    {
        /// <summary>
        /// Unique ID for this prompt.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Type of prompt (e.g., "OpportunityAttack", "Counterspell").
        /// </summary>
        public string PromptType { get; set; } = string.Empty;

        /// <summary>
        /// ID of the combatant being prompted.
        /// </summary>
        public string SourceCombatantId { get; set; } = string.Empty;

        /// <summary>
        /// ID of the combatant triggering the reaction (may be empty).
        /// </summary>
        public string TargetCombatantId { get; set; } = string.Empty;

        /// <summary>
        /// Round number when this prompt expires (0 = no timeout).
        /// </summary>
        public int TimeoutRound { get; set; }
    }
}
