# Auto-Battle Debugging Workflow

## Overview

The auto-battle system is a **bug-finding engine** that plays the game against itself using the exact same code paths as a human player. Unlike unit tests or simulation harnesses that mock game logic, the auto-battle runs the **real CombatArena.tscn scene** with AI-controlled units executing actions through the public API (`ExecuteAbility`, `ExecuteMovement`, `EndCurrentTurn`).

**Key insight**: If a bug exists in the game's combat flow — state machine transitions, action budget consumption, turn advancement, resource management — the auto-battle will **trigger it reliably** because the AI keeps trying to act. Bugs that might be rare in manual testing (requiring specific action sequences) become **deterministic failures** in auto-battles.

## Why This Works

Traditional testing approaches often miss gameplay bugs because:
- **Unit tests** mock dependencies and test components in isolation
- **Simulation harnesses** bypass the real scene tree and signal flow
- **Manual testing** is non-deterministic and can't explore all edge cases
- **Integration tests** use hardcoded sequences that don't stress the system

The auto-battle approach is different:
1. **Runs the real game** - Same scene, same nodes, same state machine, same services
2. **Uses the public API** - AI calls `CombatArena.ExecuteAbility()` just like the player UI does
3. **Exercises full game loops** - Doesn't stop after one action; keeps playing until combat ends
4. **Deterministic with seeds** - Same seed = same initiative, same AI decisions, same RNG
5. **Has safety nets** - Watchdog detects infinite loops and freezes before they corrupt state

## When to Use Auto-Battle Debugging

Use this workflow when you encounter:
- **State machine bugs** - Combat freezes, turns don't advance, stuck in wrong state
- **Action budget issues** - Units can act multiple times, or can't act at all
- **Turn queue problems** - Initiative order wrong, skipped turns, duplicate turns
- **Resource tracking bugs** - HP/movement/actions not updating correctly
- **Victory condition failures** - Combat doesn't end when it should
- **Rare edge cases** - Bugs that only manifest after specific action sequences

## Running an Auto-Battle

### Basic Usage
```bash
# Run with default 4v4 scenario and seed
./scripts/run_autobattle.sh --seed 1234

# Run with a custom scenario
./scripts/run_autobattle.sh --seed 5678 --scenario res://Data/Scenarios/boss_fight.json

# Adjust safety limits (useful for debugging)
./scripts/run_autobattle.sh --seed 1234 \
  --freeze-timeout 5 \     # Trigger freeze detection after 5s of no activity
  --loop-threshold 10 \    # Trigger loop detection after 10 identical actions
  --max-turns 100          # Force-stop after 100 turns
```

### Output
The auto-battle writes two streams of data:

1. **combat_log.jsonl** - Structured JSON-Lines log with every decision and action:
   ```jsonl
   {"ts":1234567890,"event":"BATTLE_START","seed":1234,"units":[...]}
   {"ts":1234567891,"event":"TURN_START","turn":1,"round":1,"unit":"player_fighter",...}
   {"ts":1234567892,"event":"DECISION","unit":"player_fighter","action":"Attack","target":"enemy_orc",...}
   {"ts":1234567893,"event":"ACTION_RESULT","unit":"player_fighter","action":"Attack","success":true,...}
   {"ts":1234567900,"event":"WATCHDOG_ALERT","action":"TIMEOUT_FREEZE","error":"No action for 10s"}
   ```

2. **stdout** - Human-readable log with state transitions and debug messages:
   ```
   [RealtimeAIController] Turn started for Fighter
   [RealtimeAIController] Fighter -> Attack (#1) score:0.85
   [CombatArena] [STATE] PlayerDecision -> ActionExecution
   [AutoBattleWatchdog] FATAL: TIMEOUT_FREEZE - No action logged for 10s
   ```

## Failure Modes & What They Mean

### 1. TIMEOUT_FREEZE (No activity for N seconds)
**Symptom**: Auto-battle logs one or more actions, then goes silent for 10+ seconds.

**What it means**: The game is stuck in a state where the AI can't act and nothing is advancing the turn.

**Common causes**:
- State machine stuck in `ActionExecution` instead of returning to decision state
- Turn queue doesn't advance after `EndCurrentTurn()`
- Event signals disconnected or not firing
- Async operations (timers, tweens) blocking the main loop

