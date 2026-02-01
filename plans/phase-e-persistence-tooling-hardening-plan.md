# Plan: Phase E - Persistence + Tooling + Hardening

**Created:** 2026-02-01
**Status:** Ready for Atlas Execution

## Summary

Phase E completes the tactical combat system by implementing mid-combat save/load, editor tools for content authoring, automated simulation testing, and performance profiling. This phase ensures the system is production-ready with proper persistence, CI-friendly benchmarks, and developer tooling for efficient content creation.

## Context & Analysis

**Phase D Completed:**
- AI Decision Pipeline with utility-based scoring ✅
- AI Archetypes (Aggressive, Defensive, Support, etc.) ✅
- Combat Log with breakdown payloads ✅
- HUD data models (TurnTracker, ActionBar, ResourceBar) ✅
- Animation timeline and camera hooks ✅

**Phase E Scope (from Master TODO):**
- Save/load mid-combat
- Editor tools and sandbox resources
- Automated tests + simulation runner
- Performance pass + profiling
- Testbed: Save/load regression + benchmarks wired into CI

## Implementation Phases

---

### Phase 1: Combat State Snapshot Model

**Objective:** Create a serializable data transfer object (DTO) that captures the complete combat state.

**Files to Create:**
- `Combat/Persistence/CombatSnapshot.cs` - Main snapshot container
- `Combat/Persistence/CombatantSnapshot.cs` - Individual combatant state
- `Combat/Persistence/SurfaceSnapshot.cs` - Surface/field effect state
- `Combat/Persistence/StatusSnapshot.cs` - Active status state

**Key Classes:**
```csharp
public class CombatSnapshot
{
    public int Version { get; set; } = 1;
    public long Timestamp { get; set; }
    
    // Flow state
    public string CombatState { get; set; }  // enum name
    public int CurrentRound { get; set; }
    public int CurrentTurnIndex { get; set; }
    
    // RNG state (determinism)
    public int InitialSeed { get; set; }
    public int RollIndex { get; set; }
    
    // Entities
    public List<string> TurnOrder { get; set; }
    public List<CombatantSnapshot> Combatants { get; set; }
    public List<SurfaceSnapshot> Surfaces { get; set; }
    public List<StatusSnapshot> ActiveStatuses { get; set; }
    
    // Resolution stack (for mid-reaction saves)
    public List<StackItemSnapshot> ResolutionStack { get; set; }
    
    // Cooldowns
    public Dictionary<string, int> AbilityCooldowns { get; set; }
}
```

**Tests:** `Tests/Unit/CombatSnapshotTests.cs`
- Snapshot serializes to JSON without errors
- All fields round-trip correctly
- Version field present for migration support

**Acceptance Criteria:**
- [ ] CombatSnapshot contains all state listed in AGENTS-MASTER-TO-DO.md section 9
- [ ] Serialization produces valid JSON
- [ ] Deserialization restores identical structure

---

### Phase 2: Roll Index Tracking

**Objective:** Add roll index tracking to the RNG system for deterministic replay after load.

**Files to Modify:**
- `Combat/Rules/RulesEngine.cs` - Add RollIndex property and tracking
- `Combat/Rules/DiceRoller.cs` - Track and expose roll count

**Key Changes:**
```csharp
public class DiceRoller
{
    private int _rollIndex = 0;
    public int RollIndex => _rollIndex;
    
    public int Roll(int sides)
    {
        _rollIndex++;
        return _random.Next(1, sides + 1);
    }
    
    public void SetState(int seed, int rollIndex)
    {
        _random = new Random(seed);
        // Fast-forward to rollIndex
        for (int i = 0; i < rollIndex; i++)
            _random.Next();
        _rollIndex = rollIndex;
    }
}
```

**Tests:** `Tests/Unit/DiceRollerTests.cs`
- RollIndex increments on each roll
- SetState(seed, index) produces deterministic sequence
- Same seed + index = same subsequent rolls

**Acceptance Criteria:**
- [ ] RollIndex tracked and exposed
- [ ] SetState restores exact RNG position
- [ ] Test verifies determinism after fast-forward

---

### Phase 3: State Capture Service

**Objective:** Implement service to capture/restore complete combat state from all subsystems.

