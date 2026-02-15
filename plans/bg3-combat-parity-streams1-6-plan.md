# Plan: BG3 Combat Parity (Streams 1–6)

**Created:** 2026-02-15
**Status:** Ready for Atlas Execution

## Summary

This plan turns the current “playable approximation” into a verifiable BG3-parity combat loop by hardening validation gates, making character builds reproducible and BG3-sourced where possible, integrating the existing forced-movement service into real action execution, wiring toggleable passives end-to-end (data → runtime state → HUD), and tightening AoE targeting + action coverage.

The work is organized into Streams 1–6 (character builds, forced movement, toggles, missing mechanics, AoE UX, coverage), but sequenced by dependency so we get fast feedback from CI and headless runs before gameplay changes.

## Context & Analysis

**Relevant Files (high-signal touchpoints):**
- Combat/Arena/CombatArena.cs: service wiring, action registry init, action bar population, action selection/execution.
- Data/ScenarioLoader.cs: scenario schema + spawn pipeline; merges resolved actions/passives/equipment.
- Data/ScenarioGenerator.cs: random unit generation for autobattle; currently not BG3-exact.
- Data/CharacterModel/CharacterResolver.cs: class/race/feat resolution; grants abilities + extra attacks.
- Combat/Actions/EffectPipeline.cs: creates EffectContext; uses ActionRegistry fallback; CanUseAbility.
- Combat/Actions/Effects/Effect.cs: effect implementations; currently has simple forced_move that bypasses ForcedMovementService.
- Combat/Movement/ForcedMovementService.cs: correct forced-movement implementation (collision, surfaces, fall damage) but not used by forced_move effect.
- Combat/Passives/PassiveManager.cs: grants/removes passives + boosts; currently no toggle state.
- Data/Passives/BG3PassiveData.cs + Data/Parsers/BG3PassiveParser.cs: toggle metadata (ToggleOn/OffFunctors, ToggleGroup, IsToggled).
- Combat/UI/HudController.cs + Combat/UI/ActionBarModel.cs + Combat/UI/Panels/ActionBarPanel.cs: action bar UI + variant popup.
- Combat/Arena/CombatInputHandler.cs: click-to-execute targeting for single target, AoE, and targetless actions.
- Tests/Helpers/ParityDataValidator.cs: current mandatory parity validation gate (CI).
- Data/Validation/ParityValidator.cs: richer registry-based validator (already exists; currently not the CI gate).
- Data/Actions/common_actions.json + Data/Actions/bg3_mechanics_actions.json: overlapping definitions (e.g. shove appears twice).

**Key Observations (parity blockers):**
- Forced movement exists as a robust service but forced_move effect currently teleports targets with no collision/height/fall handling.
- Toggleable passive metadata exists and is parsed, but there is no runtime toggle-state model, no execution of toggle functors, and no HUD affordance.
- Shove’s behavior is inconsistent across data packs (duplicate IDs) and currently does not consistently use forced movement + contested checks.
- CI parity gate validates CharacterModel granted abilities against Data/Actions only; it does not consider BG3 ActionRegistry spell IDs, which is essential for “exact BG3 replicas”.

**Build/Test Gates (must run in every phase):**
- scripts/ci-build.sh
- scripts/ci-test.sh
- scripts/run_headless_tests.sh (for services/registries/scenarios without rendering)

## Implementation Phases

### Phase 1: Expand Parity Gates (Stream 6 foundation)

**Objective:** Make missing/invalid action IDs fail loudly in CI, using the union of DataRegistry and BG3 ActionRegistry.

**Task 1.1 — Union action IDs in CI parity gate**
- **Files to Modify:**
  - Tests/Helpers/ParityDataValidator.cs
- **Tests to Write/Update:**
  - Tests/Unit/ParityValidationTests.cs (existing) should start catching BG3-spell grant issues deterministically.
- **Steps:**
  1. Extend ParityDataValidator to initialize Combat/Actions/ActionRegistry via Data/Actions/ActionRegistryInitializer.Initialize using BG3_Data.
  2. Build `knownActionIds = Data/Actions IDs ∪ BG3 ActionRegistry IDs`.
  3. Update `ValidateMissingGrantedAbilities(...)` to validate against `knownActionIds`.
  4. Ensure error messages still include source file + granting feature/race/class.