**Debugging workflow**:
1. Check the last logged state in `combat_log.jsonl` - look for the final `event` before timeout
2. Look for state transition patterns - e.g., `PlayerDecision -> ActionExecution` with no return
3. Read stdout for the last `[STATE]` transition - this is where the freeze occurred
4. Search the codebase for that state transition and check if it has a return path

**Example**:
```json
{"event":"ACTION_RESULT","success":true}
{"event":"WATCHDOG_ALERT","action":"TIMEOUT_FREEZE","message":"No action for 10s"}
```
→ The action succeeded but the state machine never transitioned back to a decision state.

### 2. INFINITE_LOOP (Same action 20+ times in 1 second)
**Symptom**: Auto-battle logs the same action repeatedly in rapid succession until watchdog kills it.

**What it means**: A combatant's action budget is being refilled every frame/loop iteration, or the turn isn't advancing.

**Common causes**:
- `ActionBudget.ResetForTurn()` called in a loop (e.g., `BeginTurn` called repeatedly for same unit)
- `EndCurrentTurn()` fails validation but re-entry is allowed
- State checks have wrong logic (e.g., always returning to `AIDecision`)
- Turn queue `AdvanceTurn()` returns early without incrementing

**Debugging workflow**:
1. Count consecutive identical `DECISION` events in the log - how many before watchdog?
2. Check if `HasAction` is being reset - look for `ActionBudget` calls in the code path
3. Trace `BeginTurn` → `ExecuteAction` → `EndCurrentTurn` → `BeginTurn` to find the loop
4. Look for missing return statements or early bailouts that cause re-entry

**Example**:
```json
{"event":"DECISION","unit":"enemy_orc","action":"Attack","target":"player_mage"}
{"event":"ACTION_RESULT","unit":"enemy_orc","action":"Attack","success":true}
{"event":"DECISION","unit":"enemy_orc","action":"Attack","target":"player_mage"}
{"event":"ACTION_RESULT","unit":"enemy_orc","action":"Attack","success":true}
... (repeats 18 more times)
{"event":"WATCHDOG_ALERT","action":"INFINITE_LOOP"}
```
→ The turn isn't ending, and the budget is being refilled on every iteration.

### 3. MAX_TURNS_EXCEEDED (Battle runs for 500+ turns)
**Symptom**: Combat continues normally but never reaches an end condition.

**What it means**: Victory/defeat detection is broken, or combatants aren't dying properly.

**Common causes**:
- `TurnQueue.ShouldEndCombat()` has wrong faction logic
- `Combatant.IsActive` isn't being set to false on death
- HP reaches 0 but `LifeState` isn't updating
- Resurrection/healing loop keeps reviving units

**Debugging workflow**:
1. Look for `STATE_SNAPSHOT` events in the log - check HP values at round 50, 100, etc.
2. Verify all units of one faction have `"hp":0` and `"alive":false` - if so, victory check is broken
3. If HP oscillates (e.g., 0 → 15 → 0 → 15), find the healing/resurrection source
4. If HP is always >0, check damage calculation in `EffectPipeline`

### 4. Crashes or Exceptions
**Symptom**: Godot prints a stack trace and exits with code != 1.

**What it means**: Code bug - null reference, array out of bounds, missing service, etc.

**Debugging workflow**:
1. Copy the stack trace from stdout
2. Look for the innermost call in your code (ignore Godot engine frames)
3. Read that file/line and check for null derefs, array accesses, dictionary lookups
4. Add null checks or defensive guards

## Creating Test Scenarios

Scenarios are JSON files that define the initial combat setup. By tweaking unit positions, HP, initiative, and factions, you can create stress tests that expose specific bug categories.

### Scenario Structure
```json
{
  "name": "Test Scenario Name",
  "seed": 1234,
  "combatants": [
    {
      "id": "player_1",
      "name": "Fighter",
      "faction": "Player",
      "hp": 50,
      "initiative": 20,
      "position": [0, 0, 0]
    },
    {
      "id": "enemy_1",
      "name": "Orc",
      "faction": "Hostile",
      "hp": 30,
      "initiative": 15,
      "position": [8, 0, 0]
    }
  ]
}
```

