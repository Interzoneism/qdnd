#!/usr/bin/env bash
# Parity coverage sweep runner - runs multiple auto-battles and aggregates coverage.
# Generates an aggregate parity report showing cumulative ability/effect coverage.
#
# Usage:
#   ./scripts/run_parity_sweep.sh                    # 10 runs (default)
#   ./scripts/run_parity_sweep.sh --seeds 20          # 20 randomized runs
#   ./scripts/run_parity_sweep.sh --character-level 5 --seeds 15
#   ./scripts/run_parity_sweep.sh --output-dir artifacts/parity_sweep_results
#
# Options:
#   --seeds <N>          Number of random battle seeds to run (default: 10)
#   --character-level <N> Character level for generated scenarios (default: 3)
#   --output-dir <DIR>    Output directory for logs and reports (default: artifacts/autobattle)
#   --quiet              Suppress per-run output

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

# Colors
GREEN='\033[0;32m'
CYAN='\033[0;36m'
NC='\033[0m'

# Defaults
NUM_SEEDS=10
CHARACTER_LEVEL=3
OUTPUT_DIR="$PROJECT_DIR/artifacts/autobattle"
QUIET=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        --seeds)
            NUM_SEEDS="$2"
            shift 2
            ;;
        --character-level)
            CHARACTER_LEVEL="$2"
            shift 2
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --quiet)
            QUIET=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

mkdir -p "$OUTPUT_DIR"

echo -e "${CYAN}╔═══════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║         PARITY COVERAGE SWEEP                             ║${NC}"
echo -e "${CYAN}╚═══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}Running $NUM_SEEDS auto-battles to collect parity metrics${NC}"
echo -e "${GREEN}Output directory: $OUTPUT_DIR${NC}"
echo ""

PASSED=0
FAILED=0
LOGS=()

for ((i=1; i<=NUM_SEEDS; i++)); do
    SEED=$(( RANDOM * 32768 + RANDOM ))
    LOG_FILE="$OUTPUT_DIR/combat_log_seed_${SEED}.jsonl"
    
    echo -e "${CYAN}[$i/$NUM_SEEDS] Running seed $SEED...${NC}"
    
    EXTRA_ARGS="--parity-report"
    if [[ "$QUIET" == "true" ]]; then
        EXTRA_ARGS="$EXTRA_ARGS --quiet"
    fi
    
    # Run autobattle with parity reporting enabled
    if "$SCRIPT_DIR/run_autobattle.sh" \
        --seed "$SEED" \
        --character-level "$CHARACTER_LEVEL" \
        --log-file "$LOG_FILE" \
        $EXTRA_ARGS \
        > /dev/null 2>&1; then
        PASSED=$((PASSED + 1))
        LOGS+=("$LOG_FILE")
        echo -e "${GREEN}  ✓ Passed${NC}"
    else
        FAILED=$((FAILED + 1))
        echo -e "\033[0;31m  ✗ Failed${NC}"
    fi
done

echo ""
echo -e "${CYAN}═══════════════════════════════════════════════════${NC}"
echo -e "${GREEN}Sweep complete:${NC}"
echo -e "  Passed: $PASSED"
echo -e "  Failed: $FAILED"
echo ""

# Aggregate parity reports from all successful runs
if [[ $PASSED -gt 0 ]]; then
    echo -e "${GREEN}Aggregating parity metrics from $PASSED successful runs...${NC}"
    
    # Use grep and jq to extract parity summaries
    TOTAL_DAMAGE=0
    TOTAL_STATUSES=0
    TOTAL_SURFACES=0
    GRANTED_ABILITIES=0
    ATTEMPTED_ABILITIES=0
    
    if command -v jq &> /dev/null; then
        for log in "${LOGS[@]}"; do
            if [[ -f "$log" ]]; then
                # Extract PARITY_SUMMARY event
                SUMMARY=$(grep '"event":"PARITY_SUMMARY"' "$log" 2>/dev/null | tail -1)
                if [[ -n "$SUMMARY" ]]; then
                    DAMAGE=$(echo "$SUMMARY" | jq -r '.metrics.total_damage_dealt // 0')
                    STATUSES=$(echo "$SUMMARY" | jq -r '.metrics.total_statuses_applied // 0')
                    SURFACES=$(echo "$SUMMARY" | jq -r '.metrics.total_surfaces_created // 0')
                    TOTAL_DAMAGE=$((TOTAL_DAMAGE + DAMAGE))
                    TOTAL_STATUSES=$((TOTAL_STATUSES + STATUSES))
                    TOTAL_SURFACES=$((TOTAL_SURFACES + SURFACES))
                fi
                
                # Extract ABILITY_COVERAGE event
                COVERAGE=$(grep '"event":"ABILITY_COVERAGE"' "$log" 2>/dev/null | tail -1)
                if [[ -n "$COVERAGE" ]]; then
                    GRANTED=$(echo "$COVERAGE" | jq -r '.metrics.granted // 0')
                    ATTEMPTED=$(echo "$COVERAGE" | jq -r '.metrics.attempted // 0')
                    if [[ $GRANTED -gt $GRANTED_ABILITIES ]]; then
                        GRANTED_ABILITIES=$GRANTED
                    fi
                    if [[ $ATTEMPTED -gt $ATTEMPTED_ABILITIES ]]; then
                        ATTEMPTED_ABILITIES=$ATTEMPTED
                    fi
                fi
            fi
        done
        
        echo ""
        echo -e "${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
        echo -e "${GREEN}║        AGGREGATE PARITY COVERAGE REPORT                ║${NC}"
        echo -e "${GREEN}╚════════════════════════════════════════════════════════╝${NC}"
        echo ""
        echo -e "  Total Runs:           $PASSED"
        echo -e "  Abilities Granted:    $GRANTED_ABILITIES (max across runs)"
        echo -e "  Abilities Attempted:  $ATTEMPTED_ABILITIES (max across runs)"
        echo -e "  Total Damage Dealt:   $TOTAL_DAMAGE"
        echo -e "  Total Statuses:       $TOTAL_STATUSES"
        echo -e "  Total Surfaces:       $TOTAL_SURFACES"
        echo ""
    else
        echo -e "\033[1;33m[WARN]${NC} jq not installed - cannot aggregate metrics"
        echo "       Install jq for metric aggregation: sudo apt install jq"
    fi
fi

if [[ $PASSED -eq 0 ]]; then
    echo -e "\033[0;31mERROR: All battles failed - no data collected${NC}"
    exit 1
fi

echo -e "${GREEN}Parity sweep completed successfully${NC}"
exit 0
