# Tactical Turn-Based Combat System (BG3 / Divinity-Style) — Master To-Do (Godot 3D, C#)

> Goal: When everything in this file is done, the combat **plays and feels** like BG3 / Divinity 2 in all important aspects (turn-based, initiative, action economy, tactical movement, reactions, statuses, surfaces/field effects, targeting, probability, AI, UI/UX, persistence, mod/tooling).
> Non-goal: Specific game content (spells, classes, races, named abilities). We build the **systems** that can host them.

---

## Testbed-first rule (mandatory for all tasks)

### What Testbed.tscn is

* `Testbed.tscn` **already exists** and is the **single always-current integration scene**.
* It must **always reflect the current game/combat state** and provide a reliable way to exercise new systems **without requiring visual inspection**.

### Non-visual / agent-safe definition of “included in Testbed”

A feature is “included in Testbed” when it is wired so it can be verified by at least one of:

* **Compile-time integration** (scene loads, nodes/services resolve, no missing scripts/resources).
* **Headless validation** (unit tests, simulation tests, deterministic replay tests).
* **Structured logs / events** with assertions (combat log events, replay logs, invariant checks).
* **Debug commands / feature flags** that can be toggled via code/config and verified by emitted state/events.

### Absolute prohibition

* No task may require “launch the game and look at it”, “verify visually”, “check that it feels right by playing”, etc.
* Use logs, tests, deterministic replays, assertions, and state snapshots instead.

### Testbed contract (must remain true)

* Testbed is the **default boot scene** in dev (or selectable via one config constant) and can run in headless mode where possible.
* Testbed has a **Scenario Loader** (data-driven) to spawn units, surfaces, props, and scripted combat situations.
* Every new system adds at least one **Scenario** and at least one **automated validation** (unit test or simulation invariant) that proves it works.

---

## 0) Project foundations

### 0.1 Repo / structure

* [ ] Define project folder structure for combat modules (`Combat/`, `Rules/`, `UI/`, `AI/`, `Data/`, `Net/`, `Tools/`, `Tests/`).
* [ ] Add coding standards for agents: naming, logging, deterministic rules evaluation, data-driven design, no scene-tight coupling.
* [ ] Add a central `CombatContext` and service-locator / DI pattern (simple internal DI is fine).
* [ ] Add a feature-flag / debug command system (toggle fog of war, show hit chances, force rolls, skip animations).
* [ ] **Testbed:** Add `TestbedBootstrap` that builds `CombatContext`, registers services, loads a default scenario, and emits a “ready” event/log with resolved services list.

### 0.2 Determinism + reproducibility

* [ ] Implement deterministic RNG with seeds per combat instance; log every roll with source.
* [ ] Create a full combat “replay log” format: inputs + rolls + resolved outcomes for debugging.
* [ ] **Testbed:** Provide a “RunScenario(seed)” path that prints/exports replay log and final state snapshot deterministically.

### 0.3 Data-driven content pipeline

* [ ] Choose data format (JSON / YAML / Godot Resources) for: abilities, effects, items, AI profiles, rules constants.
* [ ] Build a runtime registry + validation (schema checks, missing references, circular dependencies).
* [ ] Hot-reload in dev (where possible) for tuning.
* [ ] **Testbed:** Add a registry validation step at startup that fails fast with structured errors (and test coverage).

---

## 1) Core combat architecture

### 1.1 Combat states + state machine

* [ ] Implement top-level combat state machine:

  * [ ] Exploration (no combat)
  * [ ] Combat start (setup, initiative)
  * [ ] Turn start
  * [ ] Player/AI decision
  * [ ] Action execution (movement/ability/item)
  * [ ] Reactions / interrupts
  * [ ] Turn end
  * [ ] Round end
  * [ ] Combat end (loot, cleanup, persistence)
* [ ] Support nested substates:

  * [ ] Target selection
  * [ ] AoE preview / placement
  * [ ] Movement pathing preview
  * [ ] Reaction prompt
  * [ ] Cinematic/animation lock (with safe cancel/back)
