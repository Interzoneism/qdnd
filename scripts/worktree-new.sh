#!/usr/bin/env bash
set -euo pipefail

AGENT="${1:-}"
TASK="${2:-}"
if [[ -z "$AGENT" || -z "$TASK" ]]; then
  echo "Usage: scripts/worktree-new.sh <agent> <task-slug>"
  exit 1
fi

mkdir -p ".worktrees/$AGENT"
BR="agent/${AGENT}/${TASK}"
DIR=".worktrees/${AGENT}/${TASK}"

git worktree add "$DIR" -b "$BR"
echo "Created worktree: $DIR  (branch: $BR)"
