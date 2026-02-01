# Phase E Implementation Guide: Persistence, Tooling, and Hardening

## Overview

Phase E establishes the complete infrastructure for persistence, developer tooling, and automated testing. This phase enables save/load functionality, debug console commands, deterministic replay, performance profiling, and editor plugins for content creation.

## Objectives

- [x] Combat State Snapshot Model (serializable DTOs)
- [x] Roll Index Tracking (deterministic RNG replay)
- [x] State Capture Service (orchestrated save/load)
- [x] File I/O and Validation (secure file handling)
- [x] Save/Load Integration Tests (round-trip verification)
- [x] Debug Console Implementation (runtime commands)
- [x] Simulation Test Runner (headless testing)
- [x] Deterministic Export Format (golden test support)
- [x] Scenario-Based Test Suite (regression detection)
- [x] Performance Profiling Harness (benchmarks)
- [x] Benchmark CI Integration (automated perf gates)
- [x] Editor Plugin Foundation (Godot integration)
- [x] Data Inspector Panel (ability/status editing)
- [x] Scenario Editor Panel (spawn point placement)

## Architecture

### Persistence System

```
Combat/Persistence/
├── CombatSnapshot.cs          # Root snapshot container
├── CombatantSnapshot.cs       # Per-combatant state
├── StatusSnapshot.cs          # Active status instances
├── CooldownSnapshot.cs        # Ability cooldowns
├── SurfaceSnapshot.cs         # Surface instances
├── PropSnapshot.cs            # Prop objects
├── ReactionPromptSnapshot.cs  # Pending reaction prompts
├── StackItemSnapshot.cs       # Resolution stack items
├── CombatSaveService.cs       # Orchestrates capture/restore
├── SaveFileManager.cs         # File I/O with security
├── SaveValidator.cs           # Schema validation
├── SaveMigrator.cs            # Version migration
└── DeterministicExporter.cs   # Golden test export
```

### Debug Console

```
Tools/
├── DebugConsole.cs            # Command parser and executor
└── NewDebugCommands.cs        # Extended command set

Commands:
- help [command]    - Show help
- clear            - Clear console
- history          - Show command history
- spawn <id> <team> [x] [y] [z]
- kill <id>
- damage <id> <amount>
- heal <id> <amount>
- setstatus <id> <statusId> [stacks] [duration]
- clearstatus <id> [statusId]
- surface <surfaceId> <x> <y> <z> <radius>
- clearsurfaces
- setcooldown <combatantId> <abilityId> <turns>
- resetcooldowns <combatantId>
- godmode [id]     - Toggle invulnerability
- endturn          - Force turn end
```

### Simulation Framework

```
Tests/Simulation/
├── SimulationRunner.cs        # Headless combat runner
├── SimulationState.cs         # Tracked simulation state
├── SimulationScenario.cs      # Scenario definition
├── InvariantChecker.cs        # Rule enforcement
├── ScenarioTestRunner.cs      # xUnit integration
└── ScenarioRegressionTests.cs # Regression test cases
```

### Performance Profiling

```
Tools/Profiling/
├── ProfilerHarness.cs         # Measurement infrastructure
├── ProfilerMetrics.cs         # Statistical results
└── BenchmarkSuite.cs          # Benchmark definitions

Tests/Performance/
├── PerformanceBenchmarks.cs   # Benchmark tests
├── BenchmarkReporter.cs       # Results formatting
├── CIBenchmarkRunner.cs       # CI integration
└── CIBenchmarkTests.cs        # Regression detection
```

### Editor Integration

```
addons/qdnd_tools/
├── plugin.cfg                 # Godot plugin manifest
├── QDNDEditorPlugin.cs        # Main plugin entry
├── DataInspectorDock.cs       # Data file browser
├── AbilityEditor.cs           # Ability definition UI
└── StatusEditor.cs            # Status definition UI

Editor/
├── EditorHelpers.cs           # JSON utilities
├── DataDefinitions.cs         # Editable data models
├── ScenarioEditorDock.cs      # Scenario editing UI
└── SpawnPointGizmo.cs         # 3D spawn visualization
```

## API Reference

### Saving Combat State

```csharp
// Capture current state
var saveService = new CombatSaveService(
    rulesEngine, turnQueue, stateMachine, 
    statusSystem, surfaceManager, resolutionStack, 
    effectPipeline, reactionPromptStore);

CombatSnapshot snapshot = saveService.CaptureSnapshot(combatants);

// Save to file
var fileManager = new SaveFileManager();
string path = fileManager.SaveSnapshot(snapshot, "quicksave");
```

### Loading Combat State

```csharp
// Load from file
var fileManager = new SaveFileManager();
CombatSnapshot snapshot = fileManager.LoadSnapshot("quicksave");

// Validate
var validator = new SaveValidator();
if (!validator.Validate(snapshot, out var errors))
{
    // Handle validation errors
}

// Restore state
saveService.RestoreSnapshot(snapshot, combatants);
```

### Deterministic Replay

```csharp
// RNG state is tracked automatically
var dice = RulesEngine.DiceRoller;
dice.Seed = 12345;

// After some rolls...
int rollIndex = dice.RollIndex;

// Later, restore exact RNG state
dice.SetState(12345, rollIndex);
// Future rolls will be identical
```

