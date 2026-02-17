using System.Collections.Generic;

namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot link for a sustained effect owned by concentration.
    /// </summary>
    public class ConcentrationEffectSnapshot
    {
        /// <summary>
        /// The status effect applied by concentration.
        /// </summary>
        public string StatusId { get; set; } = string.Empty;

        /// <summary>
        /// The target affected by this effect.
        /// </summary>
        public string TargetId { get; set; } = string.Empty;

        /// <summary>
        /// Optional precise status instance ID for exact cleanup.
        /// </summary>
        public string StatusInstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Optional surface instance ID linked to this concentration effect.
        /// </summary>
        public string SurfaceInstanceId { get; set; } = string.Empty;
    }

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
        public string ActionId { get; set; } = string.Empty;

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

        /// <summary>
        /// Linked sustained effects to remove when concentration ends.
        /// </summary>
        public List<ConcentrationEffectSnapshot> LinkedEffects { get; set; } = new();
    }
}