- **Acceptance Criteria:**
  - [ ] CI parity gate fails when a class/feat/race grants an ID not in either registry.
  - [ ] CI parity gate no longer flags valid BG3 spell IDs as “missing” just because they are not in Data/Actions JSON.

**Task 1.2 — Add an explicit “No summons” validation (repo constraint)**
- **Files to Modify:**
  - Tests/Helpers/ParityDataValidator.cs
  - Data/Validation/parity_allowlist.json (only if a temporary allowlist is required during transition)
- **Steps:**
  1. Define “summoning spell” as any action whose Effects contain type `summon`.
  2. Load a minimal BG3 ActionRegistry and scan for summon-type effects in actions that are referenced by scenarios or CharacterModel-granted abilities.
  3. Fail CI if any such action is granted/used in canonical scenario packs.
- **Acceptance Criteria:**
  - [ ] Parity gate fails if a scenario/unit grants an action that summons.
  - [ ] Gate output lists the exact action ID(s) and source scenario/unit.

---

### Phase 2: BG3-Exact Character Build Inputs (Stream 1)

**Objective:** Allow scenarios to request BG3 stat blocks as the starting point, so “exact BG3 replicas” are achievable without manual duplication of base stats/passives.

**Task 2.1 — Add BG3 character template reference to ScenarioUnit**
- **Files to Modify:**
  - Data/ScenarioLoader.cs
- **Tests to Write:**
  - Tests/Integration (new): `ScenarioLoader_BG3Template_AppliesBaseStats`
- **Steps:**
  1. Add a new optional field to `ScenarioUnit`: `BG3CharacterTemplateId` (exact entry name from BG3_Data/Stats/Character.txt).
  2. Add schema-compatible JSON alias (e.g., `bg3Template`) so old scenarios remain valid.
  3. Ensure deserialization does not break existing scenario files.
- **Acceptance Criteria:**
  - [ ] Scenario JSON can specify a BG3 character template id without breaking old schemas.

**Task 2.2 — Wire StatsRegistry into ScenarioLoader**
- **Files to Modify:**
  - Data/ScenarioLoader.cs
  - Combat/Arena/CombatArena.cs
- **Tests to Write:**
  - Tests/Integration (new): `CombatArena_LoadScenario_UsesBG3TemplateStats`
- **Steps:**
  1. Add `SetStatsRegistry(StatsRegistry registry)` to ScenarioLoader.
  2. In CombatArena service registration, after StatsRegistry loads, call `ScenarioLoader.SetStatsRegistry(_statsRegistry)`.
  3. In ScenarioLoader.SpawnCombatants, if `BG3CharacterTemplateId` is set:
     - Pull BG3CharacterData.
     - Apply base ability scores, base HP, initiative bonus (or initiative), and passive list (append scenario overrides).
     - Do not override explicit scenario equipment/known actions; those remain the “replica definition”.
- **Acceptance Criteria:**
  - [ ] A scenario using BG3 templates spawns combatants with the template’s STR/DEX/CON/INT/WIS/CHA.
  - [ ] Passives from the BG3 template are present on the combatant (and show up in PassiveManager.ActivePassiveIds after CombatArena grants them).

**Task 2.3 — Deterministic “replica build” scenario pack**
- **Files to Create/Modify:**
  - Data/Scenarios (new JSON scenario(s); keep each scenario minimal)
- **Tests to Write:**
  - Headless: extend Tools/HeadlessTestRunner.cs OR add a new xUnit integration test that loads the scenario and asserts key build invariants.
- **Steps:**
  1. Add one canonical scenario with 2–4 units that are explicitly-defined “BG3 replicas” (explicit KnownActions, equipment, class levels, feats).
  2. Ensure no unit grants summon actions.
  3. Use this scenario as the first end-to-end parity target for Streams 2–5.
- **Acceptance Criteria:**
  - [ ] Scenario loads headless.
  - [ ] Spawned combatants have deterministic action lists and equipment.

---

### Phase 3: Forced Movement Uses the Correct Service (Stream 2)

**Objective:** Make forced movement in actions (e.g., shove, thunderwave) use ForcedMovementService so it matches BG3 expectations: collision blocking, surface triggers, fall damage.

**Task 3.1 — Register ForcedMovementService in CombatArena and keep it updated**
- **Files to Modify:**
  - Combat/Arena/CombatArena.cs
- **Tests to Write:**
  - Tests/Unit/ForcedMovementTests.cs (extend)
  - Tests/Integration (new): `ExecuteAction_ForcedMove_UsesService`
