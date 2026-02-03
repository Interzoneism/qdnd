## Phase 1 Complete: Test Cleanup

Enabled PositionSystem tests and removed redundant skipped duplicates to reduce test-suite confusion and improve baseline coverage.

**Files created/changed:**
- READY_TO_START.md
- Tests/Unit/PositionSystemTests.cs
- Tests/Integration/PhaseCIntegrationTests.cs.skip (deleted)
- Tests/Unit/MovementServiceTests.cs.skip (deleted)
- Tests/Unit/ForcedMovementTests.cs.skip (deleted)
- Tests/Unit/SpecialMovementTests.cs.skip (deleted)
- Tests/Unit/TargetValidatorTests.cs.skip (deleted)
- Tests/Unit/LOSServiceTests.cs.skip (deleted)
- Tests/Unit/HUDModelTests.cs.skip (deleted)
- Tests/Unit/AIDecisionPipelineTests.cs.skip (deleted)
- Tests/Unit/AITargetEvaluatorTests.cs.skip (deleted)
- Tests/Unit/AIMovementTests.cs.skip (deleted)
- Tests/Unit/AIScorerTests.cs.skip (deleted)
- Tests/Unit/HeightServiceTests.cs.skip (deleted)
- Tests/Unit/PositionSystemTests.cs.skip (deleted)

**Functions created/changed:**
- None

**Tests created/changed:**
- PositionSystemTests (enabled; 15 tests)

**Review Status:** APPROVED with minor recommendations

**Git Commit Message:**
chore: enable PositionSystemTests and clean skips

- Enable PositionSystemTests by removing .cs.skip suffix
- Delete redundant duplicate .cs.skip test files
- Update READY_TO_START with current test status
