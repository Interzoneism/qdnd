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
  - Normalizes common BG3 raw surface tokens/aliases (for example `WaterFrozen`, `FogCloud`, `DarknessCloud`, `SpikeGrowth`, `Vines`)
  - Merges same-type/layer overlaps
  - Resolves overlap/contact reactions with existing surfaces
- `ApplySurfaceEvent(eventId, position, radius, sourceId)`:
  - Applies event reactions (for example: `ignite`, `freeze`, `electrify`, `douse`, `melt`)
  - Supports additional BG3 event aliases such as `DestroyWater`/`destroy_water`
  - Supports global daylight cleanup of darkness-style surfaces
  - Supports fallback transforms for common BG3-style interactions
- `AddSurfaceArea` and `SubtractSurfaceArea` allow dynamic growth/carving at runtime.
- `ResolveCombatants` hook enables reaction explosions/status application to nearby units.

## Extended surface IDs

Alongside the original core set, runtime definitions now include:

- `poison_cloud`
- `spores`
- `insect_plague`
- `wind`
- `entangle`
- `daylight`
- `stone_wall`

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