- **Steps:**
  1. Instantiate ForcedMovementService in RegisterServices, passing RuleEventBus + SurfaceManager + HeightService.
  2. Register it in CombatContext.
  3. When combatants are spawned/registered, call ForcedMovementService.RegisterCombatant(combatant).
- **Acceptance Criteria:**
  - [ ] Service exists in context and contains all combatants.

**Task 3.2 — Add ForcedMovementService to EffectContext and wire in EffectPipeline**
- **Files to Modify:**
  - Combat/Actions/Effects/Effect.cs (EffectContext)
  - Combat/Actions/EffectPipeline.cs
- **Tests to Write:**
  - Tests/Integration (new): asserts EffectContext.ForcedMovement is non-null during execution when arena is fully wired.
- **Steps:**
  1. Add `public Combat.Movement.ForcedMovementService ForcedMovement { get; set; }` to EffectContext.
  2. When EffectPipeline builds EffectContext, set it from the service in CombatContext (or from a pipeline property set by CombatArena).
- **Acceptance Criteria:**
  - [ ] ForcedMoveEffect can access ForcedMovementService via context.

**Task 3.3 — Refactor forced_move effect to delegate to ForcedMovementService**
- **Files to Modify:**
  - Combat/Actions/Effects/Effect.cs
- **Tests to Write:**
  - Tests/Unit/ForcedMovementTests.cs: add a test that validates collision blocking when forced_move is executed.
- **Steps:**
  1. In ForcedMoveEffect.Execute, if `context.ForcedMovement != null`, call Push/Pull based on parameters.
  2. Keep existing fallback logic only for non-arena unit tests.
  3. Ensure surfaces enter/leave are emitted once (avoid double-processing if service already handles them).
- **Acceptance Criteria:**
  - [ ] Forced movement no longer ignores collisions/height when running in CombatArena.

---

### Phase 4: Toggleable Passives End-to-End (Stream 3)

**Objective:** Toggleable BG3 passives (Non-Lethal Attacks, GWM/Sharpshooter-style toggles, etc.) appear in the action bar, persist per-combatant, execute ToggleOn/OffFunctors, and update combat behavior.

**Task 4.1 — Add toggle-state tracking to PassiveManager**
- **Files to Modify:**
  - Combat/Passives/PassiveManager.cs
- **Tests to Write:**
  - Tests/Unit (new): `PassiveManager_ToggleablePassives_DefaultAndFlipState`
- **Steps:**
  1. Store a per-passive toggle state map (only for passives where `BG3PassiveData.IsToggleable`).
  2. Add API: `GetToggleState(passiveId)` and `SetToggleState(passiveId, bool enabled)`.
  3. Handle ToggleGroup mutual exclusivity (enabling one disables others in the same group).
- **Acceptance Criteria:**
  - [ ] Toggle state is tracked independently from “passive is granted”.

**Task 4.2 — Execute ToggleOn/OffFunctors when toggled**
- **Files to Modify:**
  - Combat/Arena/CombatArena.cs
- **Tests to Write:**
  - Tests/Integration (new): `TogglePassive_ApplyStatusFunctor_AddsStatus`
- **Steps:**
  1. Store a FunctorExecutor instance on CombatArena (it is already constructed during service wiring).
  2. Add an arena method: `TogglePassive(string combatantId, string passiveId)`.
  3. Look up BG3PassiveData via PassiveRegistry, parse ToggleOn/OffFunctors via FunctorParser, execute via FunctorExecutor with source=target=combatantId.
- **Acceptance Criteria:**
  - [ ] Toggling a passive on applies its ToggleOnFunctors; toggling off applies ToggleOffFunctors.

**Task 4.3 — Expose toggle passives in the action bar model**
- **Files to Modify:**
  - Combat/UI/ActionBarModel.cs
  - Combat/UI/Panels/ActionBarPanel.cs
- **Tests to Write:**
  - Tests/Unit/HUDModelTests.cs (unskip if feasible; otherwise add a headless HUD smoke test)
- **Steps:**
  1. Extend ActionBarEntry with fields for `IsToggle`, `IsToggledOn`, and optional `ToggleGroup`.
  2. Update ActionBarPanel rendering:
     - show “on” state via the existing highlight styling (no new theme tokens).
     - keep disabled visuals if usability is not Available.
