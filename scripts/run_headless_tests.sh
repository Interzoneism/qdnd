#!/usr/bin/env bash
# Headless test runner for Godot project.
# Runs deterministic validation tests in headless mode (no rendering).
#
# Usage:
#   ./scripts/run_headless_tests.sh
#   GODOT_BIN=/path/to/godot ./scripts/run_headless_tests.sh
#
# Exit codes:
#   0 - All tests passed
#   1 - One or more tests failed
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

# Run headless tests
log_info "Running headless tests..."

cd "$PROJECT_DIR"

# Run the CLI runner scene in headless mode with --run-tests flag
"$GODOT_BIN" --headless --path . res://Tools/CLIRunner.tscn -- --run-tests
EXIT_CODE=$?

if [[ $EXIT_CODE -eq 0 ]]; then
    log_info "Headless tests PASSED"
else
    log_error "Headless tests FAILED (exit code: $EXIT_CODE)"
fi

exit $EXIT_CODE
