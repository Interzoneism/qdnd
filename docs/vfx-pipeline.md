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
5. Resolver hint mapping (`cast_start`, variant/action VFX IDs, spell type hints)
6. `fallbackRule` by phase

## Request Contract
`VfxRequest` now carries context needed for deterministic visual mapping:
- Action/source/target IDs and positions
- Optional `AttackType`, `TargetType`, `DamageType`, `Intent`
- Spell metadata (`IsSpell`, `SpellSchool`, `SpellType`) and optional VFX hint IDs (`ActionVfxId`, `VariantVfxId`)
- `Phase`, `Pattern`, `Magnitude`, `Seed`

`SfxRequest` now mirrors the same context fields in its schema.

## Spell Coverage
- Spells no longer depend on `AttackType` being populated to get spell-looking visuals.
- `defaultRules` can now match `isSpell: true` for generic spell coverage.
- Resolver fallback also consumes spell metadata + VFX hints (`cast_start`, action/variant VFX IDs, BG3 spell type hints) before phase fallback.
- Result: every spell request resolves to a valid preset, even when a spell lacks explicit action overrides.

## Validation Flow
Recommended verification after VFX changes:

```bash
./scripts/run_screenshots.sh
./scripts/compare_screenshots.sh
./scripts/run_autobattle.sh --full-fidelity --seed 42
```
