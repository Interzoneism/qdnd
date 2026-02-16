## Phase 4 Complete: Movement Action Logging

Extended the logging system to capture movement actions (Move, Jump, Dash, Climb, Teleport, Fly, Swim) which don't go through EffectPipeline. AutoBattleRuntime now subscribes to both MovementService.OnMovementCompleted and RuleEventBus custom events for special movements, emitting ACTION_DETAIL events with rich positional and movement data. Disengage is handled by the existing ACTION_RESULT + status logging path.

**Files created/changed:**
- Tools/AutoBattler/MovementDetailCollector.cs (created)
- Tools/AutoBattler/AutoBattleRuntime.cs (modified)
- Tests/Unit/MovementLoggingTests.cs (created)

**Functions created/changed:**
- MovementDetailCollector.CollectFromMovement(MovementResult) — extracts standard movement data
- MovementDetailCollector.CollectFromSpecialMovement(RuleEvent) — extracts special movement data with computed distance
- MovementDetailCollector.ConvertToFloatArray(object) — reflection-based Vector3 conversion
- AutoBattleRuntime.OnMovementCompletedForDetail(MovementResult) — handler for standard movement
- AutoBattleRuntime.OnSpecialMovementEvent(RuleEvent) — handler for Jump/Dash/Climb/Teleport/Fly/Swim
- AutoBattleRuntime.IsSpecialMovementEvent(RuleEvent) — case-insensitive filter

**Tests created/changed:**
- MovementAction_LogsStartEndPositionAndDistance
- JumpAction_LogsOriginTargetAndLandingPosition
- DashAction_LogsMovementBudgetChange
- MovementWithOpportunityAttacks_LogsCount
- TeleportAction_LogsPositions
- ClimbAction_LogsPositionsAndComputedDistance
- FlyAction_LogsPositionsAndComputedDistance
- SwimAction_LogsPositionsAndComputedDistance
- CaseInsensitiveCustomType_StillExtractsPositions
- CollectFromMovement_HandlesNullGracefully
- CollectFromSpecialMovement_HandlesNullGracefully
- UnknownSpecialMovement_StillLogsMovementType

**Review Status:** APPROVED (after revision for case-insensitive matching, computed distances, and expanded test coverage)

**Git Commit Message:**
```
feat: add movement action logging for combat forensic log

- Create MovementDetailCollector for standard and special movement data extraction
- Subscribe AutoBattleRuntime to MovementService and RuleEventBus movement events
- Emit ACTION_DETAIL events with positions, distances, budget changes, opportunity attacks
- Add 12 unit tests covering Walk, Jump, Dash, Climb, Teleport, Fly, Swim, and edge cases
```
