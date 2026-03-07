# BG3 Surface System (Runtime Notes)

This project now includes a BG3-style surface runtime centered on `SurfaceManager`.

## Core model

- `SurfaceDefinition` supports:
  - Layering (`Ground`, `Cloud`)
  - Spawn pattern metadata (`Pattern`, `PatternNoise`, `VisualPaddingCells`)
  - Contact reactions (`ContactReactions`)
  - Event reactions (`EventReactions`)
  - Visual metadata (`ColorHex`, `VisualOpacity`, `IsLiquidVisual`, wave params)
  - Terrain/status metadata (tags, movement multiplier, status application)
- `SurfaceInstance` is now **cell-authoritative**:
  - Occupancy is tracked as `HashSet<SurfaceCell>` (0.5m grid)
  - `ContainsPosition`, overlap checks, movement traversal, and status/damage triggers all query cell occupancy
  - `Position`/`Radius` are derived convenience values (centroid + approximate bounds), not primary truth

## Runtime behavior

- `CreateSurface`:
  - Rasterizes a patterned cell mask from radius and surface definition
  - Normalizes common BG3 raw surface tokens/aliases (for example `WaterFrozen`, `FogCloud`, `DarknessCloud`, `SpikeGrowth`, `Vines`)
  - Merges same-type/layer overlaps based on cell overlap/adjacency
  - Resolves overlap/contact reactions with existing surfaces
- `ApplySurfaceEvent(eventId, position, radius, sourceId)`:
  - Applies event reactions (for example: `ignite`, `freeze`, `electrify`, `douse`, `melt`)
  - Supports additional BG3 event aliases such as `DestroyWater`/`destroy_water`
  - Supports global daylight cleanup of darkness-style surfaces
  - Supports fallback transforms for common BG3-style interactions
- `AddSurfaceArea` and `SubtractSurfaceArea` add/remove occupied cells at runtime.
- `ResolveCombatants` hook enables reaction explosions/status application to nearby units.
- `CreatePuddle` generates irregular puddles from random-walk cell growth (explicit `totalCells` semantics).

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

- `SurfaceVisual` builds a dynamic tile mesh directly from occupied grid cells.
- Mesh generation uses per-cell quads plus configurable padding (`VisualPaddingCells`) to avoid visible seams.
- Liquid/cloud/solid shaders are still selected per surface category.
- `CombatArena` listens for:
  - `OnSurfaceCreated`
  - `OnSurfaceTransformed`
  - `OnSurfaceRemoved`
  - `OnSurfaceGeometryChanged`
  to keep visuals synced with runtime geometry changes.
