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
See [AGENTS-TESTING-GAME.md](AGENTS-TESTING-GAME.md) for the complete workflow on:
- Writing and running simulation tests
- Debugging combat bugs with sub-agents
- Pairing simulation tests with vision tools for visual verification

## Parallel work
- Use when the sub-agents can work on very different systems

## Vision bridge tools
- `mcp_vision-bridge_vision_ask`: submit a screenshot path plus a concrete request (e.g., describe characters, UI, and layout) to get a natural-language summary of what is visible.
- `mcp_vision-bridge_vision_ocr`: supply the screenshot path and ask for any readable text; useful when you need labels or log output captured in the image.
- `mcp_vision-bridge_vision_ui_spec`: send the screenshot and request the UI structure; it returns a JSON-style spec that you can use to reconstruct or compare layouts.
