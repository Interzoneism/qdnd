# Combat Tests

This folder contains automated tests for the combat system.

## Current Status: ✅ Phase A Complete

Test infrastructure is implemented with 16 passing tests.

## Structure

```
Tests/
├── QDND.Tests.csproj    # xUnit test project
├── Unit/                 # Unit tests
│   ├── CombatStateMachineTests.cs  # 8 tests
│   └── TurnQueueTests.cs           # 8 tests
└── Simulation/           # Integration tests (future)
```

## Running Tests

```bash
# Build test project
cd Tests && dotnet build

# Run all tests
cd Tests && dotnet test

# Run with verbose output
cd Tests && dotnet test --logger "console;verbosity=detailed"
```

## Test Coverage

### CombatStateMachineTests (8 tests)
- `InitialState_IsNotInCombat` - Initial state verification
- `CanTransition_FromNotInCombat_ToCombatStart` - Valid transitions
- `CannotTransition_FromNotInCombat_ToTurnStart` - Invalid transition rejection
- `ValidCombatSequence_Works` - Full combat sequence
- `ActionExecution_CanReturnToDecision` - Multi-action turns
- `TransitionHistory_TracksAllTransitions` - History tracking
- `InvalidTransition_DoesNotChangeState` - State protection

### TurnQueueTests (8 tests)
- `InitialState_IsEmpty` - Empty queue state
- `AddCombatant_AppearsInQueue` - Adding combatants
- `TurnOrder_SortedByInitiativeDescending` - Initiative sorting
- `InitiativeTiebreaker_BreaksTies` - Tie-breaker logic
- `AdvanceTurn_MovesToNextCombatant` - Turn progression
- `AdvanceTurn_WrapsToNewRound` - Round wrapping
- `DeadCombatant_ExcludedFromTurnOrder` - Dead filtering
- `DeterministicOrder_WithSameInitiativeAndTiebreaker` - Determinism

## Testing Framework

- **xUnit** for unit tests
- Tests are independent of Godot runtime
- Use isolated test versions of classes to avoid Godot dependencies

## Writing Tests

Follow these principles:
- **Deterministic**: Use fixed RNG seeds
- **Non-visual**: Assert on state/events, not visual output
- **Isolated**: Each test should be independent
- **Fast**: Unit tests should complete in milliseconds
- **Documented**: Include clear failure messages

## Future Tests (Phase B+)

### Planned Unit Tests
- Rules engine calculations
- Damage mitigation ordering
- Status duration logic
- Action economy validation

### Planned Simulation Tests
- Deterministic combat runs
- Multi-step combat sequences
- Invariant checking (HP bounds, resource limits)
- Save/load equivalence tests
