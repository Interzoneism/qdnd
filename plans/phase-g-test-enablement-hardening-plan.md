# Plan: Phase G - Test Enablement and Hardening

**Created:** 2026-02-02
**Status:** Ready for Atlas Execution

## Summary

Phase G focuses on fixing 3 pre-existing test failures and enabling 13 skipped test suites. This phase stabilizes the test infrastructure before tackling additional features from the master TO-DO.

## Context & Analysis

**Relevant Files:**
- [Tests/Unit/EffectPipelineIntegrationTests.cs](../Tests/Unit/EffectPipelineIntegrationTests.cs): 3 failing tests due to ActionBudget not being reset
- [Combat/Abilities/EffectPipeline.cs](../Combat/Abilities/EffectPipeline.cs): Enforces action economy in CanUseAbility/ExecuteAbility
- 13 `.cs.skip` test files needing enablement

**Key Functions/Classes:**
- `EffectPipeline.CanUseAbility`: Checks cooldown AND action budget
- `EffectPipeline.ProcessTurnStart`: Only ticks cooldowns, doesn't reset ActionBudget
- `ActionBudget.ResetForTurn/ResetFull`: Available methods for budget reset

**Dependencies:**
- Tests use `Godot.Vector3` which works in CI since GodotSharp is referenced
- Some services depend on `CombatContext` (a Godot Node) blocking headless tests

**Patterns & Conventions:**
- Working tests avoid instantiating Godot Nodes directly
- Some tests use `UsesAction = false` to bypass action economy

## Implementation Phases

### Phase 1: Fix EffectPipelineIntegrationTests Failures

**Objective:** Fix the 3 failing tests by updating test setup to reset ActionBudget

**Files to Modify:**
- Tests/Unit/EffectPipelineIntegrationTests.cs

**Tests to Update:**
1. `ExecuteAbility_ApplyStatus_StacksExisting` - Add ActionBudget.ResetFull() between executions
2. `ExecuteAbility_WithCooldown_TracksCharges` - Add ActionBudget.ResetForTurn() before CanUseAbility checks
3. `Reset_ClearsCooldowns` - Add ActionBudget.ResetFull() after pipeline.Reset()

**Steps:**
1. Update `ExecuteAbility_ApplyStatus_StacksExisting` to reset action budget between uses
2. Update `ExecuteAbility_WithCooldown_TracksCharges` to reset action budget after ProcessTurnStart
3. Update `Reset_ClearsCooldowns` to reset combatant budget after pipeline reset
4. Run `./scripts/ci-test.sh` to verify all pass

**Acceptance Criteria:**
- [ ] All 3 previously failing tests now pass
- [ ] No regressions in other EffectPipelineIntegrationTests
- [ ] `./scripts/ci-test.sh` passes

---

### Phase 2: Enable Plain C# Service Tests (Low-Hanging Fruit)

**Objective:** Rename and fix 6 test files for services already decoupled from Godot Nodes

**Files to Enable:**
1. Tests/Unit/HeightServiceTests.cs.skip → Tests/Unit/HeightServiceTests.cs
2. Tests/Unit/SpecialMovementTests.cs.skip → Tests/Unit/SpecialMovementTests.cs  
3. Tests/Unit/TargetValidatorTests.cs.skip → Tests/Unit/TargetValidatorTests.cs
4. Tests/Unit/LOSServiceTests.cs.skip → Tests/Unit/LOSServiceTests.cs
5. Tests/Unit/ForcedMovementTests.cs.skip → Tests/Unit/ForcedMovementTests.cs
6. Tests/Unit/MovementServiceTests.cs.skip → Tests/Unit/MovementServiceTests.cs

**Steps:**
1. Rename each file (remove .skip extension)
2. Run tests to identify failures
3. Fix any Vector3/Godot compatibility issues
4. Ensure services match test expectations
5. Run `./scripts/ci-test.sh` to verify

