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

# Recommended: Dynamic short gameplay test (1v1 randomized builds)
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay

# Focused action test (1v1, first unit always acts first, single action loadout)
./scripts/run_autobattle.sh --full-fidelity --ff-action-test magic_missile

# With extended watchdog timeout (if animations are slow on your machine)
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --freeze-timeout 20

# Hard runtime cap for CI safety (example: 3 minutes)
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --max-time-seconds 180

# Enable verbose per-system debug logs (off by default to keep logs analyzable)
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --verbose-ai-logs --verbose-arena-logs

# Save log to a specific file
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --log-file artifacts/autobattle/my_test.jsonl

# Optional: pin scenario randomization + AI decision seeds for reproduction
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --scenario-seed 1840271 --seed 42
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

### Failure: No combatants present after start

**stdout pattern:**
```
[AutoBattleRuntime] No combatants are present in CombatArena after the test started...
AUTO-BATTLE: FAILED (no_combatants_detected)
```

**What it means:** Scenario/bootstrap failed and the arena ended up with zero units.

**How to fix:** Validate scenario generation/loading path and ensure `ScenarioLoader.SpawnCombatants(...)` produces units before combat begins.

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

Dynamic full-fidelity runs may use two seeds:

- `--scenario-seed`: character/scenario randomization seed
- `--seed`: AI decision seed

When you find a failing run, capture both and replay exactly:

```bash
# Example failing run
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --scenario-seed 1840271 --seed 1234

# Fix the bug, then verify the exact same seeds
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --scenario-seed 1840271 --seed 1234

# Then run again without --scenario-seed to get a fresh randomized duel
./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay
```

For short gameplay runs, avoid reusing scenario seeds unless reproducing/fixing/verifying a previous failure.

Save the failing seeds in the commit message when fixing the bug. Example:
```
fix: state machine stuck in ActionExecution after AoE ability

The AoE timeline was not calling Complete() when no targets were in
range (empty target list caused early return before End marker).

Repro: ./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --scenario-seed 1840271 --seed 1234
```

---

## Scenario Selection

Use dynamic scenarios for full-fidelity verification:

1. Ability test scenario (1v1)
- Command: `./scripts/run_autobattle.sh --full-fidelity --ff-action-test <ability_id>`
- Purpose: isolate one ability quickly; first unit always starts; loader can replace all granted abilities with a single explicit ability.
- Workflow: run, inspect `combat_log.jsonl`, fix the implementation to match BG3 behavior, rerun until stable.

2. Short gameplay scenario (1v1 randomized)
- Command: `./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay`
- Purpose: rapid gameplay coverage with randomized race/class/subclass combinations at equal level.
- Seed policy: each run should use a fresh scenario seed by default; only pin `--scenario-seed` when reproducing/verifying a previous failure.

Optional knobs:
- `--character-level <1-12>` sets both units to the same level (default `3`)
- `--scenario-seed <int>` controls character/scenario randomization
- `--seed <int>` controls AI decision randomness

---

## Debugging Workflow

### Step-by-step process

```
1. BUILD
   ./scripts/ci-build.sh
   (If this fails, fix build errors first)

2. RUN
   ./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay

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
   - Re-run with same seeds: ./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay --scenario-seed <same> --seed <same>
   - Run again without --scenario-seed (fresh randomized setup) to confirm no regressions
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

## WSL / Software Rendering Safety

> **Full-fidelity tests under WSL2 use llvmpipe (software Vulkan) which burns heavy CPU and can freeze the entire OS. The script applies CPU deprioritization and a hard kill timeout automatically.**

### What happens without protection

When there is no physical GPU (typical in WSL2), Godot falls back to **llvmpipe** — a CPU-based Vulkan implementation. The Forward+ renderer software-renders every frame, consuming 40-50%+ of a core continuously. Even with a timeout, llvmpipe can make WSL unresponsive during the run if the process has normal scheduling priority. Two failure modes exist:

1. **Hung quit path**: Godot's internal watchdog fires but the shutdown stalls while freeing GPU resources through llvmpipe. The process stays alive forever.
2. **CPU starvation**: llvmpipe at normal priority starves WSL's infrastructure processes, causing the entire environment to freeze even while the game is "running normally."

### Safeguards in the script

`run_autobattle.sh` enforces three layers of protection for full-fidelity runs:

1. **`nice -n 19` (automatic on WSL)** — lowers Godot's CPU scheduling priority to the minimum, preventing it from starving other processes. This is the key fix that stops WSL from freezing during the run.
2. **`--max-time-seconds 60` (injected automatically)** — tells Godot to self-terminate after 60s wall-clock time. Override with an explicit `--max-time-seconds <N>`.
3. **`timeout --signal=KILL` wrapper** — the shell hard-kills the Godot process 30s after the internal timeout (default: 90s total). If Godot's quit path hangs (common with llvmpipe), this guarantees the process dies.

### Rules for agents

- **ALWAYS** use `run_autobattle.sh` for full-fidelity tests. It handles `nice`, `timeout`, and Xvfb automatically.
- **NEVER** run Godot directly for full-fidelity tests under WSL. Calling Godot bypasses all safety (no `nice`, no `timeout`).
- **NEVER** run full-fidelity tests in background (`isBackground: true`) without a timeout — an orphaned Godot+llvmpipe process will burn CPU until WSL is killed.
- Keep `--max-time-seconds` short (≤120s). 60s is the default; most autobattles finish in 20-40s.
- Prefer **`--ff-short-gameplay`** (1v1) over full party scenarios — fewer units = less rendering load.
- **After a WSL crash/freeze**, always check for orphaned processes: `ps aux | grep godot` and kill them with `kill -9`.
- The Xvfb resolution is 1280x720 (not 1920x1080) to reduce software rendering overhead.

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
