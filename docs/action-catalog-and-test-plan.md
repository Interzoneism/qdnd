# Action Catalog & Full-Fidelity Test Plan

Generated: 2026-02-11 | Last tested: 2026-02-11 | Results: **123/124 PASS** (1 SKIP)

## Test Command Template
```bash
./scripts/run_autobattle.sh --full-fidelity --ff-action-test <action_id> --max-time-seconds 10 --seed 42
```

## Test Status Legend
- [ ] Not tested
- [x] PASS
- [!] FAIL (needs fix)

---

## 1. Core Actions (Available to All Characters)

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 1 | `basic_attack` | Basic Attack | singleUnit/enemies | Action | [x] |
| 2 | `ranged_attack` | Ranged Attack | singleUnit/enemies | Action | [x] |
| 3 | `dash_action` | Dash | self | Action | [x] |
| 4 | `disengage_action` | Disengage | self | Action | [x] |
| 5 | `dodge_action` | Dodge | self | Action | [x] |
| 6 | `hide_action` | Hide | self | Action | [x] |
| 7 | `help_action` | Help | singleUnit/allies | Action | [x] |
| 8 | `shove` | Shove | singleUnit/enemies | Bonus Action | [x] |
| 9 | `jump_action` | Jump | point | Bonus Action | [x] |
| 10 | `throw_action` | Throw | singleUnit/enemies | Action | [x] |
| 11 | `dip_action` | Dip | self | Bonus Action | [x] |

## 2. Weapon Actions

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 12 | `cleave` | Cleave | circle/enemies | Action | [x] |
| 13 | `lacerate` | Lacerate | singleUnit/enemies | Action | [x] |
| 14 | `smash` | Smash | singleUnit/enemies | Action | [x] |
| 15 | `topple` | Topple | singleUnit/enemies | Action | [x] |
| 16 | `pommel_strike` | Pommel Strike | singleUnit/enemies | Bonus Action | [x] |
| 17 | `offhand_attack` | Off-Hand Attack | singleUnit/enemies | Bonus Action | [x] |
| 18 | `power_strike` | Power Strike | singleUnit/enemies | Action | [x] |

## 3. Cantrips

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 19 | `fire_bolt` | Fire Bolt | singleUnit/enemies | Action | [x] |
| 20 | `ray_of_frost` | Ray of Frost | singleUnit/enemies | Action | [x] |
| 21 | `sacred_flame` | Sacred Flame | singleUnit/enemies | Action | [x] |
| 22 | `eldritch_blast` | Eldritch Blast | singleUnit/enemies | Action | [x] |
| 23 | `toll_the_dead` | Toll the Dead | singleUnit/enemies | Action | [x] |
| 24 | `chill_touch` | Chill Touch | singleUnit/enemies | Action | [x] |
| 25 | `shocking_grasp` | Shocking Grasp | singleUnit/enemies | Action | [x] |
| 26 | `thorn_whip` | Thorn Whip | singleUnit/enemies | Action | [x] |
| 27 | `vicious_mockery` | Vicious Mockery | singleUnit/enemies | Action | [x] |
| 28 | `poison_spray` | Poison Spray | singleUnit/enemies | Action | [x] |
| 29 | `blade_ward` | Blade Ward | self | Action | [x] |
| 30 | `produce_flame` | Produce Flame | singleUnit/enemies | Action | [x] |

