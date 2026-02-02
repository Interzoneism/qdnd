namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of an active concentration effect for save/load.
    /// </summary>
    public class ConcentrationSnapshot
    {
        /// <summary>
        /// The combatant who is concentrating.
        /// </summary>
        public string CombatantId { get; set; } = string.Empty;

        /// <summary>
        /// The ability being concentrated on.
        /// </summary>
        public string AbilityId { get; set; } = string.Empty;

        /// <summary>
        /// The status effect applied by the concentration.
        /// </summary>
        public string StatusId { get; set; } = string.Empty;

        /// <summary>
        /// The target affected by the concentration effect.
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// When concentration started (Unix timestamp milliseconds).
        /// </summary>
        public long StartedAt { get; set; }
    }
}
