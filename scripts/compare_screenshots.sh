#!/usr/bin/env bash
# Screenshot comparison for visual regression testing.
# Compares screenshots against baseline images using ImageMagick.
#
# Usage:
#   ./scripts/compare_screenshots.sh
#   ./scripts/compare_screenshots.sh --threshold 0.01
#   ./scripts/compare_screenshots.sh --scene CombatArena
#
# Options:
#   --threshold <float>  Allowed difference (0.0-1.0, default: 0.001)
#   --scene <name>       Compare specific scene (matches *<name>*.png)
#   --baseline <dir>     Baseline directory (default: artifacts/baseline)
#   --screens <dir>      Screenshots directory (default: artifacts/screens)
#   --diff <dir>         Diff output directory (default: artifacts/diff)
#
# Exit codes:
#   0 - All comparisons passed (or no comparisons made)
#   1 - One or more comparisons failed
#   2 - Setup/configuration error

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Defaults
BASELINE_DIR="artifacts/baseline"
SCREENS_DIR="artifacts/screens"
DIFF_DIR="artifacts/diff"
THRESHOLD="0.001"
SCENE_FILTER=""

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
        --threshold)
            THRESHOLD="$2"
            shift 2
            ;;
        --scene)
            SCENE_FILTER="$2"
            shift 2
            ;;
        --baseline)
            BASELINE_DIR="$2"
            shift 2
            ;;
        --screens)
            SCREENS_DIR="$2"
            shift 2
            ;;
        --diff)
            DIFF_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [--threshold <float>] [--scene <name>] [--baseline <dir>] [--screens <dir>] [--diff <dir>]"
            exit 0
            ;;
        *)
            log_error "Unknown option: $1"
            exit 2
            ;;
    esac
done

# Check if ImageMagick compare is available
if ! command -v compare &> /dev/null; then
    log_error "ImageMagick 'compare' not found"
    log_error "Install with: sudo apt-get install imagemagick"
    exit 2
fi

# Full paths
BASELINE_PATH="$PROJECT_DIR/$BASELINE_DIR"
SCREENS_PATH="$PROJECT_DIR/$SCREENS_DIR"
DIFF_PATH="$PROJECT_DIR/$DIFF_DIR"

log_info "Comparing screenshots..."
log_info "Baseline: $BASELINE_PATH"
log_info "Screenshots: $SCREENS_PATH"
log_info "Diff output: $DIFF_PATH"
log_info "Threshold: $THRESHOLD"

# Check directories exist
if [[ ! -d "$BASELINE_PATH" ]]; then
    log_warn "Baseline directory not found: $BASELINE_PATH"
    log_info "Creating baseline directory..."
    mkdir -p "$BASELINE_PATH"
    log_info "No baselines to compare against. Run screenshots first and copy to baseline/"
    exit 0
fi

if [[ ! -d "$SCREENS_PATH" ]]; then
    log_error "Screenshots directory not found: $SCREENS_PATH"
    log_error "Run ./scripts/run_screenshots.sh first"
    exit 2
fi

# Ensure diff directory exists
mkdir -p "$DIFF_PATH"

# Find baseline images to compare
if [[ -n "$SCENE_FILTER" ]]; then
    BASELINES=$(find "$BASELINE_PATH" -name "*${SCENE_FILTER}*_latest.png" -o -name "*${SCENE_FILTER}*.png" 2>/dev/null | head -1)
else
    BASELINES=$(find "$BASELINE_PATH" -name "*_latest.png" 2>/dev/null)
fi

if [[ -z "$BASELINES" ]]; then
    log_warn "No baseline images found matching filter"
    log_info "Add baseline images to $BASELINE_PATH"
    exit 0
fi

# Track results
PASS_COUNT=0
FAIL_COUNT=0
SKIP_COUNT=0

# Compare each baseline
while IFS= read -r baseline; do
    [[ -z "$baseline" ]] && continue
    
    filename=$(basename "$baseline")
    screenshot="$SCREENS_PATH/$filename"
    diff_file="$DIFF_PATH/diff_$filename"
    
    log_info "Comparing: $filename"
    
    if [[ ! -f "$screenshot" ]]; then
        log_warn "  Screenshot not found, skipping: $screenshot"
        SKIP_COUNT=$((SKIP_COUNT + 1))
        continue
    fi
    
    # Run ImageMagick compare
    # -metric AE returns absolute error (number of different pixels)
    # -metric RMSE returns root mean square error
    # We use -metric AE with -fuzz to allow minor variations
    
    set +e
    COMPARE_OUTPUT=$(compare -metric AE -fuzz 1% "$baseline" "$screenshot" "$diff_file" 2>&1)
    COMPARE_EXIT=$?
    set -e
    
    # Extract the numeric difference
    DIFF_PIXELS=$(echo "$COMPARE_OUTPUT" | grep -o '^[0-9.]*' || echo "0")
    
    # Get image dimensions for percentage calculation
    DIMENSIONS=$(identify -format "%w %h" "$baseline" 2>/dev/null || echo "1920 1080")
    WIDTH=$(echo "$DIMENSIONS" | awk '{print $1}')
    HEIGHT=$(echo "$DIMENSIONS" | awk '{print $2}')
    TOTAL_PIXELS=$((WIDTH * HEIGHT))
    
    # Calculate difference percentage
    if [[ "$TOTAL_PIXELS" -gt 0 ]]; then
        DIFF_PERCENT=$(echo "scale=6; $DIFF_PIXELS / $TOTAL_PIXELS" | bc -l)
    else
        DIFF_PERCENT="0"
    fi
    
    # Compare against threshold
    PASSED=$(echo "$DIFF_PERCENT <= $THRESHOLD" | bc -l)
    
    if [[ "$PASSED" == "1" ]]; then
        log_info "  PASS (diff: $DIFF_PIXELS pixels, ${DIFF_PERCENT}%)"
        PASS_COUNT=$((PASS_COUNT + 1))
        # Remove diff file if passed
        rm -f "$diff_file"
    else
        log_error "  FAIL (diff: $DIFF_PIXELS pixels, ${DIFF_PERCENT}% > ${THRESHOLD})"
        log_error "  Diff image: $diff_file"
        FAIL_COUNT=$((FAIL_COUNT + 1))
    fi
    
done <<< "$BASELINES"

# Summary
echo ""
log_info "=== COMPARISON SUMMARY ==="
log_info "Passed: $PASS_COUNT"
[[ $FAIL_COUNT -gt 0 ]] && log_error "Failed: $FAIL_COUNT" || log_info "Failed: $FAIL_COUNT"
[[ $SKIP_COUNT -gt 0 ]] && log_warn "Skipped: $SKIP_COUNT" || log_info "Skipped: $SKIP_COUNT"

if [[ $FAIL_COUNT -gt 0 ]]; then
    log_error "VISUAL REGRESSION DETECTED"
    log_error "Check diff images in: $DIFF_PATH"
    exit 1
else
    log_info "OK"
    exit 0
fi
