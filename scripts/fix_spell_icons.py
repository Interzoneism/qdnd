"""
Fix missing spell/action icons in JSON data files.
Maps action IDs to their correct icon paths from assets/Images/.

Usage: python scripts/fix_spell_icons.py
"""

import os
import json
import re
import sys

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
IMAGES_ROOT = os.path.join(REPO_ROOT, "assets", "Images")
DATA_ACTIONS = os.path.join(REPO_ROOT, "Data", "Actions")

# Icon folder priority order (more specific first)
ICON_FOLDERS = [
    ("Icons Spells", "spell"),
    ("Icons Actions", "action"),
    ("Icons Weapon Actions", "weapon"),
    ("Icons Passive Features", "passive"),
    ("Icons Conditions", "condition"),
    ("Icons General", "general"),
    ("Icons Weapons and Other", "item"),
    ("Icons Armour", "armour"),
    ("Portraits Temp", "portrait"),
]

GENERIC_DEFAULTS = [
    "res://assets/Images/spell_icons/default_spell.png",
    "res://assets/Images/Icons Spells/default_spell.png",
    "res://assets/Images/Icons/fire_bolt.png",
    "res://assets/Images/Icons/fireball.png",
    "res://assets/Images/Icons/cure_wounds.png",
    "res://assets/Images/Icons/bless.png",
]

JSON_FILES = [
    "bg3_spells_phase3.json",
    "bg3_spells_phase3b.json",
    "bg3_spells_phase4.json",
    "bg3_spells_expanded.json",
    "bg3_spells_high_level.json",
    "bg3_mechanics_actions.json",
    "common_actions.json",
    "consumable_items.json",
]


def normalize_key(name: str) -> str:
    """Normalize a name to a lookup key: lowercase, replace non-alphanum with underscore."""
    name = name.lower()
    name = re.sub(r"[^a-z0-9]+", "_", name)
    name = name.strip("_")
    return name


def build_icon_lookup() -> dict:
    """Build a mapping from normalized names to res:// icon paths."""
    lookup = {}  # normalized_key -> res:// path

    for folder_name, _ in ICON_FOLDERS:
        folder_path = os.path.join(IMAGES_ROOT, folder_name)
        if not os.path.isdir(folder_path):
            print(f"  [WARN] Icon folder not found: {folder_path}")
            continue

        for fname in os.listdir(folder_path):
            if not fname.endswith(".png"):
                continue

            # Strip _Unfaded_Icon.png suffix to get the base name
            base = fname
            for suffix in ["_Unfaded_Icon.png", "_Icon.png", ".png"]:
                if base.endswith(suffix):
                    base = base[: -len(suffix)]
                    break

            key = normalize_key(base)
            res_path = f"res://assets/Images/{folder_name}/{fname}"

            # Only add if not already in lookup (first folder wins for equally normalized keys)
            if key not in lookup:
                lookup[key] = res_path

            # Also add the full filename normalized (to match "expeditious_retreat_unfaded_icon" etc.)
            full_key = normalize_key(fname[: -len(".png")] if fname.endswith(".png") else fname)
            if full_key not in lookup:
                lookup[full_key] = res_path

    print(f"  Built icon lookup with {len(lookup)} entries")
    return lookup


# Manual overrides for IDs that don't resolve automatically.
# Maps action_id -> icon filename base (without _Unfaded_Icon.png suffix).
MANUAL_OVERRIDES = {
    # Variant name mappings (different suffix/variant in filenames)
    "darkvision": "Darkvision_spell",
    "detect_magic": "Detect_Evil_and_Good",   # no detect_magic icon; use closest
    "dispel_magic": "Remove_Curse",            # no dispel_magic icon; fallback
    "water_walk": "Freedom_of_Movement",       # no water_walk icon; fallback
    "raise_dead": "Revivify",                  # no raise_dead icon; fallback
    "contagion": "Contagion_Blinding_Sickness",
    "eyebite": "Eyebite_Asleep",
    "branding_smite": "Branding_Smite_Melee",
    "create_or_destroy_water": "Create_Water",
    "divine_favor": "Divine_Favour",           # British spelling
    "conjure_woodland_beings": "Conjure_Woodland_Being",  # singular
    "conjure_minor_elementals": "Conjure_Minor_Elemental_Azer",
}


