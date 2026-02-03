# Plan: Small Fixes and Missing Features

**Created:** February 3, 2026
**Status:** Ready for Atlas Execution

## Summary

This plan addresses immediate issues and small missing features that should have been completed in previous phases. It focuses on: (1) test cleanup (re-enabling PositionSystemTests, removing obsolete `.cs.skip` duplicates), (2) damage type resistance/vulnerability/immunity support in the damage pipeline, and (3) a targeted audit of “missing effect types” to implement only what cleanly fits the current architecture.

## Context & Analysis

**Relevant Files:**
- [Tests/Unit/PositionSystemTests.cs.skip](Tests/Unit/PositionSystemTests.cs.skip): Unique tests for distance/range validation - should be enabled
- 12 other `.cs.skip` files: Obsolete duplicates of active test files, should be deleted
- [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs): Contains 8 effect types, missing 5+ required by master TO-DO
- [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs): Damage pipeline exists but no resistance/vulnerability/immunity
- [Combat/Rules/Modifier.cs](Combat/Rules/Modifier.cs): Modifier system includes tag plumbing (`QueryInput.Tags` → `ModifierContext.Tags`) but tag filtering isn’t implemented in `ModifierStack`
- [AGENTS-MASTER-TO-DO.md](AGENTS-MASTER-TO-DO.md): Many items marked `[ ]` but actually implemented
- [plans/combatarena-polish-and-gameplay-tests-plan.md](plans/combatarena-polish-and-gameplay-tests-plan.md): 8-phase plan, unclear completion status

**Key Functions/Classes:**
- `PositionSystemTests`: Contains critical distance/range validation tests not covered elsewhere
- `EffectPipeline()` constructor: Registers effect handlers, currently only 8 types
- `RulesEngine.RollDamage()`: Damage calculation, no resistance/vulnerability logic
- `ModifierStack.GetModifiers()`: Returns modifiers but doesn’t filter by `Modifier.Tags` vs `ModifierContext.Tags`
- `AoEIndicator`, `MovementPreview`, `ReactionPromptUI`: Already implemented in Combat/Arena/

**Reality Check (from code audit):**
- CombatArena already wires ReactionSystem + LOSService + HeightService into EffectPipeline (so “polish plan Phase 1 wiring” is already done).
- AoEIndicator, MovementPreview, ReactionPromptUI, and movement targeting mode exist (so large parts of the polish plan appear already implemented).

**Dependencies:**
- Godot 4.5 C# (no editor required for these changes)
- Existing service architecture (CombatContext, EffectPipeline, RulesEngine)
- Test infrastructure already works (dotnet test)

**Patterns & Conventions:**
- All effect handlers in single file (Effect.cs) following project pattern
- Tag-based system for damage types already partially exists
- Service registration via CombatContext
- Data-driven validation via tests/logs, not visuals

## Implementation Phases

---

### Phase 1: Test Cleanup

**Objective:** Enable PositionSystemTests and remove obsolete .skip files to clean up test suite.

**Files to Modify/Create:**
- Rename: `Tests/Unit/PositionSystemTests.cs.skip` → `Tests/Unit/PositionSystemTests.cs`
- Delete 12 redundant `.cs.skip` files

**Steps:**
1. Rename `PositionSystemTests.cs.skip` to `PositionSystemTests.cs`
   - This contains unique tests for `IsInRange`, `GetDistance`, etc.
   - No other test file covers this functionality

2. Delete obsolete `.cs.skip` files (all are duplicates of enabled `.cs` files):
   - `Tests/Integration/PhaseCIntegrationTests.cs.skip`
   - `Tests/Unit/MovementServiceTests.cs.skip`
   - `Tests/Unit/ForcedMovementTests.cs.skip`
   - `Tests/Unit/SpecialMovementTests.cs.skip`
   - `Tests/Unit/TargetValidatorTests.cs.skip`
   - `Tests/Unit/LOSServiceTests.cs.skip`
   - `Tests/Unit/HUDModelTests.cs.skip`
   - `Tests/Unit/AIDecisionPipelineTests.cs.skip`
   - `Tests/Unit/AITargetEvaluatorTests.cs.skip`
   - `Tests/Unit/AIMovementTests.cs.skip`
   - `Tests/Unit/AIScorerTests.cs.skip`
   - `Tests/Unit/HeightServiceTests.cs.skip`

3. Run tests to verify PositionSystemTests passes:
   ```bash
   dotnet test Tests/QDND.Tests.csproj --filter "PositionSystemTests"
   ```

4. Update READY_TO_START.md to reflect cleanup