* [ ] Guarantee state transitions are explicit and logged.
* [ ] **Testbed:** Include scenarios that traverse each major state and assert state transition sequence via logged events (no visuals).

### 1.2 Entity model (combatants)

* [ ] Define `Combatant` abstraction (characters, summons, companions, enemies, neutral units).
* [ ] Component model:

  * [ ] Stats component (attributes, derived stats)
  * [ ] Resources component (HP, temporary HP, armor-like layers if used, action points, spell slots-like, etc.)
  * [ ] Equipment component (weapons/armor modifiers, tags)
  * [ ] Conditions/Statuses component
  * [ ] Faction/Allegiance component
  * [ ] Perception/Visibility component
  * [ ] Movement component (speed, modes)
  * [ ] Action/Reactions component
* [ ] Handle “downed”, “dead”, “unconscious”, “removed from fight”, “fled”.
* [ ] Summons / pets / controllable allies ownership and initiative rules.
* [ ] **Testbed:** Provide a scenario that spawns at least: 2 allies, 2 enemies, 1 summon, and validates ownership + initiative placement via assertions.

### 1.3 Time, turns, rounds

* [ ] Implement initiative system:

  * [ ] Rolls/values + tie-breakers
  * [ ] Group initiatives (optional rule toggle) / allied turn grouping support
  * [ ] Delaying / holding / readying support (system-level even if content varies)
* [ ] Turn order UI and underlying queue structure.
* [ ] Round counters and per-round triggers.
* [ ] **Testbed:** Provide deterministic initiative scenarios that assert queue order, grouping behavior, and delay/ready changes without UI.

---

## 2) Rules engine (BG3-like outcomes)

### 2.1 Rules evaluation framework

* [ ] Build a rules engine with:

  * [ ] Pure functions where possible (input snapshot -> output)
  * [ ] “Modifiers” stack (buffs/debuffs, equipment, situational)
  * [ ] “Queries” for calculations (hit chance, damage, DCs)
  * [ ] “Events” that fire for triggers (OnAttackDeclared, OnDamageTaken, etc.)
* [ ] Ensure it supports layered overrides and priority ordering.
* [ ] **Testbed:** Add a “RulesProbe” scenario runner that executes a set of query cases and asserts expected numeric results (golden tests).

### 2.2 Action economy (hallmark feature)

* [ ] Implement a flexible action economy model:

  * [ ] Main action (or equivalent)
  * [ ] Bonus / minor action (or equivalent)
  * [ ] Movement budget (distance-based)
  * [ ] Reaction budget (per round)
  * [ ] Free actions / interaction actions
* [ ] Support action conversion rules (e.g., using action to dash, etc.) via data.
* [ ] Action cost preview in UI (including “this will consume your reaction”).
* [ ] **Testbed:** Scenario asserts budgets decrement correctly and reset at correct turn/round boundaries (via state snapshot).

### 2.3 Movement rules

* [ ] Movement types:

  * [ ] Walk
  * [ ] Jump / leap (parabola + validation)
  * [ ] Climb / drop (with fall consequences)
  * [ ] Fly (if enabled)
  * [ ] Swim (if enabled)
  * [ ] Teleport-like relocation (system support)
* [ ] Opportunity attacks / disengage-like rules (data-driven).
* [ ] Difficult terrain / movement penalties (tile/volume-based).
* [ ] Forced movement (push/pull/knockback) with collision checks.
* [ ] Line of sight + line of fire influence on movement previews.
* [ ] **Testbed:** Add movement validation scenarios that assert path cost, opportunity trigger eligibility, forced-move collision outcomes, and jump validation results.

### 2.4 Attack resolution (generic)

* [ ] Support:

  * [ ] Attack rolls vs defenses OR deterministic hit vs miss rules (toggleable)
  * [ ] Advantage/disadvantage-like systems (multiple sources, stacking rules)
  * [ ] Critical hits / critical failures
  * [ ] Auto-hit / auto-miss rules when applicable
