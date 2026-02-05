# Game Testing & Debugging Workflow

## Simulation testing workflow

Use the simulation test harness to exercise the combat system in headless mode — no rendering required. Tests are data-driven JSON manifests that inject commands (abilities, movement, end-turn) into a live CombatArena and verify state changes via snapshot deltas.

### Running tests
```bash
# Run the default golden-path suite
./scripts/run_simulation_tests.sh

# Run a specific manifest
xvfb-run -a godot --headless --path . res://Tools/CLIRunner.tscn -- \
  --run-simulation --manifest res://Data/SimulationTests/extended_combat.manifest.json

# Run all manifests in the directory
xvfb-run -a godot --headless --path . res://Tools/CLIRunner.tscn -- \
  --run-simulation --test-dir res://Data/SimulationTests
```

### Writing a test manifest
Create a `.manifest.json` file in `Data/SimulationTests/`. Structure:
```jsonc
{
  "name": "Suite Name",
  "defaultScenarioPath": "res://Data/Scenarios/minimal_combat.json",
  "tests": [
    {
      "name": "test_id",
      "description": "Human-readable goal",
      "commands": [
        { "type": "UseAbility", "actorId": "ally_1", "abilityId": "basic_attack", "targetId": "enemy_1" },
        { "type": "MoveTo",     "actorId": "ally_1", "position": [3, 0, 0] },
        { "type": "EndTurn" }
      ],
      "assertions": [
        { "combatantId": "enemy_1", "field": "CurrentHP",       "operator": "changed"   },
        { "combatantId": "ally_1",  "field": "HasAction",       "operator": "equals",     "expectedValue": "False" },
        { "combatantId": null,      "field": "CurrentRound",    "operator": "greaterThan", "expectedValue": "1" },
        { "combatantId": "enemy_1", "field": "ActiveStatuses",  "operator": "contains",   "expectedValue": "poisoned" }
      ]
    }
  ]
}
```

**Available command types**: `UseAbility`, `MoveTo`, `EndTurn`, `Select`, `SelectAbility`, `ClearSelection`, `Wait`.  
**Available assertion operators**: `equals`, `changed`, `unchanged`, `greaterThan`, `lessThan`, `contains`, `notContains`.  
**Available fields**: `CurrentHP`, `MaxHP`, `TempHP`, `HasAction`, `HasBonusAction`, `HasReaction`, `RemainingMovement`, `MaxMovement`, `ActiveStatuses`, `PositionX/Y/Z`, `Position` (composite), `EffectiveAC`, `AttackBonus`, `DamageBonus`, `SaveBonus`, `HasAdvantageOnAttacks/Saves`, `HasDisadvantageOnAttacks/Saves`. Global fields: `CombatState`, `CurrentCombatantId`, `CurrentRound`.

### Debugging combat bugs
1. **Write a failing test** that reproduces the issue (e.g., EndTurn not advancing rounds).
2. **Run the manifest** — the output shows pre/post snapshot deltas and assertion failures.
3. **Dispatch a sub-agent** with the failure details:
   ```
   runSubagent: "Fix EndTurn after ability bug"
   Prompt: "BUG: After executing an ability, EndTurn fails because state is ActionExecution…"
   ```
4. **Rebuild and re-run** to confirm the fix. Repeat until all assertions pass.

### Pairing with vision tools for visual verification
After running simulation tests for logic verification, use the screenshot pipeline to validate the visual side:

```bash
# 1. Capture a screenshot of the game state
./scripts/run_screenshots.sh
```

Then use the vision bridge to inspect what the player would actually see:

```
# Describe the overall scene (characters, HUD, layout)
mcp_vision-bridge_vision_ask  image_path="artifacts/screens/combat.png"
                               question="Describe the combat scene: character positions, health bars, active status effects, and turn indicator"

# Read specific UI text (damage numbers, status labels, turn order)
mcp_vision-bridge_vision_ocr   image_path="artifacts/screens/combat.png"

# Extract structured UI layout for automated comparison
mcp_vision-bridge_vision_ui_spec  image_path="artifacts/screens/combat.png"
```

**Typical combined workflow**:
1. Simulation test confirms: Goblin HP 20→0 after 4 rounds, poisoned status applied.
2. Screenshot capture renders the final combat state.
3. `vision_ask` confirms: "Goblin character model is in death animation, poison VFX visible."
4. `vision_ocr` reads: turn counter shows "Round 4", combat log says "Goblin defeated."

This closes the loop between *mechanical correctness* (simulation harness) and *visual correctness* (screenshot + vision analysis).
