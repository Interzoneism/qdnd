## Plan Complete: Detailed Action Effect Logging for Combat Log

Extended the full-fidelity testing combat_log.jsonl with rich, per-action forensic data. Every ability execution and movement action now emits an `ACTION_DETAIL` event containing structured, action-category-specific details (damage rolls, healing amounts, status applications, positions, movement distances, saving throws, attack rolls, etc.). This enables agents to verify action correctness directly from the log without cross-referencing multiple events or guessing at internal state.

**Phases Completed:** 5 of 5
1. ✅ Phase 1: ACTION_DETAIL event type + LogEntry fields + LogActionDetail method
2. ✅ Phase 2: ActionDetailCollector — action-specific data extractor
3. ✅ Phase 3: Wired ActionDetailCollector into AutoBattleRuntime via EffectPipeline
4. ✅ Phase 4: Movement action logging (Walk, Jump, Dash, Climb, Teleport, Fly, Swim)
5. ✅ Phase 5: Documentation update + verbose stdout echo support

**All Files Created/Modified:**
- Tools/AutoBattler/BlackBoxLogger.cs
- Tools/AutoBattler/ActionDetailCollector.cs (new)
- Tools/AutoBattler/MovementDetailCollector.cs (new)
- Tools/AutoBattler/AutoBattleRuntime.cs
- Tools/AutoBattler/AutoBattlerManager.cs
- Combat/Actions/EffectPipeline.cs
- Combat/Arena/CombatArena.cs
- scripts/run_autobattle.sh
- AGENTS-FULL-FIDELITY-TESTING.md
- Tests/Unit/BlackBoxLoggerTests.cs (new)
- Tests/Unit/ActionDetailCollectorTests.cs (new)
- Tests/Unit/MovementLoggingTests.cs (new)

**Key Functions/Classes Added:**
- ActionDetailCollector.Collect() — extracts rich details from ActionExecutionResult
- ActionDetailCollector.TargetSnapshot — per-target HP/position snapshot
- MovementDetailCollector.CollectFromMovement() — extracts standard movement data
- MovementDetailCollector.CollectFromSpecialMovement() — extracts special movement data
- BlackBoxLogger.LogActionDetail() — convenience method for ACTION_DETAIL events
- BlackBoxLogger.VerboseDetailLogging — controls stdout echo
- AutoBattleRuntime.OnAbilityExecutedForDetail() — EffectPipeline handler
- AutoBattleRuntime.OnMovementCompletedForDetail() — MovementService handler
- AutoBattleRuntime.OnSpecialMovementEvent() — RuleEventBus handler
- ActionExecutionResult.SourcePositionBefore / TargetPositionsBefore — pre-execution snapshots
- AutoBattleConfig.VerboseDetailLogging — CLI config property

**Test Coverage:**
- Total tests written: 31 (11 BlackBoxLogger + 20 ActionDetailCollector + 12 MovementLogging) [note: some were added during revisions]
- All tests passing: ✅ (1555 total suite, 0 failures)

**Recommendations for Next Steps:**
- Add REACTION_DETAIL event type for reaction/interrupt logging (natural Phase 6)
- Consider per-target detail breakdown for multi-target AoE spells
- Add concentration tracking to status detail logging
- Validate ACTION_DETAIL output against the special_spells.md reference document during full-fidelity runs
