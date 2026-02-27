namespace QDND.Combat.Targeting;

/// <summary>
/// Shared validity states for targeting checks, modeled after BG3's targeting feedback.
/// Each state maps to a distinct visual/audio feedback cue.
/// </summary>
public enum TargetingValidity
{
    /// <summary>The target or placement is fully valid.</summary>
    Valid,

    /// <summary>Target is beyond the ability's maximum range.</summary>
    OutOfRange,

    /// <summary>Line of sight to the target is obstructed.</summary>
    NoLineOfSight,

    /// <summary>The movement path to the target is blocked.</summary>
    PathInterrupted,

    /// <summary>The hovered entity is not a valid target type for this ability.</summary>
    InvalidTargetType,

    /// <summary>The AoE or ground-targeted placement is in an invalid location.</summary>
    InvalidPlacement,
}

/// <summary>
/// Determines which cursor shape/visual the UI should display during targeting.
/// </summary>
public enum TargetingCursorMode
{
    /// <summary>Standard pointer cursor.</summary>
    Default,

    /// <summary>Melee or ranged weapon attack cursor.</summary>
    Attack,

    /// <summary>Spell/ability cast cursor.</summary>
    Cast,

    /// <summary>Ground-placement cursor (AoE, summon, etc.).</summary>
    Place,

    /// <summary>Movement cursor.</summary>
    Move,

    /// <summary>Red X / forbidden cursor when targeting is invalid.</summary>
    Invalid,
}

/// <summary>
/// Identifies which targeting behavior (mode) is currently active.
/// Each value maps to a concrete <see cref="ITargetingMode"/> implementation.
/// </summary>
public enum TargetingModeType
{
    /// <summary>No targeting active.</summary>
    None,

    /// <summary>Click-on-entity single target (most attacks, single-target spells).</summary>
    SingleTarget,

    /// <summary>Free-aim on the ground plane (Fireball placement, teleport destination).</summary>
    FreeAimGround,

    /// <summary>Straight-line ray from caster toward cursor (Scorching Ray, Lightning Bolt).</summary>
    StraightLine,

    /// <summary>Ballistic arc trajectory (thrown items, catapults).</summary>
    BallisticArc,

    /// <summary>Bezier curve path (guided projectiles).</summary>
    BezierCurve,

    /// <summary>Pathfind-following projectile preview.</summary>
    PathfindProjectile,

    /// <summary>Area-of-effect circle on the ground.</summary>
    AoECircle,

    /// <summary>Area-of-effect cone emanating from the caster.</summary>
    AoECone,

    /// <summary>Area-of-effect line (e.g., Lightning Bolt).</summary>
    AoELine,

    /// <summary>Area-of-effect wall placement (Wall of Fire — two-click).</summary>
    AoEWall,

    /// <summary>Multi-target sequential selection (e.g., Eldritch Blast, Magic Missile).</summary>
    MultiTarget,

    /// <summary>Chain targeting that bounces between entities (Chain Lightning).</summary>
    Chain,
}

/// <summary>
/// Lifecycle phase of the targeting system as a whole.
/// </summary>
public enum TargetingPhase
{
    /// <summary>No ability is being targeted.</summary>
    Inactive,

    /// <summary>User is previewing / aiming but has not confirmed.</summary>
    Previewing,

    /// <summary>A multi-step targeting flow is in progress (e.g., wall start→end).</summary>
    MultiStep,
}

/// <summary>
/// Types of 2-D ground projections rendered beneath the cursor or around entities.
/// </summary>
public enum GroundShapeType
{
    /// <summary>Filled or outlined circle (AoE radius).</summary>
    Circle,

    /// <summary>Cone wedge emanating from caster.</summary>
    Cone,

    /// <summary>Rectangular line strip (Lightning Bolt-style).</summary>
    Line,

    /// <summary>Two-endpoint wall segment.</summary>
    Wall,

    /// <summary>Crosshair-style reticle at the cursor point.</summary>
    Reticle,

    /// <summary>Ring drawn at a unit's feet (selection/range indicator).</summary>
    FootprintRing,

    /// <summary>Large range ring around the caster showing max reach.</summary>
    RangeRing,
}

/// <summary>
/// Visual marker types rendered at impact / endpoint positions.
/// </summary>
public enum ImpactMarkerType
{
    /// <summary>Circular ring marker.</summary>
    Ring,

    /// <summary>Cross / plus-sign marker.</summary>
    Cross,

    /// <summary>Small dot marker.</summary>
    Dot,

    /// <summary>Directional arrow marker.</summary>
    Arrow,

    /// <summary>X-shaped invalid/blocked marker.</summary>
    XMark,
}

/// <summary>
/// How a unit should be visually highlighted during targeting.
/// </summary>
public enum UnitHighlightType
{
    /// <summary>The entity directly under the cursor (primary target).</summary>
    PrimaryTarget,

    /// <summary>An enemy that will be affected by the ability.</summary>
    AffectedEnemy,

    /// <summary>A friendly unit that will be affected.</summary>
    AffectedAlly,

    /// <summary>A neutral entity that will be affected.</summary>
    AffectedNeutral,

    /// <summary>A previously-selected target in multi-target mode.</summary>
    SelectedTarget,

    /// <summary>A friendly unit in the blast zone (friendly-fire warning).</summary>
    Warning,
}

/// <summary>
/// Types of floating text rendered near the cursor or target entities.
/// </summary>
public enum FloatingTextType
{
    /// <summary>Numeric hit-chance percentage (e.g., "85%").</summary>
    HitChance,

    /// <summary>Reason / validity string (e.g., "OUT OF RANGE").</summary>
    ReasonString,

    /// <summary>Distance or range value.</summary>
    Range,

    /// <summary>Multi-target counter (e.g., "2 / 3").</summary>
    TargetCounter,
}