## 4. Level 1 Spells

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 31 | `magic_missile` | Magic Missile | singleUnit/enemies | Action | [x] |
| 32 | `cure_wounds` | Cure Wounds | singleUnit/allies | Action | [x] |
| 33 | `guiding_bolt` | Guiding Bolt | singleUnit/enemies | Action | [x] |
| 34 | `healing_word` | Healing Word | singleUnit/allies | Bonus Action | [x] |
| 35 | `shield_of_faith` | Shield of Faith | singleUnit/allies | Bonus/Action | [x] |
| 36 | `thunderwave` | Thunderwave | circle/all | Action | [x] |
| 37 | `burning_hands` | Burning Hands | cone/all | Action | [x] |
| 38 | `chromatic_orb` | Chromatic Orb | singleUnit/enemies | Action | [x] |
| 39 | `witch_bolt` | Witch Bolt | singleUnit/enemies | Action | [x] |
| 40 | `inflict_wounds` | Inflict Wounds | singleUnit/enemies | Action | [x] |
| 41 | `bless` | Bless | multiUnit/allies | Action | [x] |
| 42 | `bane` | Bane | multiUnit/enemies | Action | [x] |
| 43 | `sleep` | Sleep | circle/enemies | Action | [x] |
| 44 | `grease` | Grease | circle/all | Action | [x] |
| 45 | `dissonant_whispers` | Dissonant Whispers | singleUnit/enemies | Action | [x] |
| 46 | `hunters_mark` | Hunter's Mark | singleUnit/enemies | Bonus Action | [x] |
| 47 | `ensnaring_strike` | Ensnaring Strike | self | Bonus Action | [x] |
| 48 | `hail_of_thorns` | Hail of Thorns | self | Bonus Action | [x] |
| 49 | `mage_armor` | Mage Armor | singleUnit/allies | Action | [x] |
| 50 | `armor_of_agathys` | Armor of Agathys | self | Action | [x] |
| 51 | `faerie_fire` | Faerie Fire | circle/all | Action | [x] |
| 52 | `command` | Command | singleUnit/enemies | Action | [x] |
| 53 | `sanctuary` | Sanctuary | singleUnit/allies | Bonus Action | [x] |
| 54 | `create_water` | Create Water | circle/all | Action | [x] |
| 55 | `hex` | Hex | singleUnit/enemies | Bonus Action | [x] |

## 5. Level 2 Spells

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 56 | `shatter` | Shatter | circle/all | Action | [x] |
| 57 | `scorching_ray` | Scorching Ray | singleUnit/enemies | Action | [x] |
| 58 | `hold_person` | Hold Person | singleUnit/enemies | Action | [x] |
| 59 | `blur` | Blur | self | Action | [x] |
| 60 | `moonbeam` | Moonbeam | circle/all | Action | [x] |
| 61 | `silence` | Silence | circle/all | Action | [x] |
| 62 | `lesser_restoration` | Lesser Restoration | singleUnit/allies | Action | [x] |
| 63 | `web` | Web | circle/all | Action | [x] |
| 64 | `darkness` | Darkness | circle/all | Action | [x] |
| 65 | `invisibility` | Invisibility | singleUnit/allies | Action | [x] |
| 66 | `flaming_sphere` | Flaming Sphere | circle/all | Action | [x] |
| 67 | `heat_metal` | Heat Metal | singleUnit/enemies | Action | [x] |
| 68 | `mirror_image` | Mirror Image | self | Action | [x] |
| 69 | `spike_growth` | Spike Growth | circle/all | Action | [x] |
| 70 | `cloud_of_daggers` | Cloud of Daggers | circle/all | Action | [x] |
| 71 | `spiritual_weapon` | Spiritual Weapon | self | Bonus Action | [x] |

## 6. Level 3 Spells

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 72 | `fireball` | Fireball | circle/all | Action | [x] |
| 73 | `lightning_bolt` | Lightning Bolt | line/all | Action | [x] |
| 74 | `spirit_guardians` | Spirit Guardians | self | Action | [x] |
| 75 | `haste` | Haste | singleUnit/allies | Action | [x] |
| 76 | `slow` | Slow | multiUnit/enemies | Action | [x] |
| 77 | `hunger_of_hadar` | Hunger of Hadar | circle/all | Action | [x] |
| 78 | `hypnotic_pattern` | Hypnotic Pattern | circle/all | Action | [x] |
| 79 | `mass_healing_word` | Mass Healing Word | multiUnit/allies | Bonus Action | [x] |
| 80 | `call_lightning` | Call Lightning | circle/all | Action | [x] |
| 81 | `spirit_shroud` | Spirit Shroud | self | Bonus Action | [x] |
| 82 | `bestow_curse` | Bestow Curse | singleUnit/enemies | Action | [x] |
| 83 | `revivify` | Revivify | singleUnit/allies | Action | [SKIP] |