**Acceptance Criteria:**
- [ ] PositionSystemTests.cs exists and all tests pass
- [ ] 12 obsolete `.cs.skip` duplicates deleted
- [ ] Full test suite still passes (`dotnet test`)
- [ ] Documentation updated

---

### Phase 2: Missing Effect Types (Targeted / Architecture-Fit)

**Objective:** Implement missing effect types only where they fit the existing EffectPipeline/Status/Rules architecture without forcing a large new “unit template” system.

**Files to Modify:**
- [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs): Add new effect handlers
- [Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs): Register new effects
- [Tests/Unit/EffectSystemTests.cs](Tests/Unit/EffectSystemTests.cs): Add tests for new effects

**Important:** In this codebase, effect type strings are lowercase (e.g., `damage`, `apply_status`, `spawn_surface`). Any new types should follow this convention.

**Proposed “small but real” additions:**
1. `add_modifier` (or `grant_advantage`) — adds a RulesEngine modifier to a combatant for N turns (enables advantage/disadvantage and typed damage modifiers cleanly)
2. `set_visibility` — minimal hook (e.g., a boolean on Combatant) plus TargetValidator honoring it (keeps scope small)

**Deferred / optional (needs more plumbing):**
- `summon_combatant` and `spawn_object`: there is no DataRegistry template system; these can be implemented as (a) custom RuleEvents only, or (b) event + optional callback injected via EffectContext to actually register entities into CombatContext/TurnQueue.
- `interrupt`: cancellation is currently modeled via ReactionTriggerContext + resolution stack and “Cancel” flags on trigger events, not via a dedicated effect handler. Implementing this as an effect likely requires additional cross-system wiring.

**Steps:**
1. Add `SummonCombatantEffect` in Effect.cs:
   - Takes: combatant template ID, position, faction
   - Spawns combatant via CombatContext.RegisterCombatant
   - Adds to initiative queue at current round
   - Emits `CombatantSummoned` event

2. Add `SpawnObjectEffect` in Effect.cs:
   - Takes: object template ID, position
   - Creates interactive object with HP, reactions
   - Registers in environment/object registry
   - Emits `ObjectSpawned` event

3. Add `GrantAdvantageEffect` in Effect.cs:
   - Takes: target, duration, condition (optional)
   - Creates Advantage modifier via RulesEngine
   - Can target attacks, saves, or specific skill checks
   - Uses existing modifier system

4. Add `SetVisibilityEffect` in Effect.cs:
   - Takes: target, visibility state (Hidden/Visible/Revealed)
   - Sets combatant.IsHidden flag
   - Emits `VisibilityChanged` event
   - AI/targeting uses this for selection logic

5. Add `InterruptEffect` in Effect.cs:
   - Takes: action to cancel, replacement action (optional)
   - Works with ResolutionStack to cancel/replace
   - Used primarily in reaction definitions
   - Emits `ActionInterrupted` event

6. Register any new effects in the `EffectPipeline` constructor via `RegisterEffect(new XxxEffect())`.

7. Write unit tests for each new effect:
   - `SummonCombatant_SpawnsCombatantAtPosition`
   - `SummonCombatant_AddsToInitiativeQueue`
   - `SpawnObject_CreatesInteractiveObject`
   - `GrantAdvantage_AddsModifier`
   - `SetVisibility_TogglesHiddenFlag`
   - `Interrupt_CancelsAction`

8. Create test scenario: `Data/Scenarios/effect_advanced_test.json`
   - Uses each new effect type
   - Validates via event log

**Acceptance Criteria:**
- [ ] All 5 new effect types implemented
- [ ] Effects registered in EffectPipeline
- [ ] Unit tests pass for each effect
- [ ] Test scenario loads and executes
- [ ] Events emitted correctly (verified by logs)
- [ ] No build errors, all existing tests still pass

---

### Phase 3: Damage Type Resistance Framework (Minimal, Compatible)

**Objective:** Implement resistance/vulnerability/immunity for damage types as required by AGENTS-MASTER-TO-DO.md Section 2.6, using the existing `damage:<type>` tag that DealDamageEffect already attaches to `QueryInput.Tags`.

**Files to Modify/Create:**
- [Combat/Entities/Combatant.cs](Combat/Entities/Combatant.cs): Add resistance/vulnerability/immunity collection keyed by the existing string damage types (e.g., `"fire"`, `"poison"`)
- [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs): Apply resistance multiplier during `RollDamage`
- [Tests/Unit/DamageResistanceTests.cs](Tests/Unit/DamageResistanceTests.cs) (new): Golden tests

**Steps:**
1. Add to Combatant a simple resistance map + helper:
   - Key is the string damage type already used in data (`physical`, `fire`, `poison`, …).
   - Value is an enum or multiplier.