**Files to Create:**
- `Combat/Persistence/CombatSaveService.cs` - Orchestrates snapshot creation/restoration

**Files to Modify:**
- `Combat/Services/TurnQueueService.cs` - Add `ExportState()`/`ImportState()` methods
- `Combat/Statuses/StatusSystem.cs` - Add `ExportState()`/`ImportState()` methods
- `Combat/Environment/SurfaceManager.cs` - Add `ExportState()`/`ImportState()` methods
- `Combat/States/CombatStateMachine.cs` - Add `ExportState()`/`ImportState()` methods
- `Combat/Reactions/ResolutionStack.cs` - Add `ExportState()`/`ImportState()` methods

**Key Interface:**
```csharp
public interface IStateExportable<T>
{
    T ExportState();
    void ImportState(T snapshot);
}

public class CombatSaveService
{
    public CombatSnapshot CaptureSnapshot(CombatContext context);
    public void RestoreSnapshot(CombatContext context, CombatSnapshot snapshot);
}
```

**Tests:** `Tests/Unit/CombatSaveServiceTests.cs`
- CaptureSnapshot gathers all subsystem states
- RestoreSnapshot applies all subsystem states
- Round-trip preserves combat state hash

**Acceptance Criteria:**
- [ ] All major services implement IStateExportable
- [ ] Snapshot captures complete combat state
- [ ] Restoration produces matching state hash

---

### Phase 4: File I/O and Validation

**Objective:** Implement save file writing, loading, and validation with version migration.

**Files to Create:**
- `Combat/Persistence/SaveFileManager.cs` - File I/O operations
- `Combat/Persistence/SaveMigrator.cs` - Version migration logic
- `Combat/Persistence/SaveValidator.cs` - Post-load validation

**Key Features:**
- Save to JSON file with compression option
- Load with version check and migration
- Validate loaded state (no null references, valid IDs, resource bounds)
- Error reporting for corrupted saves

**Tests:** `Tests/Unit/SaveFileManagerTests.cs`
- Write/read cycle produces identical JSON
- Validation catches invalid state (negative HP, missing IDs)
- Migration transforms old version to current

**Acceptance Criteria:**
- [ ] Save files written to `user://saves/`
- [ ] Load validates before applying
- [ ] Migration path exists for version 1

---

### Phase 5: Save/Load Integration Tests

**Objective:** Create comprehensive tests for save/load during combat.

**Files to Create:**
- `Tests/Integration/SaveLoadIntegrationTests.cs`

**Test Scenarios:**
1. Save at turn start, reload, continue - final state matches continuous run
2. Save during reaction prompt, reload, reaction resolves correctly
3. Save with active surfaces, reload, surfaces tick correctly
4. Corrupted save file produces clear error
5. Hash verification: save → load → same state hash

**Tests:**
```csharp
[Fact]
public void SaveLoad_MidCombat_ProducesSameOutcome()
{
    // Run 5 turns
    // Capture snapshot
    // Continue to turn 10, record final hash
    // Restore to snapshot
    // Continue to turn 10, record final hash
    // Assert hashes match
}
```

**Acceptance Criteria:**
- [ ] All integration tests pass
- [ ] State hash verification works
- [ ] CI runs save/load tests

---

### Phase 6: Debug Console Implementation

**Objective:** Expand DebugCommands into a functional runtime console.

**Files to Modify:**
- `Tools/DebugCommands.cs` - Implement all stub methods

**Files to Create:**
- `Tools/DebugConsole.cs` - Command parser and execution
- `Tools/DebugConsoleUI.cs` - UI overlay (Godot Control node)
- `Tools/DebugConsoleUI.tscn` - Console scene

**Commands to Implement:**
- `spawn <combatant_id> <x> <y> <z> <faction>` - Spawn unit
- `kill [target]` - Kill current target or selected
- `damage <amount> [target]` - Apply damage
- `heal <amount> [target]` - Apply healing
- `status <status_id> [target]` - Apply status
- `surface <type> <x> <y> <z>` - Spawn surface
- `cooldown reset [ability]` - Reset cooldowns
- `initiative <combatant> <value>` - Set initiative
- `skip` - Skip current turn
- `godmode [on|off]` - Toggle invincibility
- `fow [on|off]` - Toggle fog of war
- `los [on|off]` - Toggle LOS debug overlay

