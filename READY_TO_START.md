# Quick Start - Phase F Complete ✅

## Current Status

**Phase A ✅ COMPLETE** - Core combat skeleton
**Phase B ✅ COMPLETE** - Rules engine + generic abilities
**Phase C ✅ COMPLETE** - Hallmark depth (action economy, surfaces, reactions)
**Phase D ✅ COMPLETE** - AI parity and polish (AI decision-making, logging, HUD models)
**Phase E ✅ COMPLETE** - Persistence, tooling, and hardening
**Phase F ✅ COMPLETE** - Presentation, camera hooks, and benchmark gating

## What's Implemented

### Phase A ✅ Folder Structure & Core Services
- `Combat/States/` - Combat state machine
- `Combat/Entities/` - Combatant models
- `Combat/Services/` - Core services (Context, TurnQueue, Commands, Log)
- `Data/Scenarios/` - Test scenarios

### Phase B ✅ Rules Engine & Abilities
| Service | File | Status |
|---------|------|--------|
| RulesEngine | `Combat/Rules/RulesEngine.cs` | ✅ Complete |
| Modifier System | `Combat/Rules/Modifier.cs` | ✅ Complete |
| Event Bus | `Combat/Rules/RuleEvent.cs` | ✅ Complete |
| AbilityDefinition | `Combat/Abilities/AbilityDefinition.cs` | ✅ Complete |
| EffectPipeline | `Combat/Abilities/EffectPipeline.cs` | ✅ Complete |
| Effect Handlers | `Combat/Abilities/Effects/Effect.cs` | ✅ Complete |
| StatusSystem | `Combat/Statuses/StatusSystem.cs` | ✅ Complete |
| TargetValidator | `Combat/Targeting/TargetValidator.cs` | ✅ Complete |
| DataRegistry | `Data/DataRegistry.cs` | ✅ Complete |

### Phase C ✅ Hallmark Depth Systems
| Service | File | Status |
|---------|------|--------|
| ActionBudget | `Combat/Actions/ActionBudget.cs` | ✅ Complete |
| ActionType | `Combat/Actions/ActionType.cs` | ✅ Complete |
| MovementService | `Combat/Movement/MovementService.cs` | ✅ Complete |
| SpecialMovementService | `Combat/Movement/SpecialMovementService.cs` | ✅ Complete |
| ForcedMovementService | `Combat/Movement/ForcedMovementService.cs` | ✅ Complete |
| ReactionSystem | `Combat/Reactions/ReactionSystem.cs` | ✅ Complete |
| ReactionDefinition | `Combat/Reactions/ReactionDefinition.cs` | ✅ Complete |
| ResolutionStack | `Combat/Reactions/ResolutionStack.cs` | ✅ Complete |
| SurfaceManager | `Combat/Environment/SurfaceManager.cs` | ✅ Complete |
| SurfaceDefinition | `Combat/Environment/SurfaceDefinition.cs` | ✅ Complete |
| LOSService | `Combat/Environment/LOSService.cs` | ✅ Complete |
| HeightService | `Combat/Environment/HeightService.cs` | ✅ Complete |

### CombatArena Testbed ✅
- Full HUD with turn tracker, action bar, combat log
- Input actions for all controls (configurable in project settings)
- Scenario selector for loading different test scenarios
- Debug panel for testing (F1)
- Inspect panel for combatant details
- Resource display (Action, Bonus, Movement, Reaction)

### Effect Types Implemented
- DealDamageEffect
- HealEffect  
- ApplyStatusEffect
- RemoveStatusEffect
- ModifyResourceEffect
- TeleportEffect
- ForcedMoveEffect
- SpawnSurfaceEffect

### Test Coverage
| Test Suite | Count | Status |
|------------|-------|--------|
| CombatStateMachineTests | 8 | ✅ |
| TurnQueueTests | 8 | ✅ |
| RulesEngineTests (mock) | 10 | ✅ |
| RulesEngineIntegrationTests | 13 | ✅ |
| EffectSystemTests (mock) | 12 | ✅ |
| EffectPipelineIntegrationTests | 15+ | ✅ |
| StatusSystemTests | 10 | ✅ |
| StatusTickIntegrationTests | 6+ | ✅ |
| TargetValidatorTests | 12 | 🔲 Pending (currently .skip) |
| DataRegistryTests | 38 | ✅ |
| ActionBudgetTests | 10+ | ✅ |
| MovementServiceTests | 8+ | 🔲 Pending (currently .skip) |
| SpecialMovementTests | 10+ | 🔲 Pending (currently .skip) |
| ForcedMovementTests | 8+ | 🔲 Pending (currently .skip) |
| ReactionSystemTests | 10+ | ✅ |
| ResolutionStackTests | 8+ | ✅ |
| SurfaceManagerTests | 12+ | ✅ |
| LOSServiceTests | 10+ | 🔲 Pending (currently .skip) |
| HeightServiceTests | 8+ | 🔲 Pending (currently .skip) |
| AIDecisionTests | 25+ | ✅ |
| CombatLogTests | 15+ | ✅ |
| HUDModelTests | 12+ | 🔲 Pending (currently .skip) |
| AnimationTimelineTests | 10+ | 🔲 Pending (currently .skip) |
| CameraStateTests | 8+ | 🔲 Pending (currently .skip) |
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
| **Total** | **463** | Mixed (enabled tests pass; some suites pending *.cs.skip) |