2. Update `RulesEngine.RollDamage()`:
   - Detect damage type from `QueryInput.Tags` entries shaped like `damage:<type>`.
   - If `input.Target` has a configured resistance/vulnerability for that type, apply multiplier after DamageDealt/DamageTaken modifiers.
   - Keep default as 1.0 (no change) so existing tests don’t regress.

3. Add unit tests that construct Combatants directly and call `RulesEngine.RollDamage()` with `QueryInput.Tags` containing `damage:fire` etc.

6. Write tests:
   - `Resistance_HalvesDamage`
   - `Vulnerability_DoublesDamage`
   - `Immunity_NegatesDamage`
   - `UnknownType_NoResistance`
   - `MultipleHits_DifferentTypes`

7. Update breakdown system to show resistance in log

**Acceptance Criteria:**
- [ ] DamageType enum defined
- [ ] DamageResistance model implemented
- [ ] Combatant has resistance collection
- [ ] RulesEngine applies resistances in damage pipeline
- [ ] Tests pass (including golden tests for pipeline order)
- [ ] Breakdown shows resistance modifiers
- [ ] Existing damage tests still pass

---

### Phase 4: Verify Polish Plan Completion (Fast Audit)

**Objective:** Audit the `combatarena-polish-and-gameplay-tests-plan.md` and reconcile it with the actual repo state.

**Files to Review:**
- [plans/combatarena-polish-and-gameplay-tests-plan.md](plans/combatarena-polish-and-gameplay-tests-plan.md)
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs)
- [Combat/Arena/AoEIndicator.cs](Combat/Arena/AoEIndicator.cs)
- [Combat/Arena/MovementPreview.cs](Combat/Arena/MovementPreview.cs)
- [Combat/Arena/ReactionPromptUI.cs](Combat/Arena/ReactionPromptUI.cs)
- [Combat/Arena/DebugPanel.cs](Combat/Arena/DebugPanel.cs)

**Steps:**
1. Read the polish plan (8 phases)

2. For each phase, verify completion:
   - **Phase 1 (Systems Wiring)**: already appears complete (CombatArena wires ReactionSystem + LOS + Height into EffectPipeline)
   - **Phase 2 (Movement Preview)**: MovementPreview exists
   - **Phase 3 (AoE Indicators)**: AoEIndicator exists
   - **Phase 4 (Reaction Prompt)**: ReactionPromptUI exists
   - **Phase 5 (Combat Feedback)**: Check combat log, floating text enhancements
   - **Phase 6 (Gameplay Tests)**: Check for simulation tests
   - **Phase 7 (Surface Visuals)**: Check for SurfaceVisual.cs
   - **Phase 8 (Debug Panel)**: Check DebugPanel features

3. For incomplete phases, determine:
   - What's missing
   - Estimated effort (Small/Medium/Large)
   - Whether it blocks other work

4. Create focused sub-plan if needed for remaining items

5. Document findings in completion report

**Acceptance Criteria:**
- [ ] Each phase audited with evidence (file exists, tests pass, features work)
- [ ] Incomplete items identified with severity
- [ ] Decision made: continue with polish plan or defer
- [ ] Documentation updated (READY_TO_START.md)

---

### Phase 5: Update Master TO-DO Checkboxes (Documentation Accuracy)

**Objective:** Update AGENTS-MASTER-TO-DO.md so the checklist reflects reality (many items are implemented but still unchecked).

**Files to Modify:**
- [AGENTS-MASTER-TO-DO.md](AGENTS-MASTER-TO-DO.md)

**Items to Check Off (Already Implemented):**

From **Section 0.1**:
- [x] Feature-flag / debug command system (DebugFlags.cs, DebugConsole.cs exist with tests)

From **Section 1.3**:
- [x] Advantage/disadvantage system (Modifier.cs, ModifierStack with tests)

From **Section 2.1**:
- [x] Rules engine with modifiers stack ✅
- [x] Queries for calculations ✅
- [x] Events that fire for triggers ✅

From **Section 4.1**:
- [x] Turn order tracker ✅
- [x] Action bar with costs/disabled reasons ✅
- [x] Resource panels ✅
- [x] Combat log with rolls/breakdown ✅
- [x] Inspect panel ✅

**Steps:**
1. Read through AGENTS-MASTER-TO-DO.md systematically

2. For each `[ ]` item in completed phases (A-J):
   - Search codebase for implementation
   - Check if tests exist
   - If both exist, mark as `[x]`
   - Add comment with file path for reference

3. Leave items truly incomplete as `[ ]`

