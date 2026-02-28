# Agent Rules (Godot 4.6 C#)

## Governance
- **[PROJECT_STATUS.md](PROJECT_STATUS.md)** — Single source of truth for project state, architecture, and technical debt
- **[CODING_STANDARDS.md](CODING_STANDARDS.md)** — Mandatory coding standards (naming, namespaces, patterns, events, error handling)
- All documentation referenced in `PROJECT_STATUS.md § Deprecated Documentation` is **banned** — do not follow those docs

## Scope & safety
- Operate strictly within /workspace (repo root). Never write outside the repo.
- Prefer minimal diffs and reversible changes.
- Never mass-reformat or rename large sets of files unless explicitly required.

## Build gates
- Before declaring done, always run:
  - scripts/ci-build.sh
  - scripts/ci-test.sh (if a test project exists)
  - scripts/ci-godot-log-check.sh — lightweight Godot startup smoke test; runs the engine headless for ~60 frames and fails if any `ERROR:`, `SCRIPT ERROR:`, or `Unhandled Exception:` lines appear in the log. Much faster than a full autobattle or headless test suite. Catches script parse errors, autoload failures, and `_Ready()` exceptions introduced by a change. Set `GODOT_BIN` if godot is not on PATH.
- If you introduce new systems, update documentation in /docs.

## Git / Source
- Do not commit, the human user will do this
- Do not push to git, the human user will do this

## Godot-specific commands
### Automation helpers
- `./scripts/run_headless_tests.sh` runs the headless verification suite that uses `Tools/HeadlessTestRunner.cs` to validate services, registries, and scenarios without rendering.
- `./scripts/run_screenshots.sh` builds a HUD scene capture under Xvfb and drops fresh images into `artifacts/screens/`; pair it with `./scripts/compare_screenshots.sh` to diff against `artifacts/baseline/`.

## Game testing & debugging
### Full-fidelity testing (primary method for verifying the game works)
- **Purpose**: Run the game exactly as a player would experience it — full HUD, animations, visuals, camera — with a UI-aware AI playing like a human.
- **When to use**: To verify that the game works end-to-end after any change. If it breaks here, it would break for a real player.
- **Quick start**: `./scripts/run_autobattle.sh --full-fidelity --seed 42`
- **Full guide**: See [AGENTS-FULL-FIDELITY-TESTING.md](AGENTS-FULL-FIDELITY-TESTING.md)
- **Iron rule**: NEVER disable systems or bypass components to make the test pass. Fix the game code.

### Fast auto-battle (quick iteration on combat logic)
- **Purpose**: Run the real CombatArena.tscn scene headless with AI-controlled units to expose state machine bugs, action budget issues, turn queue problems, and victory condition failures.
- **When to use**: Quick iteration on combat logic bugs, or stress-testing with many seeds.
- **Quick start**: `./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20`
- **Debug guide**: See [AGENTS-AUTOBATTLE-DEBUG.md](AGENTS-AUTOBATTLE-DEBUG.md) for:
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

## Common Gotchas
- Ring mesh orientation: `TorusMesh` is already ground-aligned in Godot. Do **not** rotate range/selection/target torus indicators by 90 degrees unless you have verified the mesh orientation in-scene. A forced X-axis 90 rotation will put rings on the wrong axis.
- Test-host interop: In `dotnet test` (`testhost`/`vstest`) processes, direct Godot interop calls like `Godot.GD.Print/PrintErr` (and sometimes `Godot.FileAccess`/`DirAccess`) can crash the host. For parser/data paths exercised by unit tests, guard for testhost and fall back to `Console` + `System.IO`.
- Keep this section updated: when you discover a recurring engine/UI pitfall that can waste debugging time, add it here as a concise rule for future agents.
