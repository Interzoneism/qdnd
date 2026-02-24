# BG3 Surface System (Runtime Notes)

This project now includes a BG3-style surface runtime centered on `SurfaceManager`.

## Core model

- `SurfaceDefinition` supports:
  - Layering (`Ground`, `Cloud`)
  - Contact reactions (`ContactReactions`)
  - Event reactions (`EventReactions`)
  - Visual metadata (`ColorHex`, `VisualOpacity`, `IsLiquidVisual`, wave params)
  - Terrain/status metadata (tags, movement multiplier, status application)
- `SurfaceInstance` supports multi-blob geometry:
  - `InitializeGeometry(center, radius)`
  - `AddBlob(center, radius)` (grow)
  - `SubtractArea(center, radius)` (carve/remove)
  - `MergeGeometryFrom(other)` (merge overlapping same-surface areas)

## Runtime behavior

- `CreateSurface`:
  - Creates blob geometry
  - Merges same-type/layer overlaps
  - Resolves overlap/contact reactions with existing surfaces
- `ApplySurfaceEvent(eventId, position, radius, sourceId)`:
  - Applies event reactions (for example: `ignite`, `freeze`, `electrify`, `douse`, `melt`)
  - Supports fallback transforms for common BG3-style interactions
- `AddSurfaceArea` and `SubtractSurfaceArea` allow dynamic growth/carving at runtime.
- `ResolveCombatants` hook enables reaction explosions/status application to nearby units.

## Character interactions

- Enter/turn-start triggers apply damage/status from surface definitions.
- Slippery surfaces (`slippery` tag) can apply `prone` after a Dex save.
- Difficult terrain comes from `MovementCostMultiplier` and tags such as `difficult_terrain`.

## Visuals

- `SurfaceVisual` renders one shallow mesh per blob.
- Liquid surfaces use an animated shader-based material.
- Non-liquid surfaces use translucent flat materials.
- `CombatArena` listens for:
  - `OnSurfaceCreated`
  - `OnSurfaceTransformed`
  - `OnSurfaceRemoved`
  - `OnSurfaceGeometryChanged`
  to keep visuals synced with runtime geometry changes.
