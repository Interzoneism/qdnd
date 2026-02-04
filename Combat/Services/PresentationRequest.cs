using System;
using System.Numerics;

namespace QDND.Combat.Services
{
    /// <summary>
    /// Type of presentation request.
    /// </summary>
    public enum PresentationRequestType
    {
        VFX,
        SFX,
        CameraFocus,
        CameraRelease
    }

    /// <summary>
    /// Base class for presentation requests (VFX, SFX, camera).
    /// </summary>
    public abstract class PresentationRequest
    {
        /// <summary>
        /// Type of presentation request.
        /// </summary>
        public abstract PresentationRequestType Type { get; }

        /// <summary>
        /// Correlation ID linking to the action/ability instance.
        /// </summary>
        public string CorrelationId { get; }

        /// <summary>
        /// Timestamp when request was created.
        /// </summary>
        public long Timestamp { get; internal set; }

        protected PresentationRequest(string correlationId)
        {
            CorrelationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        }
    }

    /// <summary>
    /// Request to play a visual effect.
    /// </summary>
    public class VfxRequest : PresentationRequest
    {
        public override PresentationRequestType Type => PresentationRequestType.VFX;

        /// <summary>
        /// ID of the VFX to play (e.g., "vfx_fireball").
        /// </summary>
        public string EffectId { get; }

        /// <summary>
        /// Position where the effect should appear.
        /// </summary>
        public Vector3 Position { get; }

        /// <summary>
        /// Optional target entity ID for attached effects.
        /// </summary>
        public string TargetId { get; }

        public VfxRequest(string correlationId, string effectId, Vector3 position, string targetId = null)
            : base(correlationId)
        {
            EffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
            Position = position;
            TargetId = targetId;
        }
    }

    /// <summary>
    /// Request to play a sound effect.
    /// </summary>
    public class SfxRequest : PresentationRequest
    {
        public override PresentationRequestType Type => PresentationRequestType.SFX;

        /// <summary>
        /// ID of the sound to play (e.g., "sfx_sword_slash").
        /// </summary>
        public string SoundId { get; }

        /// <summary>
        /// Optional position for 3D sound (null for 2D).
        /// </summary>
        public Vector3? Position { get; }

        public SfxRequest(string correlationId, string soundId, Vector3? position = null)
            : base(correlationId)
        {
            SoundId = soundId ?? throw new ArgumentNullException(nameof(soundId));
            Position = position;
        }
    }

    /// <summary>
    /// Request to focus camera on a target or position.
    /// </summary>
    public class CameraFocusRequest : PresentationRequest
    {
        public override PresentationRequestType Type => PresentationRequestType.CameraFocus;

        /// <summary>
        /// Target entity ID to focus on (if tracking an entity).
        /// </summary>
        public string TargetId { get; }

        /// <summary>
        /// Position to focus on (if focusing a location).
        /// </summary>
        public Vector3? Position { get; }

        public CameraFocusRequest(string correlationId, string targetId = null, Vector3? position = null)
            : base(correlationId)
        {
            if (string.IsNullOrEmpty(targetId) && position == null)
                throw new ArgumentException("Either targetId or position must be provided.");

            TargetId = targetId;
            Position = position;
        }
    }

    /// <summary>
    /// Request to release camera focus.
    /// </summary>
    public class CameraReleaseRequest : PresentationRequest
    {
        public override PresentationRequestType Type => PresentationRequestType.CameraRelease;

        public CameraReleaseRequest(string correlationId)
            : base(correlationId)
        {
        }
    }
}
