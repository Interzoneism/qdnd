namespace QDND.Combat.Rules.Boosts
{
    /// <summary>
    /// Represents a boost that is currently active on a combatant.
    /// Tracks the boost definition along with its source information for removal purposes.
    /// 
    /// Example:
    /// - A "BLESSED" status applies "Advantage(AttackRoll)" → ActiveBoost with Source="Status", SourceId="BLESSED"
    /// - When the status expires, all ActiveBoosts with SourceId="BLESSED" are removed
    /// </summary>
    public class ActiveBoost
    {
        /// <summary>
        /// The boost definition containing type, parameters, and optional condition.
        /// </summary>
        public BoostDefinition Definition { get; set; }

        /// <summary>
        /// The type of entity that granted this boost.
        /// Examples: "Status", "Passive", "Equipment", "Spell", "Feat"
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// The specific instance ID of the source.
        /// Examples: "BLESSED" (status ID), "RAGE" (passive ID), "PLATE_ARMOR" (equipment ID)
        /// Used to identify which boosts to remove when a source expires.
        /// </summary>
        public string SourceId { get; set; }

        /// <summary>
        /// Create a new active boost.
        /// </summary>
        /// <param name="definition">The boost definition</param>
        /// <param name="source">The type of source that granted this boost</param>
        /// <param name="sourceId">The specific instance ID of the source</param>
        public ActiveBoost(BoostDefinition definition, string source, string sourceId)
        {
            Definition = definition;
            Source = source;
            SourceId = sourceId;
        }

        /// <summary>
        /// Returns true if this boost is conditional (has an IF clause).
        /// Conditional boosts are only evaluated when their condition is met.
        /// </summary>
        public bool IsConditional => Definition?.IsConditional ?? false;

        /// <summary>
        /// Returns true if this boost matches the specified source and source ID.
        /// Used for removing boosts when a specific source expires.
        /// </summary>
        public bool IsFromSource(string source, string sourceId)
        {
            return Source == source && SourceId == sourceId;
        }

        public override string ToString()
        {
            return $"{Definition} [From: {Source}/{SourceId}]";
        }
    }
}
