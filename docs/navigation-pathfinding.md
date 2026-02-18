# Tactical Pathfinding (Arena + AI)

## Overview

Combat movement now uses tactical pathfinding instead of straight-line reach checks.

- Core solver: `Combat/Movement/TacticalPathfinder.cs`
- Movement integration: `Combat/Movement/MovementService.cs`
- Arena obstacle probe: `Combat/Arena/CombatArena.cs`
- AI scoring integration: `Combat/AI/AIDecisionPipeline.cs`

The pathfinder uses A* on the XZ plane, supports diagonal movement (with corner-cut prevention), and applies terrain multipliers along traversed path segments.

## What Is Considered Blocking

- Static world geometry via `MovementService.IsWorldPositionBlocked` callback.
- Active combatants (except the mover), using collision radius and vertical tolerance checks.
- Occupied destination cells remain invalid.

In the arena, `CombatArena` wires the callback to a physics shape probe (`IntersectShape`) so walls/crates made from `StaticBody3D` are treated as movement blockers automatically.

## Movement Cost Model

Movement cost is now path-weighted:

- Cost is sampled per segment along the planned route.
- Difficult terrain only increases cost on the portion of path that actually crosses it.
- Detours around blockers naturally cost more movement than direct lines.

This applies to:

- `MovementService.CanMoveTo`
- `MovementService.MoveTo`
- `MovementService.GetPathCost`
- `MovementService.GetPathPreview`

## AI Behavior Impact

AI movement scoring now uses path preview/cost instead of pure straight-line distance.

- Invalid path => candidate rejected.
- Detours add a score penalty.
- Higher path cost lowers movement efficiency score.
- Threat/hazard exposure along waypoints adds defensive penalties.

## Tuning

`MovementService` exposes:

- `PathNodeSpacing` (default set by arena to `0.75`)
- `IsWorldPositionBlocked` callback

`TacticalPathfinder` exposes internal defaults:

- `NodeSpacing`
- `SearchPaddingCells`
- `MaxExpandedNodes`

## Jump Trajectory Pathfinding

Jump targeting now uses a dedicated 3D planner:

- Core solver: `Combat/Movement/JumpPathfinder3D.cs`
- Jump preview: `Combat/Arena/JumpTrajectoryPreview.cs`
- Arena integration: `Combat/Arena/CombatArena.cs`

Behavior:

- Jump preview is rendered as a dotted arc instead of the normal range circle.
- The planner validates a collision-free 3D route against world geometry and other combatants.
- The generated arc enforces a minimum midpoint lift of `2.0m`.
- Path validity and execution both use the same planned trajectory to avoid preview/execution mismatch.
- Preview turns red when trajectory length exceeds the actor's jump distance.
