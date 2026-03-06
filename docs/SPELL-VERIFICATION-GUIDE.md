# Spell Verification Guide

> **Audience**: AI agents performing autonomous spell-by-spell verification.
> **Last updated**: 2026-03-03

---

## 1. Overview

### Purpose
Systematically verify every action and spell in the QDND combat system against the [BG3 wiki](https://bg3.wiki) for accuracy. Each spell receives an individual verification pass where its behavior (damage, saves, statuses, AoE, concentration, range, etc.) is compared to the canonical BG3 data.

### Scale
| Tier | Count | Description |
|------|-------|-------------|
| Priority spells | ~120 | Listed in `batch_ability_test.sh`, tracked in `spell_verification_tracker.json` |
| Total actions | ~1500+ | All BG3-parsed spells + 427 supplementary JSON actions in `ActionRegistry` |

Each spell gets:
1. A BG3 wiki lookup for expected behavior
2. A full-fidelity test run with specific parameters
3. A structured verdict recorded in the tracker

### Key Files
| File | Purpose |
|------|---------|
| `Data/Validation/spell_verification_tracker.json` | Master tracker — status of every spell |
| `Data/Validation/verification_configs/{id}.json` | Per-spell test configuration (optional) |
| `Scripts/run_spell_verification.ps1` | Windows runner for a single spell |
| `Scripts/run_spell_verification_batch.ps1` | Batch runner for all unverified spells |
| `scripts/batch_ability_test.sh` | Existing bash batch test (smoke-test level) |
| `combat_log.jsonl` | Runtime combat log output (JSONL) |

---

## 2. Agent Workflow (Step by Step)

### Step 1: Pick the Next Spell

Read `Data/Validation/spell_verification_tracker.json`. Scan the `categories` object in priority order (lowest `priority` number first). Within each category, find the first entry with `"status": "unverified"`.

```bash
# Quick check from terminal
grep -c '"unverified"' Data/Validation/spell_verification_tracker.json
```

Skip entries with `"status": "blocked"` — these cannot be tested due to known functor/system stubs.

### Step 2: Research on BG3 Wiki

Use the `fetch_webpage` tool to load the spell's wiki page:

```
https://bg3.wiki/wiki/{Spell_Name}
```

**URL rules** (see [Section 4](#4-bg3-wiki-url-patterns) for full details):
- Title-case each word
- Replace spaces with underscores
- Example: `magic_missile` → `https://bg3.wiki/wiki/Magic_Missile`

The wiki page provides authoritative values for damage, saves, conditions, range, AoE, concentration, duration, and upcast scaling.

### Step 3: Extract Expected Behavior

From the wiki page, note the following fields (as applicable):

| Field | Example | Where to Find on Wiki |
|-------|---------|----------------------|
| Spell level | 3 | Info box, top of page |
| School | Evocation | Info box |
| Damage dice | 8d6 | Description / damage section |
| Damage type | Fire | Description / damage section |
| Save type | Dexterity | Description |
| Half damage on save | true | "Save: ... for half damage" |
| Concentration | true/false | Info box, "Concentration" tag |
| AoE type | Sphere/Cone/Line/Cube | Info box or description |
| AoE radius/size | 4m / 6m / etc. | Description |
| Range | 18m | Info box |
| Status applied | Paralyzed, Frightened, etc. | Description |
| Duration | 10 turns / Until long rest | Info box |
| Upcast scaling | +1d6 per level | "At Higher Levels" section |

### Step 4: Design Test Parameters

Based on the spell's category, choose the test setup:

| Spell Type | Caster Level | Targets | Formation | Notes |
|------------|-------------|---------|-----------|-------|
| Single-target damage | spell_level + 2 (min 3) | 1 hostile | single | 1v1 |
| AoE damage | spell_level + 2 (min 3) | 3 hostile | cluster | Targets within AoE |
| Healing | spell_level + 2 (min 3) | 1 wounded ally + 1 hostile | single | Need hostile for combat |
| Buff | spell_level + 2 (min 3) | 1 hostile | single | Cast on self, verify status |
| Debuff/Control | spell_level + 2 (min 3) | 1–3 hostile | single or cluster | Verify condition |
| Weapon action | 5 (Fighter/Barbarian) | 1 hostile | single | Correct weapon equipped |
| Class feature | Min level that grants it | 1 hostile | single | Correct class |
| Cantrip | 5 | 1 hostile | single | Level 5 for scaling |
| Concentration | spell_level + 2 | 1 hostile | single | Verify status on caster |

### Step 5: Create Verification Config (Optional)

For non-trivial spells, write a `SpellVerificationSetup` JSON config:

```bash
Data/Validation/verification_configs/{action_id}.json
```

See existing examples: `fireball.json`, `magic_missile.json`, `cure_wounds.json`, `hold_person.json`.

**SpellVerificationSetup JSON schema:**

```json
{
  "action_id": "string — matches ActionRegistry key",
  "caster_class": "string — wizard, cleric, fighter, etc.",
  "caster_level": "int — character level",
  "caster_race": "string — high_elf, human, etc.",
  "target_count": "int — number of targets",
  "target_formation": "string — single, cluster, line, spread",
  "target_faction": "string — hostile, ally",
  "target_distance": "float — distance in meters from caster",
  "targets_wounded": "bool (optional) — for healing tests",
  "bg3_wiki_url": "string — full wiki URL",
  "expected_behavior": {
    "spell_level": "int",
    "school": "string",
    "damage_dice": "string — e.g. 8d6",
    "damage_type": "string — fire, force, radiant, etc.",
    "healing_dice": "string — e.g. 1d8 + WIS modifier",
    "save_type": "string — strength, dexterity, constitution, intelligence, wisdom, charisma",
    "half_damage_on_save": "bool",
    "requires_concentration": "bool",
    "applies_status": "string — paralyzed, frightened, etc.",
    "area_type": "string — sphere, cone, line, cube, cylinder",
    "area_radius": "float — meters",
    "range": "float — meters",
    "duration": "string — e.g. 10 turns, until long rest",
    "upcast_per_level": "string — e.g. 1d6, +1 dart",
    "description": "string — free-form expected behavior summary"
  },
  "notes": "string — test-specific notes for the verifying agent"
}
```

### Step 6: Run the Test

**Simple run (no config):**
```bash
./scripts/run_autobattle.sh --full-fidelity --ff-spell-verify {action_id} --seed 42 --max-time-seconds 15
```

**With config:**
```bash
./scripts/run_autobattle.sh --full-fidelity --ff-spell-verify {action_id} --verify-config Data/Validation/verification_configs/{action_id}.json --seed 42 --max-time-seconds 15
```

**Windows PowerShell:**
```powershell
.\Scripts\run_spell_verification.ps1 -ActionId {action_id}
.\Scripts\run_spell_verification.ps1 -ActionId {action_id} -ConfigPath Data/Validation/verification_configs/{action_id}.json
```

**Parameters:**
| Flag | Default | Description |
|------|---------|-------------|
| `--seed` | 42 | RNG seed for reproducibility |
| `--max-time-seconds` | 15 | Timeout before force-ending |
| `--character-level` | 5 | Caster level |
| `--verify-config` | (none) | Path to SpellVerificationSetup JSON |
| `--log-file` | combat_log.jsonl | Output path for combat log |

### Step 7: Analyze Results

Read `combat_log.jsonl` (or the specified log file) and look for these JSONL event types:

| Event Type | What to Check |
|------------|---------------|
| `ACTION_DETAIL` | `ability_id` matches the spell being tested |
| `DAMAGE_DEALT` | Damage amount, damage type, target |
| `HEALING_DONE` | Healing amount, target |
| `SAVING_THROW` | Save type, DC, result (pass/fail) |
| `STATUS_APPLIED` | Status name, target, duration |
| `STATUS_REMOVED` | Status removal (concentration break, etc.) |
| `CONCENTRATION_START` | Concentration tracking on caster |
| `CONCENTRATION_BREAK` | Concentration broken by damage |
| `BATTLE_END` | Confirms combat completed without crash |

**Quick log analysis:**
```bash
# Did the spell fire?
grep "ability_id.*{action_id}" combat_log.jsonl

# Any damage?
grep "DAMAGE_DEALT" combat_log.jsonl

# Any status applied?
grep "STATUS_APPLIED" combat_log.jsonl

# Any saves?
grep "SAVING_THROW" combat_log.jsonl
```

### Step 8: Compare Against Expected Behavior

Run through this checklist for the spell:

- [ ] **Damage type** — Matches wiki (fire, force, radiant, etc.)
- [ ] **Damage range** — Within expected dice range (e.g. 8d6 = 8–48)
- [ ] **Save type** — Correct ability (STR/DEX/CON/INT/WIS/CHA)
- [ ] **Half damage on save** — If wiki says "half on save", verify reduced damage on successful save
- [ ] **Status applied** — Matches expected condition (Paralyzed, Frightened, Prone, etc.)
- [ ] **Concentration tracked** — If concentration spell, caster has concentration status
- [ ] **AoE hit count** — If AoE, correct number of targets hit
- [ ] **Range** — Spell fires at expected range (not blocked as "out of range")
- [ ] **Healing amount** — Within expected dice range
- [ ] **Duration** — Status lasts expected number of turns

### Step 9: Record Verdict

Update the spell's entry in `Data/Validation/spell_verification_tracker.json`:

| Verdict | Meaning | When to Use |
|---------|---------|-------------|
| `"pass"` | All checks match BG3 wiki | Everything works correctly |
| `"fail"` | One or more checks don't match | Wrong damage type, missing save, etc. |
| `"partial"` | Core behavior works, minor differences | Damage works but missing upcast, etc. |
| `"blocked"` | Cannot test due to system limitation | Functor stub, missing system |
| `"crash"` | Test crashes or times out | Exception, freeze, timeout |

**Update these fields:**
```json
{
  "status": "pass",
  "verified_date": "2026-03-03",
  "notes": "Verified against BG3 wiki. 8d6 fire, DEX save, 4m radius AoE. All 3 targets hit.",
  "issues": []
}
```

For failures, populate `issues`:
```json
{
  "status": "fail",
  "verified_date": "2026-03-03",
  "notes": "Damage type is wrong",
  "issues": ["Deals radiant damage instead of fire damage (BG3 wiki says fire)"]
}
```

**Also update the `summary` object** at the top of the tracker to reflect the new counts.

### Step 10: Write Verification Notes

Use the checklist template from [Section 6](#6-verification-checklist-template) to write structured notes. Include:
- What was checked
- What matched the wiki
- What didn't match (with specifics)
- Any bugs filed or code fixes needed
- The seed used and whether the result is reproducible

---

## 3. Spell Category Guidelines

### Cantrips
- **Level**: 5 (for cantrip scaling — most cantrips gain extra dice at level 5)
- **Targets**: 1 hostile (single formation)
- **Class**: Match the cantrip's spell list (Wizard for `fire_bolt`, Cleric for `sacred_flame`, etc.)
- **Verify**: Damage scales to 2 dice at level 5

### Level 1–3 Spells
- **Level**: `spell_level + 2` (minimum 3)
- **Targets**: 1 for single-target, 3 clustered for AoE
- **Class**: Match primary spell list
- **Verify**: Base damage, correct save type, status effects

### Level 4–6 Spells
- **Level**: `max(spell_level + 2, 7)`
- **Targets**: Use wider spacing for large-AoE spells
- **Verify**: Higher-level slot availability, AoE coverage

### Weapon Actions
- **Class**: Fighter or Barbarian (level 5 for Extra Attack)
- **Weapon**: Correct weapon type for the action (greatsword for Cleave, etc.)
- **Targets**: 1 hostile, melee range
- **Verify**: Bonus effect (prone for Topple, bleeding for Lacerate, etc.)

### Class Features
- **Class**: The specific class at minimum level that grants the feature
- **Targets**: Varies by feature
- **Verify**: Resource consumption (Ki, Superiority Dice, etc.), effect application

### Racial Abilities
- **Race**: The specific race/subrace that grants the ability
- **Class**: Any (Fighter is a safe default)
- **Level**: 3+ (racial abilities available from level 1, but level 3 gives more HP buffer)
- **Verify**: Damage, AoE shape, save type

### Healing Spells
- **Setup**: Caster + 1 wounded ally + 1 hostile
- **Target**: The wounded ally
- **Verify**: Healing amount within expected dice range, touch/ranged range

### Buff Spells
- **Setup**: 1v1 (caster + 1 hostile)
- **Target**: Self or ally
- **Verify**: Status applied, correct duration, concentration if applicable

### Control / Debuff Spells
- **Setup**: 1–3 hostile targets
- **Verify**: Condition applied on failed save, save repeat at end of turn if applicable

### Concentration Spells
- **Verify**: `CONCENTRATION_START` event after casting, concentration status on caster
- **Bonus**: If possible, verify concentration break on damage (look for `CONCENTRATION_BREAK`)

### Summon Spells
- **Status**: Mark as `"blocked"` — the summon system is incomplete
- **Notes**: Document which functor is missing

### Surface Spells
- **Status**: Mark as `"blocked"` — the `SpawnSurface` functor is a stub
- **Notes**: Document that the surface creation pipeline doesn't execute

---

## 4. BG3 Wiki URL Patterns

### Base URL
```
https://bg3.wiki/wiki/
```

### Standard Spells
Convert the `action_id` to title case, replace underscores with underscores in the URL:
```
magic_missile    → https://bg3.wiki/wiki/Magic_Missile
cure_wounds      → https://bg3.wiki/wiki/Cure_Wounds
fireball         → https://bg3.wiki/wiki/Fireball
hold_person      → https://bg3.wiki/wiki/Hold_Person
shield_of_faith  → https://bg3.wiki/wiki/Shield_of_Faith
```

### Core Actions
```
main_hand_attack → https://bg3.wiki/wiki/Melee_Attack
ranged_attack    → https://bg3.wiki/wiki/Ranged_Attack
dash             → https://bg3.wiki/wiki/Dash_(Action)
disengage        → https://bg3.wiki/wiki/Disengage_(Action)
dodge_action     → https://bg3.wiki/wiki/Dodge_(Action)
hide             → https://bg3.wiki/wiki/Hide_(Action)
help             → https://bg3.wiki/wiki/Help_(Action)
shove            → https://bg3.wiki/wiki/Shove_(Action)
jump             → https://bg3.wiki/wiki/Jump_(Action)
throw            → https://bg3.wiki/wiki/Throw_(Action)
dip              → https://bg3.wiki/wiki/Dip_(Action)
```

### Class Features
```
action_surge             → https://bg3.wiki/wiki/Action_Surge
second_wind              → https://bg3.wiki/wiki/Second_Wind
cunning_action_dash      → https://bg3.wiki/wiki/Cunning_Action:_Dash
cunning_action_disengage → https://bg3.wiki/wiki/Cunning_Action:_Disengage
cunning_action_hide      → https://bg3.wiki/wiki/Cunning_Action:_Hide
wild_shape_wolf          → https://bg3.wiki/wiki/Wild_Shape:_Wolf
wild_shape_bear          → https://bg3.wiki/wiki/Wild_Shape:_Bear
wild_shape_spider        → https://bg3.wiki/wiki/Wild_Shape:_Spider
```

### Feats
```
great_weapon_master_toggle → https://bg3.wiki/wiki/Great_Weapon_Master
sharpshooter_toggle        → https://bg3.wiki/wiki/Sharpshooter
polearm_butt_attack        → https://bg3.wiki/wiki/Polearm_Master
tavern_brawler_throw       → https://bg3.wiki/wiki/Tavern_Brawler
```

### Racial Abilities (Dragonborn Breath Weapons)
```
acid_breath_line      → https://bg3.wiki/wiki/Acid_Breath
fire_breath_line      → https://bg3.wiki/wiki/Fire_Breath
fire_breath_cone      → https://bg3.wiki/wiki/Fire_Breath
cold_breath_cone      → https://bg3.wiki/wiki/Cold_Breath
lightning_breath_line  → https://bg3.wiki/wiki/Lightning_Breath
poison_breath_cone    → https://bg3.wiki/wiki/Poison_Breath
```

### When a URL Doesn't Work
1. Try adding `_(Action)` or `_(Spell)` suffix
2. Search on `https://bg3.wiki/w/index.php?search={term}`
3. Check the class page for class features
4. Check the weapon page for weapon actions
5. Note the correct URL in the verification config for future reference

---

## 5. Known Limitations

These limitations affect what can and cannot be verified. Mark affected spells as `"blocked"` in the tracker.

### Functor Stubs (do nothing — log warning only)
| Functor | Affected Spells | Impact |
|---------|----------------|--------|
| `SpawnSurface` | Grease, Web, Darkness, Spike Growth, Create Water, Fog Cloud, Entangle | No surface created; spell appears to do nothing |
| `Teleport` | Misty Step, Dimension Door, Thunder Step | No teleportation occurs |
| `UseSpell` | Chained/triggered spells | Follow-up spell doesn't fire |
| `Resurrect` | Revivify (partial) | May not restore properly |
| `Counterspell` | Counterspell | Cannot counter spells |
| `SummonInInventory` | Conjure spells | No items conjured |
| `Explode` | Some AoE follow-ups | Explosion effect missing |
| `CreateZone` | Zone-based spells | Zone not created |
| `FireProjectile` | Some projectile spells | Visual only, may still deal damage |
| `Douse` | Douse effects | Cannot extinguish fires |

### Incomplete Systems
| System | Impact |
|--------|--------|
| ObscurementService zones | Darkness/Fog Cloud don't create obscurement zones |
| Barrier system | Damage absorption layer exists but nothing feeds it |
| Exhaustion levels 2–6 | Only Level 1 (disadvantage) implemented |
| Summon system | Summon spells won't create creatures |

### Known Quirks
| Quirk | Impact |
|-------|--------|
| ConditionEvaluator fail-open | Unknown BG3 condition functions return `true` — some conditions may not filter correctly |
| Reaction budget flags | Reactions skip range/budget validation during execution (checked at prompt time) |
| Flaming Sphere / Spiritual Weapon | Likely blocked (summon-like spells) |
| Moonbeam / Call Lightning | May partially work (damage might fire but zone won't persist) |

---

## 6. Verification Checklist Template

Copy this template when writing verification notes for a spell:

```markdown
## Verification: {spell_name}
**Action ID**: {action_id}
**BG3 Wiki**: {url}
**Test Command**: `./scripts/run_autobattle.sh --full-fidelity --ff-spell-verify {action_id} --seed 42 --max-time-seconds 15`
**Test Seed**: 42
**Test Date**: {YYYY-MM-DD}

### Expected (from BG3 wiki)
- Spell Level: {N}
- School: {school}
- Damage: {dice} {type}
- Save: {ability} (half on save: {yes/no})
- Range: {N}m
- AoE: {type}, {radius}m
- Concentration: {yes/no}
- Status: {condition}
- Duration: {N turns / until long rest}
- Upcast: {scaling}

### Observed
- Damage dealt: {amount} {type}
- Save triggered: {ability} DC {N}, result {pass/fail}
- Status applied: {condition} on {target}
- Targets hit: {N}
- Concentration: {tracked/not tracked}
- Healing: {amount} on {target}

### Checklist
- [ ] Correct damage type
- [ ] Damage in expected range
- [ ] Correct save type
- [ ] Half damage on successful save
- [ ] Status applied matches expected
- [ ] Concentration tracked
- [ ] AoE hits multiple targets
- [ ] Range appears correct

### Verdict: PASS / FAIL / PARTIAL / BLOCKED
### Notes:
{Free-form notes about what matched, what didn't, bugs found, etc.}
```

---

## 7. Priority Order

Spells are verified in this priority order (lowest number = highest priority):

| Priority | Category | Count | Rationale |
|----------|----------|-------|-----------|
| 1 | Core actions | 11 | Every character uses these every combat |
| 2 | Cantrips | 12 | Most frequently cast spells (at-will) |
| 3 | Level 1 spells | 25 | Most common spell slot level |
| 4 | Level 2 spells | 16 | Second most common |
| 5 | Level 3 spells | 12 | Powerful spells, still common |
| 6 | Class features | 27 | Core class identity abilities |
| 7 | Weapon actions | 6 | Martial ability verification |
| 8 | Racial abilities | 6 | Race-specific features |
| 9 | Feats | 4 | Optional character customization |
| 10 | Miscellaneous | 1 | Edge cases, high-level spells |

**Within each category**, verify spells in the order they appear in `spell_verification_tracker.json`. This order matches `batch_ability_test.sh` for consistency.

**After the priority 120**: The remaining ~1,380 actions in `ActionRegistry` can be verified in follow-up passes, grouped by spell level and school.

---

## 8. Troubleshooting

### Test Times Out
- Increase `--max-time-seconds` (try 30)
- Check if the spell requires specific conditions to cast (e.g., concentration, resources)
- Verify the action ID exists: `grep "{action_id}" Data/Actions/*.json`

### Spell Not Cast
- Check the combat log for `ACTION_DETAIL` events — the AI may have chosen a different action
- Verify the caster has the spell on their spell list
- Verify sufficient spell slots for the spell level
- Try a different seed

### Crash / Exception
- Read the full stdout output for stack traces
- Check `artifacts/autobattle/` for crash logs
- Common causes: null reference in effect pipeline, missing boost or condition handler

### No Combat Log Output
- Verify `--log-file` path is writable
- Check that the Godot binary was found (look for "Godot binary not found" in output)
- Ensure the `--full-fidelity` flag is present (required for spell verify mode)

### Spell Exists but Behaves Differently than Wiki
- This is a legitimate finding — record as `"fail"` with detailed notes
- Check if the BG3 data was parsed differently: `grep "{action_id}" Data/Actions/action-registry*.json`
- Check for known parity gaps: `Data/Validation/parity_allowlist.json`
