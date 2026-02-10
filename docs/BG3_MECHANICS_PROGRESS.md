# BG3 Mechanics Progress

This document tracks implementation status for `BG3-MECHANICS-GUIDE.md` in the prototype combat runtime.

## Implemented in this pass

- Core class/spell resource plumbing is now in runtime:
  - added combatant-level non-HP resource pools (`CombatantResourcePool`) with current/max tracking
  - character resolution now imports level progression resources and spell slot ladders into resolved builds
  - scenario loading seeds resolved resources into live combatants
  - ability usability now checks resource costs and execution consumes those resources
  - `modify_resource` effects can now modify non-HP resources
  - save/load snapshots now persist non-HP resource current/max values
  - action bar now exposes real ability resource costs to the HUD model
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

## Foundation Readiness Assessment

- Resolution foundation: **good enough to scale**
  - attack/save/DC math, advantage/disadvantage, concentration loop, and crit-threshold plumbing are active and testable.
- Resource foundation: **combat-ready, rest systems deferred**
  - non-HP costs can be enforced; rest (short/long) refresh policy is intentionally deprioritized for the combat-only scope. Abilities that would consume rest-based resources in BG3 are refreshed each combat (per-encounter refresh) for this game.
- Status/surface foundation: **usable, with fidelity gaps**
  - core flow is wired, but several BG3-specific conditional interactions still rely on simplified modifier behavior.
- Content foundation: **data-rich, runtime-light**
  - class/feat JSON coverage is broad, but many entries are descriptive and not yet executed as concrete combat hooks.
- Validation foundation: **usable**
  - headless, autobattle, and full-fidelity harnesses are in place and suitable for iterative feature landing.

## Remaining Broad Backlog (Guide-Aligned)

### 1) Resource Lifecycle and Rest Systems (DEFERRED)

- Deprioritized for the combat-only scope: runtime short-rest and long-rest refresh policies are out of scope for this pass. Abilities/actions that would use rest mechanics in BG3 will be refreshed each combat (per-encounter refresh).
- Convert per-rest usage caps into per-combat/per-encounter caps where appropriate; full per-rest cap systems are deferred.
- Expose in-combat refresh state in HUD and save/load snapshots; rest-recharge UI/state is deferred.

### 2) Class Mechanics Coverage (Sections 3.1-3.10)

- Implement missing high-impact mechanics as runtime hooks, not text-only definitions:
  - Barbarian: Rage package, Reckless Attack advantage exchange, Frenzy/Wildheart behavior.
  - Bard: Bardic Inspiration die usage flow, Cutting Words reaction, Flourish spending.
  - Cleric/Paladin: Channel Divinity options, Divine Smite trigger path, Aura of Protection radius save bonus.
  - Druid: Wild Shape layered HP model, form swapping rules, Moon/Spores branches.
  - Fighter/Monk/Rogue/Sorcerer/Warlock: Action Surge cadence, ki spenders, Sneak Attack once/turn gates, metamagic transforms, Eldritch Blast beam scaling.

### 3) Action Economy Registry Completion (Section 4)

- Complete tactical actions with rule-correct checks and movement interactions:
  - Dash, Disengage, Throw, Hide, Help, Shove, Jump, Dip.
- Wire weapon action recharge cadence (reset per combat) and weapon proficiency gating in runtime checks.

### 4) Feat Runtime Activation (Section 5)

- Convert major feats from passive data into executable mechanics:
  - Great Weapon Master / Sharpshooter toggles and kill/crit bonus attacks.
  - Tavern Brawler doubled STR contribution rules.
  - War Caster OA cast and concentration advantage flow.
  - Sentinel reaction lock and movement-zero enforcement.
  - Lucky point pool with per-day depletion and reroll APIs.

### 5) Spell Fidelity and Casting Interrupts (Section 6)

- Complete upcast scaling for all spells that modify dice/targets by slot level.
- Finish reaction interrupt fidelity:
  - counterspell level checks, prompt timing, and cancellation semantics.
- Add missing tactical spell edge behaviors:
  - Spirit Guardians enter/start triggers, Spike Growth movement ticks, Haste lethargic crash turn logic, Cloud of Daggers dual-tick behavior.

### 6) Surface/Status Mechanical Fidelity (Sections 6.2, 7)

- Expand non-generic conditional modifiers for:
  - damage-type-conditional resist/vuln switching (Wet/Fire/Cold/Lightning)
  - status-dependent AC/save changes (Dazed, Prone, Paralyzed, Threatened edge rules).
- Ensure break/clear semantics match BG3 expectations (Help, damage break, turn-end decay, stand-up costs).

### 7) Content Authoring and Regression Safety

- Expand scenario matrix that covers each major mechanic family with deterministic seeds.
- Add missing integration tests for each newly activated class/feat/spell feature.
- Keep full-fidelity verification as release gate for every mechanic batch.

## Research Notes (Current Codebase Signals)

- Class and feat data is extensive (`Data/Classes/*.json`, `Data/Feats/bg3_feats.json`), but many effects are currently represented as descriptions or generic modifiers and need explicit combat runtime handlers.
- Some high-impact features are partially present (for example critical threshold, war caster concentration advantage, alert initiative bonus), indicating the architecture supports incremental feature activation.
- Weapon action definitions (Cleave/Lacerate/Smash/Topple/Pommel Strike/Tenacity) exist in data packs, but recharge/proficiency/full tactical constraints are not fully end-to-end in runtime.
- Surface, reaction, and concentration systems are now solid enough to support layered mechanics without re-architecting core combat state flow.

## Parallel Work Package Plan

### Wave A: Combat-focused Resource Core (highest leverage)

- WP-A1: In-combat refresh rules + per-resource current/max tracking.
- WP-A2: HUD/save-load exposure for in-combat refresh state.
- WP-A3: Tests for per-combat refresh determinism and per-combat caps.
- Note: Full rest/long-rest systems are deferred to a later backlog wave; combat-first refresh rules will be used for the game's scope.

### Wave B: Class Combat MVPs

- WP-B1: Fighter/Rogue/Barbarian active mechanics.
- WP-B2: Cleric/Paladin/Druid active mechanics.
- WP-B3: Bard/Monk/Sorcerer/Warlock active mechanics.

### Wave C: Action/Feat/Spell Completion

- WP-C1: Core action economy completion.
- WP-C2: High-impact feat execution.
- WP-C3: Spell edge-case fidelity and interrupt timing.

### Wave D: Status/Surface Fidelity and Hardening

- WP-D1: Conditional resistance/vulnerability/status math upgrades.
- WP-D2: Regression sweep across deterministic seeds and scenario matrix.

## Verification Protocol Per Package

- Headless/unit/integration first:
  - `./scripts/ci-build.sh`
  - `./scripts/ci-test.sh`
- Scenario verification:
  - max 2 full-fidelity runs before analyze/fix loop
  - preferred deterministic seed + short scenario before broad/random scenario sweeps
