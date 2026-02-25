# BG3 vs QDND Gap Analysis Report

**Generated from:** exhaustive codebase survey + BG3 wiki cross-reference  
**Scope:** Spells · Classes · Backgrounds · Races  
**Architecture rules:** `ResolvedCharacter`/`CharacterSheet` only; `ActionRegistry` only; `ConditionEffects.cs` as sole mechanic authority; `EquipSlot` 12-slot; `(int)Math.Floor((score-10)/2.0)` ability mod formula.

---

## Table of Contents
1. [Spells](#1-spells)
2. [Classes](#2-classes)
3. [Backgrounds](#3-backgrounds)
4. [Races](#4-races)
5. [Cross-Cutting Issues](#5-cross-cutting-issues)
6. [Priority Matrix](#6-priority-matrix)

---

## 1. Spells

### BG3 Target Behaviour
**Wiki:** https://bg3.wiki/wiki/Spells

- 8 schools: Abjuration, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation.
- Spell levels 0 (cantrips) through 6 for player characters.
- Prepared vs Known vs Always-prepared per class — see sub-headings below.
- Concentration: one concentration spell at a time; broken by taking damage (BG3 = Constitution Save DC 10 or half damage taken, whichever is higher).
- Upcasting: cast a lower-level spell in a higher slot for increased effect.
- Ritual casting: cast qualifying spells without expending a slot (takes longer; BG3 implements this as a "free" toggle or additional button).
- Pact Magic (Warlock): always casts at highest pact level; regains on short rest.  
- Eldritch Knight / Arcane Trickster: ⅓ casters (modifier 0.333) — Int-based, only Abjuration/Evocation (EK) or Illusion/Enchantment (AT).
- Spellcasting abilities: Int — Wizard, EK, AT; Wis — Cleric, Druid, Ranger; Cha — Bard, Paladin, Sorcerer, Warlock.

### Current Codebase State

| File | Details |
|---|---|
| `Data/Actions/bg3_spells_phase3.json` | 82 entries, 80 have `spellLevel` |
| `Data/Actions/bg3_spells_expanded.json` | 38 entries, all spell |
| `Data/Actions/bg3_spells_high_level.json` | 22 entries, all spell |
| `Data/Actions/bg3_spells_phase3b.json` | 12 entries, all spell |
| `Data/Actions/bg3_spells_phase4.json` | 7 entries, all spell |
| `Data/Actions/bg3_mechanics_actions.json` | 226 entries; 66 have `spellLevel`; 160 are non-spell class abilities |

**Total unique spell entries (across ALL loaded files): 225**  
**Total unique action IDs (spells + non-spell abilities): 415**

All `*.json` files in `Data/Actions/` are loaded by `Data/Actions/ActionRegistryInitializer.cs` line 165 (`Directory.GetFiles(dataActionsPath, "*.json")`).

**Spell level distribution (phase files):** L0: 6 · L1: 36 · L2: 35 · L3: 33 · L4: 20 · L5: 14 · L6: 15 = **159 entries**  
**Mechanics file adds 66 more spell entries** (fireball, magic_missile, cure_wounds, healing_word, etc.).

**Key spells confirmed present:**  
✓ fireball · magic_missile · fire_bolt · counterspell · healing_word · cure_wounds · sacred_flame · haste · spiritual_weapon · guiding_bolt · shield · sleep · mage_armor · hex · hunter's_mark · eldritch_blast · bless · minor_illusion · guidance · prestidigitation · produce_flame · ray_of_frost · toll_the_dead · true_strike

**Mechanical systems status:**
- `requiresConcentration` flag: ✓ set per spell
- `Data/Spells/SpellUpcastRules.cs`: ✓ per-spell upcast scaling (dice, targets, flat)
- `Data/CharacterModel/CharacterResolver.cs MergeMulticlassSpellSlots` (line 599): ✓ ESL calculation with `MulticlassSlotTable`
- Warlock Pact Magic `WarlockSpellSlot` resource: ✓ separate slot type in `ResourceManager.cs`
- Ritual casting: **NOT IMPLEMENTED** — no `isRitual` flag parsed or mechanic present

### Gaps & Flaws

| ID | Severity | Description | File / Line |
|---|---|---|---|
| SP-01 | **CRITICAL** | All 225 spell entries **lack `spellSchool` field**. The 5 `bg3_spells_*.json` phase files have `"spellSchool": "MISSING"` (i.e., the field key exists but all values are the string literal `"MISSING"`). The 66 spells in `bg3_mechanics_actions.json` have **no** `spellSchool` key at all. Result: school-based filtering (Wizard Arcane Recovery applies to Abjuration/Evocation for EK, school specializations, Divination checks) is broken for all spells. | All 5 phase JSON files + `bg3_mechanics_actions.json` |
| SP-02 | **CRITICAL** | Multiclass Eldritch Knight and Arcane Trickster contribute 0 to ESL — see [Classes bug CL-01](#2-classes). | `Data/Classes/martial_classes.json` + `CharacterResolver.cs:619` |
| SP-03 | **MAJOR** | No ritual casting mechanic. BG3 allows Wizard/Cleric/Druid/Bard to cast ritual-eligible spells without expending a slot. No `isRitual` or equivalent field exists on any spell entry, and `RulesEngine.cs` has no ritual-slot path. | — |
| SP-04 | **MAJOR** | Concentration break on damage not modelled. BG3 requires a Constitution Save (DC 10 or ½ damage taken) on any damage while concentrating. `RulesEngine.cs` has no save-vs-concentration logic. | `Combat/Rules/RulesEngine.cs` |
| SP-05 | **MAJOR** | School-gated spell learning for EK (Abjuration + Evocation only) and AT (Enchantment + Illusion only) is not enforced at character creation or at level-up. No `allowedSchools` field on subclass data. | `Data/Classes/martial_classes.json` |
| SP-06 | **MAJOR** | Multiple BG3 staple spells not yet in any JSON file: `find_familiar`, `conjure_animals`, `wall_of_fire`, `greater_invisibility`, `polymorph`, `conjure_elemental`, `wall_of_stone`, `Otto's_Irresistible_Dance`, `Banishing_Smite`, `Staggering_Smite`, `power_word_kill`, `chain_lightning`, `conjure_fey`. These are all BG3-valid player spells through level 6. | — |
| SP-07 | **MINOR** | Divination school is effectively at 0 in the named-school phase files (spellSchool field filled as "MISSING"). Entire category of spells — detect magic, scrying, divination (spell), find traps, locate creature — is absent or untagged. | bg3_spells_*.json |
| SP-08 | **MINOR** | Only 6 cantrips (in phase files) vs. BG3's fuller cantrip list. Cantrips present in mechanics file (fire_bolt, sacred_flame, ray_of_frost, toll_the_dead, guidance, prestidigitation, minor_illusion, produce_flame, true_strike) but lack `spellSchool`. Missing: `mending`, `light`, `friends`, `thaumaturgy`, `dancing_lights` as standalone freely choosable cantrips. | — |

---

## 2. Classes

### BG3 Target Behaviour
**Wiki:** https://bg3.wiki/wiki/Classes

12 classes, max level 12. Subclasses chosen at level 3 (most classes).  

| Class | BG3 Subclasses | Caster Type | SpellcasterModifier |
|---|---|---|---|
| Barbarian | Berserker, Wild Magic, Wildheart, Giant | None | 0 |
| Bard | College of Lore, Valour, Swords | Full | 1.0 |
| Cleric | Life, Light, Trickery, War, Knowledge, Nature, Tempest, Death | Full | 1.0 |
| Druid | Circle of Land, Moon, Spores, Stars | Full | 1.0 |
| Fighter | Champion, Battle Master, Eldritch Knight | None/⅓ | 0 (EK: 0.333) |
| Monk | Open Hand, Shadow, 4 Elements | None | 0 |
| Paladin | Oath of Devotion, Ancients, Vengeance, Crown, Oathbreaker | Half | 0.5 |
| Ranger | Hunter, Beast Master, Gloom Stalker | Half | 0.5 |
| Rogue | Thief, Assassin, Arcane Trickster, Swashbuckler | None/⅓ | 0 (AT: 0.333) |
| Sorcerer | Draconic Bloodline, Wild Magic, Storm, Shadow | Full | 1.0 |
| Warlock | Archfey, Fiend, Great Old One, Hexblade | Pact | separate |
| Wizard | Abjuration, Bladesinging, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation | Full | 1.0 |

### Current Codebase State

All 12 classes are implemented with full 12-level `LevelTable` in:
- `Data/Classes/arcane_classes.json` — wizard, sorcerer, warlock, bard
- `Data/Classes/divine_classes.json` — cleric, paladin, druid, ranger
- `Data/Classes/martial_classes.json` — fighter, barbarian, monk, rogue

**`SpellcasterModifier` values found in class data:**

| Class | SpellcasterModifier | Expected |
|---|---|---|
| wizard | (check) | 1.0 |
| sorcerer | (check) | 1.0 |
| warlock | (separate) | Pact |
| bard | (check) | 1.0 |
| cleric | (check) | 1.0 |
| druid | (check) | 1.0 |
| paladin | (check) | 0.5 |
| ranger | (check) | 0.5 |
| **fighter** | **0** | 0 for base; **EK subclass needs 0.333** |
| **rogue** | **0** | 0 for base; **AT subclass needs 0.333** |
| barbarian | 0 | 0 ✓ |
| monk | 0 | 0 ✓ |

**Subclass inventory:**
- Fighter: champion, battle_master, **eldritch_knight**, **arcane_archer** ← arcane_archer not in BG3
- Monk: open_hand, shadow, four_elements, **drunken_master** ← drunken_master not in BG3
- Ranger: hunter, beast_master, gloom_stalker, **swarmkeeper** ← swarmkeeper not in BG3 (it's from Tasha's Cauldron)
- Bard: lore, valour, swords, **glamour** ← glamour not in BG3 base roster per wiki

### Gaps & Flaws

| ID | Severity | Description | File / Line |
|---|---|---|---|
| CL-01 | **CRITICAL** | `Fighter.SpellcasterModifier = 0` and Eldritch Knight subclass has **no `SpellcasterModifier` field**. `CharacterResolver.MergeMulticlassSpellSlots` (line 619) skips any class with `SpellcasterModifier <= 0`. Result: a multiclassed EK/Paladin gets 0 caster levels from their Fighter levels. Fix: add `"SpellcasterModifier": 0.3333` to the `eldritch_knight` subclass entry AND update `MergeMulticlassSpellSlots` to check active subclass modifier. | `Data/Classes/martial_classes.json` (EK subclass); `Data/CharacterModel/CharacterResolver.cs:619` |
| CL-02 | **CRITICAL** | Same bug for Arcane Trickster. `Rogue.SpellcasterModifier = 0`, `arcane_trickster` subclass has **no `SpellcasterModifier`**. Identical fix required. | `Data/Classes/martial_classes.json` (AT subclass); `CharacterResolver.cs:619` |
| CL-03 | **MAJOR** | `CharacterResolver.MergeMulticlassSpellSlots` reads only `classDef.SpellcasterModifier` — it never reads the selected *subclass*'s modifier. Even after adding 0.333 to EK/AT subclass JSON, the resolver won't pick it up unless the lookup path is changed to: if base class modifier is 0, try `GetSubclass(characterSheet.SubclassId).SpellcasterModifier`. | `Data/CharacterModel/CharacterResolver.cs:615–623` |
| CL-04 | **MAJOR** | Four subclasses exist in our JSON that are **not in BG3**: `arcane_archer` (Fighter), `drunken_master` (Monk), `swarmkeeper` (Ranger), `glamour` (Bard). These clutter the subclass picker and may confuse BG3-parity testing. Decision needed: keep as extras or gate behind a non-BG3 flag. | `martial_classes.json`, `divine_classes.json`, `arcane_classes.json` |
| CL-05 | **MAJOR** | Eldritch Knight and Arcane Trickster school restrictions (EK: Abjuration + Evocation; AT: Illusion + Enchantment) are not enforced at spell selection. No `allowedSchools` constraint in subclass data or spell-learning code. | `Data/Classes/martial_classes.json` |
| CL-06 | **MAJOR** | Wild Shape and Rage are implemented as actions, but their limitations are not fully modelled: Wild Shape's available beast options are not defined; Rage's bonus damage per subclass (Berserker vs Wildheart) is not differentiated in `martial_classes.json` feature data. | `Data/Classes/martial_classes.json` |
| CL-07 | **MINOR** | Some class level table entries that should be empty (feat/ASI levels) are correctly empty, relying on the `FeatLevels` array. Verify that the `CharacterResolver` grants an ASI/feat choice when current level is in `FeatLevels` — the path exists but integration test coverage is absent. | `Data/CharacterModel/CharacterResolver.cs` |

---

## 3. Backgrounds

### BG3 Target Behaviour
**Wiki:** https://bg3.wiki/wiki/Backgrounds

12 backgrounds, each granting exactly 2 skill proficiencies. All are available at character creation; Haunted One is restricted to Dark Urge origin characters.

| Background | Skill 1 | Skill 2 | Note |
|---|---|---|---|
| Acolyte | Insight | Religion | |
| Charlatan | Deception | Sleight of Hand | |
| Criminal | Deception | Stealth | |
| Entertainer | Acrobatics | Performance | |
| Folk Hero | Animal Handling | Survival | |
| Guild Artisan | Insight | Persuasion | |
| **Haunted One** | **Medicine** | **Intimidation** | Dark Urge only |
| Noble | History | Persuasion | |
| Outlander | Athletics | Survival | |
| Sage | Arcana | History | |
| Soldier | Athletics | Intimidation | |
| Urchin | Sleight of Hand | Stealth | |

BG3 also grants background-specific Inspiration conditions (acts the character can perform to gain Inspiration for roleplaying reasons — e.g., Acolyte gains Inspiration when assisting a temple, Criminal gains Inspiration when committing successful crimes). These are not used in combat but affect the Inspiration resource.

### Current Codebase State

`Data/Backgrounds/BackgroundData.cs` — static `List<BackgroundEntry>` with 11 entries (C# record with `Id`, `Name`, `string[] SkillProficiencies`).

**Implemented (11/12):**
acolyte, charlatan, criminal, entertainer, folk_hero, guild_artisan, noble, outlander, sage, soldier, urchin

**Confirmed correct skill mappings:** All 11 match BG3 exactly.

### Gaps & Flaws

| ID | Severity | Description | File / Line |
|---|---|---|---|
| BG-01 | **CRITICAL** | `haunted_one` background is entirely absent. Skills: Medicine + Intimidation. Must be added even if gated behind a Dark Urge origin flag. Any Dark Urge character created without this background loses a BG3-mandated option. | `Data/Backgrounds/BackgroundData.cs` |
| BG-02 | **MAJOR** | No Inspiration condition tracking tied to backgrounds. BG3 grants/tracks Inspiration via per-background "ideals" checklist. `ResourceManager.cs` likely has no background-linked Inspiration grant path. Low priority for pure combat, high priority for narrative/roleplay fidelity. | `Combat/Services/ResourceManager.cs` |
| BG-03 | **MINOR** | Skill proficiency strings use PascalCase without spaces (`SleightOfHand`, `AnimalHandling`). Verify that the skill lookup in `CharacterSheet.cs` / `CombatantStats` uses the same casing. A mismatch would silently drop proficiency bonuses. | `Data/Backgrounds/BackgroundData.cs`; cross-check `Data/Stats/` |

---

## 4. Races

### BG3 Target Behaviour
**Wiki:** https://bg3.wiki/wiki/Races

11 playable races, all with flexible +2/+1 ability score improvement (free allocation, not fixed bonuses — changed in BG3 patch 1 to match Mordenkainen's). Each race has subraces as noted.

| Race | Subraces | Key Features |
|---|---|---|
| Human | — | Civil Militia (Pike/Spear/Halberd + Light Armor + Shield), Human Versatility (+25% carry, 1 free skill) |
| Elf | High, Wood | Fey Ancestry, Darkvision 12m, Weapon Training |
| Drow | Lolth-Sworn, Seldarine | Fey Ancestry, Superior Darkvision 24m, Weapon Training, Drow Magic (Dancing Lights L1 / Faerie Fire L3 / Darkness L5) |
| Half-Elf | High, Wood, Drow | Fey Ancestry, Darkvision 12m, Civil Militia profs, 2 free skills (Half-Elf Versatility) |
| Half-Orc | — | Darkvision 12m, Savage Attacks, Relentless Endurance, Intimidation proficiency |
| Halfling | Lightfoot, Strongheart | Lucky (reroll 1s on attack/ability/save), Brave (can't be Frightened) |
| Dwarf | Gold, Shield, Duergar | Dwarven Resilience (poison resistance + adv vs poison), Combat Training, Darkvision 12m; Duergar adds Superior Darkvision 24m + Duergar Resilience + **Duergar Magic** (Enlarge L3, Invisibility L5) |
| Gnome | Forest, Rock, Deep | Gnome Cunning (adv Int/Wis/Cha vs magic), Darkvision 12m |
| Tiefling | Asmodeus, Mephistopheles, Zariel | Hellish Resistance (fire), Darkvision 12m, subrace spells |
| Githyanki | — | Weapon/Armor profs, Astral Knowledge (temp prof in all skills of chosen ability, per long rest), Githyanki Psionics (Mage Hand L1 / Enhance Leap L3 / Misty Step L5) |
| Dragonborn | 10 draconic colors | Draconic Ancestry (elemental resistance + breath weapon matching color) |

### Current Codebase State

- `Data/Races/core_races.json`: human, elf (high_elf, wood_elf), drow (lolth_sworn_drow, seldarine_drow), half_elf (high_half_elf, wood_half_elf, drow_half_elf), half_orc, halfling (lightfoot_halfling, strongheart_halfling) — **8 races (or sub-grouped) in "core" file**.  
- `Data/Races/exotic_races.json`: dwarf (gold_dwarf, shield_dwarf, duergar), gnome (forest_gnome, rock_gnome, deep_gnome), tiefling (asmodeus_tiefling, mephistopheles_tiefling, zariel_tiefling), githyanki, dragonborn (chromatic_dragonborn × 10) — **5 races in "exotic" file**.  
- Total: **All 11 races + all subraces represented.**
- Flexible +2/+1 ASI: Implemented in `CharacterSheet.cs` + `CharacterBuilder.cs`. ✓
- Halfling Lucky `lucky_reroll` tag → `RulesEngine.cs` rerolls natural 1s on attack and save rolls. ✓
- Drow Magic (Dancing Lights, Faerie Fire, Darkness) by subrace level: ✓ defined in `GrantedAbilities`.
- Dragonborn breath weapons (all 10 colors): ✓ correct elements and damage types.
- Half-Orc `relentless_endurance` tag: ✓ present. (Mechanical enforcement — drop to 1 HP instead of 0 — verify in `RulesEngine.cs`).

### Gaps & Flaws

| ID | Severity | Description | File / Line |
|---|---|---|---|
| RC-01 | **CRITICAL** | **Duergar Magic entirely missing.** The `duergar` subrace has only `['superior_darkvision', 'duergar_resilience']`. No `GrantedAbilities` for Enlarge (BG3: available at Character Level 3) or Invisibility (BG3: available at Character Level 5). Per BG3 wiki: https://bg3.wiki/wiki/Duergar | `Data/Races/exotic_races.json` (duergar subrace) |
| RC-02 | **MAJOR** | **Human Versatility missing.** BG3 Human gets: (a) carrying capacity +25% and (b) proficiency in one skill of choice. Current `human_civil_militia` feature has only `Weapons: [Pike, Spear, Halberd]` and `ArmorCategories: [Light, Shield]`. No carrying capacity modifier, no `skill_choice_1` tag, no UI hook for skill selection. | `Data/Races/core_races.json` (human Features) |
| RC-03 | **MAJOR** | **Githyanki Astral Knowledge not mechanically enforced.** The action `astral_knowledge` exists in `bg3_mechanics_actions.json`, but BG3's mechanic is: after a long rest, choose one ability score — gain temporary proficiency in ALL skills governed by that ability for the next day. No code in `RulesEngine.cs` or `ResourceManager.cs` grants/expires these temporary skill proficiencies. | `Data/Actions/bg3_mechanics_actions.json`; `Combat/Rules/RulesEngine.cs` |
| RC-04 | **MAJOR** | **Githyanki Psionics absent.** BG3 Githyanki gain: Mage Hand (cantrip), Enhanced Leap (L3 equivalent jump spell), Misty Step (L5 equivalent). None of these are in the githyanki feature/`GrantedAbilities` list. Compare to Drow Magic which is correctly wired. | `Data/Races/exotic_races.json` (githyanki Features) |
| RC-05 | **MAJOR** | **Forest Gnome "Speak with Animals" frequency unclear.** BG3 grants this at will (no resource cost). Verify that the action entry does not consume a spell slot or per-day charge. | `Data/Races/exotic_races.json`; `Data/Actions/bg3_mechanics_actions.json` |
| RC-06 | **MINOR** | **Rock Gnome Artificer's Lore** grants expertise (double proficiency) on History checks involving magic items. Expertise doubling is not generically applied to skill checks in `RulesEngine.cs` — verify the `artificers_lore` tag has a handler. | `Data/Races/exotic_races.json`; `Combat/Rules/RulesEngine.cs` |
| RC-07 | **MINOR** | **Duergar `DarkvisionOverride: 24` inconsistency.** The `superior_darkvision` feature body sets `DarkvisionRange: 12` but there is a separate `DarkvisionOverride: 24` in the outer subrace object. The lower value may win depending on how CharacterResolver merges these fields. | `Data/Races/exotic_races.json` (duergar → superior_darkvision) |
| RC-08 | **MINOR** | **Halfling Brave** (immunity to Frightened condition) is noted on the BG3 wiki as a core halfling trait. Verify that the `brave` tag is present in halfling feature data and that `ConditionEffects.cs` suppresses Frightened application when this tag is present. | `Data/Races/core_races.json`; `Combat/Statuses/ConditionEffects.cs` |

---

## 5. Cross-Cutting Issues

| ID | Severity | Description | Touches |
|---|---|---|---|
| CC-01 | **CRITICAL** | `spellSchool` field is absent or `"MISSING"` on **all 225 spell entries**. School matters for: EK/AT school restrictions, Wizard Arcane Recovery (recovers slots from one spell of matching school), Evocation Sculpt Spells, etc. All spell-school-dependent mechanics are silently broken. | All `Data/Actions/bg3_spells_*.json`; `bg3_mechanics_actions.json` |
| CC-02 | **MAJOR** | No global long-rest / short-rest recovery test coverage. Races (Duergar Magic, Drow Magic, Githyanki Psionics), classes (Warlock Pact Magic, Second Wind, Bardic Inspiration, Wild Shape), and backgrounds all gate resources on rest cadence. The autobattle runner does not simulate rests between encounters by default. | `Combat/Services/ResourceManager.cs` |
| CC-03 | **MAJOR** | Concentration save on damage (Con Save vs DC 10 or ½ damage) is absent from `RulesEngine.cs`. Every concentration spell is effectively unbreakable in the current engine once applied. | `Combat/Rules/RulesEngine.cs` |
| CC-04 | **MINOR** | Skill proficiency strings use inconsistent key formats across systems (Backgrounds: `SleightOfHand`, Race features: mix of formats). A normalization layer or enum would prevent silent mismatches. | `Data/Backgrounds/BackgroundData.cs`; `Data/Races/*.json` |

---

## 6. Priority Matrix

| # | ID | Severity | One-line description |
|---|---|---|---|
| 1 | CL-01 + CL-02 + CL-03 | CRITICAL | EK and AT contribute 0 caster levels to multiclass ESL — fix subclass JSON + CharacterResolver subclass lookup |
| 2 | SP-01 / CC-01 | CRITICAL | All spells missing `spellSchool` — fill correct school tags on all 225 entries |
| 3 | RC-01 | CRITICAL | Duergar Magic (Enlarge L3 + Invisibility L5) entirely missing from duergar subrace |
| 4 | BG-01 | CRITICAL | Haunted One background entirely missing |
| 5 | CC-03 / SP-04 | MAJOR | Concentration save on damage not implemented in RulesEngine |
| 6 | RC-04 | MAJOR | Githyanki Psionics (Mage Hand, Enhanced Leap, Misty Step) not in feature data |
| 7 | RC-02 | MAJOR | Human Versatility (carry capacity +25% + skill choice) missing |
| 8 | RC-03 | MAJOR | Githyanki Astral Knowledge has no mechanical enforcement |
| 9 | SP-03 | MAJOR | Ritual casting not implemented |
| 10 | CL-04 | MAJOR | 4 non-BG3 subclasses in data (arcane_archer, drunken_master, swarmkeeper, glamour) |
| 11 | CL-05 / SP-05 | MAJOR | EK/AT school restrictions not enforced at spell selection |
| 12 | SP-06 | MAJOR | Several BG3 level 4–6 spells not yet defined (find_familiar, polymorph, wall_of_fire, chain_lightning, etc.) |
| 13 | RC-05 | MAJOR | Forest Gnome Speak with Animals frequency needs verification |
| 14 | BG-02 | MAJOR | No Inspiration condition tracking tied to background goals |
| 15 | RC-06 | MINOR | Rock Gnome Artificer's Lore expertise not verified in RulesEngine |
| 16 | RC-07 | MINOR | Duergar DarkvisionRange field inconsistency (12 vs override 24) |
| 17 | RC-08 | MINOR | Halfling Brave tag / Frightened immunity not verified in ConditionEffects |
| 18 | BG-03 | MINOR | Skill proficiency string casing may mismatch skill lookup keys |
| 19 | SP-07 | MINOR | Divination school spells absent or untagged |
| 20 | SP-08 | MINOR | Several BG3 cantrip options not available (mending, light, friends, thaumaturgy) |

---

## Implementation Notes for Coders

### Fix CL-01 / CL-02 / CL-03 — EK and AT multiclass spell slots

1. **In `Data/Classes/martial_classes.json`**, add `"SpellcasterModifier": 0.3333` to the `eldritch_knight` subclass object and `"SpellcasterModifier": 0.3333` to the `arcane_trickster` subclass object.

2. **In `Data/CharacterModel/CharacterResolver.cs` `MergeMulticlassSpellSlots` (~line 615)**, after the base class lookup, additionally check if the character has a subclass for this class and that subclass has a non-zero `SpellcasterModifier`:

```csharp
var classDef = _registry.GetClass(classId);
double classModifier = classDef?.SpellcasterModifier ?? 0;

// Third-casters: modifier lives on the subclass
if (classModifier <= 0 && sheet.SubclassId != null)
{
    var subclassDef = _registry.GetSubclass(classId, sheet.SubclassId);
    classModifier = subclassDef?.SpellcasterModifier ?? 0;
}

if (classModifier <= 0)
    continue;
rawCasterLevel += levels * classModifier;
```

### Fix RC-01 — Duergar Magic

Add to `duergar` subrace in `Data/Races/exotic_races.json`:
```json
"GrantedAbilities": [
  { "AbilityId": "enlarge_reduce", "GrantedAtLevel": 3 },
  { "AbilityId": "invisibility",   "GrantedAtLevel": 5 }
]
```
Verify both spells exist in the action registry (check `bg3_mechanics_actions.json`).

### Fix BG-01 — Haunted One Background

In `Data/Backgrounds/BackgroundData.cs`:
```csharp
new("haunted_one", "Haunted One", new[] { "Medicine", "Intimidation" }),
```

### Fix SP-01 / CC-01 — spellSchool fields

Patch each spell entry in all 6 action JSON files to include the correct school string. Reference: https://bg3.wiki/wiki/Spells (filter by school). For `bg3_mechanics_actions.json`, add `"spellSchool"` beside each spell entry's existing fields. This can be scripted using the BG3 spell name—school mapping from `BG3_Data/Spells/Spell_Shout.txt`, `Spell_Target.txt`, etc. (the `SpellSchool` field is in those raw data files).

### Fix RC-04 — Githyanki Psionics

Add to githyanki Features in `Data/Races/exotic_races.json`:
```json
{
  "Id": "githyanki_psionics",
  "GrantedAbilities": [
    { "AbilityId": "mage_hand",      "GrantedAtLevel": 1 },
    { "AbilityId": "enhance_leap",   "GrantedAtLevel": 3 },
    { "AbilityId": "misty_step",     "GrantedAtLevel": 5 }
  ]
}
```

### Fix RC-02 — Human Versatility

Add to human Features in `Data/Races/core_races.json`:
```json
{
  "Id": "human_versatility",
  "Tags": ["skill_choice_1"],
  "CarryingCapacityModifier": 1.25
}
```
Then handle `CarryingCapacityModifier` in `CharacterResolver.ApplyFeature` and `skill_choice_1` tag in CharacterBuilder skill selection UI.

---

## Test Hints

- **CL-01/02**: Create a Fighter 5 / Wizard 1 character with EK subclass. After `CharacterResolver.Resolve()`, assert `spell_slot_1 > 0` from EK contribution (Fighter 5 at modifier 0.333 = 1.665 → rounded to 1st-level slot). Seed: any scenario where multiclass EK is in the roster.
- **RC-01**: Create a Duergar character at level 3+. Assert `enlarge_reduce` is in `AllAbilities`. Run autobattle with `--seed 42` duergar hero and verify Enlarge action appears in action budget.
- **SP-04**: Run any fight where a caster with concentration spell takes damage. Assert the concentration status is sometimes stripped (not always maintained at 100%).
- **CC-01**: After populating spellSchool fields, write a unit test asserting no entry in ActionRegistry with `spellLevel >= 0` has a null/empty `SpellSchool`.
