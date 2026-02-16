#!/usr/bin/env bash
# Auto-battler runner for Godot project.
# Runs a fully automated combat where AI controls all units.
# Generates a combat_log.jsonl file with full forensic-level event history.
#
# Usage:
#   ./scripts/run_autobattle.sh                           # Fast headless mode
#   ./scripts/run_autobattle.sh --full-fidelity            # Full game mode (HUD, animations, visuals)
#   ./scripts/run_autobattle.sh --seed 1234
#   ./scripts/run_autobattle.sh --seed 42 --log-file my_battle.jsonl
#   ./scripts/run_autobattle.sh --scenario res://Data/Scenarios/autobattle_4v4.json
#   ./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay
#   ./scripts/run_autobattle.sh --full-fidelity --ff-ability-test magic_missile
#   ./scripts/run_autobattle.sh --max-rounds 50 --max-turns 200 --quiet
#
# Options (passed through to the Godot CLI runner):
#   --full-fidelity       Run with full game rendering (HUD, animations, visuals, camera).
#                         Uses Xvfb virtual display when no physical display is available.
#                         The AI plays like a human: waits for UI, animations, button readiness.
#   --seed <int>          Seed override for deterministic replay (default: scenario seed)
#   --scenario-seed <int> Seed for dynamic scenario generation (character randomization).
#   --character-level <n> Character level for dynamic scenarios (1-12, default: 3).
#   --ff-short-gameplay   Dynamic 1v1 short gameplay scenario with randomized characters.
#   --ff-ability-test <id> Dynamic 1v1 ability test. First unit always acts first and only gets this ability.
#   --scenario <path>     Scenario JSON (default: CombatArena scene default)
#   --log-file <path>     Output .jsonl log file (default: combat_log.jsonl)
#   --max-time-seconds <n> Maximum wall-clock runtime before force-fail (0 = disabled)
#   --max-rounds <int>    Maximum rounds before force-end (default: 100)
#   --max-turns <int>     Maximum total turns before force-end (default: 500)
#   --verbose-ai-logs     Enable high-volume per-action AI/controller console logging
#   --verbose-arena-logs  Enable high-volume CombatArena console logging
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

run_and_capture() {
    local cmd=("$@")
    local run_log
    run_log="$(mktemp -t autobattle.XXXXXX.log)"

    set +e
    "${cmd[@]}" 2>&1 | tee "$run_log"
    local cmd_exit=${PIPESTATUS[0]}
    set -e

    LAST_RUN_LOG="$run_log"
    LAST_RUN_EXIT="$cmd_exit"
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

# Parse mode/seed flags from arguments
FULL_FIDELITY=false
FF_SHORT_GAMEPLAY=false
FF_ABILITY_TEST=false
HAS_SCENARIO_SEED=false
PARITY_REPORT=false
NEED_NEXT_VALUE=""

for arg in "$@"; do
    if [[ -n "$NEED_NEXT_VALUE" ]]; then
        if [[ "$arg" == --* ]]; then
            log_error "--$NEED_NEXT_VALUE requires a value"
            exit 2
        fi
        case "$NEED_NEXT_VALUE" in
            scenario-seed)
                HAS_SCENARIO_SEED=true
                ;;
            ff-ability-test)
                FF_ABILITY_TEST=true
                ;;
        esac
        NEED_NEXT_VALUE=""
        continue
    fi

    case "$arg" in
        --full-fidelity)
            FULL_FIDELITY=true
            ;;
        --ff-short-gameplay)
            FF_SHORT_GAMEPLAY=true
            ;;
        --parity-report)
            PARITY_REPORT=true
            ;;
        --scenario-seed)
            NEED_NEXT_VALUE="scenario-seed"
            ;;
        --ff-ability-test)
            NEED_NEXT_VALUE="ff-ability-test"
            ;;
    esac
done

if [[ "$NEED_NEXT_VALUE" == "ff-ability-test" ]]; then
    log_error "--ff-ability-test requires an ability id"
    exit 2
fi
if [[ "$NEED_NEXT_VALUE" == "scenario-seed" ]]; then
    log_error "--scenario-seed requires an integer value"
    exit 2
fi

if [[ "$FF_SHORT_GAMEPLAY" == "true" && "$FF_ABILITY_TEST" == "true" ]]; then
    log_error "Choose only one dynamic mode: --ff-short-gameplay or --ff-ability-test <id>"
    exit 2
fi

# Build user args string, passing all script arguments through
USER_ARGS="--run-autobattle"
for arg in "$@"; do
    USER_ARGS="$USER_ARGS $arg"
done

# Short gameplay runs should use a new scenario randomization seed by default.
# Pass --scenario-seed explicitly when reproducing a previous run.
if [[ "$FF_SHORT_GAMEPLAY" == "true" && "$HAS_SCENARIO_SEED" == "false" ]]; then
    if command -v od &> /dev/null; then
        SCENARIO_SEED="$(od -An -N4 -tu4 /dev/urandom | tr -d '[:space:]')"
    else
        SCENARIO_SEED="$(( (RANDOM << 16) | RANDOM ))"
    fi
    USER_ARGS="$USER_ARGS --scenario-seed $SCENARIO_SEED"
    log_info "Generated scenario seed: $SCENARIO_SEED (reuse with --scenario-seed for verification)"
fi

# Track whether we spawned Xvfb so we can clean it up
XVFB_PID=""

cleanup_xvfb() {
    if [[ -n "$XVFB_PID" ]]; then
        kill "$XVFB_PID" 2>/dev/null || true
        wait "$XVFB_PID" 2>/dev/null || true
        log_info "Xvfb stopped"
    fi
}
trap cleanup_xvfb EXIT

