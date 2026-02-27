using Godot;
using QDND.Combat.Entities;

namespace QDND.Combat.Targeting;

/// <summary>
/// Centralized style tokens for all targeting visuals.
/// Data-driven: adjust these values to change the look of the entire targeting system.
/// No per-skill special casing — shared tokens + shared renderers.
/// </summary>
public static class TargetingStyleTokens
{
    // =========================================================================
    // COLORS
    // =========================================================================

    /// <summary>
    /// Color palette for targeting visuals — validity, relations, fills, paths, text, and markers.
    /// </summary>
    public static class Colors
    {
        // --- Validity colors ---

        /// <summary>Green — target is valid and in range.</summary>
        public static readonly Color Valid = new(0.2f, 0.85f, 0.3f, 0.8f);

        /// <summary>Red — target is invalid.</summary>
        public static readonly Color Invalid = new(0.95f, 0.2f, 0.15f, 0.8f);

        /// <summary>Yellow — valid with caution (friendly fire, partial cover).</summary>
        public static readonly Color Warning = new(1.0f, 0.85f, 0.1f, 0.8f);

        /// <summary>Grey — target is out of range.</summary>
        public static readonly Color OutOfRange = new(0.6f, 0.6f, 0.6f, 0.6f);

        // --- Relation colors (outlines, base rings) ---

        /// <summary>Red — hostile entity.</summary>
        public static readonly Color Enemy = new(0.95f, 0.25f, 0.2f, 0.8f);

        /// <summary>Blue — allied entity.</summary>
        public static readonly Color Ally = new(0.2f, 0.7f, 1.0f, 0.8f);

        /// <summary>Yellow — neutral entity.</summary>
        public static readonly Color Neutral = new(0.9f, 0.85f, 0.3f, 0.8f);

        /// <summary>Green — self / caster.</summary>
        public static readonly Color Self = new(0.3f, 1.0f, 0.5f, 0.8f);

        // --- Ground fill colors (lower alpha) ---

        /// <summary>Translucent green fill for valid areas.</summary>
        public static readonly Color ValidFill = new(0.2f, 0.85f, 0.3f, 0.15f);

        /// <summary>Translucent red fill for invalid areas.</summary>
        public static readonly Color InvalidFill = new(0.95f, 0.2f, 0.15f, 0.15f);

        /// <summary>Translucent yellow fill for warning areas.</summary>
        public static readonly Color WarningFill = new(1.0f, 0.85f, 0.1f, 0.12f);

        /// <summary>Orange-tinted fill highlighting friendly-fire zones.</summary>
        public static readonly Color FriendlyFireFill = new(1.0f, 0.6f, 0.1f, 0.18f);

        // --- Line / path colors ---

        /// <summary>Green line — clear movement path.</summary>
        public static readonly Color ClearPath = new(0.2f, 0.85f, 0.3f, 0.9f);

        /// <summary>Red line — blocked movement path.</summary>
        public static readonly Color BlockedPath = new(0.95f, 0.2f, 0.15f, 0.7f);

        // --- Text colors ---

        /// <summary>White — hit-chance percentage text.</summary>
        public static readonly Color HitChanceText = new(1.0f, 1.0f, 1.0f, 1.0f);

        /// <summary>Red — reason / error text.</summary>
        public static readonly Color ReasonText = new(1.0f, 0.3f, 0.2f, 1.0f);

        /// <summary>Light grey — range indicator text.</summary>
        public static readonly Color RangeText = new(0.8f, 0.8f, 0.8f, 0.9f);

        // --- Marker colors ---

        /// <summary>Green marker — valid impact point.</summary>
        public static readonly Color ValidMarker = new(0.2f, 0.85f, 0.3f, 0.9f);

        /// <summary>Red marker — invalid impact point.</summary>
        public static readonly Color InvalidMarker = new(0.95f, 0.2f, 0.15f, 0.9f);

        /// <summary>Orange marker — blocked impact point.</summary>
        public static readonly Color BlockedMarker = new(0.95f, 0.5f, 0.1f, 0.9f);

        // --- Multi-target ---

        /// <summary>Cyan — currently selected target in multi-target mode.</summary>
        public static readonly Color SelectedTarget = new(0.3f, 0.9f, 1.0f, 0.85f);

