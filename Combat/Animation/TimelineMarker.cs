using System;
using Godot;

namespace QDND.Combat.Animation
{
    /// <summary>
    /// Type of timeline marker.
    /// </summary>
    public enum MarkerType
    {
        Start,
        Hit,              // When damage/effect should apply
        Projectile,       // When projectile is released
        AnimationEnd,
        Sound,
        VFX,
        CameraFocus,
        CameraRelease,
        Custom
    }

    /// <summary>
    /// A marker in an action timeline.
    /// </summary>
    public class TimelineMarker
    {
        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// Type of marker.
        /// </summary>
        public MarkerType Type { get; set; }
        
        /// <summary>
        /// Time in seconds from start.
        /// </summary>
        public float Time { get; set; }
        
        /// <summary>
        /// Optional label.
        /// </summary>
        public string Label { get; set; }
        
        /// <summary>
        /// Callback to invoke.
        /// </summary>
        public Action Callback { get; set; }
        
        /// <summary>
        /// Associated data (effect ID, sound path, etc).
        /// </summary>
        public string Data { get; set; }
        
        /// <summary>
        /// Has this marker been triggered?
        /// </summary>
        public bool Triggered { get; set; }
        
        /// <summary>
        /// Target for camera/VFX.
        /// </summary>
        public string TargetId { get; set; }
        
        /// <summary>
        /// Position for effects/camera.
        /// </summary>
        public Vector3? Position { get; set; }

        public TimelineMarker() 
        {
            Id = Guid.NewGuid().ToString();
        }

        public TimelineMarker(MarkerType type, float time, Action callback = null)
        {
            Id = Guid.NewGuid().ToString();
            Type = type;
            Time = time;
            Callback = callback;
        }

        public static TimelineMarker Start() => new(MarkerType.Start, 0f);
        
        public static TimelineMarker Hit(float time, Action callback = null) 
            => new(MarkerType.Hit, time, callback);
        
        public static TimelineMarker Projectile(float time, Action callback = null)
            => new(MarkerType.Projectile, time, callback);
        
        public static TimelineMarker End(float time) 
            => new(MarkerType.AnimationEnd, time);
        
        public static TimelineMarker Sound(float time, string soundPath)
            => new(MarkerType.Sound, time) { Data = soundPath };
        
        public static TimelineMarker VFX(float time, string vfxPath, Vector3? position = null)
            => new(MarkerType.VFX, time) { Data = vfxPath, Position = position };
        
        public static TimelineMarker CameraFocus(float time, string targetId)
            => new(MarkerType.CameraFocus, time) { TargetId = targetId };
        
        public static TimelineMarker CameraRelease(float time)
            => new(MarkerType.CameraRelease, time);
    }
}