- **Acceptance Criteria:**
  - [ ] The action bar can display a toggle entry distinctly as on/off.

**Task 4.4 — Populate toggle entries and handle clicks**
- **Files to Modify:**
  - Combat/Arena/CombatArena.cs
  - Combat/UI/HudController.cs
- **Tests to Write:**
  - Tools/AutoBattler/UIAwareAIController.cs (optional) — add a guard so AI doesn’t spam toggles unless explicitly desired.
- **Steps:**
  1. In CombatArena.PopulateActionBar, append entries for toggleable passives owned by the active combatant.
     - Use ActionId convention `passive:<PassiveId>` (so it can’t collide with action IDs).
     - Fill toggle-state fields for UI.
  2. In HudController.OnActionPressed:
     - If ActionId starts with `passive:`, call Arena.TogglePassive and re-sync actions (ActionsChanged will refresh).
     - Otherwise follow the existing action selection/variant flow.
- **Acceptance Criteria:**
  - [ ] Clicking a toggle passive flips its state and immediately updates UI.
  - [ ] No targeting mode is entered for passive toggles.

**Open Question (needs a decision early):** Default toggle state for BG3 passives is not encoded explicitly in BG3PassiveData. Decide whether toggles default to off, on, or “based on presence of a status granted by ToggleOnFunctors”. Recommendation: default off unless a template/scenario explicitly enables it.

---

### Phase 5: Shove Parity (Stream 4 + Stream 2 linkage)

**Objective:** Make shove behavior consistent, BG3-aligned, and remove duplicate/competing data definitions.

**Task 5.1 — Remove/resolve duplicate `shove` action definition**
- **Files to Modify:**
  - Data/Actions/common_actions.json OR Data/Actions/bg3_mechanics_actions.json (keep only one canonical shove)
  - Data/Validation/parity_allowlist.json (if it currently allowlists duplicates)
- **Tests to Write:**
  - Existing parity gate should fail if duplicate IDs exist (unless intentionally allowlisted).
- **Steps:**
  1. Choose canonical shove definition and delete/rename the other to avoid duplicate ID collisions.
  2. Ensure shove uses `forced_move` (not placeholder damage=0 + push_distance parameters).
- **Acceptance Criteria:**
  - [ ] Only one shove action ID remains.

**Task 5.2 — Add shove variants (push vs prone) using ActionVariant**
- **Files to Modify:**
  - Data/Actions/bg3_mechanics_actions.json
- **Tests to Write:**
  - Tests/Unit/ActionVariantTests.cs (extend) to ensure variant selection routes through HUD popup.
- **Steps:**
  1. Add 2 variants to shove:
     - Push: forced_move only.
     - Prone: forced_move + apply_status prone (or vice versa).
  2. Ensure HUD variant popup appears (already implemented).
- **Acceptance Criteria:**
  - [ ] Shove prompts for a variant in the HUD.

**Task 5.3 — Implement contested check for shove (optional if BG3 behavior differs)**
- **Files to Modify (pick one implementation path):**
  - Option A (effect-based): Combat/Actions/Effects/Effect.cs + Data/Actions/bg3_mechanics_actions.json
  - Option B (rules-based): Combat/Rules/RulesEngine.cs + Combat/Actions/EffectPipeline.cs
- **Tests to Write:**
  - Tests/Integration: shove success/fail is deterministic for a fixed seed.
- **Steps:**
  1. Implement Athletics vs (Athletics|Acrobatics) resolution.
  2. Feed the result into forced movement execution.
- **Acceptance Criteria:**
  - [ ] Shove success probability reacts to STR/DEX skill changes.

---

### Phase 6: AoE Targeting UX Tightening (Stream 5)

**Objective:** Ensure AoE previews match legality and don’t mislead the player (range/LoS), while keeping existing UX rules (targetless actions are primed, not auto-fired).

**Task 6.1 — Add range/legality gating to AoE preview**
- **Files to Modify:**
  - Combat/Arena/CombatArena.cs
  - Combat/Targeting/TargetValidator.cs
- **Tests to Write:**
  - Tests/Unit targeting tests if present; otherwise add a small integration test for ResolveAreaTargets + range.
- **Steps:**
  1. When selected action is AoE, validate cursor position is within range of actor.
  2. If out of range, show indicator as invalid (reuse existing “friendly fire” flag styling; no new theme tokens) and avoid highlighting targets.
