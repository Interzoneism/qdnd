# Full-Fidelity Testing Guide

## Purpose

Full-fidelity testing runs the **real game** with every system active — HUD, animations, camera, visuals, input handling, reaction prompts — and uses a UI-aware AI to play it like a human would. Its purpose is to find **game-breaking bugs** that only surface when all systems interact together.

**This is not a performance test.** It is a correctness test. If the game would crash, freeze, or break for a real player, this test must reproduce that failure.

## The Iron Rule

> **Fix the game, never bypass the test.**

When a full-fidelity run fails, the failure is revealing a real bug that a player would hit. The correct response is **always** to fix the underlying game code.

**NEVER do any of the following to make the test pass:**
- Disable the HUD, action bar, turn tracker, or any UI component
- Skip or instant-complete animations
- Set `DebugFlags.SkipAnimations = true`
- Set `DebugFlags.IsFullFidelity = false` to fall back to fast mode
- Add `if (IsAutoBattleMode)` guards that skip game logic
- Remove or weaken the watchdog timeouts
- Comment out reaction prompt display
- Bypass `ActionBarModel` availability checks
- Catch and swallow exceptions without fixing the cause
- Add `return` statements to avoid crashing code paths

If a system is broken, **fix that system**. The test exists to prove the game works end-to-end.

---

## Quick Start

### Prerequisites

| Requirement | Check | Install |
|---|---|---|
| Godot 4.5+ with .NET | `godot --version` | Download from godotengine.org |
| .NET 8 SDK | `dotnet --version` | `sudo apt install dotnet-sdk-8.0` |
| Xvfb (headless servers) | `which Xvfb` | `sudo apt install xvfb` |
| Project builds | `./scripts/ci-build.sh` | Fix any build errors first |

### Run a full-fidelity test

```bash
# Build first — never skip this
./scripts/ci-build.sh

# Recommended: Run a random 2v2 scenario
./scripts/run_autobattle.sh --full-fidelity --random-scenario --seed 12345

# Run with a specific short scenario (<=4 combatants)
./scripts/run_autobattle.sh --full-fidelity --seed 42 --scenario res://Data/Scenarios/minimal_combat.json

# With extended watchdog timeout (if animations are slow on your machine)
./scripts/run_autobattle.sh --full-fidelity --seed 42 --freeze-timeout 20

# Save log to a specific file
./scripts/run_autobattle.sh --full-fidelity --seed 42 --log-file artifacts/autobattle/my_test.jsonl
```

### What happens

1. The script detects `--full-fidelity` and starts **Xvfb** (virtual display) if no physical display is available
2. Godot launches `CombatArena.tscn` **without** `--headless` — full rendering pipeline is active
3. `CombatArena.ConfigureAutoBattleFromCommandLine()` parses `--full-fidelity` and sets:
   - `DebugFlags.IsFullFidelity = true`
   - `DebugFlags.SkipAnimations = false`
   - HUD initializes normally (CombatHUD, ActionBar, TurnTracker, ResourceBars)
4. `UIAwareAIController` is attached instead of `RealtimeAIController`
5. After a startup delay (≥1.0s), the AI begins playing — checking HUD readiness, waiting for animations, verifying button availability, using 0.8–1.5s delays between actions
6. Battle runs until victory/defeat, watchdog timeout, or max turns
7. `combat_log.jsonl` is written with full event history

---

## How the UIAwareAIController Works

Unlike the fast-mode `RealtimeAIController` which calls `ExecuteAbility()` directly every 100ms, the `UIAwareAIController` follows a human-like loop on every `_Process` frame:

```
1. Is the HUD ready? (ActionBarModel exists, CombatContext initialized)
   NO → wait 0.5s, retry

2. Is a reaction prompt showing?
   YES → wait 1.2s (reading time), then call SimulateDecision(true)

3. Are animations still playing?
   YES → wait for all ActiveTimelines to complete

4. Is the state machine in a decision state? (PlayerDecision or AIDecision)
   NO → wait

5. Has the state settled? (0.3s since last transition)
   NO → wait

6. Has enough human-like delay passed? (0.8–1.5s random)
   NO → wait

7. Is there a valid active combatant?
   NO → retry up to 10 times, then force end turn

8. Actions this turn < 50?
   NO → force end turn (safety limit)

9. Get AI decision from AIDecisionPipeline
10. Verify chosen ability exists in ActionBarModel AND IsAvailable == true
    NOT AVAILABLE → skip, retry (max 5 consecutive skips → end turn)
11. Execute via CombatArena public API (ExecuteAbility, ExecuteMovement, EndCurrentTurn)
12. Schedule next action with random delay
```

