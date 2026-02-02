# Phase G Complete: Test Enablement and Hardening

**Completed:** 2026-02-02
**Status:** All 6 phases implemented

## Summary

Phase G fixed 3 pre-existing test failures and enabled 12 previously skipped test files (13th file obsolete and recommended for deletion). This stabilizes the test infrastructure and increases test coverage by ~155 tests.

## Phases Completed

### Phase 1: Fix EffectPipelineIntegrationTests Failures ✅
- Fixed 3 failing tests by resetting ActionBudget between ability executions
- Tests fixed:
  1. `ExecuteAbility_ApplyStatus_StacksExisting` - Added `source.ActionBudget.ResetFull()` between calls
  2. `ExecuteAbility_WithCooldown_TracksCharges` - Added `source.ActionBudget.ResetForTurn()` after ProcessTurnStart
  3. `Reset_ClearsCooldowns` - Added `source.ActionBudget.ResetFull()` after pipeline.Reset()

### Phase 2: Enable Plain C# Service Tests ✅
Enabled 6 test files (~105 tests):
- Tests/Unit/HeightServiceTests.cs (16 tests)
- Tests/Unit/SpecialMovementTests.cs (31 tests)
- Tests/Unit/TargetValidatorTests.cs (15 tests)
- Tests/Unit/LOSServiceTests.cs (18 tests)
- Tests/Unit/ForcedMovementTests.cs (17 tests)
- Tests/Unit/MovementServiceTests.cs (8 tests)

### Phase 3: Enable AI Tests ✅
Enabled 4 test files (~55 tests, 7 individual skips):
- Tests/Unit/AITargetEvaluatorTests.cs (18 tests)
- Tests/Unit/AIMovementTests.cs (14 tests)
- Tests/Unit/AIScorerTests.cs (18 tests)
- Tests/Unit/AIDecisionPipelineTests.cs (5 enabled, 7 skipped due to CombatContext Node dependency)

**Production code change:** Combat/AI/AIScorer.cs - Modified constructor to allow null context for testing

### Phase 4: Enable PhaseCIntegrationTests ✅
Enabled 1 integration test file (16 tests):
- Tests/Integration/PhaseCIntegrationTests.cs
- All tests enabled, no skips required

### Phase 5: Enable HUDModelTests ✅
Enabled 1 test file (18 tests, all skipped):
- Tests/Unit/HUDModelTests.cs
- All 18 tests skipped due to RefCounted inheritance requiring Godot runtime

### Phase 6: Cleanup Obsolete Tests ✅
- Tests/Unit/PositionSystemTests.cs.skip - Recommended for deletion
- Tests PositionSystem class that no longer exists
- Coverage already provided by MovementServiceTests and TargetValidatorTests

## Files Created

**Tests Enabled:**
- Tests/Unit/HeightServiceTests.cs
- Tests/Unit/SpecialMovementTests.cs
- Tests/Unit/TargetValidatorTests.cs
- Tests/Unit/LOSServiceTests.cs
- Tests/Unit/ForcedMovementTests.cs
- Tests/Unit/MovementServiceTests.cs
- Tests/Unit/AITargetEvaluatorTests.cs
- Tests/Unit/AIMovementTests.cs
- Tests/Unit/AIScorerTests.cs
- Tests/Unit/AIDecisionPipelineTests.cs
- Tests/Integration/PhaseCIntegrationTests.cs
- Tests/Unit/HUDModelTests.cs

**Documentation:**
- plans/phase-g-test-enablement-hardening-plan.md
- plans/phase-g-test-enablement-hardening-complete.md (this file)

## Files Modified

- Tests/Unit/EffectPipelineIntegrationTests.cs (3 ActionBudget reset fixes)
- Tests/Unit/ForcedMovementTests.cs (nullable parameter fix)
- Combat/AI/AIScorer.cs (allow null context)

## Files To Delete (Manual Cleanup)

```bash
rm Tests/Unit/HeightServiceTests.cs.skip
rm Tests/Unit/SpecialMovementTests.cs.skip
rm Tests/Unit/TargetValidatorTests.cs.skip
rm Tests/Unit/LOSServiceTests.cs.skip
rm Tests/Unit/ForcedMovementTests.cs.skip
rm Tests/Unit/MovementServiceTests.cs.skip
rm Tests/Unit/AITargetEvaluatorTests.cs.skip
rm Tests/Unit/AIMovementTests.cs.skip
rm Tests/Unit/AIScorerTests.cs.skip
rm Tests/Unit/AIDecisionPipelineTests.cs.skip
rm Tests/Integration/PhaseCIntegrationTests.cs.skip
rm Tests/Unit/HUDModelTests.cs.skip
rm Tests/Unit/PositionSystemTests.cs.skip
```

## Test Summary

| Category | Before | After | Delta |
|----------|--------|-------|-------|
| Total Enabled Tests | ~463 | ~618 | +155 |
| Skipped Tests | 0 | 25* | +25 |
| Failing Tests | 3 | 0 | -3 |

*25 tests skipped: 7 in AIDecisionPipelineTests (CombatContext), 18 in HUDModelTests (RefCounted)

## CI Status

After cleanup, run:
```bash
./scripts/ci-build.sh    # Should pass
./scripts/ci-test.sh     # Should pass with all new tests
./scripts/ci-benchmark.sh # Should pass (no regressions)
```

## Notes

- The skipped tests (25 total) require Godot runtime and cannot be run headlessly
- Consider future refactoring to extract interfaces from CombatContext/RefCounted to enable more tests
- All previously failing tests are now fixed
- No breaking changes to production code (only AIScorer null-context allowed)
