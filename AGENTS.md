# Agent Rules (Godot 4.5 C#)

## Scope & safety
- Operate strictly within /workspace (repo root). Never write outside the repo.
- Prefer minimal diffs and reversible changes.
- Never mass-reformat or rename large sets of files unless explicitly required.

## Build gates
- Before declaring done, always run:
  - scripts/ci-build.sh
  - scripts/ci-test.sh (if a test project exists)
- If you introduce new systems, update documentation in /docs.

## Godot-specific commands
### Automation helpers
- `./scripts/run_headless_tests.sh` runs the headless verification suite that uses `Tools/HeadlessTestRunner.cs` to validate services, registries, and scenarios without rendering.
- `./scripts/run_screenshots.sh` builds a HUD scene capture under Xvfb and drops fresh images into `artifacts/screens/`; pair it with `./scripts/compare_screenshots.sh` to diff against `artifacts/baseline/`.

## Game testing & debugging
### Auto-battle workflow (primary method for combat bugs)
- **Purpose**: Run the real CombatArena.tscn scene with AI-controlled units to expose state machine bugs, action budget issues, turn queue problems, and victory condition failures.
- **When to use**: Any time you suspect combat logic bugs, or want to stress-test new features.
- **Quick start**: `./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20`
- **Full guide**: See [AGENTS-AUTOBATTLE-DEBUG.md](AGENTS-AUTOBATTLE-DEBUG.md) for:
  - How to interpret failures (TIMEOUT_FREEZE, INFINITE_LOOP)
  - Creating custom scenarios to target specific bug categories
  - Log analysis workflow (combat_log.jsonl + stdout)
  - Iterative debugging loop (run → analyze → fix → verify → stress-test)
- **Key insight**: Unlike simulation tests, auto-battles use the *real game code paths* and will trigger bugs that manual testing might miss.

## Parallel work
- Use when the sub-agents can work on very different systems

## Vision bridge tools
- `mcp_vision-bridge_vision_ask`: submit a screenshot path plus a concrete request (e.g., describe characters, UI, and layout) to get a natural-language summary of what is visible.
- `mcp_vision-bridge_vision_ocr`: supply the screenshot path and ask for any readable text; useful when you need labels or log output captured in the image.
- `mcp_vision-bridge_vision_ui_spec`: send the screenshot and request the UI structure; it returns a JSON-style spec that you can use to reconstruct or compare layouts.
