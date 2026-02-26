# BG3 Combat Parity Audit — February 2026

> **Date:** 2026-02-26  
> **Method:** Full codebase audit + 3 parallel researcher passes + adversarial reviewer  
> **Scope:** All combat systems, spell data, common actions, AOE mechanics, damage pipeline  
> **Purpose:** Honest assessment of where we stand and cataloguing every known issue including small gameplay bugs

---

## Executive Summary

The combat engine architecture is production-quality (~96%). The state machine, action economy, damage pipeline, attack/save resolution, turn queue, death saves, concentration, surfaces, persistence, and AI are deeply implemented. **However, the gap between "systems exist" and "systems work correctly for every spell/action" is wider than the headline number suggests.** This audit surfaces 140+ individual issues ranging from architectural gaps to single-spell data bugs.

### Honest Parity Assessment

| Layer | Parity | Trend | Notes |
|---|---|---|---|
| **Engine Architecture** | ~96% | = | State machine, pipelines, services, AI, persistence — solid |
| **Data Definitions** | ~95% | = | Classes, races, feats, weapons, armor at 100% |
| **Spell Content** | ~88% | ↓ | 201/205 defined, but **14 AOE spells have broken targeting** (singleUnit instead of circle/cone) |
| **Status Content** | ~85% | = | 267 statuses, 261 with mechanics |
| **Passive Mechanics** | ~88% | ↓ | BoostConditions completely skipped (RV-M11); advantage tags dead code (RV-M15) |
| **Class Feature Wiring** | ~55% | = | Metamagic, Wild Shape, Warlock invocations unwired |
| **Reaction System** | ~70% | = | 13 reactions, ~6 unwired |
| **Common Actions** | ~80% | ↓ | Shove completely broken (contested check wrong + AI dispatch dead) |
| **AOE / Targeting** | ~75% | NEW | 14 spells with wrong targetType, 7 with wrong radius |
| **Overall Functional Combat** | **~80%** | ↓ | Was ~84%, reduced due to newly discovered Shove, AOE, and mechanics bugs |

---

## Part 1: Newly Discovered Issues (This Audit)

### Category A: Shove Action — Broken

| ID | Sev | Issue | File |
|---|---|---|---|
| SH-1 | 🔴 CRITICAL | **AI Shove dispatch is dead.** `ExecuteAIDecisionAction()` switch has no `AIActionType.Shove` case — falls to `default: return false`. All AI Shove candidates silently fail. | [ActionExecutionService.cs](Combat/Services/ActionExecutionService.cs) |
| SH-2 | 🔴 CRITICAL | **Shove uses DC-based save instead of contested Athletics check.** BG3: attacker STR(Athletics) vs target STR(Athletics) or DEX(Acrobatics) — both sides roll. Our code: static DC + target STR save. Fundamentally wrong mechanic. | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) |
| SH-3 | 🟠 MAJOR | **AI gates Shove on `HasAction` but Shove costs Bonus Action.** JSON correctly has `"usesBonusAction": true` but AI checks `HasAction`. AI misses bonus-action Shove opportunities and overpays when executing. | [AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs) |
| SH-4 | 🟠 MAJOR | **No creature size restriction.** A Halfling can Shove a Gargantuan dragon. BG3: can only Shove creatures at most 1 size larger. | Multiple files |
| SH-5 | 🟠 MAJOR | **AI only generates Shove near ledges — misses Shove Prone entirely.** `GenerateShoveCandidates()` only fires when `nearLedge \|\| potentialFallDamage > 0`. On flat terrain, zero Shove candidates. | [AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs) |
| SH-6 | 🟠 MAJOR | **AI scorer ignores success probability.** No estimation of attacker Athletics vs defender Athletics/Acrobatics. STR 10 goblin shoving STR 20 barbarian gets no penalty. | [AIScorer.cs](Combat/AI/AIScorer.cs) |
| SH-7 | 🟠 MAJOR | **AI scorer penalizes Shove as full Action cost.** `ShoveBaseCost = 1f` with comment "uses action" — but Shove costs Bonus Action. | [AIWeights.cs](Combat/AI/AIWeights.cs) |
| SH-8 | 🟡 MINOR | Auto-variant selection always picks first variant (Push over Prone). Fallback path always selects `Variants[0]`. | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) |

