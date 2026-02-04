namespace QDND.Combat.Movement
{
    /// <summary>
    /// Type of movement.
    /// </summary>
    public enum MovementType
    {
        Walk,       // Normal walking movement
        Jump,       // Horizontal jump (uses movement + special calculation)
        HighJump,   // Vertical jump
        Climb,      // Climb vertical surfaces (half speed usually)
        Swim,       // Movement in water
        Fly,        // Flying movement
        Teleport,   // Instant relocation (like Misty Step)
        Dash        // Double movement speed
    }

    /// <summary>
    /// Configuration for a specific movement type.
    /// </summary>
    public class MovementTypeConfig
    {
        public MovementType Type { get; set; }

        /// <summary>
        /// Speed multiplier (0.5 for climb = half speed).
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>
        /// Does this movement provoke opportunity attacks?
        /// </summary>
        public bool ProvokesOpportunityAttacks { get; set; } = true;

        /// <summary>
        /// Can this movement traverse difficult terrain?
        /// </summary>
        public bool IgnoresDifficultTerrain { get; set; }

        /// <summary>
        /// Requires a specific stat check (STR for jump/climb).
        /// </summary>
        public string RequiredStatCheck { get; set; }

        /// <summary>
        /// Fixed range for this movement (teleport has fixed range based on spell).
        /// </summary>
        public float? FixedRange { get; set; }
    }
}