        /// <summary>Dimmer cyan — hint for next target slot.</summary>
        public static readonly Color NextTargetHint = new(0.3f, 0.9f, 1.0f, 0.4f);

        // --- Range ring ---

        /// <summary>Faint white ring showing max range.</summary>
        public static readonly Color RangeRing = new(1.0f, 1.0f, 1.0f, 0.25f);
    }

    // =========================================================================
    // STROKES
    // =========================================================================

    /// <summary>
    /// Stroke widths and dash patterns (world-space meters).
    /// </summary>
    public static class Strokes
    {
        // --- Widths ---

        /// <summary>Thin stroke — subtle outlines, distant indicators.</summary>
        public const float THIN = 0.02f;

        /// <summary>Medium stroke — default line width.</summary>
        public const float MEDIUM = 0.04f;

        /// <summary>Thick stroke — emphasized outlines.</summary>
        public const float THICK = 0.08f;

        /// <summary>Extra-thick stroke — heavy emphasis, drag handles.</summary>
        public const float EXTRA_THICK = 0.12f;

        // --- Ring / outline specific ---

        /// <summary>Stroke width for base-ring geometry.</summary>
        public const float RING_STROKE = 0.05f;

        /// <summary>Stroke width for outer outline rings.</summary>
        public const float OUTLINE_RING_STROKE = 0.03f;

        // --- Dash patterns (length, gap in world-space meters) ---

        /// <summary>Length of a single dash segment.</summary>
        public const float DASH_LENGTH = 0.15f;

        /// <summary>Gap between dash segments.</summary>
        public const float DASH_GAP = 0.1f;

        /// <summary>Length of a dot segment (shorter dash).</summary>
        public const float DOT_LENGTH = 0.06f;

        /// <summary>Gap between dot segments.</summary>
        public const float DOT_GAP = 0.12f;
    }

    // =========================================================================
    // MOTION
    // =========================================================================

    /// <summary>
    /// Animation timing and magnitudes for targeting visuals.
    /// </summary>
    public static class Motion
    {
        // --- Pulse animation ---

        /// <summary>Pulse frequency in cycles per second.</summary>
        public const float PULSE_SPEED_HZ = 2.0f;

        /// <summary>Minimum alpha during breathe animation.</summary>
        public const float ALPHA_BREATHE_MIN = 0.6f;

        /// <summary>Maximum alpha during breathe animation.</summary>
        public const float ALPHA_BREATHE_MAX = 1.0f;

        /// <summary>Emission multiplier for glow effect.</summary>
        public const float GLOW_STRENGTH = 0.8f;

        /// <summary>Scale oscillation amplitude (±5%).</summary>
        public const float SCALE_PULSE_AMOUNT = 0.05f;

        // --- Path animation ---

        /// <summary>Dash scroll speed (dashes per second).</summary>
        public const float DASH_SCROLL_SPEED = 2.0f;

        // --- Arc dot animation ---

        /// <summary>Distance between arc dots in meters.</summary>
        public const float ARC_DOT_SPACING = 0.3f;

        /// <summary>Radius of individual arc dots.</summary>
        public const float ARC_DOT_SIZE = 0.06f;
    }

    // =========================================================================
    // SIZES
    // =========================================================================

    /// <summary>
    /// Dimensions for reticles, markers, rings, and height offsets (world-space meters).
    /// </summary>
    public static class Sizes
    {
        // --- Ground reticle ---

        /// <summary>Center dot radius of the ground reticle.</summary>
        public const float RETICLE_RADIUS = 0.15f;

        /// <summary>Outer ring radius of the ground reticle.</summary>
        public const float RETICLE_RING_RADIUS = 0.3f;

        // --- Impact markers ---

        /// <summary>Radius of impact marker circles.</summary>
        public const float MARKER_RADIUS = 0.2f;

        /// <summary>Half-extent of impact marker cross lines.</summary>
        public const float MARKER_CROSS_SIZE = 0.25f;

        // --- Base rings (under units) ---

        /// <summary>Base ring radius for Medium creatures.</summary>
        public const float BASE_RING_RADIUS_MEDIUM = 0.45f;

