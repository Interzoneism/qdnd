# VFX Pipeline (Main System)

## Overview
Combat VFX now runs through a single data-driven path:

1. `CombatPresentationService` publishes structured `VfxRequest` events to `PresentationRequestBus`.
2. `VfxPlaybackService` subscribes to the bus.
3. `VfxRuleResolver` resolves each request into a preset from `Data/VFX/vfx_rules.json` + `Data/VFX/vfx_presets.json`.
4. `VfxPatternSampler` computes emission points for point/per-target/circle/cone/line/path patterns.
5. `CombatVFXManager.Spawn(VfxResolvedSpec)` executes the resolved procedural recipe.

There is no legacy direct spawn path in presentation logic.

## Data Files
- `Data/VFX/vfx_presets.json`
  - Reusable presets: `id`, `particleRecipe`, `lifetime`, `sampleCount`, pattern tuning (`radius`, `coneAngle`, `lineWidth`), and runtime caps (`activeCap`, `initialPoolSize`).
- `Data/VFX/vfx_rules.json`
  - `defaultRules`
  - `actionOverrides`
  - `fallbackRule`

## Rule Precedence
Resolution order is deterministic:

1. `VfxRequest.PresetId` (explicit runtime override)
2. `actionOverrides` matching `actionId + variantId + phase`
3. `actionOverrides` matching `actionId + phase`
4. `defaultRules` best-specificity match
5. `fallbackRule` by phase

## Request Contract
`VfxRequest` now carries context needed for deterministic visual mapping:
- Action/source/target IDs and positions
- Optional `AttackType`, `TargetType`, `DamageType`, `Intent`
- `Phase`, `Pattern`, `Magnitude`, `Seed`

`SfxRequest` now mirrors the same context fields in its schema.

## Validation Flow
Recommended verification after VFX changes:

```bash
./scripts/run_screenshots.sh
./scripts/compare_screenshots.sh
./scripts/run_autobattle.sh --full-fidelity --seed 42
```