---

### Category B: AOE / Targeting — 14 Broken Spells

| ID | Sev | Issue | File |
|---|---|---|---|
| AOE-1 | 🔴 CRITICAL | **14 AOE spells have `targetType: "singleUnit"` — AOE completely broken.** `ResolveAreaTargets()` is never called because the spell takes the single-target path. These spells hit 1 creature instead of an area. Affected: `entangle`, `gust_of_wind`, `calm_emotions`, `fear`, `stinking_cloud`, `conjure_barrage`, `sleet_storm`, `confusion`, `evards_black_tentacles`, `circle_of_death`, `spirit_guardians_radiant`, `glyph_of_warding`, `wall_of_ice`, `acid_splash` | [bg3_spells_phase3.json](Data/Actions/bg3_spells_phase3.json) |
| AOE-2 | 🔴 CRITICAL | **`spirit_guardians` has `areaRadius: 0`.** The signature Cleric spell has no radius at all — completely non-functional as an aura. Should be 4.5m (15ft). | [bg3_mechanics_actions.json](Data/Actions/bg3_mechanics_actions.json) |
| AOE-3 | 🟠 MAJOR | **`hypnotic_pattern` radius 9.0m — should be 6.0m** (20ft). Affects 2.25x the correct area. | [bg3_spells_expanded.json](Data/Actions/bg3_spells_expanded.json) |
| AOE-4 | 🟠 MAJOR | **`fog_cloud` radius 4.5m — should be 6.0m** (20ft). Covers 56% of correct area. | [bg3_spells_phase3.json](Data/Actions/bg3_spells_phase3.json) |
| AOE-5 | 🟠 MAJOR | **`spike_growth` radius 3.0m — should be 6.0m** (20ft). Covers 25% of correct area. | [bg3_mechanics_actions.json](Data/Actions/bg3_mechanics_actions.json) |
| AOE-6 | 🟠 MAJOR | **`plant_growth` radius 30m — should be 9.0m** (30ft). 11x too large. | [bg3_spells_phase3.json](Data/Actions/bg3_spells_phase3.json) |
| AOE-7 | 🟠 MAJOR | **`sleet_storm` radius 12m — should be 6.0m** (20ft). 4x too large. | [bg3_spells_phase3.json](Data/Actions/bg3_spells_phase3.json) |
| AOE-8 | 🟡 MINOR | `thunderwave` radius 5.0m — should be 4.5m (15ft cube). Minor discrepancy. | [bg3_spells_expanded.json](Data/Actions/bg3_spells_expanded.json) |
| AOE-9 | 🟡 MINOR | Self-centered AOE preview bug: `distanceToCastPoint <= action.Range` fails for range=0 spells — preview circle never renders. | [CombatPresentationService.cs](Combat/Services/CombatPresentationService.cs) |
| AOE-10 | 🟡 MINOR | No Cube/Cylinder AOE shape — all modeled as Circle. Acceptable simplification but tooltip says "sphere" for cubes. | [TargetValidator.cs](Combat/Targeting/TargetValidator.cs) |

**Wrong spell ranges (separate from AOE radius):**

| ID | Sev | Spell | Current | Expected (BG3) | File |
|---|---|---|---|---|---|
| RNG-1 | 🟡 MINOR | `call_lightning` | 36m | 18m | bg3_spells_expanded.json |
| RNG-2 | 🟡 MINOR | `fog_cloud` | 18m | 36m | bg3_spells_phase3.json |
| RNG-3 | 🟡 MINOR | `spike_growth` | 18m | 45m | bg3_mechanics_actions.json |
| RNG-4 | 🟡 MINOR | `cloudkill` | 36m | 27m | bg3_spells_high_level.json |