def find_icon_for_id(action_id: str, action_name: str, lookup: dict) -> str | None:
    """Try to find a matching icon for an action ID or name."""
    # Direct ID lookup
    key = normalize_key(action_id)
    if key in lookup:
        return lookup[key]

    # Try name-based lookup
    if action_name:
        name_key = normalize_key(action_name)
        if name_key in lookup:
            return lookup[name_key]

    # Try partial matches: remove common prefixes/suffixes
    stripped = key
    for prefix in ["shout_", "target_", "projectile_", "zone_", "wall_", "throw_"]:
        if stripped.startswith(prefix):
            stripped = stripped[len(prefix):]
            if stripped in lookup:
                return lookup[stripped]

    # Try removing trailing numbers (e.g., charm_person_2 -> charm_person)
    no_num = re.sub(r"_\d+$", "", key)
    if no_num != key and no_num in lookup:
        return lookup[no_num]

    # Try with name stripping numbers
    if action_name:
        name_no_num = re.sub(r"\s+\d+$", "", action_name.strip())
        if name_no_num != action_name:
            nk = normalize_key(name_no_num)
            if nk in lookup:
                return lookup[nk]

    # Check manual overrides
    if action_id in MANUAL_OVERRIDES:
        override_base = MANUAL_OVERRIDES[action_id]
        override_key = normalize_key(override_base)
        if override_key in lookup:
            return lookup[override_key]
        # Try with _unfaded_icon suffix
        full_key = normalize_key(override_base + "_Unfaded_Icon")
        if full_key in lookup:
            return lookup[full_key]

    return None


def is_generic_icon(icon_path: str) -> bool:
    """Check if an icon path is a generic placeholder."""
    if not icon_path:
        return True
    normalized = icon_path.lower()
    for default in GENERIC_DEFAULTS:
        if normalized == default.lower():
            return True
    # Also catch missing/non-existent paths that would fall through to fallback
    return False


def fix_json_file(file_path: str, lookup: dict) -> tuple[int, int]:
    """Fix icon paths in a JSON file. Returns (fixed_count, skipped_count)."""
    if not os.path.exists(file_path):
        print(f"  [SKIP] File not found: {file_path}")
        return 0, 0

    with open(file_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    actions = data.get("actions", [])
    if not actions:
        print(f"  [SKIP] No actions array in: {os.path.basename(file_path)}")
        return 0, 0

    fixed = 0
    skipped = 0
    not_found = []

    for action in actions:
        action_id = action.get("id", "")
        action_name = action.get("name", "")
        current_icon = action.get("icon", "")

        if not is_generic_icon(current_icon):
            # Icon already set to something specific — leave it
            skipped += 1
            continue

        new_icon = find_icon_for_id(action_id, action_name, lookup)
        if new_icon:
            old = action.get("icon", "<none>")
            action["icon"] = new_icon
            fixed += 1
            print(f"    [{os.path.basename(file_path)}] {action_id}: {old} -> {new_icon}")
        else:
            not_found.append(action_id)

    if not_found:
        print(f"  [WARN] No icon found for: {', '.join(not_found)}")

    if fixed > 0:
        with open(file_path, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
            f.write("\n")

    return fixed, skipped


def main():
    print("=== Fix Spell Icons ===")
    print(f"Repo root: {REPO_ROOT}")

    print("\nBuilding icon lookup...")
    lookup = build_icon_lookup()

    total_fixed = 0
    total_skipped = 0

    print("\nProcessing JSON files...")
    for json_file in JSON_FILES:
        path = os.path.join(DATA_ACTIONS, json_file)
        print(f"\n  {json_file}")
        fixed, skipped = fix_json_file(path, lookup)
        total_fixed += fixed
        total_skipped += skipped
        print(f"  -> Fixed: {fixed}, Already correct: {skipped}")

    print(f"\n=== Done: {total_fixed} icons fixed, {total_skipped} already set ===")


if __name__ == "__main__":
    main()
