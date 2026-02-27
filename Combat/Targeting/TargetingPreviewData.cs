using System.Collections.Generic;
using Godot;

namespace QDND.Combat.Targeting;

/// <summary>
/// Render-agnostic preview data computed per-frame by the active <see cref="ITargetingMode"/>.
/// This is the <b>only</b> object the visual / UI layer reads — modes write it,
/// renderers consume it, and the orchestrator recycles it between frames.
/// </summary>
public sealed class TargetingPreviewData
{
    // ── Validity / Feedback ──────────────────────────────────────────

    /// <summary>Overall validity of the current cursor position / hover target.</summary>
    public TargetingValidity Validity { get; set; }

    /// <summary>
    /// BG3-style reason string shown to the player (e.g., "OUT OF RANGE", "NO LINE OF SIGHT").
    /// <c>null</c> when <see cref="Validity"/> is <see cref="TargetingValidity.Valid"/>.
    /// </summary>
    public string ReasonString { get; set; }

    /// <summary>Which cursor shape the UI should display this frame.</summary>
    public TargetingCursorMode CursorMode { get; set; }

    // ── Cursor / Hover State ─────────────────────────────────────────

    /// <summary>World-space position of the cursor hit-point on the ground or entity.</summary>
    public Vector3 CursorWorldPoint { get; set; }

    /// <summary>Surface normal at the cursor hit-point, if available.</summary>
    public Vector3? SurfaceNormal { get; set; }

    /// <summary>Entity ID of the hovered entity, or <c>null</c> if hovering empty space.</summary>
    public string HoveredEntityId { get; set; }

    /// <summary>The targeting mode that produced this data.</summary>
    public TargetingModeType ActiveMode { get; set; }

    // ── Visual Primitive Lists (pooled — never recreated) ────────────

    /// <summary>Ground projections (AoE circles, cones, range rings, etc.).</summary>
    public List<GroundShapeData> GroundShapes { get; } = new();

    /// <summary>Path / arc / line polyline segments for trajectory preview.</summary>
    public List<PathSegmentData> PathSegments { get; } = new();

    /// <summary>Impact / endpoint markers (ring, cross, dot, etc.).</summary>
    public List<ImpactMarkerData> ImpactMarkers { get; } = new();

    /// <summary>Per-unit highlight instructions (primary target, affected allies/enemies, etc.).</summary>
    public List<UnitHighlightData> UnitHighlights { get; } = new();

    /// <summary>Floating text entries rendered near the cursor or targets.</summary>
    public List<FloatingTextData> FloatingTexts { get; } = new();

    /// <summary>Ordered list of already-selected targets in multi-target mode.</summary>
    public List<SelectedTargetData> SelectedTargets { get; } = new();

    // ── Dirty / Staleness Tracking ───────────────────────────────────

    /// <summary>
    /// Set to <c>true</c> whenever data changes. The visual system resets this
    /// to <c>false</c> after consuming the data each render frame.
    /// </summary>
    public bool IsDirty { get; set; }

    /// <summary>
    /// Monotonically-increasing frame counter stamped by the mode that wrote this data.
    /// Renderers can compare against their last-rendered stamp to detect stale data.
    /// </summary>
    public int FrameStamp { get; set; }

    // ── Methods ──────────────────────────────────────────────────────

    /// <summary>
    /// Resets all fields to safe defaults and clears every list <b>without</b>
    /// reallocating them, keeping pooled capacity intact.
    /// </summary>
    public void Clear()
    {
        Validity = TargetingValidity.Valid;
        ReasonString = null;
        CursorMode = TargetingCursorMode.Default;
        CursorWorldPoint = Vector3.Zero;
        SurfaceNormal = null;
        HoveredEntityId = null;
        ActiveMode = TargetingModeType.None;

        GroundShapes.Clear();
        PathSegments.Clear();
        ImpactMarkers.Clear();
        UnitHighlights.Clear();
        FloatingTexts.Clear();
        SelectedTargets.Clear();

        IsDirty = false;
        // FrameStamp is NOT reset — the caller bumps it.
    }
}

// ═══════════════════════════════════════════════════════════════════════
//  Sub-structs — value types for cache-friendly, allocation-free usage
// ═══════════════════════════════════════════════════════════════════════

