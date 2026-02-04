namespace QDND.Combat.Persistence
{
    /// <summary>
    /// Snapshot of a spawned prop/object on the battlefield.
    /// </summary>
    public class PropSnapshot
    {
        /// <summary>
        /// Unique ID for this prop.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Type of prop (e.g., "Barrel", "Wall", "Trap").
        /// </summary>
        public string PropType { get; set; } = string.Empty;

        /// <summary>
        /// X coordinate in world space.
        /// </summary>
        public float X { get; set; }

        /// <summary>
        /// Y coordinate in world space.
        /// </summary>
        public float Y { get; set; }

        /// <summary>
        /// Z coordinate in world space.
        /// </summary>
        public float Z { get; set; }

        /// <summary>
        /// Whether this prop can be interacted with.
        /// </summary>
        public bool IsInteractive { get; set; }

        /// <summary>
        /// Serialized custom data specific to the prop type.
        /// Typically a JSON string with prop-specific state.
        /// </summary>
        public string CustomData { get; set; } = string.Empty;
    }
}
