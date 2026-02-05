#!/bin/bash
# Run simulation tests in headless mode
# Usage: ./scripts/run_simulation_tests.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

cd "$PROJECT_ROOT"

# Default to 'godot' if GODOT_BIN not set
GODOT_BIN="${GODOT_BIN:-godot}"

echo "=== Running Simulation Tests ==="
echo "Godot binary: $GODOT_BIN"
echo "Project root: $PROJECT_ROOT"
echo ""

"$GODOT_BIN" --headless --path . res://Tools/CLIRunner.tscn -- --run-simulation

exit_code=$?

if [ $exit_code -eq 0 ]; then
    echo ""
    echo "=== Simulation Tests PASSED ==="
else
    echo ""
    echo "=== Simulation Tests FAILED ==="
fi

exit $exit_code