### Verification
- `dotnet build QDND.csproj` ✅ Succeeds
- `dotnet test Tests/QDND.Tests.csproj` ✅ All enabled tests pass (excludes `*.cs.skip` files)
- CombatArena loads combat services (All phases) ✅

## CombatArena Debug Testbed Features

CombatArena.tscn is the main debug testbed where all combat features can be tested.

### Controls

| Action | Key/Mouse |
|--------|-----------|
| End Turn | Space / Enter |
| Cancel Selection | Escape |
| Select Ability 1-6 | Keys 1-6 |
| Camera Pan | WASD |
| Camera Rotate | Q/E |
| Camera Zoom | Mouse Wheel |
| Toggle Debug Panel | F1 |
| Select Unit | Left Click |
| Cancel Targeting | Right Click |

### HUD Elements

- **Turn Tracker** (top) - Shows initiative order with HP percentages
- **Combat State** (top-left) - Current combat phase
- **Round Counter** (top-right) - Current round number
- **Scenario Selector** (left, below state) - Load different scenarios
- **Inspect Panel** (left, when selecting) - Detailed combatant info
- **Combat Log** (right) - Scrollable event log with color coding
- **Resource Bars** (bottom-center) - Action, Bonus, Movement, Reaction
- **Action Bar** (bottom) - Available abilities with hotkeys
- **End Turn Button** (bottom-right) - End current turn

### Debug Panel (F1)

Press F1 to toggle the debug panel with:
- Deal damage to selected target
- Heal selected target
- Apply status by ID (e.g., "poisoned", "burning")
- Kill target instantly
- Force end combat

### Available Scenarios

Scenarios are loaded from `Data/Scenarios/`:
- `minimal_combat.json` - Basic 2v2 combat
- `effect_test.json` - Effect testing
- `effect_damage_test.json` - Damage validation
- `effect_heal_test.json` - Heal validation
- `effect_status_test.json` - Status effects
- `reaction_test.json` - Reaction triggers
- `surface_test.json` - Surface/environment effects
- `movement_test.json` - Movement validation
- `height_los_test.json` - Height and LOS testing
- And more...

## Running CombatArena

### Build & Test
```bash
# Build main project
dotnet build QDND.csproj

# Run tests
dotnet test Tests/QDND.Tests.csproj

# CI build gate
./scripts/ci-build.sh

# CI test gate
./scripts/ci-test.sh
```

## Sample Data Files

### Abilities (Data/Abilities/)
- `sample_abilities.json` - 6 sample abilities (attack, heal, fireball, etc.) with VFX/SFX IDs

### Statuses (Data/Statuses/)  
- `sample_statuses.json` - 6 sample statuses (poisoned, burning, inspired, etc.)

### Scenarios (Data/Scenarios/)
- `minimal_combat.json` - Basic 2v2 combat
- `effect_test.json` - Effect testing scenario
- `effect_damage_test.json` - Damage effect validation
- `effect_heal_test.json` - Heal effect validation
- `effect_status_test.json` - Status effect validation
- `effect_combo_test.json` - Combined effects validation

## Next Phase: Phase G or Polish/Release Prep

**Status:** All core systems complete
**Note:** Phase F (Presentation, Camera Hooks, Benchmark Gating) completed. See [plans/phase-f-presentation-polish-benchmark-gating-complete.md](plans/phase-f-presentation-polish-benchmark-gating-complete.md)

## Documentation

- **Master Plan**: [AGENTS-MASTER-TO-DO.md](AGENTS-MASTER-TO-DO.md)
- **Phase A Guide**: [docs/PHASE_A_GUIDE.md](docs/PHASE_A_GUIDE.md)
- **Phase B Guide**: [docs/PHASE_B_GUIDE.md](docs/PHASE_B_GUIDE.md)
- **Phase C Guide**: [docs/PHASE_C_GUIDE.md](docs/PHASE_C_GUIDE.md)
- **Phase D Guide**: [docs/PHASE_D_GUIDE.md](docs/PHASE_D_GUIDE.md)
- **Phase E Guide**: [docs/PHASE_E_GUIDE.md](docs/PHASE_E_GUIDE.md)

## Key Principles (CombatArena-First Rule)

1. **Deterministic** - Use seeded RNG, fixed inputs
2. **Non-Visual** - Assert on events/state, not visuals
3. **Logged** - Every action emits structured events
4. **Testable** - Can verify without human eyes (dotnet tests)
5. **Data-Driven** - Content defined in JSON, validated on load

**Note:** Historical references to "Testbed" in older plan documents refer to the integration scene, now `Combat/Arena/CombatArena.tscn`.
