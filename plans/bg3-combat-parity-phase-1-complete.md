## Phase 1 Complete: Testing Infrastructure & Observability Upgrade

The full-fidelity testing system is now a parity quality oracle. Auto-battles collect detailed metrics on ability usage, unhandled effects, status behavior, damage events, and surface creation. The `--parity-report` flag enables end-to-end parity tracking, and the parity sweep script runs multiple seeds with aggregate reporting.

**Files created/changed:**
- Tools/AutoBattler/BlackBoxLogger.cs
- Tools/AutoBattler/AutoBattleRuntime.cs
- Tools/AutoBattler/AutoBattleWatchdog.cs
- Tools/AutoBattler/ParityCoverageReport.cs (new)
- Combat/Actions/EffectPipeline.cs
- Combat/Arena/CombatArena.cs
- Tools/DebugFlags.cs
- scripts/run_autobattle.sh
- scripts/run_parity_sweep.sh (new)
- Tests/Unit/ParityCoverageReportTests.cs (new)

**Functions created/changed:**
- BlackBoxLogger: LogEffectUnhandled(), LogStatusApplied(), LogStatusRemoved(), LogSurfaceCreated(), LogDamageDealt(), LogAbilityCoverage(), LogParitySummary()
- BlackBoxLogger.UnitSnapshot: Added Abilities field; SnapshotUnit() now populates abilities from KnownActions
- BlackBoxLogger.LogEventType: Added ABILITY_COVERAGE, EFFECT_UNHANDLED, STATUS_APPLIED, STATUS_REMOVED, STATUS_NO_RUNTIME_BEHAVIOR, SURFACE_CREATED, DAMAGE_DEALT, PARITY_SUMMARY
- AutoBattleRuntime: Added parity metric tracking (granted/attempted/succeeded/failed abilities, unhandled effects, damage, statuses, surfaces), wired StatusManager/EffectPipeline/SurfaceManager events, logs ABILITY_COVERAGE and PARITY_SUMMARY at battle end
- AutoBattleWatchdog: Added per-ability freeze tracking (last_ability_id in FeedAction and alert messages)
- EffectPipeline: Added OnEffectUnhandled event fired when effect type has no handler
- CombatArena: Added --parity-report CLI arg parsing that sets DebugFlags.ParityReportMode
- DebugFlags: Added ParityReportMode flag
- ParityCoverageReport: GenerateFromLog(), ToJson(), PrintSummary() — reads combat_log.jsonl and produces coverage report

**Tests created/changed:**
- ParityCoverageReportTests.EmptyLogReturnsZeroCoverage
- ParityCoverageReportTests.ParsesGrantedAbilitiesFromBattleStart
- ParityCoverageReportTests.TracksAttemptedAbilities
- ParityCoverageReportTests.TracksSucceededAbilities
- ParityCoverageReportTests.TracksFailedAbilities
- ParityCoverageReportTests.TracksUnhandledEffects
- ParityCoverageReportTests.CountsStatusAndDamageEvents
- ParityCoverageReportTests.CalculatesCoveragePercentages
- ParityCoverageReportTests.SerializesToJson
- ParityCoverageReportTests.IdentifiesNeverAttemptedAbilities
- ParityCoverageReportTests.MalformedLinesAreSkipped
- ParityCoverageReportTests.ParsesStatusNoRuntimeBehavior
- ParityCoverageReportTests.ParsesAbilitiesFromBattleStartUnits
- ParityCoverageReportTests.ParsesAbilityCoverageEvent
- ParityCoverageReportTests.ParsesParitySummaryEvent

**Review Status:** APPROVED (all issues resolved in revision)

**Git Commit Message:**
```
feat: add parity testing infrastructure and observability

- Add parity metrics collection to AutoBattleRuntime (ability coverage,
  unhandled effects, status behavior, damage, surfaces)
- Add new BlackBoxLogger events: ABILITY_COVERAGE, EFFECT_UNHANDLED,
  STATUS_APPLIED, STATUS_REMOVED, DAMAGE_DEALT, SURFACE_CREATED,
  PARITY_SUMMARY
- Add OnEffectUnhandled event to EffectPipeline for unhandled effect
  type detection
- Add per-ability freeze tracking to AutoBattleWatchdog
- Add ParityCoverageReport class for post-battle coverage analysis
- Add --parity-report CLI flag wired through CombatArena
- Add run_parity_sweep.sh for multi-seed aggregate coverage sweeps
- Add ParityReportMode debug flag
- Add 15 unit tests for parity report generation
```
