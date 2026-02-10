# BG3 Mechanics Progress

This document tracks implementation status for `BG3-MECHANICS-GUIDE.md` in the prototype combat runtime.

## Implemented in this pass

- Concentration runtime is now fully wired in live combat (`CombatArena` + `EffectPipeline`):
  - concentration starts for concentration abilities even when no explicit target list is present (point/zone casts)
  - concentration saves now use the concentrating combatant's actual Constitution save bonus and active modifiers
  - concentration break now removes matching concentration statuses applied by the caster across all affected targets (multi-target effects)
- BG3 hard-control break behavior added:
  - `asleep` and `hypnotised` now end when the target takes real damage
- Shield/magic-missile interaction corrected:
  - Shield reaction now applies `shield_spell` status in runtime
  - `magic_missile` damage is fully negated while `shield_spell` is active
- Core resolution math now uses combatant stats/proficiency for:
  - weapon/spell attack bonuses
  - saving throw bonuses
  - dynamic save DC when not explicitly specified (`8 + proficiency + relevant casting/attack stat`)
- Critical threshold support added to the rules engine and driven by character features/feats:
  - Fighter Champion `Improved Critical` support (`19-20` weapon crit)
  - `Spell Sniper` support (`19-20` spell crit)
- Status-based action blocking wired into ability usability checks.
- Surface system integration expanded:
  - surface statuses are now applied on enter/turn-start
  - turn-start/turn-end/round-end surface processing is wired into combat flow
  - movement now uses real surface data for terrain and triggers
  - `spawn_surface` effects now create live surfaces through `SurfaceManager`
  - area-targeted `spawn_surface` effects now use the selected target point (not only first resolved target)
  - duplicate surface recasts at the same location/type now refresh existing surface state instead of unbounded stacking
- Reaction baseline plumbing expanded:
  - baseline opportunity attack reaction is granted to spawned combatants
  - counterspell/shield reaction definitions are registered
  - synchronous cast/damage reaction handling supports cancel and damage reduction paths
- New mechanics content packs added:
  - `Data/Abilities/bg3_mechanics_abilities.json`
  - `Data/Statuses/bg3_mechanics_statuses.json`
- New full-fidelity scenario added for focused mechanics validation:
  - `Data/Scenarios/ff_short_bg3_mechanics.json`
- Full-fidelity watchdog startup handling improved:
  - added initial-action grace for full-fidelity auto-battle startup so HUD/bootstrap latency does not cause false freeze failures.

## Remaining broad backlog (guide-level)

- Full class-resource systems and per-rest refresh logic are still partial (many entries exist in class/feat data but not all are runtime-active mechanics).
- Several mechanics are currently approximations due generic modifier model limits (for example damage-type-conditional vulnerability/resistance).
- Some advanced spell behaviors are present as prototype hooks but not fully tactical-complete (for example exact area-trigger semantics and fully interactive reaction prompts during cast interrupts).