* [ ] Implement “to-hit breakdown” explanation (hover tooltip shows contributors).
* [ ] **Testbed:** Add “AttackMatrix” scenario generating many attacks with fixed seed and asserting distribution / critical logic + breakdown payload structure.

### 2.5 Saving throws / contested checks

* [ ] Implement save/check system:

  * [ ] DC calculation
  * [ ] Ability/skill-like modifiers
  * [ ] Proficiency/expertise-like multipliers (generic)
  * [ ] Advantage/disadvantage on saves
* [ ] Contested checks (two-sided rolls) framework.
* [ ] **Testbed:** Deterministic save/contest scenarios that assert DC math and contested winner rules (ties, modifiers).

### 2.6 Damage, healing, mitigation

* [ ] Damage pipeline:

  * [ ] Base damage
  * [ ] Additive modifiers
  * [ ] Multipliers (vulnerability/resistance-like)
  * [ ] Flat reductions / shields
  * [ ] Temporary HP / barrier layers
  * [ ] Overkill + spill rules
* [ ] Healing pipeline:

  * [ ] Direct heal, regen over time
  * [ ] Heal reduction / prevention statuses
* [ ] Damage types framework (system-level categories, not content-specific).
* [ ] Immunity/resistance/vulnerability framework.
* [ ] **Testbed:** Add golden tests for pipeline ordering and edge cases (barrier spill, resist+flat reduction ordering).

### 2.7 Conditions / statuses / tags

* [ ] Robust status system:

  * [ ] Duration models: turns, rounds, real-time seconds, until event
  * [ ] Stacking rules: refresh, extend, stack magnitude, unique
  * [ ] Source attribution (who applied it)
  * [ ] Dispels/cleanses with filters
* [ ] Condition effects:

  * [ ] Stat modifiers
  * [ ] Action restrictions (silenced, stunned, prone-like)
  * [ ] Triggered events (on hit, on move, on start turn)
  * [ ] Control effects (charm/fear-like) as system hooks
* [ ] Tagging system for synergy checks (`weapon:melee`, `surface:fire`, `condition:wet`, etc.).
* [ ] **Testbed:** Include a “StatusStacking” scenario that applies/refreshes/extends statuses and asserts final duration/magnitude and event triggers.

### 2.8 Reactions and interrupts (big hallmark)

* [ ] Reaction system core:

  * [ ] Trigger detection (enemy leaves melee, ally hit, spell cast nearby, etc.)
  * [ ] Eligible reactors query
  * [ ] Reaction priority + ordering when many are eligible
  * [ ] Prompt UI for player-controlled reactors
  * [ ] AI reaction policy for AI units
  * [ ] “Ask every time” vs “auto use” toggles per reaction
* [ ] Interrupt execution that can:

  * [ ] Cancel/modify the triggering event
  * [ ] Insert additional actions into resolution stack
* [ ] Ensure reactions work during:

  * [ ] Movement
  * [ ] Attacks
  * [ ] Ability casts
  * [ ] Damage application
  * [ ] Status application
* [ ] **Testbed:** Provide scripted scenarios with multiple eligible reactors and assert reaction order, prompt payload, and resulting modified/cancelled events.

### 2.9 Concentration/channeling-like mechanics

* [ ] Support persistent effects tied to a unit state:

  * [ ] Concentration: only one at a time, breaks on damage/save
  * [ ] Channels: costs action/maintain rules
  * [ ] Aura effects: continuous area query each tick/turn
* [ ] **Testbed:** Scenario asserts “only one concentration” rule, break checks, and aura tick effects.

### 2.10 Environmental interactions (BG3/DOS hallmark)

