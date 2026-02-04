namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of a surface/field effect.
    /// </summary>
    public class SurfaceSnapshot
    {
        /// <summary>
        /// Unique instance ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Surface type (fire, water, poison, etc.).
        /// </summary>
        public string SurfaceType { get; set; } = string.Empty;

        // --- Position ---

        /// <summary>
        /// X coordinate of center position.
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// Y coordinate of center position.
        /// </summary>
        public float PositionY { get; set; }

        /// <summary>
        /// Z coordinate of center position.
        /// </summary>
        public float PositionZ { get; set; }

        // --- Area ---

        /// <summary>
        /// Radius of the surface effect.
        /// </summary>
        public float Radius { get; set; }

        // --- Duration ---

        /// <summary>
        /// Remaining duration in rounds (0 = permanent).
        /// </summary>
        public int RemainingDuration { get; set; }

        // --- Owner ---

        /// <summary>
        /// ID of combatant who created this surface.
        /// </summary>
        public string OwnerCombatantId { get; set; } = string.Empty;
    }
}
