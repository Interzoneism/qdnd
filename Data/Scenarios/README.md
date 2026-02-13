# Combat Scenarios

This project supports both static JSON scenarios and dynamic scenario generation.

## Recommended for Full-Fidelity Testing

Use dynamic scenarios instead of hardcoded files:

1. Ability testing (1v1, first unit always goes first, single ability override)
```bash
./scripts/run_autobattle.sh --full-fidelity --ff-ability-test <ability_id>
```

2. Short gameplay testing (1v1, both characters randomized at same level)
```bash
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay
```

Optional dynamic controls:

- `--character-level <1-12>`: force both characters to the same level (default `3`)
- `--scenario-seed <int>`: seed character/scenario randomization
- `--seed <int>`: AI decision seed (separate from scenario randomization)
- `--max-time-seconds <n>`: hard wall-clock cap for a run (fails when exceeded)
- `--verbose-ai-logs` / `--verbose-arena-logs`: opt in to high-volume debug logs

### Seed policy for short gameplay

- By default, `--ff-short-gameplay` generates a fresh random `--scenario-seed` each run.
- Reuse a scenario seed only when reproducing/fixing/verifying a prior run.

## Static JSON Schema (Still Supported)

Scenarios use JSON with this structure:

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
      "initiative": 15,
      "x": 0,
      "y": 0,
      "z": 0,
      "abilities": ["basic_attack"],
      "replaceAbilities": false
    }
  ]
}
```

### `replaceAbilities`

When `true`, scenario loader uses only the explicit `abilities` list and does not merge class/race-granted abilities.
This is used by dynamic ability-test scenarios to isolate one ability at a time.

## Loading Scenarios

Static scenario loading:

```csharp
var loader = new ScenarioLoader();
var scenario = loader.LoadFromFile("res://Data/Scenarios/minimal_combat.json");
var combatants = loader.SpawnCombatants(scenario, turnQueue);
```

Dynamic scenario generation uses `Data/ScenarioGenerator.cs` and is wired through `CombatArena` CLI flags.