* [ ] Surface / field-effect system:

  * [ ] Surfaces as volumes/areas with type + intensity
  * [ ] Surface creation/removal/transform rules (data-driven)
  * [ ] Surface interactions (ignite, freeze, electrify, etc. as generic transforms)
  * [ ] Unit interaction: entering, leaving, starting turn within
  * [ ] Projectile/area interactions (arrows/spells passing through)
* [ ] Physics/object interactions:

  * [ ] Pick up / throw objects (system support)
  * [ ] Breakable props affecting line of sight / cover
  * [ ] Explosive barrels-like objects: generic “reactive objects”
  * [ ] Pushing objects/units, shoving off ledges
* [ ] Height & verticality:

  * [ ] High/low ground modifiers (as generic rule hooks)
  * [ ] Fall damage and forced fall
  * [ ] Jump distance and landing validation
* [ ] Cover / obscured:

  * [ ] Partial cover, full cover, obscured rules (generic)
  * [ ] Hide/stealth support hooks (combat-only is enough)
* [ ] **Testbed:** Add surface transform scenarios and reactive object scenarios; assert transforms, triggers, and resulting status/damage events.

---

## 3) Targeting, geometry, and validation

### 3.1 Grids / free movement hybrid

* [ ] Choose approach: free movement with distance budget OR optional grid overlay.
* [ ] Implement distance measurement that matches camera and ground navigation.
* [ ] Ensure ability ranges use the same measurement system as movement.
* [ ] **Testbed:** Add measurement tests (distance, range checks, edge tolerance) validated numerically.

### 3.2 Line-of-sight / line-of-fire

* [ ] Implement LOS checks:

  * [ ] Raycast sets (multi-sample for capsule width)
  * [ ] Height-aware checks
  * [ ] Dynamic obstacles and doors
* [ ] LOF vs LOS distinction where needed (shooting through glass, etc. as hooks).
* [ ] **Testbed:** Scenario spawns obstacles/doors and asserts LOS/LOF booleans for specific source/target pairs.

### 3.3 AoE shapes and previews

* [ ] AoE shape library:

  * [ ] Sphere/circle
  * [ ] Cone
  * [ ] Line/beam
  * [ ] Box
  * [ ] Ring/donut
  * [ ] Chain / bounce targeting support
* [ ] Placement validation:

  * [ ] Snap to ground
  * [ ] Blocked by LOS/LOF
  * [ ] Max range
  * [ ] Requires target creature/object/point
* [ ] Preview:

  * [ ] Highlight affected tiles/units
  * [ ] Show expected hit chance / save DC preview per target
  * [ ] Show friendly-fire warnings
* [ ] **Testbed:** Add shape “golden geometry” tests that assert target inclusion sets for fixed layouts.

### 3.4 Hitbox / selection

* [ ] Robust selection system:

  * [ ] Hover outline
  * [ ] Click to select target
  * [ ] Cycling targets under cursor
  * [ ] Selection priority (units > interactables > ground)
* [ ] Multi-target selection UI flow.
* [ ] **Testbed:** Provide non-visual selection tests that assert priority ordering for overlapping colliders via ray queries.

---

## 4) Combat UI/UX (must feel like BG3)

> UI tasks must be verifiable without human eyes: by data models, widget presence, bindings, and emitted events.

### 4.1 Core HUD

* [ ] Turn order tracker with:

  * [ ] Portraits/icons, statuses, reaction availability indicator
  * [ ] Round indicator
  * [ ] Hover to inspect
* [ ] Action bar:

  * [ ] Categorized abilities (common, bonus, reactions, items)
  * [ ] Costs displayed
  * [ ] Disabled reasons explained (tooltip)
  * [ ] Cooldowns / limited uses / charges
* [ ] Resource panels: HP, temp HP/barrier, action/bonus/move, reaction, other resources.
* [ ] Combat log:

  * [ ] Rolls with breakdown
  * [ ] State transitions (turn start/end)
  * [ ] Damage/heal events
  * [ ] Status applied/removed
  * [ ] Reaction prompts and outcomes
