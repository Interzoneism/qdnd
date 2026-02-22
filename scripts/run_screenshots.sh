#!/usr/bin/env bash
# Screenshot runner for visual regression testing.
# Captures screenshots of Godot scenes for UI/HUD verification.
# Can run under Xvfb for headless environments (WSL, CI).
#
# Usage:
#   ./scripts/run_screenshots.sh
#   ./scripts/run_screenshots.sh --scene res://Combat/Arena/CombatArena.tscn
#   GODOT_BIN=/path/to/godot ./scripts/run_screenshots.sh
#
# Options:
#   --scene <path>   Scene to capture (default: CombatArena.tscn)
#   --no-xvfb        Run without Xvfb (requires display)
#   --width <int>    Screenshot width (default: 1920)
#   --height <int>   Screenshot height (default: 1080)
#   --open-inventory Toggle inventory HUD before screenshot capture
#
# Exit codes:
#   0 - Screenshot captured successfully
#   1 - Screenshot failed
#   2 - Setup/configuration error

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Defaults
GODOT_BIN="${GODOT_BIN:-godot}"
SCENE="res://Combat/Arena/CombatArena.tscn"
OUTPUT_DIR="artifacts/screens"
WIDTH=1920
HEIGHT=1080
USE_XVFB=true
OPEN_INVENTORY=false

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

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --scene)
            SCENE="$2"
            shift 2
            ;;
        --no-xvfb)
            USE_XVFB=false
            shift
            ;;
        --width)
            WIDTH="$2"
            shift 2
            ;;
        --height)
            HEIGHT="$2"
            shift 2
            ;;
        --open-inventory)
            OPEN_INVENTORY=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [--scene <path>] [--no-xvfb] [--width <int>] [--height <int>] [--open-inventory]"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            exit 2
            ;;
    esac
done

# Check if Godot is available
if ! command -v "$GODOT_BIN" &> /dev/null; then
    log_error "Godot not found at: $GODOT_BIN"
    log_error "Set GODOT_BIN environment variable to your Godot binary path"
    exit 2
fi

# Check if xvfb-run is available when needed
if [[ "$USE_XVFB" == true ]] && ! command -v xvfb-run &> /dev/null; then
    log_warn "xvfb-run not found, installing may be required:"
    log_warn "  sudo apt-get install xvfb"
    log_error "Cannot run without display. Use --no-xvfb if you have a display."
    exit 2
fi

log_info "Using Godot: $GODOT_BIN"
log_info "Project dir: $PROJECT_DIR"
log_info "Target scene: $SCENE"
log_info "Resolution: ${WIDTH}x${HEIGHT}"
log_info "Use Xvfb: $USE_XVFB"
log_info "Open inventory: $OPEN_INVENTORY"

# Ensure output directory exists
mkdir -p "$PROJECT_DIR/$OUTPUT_DIR"

cd "$PROJECT_DIR"

# Build the Godot command
EXTRA_ARGS=""
if [[ "$OPEN_INVENTORY" == true ]]; then
    EXTRA_ARGS="--open-inventory"
fi
GODOT_CMD="$GODOT_BIN --path . --rendering-driver opengl3 res://Tools/ScreenshotRunner.tscn -- --scene $SCENE --screenshot-out $OUTPUT_DIR --w $WIDTH --h $HEIGHT $EXTRA_ARGS"

# Run with or without Xvfb
if [[ "$USE_XVFB" == true ]]; then
    log_info "Running under Xvfb virtual display..."
    xvfb-run -a -s "-screen 0 ${WIDTH}x${HEIGHT}x24" $GODOT_CMD
    EXIT_CODE=$?
else
    log_info "Running with native display..."
    $GODOT_CMD
    EXIT_CODE=$?
fi

if [[ $EXIT_CODE -eq 0 ]]; then
    log_info "Screenshot captured successfully"
    log_info "Output: $PROJECT_DIR/$OUTPUT_DIR/"
    ls -la "$PROJECT_DIR/$OUTPUT_DIR/"*.png 2>/dev/null || true
else
    log_error "Screenshot capture FAILED (exit code: $EXIT_CODE)"
fi

exit $EXIT_CODE
