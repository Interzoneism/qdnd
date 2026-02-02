# Phase A Implementation Guide

## Overview

Phase A establishes the skeleton of the combat system that must compile and run headless. This is the foundation for all subsequent work.

## Objectives

- [x] Combat state machine + turn queue
- [x] Combatant model + resources
- [x] Basic move + end turn commands
- [x] Minimal UI model: turn tracker data + end turn command (UI nodes optional)
- [x] CombatArena: Loads a minimal scenario and prints deterministic event log + final state hash

## Current Status

✅ **Infrastructure Ready:**
- CombatContext service locator created
- CombatArena integrated for visual gameplay
- Folder structure established
- Project compiles successfully

## Next Steps (First Ticket)

### 1. Combat State Machine
**Location:** `Combat/States/CombatStateMachine.cs`

Create a state machine with these states:
- `NotInCombat` (default)
- `CombatStart` (setup, initiative)
- `TurnStart`
- `PlayerDecision` / `AIDecision`
- `ActionExecution`
- `TurnEnd`
- `RoundEnd`
- `CombatEnd`

**Requirements:**
- Explicit state transitions with logging
- Event emission for each transition
- Deterministic state progression
- Headless-testable (no visual dependencies)

**CombatArena Integration:**
- Register as service in CombatArena.RegisterServices()
- Add state transition assertions via tests

### 2. Turn Queue System
**Location:** `Combat/Services/TurnQueueService.cs`

**Features:**
- Initiative tracking
- Current turn pointer
- Next/previous turn navigation
- Round counter
- Turn order queries

**Requirements:**
- Deterministic ordering with tie-breakers
- Support for adding/removing combatants mid-combat
- Event emission on turn changes

### 3. Combatant Model
**Location:** `Combat/Entities/Combatant.cs`

**Components needed:**
- Base entity with ID and name
- Stats component (stub for now, just HP)
- Resources component (basic HP tracking)
- Faction/allegiance
- Initiative value

**Keep it minimal** - full component model comes in Phase B

### 4. Basic Commands
**Location:** `Combat/Services/CommandService.cs`

**Commands:**
- `EndTurnCommand` - finish current turn
- `MoveCommand` - stub that accepts position (validation in Phase C)

**Requirements:**
- Command validation (can this command execute now?)
- Command execution updates state
- All commands emit events

### 5. Minimal Scenario Loader
**Location:** `Data/ScenarioLoader.cs`

**Features:**
- Load JSON scenario files from `Data/Scenarios/`
- Spawn combatants with basic properties
- Initialize turn queue
- Set RNG seed

**Test Scenario:**
Create `Data/Scenarios/minimal_combat.json`:
```json
{
  "id": "minimal_combat",
  "seed": 42,
  "units": [
    {"id": "ally_1", "faction": "player", "hp": 50, "initiative": 15},
    {"id": "enemy_1", "faction": "hostile", "hp": 30, "initiative": 12}
  ]
}
```

### 6. Event Log System
**Location:** `Combat/Services/CombatLog.cs`

**Features:**
- Record all events with timestamps
- Query events by type/turn/round
- Export log to JSON for assertions
- Calculate state hash for reproducibility

### 7. Automated Validation
**Updates to:** `Tests/Simulation/ScenarioRegressionTests.cs`

**Add:**
- Load minimal_combat.json by default
- Run combat through several turns automatically
- Print deterministic event log
- Calculate and print final state hash
- Success/failure assertion output

## Success Criteria

When Phase A is complete, the automated tests should:

1. **Compile without errors** ✅ (already verified)
2. **Run headlessly via dotnet** (`./scripts/ci-test.sh`)
3. **Print structured log** showing:
   - Services registered
   - Scenario loaded (combatant count, seed)
   - State transitions (NotInCombat → CombatStart → TurnStart → etc.)
   - Turn sequence (Ally_1 turn → Enemy_1 turn → Round 2 → etc.)
   - Commands executed (EndTurn, basic moves)
   - Final state hash (deterministic)
4. **Exit cleanly** with success indicator

**Humans can also run:** `Combat/Arena/CombatArena.tscn` in the Godot editor for visual verification.

## Verification Commands

```bash
# Build
dotnet build

# Run tests (headless via dotnet)
./scripts/ci-test.sh

# Human verification (requires Godot editor)
# Open Combat/Arena/CombatArena.tscn and run
```

## Files Created ✅

### Core Implementation:
- [x] `Combat/States/CombatStateMachine.cs`
- [x] `Combat/Services/TurnQueueService.cs`
- [x] `Combat/Services/CommandService.cs`
- [x] `Combat/Services/CombatLog.cs`
- [x] `Combat/Entities/Combatant.cs`
- [x] `Data/ScenarioLoader.cs`
- [x] `Data/Scenarios/minimal_combat.json`

### Tests:
- [x] `Tests/Unit/TurnQueueTests.cs` (8 tests)
- [x] `Tests/Unit/CombatStateMachineTests.cs` (8 tests)
- [ ] `Tests/Simulation/PhaseAIntegrationTest.cs` (future: Godot headless testing)

## Notes for Agents

- Keep everything **deterministic** - use seeded RNG
- Log **everything** - state transitions, commands, events
- **No visual dependencies** - must work headless via dotnet tests
- Follow the **CombatArena-first rule** - every feature must be testable via automated tests
- Use structured events that can be **asserted in tests**

## Next Phase Preview

Phase B will add:
- Full rules engine
- Ability definitions and effects
- Targeting and AoE
- Damage/heal/status basics

But Phase A must be solid first - it's the foundation everything else builds on.