if [[ "$FULL_FIDELITY" == "true" ]]; then
    log_info "Running in FULL-FIDELITY mode (HUD, animations, visuals active)"
    log_info "Running auto-battle..."
    echo -e "${CYAN}═══════════════════════════════════════════════════${NC}"

    # Full-fidelity mode: run with a real display.
    # If no display is available, start Xvfb as a virtual framebuffer.
    if [[ -z "${DISPLAY:-}" ]]; then
        if command -v Xvfb &> /dev/null; then
            XVFB_DISPLAY=":99"
            log_info "No display detected, starting Xvfb on $XVFB_DISPLAY"
            Xvfb "$XVFB_DISPLAY" -screen 0 1920x1080x24 &
            XVFB_PID=$!
            sleep 0.5
            export DISPLAY="$XVFB_DISPLAY"
        else
            log_warn "No display available and Xvfb not found. Install xvfb for headless full-fidelity mode."
            log_warn "Falling back to headless rendering (some visual components may not load)."
        fi
    else
        log_info "Using existing display: $DISPLAY"
    fi

    # Run WITHOUT --headless so the full rendering pipeline is active
    run_and_capture "$GODOT_BIN" --path . res://Combat/Arena/CombatArena.tscn -- $USER_ARGS
    EXIT_CODE="$LAST_RUN_EXIT"
else
    log_info "Running in fast headless mode"
    log_info "Running auto-battle..."
    echo -e "${CYAN}═══════════════════════════════════════════════════${NC}"

    # Fast mode: run headless (no rendering, no HUD, instant animations)
    run_and_capture "$GODOT_BIN" --headless --path . res://Combat/Arena/CombatArena.tscn -- $USER_ARGS
    EXIT_CODE="$LAST_RUN_EXIT"
fi

if [[ -n "${LAST_RUN_LOG:-}" ]]; then
    if grep -Fq "Cannot instantiate C# script because the associated class could not be found" "$LAST_RUN_LOG"; then
        log_error "Detected C# script class load failure. Build C# solutions and verify GODOT_BIN points to a .NET-enabled Godot binary."
        EXIT_CODE=1
    fi
    rm -f "$LAST_RUN_LOG"
fi

echo -e "${CYAN}═══════════════════════════════════════════════════${NC}"

# Determine the combat log file path
LOG_FILE="combat_log.jsonl"
prev_was_log=false
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

# Check for InvalidOperationException in captured output
FAILED_VALIDATION=false
if [[ -n "${LAST_RUN_LOG:-}" && -f "${LAST_RUN_LOG}" ]]; then
    if grep -Fq "InvalidOperationException" "$LAST_RUN_LOG"; then
        log_error "Detected InvalidOperationException - game failed to initialize"
        FAILED_VALIDATION=true
        EXIT_CODE=1
    fi
fi

# Check if combat actually happened (even if exit code was 0)
COMBAT_VERIFIED=false
if [[ $EXIT_CODE -eq 0 && "$FAILED_VALIDATION" == "false" ]]; then
    if [[ ! -f "$LOG_FILE" ]]; then
        log_error "Combat log file not found: $LOG_FILE"
        log_error "Combat did not initialize - treating as FAILED"
        EXIT_CODE=1
    else
        # Check for evidence of combat events
        # Events: BATTLE_START, STATE_CHANGE, ACTION_START, DAMAGE_DEALT, etc.
        if grep -Eq "(BATTLE_START|STATE_CHANGE|ACTION_START|DAMAGE_DEALT|BATTLE_END)" "$LOG_FILE"; then
            COMBAT_VERIFIED=true
        else
            log_error "Combat log exists but contains no combat events"
            log_error "Game may have crashed or failed to start combat"
            EXIT_CODE=1
        fi
    fi
fi

if [[ $EXIT_CODE -eq 0 ]]; then
    log_info "Auto-battle PASSED"
    
    if [[ -f "$LOG_FILE" ]]; then
        LINES=$(wc -l < "$LOG_FILE")
        SIZE=$(du -h "$LOG_FILE" | cut -f1)
        log_info "Combat log: $LOG_FILE ($LINES entries, $SIZE)"
    fi

    # Check for and report parity events if --parity-report was enabled
    if [[ "$PARITY_REPORT" == "true" && -f "$LOG_FILE" ]]; then
        log_info "Parity report mode was enabled"
        
        # Check if PARITY_SUMMARY events exist in the log
        if grep -q '"event":"PARITY_SUMMARY"' "$LOG_FILE"; then
            log_info "Parity summary events found in combat log"
            
            # Extract and display the parity summary
            PARITY_SUMMARY=$(grep '"event":"PARITY_SUMMARY"' "$LOG_FILE" | tail -1)
            if [[ -n "$PARITY_SUMMARY" ]]; then
                echo ""
                log_info "Parity Coverage Summary:"
                echo "$PARITY_SUMMARY" | jq -C '.' 2>/dev/null || echo "$PARITY_SUMMARY"
                echo ""
            fi
        else
            log_warn "No PARITY_SUMMARY events found in combat log"
            log_warn "Ensure DebugFlags.ParityReportMode is enabled when battle starts"
        fi
        
        # Check for ABILITY_COVERAGE events
        if grep -q '"event":"ABILITY_COVERAGE"' "$LOG_FILE"; then
            ABILITY_COVERAGE=$(grep '"event":"ABILITY_COVERAGE"' "$LOG_FILE" | tail -1)
            if [[ -n "$ABILITY_COVERAGE" ]]; then
                log_info "Ability Coverage:"
                echo "$ABILITY_COVERAGE" | jq -C '.' 2>/dev/null || echo "$ABILITY_COVERAGE"
            fi
        fi
    fi
else
    log_error "Auto-battle FAILED (exit code: $EXIT_CODE)"
fi

exit $EXIT_CODE
