namespace QDND.Combat.Persistence
{
    public class SurfaceBlobSnapshot
    {
        public float CenterX { get; set; }
        public float CenterY { get; set; }
        public float CenterZ { get; set; }
        public float Radius { get; set; }
    }

    public class SurfaceCellSnapshot
    {
        public int X { get; set; }
        public int Z { get; set; }
    }

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
        /// Approximate enclosing radius of the surface effect.
        /// </summary>
        public float Radius { get; set; }

        /// <summary>
        /// Authoritative occupied surface cells.
        /// </summary>
        public System.Collections.Generic.List<SurfaceCellSnapshot> Cells { get; set; } = new();

        /// <summary>
        /// Legacy blob geometry format used by older saves.
        /// Kept for backward compatibility when importing old snapshots.
        /// </summary>
        public System.Collections.Generic.List<SurfaceBlobSnapshot> Blobs { get; set; } = new();

        // --- Duration ---

        /// <summary>
        /// Remaining duration in rounds (0 = permanent).
        /// </summary>
        public int RemainingDuration { get; set; }

        /// <summary>
        /// Rounds since last surface growth tick.
        /// </summary>
        public int RoundsSinceLastGrowth { get; set; }

        // --- Owner ---

        /// <summary>
        /// ID of combatant who created this surface.
        /// </summary>
        public string OwnerCombatantId { get; set; } = string.Empty;
    }
}
