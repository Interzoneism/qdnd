# Plan: Phase B Completion - Rules Engine + Generic Abilities

**Created:** 2026-01-30
**Status:** ✅ COMPLETE (2026-01-30)

## Summary

Phase B code is largely implemented but not fully integrated or tested. This plan completes Phase B by: (1) wiring existing systems into Testbed, (2) creating real integration tests that exercise actual implementations, (3) completing missing effect types, and (4) adding the scenario pack that validates all effect types.

## Context & Analysis

**Relevant Files:**
- [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs): Core rules engine - COMPLETE
- [Combat/Rules/Modifier.cs](Combat/Rules/Modifier.cs): Modifier stacking - COMPLETE
- [Combat/Rules/RuleEvent.cs](Combat/Rules/RuleEvent.cs): Event bus - COMPLETE
- [Combat/Abilities/AbilityDefinition.cs](Combat/Abilities/AbilityDefinition.cs): Ability schema - COMPLETE
- [Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs): Effect execution - COMPLETE
- [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs): Effect handlers - NEEDS MORE TYPES
- [Combat/Statuses/StatusSystem.cs](Combat/Statuses/StatusSystem.cs): Status management - COMPLETE
- [Combat/Targeting/TargetValidator.cs](Combat/Targeting/TargetValidator.cs): Target validation - COMPLETE
- [Data/DataRegistry.cs](Data/DataRegistry.cs): Data loading/validation - COMPLETE
- [Scripts/Tools/TestbedBootstrap.cs](Scripts/Tools/TestbedBootstrap.cs): Testbed init - NEEDS PHASE B WIRING
- [Tests/Unit/*.cs](Tests/Unit/): Current tests use MOCK implementations, not real code

**Key Functions/Classes:**
- `EffectPipeline.ExecuteAbility()`: Main entry point for ability execution
- `RulesEngine.RollAttack/RollSave/RollDamage()`: Core dice mechanics
- `StatusManager.ApplyStatus/ProcessTurnEnd()`: Status lifecycle
- `TargetValidator.Validate()`: Target validation
- `DataRegistry.LoadFromDirectory()`: Content loading

**Dependencies:**
- System.Text.Json: JSON serialization
- Godot 4.5: Node system, Vector3, GD.Print
- xUnit: Testing framework

**Patterns & Conventions:**
- Deterministic RNG with seeds for reproducibility
- Event-driven architecture (RuleEventBus)
- Service locator pattern (CombatContext)
- Data-driven content (JSON files)
- All verification via logs/events, not visuals

## Implementation Phases

### Phase 1: Wire Phase B Services into Testbed

**Objective:** Integrate RulesEngine, EffectPipeline, StatusManager, and DataRegistry into TestbedBootstrap so they're available during scenario execution.

**Files to Modify:**
- `Scripts/Tools/TestbedBootstrap.cs`: Add Phase B service registration and initialization

**Steps:**
1. Add DataRegistry initialization in TestbedBootstrap
2. Load sample_abilities.json and sample_statuses.json on startup
3. Create EffectPipeline and wire it to RulesEngine and StatusManager
4. Register StatusManager with CombatContext
5. Add validation step that fails fast on registry errors
6. Emit "Phase B services ready" log with counts

**Tests to Write:**
- `Tests/Unit/TestbedPhaseB_Tests.cs`: Verify services resolve from context

**Acceptance Criteria:**
- [ ] DataRegistry loads abilities and statuses on Testbed start
- [ ] EffectPipeline, StatusManager registered in CombatContext
- [ ] Registry validation runs and logs results
- [ ] No errors on `dotnet build QDND.csproj`

---

### Phase 2: Create Real Integration Tests for Rules Engine

**Objective:** Replace mock-based tests with integration tests that exercise the REAL RulesEngine implementation.

**Files to Create:**
- `Tests/Unit/RulesEngineIntegrationTests.cs`: Tests for actual RulesEngine

**Files to Modify:**
- `Tests/QDND.Tests.csproj`: Ensure Godot mock/stub setup if needed

**Tests to Write:**
- `AttackRoll_WithModifiers_AppliesCorrectly`: Real attack roll
- `DamageRoll_WithResistance_ReducesDamage`: Damage pipeline
- `SavingThrow_WithAdvantage_RollsTwice`: Advantage mechanics
- `HitChance_Calculation_MatchesExpected`: Hit chance math
- `Modifiers_StackingOrder_FlatThenPercentage`: Modifier ordering

**Steps:**
1. Create test file with proper using statements
2. Create TestHelper class that initializes RulesEngine without Godot dependencies
3. Write 5+ tests covering core rules mechanics
4. Ensure deterministic results with fixed seeds
5. Run tests and verify pass

**Acceptance Criteria:**
- [ ] 5+ new integration tests pass
- [ ] Tests use REAL RulesEngine, not mocks
- [ ] Tests are deterministic (same results on re-run)
- [ ] All tests pass in CI

---

### Phase 3: Create Integration Tests for Effect Pipeline

**Objective:** Test the real EffectPipeline with actual damage/heal/status effects.

**Files to Create:**
- `Tests/Unit/EffectPipelineIntegrationTests.cs`: Real effect tests

**Tests to Write:**
- `DealDamage_ReducesTargetHP`: Damage effect works
- `Heal_IncreasesTargetHP_CappedAtMax`: Heal effect works
- `ApplyStatus_AddsToTarget`: Status application works
- `AbilityExecution_FullFlow_DamageAndStatus`: Full ability flow
- `Cooldown_DecrementOnTurn`: Cooldown mechanics
- `Preview_ReturnsExpectedRange`: Preview calculation

**Steps:**
1. Create test file
2. Create TestHelper that builds EffectPipeline with mock Godot dependencies
3. Write integration tests for each effect type
4. Test full ability execution flow
5. Verify cooldown mechanics

**Acceptance Criteria:**
- [ ] 6+ new integration tests pass
- [ ] Tests exercise real EffectPipeline code
- [ ] Damage/Heal/Status effects verified
- [ ] Cooldown mechanics verified

---

### Phase 4: Add Missing Effect Types

**Objective:** Implement remaining effect types needed for Phase B: Teleport, ForcedMove, SpawnSurface (stub).

**Files to Modify:**
- `Combat/Abilities/Effects/Effect.cs`: Add new effect handlers

**Effects to Add:**
- `TeleportEffect`: Relocate unit to position (stub - requires position system)
- `ForcedMoveEffect`: Push/pull unit (stub - requires position system)
- `SpawnSurfaceEffect`: Create surface at location (stub - surface system in Phase C)

**Steps:**
1. Add TeleportEffect class (stub that emits event)
2. Add ForcedMoveEffect class (stub that emits event)
3. Add SpawnSurfaceEffect class (stub that emits event)
4. Register new effects in EffectPipeline constructor
5. Add to sample_abilities.json for testing
6. Write tests for event emission

**Tests to Write:**
- `TeleportEffect_EmitsEvent`: Verify event dispatched
- `ForcedMoveEffect_EmitsEvent`: Verify event dispatched

**Acceptance Criteria:**
- [ ] TeleportEffect, ForcedMoveEffect, SpawnSurfaceEffect classes exist
- [ ] Effects registered in EffectPipeline
- [ ] Effects emit appropriate events
- [ ] Tests pass

---

### Phase 5: Create Effect Scenario Pack

**Objective:** Add scenario files that exercise each effect type for automated validation.

**Files to Create:**
- `Data/Scenarios/effect_damage_test.json`: Damage effect scenario
- `Data/Scenarios/effect_heal_test.json`: Heal effect scenario
- `Data/Scenarios/effect_status_test.json`: Status effect scenario
- `Data/Scenarios/effect_combo_test.json`: Combined effects scenario

**Steps:**
1. Create damage test scenario with expected outcomes
2. Create heal test scenario
3. Create status application scenario
4. Create combo scenario (damage + status + heal)
5. Add scenario loader support for expected outcomes
6. Add test that runs scenarios and asserts outcomes

**Tests to Write:**
- `ScenarioRunner_DamageScenario_CorrectOutcome`: Run damage scenario
- `ScenarioRunner_StatusScenario_StatusApplied`: Run status scenario

**Acceptance Criteria:**
- [ ] 4 new scenario files created
- [ ] Scenarios can be loaded by ScenarioLoader
- [ ] Tests verify scenario outcomes

---

### Phase 6: Status System Integration + Tick Effects

**Objective:** Wire status tick effects to actually deal damage/heal when status ticks.

**Files to Modify:**
- `Combat/Statuses/StatusSystem.cs`: Enhance ProcessTickEffects
- `Scripts/Tools/TestbedBootstrap.cs`: Wire status ticks to combat flow

**Steps:**
1. Modify ProcessTickEffects to actually apply damage/heal
2. Connect StatusManager.OnStatusTick to process effects
3. Wire turn end to trigger status processing
4. Test poison DOT dealing damage
5. Test burning DOT dealing damage

**Tests to Write:**
- `StatusTick_PoisonDOT_DealsDamage`: Poison status deals tick damage
- `StatusTick_Burning_DealsDamage`: Burning status deals tick damage
- `StatusTick_MultiStack_ScalesDamage`: Stacked status scales damage

**Acceptance Criteria:**
- [ ] Status tick effects apply damage/heal
- [ ] Tick damage scales with stacks
- [ ] Events emitted for tick damage
- [ ] Tests pass

---

### Phase 7: Targeting System Validation Tests

**Objective:** Add comprehensive tests for TargetValidator including AoE resolution.

**Files to Create:**
- `Tests/Unit/TargetValidatorTests.cs`: Targeting system tests

**Tests to Write:**
- `Validate_EnemiesOnly_FiltersAllies`: Faction filtering
- `Validate_RangeCheck_Works`: Range validation (when positions added)
- `ResolveArea_Circle_CorrectTargets`: Circle AoE resolution
- `ResolveArea_Cone_CorrectTargets`: Cone AoE resolution
- `GetValidTargets_ReturnsAllValid`: Valid target query

**Steps:**
1. Create test file
2. Write faction filtering tests
3. Write AoE geometry tests (circle, cone, line)
4. Create mock position data for geometry tests
5. Run and verify tests

**Acceptance Criteria:**
- [ ] 5+ targeting tests pass
- [ ] Faction filtering verified
- [ ] AoE geometry verified (circle, cone, line)

---

### Phase 8: DataRegistry Validation at Startup

**Objective:** Ensure Testbed fails fast if data validation fails, with clear error messages.

**Files to Modify:**
- `Scripts/Tools/TestbedBootstrap.cs`: Add validation step
- `Data/DataRegistry.cs`: Enhance error messages

**Steps:**
1. Add registry.ValidateOrThrow() after loading
2. Enhance validation error messages with file paths
3. Add missing reference detection (ability refs missing status)
4. Test with intentionally broken data

**Tests to Write:**
- `Registry_MissingStatusRef_ReportsError`: Missing status reference detected
- `Registry_DuplicateId_ReportsError`: Duplicate ID detected

**Acceptance Criteria:**
- [ ] Testbed fails fast on invalid data
- [ ] Error messages include file paths and specific issues
- [ ] Missing references detected
- [ ] Tests pass

---

### Phase 9: Final Verification + CI Check

**Objective:** Run full build and test suite, update documentation.

**Files to Modify:**
- `READY_TO_START.md`: Update status for Phase B
- `docs/PHASE_A_GUIDE.md`: Add Phase B completion notes

**Steps:**
1. Run `scripts/ci-build.sh` - verify passes
2. Run `scripts/ci-test.sh` - verify all tests pass
3. Count total tests (should be 30+)
4. Update READY_TO_START.md with Phase B status
5. Create docs/PHASE_B_GUIDE.md with completion notes

**Acceptance Criteria:**
- [ ] `scripts/ci-build.sh` passes
- [ ] `scripts/ci-test.sh` passes
- [ ] 30+ unit tests total
- [ ] Documentation updated
- [ ] Phase B marked complete

---

## Open Questions

1. **Position System**
   - **Option A:** Add position component to Combatant now (more work but enables full targeting)
   - **Option B:** Stub position system, complete in Phase C (faster, deferred)
   - **Recommendation:** Option B - stub now, complete in Phase C with movement system

2. **Godot Dependencies in Tests**
   - **Option A:** Use Godot test runner (requires Godot editor)
   - **Option B:** Abstract Godot dependencies for headless testing (current approach)
   - **Recommendation:** Option B - maintain headless test capability

## Risks & Mitigation

- **Risk:** Tests may depend on Godot types (Vector3, GD.Print)
  - **Mitigation:** Create test helpers that abstract Godot dependencies

- **Risk:** Effect stubs may not fully exercise systems
  - **Mitigation:** Emit events that can be asserted, plan for full implementation in Phase C

- **Risk:** DataRegistry file paths may differ in CI
  - **Mitigation:** Use relative paths or test-specific data directories

## Success Criteria

- [ ] All Phase B services wired into Testbed
- [ ] 30+ unit tests passing (up from 16)
- [ ] Real implementations tested (not mocks)
- [ ] Effect pipeline fully exercises damage/heal/status
- [ ] Status tick effects apply damage
- [ ] DataRegistry validates on startup
- [ ] CI build and test pass
- [ ] Documentation updated

## Notes for Implementation

1. **Start with Phase 1** - Wiring services is prerequisite for everything else
2. **Parallel potential:** Phases 2-3-4 could run in parallel once Phase 1 complete
3. **Phase 5-6-7 depend on earlier phases**
4. **Always run CI scripts before declaring phase complete**
5. **Keep commits small and focused on one phase**
6. **Godot types (Vector3) need abstraction for headless tests**

## Estimated Effort

- Phase 1: 30 min
- Phase 2: 45 min
- Phase 3: 45 min
- Phase 4: 30 min
- Phase 5: 30 min
- Phase 6: 45 min
- Phase 7: 30 min
- Phase 8: 20 min
- Phase 9: 15 min

**Total: ~5 hours**
