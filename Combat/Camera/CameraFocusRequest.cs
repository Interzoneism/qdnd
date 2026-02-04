using System;
using Godot;

namespace QDND.Combat.Camera
{
    /// <summary>
    /// Type of camera focus.
    /// </summary>
    public enum CameraFocusType
    {
        Combatant,     // Focus on a combatant
        Position,      // Focus on a world position
        TwoShot,       // Frame two combatants
        AoE,           // Frame an area
        Overhead,      // Strategic overview
        Free           // User-controlled
    }

    /// <summary>
    /// Priority for camera focus requests.
    /// </summary>
    public enum CameraPriority
    {
        Low = 0,
        Normal = 50,
        High = 100,
        Critical = 200    // Death, critical hit, etc.
    }

    /// <summary>
    /// Request to change camera focus.
    /// </summary>
    public class CameraFocusRequest
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Type of focus.
        /// </summary>
        public CameraFocusType Type { get; set; }

        /// <summary>
        /// Priority level.
        /// </summary>
        public CameraPriority Priority { get; set; } = CameraPriority.Normal;

        /// <summary>
        /// Target combatant ID (for Combatant/TwoShot).
        /// </summary>
        public string TargetId { get; set; }

        /// <summary>
        /// Secondary target (for TwoShot).
        /// </summary>
        public string SecondaryTargetId { get; set; }

        /// <summary>
        /// Target position (for Position/AoE).
        /// </summary>
        public Vector3? Position { get; set; }

        /// <summary>
        /// Radius to frame (for AoE).
        /// </summary>
        public float? Radius { get; set; }

        /// <summary>
        /// Duration in seconds.
        /// </summary>
        public float Duration { get; set; } = 1f;

        /// <summary>
        /// Transition time to this focus.
        /// </summary>
        public float TransitionTime { get; set; } = 0.3f;

        /// <summary>
        /// Camera distance override.
        /// </summary>
        public float? DistanceOverride { get; set; }

        /// <summary>
        /// Camera angle override (degrees from horizontal).
        /// </summary>
        public float? AngleOverride { get; set; }

        /// <summary>
        /// Enable slow motion.
        /// </summary>
        public bool SlowMotion { get; set; }

        /// <summary>
        /// Slow motion time scale.
        /// </summary>
        public float SlowMotionScale { get; set; } = 0.3f;

        /// <summary>
        /// Source of request for debugging.
        /// </summary>
        public string Source { get; set; }

        public CameraFocusRequest()
        {
            Id = Guid.NewGuid().ToString();
        }

        // Factory methods

        public static CameraFocusRequest FocusCombatant(string combatantId, float duration = 1f,
            CameraPriority priority = CameraPriority.Normal)
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.Combatant,
                TargetId = combatantId,
                Duration = duration,
                Priority = priority
            };
        }

        public static CameraFocusRequest TwoShot(string attackerId, string targetId, float duration = 1.5f)
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.TwoShot,
                TargetId = attackerId,
                SecondaryTargetId = targetId,
                Duration = duration,
                Priority = CameraPriority.High
            };
        }

        public static CameraFocusRequest FocusAoE(Vector3 center, float radius, float duration = 2f)
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.AoE,
                Position = center,
                Radius = radius,
                Duration = duration,
                Priority = CameraPriority.High,
                AngleOverride = 60 // More overhead for AoE
            };
        }

        public static CameraFocusRequest Overhead(float duration = 2f)
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.Overhead,
                Duration = duration,
                Priority = CameraPriority.Normal,
                AngleOverride = 75
            };
        }

        public static CameraFocusRequest CriticalHit(string targetId)
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.Combatant,
                TargetId = targetId,
                Duration = 0.8f,
                Priority = CameraPriority.Critical,
                SlowMotion = true,
                SlowMotionScale = 0.2f,
                DistanceOverride = 5f
            };
        }

        public static CameraFocusRequest Death(string combatantId)
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.Combatant,
                TargetId = combatantId,
                Duration = 2f,
                Priority = CameraPriority.Critical,
                SlowMotion = true,
                SlowMotionScale = 0.3f
            };
        }

        public static CameraFocusRequest Release()
        {
            return new CameraFocusRequest
            {
                Type = CameraFocusType.Free,
                Duration = 0,
                Priority = CameraPriority.Low
            };
        }
    }
}