---

### Category C: Combat Mechanics Bugs

| ID | Sev | Issue | File |
|---|---|---|---|
| CM-1 | 🔴 CRITICAL | **Massive damage instant death never triggers.** `TakeDamage()` caps its return at `currentHP + tempHP`, so the check `actualDamageDealt > MaxHP` can never pass. Should compare raw incoming damage before capping. BG3: if remaining damage after hitting 0 HP >= MaxHP → instant death. | [Combatant.cs](Combat/Entities/Combatant.cs) |
| CM-2 | 🟠 MAJOR | **Shield can't block Magic Missile.** Auto-hit spells bypass the attack reaction trigger path entirely. BG3: Shield explicitly blocks Magic Missile (all darts miss). | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) |
| CM-3 | 🟠 MAJOR | **Petrified resistance-to-all not wired into damage path.** The `Petrified` condition flag exists but `DamagePipeline` never queries it. Petrified creatures should resist all damage. | [DamagePipeline.cs](Combat/Rules/DamagePipeline.cs) |
| CM-4 | 🟠 MAJOR | **Allies block movement like enemies.** `GetBlockingCombatant()` has no faction check. BG3: you can move through ally spaces freely. | [MovementService.cs](Combat/Movement/MovementService.cs) |
| CM-5 | 🟠 MAJOR | **No fear source tracking.** Frightened disadvantage applies unconditionally regardless of LoS to fear source. BG3: Frightened only gives disadvantage while you can see the source. | [RulesEngine.cs](Combat/Rules/RulesEngine.cs) |
| CM-6 | 🟡 MINOR | No surprise round mechanic. | — |
| CM-7 | 🟡 MINOR | Prone incorrectly triggers concentration checks (losing HP isn't involved). | [ConcentrationSystem.cs](Combat/Statuses/ConcentrationSystem.cs) |
| CM-8 | 🟡 MINOR | Magic Missile darts trigger separate concentration checks instead of one aggregated check (RAW: simultaneous damage = single check with total). | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) |
| CM-9 | 🟡 MINOR | Non-weapon action resets the Extra Attack pool even when additional action charges remain. | [ActionExecutionService.cs](Combat/Services/ActionExecutionService.cs) |

---

## Part 2: Previously Known Issues (Confirmed Still Open)

### 🔴 CRITICAL (15 items from prior audit — confirmed open)