### Scenario Templates for Different Bug Categories

#### 1. State Machine Stress Test (Many actions per turn)
High-HP units that can take 10+ actions before dying:
```json
"combatants": [
  {"id": "tank_1", "faction": "Player",  "hp": 500, "initiative": 20, "position": [0, 0, 0]},
  {"id": "tank_2", "faction": "Hostile", "hp": 500, "initiative": 19, "position": [5, 0, 0]}
]
```
**Targets**: Bugs in action budget consumption, state transitions after many sequential actions.

#### 2. Turn Queue Stress Test (Many combatants)
8+ units with varied initiative:
```json
"combatants": [
  {"id": "unit_1", "faction": "Player",  "hp": 20, "initiative": 25, ...},
  {"id": "unit_2", "faction": "Hostile", "hp": 20, "initiative": 24, ...},
  {"id": "unit_3", "faction": "Player",  "hp": 20, "initiative": 23, ...},
  ...
]
```
**Targets**: Turn order bugs, round advancement issues, initiative ties.

#### 3. Victory Detection Stress Test (Asymmetric teams)
1 high-HP boss vs. 4 low-HP players:
```json
"combatants": [
  {"id": "boss",    "faction": "Hostile", "hp": 200, "initiative": 20, ...},
  {"id": "hero_1",  "faction": "Player",  "hp": 15,  "initiative": 18, ...},
  {"id": "hero_2",  "faction": "Player",  "hp": 15,  "initiative": 17, ...},
  {"id": "hero_3",  "faction": "Player",  "hp": 15,  "initiative": 16, ...},
  {"id": "hero_4",  "faction": "Player",  "hp": 15,  "initiative": 15, ...}
]
```
**Targets**: Victory condition when one faction is eliminated, death handling.

#### 4. Resource Tracking Stress Test (Low HP, fragile units)
Units die within 1-2 hits:
```json
"combatants": [
  {"id": "glass_1", "faction": "Player",  "hp": 5, "initiative": 20, ...},
  {"id": "glass_2", "faction": "Hostile", "hp": 5, "initiative": 19, ...}
]
```
**Targets**: HP underflow, death state transitions, turn skip on dead units.

#### 5. Distance/Movement Stress Test (Units far apart)
Large arena with units separated by 50+ units:
```json
"combatants": [
  {"id": "ranged_1", "faction": "Player",  "hp": 30, "position": [0, 0, 0]},
  {"id": "ranged_2", "faction": "Hostile", "hp": 30, "position": [100, 0, 0]}
]
```
**Targets**: Movement pathfinding, range calculation, melee vs. ranged targeting logic.

## Iterative Debugging Workflow

### The Bug-Finding Loop
```
1. RUN auto-battle with a scenario
   ↓
2. Observe failure mode (freeze, loop, crash)
   ↓
3. Analyze combat_log.jsonl and stdout
   ↓
4. Identify the root cause (state machine, budget, turn queue, etc.)
   ↓
5. FIX the code
   ↓
6. RE-RUN with same seed (should now pass)
   ↓
7. RUN with 10+ different seeds to verify robustness
   ↓
8. CREATE a NEW scenario targeting different edge cases
   ↓
   (repeat from step 1)
```

### Example: Full Debugging Session

**Initial Run**:
```bash
./scripts/run_autobattle.sh --seed 1234
```

**Output**:
```
[RealtimeAIController] Fighter -> Attack (#1)
[CombatArena] [STATE] PlayerDecision -> ActionExecution
[AutoBattleWatchdog] FATAL: TIMEOUT_FREEZE - No action for 10s
```

**Analysis** (read stdout + combat_log.jsonl):
- Last state: `ActionExecution`
- Never returned to `PlayerDecision` or `AIDecision`
- Root cause: `ExecuteAbility()` transitions to `ActionExecution` but doesn't transition back

**Fix** (in CombatArena.cs):
```csharp
public void ExecuteAbility(...) {
    _stateMachine.TryTransition(CombatState.ActionExecution, ...);
    
    // Execute ability logic...
    
    // BUG FIX: Transition back to decision state
    if (actor.IsActive) {
        var decisionState = _isPlayerTurn 
            ? CombatState.PlayerDecision 
            : CombatState.AIDecision;
        _stateMachine.TryTransition(decisionState, "Action resolved");
    }
}
```