Every check represents a real step that a human player would go through. If any check fails persistently, that's a bug.

---

## Interpreting Failures

### Failure: HUD never becomes ready

**stdout pattern:**
```
[UIAwareAI] ActionBarModel not ready
[UIAwareAI] ActionBarModel not ready
... (repeating every 0.5s)
[AutoBattleWatchdog] FATAL: TIMEOUT_FREEZE
```

**What it means:** The `ActionBarModel` is never created, so the HUD is not initializing.

**How to fix:** Investigate `CombatArena._Ready()` and the model initialization path. Check that `_actionBarModel = new ActionBarModel()` runs before the AI controller starts. Check `CombatHUD.DeferredInit()` for early returns that skip initialization in autobattle mode.

**Do NOT:** Add `if (IsFullFidelity) _hudReady = true;` to skip the check.

---

### Failure: Animation blocks decision state

**stdout pattern:**
```
[UIAwareAI] Turn started for Fighter
[UIAwareAI] Fighter -> Attack (#1) score:0.85
[UIAwareAI] waiting for: animation to complete (1 playing)
... (repeating for 10+ seconds)
[AutoBattleWatchdog] FATAL: TIMEOUT_FREEZE
```

**What it means:** An `ActionTimeline` started playing but never reached `Completed` state. The state machine is stuck in `ActionExecution` because the timeline's `Complete()` method never fired.