### DeterministicExporter for Golden Tests

```csharp
var exporter = new DeterministicExporter();

// Export with stable ordering (for diff comparison)
string json = exporter.ExportToJson(snapshot);

// Import for comparison
CombatSnapshot loaded = exporter.ImportFromJson(json);
```

### Debug Console Usage

```csharp
var console = new DebugConsole(combatContext);

// Execute commands
console.Execute("spawn orc 2 10 0 10");
console.Execute("damage orc 25");
console.Execute("setstatus orc poisoned 3 5");
console.Execute("godmode player");
```

### Simulation Testing

```csharp
var runner = new SimulationRunner();

// Run scenario
var result = runner.Run("Data/Scenarios/test_save_load.json", 
    seed: 12345, 
    maxTurns: 50);

// Check results
Assert.Empty(result.Violations);
Assert.True(result.Completed);
```

### Profiling Operations

```csharp
var harness = new ProfilerHarness();

// Measure operation (with warmup)
var metrics = harness.Measure("LOS Query", () =>
{
    losService.HasLineOfSight(origin, target);
}, iterations: 100);

Console.WriteLine($"Mean: {metrics.MeanMs:F3}ms, P95: {metrics.P95Ms:F3}ms");
```

## Test Coverage

| Test Suite | Count | Status |
|------------|-------|--------|
| CombatSnapshotTests | 8 | ✅ |
| CombatSaveServiceTests | 8 | ✅ |
| SaveFileManagerTests | 10 | ✅ |
| SaveValidatorTests | 8 | ✅ |
| SaveMigratorTests | 6 | ✅ |
| SaveLoadIntegrationTests | 10 | ✅ |
| DiceRollerStateTests | 16 | ✅ |
| DebugConsoleTests | 11 | ✅ |
| SimulationRunnerTests | 10 | ✅ |
| DeterministicExporterTests | 9 | ✅ |
| ScenarioRegressionTests | 18 | ✅ |
| PerformanceBenchmarks | 8 | ✅ |
| CIBenchmarkTests | 6 | ✅ |
| EditorHelpersTests | 7 | ✅ |
| **Total** | **135+** | ✅ |

## Benchmark Targets

| Operation | Target | Status |
|-----------|--------|--------|
| LOS Query (20 units) | < 1ms avg | ✅ |
| AoE Collection | < 2ms avg | ✅ |
| AI Decision (per turn) | < 100ms | ✅ |
| Surface Tick | < 5ms | ✅ |
| Status Tick | < 2ms | ✅ |
| State Hash | < 1ms | ✅ |
| Full Tick (60fps) | < 16ms | ✅ |

## CI Integration

### Build Gate
```bash
./scripts/ci-build.sh
# Fails on any build error or warning
```

### Test Gate
```bash
./scripts/ci-test.sh
# Runs all unit, integration, and simulation tests
```

### Benchmark Gate
```bash
./scripts/ci-benchmark.sh
# Runs benchmarks, saves results, detects regression
```

## Editor Plugin Installation

1. Enable plugins in Godot: Project → Project Settings → Plugins
2. Find "QDND Tools" in the plugin list
3. Click "Enable"
4. Access via Editor → QDND menu

### Features
- **Data Inspector**: Browse and edit ability/status JSON files
- **Scenario Editor**: Visual combatant placement with spawn gizmos

## Save File Format

Save files use JSON with version tracking:

```json
{
  "version": 1,
  "turnOrder": ["knight", "goblin1", "goblin2"],
  "currentTurnIndex": 0,
  "roundNumber": 1,
  "initialSeed": 12345,
  "rollIndex": 47,
  "flowState": "PlayerTurn",
  "combatants": [...],
  "surfaces": [...],
  "statuses": [...],
  "prompts": [...],
  "stackItems": [],
  "cooldowns": [...]
}
```

## Migration System

When save format changes:

1. Increment `Version` in `CombatSnapshot`
2. Add migration method in `SaveMigrator`
3. Migrator auto-applies migrations in sequence

```csharp
// SaveMigrator handles version upgrades
var migrator = new SaveMigrator();
CombatSnapshot upgraded = migrator.Migrate(oldSnapshot);
```

## Key Principles

1. **Deterministic** - RNG state captured and restorable
2. **Secure** - Path traversal protection, validated inputs
3. **Versioned** - Migrations handle format evolution
4. **Testable** - Headless simulation, golden tests
5. **Profiled** - Benchmarks track performance over time
6. **Tooled** - Debug console for runtime inspection
7. **Integrated** - Editor plugins for content creation

## Documentation Links

- **Master Plan**: [AGENTS-MASTER-TO-DO.md](../AGENTS-MASTER-TO-DO.md)
- **Phase A Guide**: [PHASE_A_GUIDE.md](PHASE_A_GUIDE.md)
- **Phase B Guide**: [PHASE_B_GUIDE.md](PHASE_B_GUIDE.md)
- **Phase C Guide**: [PHASE_C_GUIDE.md](PHASE_C_GUIDE.md)
- **Phase D Guide**: [PHASE_D_GUIDE.md](PHASE_D_GUIDE.md)
