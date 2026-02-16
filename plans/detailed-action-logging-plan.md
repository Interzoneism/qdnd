## Plan: Detailed Action Effect Logging for Combat Log

Extend the `BlackBoxLogger` and `AutoBattleRuntime` to emit rich, action-specific detail events after every ability execution. Instead of just logging `ACTION_RESULT` with a description string, the system will emit a new `ACTION_DETAIL` event containing structured data specific to each action category (damage spells, healing, movement/jump, forced movement, status application, summons, surfaces, etc.). This enables agents to verify that actions work correctly without needing to cross-reference multiple log lines or guess at internal state.

**Phases (5 phases)**

1. **Phase 1: Extend LogEntry and BlackBoxLogger with ACTION_DETAIL event type**
    - **Objective:** Add a new `ACTION_DETAIL` log event type and a flexible `details` dictionary field on `LogEntry` that can carry arbitrary action-specific key-value data. Add a convenience method `LogActionDetail()` on `BlackBoxLogger`.
    - **Files/Functions to Modify/Create:**
        - `Tools/AutoBattler/BlackBoxLogger.cs` — add `ACTION_DETAIL` to `LogEventType` enum, add `details` field to `LogEntry`, add `LogActionDetail()` method
    - **Tests to Write:**
        - `BlackBoxLogger_LogActionDetail_WritesCorrectEventType`
        - `BlackBoxLogger_LogActionDetail_IncludesAllDetailFields`
        - `BlackBoxLogger_LogActionDetail_NullDetailsOmitted`
    - **Steps:**
        1. Write unit tests for the new `ACTION_DETAIL` event type and `LogActionDetail` method
        2. Run tests — see them fail
        3. Add `ACTION_DETAIL` to `LogEventType`, add `details` dictionary field to `LogEntry`, implement `LogActionDetail`
        4. Run tests — confirmation they pass
        5. Build with `ci-build.sh`

2. **Phase 2: Create ActionDetailCollector — the action-specific data extractor**
    - **Objective:** Create a new class `ActionDetailCollector` that takes an `ActionExecutionResult`, the `ActionDefinition`, source `Combatant`, target `Combatant`(s), and produces a `Dictionary<string, object>` with rich, action-category-specific detail.
    - **Files/Functions to Create:**
        - `Tools/AutoBattler/ActionDetailCollector.cs` — new class
    - **Tests to Write:**
        - `ActionDetailCollector_DamageSpell_IncludesRollAndMitigation`
        - `ActionDetailCollector_HealEffect_IncludesHealAmount`
        - `ActionDetailCollector_StatusApply_IncludesStatusIdAndDuration`
        - `ActionDetailCollector_AttackRoll_IncludesNaturalRollAndModifiers`
        - `ActionDetailCollector_SavingThrow_IncludesDCAndResult`
        - `ActionDetailCollector_MultiProjectile_IncludesPerProjectileResults`
    - **Steps:**
        1. Write tests covering the major action categories
        2. Run tests — see them fail
        3. Implement `ActionDetailCollector`
        4. Run tests — confirmation they pass
        5. Build with `ci-build.sh`

3. **Phase 3: Wire ActionDetailCollector into AutoBattleRuntime**
    - **Objective:** Subscribe to `EffectPipeline.OnAbilityExecuted` unconditionally and use `ActionDetailCollector` to emit `ACTION_DETAIL` events. Add pre-execution position snapshots.
    - **Files/Functions to Modify:**
        - `Tools/AutoBattler/AutoBattleRuntime.cs`
        - `Combat/Actions/EffectPipeline.cs`
    - **Tests to Write:**
        - `AutoBattleRuntime_LogsActionDetailOnAbilityExecuted`
        - `ActionExecutionResult_CarriesPreExecutionPositions`
    - **Steps:**
        1. Write tests
        2. Run tests — see them fail
        3. Add position fields to `ActionExecutionResult`, populate in `ExecuteAction()`
        4. Wire `AutoBattleRuntime` to emit `ACTION_DETAIL`
        5. Run tests — confirmation they pass
        6. Build with `ci-build.sh`

4. **Phase 4: Extend logging for movement actions (Move, Jump, Dash, Disengage)**
    - **Objective:** Movement actions don't go through `EffectPipeline`, so add separate logging hooks.
    - **Files/Functions to Modify:**
        - `Tools/AutoBattler/AutoBattleRuntime.cs`
        - `Combat/Movement/MovementService.cs`
        - `Combat/Movement/SpecialMovementService.cs`
    - **Tests to Write:**
        - `MovementAction_LogsStartEndPositionAndDistance`
        - `JumpAction_LogsOriginTargetAndLandingPosition`
        - `DashAction_LogsMovementBudgetChange`
    - **Steps:**
        1. Write tests
        2. Run tests — see them fail
        3. Extend movement events with position data
        4. Subscribe in `AutoBattleRuntime` and emit `ACTION_DETAIL`
        5. Run tests — confirmation they pass
        6. Build with `ci-build.sh`

5. **Phase 5: Update documentation and add stdout echo for key details**
    - **Objective:** Document the new `ACTION_DETAIL` event and add verbose stdout echo support.
    - **Files/Functions to Modify:**
        - `AGENTS-FULL-FIDELITY-TESTING.md`
        - `Tools/AutoBattler/BlackBoxLogger.cs`
    - **Tests to Write:**
        - `BlackBoxLogger_ActionDetail_NotEchoedByDefault`
        - `BlackBoxLogger_ActionDetail_EchoedWhenVerbose`
    - **Steps:**
        1. Write tests for echo behavior
        2. Run tests — see them fail
        3. Add verbose flag and conditional echo
        4. Update documentation
        5. Run tests — confirmation they pass
        6. Final build: `ci-build.sh` and `ci-test.sh`

**Open Questions**
1. ACTION_DETAIL always logged to JSONL (negligible overhead), only echoed to stdout when verbose.
2. REACTION_DETAIL could be a future Phase 6.
3. Generic `details` dictionary for spell-specific fields (flexibility over rigid schemas).
4. Nested objects supported in details dictionary for per-target breakdowns.
