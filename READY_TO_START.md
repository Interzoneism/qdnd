# Quick Start - Phase A Complete ✅

## Current Status

**Phase A is COMPLETE.** All core combat skeleton systems are implemented and tested.

## What's Implemented

### ✅ Folder Structure
- `Combat/States/` - Combat state machine
- `Combat/Entities/` - Combatant models
- `Combat/Services/` - Core services (Context, TurnQueue, Commands, Log)
- `Data/Scenarios/` - Test scenarios
- `Tests/Unit/` - Unit tests (16 tests passing)

### ✅ Core Services
| Service | File | Status |
|---------|------|--------|
| CombatContext | `Combat/Services/CombatContext.cs` | ✅ Complete |
| CombatStateMachine | `Combat/States/CombatStateMachine.cs` | ✅ Complete |
| TurnQueueService | `Combat/Services/TurnQueueService.cs` | ✅ Complete |
| CommandService | `Combat/Services/CommandService.cs` | ✅ Complete |
| CombatLog | `Combat/Services/CombatLog.cs` | ✅ Complete |
| ScenarioLoader | `Data/ScenarioLoader.cs` | ✅ Complete |

### ✅ Entity Model
| Entity | File | Status |
|--------|------|--------|
| Combatant | `Combat/Entities/Combatant.cs` | ✅ Complete |

### ✅ Test Infrastructure
| Component | File | Status |
|-----------|------|--------|
| TestbedBootstrap | `Scripts/Tools/TestbedBootstrap.cs` | ✅ Complete |
| Testbed Scene | `Scripts/Tools/Testbed.tscn` | ✅ Complete |
| State Machine Tests | `Tests/Unit/CombatStateMachineTests.cs` | ✅ 8 tests |
| Turn Queue Tests | `Tests/Unit/TurnQueueTests.cs` | ✅ 8 tests |
| Test Scenario | `Data/Scenarios/minimal_combat.json` | ✅ Complete |

### ✅ Verification
- `dotnet build QDND.csproj` ✅ Succeeds
- `dotnet test Tests/` ✅ 16/16 tests pass
- Testbed prints deterministic state hash ✅

## Running the Testbed

### Build & Test
```bash
# Build main project
dotnet build QDND.csproj

# Run unit tests
cd Tests && dotnet test

# In Godot Editor (optional)
# Open Scripts/Tools/Testbed.tscn and run (F6)
```

## Next Phase: Phase B

See [docs/PHASE_B_GUIDE.md](docs/PHASE_B_GUIDE.md) (to be created) for implementation guide.

### Phase B Scope
- Rule queries/modifiers/events
- Ability definitions + effect pipeline
- Targeting + AoE preview (geometry + validation)
- Damage/heal/status basics

## Documentation

- **Master Plan**: [AGENTS-MASTER-TO-DO.md](AGENTS-MASTER-TO-DO.md)
- **Phase A Guide**: [docs/PHASE_A_GUIDE.md](docs/PHASE_A_GUIDE.md) ✅ Complete
- **Scenarios Info**: [Data/Scenarios/README.md](Data/Scenarios/README.md)
- **Testing Info**: [Tests/README.md](Tests/README.md)

## Key Principles (Testbed-First Rule)

1. **Deterministic** - Use seeded RNG, fixed inputs
2. **Non-Visual** - Assert on events/state, not visuals
3. **Logged** - Every action emits structured events
4. **Testable** - Can verify without human eyes
