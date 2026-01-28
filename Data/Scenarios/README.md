# Combat Scenarios

This folder contains test scenarios for the Testbed system.

## Current Status: ✅ Phase A Complete

The scenario loading system is implemented and working.

## Implemented Files

| File | Purpose |
|------|---------|
| `minimal_combat.json` | Test scenario with 2 allies, 2 enemies |

## Format

Scenarios use **JSON** with the following schema:

```json
{
  "id": "unique_scenario_id",
  "name": "Human-readable name",
  "seed": 42,
  "units": [
    {
      "id": "unit_id",
      "name": "Display Name",
      "faction": "player|hostile|neutral|ally",
      "hp": 50,
      "initiative": 15,
      "initiativeTiebreaker": 0
    }
  ]
}
```

## Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique scenario identifier |
| `name` | string | Human-readable name |
| `seed` | int | RNG seed for deterministic runs |
| `units` | array | List of combatants to spawn |

### Unit Fields

| Field | Type | Description |
|-------|------|-------------|
| `id` | string | Unique unit identifier |
| `name` | string | Display name (optional, defaults to id) |
| `faction` | string | `player`, `hostile`, `neutral`, or `ally` |
| `hp` | int | Maximum/starting HP |
| `initiative` | int | Initiative value for turn order |
| `initiativeTiebreaker` | int | Tie-breaker (higher goes first) |

## Loading Scenarios

Scenarios are loaded via `ScenarioLoader` in `Data/ScenarioLoader.cs`:

```csharp
var loader = new ScenarioLoader();
var scenario = loader.LoadFromFile("res://Data/Scenarios/minimal_combat.json");
var combatants = loader.SpawnCombatants(scenario, turnQueue);
```

## Creating New Scenarios

1. Create a new `.json` file in this folder
2. Follow the schema above
3. Use unique `id` values for the scenario and all units
4. Set a fixed `seed` for reproducibility
5. Add to TestbedBootstrap if you want it loaded by default

## Future Additions (Phase B+)

- Environment setup (surfaces, obstacles)
- Starting positions (3D coordinates)
- Pre-applied statuses/conditions
- Validation criteria / expected outcomes
