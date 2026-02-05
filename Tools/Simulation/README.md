# Gameplay Simulation Harness

This directory contains tools for running automated gameplay tests against the full CombatArena in headless mode.

## Components

### Core Classes

- **`StateSnapshot.cs`** - Captures the complete state of combat at a point in time
- **`SnapshotDelta.cs`** - Compares two snapshots and identifies changes
- **`SimulationCommand.cs`** - Represents gameplay commands (move, use ability, end turn, etc.)
- **`SimulationCommandInjector.cs`** - Executes commands programmatically into CombatArena
- **`SimulationTestCase.cs`** - Defines test cases with commands and assertions
- **`SimulationTestRunner.cs`** - Orchestrates test execution (Snapshot → Act → Verify)

## Usage Example

```csharp
using QDND.Tools.Simulation;

// Create a test runner
var runner = new SimulationTestRunner();

// Add smoke tests
runner.AddSmokeTests();

// Or create a custom test
var customTest = new SimulationTestCase
{
    Name = "fighter_attacks_goblin",
    Description = "Fighter uses basic attack on goblin",
    Commands = new List<SimulationCommand>
    {
        SimulationCommand.UseAbility("hero_fighter", "basic_attack", "enemy_goblin")
    },
    Assertions = new List<SimulationAssertion>
    {
        SimulationAssertion.HpLessThan("enemy_goblin", 20)
    }
};
runner.AddTestCase(customTest);

// Run all tests (parent must be in scene tree)
var (allPassed, results) = runner.RunAllTests(parentNode);

// Print results as JSON
runner.PrintResults();
```

## Command Factory Methods

```csharp
// Movement
SimulationCommand.MoveTo("hero_fighter", 5f, 0f, 2f);

// Abilities
SimulationCommand.UseAbility("hero_fighter", "basic_attack", "enemy_goblin");

// Turn management
SimulationCommand.EndTurn();
SimulationCommand.Wait(0.5f);

// Selection (for complex sequences)
SimulationCommand.Select("hero_fighter");
SimulationCommand.SelectAbility("fireball");
SimulationCommand.ClearSelection();
```

## Assertion Factory Methods

```csharp
// Exact value checks
SimulationAssertion.Equals("hero_fighter", "CurrentHP", "50");
SimulationAssertion.HpEquals("hero_fighter", 50);
SimulationAssertion.PositionEquals("hero_fighter", 1f, 0f, 0f);

// Comparisons
SimulationAssertion.HpLessThan("enemy_goblin", 15);

// Change detection
SimulationAssertion.Changed("hero_fighter", "RemainingMovement");
SimulationAssertion.Changed(null, "CurrentCombatantId"); // Global state
```

## Output Format

`PrintResults()` outputs JSON like:

```json
{
  "summary": {
    "total": 2,
    "passed": 2,
    "failed": 0,
    "executionTimeMs": 150
  },
  "tests": [
    {
      "name": "move_fighter_one_tile",
      "passed": true,
      "executionTimeMs": 75
    },
    {
      "name": "end_turn_changes_combatant",
      "passed": true,
      "executionTimeMs": 75
    }
  ]
}
```

## Integration with CI

The test runner can be invoked from a Godot headless script:

```csharp
// In a test scene or CLIEntryPoint
var runner = new SimulationTestRunner();
runner.AddSmokeTests();

var (allPassed, _) = runner.RunAllTests(GetTree().Root);
runner.PrintResults();

GetTree().Quit(allPassed ? 0 : 1);
```

## Extending

### Custom Assertions

To add new assertion operators, extend `SimulationAssertion` and update `VerifyComparison()` in `SimulationTestRunner.cs`.

### Custom Commands

To add new command types:
1. Add to `SimulationCommandType` enum
2. Add factory method to `SimulationCommand`
3. Add handler in `SimulationCommandInjector.Execute()`

### Scenario Loading

Tests can specify custom scenarios:

```csharp
var test = new SimulationTestCase
{
    Name = "custom_scenario_test",
    ScenarioPath = "res://Data/Scenarios/boss_fight.json",
    Seed = 42,
    Commands = { /* ... */ },
    Assertions = { /* ... */ }
};
```