**Acceptance Criteria:**
- [ ] All 6 test files renamed and enabled
- [ ] Tests pass (or are marked Skip for specific methods requiring refactoring)
- [ ] `./scripts/ci-test.sh` passes

---

### Phase 3: Enable AI Tests with Test Context

**Objective:** Enable AI-related tests by creating testable interfaces/mocks for CombatContext

**Files to Enable:**
1. Tests/Unit/AITargetEvaluatorTests.cs.skip
2. Tests/Unit/AIMovementTests.cs.skip
3. Tests/Unit/AIScorerTests.cs.skip
4. Tests/Unit/AIDecisionPipelineTests.cs.skip

**Approach:**
- Create `ICombatContext` interface or mock CombatContext for tests
- Update AI service constructors to accept interface
- Wire tests to use mock context

**Steps:**
1. Create ICombatContext interface with essential methods
2. Update AIScorer/AIDecisionPipeline to use interface
3. Create TestCombatContext that implements interface
4. Update tests to use TestCombatContext
5. Rename and run tests

**Acceptance Criteria:**
- [ ] All 4 AI test files enabled
- [ ] AI tests pass headlessly
- [ ] No breaking changes to production CombatArena

---

### Phase 4: Enable Integration Tests

**Objective:** Enable PhaseCIntegrationTests by ensuring all Phase C services work together

**Files to Enable:**
1. Tests/Integration/PhaseCIntegrationTests.cs.skip

**Steps:**
1. Rename file to remove .skip
2. Run tests to identify failures
3. Fix service implementations as needed
4. Verify integration between movement, surfaces, and height services

**Acceptance Criteria:**
- [ ] PhaseCIntegrationTests enabled and passing
- [ ] All Phase C subsystems integrate correctly

---

### Phase 5: Enable HUD Model Tests

**Objective:** Enable HUDModelTests by handling EmitSignal safely

**Files to Enable:**
1. Tests/Unit/HUDModelTests.cs.skip

**Steps:**
1. Update HUD models with testable signal pattern
2. Or mark signal-dependent tests as Skip
3. Enable remainder of tests

**Acceptance Criteria:**
- [ ] HUDModelTests enabled
- [ ] Non-signal tests pass

---

### Phase 6: Cleanup Obsolete Tests

**Objective:** Remove obsolete PositionSystemTests

**Files to Modify/Remove:**
- Tests/Unit/PositionSystemTests.cs.skip (delete or migrate)

**Steps:**
1. Verify PositionSystem functionality is covered by MovementServiceTests
2. Delete obsolete file
3. Update documentation

**Acceptance Criteria:**
- [ ] No orphan test files
- [ ] All position-related tests covered by MovementService

---

## Open Questions

1. **Should EffectPipeline.ProcessTurnStart also reset ActionBudget?**
   - **Option A:** No, keep separation (tests manage their own budget)
   - **Option B:** Yes, add optional combatant parameter for budget reset
   - **Recommendation:** Option A for now, simpler and tests are explicit

2. **How to handle Node-dependent AI services?**
   - **Option A:** Create ICombatContext interface
   - **Option B:** Extract service registry into plain C# class
   - **Recommendation:** Option A for minimal refactoring

## Risks & Mitigation

- **Risk:** Enabling tests may reveal production bugs
  - **Mitigation:** Fix bugs as discovered, add regression tests

- **Risk:** Refactoring AI services may break production
  - **Mitigation:** Use interface extension pattern, no breaking changes

## Success Criteria

- [ ] All 3 EffectPipelineIntegrationTests failures fixed
- [ ] At least 6 skipped test files enabled and passing
- [ ] `./scripts/ci-build.sh` passes
- [ ] `./scripts/ci-test.sh` passes
- [ ] `./scripts/ci-benchmark.sh` passes

## Notes for Atlas

- Start with Phase 1 (fixing existing failures) before enabling new tests
- Phase 2 is low-risk and can be parallelized
- Phases 3-5 require more careful refactoring
- Always run CI scripts after each phase