* [ ] Inspect panel:

  * [ ] Stats, resistances, active effects with durations
  * [ ] Relationship/faction display
* [ ] **Testbed:** Include HUD scene tree in Testbed and validate via automated checks:

  * nodes exist, bindings resolve, and UI receives events from combat log stream.

### 4.2 Input flow

* [ ] Mouse + keyboard support:

  * [ ] Click-to-move with path preview
  * [ ] Confirm/cancel flows
  * [ ] Undo “aiming” stage (not undo executed outcomes)
  * [ ] Hotkeys for actions
* [ ] Controller support hooks (optional but system-ready).
* [ ] **Testbed:** Input flow must be testable through injected commands (not human input), asserting state transitions.

### 4.3 Telemetry & feedback

* [ ] Floating combat text (damage, heal, miss, critical).
* [ ] Hit chance overlays on targets.
* [ ] Ground decals for AoE preview.
* [ ] Reaction “pause and ask” UI.
* [ ] Anim timing locks that still keep UI responsive.
* [ ] **Testbed:** Telemetry is validated by emitted events (e.g., `FloatingTextRequested`) and payload correctness.

---

## 5) Ability system (generic, content-agnostic)

### 5.1 Ability definition model

* [ ] Define an `AbilityDefinition` schema:

  * [ ] Target type (self/unit/point/object)
  * [ ] Range + scaling rules
  * [ ] Costs (action economy + resources)
  * [ ] Requirements (weapon equipped, status present, etc.)
  * [ ] Effects list (damage, heal, apply status, spawn surface, move, summon, etc.)
  * [ ] Save/attack parameters (if any)
  * [ ] Anim/VFX/SFX hooks (by name/id)
  * [ ] AI desirability hints
* [ ] Support ability “variants” and “upcasting” style scaling as generic modifiers.
* [ ] **Testbed:** Include at least a minimal “sample ability pack” solely for systems validation (not real content).

### 5.2 Effect execution pipeline

* [ ] Implement `Effect` framework:

  * [ ] Pre-check: gather targets, validate, compute preview
  * [ ] Execute: apply rules and create events
  * [ ] Post: cleanup, triggers
* [ ] Effects needed (system-level):

  * [ ] DealDamage
  * [ ] Heal
  * [ ] ApplyStatus
  * [ ] RemoveStatus
  * [ ] ModifyResource
  * [ ] Teleport/Relocate
  * [ ] ForcedMove (push/pull)
  * [ ] SpawnSurface/FieldEffect
  * [ ] SummonCombatant
  * [ ] SpawnObject/Prop (combat)
  * [ ] GrantAdvantage/Disadvantage (generic)
  * [ ] SetVisibility/Reveal/Hide (hook)
  * [ ] Interrupt/Counter (for reactions)
* [ ] Ensure every effect can be:

  * [ ] Previewed (expected outcome ranges)
  * [ ] Serialized (save/load)
  * [ ] Logged and replayed
* [ ] **Testbed:** For every new effect type, add:

  * a Scenario using it, and
  * a deterministic test asserting its emitted events and final state delta.

### 5.3 Cooldowns / limited uses / charges

* [ ] Cooldowns with:

  * [ ] Turn-based decrement
  * [ ] Round-based decrement
* [ ] Charges per rest / per combat / per time window (generic).
* [ ] Ability disable reasons and UI messaging.
* [ ] **Testbed:** Scenario asserts cooldown decrement rules and disable reasons serialization.

---

## 6) AI (tactical, reactive, BG3-like)

### 6.1 AI architecture

* [ ] Decision pipeline:

  * [ ] Generate candidate actions
  * [ ] Score candidates (utility)
  * [ ] Simulate outcomes (lightweight)
  * [ ] Choose and commit
* [ ] AI personalities/profiles (aggressive, defensive, support, control) via data.
* [ ] AI memory:

  * [ ] Observed threats
  * [ ] Last known positions (for stealth/fog)
  * [ ] “Preference” to finish downed targets or not (rule flag)
