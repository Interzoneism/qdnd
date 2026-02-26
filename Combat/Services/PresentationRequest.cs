using System;
using System.Collections.Generic;
using System.Numerics;
using QDND.Combat.Actions;
using QDND.Data.CharacterModel;

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

    public enum VfxEventPhase
    {
        Start,
        Projectile,
        Impact,
        Area,
        Status,
        Death,
        Heal,
        Custom
    }

    public enum VfxTargetPattern
    {
        Point,
        PerTarget,
        Circle,
        Cone,
        Line,
        Path,
        SourceAura,
        TargetAura
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

        public string PresetId { get; set; }
        public string ActionId { get; set; }
        public string VariantId { get; set; }
        public string SourceId { get; set; }
        public string PrimaryTargetId { get; set; }
        public List<string> TargetIds { get; } = new();

        public Vector3? SourcePosition { get; set; }
        public Vector3? TargetPosition { get; set; }
        public Vector3? CastPosition { get; set; }
        public Vector3? Direction { get; set; }
        public List<Vector3> TargetPositions { get; } = new();

        public AttackType? AttackType { get; set; }
        public TargetType? TargetType { get; set; }
        public DamageType? DamageType { get; set; }
        public VerbalIntent? Intent { get; set; }
        public bool IsSpell { get; set; }
        public QDND.Combat.Actions.SpellSchool SpellSchool { get; set; } = QDND.Combat.Actions.SpellSchool.None;
        public string SpellType { get; set; }
        public string ActionVfxId { get; set; }
        public string VariantVfxId { get; set; }
        public bool IsCritical { get; set; }
        public bool DidKill { get; set; }

        public VfxEventPhase Phase { get; set; }
        public VfxTargetPattern Pattern { get; set; } = VfxTargetPattern.Point;
        public float Magnitude { get; set; } = 1f;
        public int Seed { get; set; }

        public VfxRequest(string correlationId, VfxEventPhase phase)
            : base(correlationId)
        {
            Phase = phase;
        }
    }

    /// <summary>
    /// Request to play a sound effect.
    /// </summary>
    public class SfxRequest : PresentationRequest
    {
        public override PresentationRequestType Type => PresentationRequestType.SFX;

        public string SoundId { get; }

        public string ActionId { get; set; }
        public string VariantId { get; set; }
        public string SourceId { get; set; }
        public string PrimaryTargetId { get; set; }
        public List<string> TargetIds { get; } = new();

        public Vector3? SourcePosition { get; set; }
        public Vector3? TargetPosition { get; set; }
        public Vector3? CastPosition { get; set; }
        public Vector3? Direction { get; set; }

        public AttackType? AttackType { get; set; }
        public TargetType? TargetType { get; set; }
        public DamageType? DamageType { get; set; }
        public VerbalIntent? Intent { get; set; }

        public VfxEventPhase Phase { get; set; } = VfxEventPhase.Custom;
        public VfxTargetPattern Pattern { get; set; } = VfxTargetPattern.Point;
        public float Magnitude { get; set; } = 1f;
        public int Seed { get; set; }

        public SfxRequest(string correlationId, string soundId)
            : base(correlationId)
        {
            SoundId = soundId ?? throw new ArgumentNullException(nameof(soundId));
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