**Verification**:
```bash
./scripts/run_autobattle.sh --seed 1234  # Should now complete successfully
./scripts/run_autobattle.sh --seed 5678  # Test with different seed
./scripts/run_autobattle.sh --seed 9999  # Test another seed
```

**Expand Coverage**:
Create a new scenario with 8 combatants to stress-test turn queue:
```bash
./scripts/run_autobattle.sh --seed 1234 --scenario res://Data/Scenarios/8v8_chaos.json
```

## Complementing Other Testing Methods

The auto-battle workflow is **not a replacement** for other testing, but a **complement**:

| Method | Strengths | Weaknesses | Use When |
|--------|-----------|------------|----------|
| **Unit Tests** | Fast, isolated, deterministic | Don't test integration | Verifying individual functions/classes |
| **Simulation Tests** | Data-driven, repeatable, fast | Mock dependencies, may not match real game | Testing specific action sequences |
| **Screenshot Tests** | Visual verification | Requires rendering, brittle | Validating UI layout, VFX, animations |
| **Auto-Battle** | Tests real game, finds edge cases, deterministic | Slow, requires full scene setup | Finding state machine/integration bugs |
| **Manual Testing** | Catches UX issues, exploratory | Non-deterministic, time-consuming | Final validation before release |

**Recommended workflow**:
1. **Unit tests** - Verify core logic (damage calculation, initiative sorting, etc.)
2. **Auto-battle** - Find integration bugs (state transitions, turn flow, resource tracking)
3. **Simulation tests** - Regression tests for specific fixed bugs
4. **Screenshot tests** - Validate visual correctness
5. **Manual testing** - Final sanity check

## Advanced Techniques

### Bisecting Seeds to Find Rare Bugs
If a bug only happens sometimes:
```bash
# Run 100 auto-battles with different seeds
for seed in {1..100}; do
  ./scripts/run_autobattle.sh --seed $seed --quiet > result_$seed.txt 2>&1
  if grep -q "FATAL_ERROR" result_$seed.txt; then
    echo "FAILED at seed $seed"
  fi
done
```

### Custom AI Archetypes for Targeted Testing
Modify `RealtimeAIController.cs` to use extreme AI profiles:
```csharp
// Only uses movement (never attacks)
var moveOnlyProfile = new AIProfile { MovementWeight = 10.0, DamageWeight = 0.0 };

// Only attacks (never moves)
var attackOnlyProfile = new AIProfile { MovementWeight = 0.0, DamageWeight = 10.0 };

// Random chaos (equal weights)
var randomProfile = new AIProfile { /* all weights = 1.0 */ };
```

### Logging-Based Assertions
Add post-battle validation checks:
```csharp
// After auto-battle ends, parse combat_log.jsonl and assert:
- Total turns < 200 (no infinite loops)
- Every TURN_START has matching TURN_END
- Every combatant appears at least once
- Final STATE_SNAPSHOT has exactly one faction alive
```

## Troubleshooting the Auto-Battle System Itself

If the auto-battle doesn't run at all:

1. **Missing Godot binary**: Verify `which godot` returns a valid path
2. **Scene not found**: Check that `Combat/Arena/CombatArena.tscn` exists
3. **Build errors**: Run `./scripts/ci-build.sh` to see compilation errors
4. **Script not executable**: Run `chmod +x scripts/run_autobattle.sh`

If the watchdog triggers too early:
```bash
# Increase timeout for slow machines
./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 30
```

If combat ends too early:
```bash
# Increase turn limit
./scripts/run_autobattle.sh --seed 1234 --max-turns 1000
```

## Summary

The auto-battle debugging workflow is a **force multiplier** for finding gameplay bugs. By running the real game autonomously and monitoring for failures, you can:

- **Discover bugs** that manual testing would miss
- **Reproduce bugs** deterministically with seeds
- **Verify fixes** by re-running the same scenario
- **Stress test** edge cases with custom scenarios
- **Prevent regressions** by running auto-battles in CI

With enough iterations across varied scenarios and seeds, this approach can systematically eliminate entire categories of gameplay bugs — not just the ones you happen to encounter, but the ones you *would have* encountered under the right conditions.