* [ ] **Testbed:** Headless AI scenarios that run N turns and assert:

  * AI produces valid commands,
  * no illegal actions taken,
  * utility scoring returns finite values.

### 6.2 Tactical movement

* [ ] AI pathing with:

  * [ ] Threat evaluation (avoid opportunity zones unless worth it)
  * [ ] High ground preference hook
  * [ ] Cover seeking hook
  * [ ] Distance-to-target optimization
* [ ] Jump/leap usage where beneficial.
* [ ] Shove/push off ledges considerations (generic).
* [ ] **Testbed:** Provide geometry layouts and assert AI chooses among candidate moves (by comparing chosen command to expected set).

### 6.3 Target selection + ability usage

* [ ] Evaluate:

  * [ ] Hit chance
  * [ ] Expected damage/heal
  * [ ] Status value (control, debuff)
  * [ ] Friendly fire cost
  * [ ] Resource conservation (limited uses)
* [ ] Reaction usage policy:

  * [ ] Auto-trigger thresholds
  * [ ] Save reaction for “better moment” logic
* [ ] **Testbed:** Deterministic AI “choice” tests: fixed seed, fixed board, assert chosen action id and target set.

### 6.4 Performance & debugging

* [ ] AI turn time budget and fallback to simpler choices.
* [ ] AI debug overlays: candidate scores, chosen plan, path.
* [ ] **Testbed:** Debug output is validated as structured data (JSON/log events), not visuals.

---

## 7) Camera, presentation, and animation integration

> Presentation tasks must be verifiable by state/events and timeline markers, not by watching animations.

### 7.1 Tactical camera

* [ ] Implement:

  * [ ] Orbit/rotate around focus
  * [ ] Zoom with clamp
  * [ ] Pan with edge scroll toggle
  * [ ] Focus on active unit and on selected target
  * [ ] Cinematic camera cuts for key actions (optional toggles)
* [ ] Keep camera and selection stable during reaction interrupts.
* [ ] **Testbed:** Validate camera controller responds to focus events by asserting camera target state variables (not rendered output).

### 7.2 Animation/VFX/SFX hooks

* [ ] Action timeline system:

  * [ ] Wind-up -> release -> impact -> recovery phases
  * [ ] Events fired at timeline markers (spawn projectile, apply damage)
* [ ] Projectiles system:

  * [ ] Hitscan and projectile travel support
  * [ ] Arc projectiles (throw)
  * [ ] Homing optional hook
* [ ] Impact decals, hit reactions, ragdoll/knockback hooks.
* [ ] **Testbed:** Validate that timeline markers fire and produce correct sequencing of combat events (impact must occur before damage applied, etc.).

---

## 8) Multiplayer/co-op readiness (even if not enabled day 1)

> BG3’s feel benefits from robust turn ownership, prompts, and sync. Even single-player should be built on these abstractions.

* [ ] Authority model: host authoritative simulation.
* [ ] Input replication: commands (Move, UseAbility, EndTurn, ReactionChoice).
* [ ] State replication:

  * [ ] Deterministic log replication OR snapshot replication (choose one)
  * [ ] Desync detection
* [ ] Reaction prompt handling for remote players.
* [ ] Turn timer rules as optional feature.
* [ ] Drop-in/out hooks and reconnect state recovery.
* [ ] **Testbed:** Add a “fake net harness” that feeds remote commands and asserts identical final state hash between host and client simulation.

---

## 9) Persistence (save/load mid-combat)

* [ ] Serialize full combat state:

  * [ ] Initiative order + current turn
  * [ ] Units, stats, resources, cooldowns, charges
  * [ ] Active statuses with remaining duration
  * [ ] Surfaces/field effects with parameters
  * [ ] Spawned objects/props state
  * [ ] RNG seed + roll index
  * [ ] Pending reaction stack and prompts