/// <summary>
/// Describes a single ground projection (circle, cone, line, wall, ring, etc.).
/// Written by targeting modes, consumed by the ground-decal renderer.
/// </summary>
public struct GroundShapeData
{
    /// <summary>The geometric shape type.</summary>
    public GroundShapeType Type;

    /// <summary>World-space center of the shape.</summary>
    public Vector3 Center;

    /// <summary>Radius in meters (circle, reticle, footprint ring, range ring).</summary>
    public float Radius;

    /// <summary>Cone half-angle in degrees.</summary>
    public float Angle;

    /// <summary>Width in meters (line, wall).</summary>
    public float Width;

    /// <summary>Length in meters (line, cone).</summary>
    public float Length;

    /// <summary>Normalized direction vector (cone forward, line forward).</summary>
    public Vector3 Direction;

    /// <summary>Second endpoint for wall-type shapes.</summary>
    public Vector3 EndPoint;

    /// <summary>Per-shape validity (a shape can be invalid even if the overall aim is valid).</summary>
    public TargetingValidity Validity;

    /// <summary>
    /// Optional fill color override. When set, the renderer uses this instead of
    /// deriving the fill color from <see cref="Validity"/>.
    /// Used for friendly-fire fill when allies are in an AoE zone.
    /// </summary>
    public Color? FillColorOverride;
}

/// <summary>
/// A polyline segment of a path, arc, or line trajectory preview.
/// Multiple segments compose a full trajectory (e.g., pre-block + post-block).
/// </summary>
public struct PathSegmentData
{
    /// <summary>Ordered world-space polyline points defining this segment.</summary>
    public Vector3[] Points;

    /// <summary>
    /// <c>true</c> if this segment lies past an obstruction — the renderer may
    /// draw it faded, red, or dashed.
    /// </summary>
    public bool IsBlocked;

    /// <summary>Exact world-space point where the blockage occurs, if any.</summary>
    public Vector3? BlockPoint;

    /// <summary>Visual style hint: draw as a dashed line instead of solid.</summary>
    public bool IsDashed;
}

/// <summary>
/// A single impact or endpoint marker rendered at a world position.
/// </summary>
public struct ImpactMarkerData
{
    /// <summary>World-space position of the marker.</summary>
    public Vector3 Position;

    /// <summary>Visual type of the marker.</summary>
    public ImpactMarkerType Type;

    /// <summary>Validity state (controls marker color / icon variant).</summary>
    public TargetingValidity Validity;
}

/// <summary>
/// Per-unit highlight instruction consumed by the unit-highlight renderer.
/// </summary>
public struct UnitHighlightData
{
    /// <summary>Entity ID of the unit to highlight.</summary>
    public string EntityId;

    /// <summary>How the unit should be highlighted.</summary>
    public UnitHighlightType HighlightType;

    /// <summary>Whether this highlight represents a valid target.</summary>
    public bool IsValid;

    /// <summary>
    /// Numeric hit-chance percentage to display above the unit.
    /// <c>null</c> means do not show a hit-chance tooltip.
    /// </summary>
    public int? HitChancePercent;

    /// <summary>
    /// Optional per-target reason override (e.g., "ALLY" or "IMMUNE").
    /// <c>null</c> falls back to the global <see cref="TargetingPreviewData.ReasonString"/>.
    /// </summary>
    public string ReasonOverride;
}

/// <summary>
/// A floating text entry rendered in screen space near a world anchor point.
/// </summary>
public struct FloatingTextData
{
    /// <summary>World-space anchor that the text is projected from.</summary>
    public Vector3 WorldAnchor;

    /// <summary>The text content to display.</summary>
    public string Text;

    /// <summary>Semantic type of the text (hit chance, reason, range, counter).</summary>
    public FloatingTextType TextType;

    /// <summary>Validity state used for color-coding the text.</summary>
    public TargetingValidity Validity;
}

/// <summary>
/// Tracks a single already-confirmed target in multi-target mode
/// (e.g., one beam of Eldritch Blast, one missile of Magic Missile).
/// </summary>
public struct SelectedTargetData
{
    /// <summary>Entity ID of the selected target.</summary>
    public string EntityId;

    /// <summary>1-based selection order index.</summary>
    public int Index;
}
