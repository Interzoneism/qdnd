using System;
using Godot;

namespace QDND.Combat.Environment
{
    /// <summary>
    /// Active surface instance in the world.
    /// </summary>
    public class SurfaceInstance
    {
        /// <summary>
        /// Unique instance ID.
        /// </summary>
        public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

        /// <summary>
        /// The definition of this surface.
        /// </summary>
        public SurfaceDefinition Definition { get; }

        /// <summary>
        /// Center position of the surface.
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Radius of the surface.
        /// </summary>
        public float Radius { get; set; }

        /// <summary>
        /// Who created this surface.
        /// </summary>
        public string CreatorId { get; set; }

        /// <summary>
        /// Remaining duration in rounds.
        /// </summary>
        public int RemainingDuration { get; set; }

        /// <summary>
        /// When this surface was created.
        /// </summary>
        public long CreatedAt { get; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Is this surface permanent?
        /// </summary>
        public bool IsPermanent => Definition.DefaultDuration == 0;

        public SurfaceInstance(SurfaceDefinition definition)
        {
            Definition = definition;
            RemainingDuration = definition.DefaultDuration;
        }

        /// <summary>
        /// Check if a position is within this surface.
        /// </summary>
        public bool ContainsPosition(Vector3 pos)
        {
            float distance = Position.DistanceTo(pos);
            return distance <= Radius;
        }

        /// <summary>
        /// Tick duration at round end.
        /// </summary>
        public bool Tick()
        {
            if (IsPermanent)
                return true;

            RemainingDuration--;
            return RemainingDuration > 0;
        }

        public override string ToString()
        {
            string duration = IsPermanent ? "permanent" : $"{RemainingDuration} rounds";
            return $"[Surface:{Definition.Name}] at {Position}, radius {Radius}, {duration}";
        }
    }
}
