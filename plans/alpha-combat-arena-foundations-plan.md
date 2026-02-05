# Plan: Alpha Combat Arena Foundations (BG3/DOS2-Style Vertical Slice)

**Created:** 2026-02-04  
**Status:** Ready for Atlas Execution

## Summary

Deliver a playable, reliable combat vertical slice in `CombatArena.tscn` with: per-meter (non-grid) movement + preview, 3 core abilities (melee, ranged, heal) with clear range/AoE visualization, strict turn-based control (only active unit acts), basic enemy AI using the same actions, and camera centering on the active unit. Prioritize correctness + clarity over feature breadth; remove/disable misleading “half-built” systems that break the core loop.

## Context & Analysis

### Relevant Files
- [Combat/Arena/CombatArena.tscn](Combat/Arena/CombatArena.tscn): Arena scene; currently has ground mesh but (likely) missing collision and a node-name mismatch for the input handler.
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): Main arena coordinator; currently hardcodes combatants and abilities, converts positions via `TileSize`, doesn’t center camera on turn change.
- [Combat/Arena/CombatInputHandler.cs](Combat/Arena/CombatInputHandler.cs): Mouse/keyboard input; only gates on `Arena.IsPlayerTurn` (not “active combatant”), depends on physics raycasts that may never hit.
- [Combat/Arena/CombatHUD.cs](Combat/Arena/CombatHUD.cs): HUD wiring; subscribes to models too late (misses initial signals); resource IDs don’t match arena model IDs.
- [Combat/Services/TurnQueueService.cs](Combat/Services/TurnQueueService.cs): Turn order + current combatant.
- [Combat/Services/CommandService.cs](Combat/Services/CommandService.cs): Already contains correct validation for “only current combatant can act”; currently bypassed by arena move/ability paths.
- [Combat/Movement/MovementService.cs](Combat/Movement/MovementService.cs): Movement cost + `GetPathPreview` (linear waypoint sampling); no navmesh.
- [Combat/Arena/MovementPreview.cs](Combat/Arena/MovementPreview.cs): Renders only a single line segment; ignores waypoints.
- [Combat/Targeting/TargetValidator.cs](Combat/Targeting/TargetValidator.cs): Range/AoE math uses `Combatant.Position`.
- [Combat/Arena/RangeIndicator.cs](Combat/Arena/RangeIndicator.cs), [Combat/Arena/AoEIndicator.cs](Combat/Arena/AoEIndicator.cs): World-space indicators.
- [Data/Scenarios/minimal_combat.json](Data/Scenarios/minimal_combat.json): Scenario file exists but is not actually used by default.
- [Data/Abilities/sample_abilities.json](Data/Abilities/sample_abilities.json): Has melee + heal + AoE circle; **missing a basic ranged single-target ability**.
- [Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs): Utility AI; currently integrated as “one action then end turn”; movement actions may be ignored in the arena integration.

### Key Observations (from research)
- **Scenario path is ignored:** `CombatArena._Ready()` calls `SetupDefaultCombat()` (hardcoded) instead of loading `ScenarioPath`.
- **Coordinate system is inconsistent:** `Combatant.Position` is treated as “grid”, then converted to world using `TileSize`; indicators are drawn in world but given unscaled radii; input handler mixes conversions; requirement wants **per-meter non-grid**.
- **Control gating is insufficient:** Input checks only `IsPlayerTurn` so the player can select/control non-active units during a player turn.
- **Raycasts likely don’t hit ground:** Arena floor is a `MeshInstance3D` without collision; ground raycasts use collision mask 1.
- **HUD model drift:** Resource IDs (`bonus` vs `bonus_action`, missing `health`) and missed initial model events can leave HUD disabled/out of sync.

### Design Decision (recommended)
Adopt **world-space meters as the single coordinate system**:
- 1 Godot unit = 1 meter
- `Combatant.Position` is world position
- Ability ranges/radii in JSON are meters
- Remove or neutralize `TileSize` usage (set to 1, then delete once stable)

This best matches the user’s requirement (“non-grid per meter system”) and reduces conversion bugs.

## Implementation Phases

### Phase 1: Make `CombatArena` load reliably + fix scene wiring

**Objective:** The arena loads `ScenarioPath`, spawns combatants with consistent coordinates, and input raycasts work.

**Files to Modify/Create**
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): Replace hardcoded setup as default.
- [Combat/Arena/CombatArena.tscn](Combat/Arena/CombatArena.tscn): Add ground collision OR adjust code to avoid physics dependency.
- (Optional) [Data/Scenarios/minimal_combat.json](Data/Scenarios/minimal_combat.json): Ensure positions make sense in meters.

