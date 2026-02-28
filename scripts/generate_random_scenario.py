#!/usr/bin/env python3
"""
Generate randomized 2v2 combat scenarios for QDND game.

Usage:
    python generate_random_scenario.py --seed 42 [--output path/to/file.json]
"""

import argparse
import json
import random
import sys


# Ability pools
ADDITIONAL_ABILITIES = [
    "ranged_attack",
    "offhand_attack",
    "shove",
    "dash",
    "dodge_action",
    "hide",
    "second_wind",
    "bardic_inspiration",
    "cure_wounds",
    "Target_PoisonSpray"
]

# Name pools
PLAYER_NAMES = [
    "Aldric",
    "Brienne",
    "Cedric",
    "Delara",
    "Eldrin",
    "Fiona",
    "Gareth",
    "Helena",
    "Isadora",
    "Jareth"
]

HOSTILE_NAMES = [
    "Grimfang",
    "Vex",
    "Kragnar",
    "Shadowblade",
    "Malakar",
    "Dreadmaw",
    "Skorn",
    "Nightshade",
    "Razorclaw",
    "Hexbane"
]


def assign_tags(abilities):
    """Auto-assign tags based on abilities."""
    tags = []
    
    if "Target_PoisonSpray" in abilities:
        tags.extend(["melee", "debuffer"])
    if "cure_wounds" in abilities:
        tags.extend(["healer", "support"])
    if "ranged_attack" in abilities and "ranged" not in tags:
        tags.append("ranged")
    if "offhand_attack" in abilities and "melee" not in tags:
        tags.append("melee")
    if "bardic_inspiration" in abilities and "support" not in tags:
        tags.append("support")
    
    # Remove duplicates while preserving order
    seen = set()
    unique_tags = []
    for tag in tags:
        if tag not in seen:
            seen.add(tag)
            unique_tags.append(tag)
    
    return unique_tags


def generate_character(char_id, name, faction, x_range, z_range, rng):
    """Generate a single character with randomized stats and abilities."""
    # Always include main_hand_attack
    abilities = ["main_hand_attack"]
    
    # Add 1-3 additional abilities
    num_additional = rng.randint(1, 3)
    additional = rng.sample(ADDITIONAL_ABILITIES, num_additional)
    abilities.extend(additional)
    
    # Randomized stats
    hp = rng.randint(35, 80)
    initiative = rng.randint(8, 18)
    
    # Position
    x = rng.uniform(*x_range)
    z = rng.uniform(*z_range)
    
    # Auto-assign tags
    tags = assign_tags(abilities)
    
    return {
        "id": char_id,
        "name": name,
        "faction": faction,
        "hp": hp,
        "maxHp": hp,
        "initiative": initiative,
        "initiativeTiebreaker": rng.randint(1, 100),
        "x": round(x, 2),
        "y": 0,
        "z": round(z, 2),
        "abilities": abilities,
        "tags": tags
    }


def generate_scenario(seed):
    """Generate a complete 2v2 scenario."""
    rng = random.Random(seed)
    
    # Select names for this scenario
    player_names = rng.sample(PLAYER_NAMES, 2)
    hostile_names = rng.sample(HOSTILE_NAMES, 2)
    
    units = []
    
    # Generate 2 player units
    for i, name in enumerate(player_names, 1):
        char = generate_character(
            char_id=f"player_{i}",
            name=name,
            faction="player",
            x_range=(-5, -3),
            z_range=(-2, 2),
            rng=rng
        )
        units.append(char)
    
    # Generate 2 hostile units
    for i, name in enumerate(hostile_names, 1):
        char = generate_character(
            char_id=f"hostile_{i}",
            name=name,
            faction="hostile",
            x_range=(3, 5),
            z_range=(-2, 2),
            rng=rng
        )
        units.append(char)
    
    return {
        "id": f"random_2v2_seed_{seed}",
        "name": f"Random 2v2 (Seed {seed})",
        "seed": seed,
        "units": units
    }


def main():
    parser = argparse.ArgumentParser(
        description="Generate randomized 2v2 combat scenarios for QDND"
    )
    parser.add_argument(
        "--seed",
        type=int,
        required=True,
        help="Random seed for deterministic generation"
    )
    parser.add_argument(
        "--output",
        type=str,
        default=None,
        help="Output file path (default: stdout)"
    )
    
    args = parser.parse_args()
    
    # Generate scenario
    scenario = generate_scenario(args.seed)
    
    # Output as formatted JSON
    json_output = json.dumps(scenario, indent=2)
    
    if args.output:
        with open(args.output, 'w') as f:
            f.write(json_output)
            f.write('\n')
        print(f"Generated scenario saved to: {args.output}", file=sys.stderr)
    else:
        print(json_output)


if __name__ == "__main__":
    main()