4. Add a summary section at top:
   ```markdown
   ## Implementation Status (Updated Feb 3, 2026)
   
   **Phases Complete:** A-J
   **Total Items:** ~X
   **Implemented:** ~Y
   **Remaining:** ~Z
   ```

**Acceptance Criteria:**
- [ ] All implemented items marked `[x]` with evidence
- [ ] Incomplete items remain `[ ]` with notes
- [ ] Summary section added
- [ ] Document is accurate reference for remaining work

---

### Phase 6: Small Missing Wiring (If Found in Phase 4 Audit)

**Objective:** Complete any small wiring/integration items discovered during polish plan audit.

**This phase is contingent on Phase 4 findings.**

**Potential Items (examples, to be confirmed):**
- Wire ReactionSystem into EffectPipeline if not done
- Wire LOS/Height services into TargetValidator
- Add range checks to GetValidTargets()
- Subscribe CombatArena to reaction prompts
- Add missing keyboard shortcuts

**Steps:**
1. Based on Phase 4 audit, create specific task list

2. For each item:
   - Implement minimal wiring change
   - Write/update integration test
   - Verify via test, not visuals

3. Run full test suite after each change

**Acceptance Criteria:**
- [ ] All small wiring items from Phase 4 completed
- [ ] Integration tests pass
- [ ] No regressions in existing tests
- [ ] CombatArena remains functional

---

## Open Questions

1. **Polish Plan Priority**: Should we complete all 8 phases of the polish plan now, or defer visual polish?
   - **Option A:** Complete polish plan immediately (improves UX)
   - **Option B:** Defer polish, focus on missing systems (entity components, nested substates)
   - **Recommendation:** Phase 4 audit will inform this. If polish is >80% done, finish it. Otherwise defer.

2. **Entity Component Model**: Should we refactor Combatant to use components?
   - **Option A:** Major refactor now (high risk, breaks existing code)
   - **Option B:** Incremental componentization (low risk, gradual)
   - **Option C:** Keep monolithic (works fine, master TO-DO may be outdated)
   - **Recommendation:** Option C unless specific pain points identified. Current design works well with service architecture.

3. **Nested Substates**: Should we formalize substates in CombatStateMachine?
   - **Option A:** Add formal substate stack (clean architecture)
   - **Option B:** Keep current arena mode approach (works, simpler)
   - **Recommendation:** Option B - current approach is pragmatic and testable. Formal substates are over-engineering.

## Risks & Mitigation

- **Risk:** Enabling PositionSystemTests reveals bugs
  - **Mitigation:** Tests are for fundamental math (distance, range) - if they fail, that's critical and must be fixed

- **Risk:** New effect types may conflict with existing effects
  - **Mitigation:** Each effect is isolated, uses common pipeline. Tests verify no conflicts.

- **Risk:** Resistance framework may break existing damage tests
  - **Mitigation:** Default resistance is 1x (no change). Existing tests won't set resistances.

- **Risk:** Polish plan audit may reveal incomplete work requiring major effort
  - **Mitigation:** Phase 4 is discovery-only. Make informed decision based on findings.

## Success Criteria

- [ ] Test suite cleaned up (PositionSystemTests enabled, redundant files deleted)
- [ ] 5 missing effect types implemented and tested
- [ ] Damage resistance framework implemented and tested
- [ ] Polish plan completion status documented
- [ ] Master TO-DO checkboxes updated accurately
- [ ] All tests pass (`scripts/ci-test.sh`)
- [ ] Build succeeds (`scripts/ci-build.sh`)
- [ ] READY_TO_START.md updated with current status

## Notes for Atlas

**Execution Order:** Phases 1-3 can be executed in parallel (independent). Phase 4 should complete before Phase 6. Phase 5 can be done anytime.

**Testing Strategy:** 
- Phase 1: Verify PositionSystemTests passes after enabling
- Phase 2: Each new effect gets unit tests
- Phase 3: Resistance tests verify pipeline order
- Phase 4: Audit-only, no code changes
- Phase 5: Documentation-only, no testing needed
- Phase 6: Contingent on Phase 4

**Parallel Opportunities:**
- Phase 1 (test cleanup) + Phase 2 (effects) + Phase 3 (resistance) can run in parallel
- Use 3 separate sub-agents (Sisyphus instances) if desired
- Phase 4 (audit) can be delegated to Oracle-subagent for research

**Small, Safe Changes:** All phases are low-risk, incremental changes. No major refactoring.

**Quick Wins:** Phase 1 is ~5 minutes (rename + delete). Do it first for immediate value.

**Research Before Code:** Phase 4 is pure research. Results inform Phase 6 scope. Don't skip it.
