# Quick Start - First Ticket Ready ✅

## Setup Complete

All necessary infrastructure is in place for Phase A development.

## What's Ready

### ✅ Folder Structure
- `Combat/States/` - Combat state machine
- `Combat/Entities/` - Combatant models
- `Combat/Services/` - Core services (Context, TurnQueue, Commands, Log)
- `Data/Scenarios/` - Test scenarios
- `Tests/Unit/` - Unit tests
- `Tests/Simulation/` - Integration tests

### ✅ Core Infrastructure
- **CombatContext** (`Combat/Services/CombatContext.cs`)
  - Service locator/DI pattern
  - Service registration and lookup
  
- **TestbedBootstrap** (`Scripts/Tools/TestbedBootstrap.cs`)
  - Initializes CombatContext
  - Registers services
  - Loads scenarios
  - Emits diagnostic events
  
- **Testbed.tscn** (`Scripts/Tools/Testbed.tscn`)
  - Integration scene with TestbedBootstrap attached
  - Ready for scenario testing

### ✅ Project Status
- Compiles successfully ✅
- No errors or warnings ✅
- Ready for first ticket implementation ✅

## First Ticket: Implement Phase A Skeleton

See [docs/PHASE_A_GUIDE.md](docs/PHASE_A_GUIDE.md) for complete implementation guide.

### Quick Checklist

Create these files:
- [ ] `Combat/States/CombatStateMachine.cs` - State machine with transitions
- [ ] `Combat/Services/TurnQueueService.cs` - Initiative and turn order
- [ ] `Combat/Services/CommandService.cs` - Command validation and execution
- [ ] `Combat/Services/CombatLog.cs` - Event logging and state hashing
- [ ] `Combat/Entities/Combatant.cs` - Basic entity model
- [ ] `Data/ScenarioLoader.cs` - Load JSON scenarios
- [ ] `Data/Scenarios/minimal_combat.json` - Test scenario

Add tests:
- [ ] `Tests/Unit/TurnQueueTests.cs`
- [ ] `Tests/Unit/CombatStateMachineTests.cs`
- [ ] `Tests/Simulation/PhaseAIntegrationTest.cs`

Update:
- [ ] `Scripts/Tools/TestbedBootstrap.cs` - Register new services

## Running the Testbed

### In Godot Editor
1. Open project in Godot
2. Open `Scripts/Tools/Testbed.tscn`
3. Run scene (F6)
4. Check console output for diagnostic logs

### Build & Test
```bash
# Build
dotnet build

# Run tests (once created)
dotnet test

# Future: Headless mode
godot --headless --path . Scripts/Tools/Testbed.tscn
```

## Key Principles (Testbed-First Rule)

1. **Deterministic** - Use seeded RNG, fixed inputs
2. **Non-Visual** - Assert on events/state, not visuals
3. **Logged** - Every action emits structured events
4. **Testable** - Can verify without human eyes

## Success Criteria for First Ticket

Testbed.tscn runs and prints:
- ✅ Services registered
- ✅ Scenario loaded with 2+ combatants
- ✅ State transitions logged
- ✅ Turn order established
- ✅ Turns execute (EndTurn commands work)
- ✅ Deterministic state hash at end
- ✅ Clean exit

## Documentation

- **Master Plan**: [AGENTS-MASTER-TO-DO.md](AGENTS-MASTER-TO-DO.md)
- **Phase A Guide**: [docs/PHASE_A_GUIDE.md](docs/PHASE_A_GUIDE.md)
- **Scenarios Info**: [Data/Scenarios/README.md](Data/Scenarios/README.md)
- **Testing Info**: [Tests/README.md](Tests/README.md)

## Ready to Code! 🚀

The project is fully prepared. Start with the Phase A guide and implement the combat skeleton.