**Steps**
1. Change `CombatArena._Ready()` flow:
   - Try `LoadScenario(ScenarioPath)` first.
   - If load fails, fallback to `SetupDefaultCombat()` but log prominently.
2. Fix input handler node lookup resilience:
   - `GetNodeOrNull("CombatInputHandler")` OR `GetNodeOrNull("InputHandler")`.
3. Fix coordinate conversion:
   - Make `CombatantPositionToWorld(pos)` return `pos` (include Y) or set `TileSize = 1` and keep identity.
4. Ensure ground ray hits:
   - Preferred: add `StaticBody3D` + `CollisionShape3D` plane under floor on collision layer 1.
   - Alternative: implement analytic ray-plane intersection for ground selection (no physics dependency).

**Acceptance Criteria**
- [ ] Arena boots headless without exceptions.
- [ ] Scenario combatants spawn from JSON by default.
- [ ] Clicking ground returns a valid target position.

---

### Phase 2: Enforce “only active combatant is controllable”

**Objective:** During a player turn, the player can only move/use abilities with the currently active combatant.

**Files to Modify/Create**
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs)
- [Combat/Arena/CombatInputHandler.cs](Combat/Arena/CombatInputHandler.cs)
- [Combat/Services/CommandService.cs](Combat/Services/CommandService.cs) (if adding new commands)

**Tests to Write**
- New unit test(s) under `Tests/Unit` validating the gating helper without Godot.

**Steps**
1. Add a single source of truth helper in `CombatArena`:
   - `string ActiveCombatantId => _turnQueue.CurrentCombatant?.Id`.
   - `bool CanPlayerControl(string combatantId)` checks:
     - `_isPlayerTurn == true`
     - `combatantId == ActiveCombatantId`
     - state machine is `PlayerDecision`.
2. Guard these `CombatArena` entrypoints:
   - `SelectCombatant`, `ExecuteMovement`, `ExecuteAbility`, `SelectAbility`.
   - Reject/ignore if actorId != active.
3. In `BeginTurn`, if it’s player turn: auto-select the active combatant and clear any stale move/ability mode.
4. In input handler, on turn change/end turn: force exit move mode and clear selection.

**Acceptance Criteria**
- [ ] Clicking another ally/enemy during your turn does not change the acting unit.
- [ ] Movement/abilities executed always use the active combatant.

---

### Phase 3: Movement MVP (per-meter) + clear visualization

**Objective:** Player can preview and execute movement within movement budget, with a visually clear path and cost.

**Files to Modify/Create**
- [Combat/Arena/MovementPreview.cs](Combat/Arena/MovementPreview.cs)
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs)
- [Combat/Movement/MovementService.cs](Combat/Movement/MovementService.cs) (optional improvements)

**Steps**
1. Update `MovementPreview.Update(...)` signature to accept a `PathPreview` (or waypoint list) and draw a polyline.
2. In `CombatArena.UpdateMovementPreview`, call `MovementService.GetPathPreview` and pass the full waypoint list to `MovementPreview`.
3. Add a simple “max move” ring (reuse `RangeIndicator`) while in move mode.
4. Ensure executed movement consumes `ActionBudget.RemainingMovement` and HUD updates accordingly.

**Acceptance Criteria**
- [ ] Preview line follows multiple waypoint points (not just one segment).
- [ ] Cost label matches movement consumed after execution.

---

### Phase 4: Abilities MVP (Melee, Ranged, Heal) + targeting visuals

**Objective:** Player can select melee/ranged/heal from HUD, see valid targets + range/AoE shapes, and execute with action point costs.

**Files to Modify/Create**
- [Data/Abilities/sample_abilities.json](Data/Abilities/sample_abilities.json): Add `ranged_attack` (single target, enemies, e.g. range 10–15m) and (optional) a cone ability if needed.
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): Remove/disable `RegisterDefaultAbilities()` overwriting JSON definitions.
- [Combat/Arena/CombatInputHandler.cs](Combat/Arena/CombatInputHandler.cs): Make AoE click semantics placement-based and consistent.
- [Combat/Arena/CombatHUD.cs](Combat/Arena/CombatHUD.cs): Fix missed initial state + resource IDs.

**Steps**
1. Make `DataRegistry` the **only** source of ability definitions used by `EffectPipeline`.
   - Remove the hardcoded `RegisterDefaultAbilities()` call or gate it behind a debug flag that is off by default.
2. Fix indicator scaling by removing grid/world mismatch (Phase 1 decision):
   - Use world-space positions and world-space radii/lengths consistently.
3. Fix AoE execution:
   - On click, execute ability as an AoE at the chosen center and apply effects to all affected combatants (not “first affected target”).