> **Note:** `revivify` requires a dead ally target, which is impossible in the 1v1 test scenario.

## 7. Class Features

| # | Action ID | Name | Class | Target Type | Action Cost | Status |
|---|-----------|------|-------|-------------|-------------|--------|
| 84 | `action_surge` | Action Surge | Fighter | self | Bonus Action | [x] |
| 85 | `second_wind` | Second Wind | Fighter | self | Bonus Action | [x] |
| 86 | `trip_attack` | Trip Attack | Fighter/BM | singleUnit/enemies | Action | [x] |
| 87 | `menacing_attack` | Menacing Attack | Fighter/BM | singleUnit/enemies | Action | [x] |
| 88 | `rage` | Rage | Barbarian | self | Bonus Action | [x] |
| 89 | `reckless_attack` | Reckless Attack | Barbarian | self | Free | [x] |
| 90 | `frenzy` | Frenzy | Barbarian | singleUnit/enemies | Bonus Action | [x] |
| 91 | `cunning_action_dash` | Cunning Action: Dash | Rogue | self | Bonus Action | [x] |
| 92 | `cunning_action_disengage` | Cunning Action: Disengage | Rogue | self | Bonus Action | [x] |
| 93 | `cunning_action_hide` | Cunning Action: Hide | Rogue | self | Bonus Action | [x] |
| 94 | `sneak_attack` | Sneak Attack | Rogue | singleUnit/enemies | Action | [x] |
| 95 | `flurry_of_blows` | Flurry of Blows | Monk | singleUnit/enemies | Bonus Action | [x] |
| 96 | `stunning_strike` | Stunning Strike | Monk | singleUnit/enemies | Free | [x] |
| 97 | `step_of_the_wind` | Step of the Wind | Monk | self | Bonus Action | [x] |
| 98 | `patient_defence` | Patient Defence | Monk | self | Bonus Action | [x] |
| 99 | `divine_smite` | Divine Smite | Paladin | singleUnit/enemies | Free | [x] |
| 100 | `lay_on_hands` | Lay on Hands | Paladin | singleUnit/allies | Action | [x] |
| 101 | `turn_undead` | Turn Undead | Cleric | circle/enemies | Action | [x] |
| 102 | `preserve_life` | Preserve Life | Cleric | circle/allies | Action | [x] |
| 103 | `guided_strike` | Guided Strike | Cleric/War | self | Free | [x] |
| 104 | `war_priest` | War Priest | Cleric/War | singleUnit/enemies | Bonus Action | [x] |
| 105 | `bardic_inspiration` | Bardic Inspiration | Bard | singleUnit/allies | Bonus Action | [x] |
| 106 | `wild_shape_wolf` | Wild Shape: Wolf | Druid | self | Action | [x] |
| 107 | `wild_shape_bear` | Wild Shape: Bear | Druid | self | Action | [x] |
| 108 | `wild_shape_spider` | Wild Shape: Spider | Druid | self | Action | [x] |
| 109 | `symbiotic_entity` | Symbiotic Entity | Druid/Spores | self | Bonus Action | [x] |
| 110 | `create_sorcery_points` | Create Sorcery Points | Sorcerer | self | Bonus Action | [x] |