| # | ID | Subject | Description |
|---|---|---|---|
| 1 | R-1 | Resources | Short rest does NOT heal to 50% max HP (Explorer heals to 100%, others don't heal at all) |
| 2 | W-1 | Weapons | No enchantment bonus (+1/+2/+3) field on weapons |
| 3 | C-1 | Consumables | No Elixir category (until-long-rest duration) |
| 4 | C-2 | Consumables | Healing potions cannot be thrown at allies |
| 5 | EQ-1 | Equipment | Non-proficient armor: no save Disadvantage on STR/DEX |
| 6 | BG-1 | Backgrounds | Haunted One background entirely absent |
| 7 | RC-1 | Races | Duergar Magic entirely missing (Enlarge L3, Invisibility L5) |
| 8 | CL-1/2 | Classes | EK/AT contribute 0 caster levels to multiclass ESL |
| 9 | RV-C2 | Surfaces | Ice/Grease Prone bypasses DEX save (architectural gap) |
| 10 | RV-C3 | Classes | Warlock Eldritch Invocations completely non-functional |
| 11 | DT-1 | Terrain | Plant Growth surface missing (×4 movement cost) |
| 12 | DT-2 | Terrain | Spike Growth damage flat instead of per-distance-moved |
| 13 | S-1/S-2 | Surfaces | Ice/Grease do NOT apply Prone |
| 14 | OB-1/2/3/4 | Obscurity | Entire obscurity system non-functional (dead code, no tiers) |
| 15 | RV-C1 | Actions | ~~Action Surge costs Bonus Action~~ **FIXED** ✅ |

**Note:** RV-C1 (Action Surge) and RV-M7 (Frozen advantage) confirmed FIXED by deep audit.

### 🟠 MAJOR (from prior audit, still open — 60+ items)

#### Attacks & Damage
| ID | Issue |
|---|---|
| AT-1 | Mage Armor gives flat AC 13, missing DEX modifier |
| D-1 | Same-type resistance/vulnerability stacks multiplicatively instead of capping |
| RV-M1 | D-1 is worse than reported — cross-subsystem double-apply (Wet + racial resistance) |
| RV-M3 | FightingStyle_Archery +2 never applied to attack rolls |

#### Saving Throws
| ID | Issue |
|---|---|
| ST-1 | Save DC tooltip missing spellcasting ability modifier (UI-only) |
| ST-2 | Death saves bypass modifier stack — Bless/Lucky don't apply |

#### Movement
| ID | Issue |
|---|---|
| MS-1 | Unit ambiguity: race speeds need audit (ft vs m) |
| MS-2 | Race-specific speeds not verified |

#### Resources
| ID | Issue |
|---|---|
| R-2 | Missing resource types (BladeSongPower, StarMap, etc.) |
| R-3 | Bardic Inspiration die not level-scaled (d6→d8→d10) |
| RV-M2 | Explorer mode heals to 100% instead of 50% |

#### Healing
| ID | Issue |
|---|---|
| H-2 | Bleeding NOT removed on heal |
| H-3 | Healing potions don't remove Burning |
| H-6 | Limited healing spell roster |

#### Weapons
| ID | Issue |
|---|---|
| W-2 | No Dippable weapon property |
| W-3 | TWF offhand damage penalty unclear |
| W-4 | Loading (crossbow) enforcement unclear |
| W-5 | Weapon Action proficiency gate not validated |

#### Consumables
| ID | Issue |
|---|---|
| C-4 | No Coatings system |
| C-5 | No Arrow-consumables |
| C-6 | Very few scrolls (2 vs dozens) |
| C-7 | No Camp Supplies / Long Rest resource management |

#### Equipment
| ID | Issue |
|---|---|
| EQ-2 | Non-proficient armor: no ability check Disadvantage |
| EQ-3 | Shield passive AC across weapon sets unclear |
| EQ-4 | Unarmored Defense not implemented (Barb 10+DEX+CON, Monk 10+DEX+WIS) |
| RV-M5 | Barbarian/Monk Unarmored Defense passive ID mismatch |

#### Conditions
| ID | Issue |
|---|---|
| CO-1 | Charmed: no targeting validation vs charmer |
| CO-2 | Cursed condition absent from ConditionType |
| CO-3 | Diseased condition absent |
| CO-4 | Polymorphed not in ConditionType |

#### Feats
| ID | Issue |
|---|---|
| F-5 | Martial Adept: no maneuver selection |
| F-8 | Ritual Caster: no spell selection/grant |
| F-9 | Weapon Master: proficiencies not granted |

#### Passives
| ID | Issue |
|---|---|
| RV-M11 | BoostConditions evaluation completely skipped (FightingStyle_Defense +1 AC applies even unarmored) |
| RV-M15 | All racial `advantage_vs_*` saving throw tags are dead code (Halfling Brave, Fey Ancestry, Dwarven Resilience) |
| RV-M6 | Reliable Talent is dead code — `GetMinimumRollResult()` never called |

#### Spells
| ID | Issue |
|---|---|
| SP-2 | No ritual casting mechanic |
| SP-4 | EK/AT school restrictions not enforced at spell learning |

#### Classes
| ID | Issue |
|---|---|
| CL-3 | `MergeMulticlassSpellSlots` ignores subclass SpellcasterModifier |
| CL-4 | 12 non-BG3 subclasses in data (arcane_archer, drunken_master, hexblade, etc.) |
| CL-5 | Wild Shape beast options not defined; Rage per-subclass not differentiated |
| RV-M4 | Aura of Protection: wrong value (hardcoded 3 instead of CHA mod) + no ally propagation |
| RV-M8 | TemporaryHP boost type parsed but never applied |
| RV-M10 | StatsFunctorContext mapping incomplete (missing OnKill, OnShortRest, etc.) |
| RV-M12 | Cantrip damage scaling threshold wrong (< 10 instead of < 11 — L10 deals 3 dice instead of 2) |
| RV-M13 | Paladin `UsesPreparedSpells` missing |
| RV-M14 | 12 non-BG3 subclasses (expanded from 4 originally reported) |

#### Surfaces & Terrain
| ID | Issue |
|---|---|
| DT-3 | Missing surfaces: Mud, Black Tentacles, Vines, Sewage, Lava |
| DT-4 | Jump cost not affected by difficult terrain |
| DT-5 | Movement-expending weapon actions not blocked in DT |
| S-3 | Fire surface damage flat 5 instead of 1d4 |
| S-4 | Acid surface doesn't reduce AC |
| S-5 | Dip weapon surface mechanic fully wired but Dippable property not on WeaponDefinition |
| S-6 | Steam missing Wet status |
| S-7 | Oil + Fire interaction only fires on new surface creation |
| RV-M9 | Fire into Oil creates two overlapping fire surfaces (double damage) |

#### Clouds & Obscurity
| ID | Issue |
|---|---|
| CL-4 | No cloud vs surface architecture |
| CL-5 | Cloudkill damage flat 5 instead of 5d8 |
| CL-6/7 | Steam/Electrified Steam missing Wet |
| CL-8 | No Gust of Wind cloud removal |
| CL-9/10 | Haste Spores / Pacifying Spores missing |
| OB-5 | No Darkvision mechanic |
| OB-6/7 | Hide + Stealth vs Passive Perception absent from obscurity zones |

#### Backgrounds & Races
| ID | Issue |
|---|---|
| BG-2 | No Inspiration tracking |
| RC-2 | Human Versatility missing |
| RC-3 | Githyanki Astral Knowledge not enforced |
| RC-5 | Forest Gnome Speak with Animals frequency unverified |

---

## Part 3: Full Issue Count & Severity Matrix

### By Severity

| Severity | Prior Audit | New This Audit | Total |
|---|---|---|---|
| 🔴 CRITICAL | 14 (1 fixed) | 5 | **18** |
| 🟠 MAJOR | 60+ | 12 | **72+** |
| 🟡 MINOR | 35+ | 12 | **47+** |
| **Total** | **109+** | **29** | **137+** |

### By System

| System | 🔴 | 🟠 | 🟡 | Total |
|---|---|---|---|---|
| **Shove Action** | 2 | 5 | 1 | **8** |
| **AOE / Targeting** | 2 | 5 | 6 | **13** |
| **Combat Mechanics** | 1 | 3 | 4 | **8** |
| **Obscurity / Clouds** | 4 | 10+ | 2 | **16+** |
| **Surfaces / Terrain** | 3 | 8+ | 1 | **12+** |
| **Weapons / Equipment** | 2 | 8+ | 2 | **12+** |
| **Spells** | 0 | 3 | 2 | **5** |
| **Classes / Features** | 3 | 10+ | 1 | **14+** |
| **Passives / Boosts** | 0 | 3 | 3 | **6** |
| **Consumables / Items** | 2 | 4+ | 2 | **8+** |
| **Resources / Rest** | 1 | 3 | 1 | **5** |
| **Conditions** | 0 | 4 | 4 | **8** |
| **Damage Pipeline** | 0 | 2 | 1 | **3** |
| **Saving Throws** | 0 | 2 | 1 | **3** |
| **Races / Backgrounds** | 2 | 4 | 2 | **8** |
| **Other (UI, Feats)** | 0 | 3+ | 4+ | **7+** |

---

## Part 4: Revised Parity Assessment

### What's Working Well
- **Combat loop, initiative, turn queue** — solid, BG3-accurate
- **Death saves** — nat-20 revive, nat-1 = 2 failures, melee auto-crit on Downed — all correct
- **Concentration** — fully implemented with War Caster support, CON save on damage
- **Attack rolls** — formula correct, finesse, crit threshold, proficiency all BG3-accurate
- **201/205 spells defined** with spell level + school metadata on all entries
- **267 statuses** with 261 having mechanical effects
- **12/12 classes, all races, all feats** defined with level tables
- **13 reactions** wired and functional
- **Common actions** (Dip, Hide, Help, Dodge, Throw) fully functional with AI scoring
- **AI system** — context-aware scoring, multi-phase decision pipeline
- **Effect pipeline** — 43 real effect handlers, 0 NoOp stubs
- **Condition evaluator** — 147 case labels covering ~98 BG3 functions
- **15 subclasses** with always-prepared spell lists
- **Item use in combat** — 15 consumables with AI scoring

### What's Broken or Missing (Priority Order)

#### Tier 1: Gameplay-Breaking (players will notice immediately)
1. **Shove is non-functional for AI and uses wrong mechanic for players** (SH-1, SH-2)
2. **14 AOE spells hit 1 target instead of an area** (AOE-1) — Entangle, Fear, Confusion, Sleet Storm, etc.
3. **Spirit Guardians has no radius** (AOE-2) — Cleric signature spell broken
4. **Massive damage instant death never triggers** (CM-1)
5. **Entire obscurity system is dead code** (OB-1/2/3/4) — Fog/Darkness do nothing
6. **Ice/Grease don't cause Prone** (S-1/S-2) — signature surface effects missing

#### Tier 2: Significant Mechanical Inaccuracies
7. **No weapon enchantment bonuses** (W-1) — +1/+2/+3 weapons don't add to rolls
8. **Healing potions can't be thrown at allies** (C-2) — BG3 signature mechanic
9. **Shield doesn't block Magic Missile** (CM-2) — classic D&D interaction
10. **BoostConditions skipped** (RV-M11) — conditional passives always active
11. **Racial save advantage tags dead code** (RV-M15) — Halfling Brave, Fey Ancestry, Dwarven Resilience all non-functional
12. **Archery fighting style +2 never applied** (RV-M3) — every Fighter/Ranger with Archery is missing +2 to ranged attacks
13. **Aura of Protection wrong value + no allies** (RV-M4) — Paladin signature ability broken
14. **Warlock Invocations non-functional** (RV-C3) — Agonizing Blast, Repelling Blast, Devil's Sight all dead
15. **Allies block movement like enemies** (CM-4) — major navigation bug

#### Tier 3: Content Gaps
16. Class features ~55% wired (metamagic, Wild Shape, many subclass features unwired)
17. No Elixir category (C-1)
18. No ritual casting (SP-2)
19. EK/AT caster levels broken for multiclass (CL-1/2)
20. 7+ AOE spells with wrong radius values (AOE-3 through AOE-7)

---

## Part 5: Parity Score Breakdown

| Category | Weight | Score | Weighted |
|---|---|---|---|
| Engine Architecture | 15% | 96% | 14.4 |
| Spell Content (definitions) | 10% | 95% | 9.5 |
| Spell Mechanics (correct targeting/AOE/radius) | 10% | 75% | 7.5 |
| Status Content | 8% | 85% | 6.8 |
| Common Actions | 5% | 70% | 3.5 |
| Passive Mechanics | 8% | 82% | 6.6 |
| Class Feature Wiring | 10% | 55% | 5.5 |
| Reaction System | 5% | 70% | 3.5 |
| Obscurity / Clouds / Surfaces | 8% | 40% | 3.2 |
| Equipment / Weapons | 6% | 70% | 4.2 |
| Resources / Rest / Consumables | 5% | 55% | 2.8 |
| Conditions | 3% | 80% | 2.4 |
| Damage Pipeline | 4% | 85% | 3.4 |
| Races / Backgrounds | 3% | 85% | 2.6 |
| **Overall** | **100%** | | **~76%** |

**Revised overall parity: ~76%** (down from ~84% in prior assessment due to newly discovered issues in Shove, AOE targeting, combat mechanics, and passive system dead code).

---

## Part 6: What Changed Since Last Audit

### Issues Confirmed FIXED
| ID | Description |
|---|---|
| RV-C1 | Action Surge no longer costs a Bonus Action ✅ |
| RV-M7 | Frozen now correctly has `GrantsAdvantageToAttackers` ✅ |

### Issues Downgraded (False Positives)
From prior audit — already removed in reviewer addendum, still accurate:
- SP-1/CC-1: spellSchool field exists as `"school"` — all 161 entries populated
- SP-3/CC-2: Concentration break on damage fully implemented
- P-1: StatsFunctors ARE executed via PassiveFunctorProviderFactory
- H-5: TemporaryHP system exists (AddTemporaryHP, DamagePipeline absorbs)
- MS-3: Prone stand-up cost not inflated by Dash
- CO-5: Sleeping wake-on-damage IS implemented
- RC-4: Githyanki Psionics present
- 6 feat false positives (Athlete, Heavily Armoured, Lightly Armoured, Magic Initiate, Med Armour Master, Moderately Armoured)

### New Issues Found This Audit
- **8 Shove bugs** (2 CRITICAL, 5 MAJOR, 1 MINOR)
- **13 AOE/targeting bugs** (2 CRITICAL, 5 MAJOR, 6 MINOR)
- **8 Combat mechanics bugs** (1 CRITICAL, 3 MAJOR, 4 MINOR)

---

## Part 7: Recommended Fix Priority

### Sprint 1: Gameplay-Breaking (Est. 2-3 days)
1. Fix 14 AOE spells `targetType` → correct shape (data fix, ~30 min)
2. Fix Spirit Guardians `areaRadius: 0` → `4.5` (data fix, ~5 min)
3. Fix 7 wrong AOE radii (data fix, ~15 min)
4. Add `AIActionType.Shove` case to `ExecuteAIDecisionAction()` dispatch
5. Fix Shove AI budget check (HasAction → HasBonusAction)
6. Fix massive damage instant death in `TakeDamage()`

### Sprint 2: Shove Overhaul (Est. 1-2 days)
7. Implement contested Athletics check for Shove (new resolution mode)
8. Add creature size restriction to Shove
9. Generate Shove Prone candidates in AI
10. Fix AI Shove scoring (success probability, bonus action cost)

### Sprint 3: Passive System Fixes (Est. 1-2 days)
11. Wire BoostConditions evaluation (RV-M11)
12. Wire racial `advantage_vs_*` tags into saving throw resolution (RV-M15)
13. Wire Archery fighting style +2 into ranged attack rolls (RV-M3)
14. Fix cantrip damage scaling threshold (RV-M12)

### Sprint 4: Surface/Cloud/Obscurity (Est. 3-5 days)
15. Ice/Grease Prone with DEX save architecture (WP-C02)
16. Obscurity tier system (WP-C01)
17. Cloud mechanics (Fog blinds, Darkness disadvantage)

### Sprint 5: Weapon + Equipment (Est. 1-2 days)
18. Enchantment bonus system (WP-C03)
19. Ally movement non-blocking (CM-4)
20. Shield blocks Magic Missile (CM-2)

---

*This audit was generated by 3 parallel researcher teams with findings cross-referenced against the existing gap analysis. All file references point to actual codebase locations verified at audit time.*
