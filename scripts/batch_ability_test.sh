#!/usr/bin/env bash
# Batch test all abilities via --ff-ability-test
# Usage: ./scripts/batch_ability_test.sh [phase]
#   phase 1 = Core actions, phase 2 = Cantrips, etc.
#   No arg = all phases

set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
RESULTS_DIR="$PROJECT_DIR/artifacts/autobattle/ability_tests"
mkdir -p "$RESULTS_DIR"

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

PHASE="${1:-all}"

# Core Actions
PHASE1=(main_hand_attack ranged_attack dash disengage dodge_action hide help shove jump throw dip)

# Weapon Actions
PHASE2_WPN=(cleave lacerate smash topple pommel_strike offhand_attack)

# Cantrips
PHASE3_CANT=(fire_bolt ray_of_frost sacred_flame eldritch_blast toll_the_dead chill_touch shocking_grasp thorn_whip vicious_mockery poison_spray blade_ward produce_flame)

# Level 1 Spells
PHASE4_L1=(magic_missile cure_wounds guiding_bolt healing_word shield_of_faith thunderwave burning_hands chromatic_orb witch_bolt inflict_wounds bless bane sleep grease dissonant_whispers hunters_mark ensnaring_strike hail_of_thorns mage_armor armor_of_agathys faerie_fire command sanctuary create_water hex)

# Level 2 Spells
PHASE5_L2=(shatter scorching_ray hold_person blur moonbeam silence lesser_restoration web darkness invisibility flaming_sphere heat_metal mirror_image spike_growth cloud_of_daggers spiritual_weapon)

# Level 3 Spells
PHASE6_L3=(fireball lightning_bolt spirit_guardians haste slow hunger_of_hadar hypnotic_pattern mass_healing_word call_lightning spirit_shroud bestow_curse revivify)

# Class Features
PHASE7_CLASS=(action_surge second_wind trip_attack menacing_attack rage reckless_attack frenzy cunning_action_dash cunning_action_disengage cunning_action_hide sneak_attack flurry_of_blows stunning_strike step_of_the_wind patient_defence divine_smite lay_on_hands turn_undead preserve_life guided_strike war_priest bardic_inspiration wild_shape_wolf wild_shape_bear wild_shape_spider symbiotic_entity create_sorcery_points)

# Feats
PHASE8_FEAT=(great_weapon_master_toggle sharpshooter_toggle polearm_butt_attack tavern_brawler_throw)

# Racial
PHASE9_RACIAL=(acid_breath_line fire_breath_line fire_breath_cone cold_breath_cone lightning_breath_line poison_breath_cone)

# Misc
PHASE10_MISC=(globe_of_invulnerability)

PASS_COUNT=0
FAIL_COUNT=0
TIMEOUT_COUNT=0
PASS_LIST=()
FAIL_LIST=()

test_ability() {
    local ability_id="$1"
    local log_file="$RESULTS_DIR/${ability_id}.jsonl"
    
    echo -n "  Testing $ability_id... "
    
    # Run the test, capture output
    local output
    output=$("$PROJECT_DIR/scripts/run_autobattle.sh" \
        --full-fidelity \
        --ff-ability-test "$ability_id" \
        --max-time-seconds 10 \
        --seed 42 \
        --log-file "$log_file" \
        2>&1)
    local exit_code=$?
    
    # Check if ability was actually used (check both ability_id field and description)
    local ability_used=false
    if [[ -f "$log_file" ]]; then
        if grep -q "\"ability_id\":\"$ability_id\"" "$log_file" 2>/dev/null; then
            ability_used=true
        elif grep -q "UseAbility:${ability_id}" "$log_file" 2>/dev/null; then
            ability_used=true
        elif grep -q "\"ability_id\":\"${ability_id}\"" "$log_file" 2>/dev/null; then
            ability_used=true
        fi
    fi
    
    # Check for crashes/errors (not timeout)
    local has_crash=false
    if echo "$output" | grep -qi "exception\|NullReference\|crash\|FATAL" 2>/dev/null; then
        has_crash=true
    fi
    
    # Determine result
    if [[ "$has_crash" == "true" ]]; then
        echo -e "${RED}CRASH${NC}"
        FAIL_COUNT=$((FAIL_COUNT + 1))
        FAIL_LIST+=("$ability_id (CRASH)")
        echo "$output" > "$RESULTS_DIR/${ability_id}_stdout.txt"
    elif [[ "$ability_used" == "true" ]]; then
        echo -e "${GREEN}PASS${NC} (ability used)"
        PASS_COUNT=$((PASS_COUNT + 1))
        PASS_LIST+=("$ability_id")
    elif [[ $exit_code -eq 0 ]]; then
        echo -e "${GREEN}PASS${NC} (battle completed)"
        PASS_COUNT=$((PASS_COUNT + 1))
        PASS_LIST+=("$ability_id")
    else
        echo -e "${YELLOW}TIMEOUT${NC} (ability not used)"
        TIMEOUT_COUNT=$((TIMEOUT_COUNT + 1))
        FAIL_LIST+=("$ability_id (TIMEOUT/not used)")
        echo "$output" > "$RESULTS_DIR/${ability_id}_stdout.txt"
    fi
}