- **Acceptance Criteria:**
  - [ ] AoE preview never highlights targets when cast point is illegal.

**Task 6.2 — Screenshot regression for AoE preview (optional but recommended)**
- **Files to Modify:**
  - docs/automation-visual-tests.md (if documentation update needed)
- **Steps:**
  1. Add a deterministic scenario that selects an AoE action.
  2. Capture baseline via scripts/run_screenshots.sh.
  3. Add comparison via scripts/compare_screenshots.sh.
- **Acceptance Criteria:**
  - [ ] AoE indicator regressions are caught by screenshot diff.

---

### Phase 7: Action Coverage & Denylists (Stream 6 completion)

**Objective:** Ensure action availability matches the parity target set, and prohibited actions (summons) never appear in HUD/AI.

**Task 7.1 — Filter summon actions from PopulateActionBar and AI candidate lists**
- **Files to Modify:**
  - Combat/Arena/CombatArena.cs
  - Tools/AutoBattler/UIAwareAIController.cs
- **Tests to Write:**
  - Integration: “no summon actions in action bar” for the canonical replica scenario.
- **Steps:**
  1. Add helper `IsSummonAction(ActionDefinition def)` (checks Effects contain `summon`).
  2. Filter it out of `GetActionsForCombatant` and `PopulateActionBar`.
  3. Ensure AI doesn’t attempt to select these actions.
- **Acceptance Criteria:**
  - [ ] Summon actions never show in player HUD and never used by AI.

**Task 7.2 — Coverage report for “granted actions” across character data**
- **Files to Modify:**
  - Tests/Helpers/ParityDataValidator.cs
- **Steps:**
  1. Emit a warning-only report listing granted action IDs by (race/class/feat) that are:
     - in Data/Actions
     - in BG3 ActionRegistry
     - missing from both
  2. Use this report to drive incremental coverage work without blocking unrelated PRs.
- **Acceptance Criteria:**
  - [ ] CI output gives a clear action-coverage inventory.

## Open Questions

1. Default toggle state for `IsToggled` passives?
   - **Option A:** Default off unless scenario enables.
   - **Option B:** Default on unless scenario disables.
   - **Option C:** Infer from status presence after ToggleOnFunctors.
   - **Recommendation:** Option A (least surprising and easiest to make deterministic).

2. Shove behavior: does BG3 allow “shove to prone” or only push?
   - **Option A:** Implement two shove variants (push/prone) to match D&D 5e.
   - **Option B:** Implement push-only to match BG3 if verified.
   - **Recommendation:** Start with variants behind data; adjust after verifying with bg3.wiki.

3. Where should forbidden-action policy live (summons)?
   - **Option A:** Filter only at HUD/action list layer.
   - **Option B:** Filter at action registry import.
   - **Recommendation:** Option A (keeps registry complete for tooling, while enforcing gameplay constraints).

## Risks & Mitigation

- **Risk:** Wiring new services into EffectContext creates implicit dependencies and brittle tests.
  - **Mitigation:** Keep fallbacks for non-arena unit tests; add small integration tests using CombatArena wiring.

- **Risk:** Duplicate action IDs in JSON packs can cause non-deterministic load order behavior.
  - **Mitigation:** Remove duplicates and make parity gate fail on duplicates unless explicitly allowlisted.

- **Risk:** Toggle functors may rely on unimplemented functor types.
  - **Mitigation:** Start with ApplyStatus/RemoveStatus (already implemented). Log stubbed functors as warnings and add allowlist only temporarily.

## Success Criteria

- [ ] `scripts/ci-build.sh` and `scripts/ci-test.sh` pass.
- [ ] Parity gate validates granted actions against union registry and blocks forbidden summon actions.
- [ ] Forced movement in action execution uses ForcedMovementService (collision/surfaces/falls).
- [ ] Toggleable passives are visible and usable in the action bar, and execute toggle functors deterministically.
- [ ] Canonical BG3-replica scenario loads headless and runs through CombatArena without manual intervention.

## Notes for Atlas

- Keep each PR/task to ~1–2 files as scoped above; if you need a new helper type, prefer creating a single-purpose file and keep call sites minimal.
- Always re-run the mandatory gates after each phase; many later tasks depend on parity gate improvements from Phase 1.
- Avoid reformatting JSON packs wholesale; keep diffs localized to the specific action(s) being corrected.
