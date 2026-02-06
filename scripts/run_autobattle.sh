#!/usr/bin/env bash
# Auto-battler runner for Godot project.
# Runs a fully automated combat where AI controls all units.
# Generates a combat_log.jsonl file with full forensic-level event history.
#
# Usage:
#   ./scripts/run_autobattle.sh
#   ./scripts/run_autobattle.sh --seed 1234
#   ./scripts/run_autobattle.sh --seed 42 --log-file my_battle.jsonl
#   ./scripts/run_autobattle.sh --scenario res://Data/Scenarios/autobattle_4v4.json
#   ./scripts/run_autobattle.sh --max-rounds 50 --max-turns 200 --quiet
#
# Options (passed through to the Godot CLI runner):
#   --seed <int>          Random seed for deterministic replay (default: 42)
#   --scenario <path>     Scenario JSON (default: autobattle_4v4.json)
#   --log-file <path>     Output .jsonl log file (default: combat_log.jsonl)
#   --max-rounds <int>    Maximum rounds before force-end (default: 100)
#   --max-turns <int>     Maximum total turns before force-end (default: 500)
#   --quiet               Suppress per-entry stdout logging
#
# Exit codes:
#   0 - Battle completed successfully
#   1 - Battle failed or watchdog triggered
#   2 - Setup/configuration error

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Godot binary location (can be overridden via environment)
GODOT_BIN="${GODOT_BIN:-godot}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Check if Godot is available
if ! command -v "$GODOT_BIN" &> /dev/null; then
    log_error "Godot not found at: $GODOT_BIN"
    log_error "Set GODOT_BIN environment variable to your Godot binary path"
    exit 2
fi

log_info "Using Godot: $GODOT_BIN"
log_info "Project dir: $PROJECT_DIR"

cd "$PROJECT_DIR"

# Build user args string, passing all script arguments through
USER_ARGS="--run-autobattle"
for arg in "$@"; do
    USER_ARGS="$USER_ARGS $arg"
done

log_info "Running auto-battle..."
echo -e "${CYAN}═══════════════════════════════════════════════════${NC}"

# Run the CLI runner scene in headless mode with --run-autobattle flag
"$GODOT_BIN" --headless --path . res://Tools/CLIRunner.tscn -- $USER_ARGS
EXIT_CODE=$?

echo -e "${CYAN}═══════════════════════════════════════════════════${NC}"

if [[ $EXIT_CODE -eq 0 ]]; then
    log_info "Auto-battle PASSED"
    
    # Check if log file was created
    LOG_FILE="combat_log.jsonl"
    # Check if user specified a custom log file
    for i in "$@"; do
        if [[ "$prev_was_log" == "true" ]]; then
            LOG_FILE="$i"
            break
        fi
        if [[ "$i" == "--log-file" ]]; then
            prev_was_log=true
        else
            prev_was_log=false
        fi
    done 2>/dev/null || true
    
    if [[ -f "$LOG_FILE" ]]; then
        LINES=$(wc -l < "$LOG_FILE")
        SIZE=$(du -h "$LOG_FILE" | cut -f1)
        log_info "Combat log: $LOG_FILE ($LINES entries, $SIZE)"
    fi
else
    log_error "Auto-battle FAILED (exit code: $EXIT_CODE)"
fi

exit $EXIT_CODE