run_phase() {
    local phase_name="$1"
    shift
    local abilities=("$@")
    
    echo ""
    echo -e "${YELLOW}=== Phase: $phase_name (${#abilities[@]} abilities) ===${NC}"
    
    for ability in "${abilities[@]}"; do
        test_ability "$ability"
    done
}

# Run requested phases
case "$PHASE" in
    1) run_phase "Core Actions" "${PHASE1[@]}" ;;
    2) run_phase "Weapon Actions" "${PHASE2_WPN[@]}" ;;
    3) run_phase "Cantrips" "${PHASE3_CANT[@]}" ;;
    4) run_phase "Level 1 Spells" "${PHASE4_L1[@]}" ;;
    5) run_phase "Level 2 Spells" "${PHASE5_L2[@]}" ;;
    6) run_phase "Level 3 Spells" "${PHASE6_L3[@]}" ;;
    7) run_phase "Class Features" "${PHASE7_CLASS[@]}" ;;
    8) run_phase "Feats" "${PHASE8_FEAT[@]}" ;;
    9) run_phase "Racial" "${PHASE9_RACIAL[@]}" ;;
    10) run_phase "Misc" "${PHASE10_MISC[@]}" ;;
    all)
        run_phase "Core Actions" "${PHASE1[@]}"
        run_phase "Weapon Actions" "${PHASE2_WPN[@]}"
        run_phase "Cantrips" "${PHASE3_CANT[@]}"
        run_phase "Level 1 Spells" "${PHASE4_L1[@]}"
        run_phase "Level 2 Spells" "${PHASE5_L2[@]}"
        run_phase "Level 3 Spells" "${PHASE6_L3[@]}"
        run_phase "Class Features" "${PHASE7_CLASS[@]}"
        run_phase "Feats" "${PHASE8_FEAT[@]}"
        run_phase "Racial" "${PHASE9_RACIAL[@]}"
        run_phase "Misc" "${PHASE10_MISC[@]}"
        ;;
    *) echo "Usage: $0 [1-10|all]"; exit 1 ;;
esac

echo ""
echo "═══════════════════════════════════════════════════"
echo -e "  ${GREEN}PASS: $PASS_COUNT${NC}  ${RED}FAIL: $FAIL_COUNT${NC}  ${YELLOW}TIMEOUT: $TIMEOUT_COUNT${NC}"
echo "═══════════════════════════════════════════════════"

if [[ ${#FAIL_LIST[@]} -gt 0 ]]; then
    echo ""
    echo "Failed/Timeout abilities:"
    for item in "${FAIL_LIST[@]}"; do
        echo "  - $item"
    done
fi

# Save summary
{
    echo "# Ability Test Results - $(date)"
    echo ""
    echo "## Summary"
    echo "- PASS: $PASS_COUNT"
    echo "- FAIL: $FAIL_COUNT" 
    echo "- TIMEOUT: $TIMEOUT_COUNT"
    echo ""
    echo "## Passed"
    for item in "${PASS_LIST[@]}"; do
        echo "- $item"
    done
    echo ""
    echo "## Failed/Timeout"
    for item in "${FAIL_LIST[@]}"; do
        echo "- $item"
    done
} > "$RESULTS_DIR/summary.md"

echo ""
echo "Full results saved to: $RESULTS_DIR/summary.md"