**How to fix:** Check the specific animation timeline. Common causes:
- Timeline duration is 0 or negative (no End marker)
- `Process(delta)` is not being called on the timeline (it's not in `_activeTimelines`)
- The timeline was created but `Play()` was never called
- A marker callback threw an exception, preventing the timeline from advancing

**Do NOT:** Set `DebugFlags.SkipAnimations = true` or filter out playing timelines.

---

### Failure: State machine never reaches decision state

**stdout pattern:**
```
[UIAwareAI] State: PlayerDecision -> ActionExecution
[UIAwareAI] waiting for: decision state (current: ActionExecution)
... (forever)
```

**What it means:** After executing an ability, the state machine transitioned to `ActionExecution` but never transitioned back to `PlayerDecision` or `AIDecision`.

**How to fix:** Trace the `ExecuteAbility()` flow. After the timeline completes, `ResumeDecisionStateIfExecuting()` should fire. Check:
- Is the timeline completing? (see animation failure above)
- Is `ResumeDecisionStateIfExecuting()` being called?
- Does the action ID match? (stale `_executingActionId`)
- Is the safety timeout timer running? (should fire after `timeline.Duration + 0.05s`)

**Do NOT:** Add a manual state transition in the AI controller.

---

### Failure: Reaction prompt hangs forever

**stdout pattern:**
```
[UIAwareAI] State: ActionExecution -> ReactionPrompt
[UIAwareAI] Waiting for reaction prompt resolution
[UIAwareAI] waiting for: reading reaction prompt (0.8s)
[UIAwareAI] Auto-resolving reaction prompt (Use) after reading delay
[UIAwareAI] waiting for: reaction prompt state to resolve
... (never leaves ReactionPrompt)
```

**What it means:** `SimulateDecision()` was called but the state machine didn't transition out of `ReactionPrompt`. Either:
- `_onDecision` callback was null
- `HandleReactionDecision()` didn't transition the state machine
- The reaction prompt's `Hide()` worked but the state machine wasn't updated

**How to fix:** Check `ReactionPromptUI.SimulateDecision()` → `_onDecision?.Invoke()` → `CombatArena.HandleReactionDecision()` → state machine transition. The method must call `TryTransition()` to leave `ReactionPrompt`.

**Do NOT:** Skip reaction prompts in autobattle mode or auto-resolve them outside the UI.

---

### Failure: Ability not found in action bar

**stdout pattern:**
```
[UIAwareAI] Ability basic_attack not found in action bar (skip #1), skipping
[UIAwareAI] Ability basic_attack not found in action bar (skip #2), skipping
...
[UIAwareAI] Max consecutive skips reached, ending turn
```

**What it means:** The `AIDecisionPipeline` returned an ability that doesn't exist in the `ActionBarModel`. This means the HUD's action bar doesn't list this ability for the current combatant, but the AI thinks it's valid.

**How to fix:** Check `ActionBarModel` population when a turn starts. Is `UpdateForCombatant()` being called? Does it include all the combatant's known abilities? Is the ability ID matching between the data registry and the AI pipeline?

**Do NOT:** Skip the action bar validation check.

---

### Failure: Godot crash / null reference

**stdout pattern:**
```
System.NullReferenceException: Object reference not set to an instance of an object
  at QDND.Combat.Arena.CombatHUD.OnTurnChanged(TurnChangeEvent evt)
  at QDND.Combat.Services.TurnQueueService.AdvanceTurn()
```

**What it means:** A HUD callback received a null reference because a node was freed, a service wasn't initialized, or `_disposed` wasn't checked before operating on UI elements.

**How to fix:** Add proper null/validity guards where the crash happens. For HUD callbacks, ensure `_disposed` and `IsInstanceValid(this)` are checked. For service references, ensure initialization happens before events fire.

**Do NOT:** Wrap the entire callback in `try/catch` with an empty catch body.

---

## Seed-Based Reproduction

Every run uses a seed to make combat deterministic. When you find a failing seed:

```bash
# This seed fails
./scripts/run_autobattle.sh --full-fidelity --seed 1234

# Fix the bug, then verify the same seed passes
./scripts/run_autobattle.sh --full-fidelity --seed 1234

# Then try one random seed and verify
./scripts/run_autobattle.sh --full-fidelity --seed 31337
```

Save the failing seed in the commit message when fixing the bug. Example:
```
fix: state machine stuck in ActionExecution after AoE ability

The AoE timeline was not calling Complete() when no targets were in
range (empty target list caused early return before End marker).

Repro: ./scripts/run_autobattle.sh --full-fidelity --seed 1234
```

---

## Scenario Selection

For full-fidelity verification, the best option is to use a **random 2v2 scenario**. This provides excellent coverage of different character combinations and abilities.

Alternatively, you can use **short, predefined scenarios (2v2 max, no more than 4 combatants)**.
This keeps runs fast while still covering HUD, animations, targeting, state transitions, and tactical AI in a reproducible way.

| Scenario | File | Tests |
|---|---|---|
| Ability mix 2v2 | `ff_short_ability_mix.json` | Damage/heal/buff rotation, action+bonus action usage |
| Control duel 1v1 | `ff_short_control_skirmish.json` | Status-driven pressure, range fallback, deterministic endgame |
| Attrition duel 2v2 | `ff_short_attrition.json` | Multi-round pacing, healing decisions, AoE usage |
| Minimal baseline 2v2 | `minimal_combat.json` | Core turn/state machine sanity |

Use larger scenarios (`autobattle_4v4.json`, `gameplay_ai_stress.json`) if the task requires it, perhaps to check for issues that arrive from complex behavior or long time effects.

### Run all scenarios

```bash
SCENARIOS=(
  ff_short_ability_mix.json
  ff_short_control_skirmish.json
  ff_short_attrition.json
  minimal_combat.json
)

for scenario in "${SCENARIOS[@]}"; do
  echo "=== $scenario ==="
  ./scripts/run_autobattle.sh --full-fidelity --seed 42 \
    --scenario "res://Data/Scenarios/$scenario" \
    --log-file "artifacts/autobattle/ff_${scenario%.json}.jsonl" \
    || echo "FAILED: $scenario"
done
```

---

## Debugging Workflow

### Step-by-step process

```
1. BUILD
   ./scripts/ci-build.sh
   (If this fails, fix build errors first)

2. RUN
   ./scripts/run_autobattle.sh --full-fidelity --seed 42

3. OBSERVE
   - Exit code 0 = passed, anything else = failure
   - Read stdout for [UIAwareAI], [CombatArena], [AutoBattleWatchdog] messages
   - Read combat_log.jsonl for structured event data

4. DIAGNOSE
   - What was the last [UIAwareAI] message before the hang/crash?
   - What was the last state transition?
   - What was the AI "waiting for"?
   - Is there a stack trace?

5. FIX THE GAME CODE
   - Find the root cause in the game systems
   - Apply the minimal fix
   - Do NOT add autobattle workarounds

6. VERIFY
   - Rebuild: ./scripts/ci-build.sh
   - Run unit tests: ./scripts/ci-test.sh
   - Re-run with same seed: ./scripts/run_autobattle.sh --full-fidelity --seed 42
   - Run with 3+ other seeds to confirm no regressions
```

### Reading the log

The `combat_log.jsonl` file contains one JSON object per line. Key event types:

```jsonl
{"event":"BATTLE_START","seed":42,"units":["fighter","mage","orc","goblin"]}
{"event":"TURN_START","round":1,"turn":1,"unit":"fighter"}
{"event":"DECISION","unit":"fighter","action":"Attack","ability":"basic_attack","target":"orc","score":0.85}
{"event":"ACTION_RESULT","unit":"fighter","action":"Attack","success":true,"damage":12}
{"event":"TURN_END","unit":"fighter","actions_taken":3}
{"event":"WATCHDOG_ALERT","type":"TIMEOUT_FREEZE","message":"No action for 10s"}
{"event":"BATTLE_END","winner":"Player","rounds":4,"turns":16}
```

Use `jq` to analyze:
```bash
# Show all watchdog alerts
jq 'select(.event == "WATCHDOG_ALERT")' combat_log.jsonl

# Show the last 5 events before a freeze
tail -6 combat_log.jsonl | head -5

# Count actions per unit
jq -r 'select(.event == "ACTION_RESULT") | .unit' combat_log.jsonl | sort | uniq -c

# Show all state transitions
grep '\[STATE\]' stdout_log.txt
```

---

## Key Files

| File | Role |
|---|---|
| `scripts/run_autobattle.sh` | Shell launcher, Xvfb management, arg forwarding |
| `Tools/AutoBattler/UIAwareAIController.cs` | Full-fidelity AI — the "human player" |
| `Tools/AutoBattler/RealtimeAIController.cs` | Fast-mode AI (not used in full-fidelity) |
| `Tools/AutoBattler/AutoBattleRuntime.cs` | Logging, watchdog attachment |
| `Tools/AutoBattler/AutoBattleWatchdog.cs` | Freeze/loop detection |
| `Tools/AutoBattler/BlackBoxLogger.cs` | JSONL log writer |
| `Tools/DebugFlags.cs` | `IsFullFidelity`, `SkipAnimations`, `IsAutoBattle` |
| `Combat/Arena/CombatArena.cs` | Scene controller, CLI parsing, controller wiring |
| `Combat/Arena/CombatHUD.cs` | HUD (active in full-fidelity, disabled in fast mode) |
| `Combat/Arena/ReactionPromptUI.cs` | Reaction prompt UI with `SimulateDecision()` |
| `Combat/UI/ActionBarModel.cs` | Action bar data model (ability availability) |

---

## Common Mistakes (and Why They're Wrong)

| Temptation | Why it's wrong | What to do instead |
|---|---|---|
| "The HUD crashes, let me disable it in autobattle" | A player would see this crash too | Fix the HUD null reference or initialization order |
| "Animation is stuck, let me skip animations" | The animation system is broken for real players | Fix the timeline so it completes correctly |
| "Reaction prompt hangs, let me auto-decide without showing UI" | The prompt UI path is broken | Fix `SimulateDecision()` → `HandleReactionDecision()` → state transition |
| "Action bar doesn't list the ability, let me skip the check" | The action bar won't show it for real players either | Fix `ActionBarModel.UpdateForCombatant()` to include all abilities |
| "State machine is stuck, let me add a forced transition in the AI" | The state machine is broken for real players | Fix the missing transition in `CombatArena` |
| "Watchdog times out, let me increase it to 300s" | The game is actually frozen | Fix whatever is preventing progress (small increases for slow machines are OK) |
| "It works in fast mode, only full-fidelity fails" | Full-fidelity is the real game; fast mode skips things | The bug is real — fast mode hides it |

---

## Relationship to Other Tests

Full-fidelity testing sits at the **top of the test pyramid**. It does not replace other tests — it catches what they miss.

```
                    ┌─────────────────────┐
                    │  Full-Fidelity Test  │  ← You are here
                    │  (real game, real UI)│
                    └──────────┬──────────┘
                               │
                    ┌──────────┴──────────┐
                    │  Fast Auto-Battle   │  Quick iteration, no rendering
                    │  (headless, skip UI)│
                    └──────────┬──────────┘
                               │
              ┌────────────────┼────────────────┐
              │                │                │
    ┌─────────┴──────┐ ┌──────┴───────┐ ┌──────┴──────┐
    │ Simulation     │ │ Integration  │ │ Screenshot  │
    │ Tests          │ │ Tests        │ │ Tests       │
    └────────────────┘ └──────────────┘ └─────────────┘
              │                │
    ┌─────────┴──────────────────┐
    │      Unit Tests (1151)     │
    └────────────────────────────┘
```

- **Unit tests** verify individual functions — run these first, they're fast
- **Fast auto-battle** (`--headless`, no `--full-fidelity`) iterates quickly on combat logic
- **Full-fidelity** proves the game works as a player would experience it
- If full-fidelity passes, you can be confident the game won't break for a real player in that scenario