**Tests:** `Tests/Unit/DebugConsoleTests.cs`
- Command parsing works for all commands
- Invalid commands produce clear errors
- Commands emit correct events

**Acceptance Criteria:**
- [ ] Console toggled via debug key (F12 or `)
- [ ] All commands implemented
- [ ] Commands log their effects

---

### Phase 7: Simulation Test Runner

**Objective:** Create a headless simulation runner for automated testing.

**Files to Create:**
- `Tests/Simulation/SimulationRunner.cs` - Core runner logic
- `Tests/Simulation/SimulationScenario.cs` - Scenario wrapper
- `Tests/Simulation/InvariantChecker.cs` - State invariant validation
- `Tests/Simulation/SimulationTests.cs` - xUnit test entry points

**Key Features:**
```csharp
public class SimulationRunner
{
    public SimulationResult Run(string scenarioPath, int seed, int maxTurns = 100);
}

public class SimulationResult
{
    public bool Completed { get; set; }
    public string TerminationReason { get; set; }
    public int TurnCount { get; set; }
    public string FinalStateHash { get; set; }
    public List<InvariantViolation> Violations { get; set; }
}

public class InvariantChecker
{
    public static List<InvariantViolation> CheckAll(CombatContext context);
    // HP never negative unless dead
    // Resources never exceed max
    // No duplicate status IDs where unique
    // Turn order contains valid combatant IDs
}
```

**Tests:** `Tests/Simulation/SimulationTests.cs`
- Run 100 combats with random seeds, assert no violations
- Same seed produces same final hash
- Detect infinite loops (max turn exceeded)

**Acceptance Criteria:**
- [ ] Simulation runner works headlessly
- [ ] Invariant checker catches violations
- [ ] CI runs simulation tests

---

### Phase 8: Deterministic Export Format

**Objective:** Create a stable export format for golden tests (no timestamps/GUIDs).

**Files to Modify:**
- `Combat/Services/CombatLog.cs` - Add `ExportDeterministic()` method
- `Combat/Services/CombatLogEntry.cs` - Add option to omit volatile fields

**Files to Create:**
- `Combat/Persistence/DeterministicExporter.cs` - Stable log/event export

**Key Features:**
```csharp
public class DeterministicExporter
{
    public string ExportLog(CombatLog log, bool omitVolatile = true);
    // Replaces EntryId with monotonic index
    // Omits Timestamp
    // Sorts by turn/action order
}
```

**Tests:** `Tests/Unit/DeterministicExporterTests.cs`
- Same combat produces identical export across runs
- Export is valid JSON
- Can be used for golden comparison

**Acceptance Criteria:**
- [ ] Deterministic export stable across runs
- [ ] No timestamps or GUIDs in output
- [ ] Works for both log and event history

---

### Phase 9: Scenario-Based Test Suite

**Objective:** Create xUnit tests that load scenario files and validate outcomes.

**Files to Create:**
- `Tests/Simulation/ScenarioTestRunner.cs` - Scenario file loader for tests
- `Tests/Simulation/ScenarioRegressionTests.cs` - Regression test cases

**Scenarios to Create:**
- `Data/Scenarios/test_save_load.json` - Save/load verification
- `Data/Scenarios/test_status_tick.json` - Status duration testing
- `Data/Scenarios/test_surface_transform.json` - Surface interaction
- `Data/Scenarios/test_reaction_chain.json` - Reaction ordering
- `Data/Scenarios/test_ai_decisions.json` - AI choice verification

**Tests:**
```csharp
[Theory]
[MemberData(nameof(GetScenarioFiles))]
public void Scenario_RunsWithoutViolations(string scenarioPath)
{
    var result = _runner.Run(scenarioPath, seed: 12345, maxTurns: 50);
    Assert.Empty(result.Violations);
    Assert.True(result.Completed);
}
```

**Acceptance Criteria:**
- [ ] All test scenarios pass
- [ ] Scenarios exercise major subsystems
- [ ] Easy to add new scenarios

---

### Phase 10: Performance Profiling Harness

**Objective:** Create profiling infrastructure for performance-critical operations.

**Files to Create:**
- `Tools/Profiling/ProfilerHarness.cs` - Profiling infrastructure
- `Tools/Profiling/ProfilerMetrics.cs` - Metrics collection
- `Tests/Performance/PerformanceBenchmarks.cs` - Benchmark tests

**Operations to Profile:**
- LOS queries (raycast sets)
- AoE target collection
- AI decision making (full turn)
- Surface tick/update
- Status tick processing
- State hash calculation

**Benchmark Targets (from Master TODO):**
- 20+ units on field
- Multiple overlapping surfaces
- Heavy reaction usage

**Key Implementation:**
```csharp
public class ProfilerHarness
{
    public ProfilerMetrics Measure(string operationName, Action operation, int iterations = 100);
}

public class ProfilerMetrics
{
    public double MeanMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MaxMs { get; set; }
}
```

**Tests:** `Tests/Performance/PerformanceBenchmarks.cs`
- LOS query < 1ms average for 20 units
- AI decision < 100ms per turn
- Full tick < 16ms (60fps target)

**Acceptance Criteria:**
- [ ] Profiler harness works in CI
- [ ] Benchmarks produce numeric results
- [ ] Results logged for tracking

---

### Phase 11: Benchmark CI Integration

**Objective:** Wire benchmarks into CI pipeline with regression detection.

**Files to Create:**
- `scripts/ci-benchmark.sh` - Benchmark runner script
- `.github/workflows/benchmark.yml` (if using GitHub Actions) - or equivalent

**Files to Modify:**
- `scripts/ci-test.sh` - Add benchmark execution option

**Key Features:**
- Run benchmarks as part of CI
- Store results in artifact/file
- Fail if regression exceeds threshold (e.g., 20% slower)
- Optional: post results as PR comment

**Acceptance Criteria:**
- [ ] `scripts/ci-benchmark.sh` runs all benchmarks
- [ ] Results written to JSON file
- [ ] Regression detection works

---

### Phase 12: Editor Plugin Foundation

**Objective:** Create the foundation for Godot editor tools.

**Files to Create:**
- `Editor/QDNDEditorPlugin.cs` - Main plugin entry point
- `Editor/QDNDEditorPlugin.cfg` - Plugin config

**Key Features:**
- Plugin loads in Godot editor
- Adds "QDND" menu item
- Sets up dock/panel infrastructure
- Plugin can be enabled/disabled via Project Settings

**Tests:** (Manual verification in editor)
- Plugin appears in Plugins menu
- Can be enabled/disabled
- No errors on load

**Acceptance Criteria:**
- [ ] Plugin registered with Godot
- [ ] Loads without errors
- [ ] Menu item visible

---

### Phase 13: Data Inspector Panel

**Objective:** Create an editor panel for viewing/editing ability and status definitions.

**Files to Create:**
- `Editor/DataInspectorDock.cs` - Main dock UI
- `Editor/AbilityEditor.cs` - Ability editing interface
- `Editor/StatusEditor.cs` - Status editing interface

**Key Features:**
- List all abilities/statuses from Data/ folders
- View/edit properties in inspector-like UI
- Save changes back to JSON
- Validation feedback (missing fields, invalid values)

**Acceptance Criteria:**
- [ ] Dock visible in Godot editor
- [ ] Lists all data files
- [ ] Edits save correctly

---

### Phase 14: Scenario Editor Panel

**Objective:** Create visual scenario editing with spawn point placement.

**Files to Create:**
- `Editor/ScenarioEditorDock.cs` - Scenario editing UI
- `Editor/SpawnPointGizmo.cs` - 3D gizmo for spawn points

**Key Features:**
- Load/save scenario JSON
- Drag spawn points in 3D viewport
- Set faction, combatant ID, position
- Preview layout in editor

**Acceptance Criteria:**
- [ ] Spawn points editable in 3D
- [ ] Changes save to JSON
- [ ] Preview shows unit icons

---

### Phase 15: Final Verification and Documentation

**Objective:** Run CI gates, update documentation, mark phase complete.

**Tasks:**
1. Run `scripts/ci-build.sh` - verify 0 errors, 0 warnings
2. Run `scripts/ci-test.sh` - verify all tests pass
3. Run `scripts/ci-benchmark.sh` - verify benchmarks complete
4. Create `docs/PHASE_E_GUIDE.md` - comprehensive API reference
5. Update `READY_TO_START.md` - mark Phase E complete
6. Update `AGENTS-MASTER-TO-DO.md` - check off Phase E items

**Documentation Content:**
- Save/Load API usage
- Debug console commands
- Simulation runner usage
- Editor plugin installation
- Benchmark interpretation

**Acceptance Criteria:**
- [ ] CI build passes
- [ ] All tests pass (unit, integration, simulation)
- [ ] Benchmarks complete without regression
- [ ] PHASE_E_GUIDE.md created
- [ ] READY_TO_START.md updated

---

## Open Questions

1. **Save File Location?**
   - **Option A:** `user://saves/` (Godot user data)
   - **Option B:** Project-relative `saves/` folder
   - **Recommendation:** Option A for user saves, B for test golden files

2. **Save File Format?**
   - **Option A:** Raw JSON (human-readable, larger)
   - **Option B:** Compressed JSON (smaller, less debug-friendly)
   - **Recommendation:** JSON for dev, compressed for release

3. **Benchmark Thresholds?**
   - Should be configurable per operation
   - Start with 20% regression threshold
   - Tune based on observed variance

## Risks & Mitigation

- **Risk:** ResolutionStack serialization complexity (mid-reaction state)
  - **Mitigation:** Design simple StackItemSnapshot; test thoroughly with reaction scenarios

- **Risk:** Editor plugin stability with Godot 4.5 C#
  - **Mitigation:** Keep plugin simple; use battle-tested EditorPlugin patterns

- **Risk:** Benchmark variance in CI environment
  - **Mitigation:** Use percentile-based thresholds (P95); warm-up runs; dedicated test resources

- **Risk:** Save format changes break old saves
  - **Mitigation:** Version field from day 1; migration system in Phase 4

## Success Criteria

- [ ] Save/load mid-combat works without errors
- [ ] Deterministic replay verified (same seed = same outcome after load)
- [ ] Debug console functional with all commands
- [ ] Simulation tests pass with 100 random seeds
- [ ] Performance benchmarks meet 60fps targets
- [ ] Editor plugin loads and provides data inspection
- [ ] All Phase E items in Master TODO can be checked off

## Notes for Atlas

- **Testbed-first rule applies:** Every new system must have non-visual verification
- **RollIndex is critical:** Without it, saves cannot be deterministic
- **Start with snapshots:** Phases 1-5 form the core; later phases build on top
- **Editor plugin is optional:** If GodotSharp issues arise, defer to Phase F
- **CI scripts must be idempotent:** Can run multiple times without side effects
- **Use existing patterns:** Follow DataRegistry JSON loading patterns for save files

## File Structure Summary

```
Combat/Persistence/
    CombatSnapshot.cs
    CombatantSnapshot.cs
    SurfaceSnapshot.cs
    StatusSnapshot.cs
    CombatSaveService.cs
    SaveFileManager.cs
    SaveMigrator.cs
    SaveValidator.cs
    DeterministicExporter.cs

Tools/
    DebugConsole.cs
    DebugConsoleUI.cs
    DebugConsoleUI.tscn
    Profiling/
        ProfilerHarness.cs
        ProfilerMetrics.cs

Tests/
    Unit/
        CombatSnapshotTests.cs
        DiceRollerTests.cs (update)
        CombatSaveServiceTests.cs
        SaveFileManagerTests.cs
        DebugConsoleTests.cs
        DeterministicExporterTests.cs
    Integration/
        SaveLoadIntegrationTests.cs
    Simulation/
        SimulationRunner.cs
        SimulationScenario.cs
        InvariantChecker.cs
        SimulationTests.cs
        ScenarioTestRunner.cs
        ScenarioRegressionTests.cs
    Performance/
        PerformanceBenchmarks.cs

Editor/
    QDNDEditorPlugin.cs
    QDNDEditorPlugin.cfg
    DataInspectorDock.cs
    AbilityEditor.cs
    StatusEditor.cs
    ScenarioEditorDock.cs
    SpawnPointGizmo.cs

scripts/
    ci-benchmark.sh

docs/
    PHASE_E_GUIDE.md

Data/Scenarios/
    test_save_load.json
    test_status_tick.json
    test_surface_transform.json
    test_reaction_chain.json
    test_ai_decisions.json
```
