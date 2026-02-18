#!/usr/bin/env bash
# ci-godot-log-check.sh — lightweight Godot startup smoke test.
#
# Runs Godot headless for ~60 frames (≈1 second), captures all log output,
# and fails if any ERROR / SCRIPT ERROR / exception lines appear.
#
# This is significantly lighter than run_headless_tests.sh (full test suite)
# or run_autobattle.sh (full combat), but still catches:
#   - Script parse / compilation errors
#   - Autoload initialization failures
#   - Resource loading errors
#   - C# exceptions thrown during _Ready()
#
# Usage:
#   ./scripts/ci-godot-log-check.sh
#   GODOT_BIN=/path/to/godot ./scripts/ci-godot-log-check.sh
#
# Exit codes:
#   0 - No errors detected in Godot startup log
#   1 - Errors found in Godot log (or Godot crashed)
#   2 - Setup/configuration error (Godot binary not found)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

GODOT_BIN="${GODOT_BIN:-godot}"

# Number of frames to run before quitting. 60 frames ≈ 1 second at 60 fps.
# All autoloads and _Ready() hooks fire within the first few frames.
QUIT_AFTER="${GODOT_QUIT_AFTER:-60}"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

log_info()  { echo -e "${GREEN}[LOG-CHECK]${NC} $1"; }
log_warn()  { echo -e "${YELLOW}[LOG-CHECK]${NC} $1"; }
log_error() { echo -e "${RED}[LOG-CHECK]${NC} $1"; }

if ! command -v "$GODOT_BIN" &>/dev/null; then
    log_error "Godot not found at: $GODOT_BIN"
    log_error "Set GODOT_BIN environment variable to your Godot binary path"
    exit 2
fi

log_info "Godot binary : $GODOT_BIN"
log_info "Project dir  : $PROJECT_DIR"
log_info "Quit after   : $QUIT_AFTER frames"
log_info "Starting Godot startup smoke test..."

cd "$PROJECT_DIR"

LOGFILE="$(mktemp -t godot-log-check.XXXXXX.txt)"
trap 'rm -f "$LOGFILE"' EXIT

# Run headless. --quit-after N tells Godot to exit cleanly after N main-loop
# iterations. No display server is initialized in --headless mode, so there is
# no Vulkan / llvmpipe overhead — safe to run on WSL2 without a GPU.
set +e
"$GODOT_BIN" --headless --quit-after "$QUIT_AFTER" --path "$PROJECT_DIR" 2>&1 | tee "$LOGFILE"
GODOT_EXIT=${PIPESTATUS[0]}
set -e

echo ""

# ── Exit-code check ──────────────────────────────────────────────────────────
# --quit-after should always produce exit code 0. A crash or hard error
# yields something else.
if [[ $GODOT_EXIT -ne 0 ]]; then
    log_error "Godot exited with non-zero exit code: $GODOT_EXIT"
    exit 1
fi

# ── Log pattern check ────────────────────────────────────────────────────────
# These are the canonical Godot 4 error prefixes that indicate real problems.
# We deliberately exclude WARN / WARNING to keep the gate focused on hard
# failures. Add patterns here only if they reliably indicate a real bug.
ERROR_PATTERNS=(
    "^ERROR:"
    "^SCRIPT ERROR:"
    "^USER ERROR:"
    "^USER SCRIPT ERROR:"
    "Unhandled Exception:"
)

# Build a combined ERE pattern for a single grep pass
GREP_PATTERN=$(printf "%s|" "${ERROR_PATTERNS[@]}")
GREP_PATTERN="${GREP_PATTERN%|}"  # strip trailing |

FOUND_ERRORS=""
if grep -qE "$GREP_PATTERN" "$LOGFILE" 2>/dev/null; then
    FOUND_ERRORS=$(grep -E "$GREP_PATTERN" "$LOGFILE" || true)
fi

if [[ -n "$FOUND_ERRORS" ]]; then
    log_error "Godot startup log contains errors:"
    echo ""
    echo "$FOUND_ERRORS"
    echo ""
    log_error "Godot startup smoke test FAILED"
    exit 1
fi

log_info "Godot startup smoke test PASSED — no errors in debug log"
exit 0
