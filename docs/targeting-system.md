# Targeting System

> Comprehensive developer guide for the BG3-style targeting system in QDND (Godot 4 C#).

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [State Machine & Lifecycle](#state-machine--lifecycle)
3. [How to Add a New Targeting Mode](#how-to-add-a-new-targeting-mode)
4. [Style Token Reference](#style-token-reference)
5. [Visual System](#visual-system)
6. [File Inventory](#file-inventory)
7. [Integration Points](#integration-points)
8. [BG3 Parity Checklist](#bg3-parity-checklist)

---

## Architecture Overview

The targeting system is built in three strictly separated layers:

```
┌─────────────────────────────────────────────────────┐
│  Layer 1 — MODES (logic)                            │
│  ITargetingMode implementations in Combat/Targeting/ │
│  Modes/. Each mode computes preview data per-frame. │
│  Pure C# — no Node, no rendering.                   │
└────────────────────────┬────────────────────────────┘
                         │ writes TargetingPreviewData
┌────────────────────────▼────────────────────────────┐
│  Layer 2 — ORCHESTRATOR (TargetingSystem)           │
│  Owns lifecycle: Begin → Update → Confirm/Cancel →  │
│  End. Manages phase transitions, events, and the    │
│  recycled TargetingPreviewData singleton.            │
└────────────────────────┬────────────────────────────┘
                         │ fires OnPreviewUpdated event
┌────────────────────────▼────────────────────────────┐
│  Layer 3 — VISUALS (rendering)                      │
│  TargetingVisualSystem + sub-renderers in            │
│  Combat/Targeting/Visuals/. Reads TargetingPreview-  │
│  Data and renders ground shapes, paths, markers,     │
│  highlights, text overlays, and cursor changes.      │
└─────────────────────────────────────────────────────┘
```

### Key Design Principles

- **Modes are render-agnostic.** A mode writes structs into `TargetingPreviewData`; it never creates meshes, materials, or nodes.
- **One preview data instance is recycled.** `TargetingSystem.CurrentPreview` is cleared and refilled each frame — lists are cleared without reallocation so renderers hold a stable reference.
- **Pool-based rendering.** All visual nodes are object-pooled via `TargetingNodePool<T>`. Nodes are hidden/shown rather than spawned/freed every frame.
- **Style tokens, not magic numbers.** All colors, stroke widths, sizes, and animation parameters live in `TargetingStyleTokens`. No per-skill special casing.

### The Preview Data Contract

`TargetingPreviewData` is the **only** bridge between modes and visuals. It contains:

| Field / List | Type | Purpose |
|---|---|---|
| `Validity` | `TargetingValidity` | Overall validity of the current aim (Valid, OutOfRange, NoLineOfSight, …) |
| `ReasonString` | `string` | BG3-style reason text ("OUT OF RANGE", "NO LINE OF SIGHT") |
| `CursorMode` | `TargetingCursorMode` | Which cursor shape to display (Attack, Cast, Place, Invalid, …) |
| `CursorWorldPoint` | `Vector3` | World-space cursor hit-point |
| `HoveredEntityId` | `string` | Entity under the cursor, or null |
| `ActiveMode` | `TargetingModeType` | Which mode produced this data |
| `GroundShapes` | `List<GroundShapeData>` | Circles, cones, lines, walls, reticles, rings |
| `PathSegments` | `List<PathSegmentData>` | Polyline trajectory segments (arcs, beams) |
| `ImpactMarkers` | `List<ImpactMarkerData>` | Endpoint markers (ring, cross, dot, arrow, X) |
| `UnitHighlights` | `List<UnitHighlightData>` | Per-unit outline + base ring instructions |
| `FloatingTexts` | `List<FloatingTextData>` | Hit-chance %, reason strings, range text |
| `SelectedTargets` | `List<SelectedTargetData>` | Ordered picks in multi-target mode |
| `IsDirty` / `FrameStamp` | `bool` / `int` | Staleness tracking — renderers skip if stamp unchanged |

All inner lists use value-type structs (`GroundShapeData`, `PathSegmentData`, etc.) for cache-friendly, allocation-free usage.

---

## State Machine & Lifecycle

### Phases

```
TargetingPhase.Inactive ──▶ TargetingPhase.Previewing ──▶ TargetingPhase.MultiStep
         ▲                           │                             │
         └───────────────────────────┴─────────────────────────────┘
                          (Confirm / Cancel / ForceEnd)
```

| Phase | Description |
|---|---|
| `Inactive` | No ability is being targeted. Visual system is idle. |
| `Previewing` | Player is aiming. Mode receives `UpdatePreview()` each frame. |
| `MultiStep` | A multi-step flow is in progress (e.g., AoEWall start → end, MultiTarget sequential picks). |

### Lifecycle Sequence

```
1. CombatArena calls TargetingSystem.BeginTargeting(actionId, action, source, worldPos)
   ├── Maps action.TargetType → TargetingModeType (via MapTargetTypeToMode)
   ├── Looks up registered ITargetingMode
   ├── Enters the correct CombatSubstate
   ├── Calls mode.Enter(action, source, sourceWorldPos)
   └── Sets phase to Previewing

2. Every frame (while phase ≠ Inactive):
   ├── CombatInputHandler produces HoverData via TargetingHoverPipeline
   ├── TargetingSystem.UpdateFrame(hover) → mode.UpdatePreview(hover, recycledData)
   ├── OnPreviewUpdated event fires
   └── TargetingVisualSystem.Render(data, camera, getVisual)

3a. LMB click → TargetingSystem.HandleConfirm(hover)
    └── mode.TryConfirm(hover) → ConfirmResult
        ├── Rejected       → play error feedback, stay in targeting
        ├── ExecuteSingle   → fire OnTargetingConfirmed, EndTargeting
        ├── ExecuteAtPos    → fire OnTargetingConfirmed, EndTargeting
        ├── AdvanceStep     → phase → MultiStep, continue
        └── Complete        → fire OnTargetingConfirmed, EndTargeting

3b. RMB click → TargetingSystem.HandleCancel()
    ├── If MultiStep: try mode.TryUndoLastStep() first
    └── If nothing to undo: Cancel() → Exit() → phase → Inactive

3c. Escape → TargetingSystem.HandleEscapeCancel()
    └── Always cancels entirely (no undo attempt)

3d. External interrupt → TargetingSystem.ForceEnd()
    └── Same as cancel but from turn-end / combat-end
```

### ConfirmResult Outcomes

| Outcome | Payload | When |
|---|---|---|
| `Rejected` | `RejectionReason` | Invalid target, out of range, etc. |
| `ExecuteSingleTarget` | `TargetEntityId` | Single-target attacks/spells |
| `ExecuteAtPosition` | `TargetPosition` | Ground-targeted abilities (Fireball) |
| `AdvanceStep` | — | Wall start → waiting for end click |
| `Complete` | `AllTargetIds` | Multi-target mode finished all picks |

---

## How to Add a New Targeting Mode

Follow these steps to add **Mode #13** (e.g., a hypothetical "Bounce" mode). You should be done in under an hour.

### Step 1: Add the enum value

In `Combat/Targeting/TargetingEnums.cs`, add a new value to `TargetingModeType`:

```csharp
public enum TargetingModeType
{
    // ... existing values ...
    Chain,

    /// <summary>Bouncing projectile that ricochets off surfaces.</summary>
    Bounce,   // ← new
}
```

### Step 2: Create the mode class

Create `Combat/Targeting/Modes/BounceMode.cs` implementing `ITargetingMode`:

```csharp
using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Actions;
using QDND.Combat.Entities;

namespace QDND.Combat.Targeting.Modes;

public sealed class BounceMode : ITargetingMode
{
    // ── Injected services ────────────────────────────────────────
    private readonly TargetValidator _validator;
    private readonly Func<string, Combatant> _getCombatant;

    // ── Per-activation state (reset in Enter) ────────────────────
    private ActionDefinition _action;
    private Combatant _source;
    private Vector3 _sourceWorldPos;

    public BounceMode(TargetValidator validator, Func<string, Combatant> getCombatant)
    {
        _validator = validator;
        _getCombatant = getCombatant;
    }

    // ── Identity ─────────────────────────────────────────────────
    public TargetingModeType ModeType => TargetingModeType.Bounce;
    public bool IsMultiStep => false;   // set true if multi-click
    public int CurrentStep => 0;
    public int TotalSteps => 1;

    // ── Lifecycle ────────────────────────────────────────────────

    public void Enter(ActionDefinition action, Combatant source, Vector3 sourceWorldPos)
    {
        _action = action;
        _source = source;
        _sourceWorldPos = sourceWorldPos;
        // Pre-compute valid surfaces, bounce geometry, etc.
    }

    public TargetingPreviewData UpdatePreview(HoverData hover, TargetingPreviewData data)
    {
        data.ActiveMode = TargetingModeType.Bounce;
        data.CursorWorldPoint = hover.CursorWorldPoint;

        // 1. Range ring around caster
        data.GroundShapes.Add(new GroundShapeData
        {
            Type = GroundShapeType.RangeRing,
            Center = _sourceWorldPos,
            Radius = _action.Range,
            Validity = TargetingValidity.Valid,
        });

        // 2. Compute bounce trajectory → add PathSegments
        // 3. Add ImpactMarkers at bounce points
        // 4. Add UnitHighlights for affected entities
        // 5. Set Validity, CursorMode, ReasonString

        return data;
    }

    public ConfirmResult TryConfirm(HoverData hover)
    {
        // Validate and return ExecuteAtPosition or Rejected
        return new ConfirmResult
        {
            Outcome = ConfirmOutcome.ExecuteAtPosition,
            TargetPosition = hover.CursorWorldPoint,
        };
    }

    public bool TryUndoLastStep() => false;
    public void Cancel() { }

    public void Exit()
    {
        _action = null;
        _source = null;
    }
}
```

**Key rules for `UpdatePreview`:**
- Write into `recycledData` — do **not** call `recycledData.Clear()` (the orchestrator already did).
- Return the same reference you received.
- Use value-type structs: `GroundShapeData`, `PathSegmentData`, `ImpactMarkerData`, `UnitHighlightData`, `FloatingTextData`.
- Use `TargetingModeHelpers.ClassifyHighlight(source, target)` for faction-aware highlight coloring.

### Step 3: Map the TargetType

In `Combat/Targeting/TargetingSystem.cs` → `MapTargetTypeToMode()`, add a case that routes the appropriate `TargetType` to your new mode:

```csharp
case TargetType.Bounce:          // if you added a new TargetType
    modeType = TargetingModeType.Bounce;
    return true;
```

If your mode reuses an existing `TargetType` (e.g., `Point`), you may need to add a secondary discriminator (check `action.Tags`, `action.AreaRadius`, etc.) similar to how `Line` dispatches to either `StraightLine` or `AoELine`:

```csharp
case TargetType.Line:
    modeType = action.AreaRadius > 0f
        ? TargetingModeType.AoELine
        : TargetingModeType.StraightLine;
    return true;
```

Also add a substate mapping in `GetSubstateForMode()`:

```csharp
TargetingModeType.Bounce => CombatSubstate.TargetSelection,
```

### Step 4: Register in CombatArena

In `Combat/Arena/CombatArena.cs` → `InitializeTargetingSystem()`, add:

```csharp
_targetingSystem.RegisterMode(new BounceMode(_targetValidator, getCombatant));
```

### Step 5: Done — No Renderer Changes Needed

If your mode uses the existing primitive types (`GroundShapeData`, `PathSegmentData`, `ImpactMarkerData`, `UnitHighlightData`, `FloatingTextData`), **no changes to the visual system are required**. The existing renderers (`GroundShapeRenderer`, `PathRenderer`, `MarkerRenderer`, `UnitHighlightManager`, `TargetingTextOverlay`, `CursorManager`) will pick up your data automatically.

Only create a new renderer if you need a fundamentally new visual primitive (extremely rare).

### Summary Checklist

| # | File | Change |
|---|---|---|
| 1 | `TargetingEnums.cs` | Add `TargetingModeType.Bounce` |
| 2 | `Modes/BounceMode.cs` | Create class implementing `ITargetingMode` |
| 3 | `TargetingSystem.cs` | Add case in `MapTargetTypeToMode` + `GetSubstateForMode` |
| 4 | `CombatArena.cs` | Register via `_targetingSystem.RegisterMode(…)` |
| 5 | Visual system | **Nothing** (unless new primitive type needed) |

---

## Style Token Reference

All visual constants live in `Combat/Targeting/TargetingStyleTokens.cs`. This is the single place to tune the look of the entire targeting system.

### Colors (`TargetingStyleTokens.Colors`)

| Token | Value (RGBA) | Usage |
|---|---|---|
| `Valid` | `(0.2, 0.85, 0.3, 0.8)` | Green — valid target outline/stroke |
| `Invalid` | `(0.95, 0.2, 0.15, 0.8)` | Red — invalid target |
| `Warning` | `(1.0, 0.85, 0.1, 0.8)` | Yellow — caution (partial cover) |
| `OutOfRange` | `(0.6, 0.6, 0.6, 0.6)` | Grey — out of range |
| `Enemy` | `(0.95, 0.25, 0.2, 0.8)` | Red — hostile entity ring |
| `Ally` | `(0.2, 0.7, 1.0, 0.8)` | Blue — allied entity ring |
| `Neutral` | `(0.9, 0.85, 0.3, 0.8)` | Yellow — neutral entity |
| `Self` | `(0.3, 1.0, 0.5, 0.8)` | Green — caster self |
| `ValidFill` | `(0.2, 0.85, 0.3, 0.15)` | Translucent green area fill |
| `InvalidFill` | `(0.95, 0.2, 0.15, 0.15)` | Translucent red area fill |
| `WarningFill` | `(1.0, 0.85, 0.1, 0.12)` | Translucent yellow area fill |
| `FriendlyFireFill` | `(1.0, 0.6, 0.1, 0.18)` | Orange fill for ally-in-AoE warning |
| `ClearPath` | `(0.2, 0.85, 0.3, 0.9)` | Green line — unblocked path |
| `BlockedPath` | `(0.95, 0.2, 0.15, 0.7)` | Red line — blocked path |
| `HitChanceText` | `(1, 1, 1, 1)` | White hit-chance text |
| `ReasonText` | `(1, 0.3, 0.2, 1)` | Red error/reason text |
| `RangeText` | `(0.8, 0.8, 0.8, 0.9)` | Light grey range text |
| `SelectedTarget` | `(0.3, 0.9, 1.0, 0.85)` | Cyan — selected multi-target |
| `NextTargetHint` | `(0.3, 0.9, 1.0, 0.4)` | Dim cyan — next slot hint |
| `RangeRing` | `(1, 1, 1, 0.25)` | Faint white max-range ring |

### Strokes (`TargetingStyleTokens.Strokes`)

| Token | Value (meters) | Usage |
|---|---|---|
| `THIN` | 0.02 | Subtle outlines |
| `MEDIUM` | 0.04 | Default line width |
| `THICK` | 0.08 | Emphasized outlines |
| `EXTRA_THICK` | 0.12 | Drag handles |
| `RING_STROKE` | 0.05 | Base-ring geometry |
| `OUTLINE_RING_STROKE` | 0.03 | Outer outline rings |
| `DASH_LENGTH` | 0.15 | Dash segment length |
| `DASH_GAP` | 0.10 | Gap between dashes |

### Motion (`TargetingStyleTokens.Motion`)

| Token | Value | Usage |
|---|---|---|
| `PULSE_SPEED_HZ` | 2.0 | Breathe animation frequency |
| `ALPHA_BREATHE_MIN` | 0.6 | Min alpha during pulse |
| `ALPHA_BREATHE_MAX` | 1.0 | Max alpha during pulse |
| `GLOW_STRENGTH` | 0.8 | Emission multiplier |
| `SCALE_PULSE_AMOUNT` | 0.05 | ±5% scale oscillation |
| `DASH_SCROLL_SPEED` | 2.0 | Dashes per second scroll |
| `ARC_DOT_SPACING` | 0.3 | Spacing between arc dots |
| `ARC_DOT_SIZE` | 0.06 | Individual dot radius |

### Sizes (`TargetingStyleTokens.Sizes`)

| Token | Value (meters) | Usage |
|---|---|---|
| `RETICLE_RADIUS` | 0.15 | Center dot of ground reticle |
| `RETICLE_RING_RADIUS` | 0.30 | Outer ring of ground reticle |
| `MARKER_RADIUS` | 0.20 | Impact marker circles |
| `MARKER_CROSS_SIZE` | 0.25 | Half-extent of cross markers |
| `BASE_RING_RADIUS_MEDIUM` | 0.45 | Medium creature ring |
| `BASE_RING_RADIUS_SMALL` | 0.35 | Small creature ring |
| `BASE_RING_RADIUS_LARGE` | 0.70 | Large creature ring |
| `WALL_HANDLE_RADIUS` | 0.20 | Wall placement drag handles |
| `GROUND_OFFSET` | 0.02 | Z-fighting prevention offset |
| `RING_HEIGHT` | 0.03 | Ring mesh height |
| `TEXT_HEIGHT_OFFSET` | 2.50 | Vertical offset for floating text |
| `SELECTED_NUMBER_SIZE` | 0.15 | Multi-target number markers |

### Material Priorities (`TargetingStyleTokens.Materials`)

| Token | Value | Purpose |
|---|---|---|
| `GROUND_FILL_PRIORITY` | 0 | Ground fill quads/decals (lowest) |
| `GROUND_OUTLINE_PRIORITY` | 1 | Ground outlines on top of fills |
| `PATH_PRIORITY` | 2 | Path/trajectory lines |
| `MARKER_PRIORITY` | 3 | Impact markers |
| `RING_PRIORITY` | 4 | Base rings (topmost ground layer) |

### Helper Methods

| Method | Returns | Purpose |
|---|---|---|
| `GetValidityColor(TargetingValidity)` | `Color` | Outline/stroke color for a validity state |
| `GetValidityFillColor(TargetingValidity)` | `Color` | Translucent fill color for a validity state |
| `GetRelationColor(Faction, Faction)` | `Color` | Faction-relative outline color (Self/Ally/Enemy/Neutral) |

---

## Visual System

### Architecture

`TargetingVisualSystem` (a `Node3D`) is the top-level visual orchestrator. It owns six sub-systems:

```
TargetingVisualSystem (Node3D)
├── GroundShapeRenderer (Node3D)   — circles, cones, lines, walls, reticles, rings
├── PathRenderer (Node3D)           — dotted arcs, solid ribbon segments
├── MarkerRenderer (Node3D)         — ring, cross, dot, arrow, X-mark markers
├── UnitHighlightManager (plain C#) — outline effects + base ring overlays
├── TargetingTextOverlay (CanvasLayer) — screen-space floating labels
└── CursorManager (plain C#)        — system cursor shape switching
```

### Pool-Based Rendering

Every renderer uses `TargetingNodePool<MeshInstance3D>` to avoid per-frame allocation:

```csharp
// Acquire a node (reuses hidden pooled node, or creates new)
var mesh = _pool.Acquire();
mesh.GlobalTransform = /* ... */;
mesh.MaterialOverride = TargetingMaterialCache.GetGroundFillMaterial(color);

// At the start of each frame, release all back to pool
_pool.ReleaseAll();   // hides nodes, resets transforms
```

- **Prewarm**: Each pool prewarms with a small initial count (`_discPool.Prewarm(4)`, `_dotPool.Prewarm(20)`, etc.).
- **Visibility toggle**: Nodes are shown/hidden via `Visible = true/false` rather than added/removed from the scene tree.
- **Disposal**: `pool.Dispose()` frees all nodes on scene exit.

### Material Cache

`TargetingMaterialCache` is a static dictionary keyed by `(MaterialKind, Color)`. It returns shared `StandardMaterial3D` instances:

| Method | Properties |
|---|---|
| `GetGroundFillMaterial(color)` | Unshaded, transparent, no depth test, double-sided |
| `GetGroundOutlineMaterial(color)` | Same + emission-boosted for visibility |
| `GetLineMaterial(color)` | Unshaded, transparent |
| `GetDashedLineMaterial(color)` | Same as line (dash is geometric) |
| `GetMarkerMaterial(color)` | Unshaded, transparent, higher render priority |

Cache is capped at `MAX_CACHE_SIZE = 64` entries and cleared on `TargetingVisualSystem.Cleanup()`.

### Render Loop

Each frame when targeting is active:

```
1. TargetingSystem.UpdateFrame(hover)
   └── mode.UpdatePreview(hover, recycledData)  // fills preview data

2. CombatArena / CombatInputHandler triggers rendering:
   └── TargetingVisualSystem.Render(data, camera, getVisual)
        ├── Skip if data.FrameStamp == _lastRenderedFrame
        ├── _groundRenderer.Update(data.GroundShapes)
        ├── _pathRenderer.Update(data.PathSegments)
        ├── _markerRenderer.Update(data.ImpactMarkers)
        ├── _highlightManager.Update(data.UnitHighlights, data.SelectedTargets, getVisual)
        ├── _textOverlay.Update(data.FloatingTexts, camera)
        └── _cursorManager.SetMode(data.CursorMode)
```

On targeting end, `TargetingVisualSystem.ClearAll()` releases all pool nodes and resets the cursor.

### Ground Shape Types

The `GroundShapeRenderer` handles all `GroundShapeType` values:

| Shape | Mesh Type | Key Fields |
|---|---|---|
| `Circle` | `CylinderMesh` (disc) | `Center`, `Radius` |
| `Cone` | Procedural `ArrayMesh` | `Center`, `Angle`, `Length`, `Direction` |
| `Line` | `BoxMesh` | `Center`, `Width`, `Length`, `Direction` |
| `Wall` | `BoxMesh` | `Center`, `EndPoint`, `Width` |
| `Reticle` | `CylinderMesh` (small disc) | `Center`, `Radius` |
| `FootprintRing` | `TorusMesh` | `Center`, `Radius` |
| `RangeRing` | `TorusMesh` | `Center`, `Radius` |

### Cursor Mapping

`CursorManager` translates `TargetingCursorMode` to Godot cursor shapes:

| Mode | Godot Shape |
|---|---|
| `Default` | `Arrow` |
| `Attack` | `Cross` |
| `Cast` | `PointingHand` |
| `Place` | `Cross` |
| `Move` | `Move` |
| `Invalid` | `Forbidden` |

---

## File Inventory

### `Combat/Targeting/` — Core

| File | Description |
|---|---|
| `ITargetingMode.cs` | Interface for all targeting modes + `HoverData`, `ConfirmResult`, `ConfirmOutcome` structs |
| `TargetingEnums.cs` | All targeting enums: `TargetingValidity`, `TargetingCursorMode`, `TargetingModeType`, `TargetingPhase`, `GroundShapeType`, `ImpactMarkerType`, `UnitHighlightType`, `FloatingTextType` |
| `TargetingPreviewData.cs` | The preview data contract and all sub-structs (`GroundShapeData`, `PathSegmentData`, `ImpactMarkerData`, `UnitHighlightData`, `FloatingTextData`, `SelectedTargetData`) |
| `TargetingStyleTokens.cs` | Centralized visual constants: colors, strokes, motion, sizes, material priorities, helper methods |
| `TargetingSystem.cs` | Main orchestrator — lifecycle, phase state machine, mode registration, TargetType→Mode mapping |
| `TargetingHoverPipeline.cs` | Per-frame cursor raycasting producing `HoverData` (entity layer 2 → ground layer 1 → fallback) |
| `TargetValidator.cs` | Validates and resolves targets — range, faction, LOS, area resolution |

### `Combat/Targeting/Modes/` — Mode Implementations

| File | Mode | Description |
|---|---|---|
| `SingleTargetMode.cs` | `SingleTarget` | Click-on-entity with hit-chance preview, range/faction/LOS validation |
| `FreeAimGroundMode.cs` | `FreeAimGround` | Free-aim ground placement (teleport destination, summon point) |
| `StraightLineMode.cs` | `StraightLine` | Straight beam ray from caster toward cursor (Scorching Ray visual) |
| `BallisticArcMode.cs` | `BallisticArc` | Parabolic arc trajectory preview (thrown items, catapults) |
| `BezierCurveMode.cs` | `BezierCurve` | Bezier curve guided projectile path |
| `PathfindProjectileMode.cs` | `PathfindProjectile` | Pathfinding-aware projectile preview |
| `AoECircleMode.cs` | `AoECircle` | Ground-placed circle AoE (Fireball, Shatter) with friendly-fire detection |
| `AoEConeMode.cs` | `AoECone` | Cone emanating from caster (Burning Hands, Cone of Cold) |
| `AoELineMode.cs` | `AoELine` | Wide line AoE (Lightning Bolt with area radius) |
| `AoEWallMode.cs` | `AoEWall` | Two-click wall segment placement (Wall of Fire) |
| `MultiTargetMode.cs` | `MultiTarget` | Sequential multi-pick (Eldritch Blast beams, Magic Missile) |
| `ChainMode.cs` | `Chain` | Chain bouncing between entities (Chain Lightning) |
| `TargetingModeHelpers.cs` | — | Shared utilities: `ClassifyHighlight()` faction-aware highlight classification |

### `Combat/Targeting/Visuals/` — Rendering

| File | Description |
|---|---|
| `TargetingVisualSystem.cs` | Top-level visual orchestrator (`Node3D`); owns all renderers, drives per-frame `Render()` calls |
| `GroundShapeRenderer.cs` | Renders circles, cones, lines, walls, reticles, rings using pooled `MeshInstance3D` with `CylinderMesh`, `TorusMesh`, `BoxMesh`, procedural `ArrayMesh` |
| `PathRenderer.cs` | Renders path/arc/trajectory as BG3-style dotted arcs or solid ribbon segments |
| `MarkerRenderer.cs` | Renders impact/endpoint markers — rings, crosses, dots, arrows, X-marks |
| `UnitHighlightManager.cs` | Manages outline effects and `TorusMesh` base rings on combatant visuals |
| `TargetingTextOverlay.cs` | Screen-space text overlay via `CanvasLayer` — hit %, reason strings, range, target counters; projects world → 2D with stagger logic |
| `TargetingNodePool.cs` | Generic `Node3D` object pool — visibility toggle instead of spawn/free |
| `TargetingMaterialCache.cs` | Static shared `StandardMaterial3D` factory/cache keyed by `(MaterialKind, Color)` |
| `CursorManager.cs` | Maps `TargetingCursorMode` → Godot built-in cursor shapes |

---

## Integration Points

### CombatArena (`Combat/Arena/CombatArena.cs`)

CombatArena owns the three targeting components and wires them together:

```csharp
// Fields
private TargetingSystem _targetingSystem;
private TargetingHoverPipeline _hoverPipeline;
private TargetingVisualSystem _targetingVisualSystem;

// Public accessors
public TargetingSystem Targeting => _targetingSystem;
public TargetingHoverPipeline HoverPipeline => _hoverPipeline;
public TargetingVisualSystem TargetingVisuals => _targetingVisualSystem;
```

**Initialization** (in `InitializeTargetingSystem()`):
1. Creates dependencies: `LOSService`, combatant lookups, position resolver, physics space state
2. Creates `TargetingHoverPipeline` with combatant lookup
3. Creates `TargetingSystem` with the combat state machine
4. Registers all 12 modes via `RegisterMode()`
5. Subscribes to `OnTargetingConfirmed` and `OnTargetingCancelled`

**Beginning targeting** (when player clicks an ability in the hotbar):
```csharp
if (_targetingSystem.BeginTargeting(actionId, action, actor, sourceWorldPos))
{
    // Targeting started — input handler will drive frame updates
}
```

**Force-ending** (turn end, combat end):
```csharp
_targetingSystem?.ForceEnd();
_targetingVisualSystem?.ClearAll();
```

### CombatInputHandler (`Combat/Arena/CombatInputHandler.cs`)

The input handler checks if targeting is active and delegates clicks:

**Left-click (confirm):**
```csharp
if (Arena?.Targeting != null && Arena.Targeting.CurrentPhase != TargetingPhase.Inactive)
{
    var mousePos = GetViewport().GetMousePosition();
    var hover = Arena.HoverPipeline.Update(Camera, mousePos);
    Arena.Targeting.HandleConfirm(hover);
    GetViewport().SetInputAsHandled();
    return;
}
```

**Right-click (cancel / undo):**
```csharp
if (Arena?.Targeting != null && Arena.Targeting.CurrentPhase != TargetingPhase.Inactive)
{
    Arena.Targeting.HandleCancel();
    GetViewport().SetInputAsHandled();
    return;
}
```

### Hover Pipeline

`TargetingHoverPipeline.Update(camera, mousePosition)` performs two-layer raycasting:

1. **Entity layer** (collision layer 2, `Area3D`): finds `CombatantVisual` under cursor
2. **Ground layer** (collision layer 1, `StaticBody3D`): finds ground hit-point
3. **Fallback**: intersects y=0 plane if ground raycast misses

Returns a fully-populated `HoverData` struct every physics frame.

---

## BG3 Parity Checklist

Status of all 12 targeting modes versus their BG3 equivalents:

| # | Mode | BG3 Equivalent | BG3 Behavior | Examples |
|---|---|---|---|---|
| 1 | `SingleTarget` | Entity targeting | Click-on-entity, hit-chance tooltip, range/faction/LOS validation, cursor changes | Melee Attack, Fire Bolt, Cure Wounds |
| 2 | `FreeAimGround` | Point targeting | Click-on-ground with range ring; reticle at cursor; no entity required | Misty Step, Dimension Door, Summon placement |
| 3 | `StraightLine` | Beam targeting | Straight ray from caster toward cursor; blocked-segment preview; obstruction detection | Scorching Ray (single beam), Eldritch Blast visual |
| 4 | `BallisticArc` | Throw trajectory | Parabolic arc with dotted preview; gravity-based trajectory; obstruction check | Throw (any item), Catapult, Alchemist Fire |
| 5 | `BezierCurve` | Guided projectile | Smooth curve path preview between caster and target | Guided projectile abilities |
| 6 | `PathfindProjectile` | Pathfind projectile | Projectile follows navmesh-aware path around obstacles | Seeking projectile variants |
| 7 | `AoECircle` | Ground circle AoE | Circle at cursor, affected-entity highlights, friendly-fire orange fill, range ring | Fireball, Shatter, Spirit Guardians, Hunger of Hadar |
| 8 | `AoECone` | Cone AoE | Cone from caster rotates with cursor; affected highlights; faction coloring | Burning Hands, Cone of Cold, Color Spray |
| 9 | `AoELine` | Line AoE (wide) | Rectangular line from caster toward cursor with width; affected entities highlighted | Lightning Bolt (area), Wall of Thorns line variant |
| 10 | `AoEWall` | Wall placement | Two-click: start → end; wall segment preview between clicks; length/angle constraints | Wall of Fire, Blade Barrier, Wind Wall |
| 11 | `MultiTarget` | Sequential multi-pick | Click targets one-by-one; numbered markers; counter text ("2/3"); RMB undoes last pick | Eldritch Blast (multi-beam), Magic Missile, Scorching Ray |
| 12 | `Chain` | Chain bounce | Auto-chain preview showing bounce order; max targets; diminishing damage indicators | Chain Lightning, certain bouncing effects |

### TargetType → Mode Mapping

| `ActionDefinition.TargetType` | Resolves To | Notes |
|---|---|---|
| `Self` | *(no mode — primed)* | Auto-targets caster |
| `All` | *(no mode — primed)* | Hits all valid targets |
| `None` | *(no mode — primed)* | No target needed |
| `SingleUnit` | `SingleTarget` | |
| `MultiUnit` | `MultiTarget` | |
| `Point` | `FreeAimGround` | |
| `Circle` | `AoECircle` | |
| `Cone` | `AoECone` | |
| `Line` (no area radius) | `StraightLine` | Beam-like abilities |
| `Line` (has area radius) | `AoELine` | Wide line AoE |
| `Charge` | `StraightLine` | Reuses straight-line preview |
| `WallSegment` | `AoEWall` | Two-click wall placement |