        /// <summary>Base ring radius for Small creatures.</summary>
        public const float BASE_RING_RADIUS_SMALL = 0.35f;

        /// <summary>Base ring radius for Large creatures.</summary>
        public const float BASE_RING_RADIUS_LARGE = 0.7f;

        // --- Wall placement handles ---

        /// <summary>Radius of draggable wall placement handles.</summary>
        public const float WALL_HANDLE_RADIUS = 0.2f;

        // --- Height offsets ---

        /// <summary>Slight offset above ground to prevent Z-fighting.</summary>
        public const float GROUND_OFFSET = 0.02f;

        /// <summary>Height of ring mesh geometry.</summary>
        public const float RING_HEIGHT = 0.03f;

        /// <summary>Vertical offset above unit origin for floating text.</summary>
        public const float TEXT_HEIGHT_OFFSET = 2.5f;

        // --- Multi-target ---

        /// <summary>Size of billboard number markers in multi-target mode.</summary>
        public const float SELECTED_NUMBER_SIZE = 0.15f;
    }

    // =========================================================================
    // MATERIALS
    // =========================================================================

    /// <summary>
    /// Render-priority offsets for layered targeting visuals (higher = renders on top).
    /// </summary>
    public static class Materials
    {
        /// <summary>Priority for ground fill quads / decals.</summary>
        public const int GROUND_FILL_PRIORITY = 0;

        /// <summary>Priority for ground outlines drawn over fills.</summary>
        public const int GROUND_OUTLINE_PRIORITY = 1;

        /// <summary>Priority for path lines.</summary>
        public const int PATH_PRIORITY = 2;

        /// <summary>Priority for impact markers.</summary>
        public const int MARKER_PRIORITY = 3;

        /// <summary>Priority for base rings (topmost ground layer).</summary>
        public const int RING_PRIORITY = 4;
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    /// <summary>
    /// Returns the outline/stroke color for the given <see cref="TargetingValidity"/> state.
    /// </summary>
    public static Color GetValidityColor(TargetingValidity validity) => validity switch
    {
        TargetingValidity.Valid             => Colors.Valid,
        TargetingValidity.OutOfRange        => Colors.OutOfRange,
        TargetingValidity.NoLineOfSight     => Colors.Invalid,
        TargetingValidity.PathInterrupted   => Colors.Warning,
        TargetingValidity.InvalidTargetType => Colors.Invalid,
        TargetingValidity.InvalidPlacement  => Colors.Invalid,
        _ => Colors.Invalid,
    };

    /// <summary>
    /// Returns the translucent fill color for the given <see cref="TargetingValidity"/> state.
    /// </summary>
    public static Color GetValidityFillColor(TargetingValidity validity) => validity switch
    {
        TargetingValidity.Valid             => Colors.ValidFill,
        TargetingValidity.OutOfRange        => Colors.InvalidFill,
        TargetingValidity.NoLineOfSight     => Colors.InvalidFill,
        TargetingValidity.PathInterrupted   => Colors.WarningFill,
        TargetingValidity.InvalidTargetType => Colors.InvalidFill,
        TargetingValidity.InvalidPlacement  => Colors.InvalidFill,
        _ => Colors.InvalidFill,
    };

    /// <summary>
    /// Returns the relation color based on <paramref name="targetFaction"/> relative to
    /// <paramref name="sourceFaction"/>. Same faction → <see cref="Colors.Self"/> when identical,
    /// otherwise maps Player↔Ally as friendly and Hostile as enemy.
    /// </summary>
    public static Color GetRelationColor(Faction sourceFaction, Faction targetFaction)
    {
        if (sourceFaction == targetFaction)
            return Colors.Self;

        return targetFaction switch
        {
            Faction.Hostile when sourceFaction is Faction.Player or Faction.Ally => Colors.Enemy,
            Faction.Player or Faction.Ally when sourceFaction == Faction.Hostile => Colors.Enemy,
            Faction.Neutral => Colors.Neutral,
            Faction.Player or Faction.Ally when sourceFaction is Faction.Player or Faction.Ally => Colors.Ally,
            _ => Colors.Neutral,
        };
    }
}