* [ ] Load validation + migration versioning.
* [ ] Save scumming safe: ensure deterministic replay given same inputs if desired.
* [ ] **Testbed:** Add save/load scenarios that:

  * run N steps, save, reload, continue,
  * assert state hashes match a continuous run.

---

## 10) Encounter orchestration & transitions

* [ ] Combat start detection:

  * [ ] Trigger volumes
  * [ ] Hostility changes
  * [ ] First attack initiates
* [ ] Join-in rules:

  * [ ] Reinforcements entering initiative mid-fight
  * [ ] Late-joining party members
* [ ] Combat end conditions:

  * [ ] All hostiles dead/fled
  * [ ] Objective-based victory/defeat
  * [ ] Surrender/flee hooks
* [ ] Post-combat cleanup:

  * [ ] Remove temporary surfaces if rule requires
  * [ ] Persistent effects cleanup
  * [ ] Loot spawn hooks (system only)
* [ ] **Testbed:** Encounter scripts that assert correct start/end triggers and cleanup outcomes by event logs + state snapshots.

---

## 11) Tooling (editor utilities & content authoring)

### 11.1 Godot editor tools

* [ ] Editor UI to author abilities/effects (Resource inspector helpers).
* [ ] Surface/field-effect volume painter tool.
* [ ] Encounter setup tool:

  * [ ] Spawn points
  * [ ] Factions
  * [ ] Patrol/idle states
  * [ ] Reinforcement waves
* [ ] **Testbed:** Tool outputs (resources/scenarios) must be loadable by Testbed and validated by registry checks.

### 11.2 Debug tools

* [ ] Combat sandbox controls:

  * [ ] Spawn any unit template
  * [ ] Apply any status
  * [ ] Spawn any surface
  * [ ] Force initiative order
* [ ] Roll inspector and breakdown window.
* [ ] Event timeline viewer (what happened this turn).
* [ ] **Testbed:** Debug tools must expose structured outputs/events that tests can assert (e.g., “AppliedStatus: X”).

---

## 12) Testing & validation (non-negotiable for “comprehensive”)

### 12.1 Unit tests (C#)

* [ ] Rules engine tests:

  * [ ] Hit chance math
  * [ ] Advantage/disadvantage stacking
  * [ ] Save/DC correctness
  * [ ] Damage mitigation ordering
  * [ ] Status duration tick correctness
* [ ] Reaction ordering tests:

  * [ ] Multiple eligible reactors
  * [ ] Interrupt cancellation correctness
* [ ] Surface interaction tests:

  * [ ] Transform rules and triggers

### 12.2 Simulation tests

* [ ] Deterministic combat simulation runner (headless):

  * [ ] Run 10k combats with random seeds
  * [ ] Detect NaNs, invalid states, infinite loops
* [ ] Property-based tests for invariants:

  * [ ] HP never negative unless allowed
  * [ ] Resources never exceed max unless allowed
  * [ ] No duplicate status IDs where unique enforced
* [ ] **Testbed:** Every new subsystem adds at least one simulation invariant test and one scenario regression test.

### 12.3 UX tests checklist (non-visual assertions)

* [ ] Every disabled action includes a non-empty “reason code” + human-readable string.
* [ ] Every roll emits a breakdown payload with contributors.
* [ ] Every interrupt exposes a cancellable prompt state before commit.
* [ ] No “soft locks”: state machine guarantees an escape/cancel transition from targeting/prompt states.
* [ ] **Testbed:** A headless “UI binding test” confirms UI subscribes to the expected streams (no missing signals/services).

---

## 13) Performance targets

* [ ] Profiling harness for:

  * [ ] LOS queries at scale
  * [ ] AoE target collection
  * [ ] AI evaluation
  * [ ] Surface tick/update
* [ ] Spatial partitioning for queries (grid, BVH, Godot physics layers, etc.).
* [ ] Turn resolution should be smooth at:

  * [ ] 20+ units
  * [ ] Multiple overlapping surfaces
  * [ ] Heavy reaction usage
