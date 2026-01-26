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

## Godot-specific constraints
- Assume the Godot editor is not available in the sandbox.
- Scene/resource edits must be text-safe (.tscn/.tres), with minimal churn.
- Avoid regenerating or re-saving scene files in ways that reorder large blocks.

## Parallel work
- Use git worktrees under .worktrees/<agent>/<task>.
- Branch naming: agent/<agent>/<task-slug>
- Keep commits small and descriptive.
