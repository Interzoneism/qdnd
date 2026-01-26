# Combat Scenarios

This folder contains test scenarios for the Testbed system.

## Purpose

Scenarios are data-driven setups that:
- Spawn units with specific configurations
- Create environmental conditions (surfaces, obstacles)
- Define starting conditions (initiative order, statuses, etc.)
- Provide validation criteria for automated testing

## Format (to be defined in Phase A)

Scenarios will use one of:
- JSON files with a defined schema
- YAML files (human-friendly)
- Godot Resource files (.tres)

Each scenario should include:
- Unique ID
- Description
- Unit spawn definitions (faction, stats, equipment, position)
- Environment setup (surfaces, obstacles, lighting)
- Expected outcomes / validation criteria
- Deterministic RNG seed (for reproducibility)

## Example Structure (planned)

```json
{
  "id": "basic_melee_encounter",
  "description": "Two allies vs two enemies, basic melee combat",
  "seed": 12345,
  "units": [
    {
      "id": "ally_1",
      "faction": "player",
      "position": [0, 0, 0],
      "stats": { "hp": 50, "ac": 15 }
    }
  ],
  "environment": {
    "surfaces": []
  },
  "validations": [
    {
      "type": "initiative_count",
      "expected": 4
    }
  ]
}
```

## Current State

Phase A implementation pending. This folder will be populated as the scenario loader system is built.