* [ ] **Testbed:** Add benchmark scenarios that output timings and fail if regression exceeds thresholds (CI-friendly, no visuals).

---

## 14) Implementation order (agents can parallelize)

### Phase A — Skeleton (must compile and run headless)

* [ ] Combat state machine + turn queue
* [ ] Combatant model + resources
* [ ] Basic move + end turn
* [ ] Minimal UI model: turn tracker data + end turn command (UI nodes optional)
* [ ] **Testbed:** Loads a minimal scenario and prints deterministic event log + final state hash.

### Phase B — Rules engine + generic abilities

* [ ] Rule queries/modifiers/events
* [ ] Ability definitions + effect pipeline
* [ ] Targeting + AoE preview (geometry + validation)
* [ ] Damage/heal/status basics
* [ ] **Testbed:** Adds scenario pack that exercises each effect type and asserts event sequences.

### Phase C — Hallmark depth

* [ ] Full action economy (action/bonus/move/reaction)
* [ ] Reactions/interrupt stack + prompts
* [ ] Surfaces/field effects + environment interactions
* [ ] LOS/cover/height hooks
* [ ] Jump/climb/fall/forced movement
* [ ] **Testbed:** Adds regression suite for hallmark interactions (reaction during move, surface transform + status tick, etc.)

### Phase D — AI parity and polish

* [ ] Tactical AI with scoring + reaction policy
* [ ] Full HUD data model, combat log, breakdown payloads
* [ ] Anim timeline integration hooks + camera state machine hooks
* [ ] **Testbed:** Headless AI runs + deterministic choice tests.

### Phase E — Persistence + tooling + hardening

* [ ] Save/load mid-combat
* [ ] Editor tools and sandbox resources
* [ ] Automated tests + simulation runner
* [ ] Performance pass + profiling
* [ ] **Testbed:** Save/load regression + benchmarks wired into CI.

---

## 15) “BG3 parity” acceptance criteria (system-level)

> The system is considered “BG3-like” when ALL are true (validated by tests/logs/state hashes, not visuals):

* [ ] Turn-based initiative with a clear queue and smooth transitions (state transition logs match expected).
* [ ] Clear action economy: action + bonus + movement + reaction (or configurable equivalent) with correct budget accounting.
* [ ] Robust targeting with AoE previews, LOS checks, and valid placement constraints (geometry tests pass).
* [ ] Deep reactions/interrupts: prompts, ordering, cancellation/modification of events (reaction ordering tests pass).
* [ ] Rich status system: stacking, durations, triggers, restrictions, cleanses (status regression suite passes).
* [ ] Surface/field effects that interact and transform, affecting movement and turn triggers (surface transform suite passes).
* [ ] Verticality: jump/fall/height influences and forced movement consequences (movement validation suite passes).
* [ ] Transparent math: every roll/query can emit breakdown data (breakdown payload tests pass).
* [ ] Tactical AI uses positioning, resources, and reactions competently (deterministic AI scenarios meet expected choices).
* [ ] Full persistence mid-combat and deterministic replay/debug logging (save/load + replay equivalence tests pass).
* [ ] Tooling to author abilities/effects and debug subsystems (registry validation + load tests pass).

---

## 16) Glossary (shared terms for agents)

* **Command**: Player/AI intention (Move, UseAbility, EndTurn, ReactionChoice).
* **Event**: Atomic resolved fact (DamageApplied, StatusAdded, MoveCommitted).
* **Effect**: Executable rule chunk (DealDamage, ApplyStatus, SpawnSurface).
* **Modifier**: Changes a query (hit chance, DC, damage) with priority and conditions.
* **Resolution Stack**: Nested execution chain that supports interrupts/reactions.

---

### One-line rule for agents

**If you add or change a feature, you must also update `Testbed.tscn` wiring and add a non-visual verification (test/log/assertion) proving it works.**