4. Fix HUD reliability:
   - After subscribing in `CombatHUD.DeferredInit`, immediately pull current model state (render actions/turn order/resources).
   - Unify resource IDs (`bonus` vs `bonus_action`, add/update `health`).

**Acceptance Criteria**
- [ ] Player can use melee attack (short range), ranged attack (longer range), and heal (ally/self) consuming `Action`.
- [ ] Range ring and AoE indicator match actual validation.
- [ ] Ability buttons enable/disable correctly by turn + action availability.

---

### Phase 5: Turn loop + enemy AI actions

**Objective:** Initiative order works; only active unit acts; AI uses the same core actions and ends turn.

**Files to Modify/Create**
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs)
- [Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs) (or adapter layer in arena)

**Steps**
1. Ensure `BeginTurn` resets budgets from `ActionBudget` values and HUD reflects real values (no hardcoded `move = 30`).
2. Refactor AI turn integration:
   - Support at least: if out of range → move closer; if in range → use `basic_attack`/`ranged_attack`; if low HP and has heal → heal.
   - Allow “move then attack” in a single turn when resources allow.
3. Keep AI deterministic for tests by using scenario seed RNG.

**Acceptance Criteria**
- [ ] Enemies take turns and perform at least one meaningful action.
- [ ] AI respects the same action/movement budgets.

---

### Phase 6: Camera centering on active combatant

**Objective:** On turn change, camera recenters to the active unit.

**Files to Modify/Create**
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs)

**Steps**
1. Implement `CenterCameraOnCombatant(Combatant)` called from `BeginTurn`.
   - Simple offset + `LookAt` is sufficient for alpha.
2. Optionally, integrate with `CameraStateHooks.FollowCombatant(activeId)` if the hook is already meaningful.

**Acceptance Criteria**
- [ ] When the active combatant changes, camera snaps/smoothly moves to them.

---

### Phase 7: Verification, tests, and build gates

**Objective:** CI scripts pass and gameplay loop is verified.

**Steps**
1. Add/extend integration tests in `Tests/Integration`:
   - Load `CombatArena` (or underlying services) and validate:
     - scenario spawn count
     - turn advance
     - gating (cannot act as non-active)
2. Run required build gates:
   - `scripts/ci-build.sh`
   - `scripts/ci-test.sh`
3. Update docs for controls and expected alpha workflow:
   - Add a short section in `docs/` describing key inputs (move, ability hotkeys, end turn) and scenario file location.

**Acceptance Criteria**
- [ ] `scripts/ci-build.sh` passes.
- [ ] `scripts/ci-test.sh` passes.
- [ ] A human can play a full 2v2 fight in `CombatArena.tscn` with clear visuals.

## Open Questions

1. **Do we keep any grid concepts at all?**
   - **Option A:** Fully world-space meters (recommended).
   - **Option B:** Keep grid internally and scale everything by `TileSize`.
   - **Recommendation:** Option A to satisfy “non-grid per meter” and eliminate conversion bugs.

2. **How should AoE targeting work on click?**
   - **Option A:** Place at clicked point, apply to all inside shape (recommended).
   - **Option B:** Click a target unit; center on unit.

3. **“Enable GPT-5.2-Codex for all clients”**
   - This is not a repo change and likely requires platform-level configuration outside this codebase. Atlas can’t implement this in-game; treat as an external ops setting.

## Risks & Mitigation

- **Risk:** Many systems exist; changing coordinate conventions could break hidden assumptions.
  - **Mitigation:** Keep changes localized (arena + input + validator). Add tests around range checks and movement cost.
- **Risk:** Godot physics raycasts still flaky in headless.
  - **Mitigation:** Use analytic ray-plane intersection for ground selection; keep physics only for combatant selection.
- **Risk:** HUD remains out-of-sync.
  - **Mitigation:** Make models the single source of truth; “pull current state” immediately after subscribing.

## Success Criteria

- [ ] `CombatArena.tscn` is the single playable alpha scenario.
- [ ] Per-meter movement preview + execution works with clear visualization.
- [ ] Melee, ranged, heal abilities work with clear range/AoE visuals and action costs.
- [ ] Turn order works; only the active unit is controllable.
- [ ] Enemies act via AI using the same systems.
- [ ] Camera recenters on active combatant.
- [ ] `scripts/ci-build.sh` and `scripts/ci-test.sh` pass.

## Notes for Atlas

- Work in a worktree: `.worktrees/Atlas/alpha-combat-arena` and branch `agent/Atlas/alpha-combat-arena`.
- Prefer small, reversible commits per phase.
- Treat `CombatArena.cs` as the integration nexus; trim or disable misleading code paths (e.g., hardcoded abilities/combatants) once scenario-driven flow works.