## 8. Feat Abilities

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 111 | `great_weapon_master_toggle` | GWM Power Attack | self | Free (toggle) | [x] |
| 112 | `sharpshooter_toggle` | Sharpshooter | self | Free (toggle) | [x] |
| 113 | `polearm_butt_attack` | Polearm Butt Attack | singleUnit/enemies | Bonus Action | [x] |
| 114 | `tavern_brawler_throw` | Tavern Brawler: Throw | singleUnit/enemies | Action | [x] |

## 9. Racial Abilities (Breath Weapons)

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 115 | `acid_breath_line` | Acid Breath (Line) | line/all | Action | [x] |
| 116 | `fire_breath_line` | Fire Breath (Line) | line/all | Action | [x] |
| 117 | `fire_breath_cone` | Fire Breath (Cone) | cone/all | Action | [x] |
| 118 | `cold_breath_cone` | Cold Breath (Cone) | cone/all | Action | [x] |
| 119 | `lightning_breath_line` | Lightning Breath (Line) | line/all | Action | [x] |
| 120 | `poison_breath_cone` | Poison Breath (Cone) | cone/all | Action | [x] |

## 10. Miscellaneous / Samples

| # | Action ID | Name | Target Type | Action Cost | Status |
|---|-----------|------|-------------|-------------|--------|
| 121 | `poison_strike` | Poison Strike | singleUnit/enemies | Action | [x] |
| 122 | `battle_cry` | Battle Cry | all/allies | Bonus Action | [x] |
| 123 | `heal_wounds` | Heal Wounds | singleUnit/allies | Action | [x] |
| 124 | `globe_of_invulnerability` | Globe of Invulnerability | self | Action | [x] |

---

## Excluded from Testing (Reactions / Passives / Utility)

These abilities require specific trigger conditions, scale with level, or have no combat effect. 
They cannot be meaningfully tested via `--ff-action-test` in isolation:

- `shield` (Reaction)
- `counterspell` (Reaction)
- `tenacity` (Reaction)
- `riposte` (Reaction, maneuver)
- `cutting_words`, `cutting_words_d6`, `cutting_words_d10` (Reaction)
- `sentinel_reaction` (Reaction)
- `polearm_opportunity` (Reaction)
- `war_caster_reaction` (Reaction)
- `lucky_attack`, `lucky_check`, `lucky_save`, `lucky_enemy_reroll` (Reaction/conditional)
- `hellish_rebuke` (Reaction)
- `war_gods_blessing` (Reaction)
- `colossus_slayer` (Passive toggle)
- `aura_of_protection` (Passive)
- `divine_smite_toggle` (Toggle, tested alongside melee)
- `blade_flourish` (Requires bardic inspiration resource)
- `astral_knowledge`, `mage_hand`, `enhance_leap`, `wizard_cantrip_choice`, `arcane_recovery`, `primeval_awareness`, `hide_in_plain_sight` (Utility/no combat effect)
- `end_wild_shape` (Requires wild shape active first)
- `intimidating_presence` (AoE frighten - could test but niche)
- Scaled cantrip variants (`fire_bolt_5`, `fire_bolt_11`, etc.) - same as base, just higher damage
- `divine_smite_2` (Variant of divine_smite)
- `eldritch_blast_2`, `eldritch_blast_11` (Scaled variants)
- `bardic_inspiration_d6`, `bardic_inspiration_d10` (Variants)

---

## Testing Execution Order

**Phase 1 - Core Combat (1-18):** Basic attacks and weapon actions - the foundation.
**Phase 2 - Cantrips (19-30):** Ranged/melee cantrips - the bread and butter.
**Phase 3 - Level 1 Spells (31-55):** Most commonly used spells.
**Phase 4 - Level 2 Spells (56-71):** Mid-tier spells.
**Phase 5 - Level 3 Spells (72-83):** High-impact spells.
**Phase 6 - Class Features (84-110):** Class-specific mechanics.
**Phase 7 - Feats & Racial (111-120):** Specialized abilities.
**Phase 8 - Misc (121-124):** Sample/legacy abilities.
