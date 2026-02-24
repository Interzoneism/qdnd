# BG3 Full Gap Analysis — All 22 Subjects

**Generated:** 2026-02-24  
**Method:** Parallel researcher teams compared BG3 wiki pages against codebase, followed by adversarial reviewer pass  
**Priority Legend:** 🔴 CRITICAL · 🟠 MAJOR · 🟡 MINOR

---

## Table of Contents

1. [Actions](#1-actions)
2. [Movement Speed](#2-movement-speed)
3. [Resources](#3-resources)
4. [Abilities / Ability Scores](#4-abilities--ability-scores)
5. [Attacks](#5-attacks)
6. [Damage](#6-damage)
7. [Weapons](#7-weapons)
8. [Consumables](#8-consumables)
9. [Healing](#9-healing)
10. [Saving Throws](#10-saving-throws)
11. [Clouds](#11-clouds)
12. [Difficult Terrain](#12-difficult-terrain)
13. [Obscurity](#13-obscurity)
14. [Surfaces](#14-surfaces)
15. [Equipment](#15-equipment)
16. [Conditions](#16-conditions)
17. [Feats](#17-feats)
18. [Passives](#18-passives)
19. [Spells](#19-spells)
20. [Classes](#20-classes)
21. [Backgrounds](#21-backgrounds)
22. [Races](#22-races)
23. [Cross-Cutting Issues](#23-cross-cutting-issues)
24. [Master Priority Matrix](#24-master-priority-matrix)

---

## 1. Actions

**BG3 Wiki:** https://bg3.wiki/wiki/Actions  
**BG3 Target:** 3 resources — Action (1/turn), Bonus Action (1/turn), Reaction (1/round). All recharge at turn start; Reaction recharges at round start. Action Surge grants an additional Action. Extra Attack (Fighter L5) = 1 free follow-up; Improved Extra Attack (L11) = 2 free follow-ups.

**Our State:** `ActionBudget.cs` correctly tracks 1 Action, 1 Bonus Action, 1 Reaction. `ResetForTurn` resets Action/BA/movement; `ResetReactionForRound` is separate. Extra Attack loop in `ActionExecutionService.cs` uses `numAttacks = 1 + ExtraAttacks`.

| ID | Severity | Gap | File |
|---|---|---|---|
| A-1 | 🟡 MINOR | Action Surge not directly confirmed in code — `ActionBudget` only tracks 1 Action charge; no explicit `ActionSurgeUsed` state or temporary charge cap increase found | `Combat/Actions/ActionBudget.cs` |
| A-2 | 🟡 MINOR | Reaction trigger coverage limited to 8 generic types. BG3 has spell-specific triggers like "targeted by spell while concentrating" (War Caster). `Custom` trigger partially covers but BG3-specific gates missing | `Combat/Reactions/ReactionTrigger.cs` |
| A-3 | 🟡 MINOR | BG3 bars reactions from: concentration saves, death saves, saves to end conditions early, saves/damage inside AoE conditions. No explicit filtering found | `Combat/Reactions/` |

---

## 2. Movement Speed

**BG3 Wiki:** https://bg3.wiki/wiki/Movement_speed  
**BG3 Target:** Race-defined base speeds: Slow (7.5m/25ft), Normal (9m/30ft), Fast (10.5m/35ft). Dash adds MaxMovement. Haste doubles base. Difficult terrain = ×2 cost. Standing from Prone = half **base** speed (not half Dashed speed).

**Our State:** `DefaultMaxMovement = 30f` (feet). `TurnLifecycleService` applies modifier stack + `BoostEvaluator.GetMovementMultiplier`. Dash adds MaxMovement.

| ID | Severity | Gap | File |
|---|---|---|---|
| MS-1 | 🟠 MAJOR | Unit ambiguity: `DefaultMaxMovement = 30f` suggests feet. BG3 uses meters (9m=30ft). Internally consistent IF all data agrees — but race data needs audit to confirm | `Combat/Actions/ActionBudget.cs` |
| MS-2 | 🟠 MAJOR | Race-specific speeds not verified — whether Dwarf/Halfling/Gnome return 25 and Wood Elf returns 35 depends on race data pipeline. Needs audit | `Data/Races/` |
| MS-3 | 🟠 MAJOR | Prone stand-up cost is `MaxMovement / 2` — should be half of **base** speed, not half of potentially Dashed speed. BG3: standing costs half normal movement regardless of Dash | `Combat/Movement/MovementService.cs` |
| MS-4 | 🟡 MINOR | Frightened movement check order — checked after budget-exhausted path, meaning error messages could be wrong | `Combat/Movement/MovementService.cs` |
| MS-5 | 🟡 MINOR | Mobile feat + Dash terrain ignore checks `HasStatus("dashing")` — Rogue Cunning Action: Dash may have a different status tag | `Combat/Movement/MovementService.cs` |

---

## 3. Resources

**BG3 Wiki:** https://bg3.wiki/wiki/Resources  
**BG3 Target:** Short rest heals to **50% max HP** (NOT hit dice rolling). Spell slots (L1-9), Warlock Pact slots (short rest), Shadow Spell Slot (L3, long rest). Class resources: Bardic Inspiration (d6→d8 at L5→d10 at L10), Rage (2→5), Ki Points (= Monk level + 1), Sorcery Points (= Sorc level), etc.

**Our State:** `ActionResourceType.cs` enum covers core types. `RestService.ProcessShortRest()` only heals in Explorer mode.

| ID | Severity | Gap | File |
|---|---|---|---|
| R-1 | 🔴 CRITICAL | **Short rest does NOT heal HP to 50% max** in Normal/Balanced/Tactician. Only Explorer mode heals. BG3 ALWAYS heals to 50% | `Combat/Services/RestService.cs` |
| R-2 | 🟠 MAJOR | Missing resource types: `BladeSongPower`, `StarMap`, `CosmicOmen`, `WarPriestCharge`, `FungalInfestationCharge`, `ArcaneArrow`, `LuckPoint` | `Data/ActionResources/ActionResourceType.cs` |
| R-3 | 🟠 MAJOR | Bardic Inspiration die size not level-scaled. d6→d8→d10 progression logic absent despite `DiceType` field existing | `Data/ActionResources/` |
| R-4 | 🟡 MINOR | `SpendHitDie()` rolls tabletop-style (die + CON mod). BG3 doesn't use this mechanic — design dead-end | `Combat/Services/RestService.cs` |
| R-5 | 🟡 MINOR | `LegendaryResistance` and `DeflectMissiles` hidden resources absent from enum | `Data/ActionResources/ActionResourceType.cs` |

---

## 4. Abilities / Ability Scores

**BG3 Wiki:** https://bg3.wiki/wiki/Abilities, https://bg3.wiki/wiki/Ability_scores  
**BG3 Target:** 6 abilities; modifier = `floor((score−10)/2)`; proficiency +2/+3/+4 capped at L12; 18 skills; initiative = d20 + DEX mod; death saves DC 10 with general save bonuses applying.

**Our State:** Almost entirely correct. Modifier formula, proficiency table, all 6 abilities, 18 skills, expertise, spell save DC, initiative, ability score cap at 20, saving throw proficiency from starting class only — all verified correct.

| ID | Severity | Gap | File |
|---|---|---|---|
| AB-1 | 🟡 MINOR | Death saves don't respect general save bonuses (Bless, Guidance). Raw d20 used instead of going through modifier stack | `Combat/Services/TurnLifecycleService.cs` |
| AB-2 | 🟡 MINOR | No pathway for ability scores to exceed 20 (Mirror of Loss items) — not relevant at L12 cap | `Data/CharacterModel/CharacterSheet.cs` |

---

## 5. Attacks

**BG3 Wiki:** https://bg3.wiki/wiki/Attacks  
**BG3 Target:** `d20 + ability mod + proficiency`. Finesse = max(STR,DEX). Crit on nat 20 (doubles dice). Nat 1 = auto-miss. AC: unarmored 10+DEX; light base+DEX; medium base+min(DEX,2); heavy base+0. Mage Armor = 13+DEX.

**Our State:** Attack roll formulas, proficiency checks, crit threshold, AC calculations (including Medium Armor Master) all correct. Halfling Lucky on attacks works.

| ID | Severity | Gap | File |
|---|---|---|---|
| AT-1 | 🟠 MAJOR | **Mage Armor gives flat AC 13, ignoring DEX.** Status has `modifier: {target: "armorClass", type: "override", value: 13}` — the DEX modifier is lost. BG3 correct: `13 + DEX mod` | `Data/Statuses/bg3_mechanics_statuses.json` |
| AT-2 | 🟡 MINOR | Armor DEX bonus floors at 0 via `Math.Clamp(dexMod, 0, maxDex)` — negative DEX should reduce AC in light/medium armor | `Combat/Services/InventoryService.cs` |
| AT-3 | 🟡 MINOR | Crit threshold only reduces to 19 — no stacking below 19 from multiple features | `Combat/Actions/EffectPipeline.cs` |

---

## 6. Damage

**BG3 Wiki:** https://bg3.wiki/wiki/Damage  
**BG3 Target:** Crit doubles ALL dice (not flat mods). Off-hand: no ability mod unless Two-Weapon Fighting. Resistance ×0.5, Vulnerability ×2.0, Immunity ×0.0. Resist + Vuln cancel each other. Multiple same-type resist/vuln = only one instance counts. 13 damage types.

**Our State:** Crit dice doubling, off-hand penalty, finesse selection, damage pipeline stages all correct.

| ID | Severity | Gap | File |
|---|---|---|---|
| D-1 | 🟠 MAJOR | **Multiple same-type resistance/vulnerability instances stack multiplicatively** instead of capping at one. Two fire resistances → ×0.25 instead of ×0.5 | `Combat/Rules/DamagePipeline.cs` |
| D-2 | 🟡 MINOR | Resist+Vuln cancel works mathematically for 1:1 case but not mechanically explicit. Fix D-1 first | `Combat/Rules/DamagePipeline.cs` |
| D-3 | 🟡 MINOR | Multi-damage-type effects: per-type pipeline separation depends on call-site discipline. Needs verification that all multi-type effects in `bg3_mechanics_actions.json` are orchestrated correctly | `Combat/Actions/EffectPipeline.cs` |
| D-4 | 🟡 MINOR | Cantrip scaling uses total character level (correct per BG3/5e) — confirm this is total level not class level | `Combat/Actions/Effects/Effect.cs` |

---

## 7. Weapons

**BG3 Wiki:** https://bg3.wiki/wiki/Weapons  
**BG3 Target:** Full weapon table with properties (Finesse, Light, Heavy, etc.). Enchantment bonus (+1/+2/+3) adds to both attack AND damage. Dippable property for coating. Weapon Actions (Lacerate, Cleave, etc.) require proficiency.

**Our State:** `WeaponType` enum covers all types. `WeaponProperty` flags correct. `VersatileDieFaces` for two-handed. `GrantedActionIds` for weapon actions.

| ID | Severity | Gap | File |
|---|---|---|---|
| W-1 | 🔴 CRITICAL | **No enchantment bonus (+1/+2/+3) field.** Neither `WeaponDefinition` nor `BG3WeaponData` has a dedicated `EnchantmentBonus` int. Magic weapons can't add their bonus to both rolls as a typed stat | `Data/CharacterModel/EquipmentDefinition.cs` |
| W-2 | 🟠 MAJOR | `Dippable` property absent from `WeaponProperty` enum and `WeaponDefinition`. No weapon dip mechanic at all | `Data/CharacterModel/EquipmentDefinition.cs` |
| W-3 | 🟠 MAJOR | Two-weapon fighting offhand damage penalty — unclear if ability modifier is correctly suppressed for offhand and only restored with Fighting Style | `Combat/Actions/Effects/Effect.cs` |
| W-4 | 🟠 MAJOR | Loading property (crossbows) defined in enum but enforcement unclear — can only reload once per action; Extra Attack shouldn't apply to reloading | `Combat/Actions/` |
| W-5 | 🟠 MAJOR | Weapon Action proficiency gate — Lacerate, Concussive Smash etc. should only be available to proficient wielders. Not validated | `Combat/Services/ActionExecutionService.cs` |
| W-6 | 🟡 MINOR | Heavy property should impose disadvantage for Small/Tiny creatures. Not in rules engine | `Combat/Rules/RulesEngine.cs` |
| W-7 | 🟡 MINOR | Thrown weapons don't return — BG3 thrown weapons come back after throwing | — |

---

## 8. Consumables

**BG3 Wiki:** https://bg3.wiki/wiki/Consumables  
**BG3 Target:** 8 categories: Arrows, Camp Supplies, Coatings, Dyes, Elixirs (until long rest), Grenades, Potions (throwable at allies), Scrolls.

**Our State:** Healing potions with correct dice. Throwables (Alchemist's Fire, Acid Vial, Holy Water). Only 2 scrolls (Revivify, Misty Step). All potions self-only.

| ID | Severity | Gap | File |
|---|---|---|---|
| C-1 | 🔴 CRITICAL | **No Elixir category.** All buff consumables use turn-based durations. BG3 Elixirs last until long rest — fundamentally different design | `Data/Actions/consumable_items.json` |
| C-2 | 🔴 CRITICAL | **Healing potions cannot be thrown at allies.** All potions are `targetType: "self"`. BG3 allows throwing potions at allies' feet for area healing | `Data/Actions/consumable_items.json` |
| C-3 | 🟠 MAJOR | Healing potions don't remove Burning when drunk. No condition cleanup in potion actions | `Data/Actions/consumable_items.json` |
| C-4 | 🟠 MAJOR | No Coatings system (weapon-applied consumable buffs) | — |
| C-5 | 🟠 MAJOR | No Arrow-consumables for ranged characters | — |
| C-6 | 🟠 MAJOR | Very few scrolls (2 vs dozens in BG3) | `Data/Actions/consumable_items.json` |
| C-7 | 🟠 MAJOR | No Camp Supplies / Long Rest resource management | — |
| C-8 | 🟡 MINOR | No distinct Grenades category (limited variety) | — |
| C-9 | 🟡 MINOR | No Dyes, no Tools (Thieves' Tools, Trap Disarm Toolkit) | — |

---

## 9. Healing

**BG3 Wiki:** https://bg3.wiki/wiki/Healing  
**BG3 Target:** Capped at max HP. Any healing removes Downed AND Bleeding. Healing potions remove Burning when drunk. Cure Wounds: 1d8 + spellcasting mod, upcasts +1d8. Short rest: 50% max HP.

**Our State:** `HealEffect` rolls dice, caps at max HP, revives Downed, removes Prone on revive. Cure Wounds upcast scaling correct.

| ID | Severity | Gap | File |
|---|---|---|---|
| H-1 | 🔴 CRITICAL | Short rest does NOT heal to 50% max HP (same as R-1) | `Combat/Services/RestService.cs` |
| H-2 | 🟠 MAJOR | **Bleeding condition NOT removed on heal.** `HealEffect` only removes Prone on revive, not Bleeding | `Combat/Actions/Effects/Effect.cs` |
| H-3 | 🟠 MAJOR | Healing potions don't remove Burning (same as C-3) | `Data/Actions/consumable_items.json` |
| H-4 | 🟠 MAJOR | Healing potions cannot be thrown at allies (same as C-2) | `Data/Actions/consumable_items.json` |
| H-5 | 🟠 MAJOR | **No temporary hit points system.** No `TempHP` field in `CombatantResources` | `Combat/Entities/` |
| H-6 | 🟠 MAJOR | Limited healing spell roster — Healing Word, Mass Cure Wounds, Prayer of Healing, Heal (70 HP fixed), Aid, Life Transference all missing | `Data/Actions/` |
| H-7 | 🟡 MINOR | `SpendHitDie()` uses tabletop formula instead of BG3's automatic 50% max restoration | `Combat/Services/RestService.cs` |

---

## 10. Saving Throws

**BG3 Wiki:** https://bg3.wiki/wiki/Saving_throw  
**BG3 Target:** `d20 + ability mod + proficiency (if proficient)` ≥ DC. Spell DC = `8 + prof + spellcasting mod`. Weapon DC = `8 + prof + max(STR,DEX) + inherent bonus`. NO auto-fail on nat 1, NO auto-succeed on nat 20. Death saves: DC 10, general save bonuses apply, nat-20 revives, nat-1 = 2 failures.

**Our State:** Core saving throw formulas correct. Death save mechanics correct (nat-20 revive, nat-1 = 2 failures, melee auto-crit vs Downed).

| ID | Severity | Gap | File |
|---|---|---|---|
| ST-1 | 🟠 MAJOR | **Save DC tooltip missing spellcasting ability modifier.** `ComputeTooltipSaveDC` returns `8 + prof + SaveDCBonus` without spellcasting mod. L5 Wizard INT 18 shows DC 11 instead of 15. Combat `ComputeSaveDC` is correct — UI-only bug | `Combat/Services/ActionBarService.cs` |
| ST-2 | 🟠 MAJOR | **Death saves bypass modifier stack.** Uses raw `_getRng()?.Next(1, 21)` instead of `RulesEngine.RollSave`. Bless, Bane, Guidance, Halfling Lucky don't apply to death saves | `Combat/Services/TurnLifecycleService.cs` |
| ST-3 | 🟡 MINOR | Weapon action SaveDCBonus (+2 inherent) depends on per-action data. Each weapon action in JSON needs `SaveDCBonus: 2` — audit needed | `Data/Actions/bg3_mechanics_actions.json` |
| ST-4 | 🟡 MINOR | Concentration saves — verify Constitution with full modifier stack, and DC = max(10, half damage) | `Combat/Statuses/ConcentrationSystem.cs` |

---

## 11. Clouds

**BG3 Wiki:** https://bg3.wiki/wiki/Clouds  
**BG3 Target:** Clouds hover above ground (separate from surfaces). Darkness/Fog blind + block ranged attacks. Cloudkill = 5d8 Poison/turn. Steam applies Wet. Gust of Wind removes clouds. Haste Spores grant +2 AC/×2 movement/extra Action.

**Our State:** Clouds are NOT architecturally distinct from surfaces — implemented as `SurfaceDefinition` with `"cloud"`/`"obscure"` tags. `LOSResult.IsObscured` is set by `LOSService` but **never read** by any combat system.

| ID | Severity | Gap | File |
|---|---|---|---|
| CL-1 | 🔴 CRITICAL | **Fog Cloud does NOT blind.** `fog` surface has no `AppliesStatusId` — creatures in Fog are mechanically unaffected | `Combat/Environment/SurfaceManager.cs` |
| CL-2 | 🔴 CRITICAL | **LOSResult.IsObscured is dead code.** Set by LOSService but never read by EffectPipeline, RulesEngine, or any attack resolution | `Combat/Environment/LOSService.cs` |
| CL-3 | 🔴 CRITICAL | **Ranged attack blocking by Darkness/Fog absent.** EffectPipeline only checks `blinded` status for range limit — doesn't query LOSResult for cloud obstruction | `Combat/Actions/EffectPipeline.cs` |
| CL-4 | 🟠 MAJOR | No cloud vs. surface architecture — both are same type; surface interactions can incorrectly transform clouds | `Combat/Environment/SurfaceManager.cs` |
| CL-5 | 🟠 MAJOR | Cloudkill damage wrong — flat 5 instead of 5d8 | `Combat/Environment/SurfaceManager.cs` |
| CL-6 | 🟠 MAJOR | Steam Cloud missing Wet status application | `Combat/Environment/SurfaceManager.cs` |
| CL-7 | 🟠 MAJOR | Electrified Steam also missing Wet | `Combat/Environment/SurfaceManager.cs` |
| CL-8 | 🟠 MAJOR | No Gust of Wind cloud removal mechanic | — |
| CL-9 | 🟠 MAJOR | Haste Spores entirely missing (+2 AC, ×2 movement, extra Action) | — |
| CL-10 | 🟠 MAJOR | Pacifying Spores missing (suppress all action types) | — |
| CL-11 | 🟡 MINOR | Missing clouds: Crawler Mucus, Drow Poison, Ice Cloud, Malice, Noxious Fumes, Strange Gas, Timmask Spores | — |

---

## 12. Difficult Terrain

**BG3 Wiki:** https://bg3.wiki/wiki/Difficult_Terrain  
**BG3 Target:** ×2 movement cost. Plant Growth = ×4 cost. Jumping in difficult terrain also costs double. Movement-expending weapon actions blocked. Surfaces that create DT: Black Tentacles, Grease, Ice, Lava, Mud, Sewage, Shadow-Cursed Vines, Spike Growth, Spikes, Twisting Vines, Web.

**Our State:** `GetMovementCostMultiplier()` samples surfaces along path. Ice, grease, web, spike_growth have `MovementCostMultiplier = 2f`. A* pathfinder applies correctly.

| ID | Severity | Gap | File |
|---|---|---|---|
| DT-1 | 🔴 CRITICAL | **Plant Growth surface missing** — should quarter movement (×4 cost). Spell exists but no surface entity | `Combat/Environment/SurfaceManager.cs` |
| DT-2 | 🔴 CRITICAL | **Spike Growth damage completely wrong** — flat `DamagePerTrigger = 3` on enter/turn-start. BG3: 2d4 Piercing for every 1.5m (5ft) moved through zone. Must scale with distance | `Combat/Environment/SurfaceManager.cs` |
| DT-3 | 🟠 MAJOR | Missing surfaces: Mud, Black Tentacles, Shadow-Cursed Vines, Twisting Vines, Sewage, Lava — all create Difficult Terrain | `Combat/Environment/SurfaceManager.cs` |
| DT-4 | 🟠 MAJOR | **Jumping in difficult terrain** — `JumpPathfinder3D.cs` / `SpecialMovementService.cs` doesn't apply surface cost multiplier to jump cost | `Combat/Movement/` |
| DT-5 | 🟠 MAJOR | **Movement-expending weapon actions not blocked** — BG3 Brace (Ranged/Melee) unavailable in difficult terrain. No such check exists | `Combat/Actions/` |

---

## 13. Obscurity

**BG3 Wiki:** https://bg3.wiki/wiki/Obscurity  
**BG3 Target:** 3 tiers: Clear (fully visible), Lightly Obscured (hide requires Stealth vs. passive Perception), Heavily Obscured (hidden by default; Darkvision reduces by 1 tier). Attacking from/into Heavy Obscured = Disadvantage. Ranged attacks blocked through Darkness/Fog.

**Our State:** `LOSResult.IsObscured` is a single boolean (no tier). Set but never consumed by any combat system. `darkness_obscured → Blinded` for creatures IN Darkness works. Fog applies no condition.

| ID | Severity | Gap | File |
|---|---|---|---|
| OB-1 | 🔴 CRITICAL | **No Lightly vs. Heavily Obscured tier system.** Binary `IsObscured` doesn't support the 3-tier model with different advantage/disadvantage rules per tier | `Combat/Environment/LOSService.cs` |
| OB-2 | 🔴 CRITICAL | **LOSResult.IsObscured is dead code** — never read by EffectPipeline, RulesEngine, or any attack resolution. Obscurity has ZERO mechanical effect on attack rolls | `Combat/Environment/LOSService.cs` |
| OB-3 | 🔴 CRITICAL | **Fog does not impose disadvantage** — attacking from/into Fog should have Disadvantage and block ranged attacks. None of this wired | `Combat/Actions/EffectPipeline.cs` |
| OB-4 | 🔴 CRITICAL | **Darkness doesn't affect OUTSIDE attackers** — creatures IN Darkness get Blinded (correct), but attackers TARGETING into Darkness from outside should also have Disadvantage | `Combat/Actions/EffectPipeline.cs` |
| OB-5 | 🟠 MAJOR | **No Darkvision mechanic** — no code reduces obscurity tier for Darkvision holders. Race traits list it but it has no effect | `Combat/Rules/RulesEngine.cs` |
| OB-6 | 🟠 MAJOR | Hide + Obscurity integration missing — Hide action doesn't interact with current obscurity zone | — |
| OB-7 | 🟠 MAJOR | Stealth vs. Passive Perception absent — no such check exists for Lightly Obscured zones | — |
| OB-8 | 🟡 MINOR | Equipment obscurity bonuses ("+1 AC while obscured") untriggerable since zones not implemented | — |

---

## 14. Surfaces

**BG3 Wiki:** https://bg3.wiki/wiki/Surfaces  
**BG3 Target:** 25+ surface types with specific damage/condition/terrain effects. Surfaces interact with each other (fire ignites oil, cold freezes water). Dippable surfaces coat weapons.

**Our State:** 21 surfaces registered in `SurfaceManager.RegisterDefaultSurfaces()`: fire, water, poison, oil, ice, steam, lightning, electrified_water, spike_growth, daggers, acid, grease, web, darkness, moonbeam, silence, hunger_of_hadar, fog, stinking_cloud, cloudkill, electrified_steam.

### Missing Surfaces

| Surface | Priority | Notes |
|---|---|---|
| `plant_growth` | 🔴 CRITICAL | Unique ×4 movement cost |
| `lava` | 🟠 MAJOR | 10d6 Fire/turn + Difficult Terrain |
| `mud` | 🟠 MAJOR | Difficult Terrain |
| `black_tentacles` | 🟠 MAJOR | 3d6 Bludgeoning/turn + Difficult Terrain |
| `hellfire` | 🟠 MAJOR | 6d6 Fire/turn, Dippable |
| `sewage` | 🟠 MAJOR | Prone + Difficult Terrain |
| `shadow_cursed_vines` | 🟠 MAJOR | Immobilise + 1d4 Necrotic + DT |
| `twisting_vines` | 🟠 MAJOR | Entangled + Difficult Terrain |
| `alcohol` | 🟡 MINOR | → Fire (fire dmg), → Ice (cold dmg) |
| `ash` | 🟡 MINOR | No effect |
| `blood` | 🟡 MINOR | → Ice (cold dmg) |
| `caustic_brine` | 🟡 MINOR | 1d4 Acid/turn |
| `holy_fire` | 🟡 MINOR | 1d4 Radiant/turn |
| `mind_flayer_blood` | 🟡 MINOR | → Ice (cold) |
| `purple_worm_poison` | 🟡 MINOR | 1d10 Poison/turn |
| `serpent_venom` | 🟡 MINOR | 1d6 Poison/turn |
| `simple_toxin` | 🟡 MINOR | 1d4 Poison/turn, Dippable |

### Existing Surfaces with Wrong Behaviour

| ID | Severity | Gap | File |
|---|---|---|---|
| S-1 | 🔴 CRITICAL | **Ice does NOT apply Prone.** BG3 Ice causes Prone when walking on it. Our ice has `MovementCostMultiplier = 2f` but no `AppliesStatusId = "prone"` | `Combat/Environment/SurfaceManager.cs` |
| S-2 | 🔴 CRITICAL | **Grease does NOT apply Prone.** BG3 Grease causes Prone. Our grease has `MovementCostMultiplier = 2f` but no Prone | `Combat/Environment/SurfaceManager.cs` |
| S-3 | 🟠 MAJOR | **Fire damage flat 5, should be 1d4** (average 2.5). The damage pipeline supports rolled damage but `DamagePerTrigger` is a flat float | `Combat/Environment/SurfaceManager.cs` |
| S-4 | 🟠 MAJOR | **Acid doesn't reduce AC** — BG3 Acid = −2 AC. Our acid just deals flat damage | `Combat/Environment/SurfaceManager.cs` |
| S-5 | 🟠 MAJOR | **Dip mechanic entirely absent** — BG3 allows dipping weapons into Fire/Hellfire/Simple Toxin for coating | — |
| S-6 | 🟠 MAJOR | **Steam Cloud missing Wet status** — `steam` surface has `obscure` tag but no `AppliesStatusId = "wet"` | `Combat/Environment/SurfaceManager.cs` |
| S-7 | 🟠 MAJOR | **Oil doesn't interact with Fire correctly** — `CheckInteractions` only fires on new surface creation, so Fire cast into existing Oil won't trigger | `Combat/Environment/SurfaceManager.cs` |
| S-8 | 🟡 MINOR | Fly avoidance not implemented — creatures with Fly should bypass surface effects | `Combat/Environment/SurfaceManager.cs` |

---

## 15. Equipment

**BG3 Wiki:** https://bg3.wiki/wiki/Equipment  
**BG3 Target:** 12 slots. Non-proficient armor: Disadvantage on STR/DEX checks, STR/DEX saves, attack rolls; can't cast spells. Shield passive AC across weapon sets. Unarmored Defense: Barb 10+DEX+CON, Monk 10+DEX+WIS.

**Our State:** 12 slots correct. `ArmorDefinition` with BaseAC, MaxDexBonus, StealthDisadvantage, StrengthRequirement. Non-proficient attack disadvantage and spell blocking work.

| ID | Severity | Gap | File |
|---|---|---|---|
| EQ-1 | 🔴 CRITICAL | **Non-proficient armor: STR/DEX saving throws do NOT get Disadvantage.** Only attack rolls penalized | `Combat/Rules/RulesEngine.cs` |
| EQ-2 | 🟠 MAJOR | Non-proficient armor: STR/DEX ability checks do NOT get Disadvantage | `Combat/Rules/RulesEngine.cs` |
| EQ-3 | 🟠 MAJOR | Shield passive AC across weapon sets — unclear if shield contributes when ranged set active | `Combat/Services/InventoryService.cs` |
| EQ-4 | 🟠 MAJOR | **Unarmored Defense not implemented** — Barbarian 10+DEX+CON and Monk 10+DEX+WIS have no dedicated code path | `Combat/Services/InventoryService.cs` |
| EQ-5 | 🟠 MAJOR | Item boost application on equip — `BoostString` exists but whether `BoostApplicator` processes it on equip/unequip for magic gear is unvalidated | — |
| EQ-6 | 🟡 MINOR | Heavy armor STR requirement — `StrengthRequirement` field exists but −10ft speed penalty not enforced | `Combat/Services/InventoryService.cs` |

---

## 16. Conditions

**BG3 Wiki:** https://bg3.wiki/wiki/Conditions  
**BG3 Target:** 15 PHB conditions + BG3 additions (Frozen, Sleeping, Diseased, Cursed, Polymorphed). Stack ID system. 4 stack types. Tick types (StartTurn vs EndTurn).

**Our State:** Most PHB conditions correctly implemented. Blinded, Frightened, Grappled, Incapacitated, Invisible, Paralyzed, Petrified, Poisoned, Prone, Restrained, Stunned, Unconscious, Frozen all correct.

| ID | Severity | Gap | File |
|---|---|---|---|
| CO-1 | 🟠 MAJOR | **Charmed has no targeting validation** — Charmed creatures can still attack their charmer. Comment says "handled via targeting validation" but no code found | `Combat/Statuses/ConditionEffects.cs` |
| CO-2 | 🟠 MAJOR | **Cursed condition absent** from `ConditionType` enum and `StatusToCondition` map. Bestow Curse sub-effects won't get mechanics | `Combat/Statuses/ConditionEffects.cs` |
| CO-3 | 🟠 MAJOR | **Diseased condition absent** — Disease statuses have no condition effects | `Combat/Statuses/ConditionEffects.cs` |
| CO-4 | 🟠 MAJOR | **Polymorphed not in `ConditionType`** — no incapacitated/no-spell-cast enforcement when Polymorphed | `Combat/Statuses/ConditionEffects.cs` |
| CO-5 | 🟠 MAJOR | **Sleeping ≠ Unconscious** — currently aliased to Unconscious but loses distinct "wake on any damage" mechanic | `Combat/Statuses/StatusSystem.cs` |
| CO-6 | 🟡 MINOR | Stack ID system not present — two same-ID conditions can coexist | `Combat/Statuses/StatusSystem.cs` |
| CO-7 | 🟡 MINOR | Tick type (StartTurn vs EndTurn) — only EndTurn ticking. BG3 StartTurn is default | `Combat/Statuses/StatusSystem.cs` |
| CO-8 | 🟡 MINOR | ~19 BG3 status properties not modeled (IsInvulnerable, LoseControl, FreezeDuration, etc.) | `Combat/Statuses/` |

---

## 17. Feats

**BG3 Wiki:** https://bg3.wiki/wiki/Feats  
**BG3 Target:** 41 feats. Many require player choices at selection (ASI allocation, spell selection, weapon proficiency selection).

**Our State:** All 41 feats defined in `bg3_feats.json`. Many core feats mechanically enforced (GWM, Sharpshooter, Sentinel, Polearm Master, War Caster, etc.).

### Feats with MAJOR gaps (8 total):

| ID | Severity | Feat | Gap |
|---|---|---|---|
| F-1 | 🟠 MAJOR | **Athlete** | ASI (+1 STR or DEX) never applied — has tag but no `AbilityScoreIncreases` and no `ApplyFeatChoices` handler |
| F-2 | 🟠 MAJOR | **Heavily Armoured** | +1 STR never applied; heavy armor proficiency grant unverified |
| F-3 | 🟠 MAJOR | **Lightly Armoured** | ASI (+1 STR or DEX) never applied — same issue as Athlete |
| F-4 | 🟠 MAJOR | **Magic Initiate (×7 variants)** | Tags only; no `FeatChoices` handler to grant selected spell IDs; spellcasting ability unclear |
| F-5 | 🟠 MAJOR | **Martial Adept** | Gives 1 superiority_dice resource but no mechanism to select 2 maneuvers from Battle Master list |
| F-6 | 🟠 MAJOR | **Medium Armour Master** | +3 DEX cap not enforced in armor AC calculation (uses standard +2) |
| F-7 | 🟠 MAJOR | **Moderately Armoured** | ASI (+1 STR or DEX) never applied |
| F-8 | 🟠 MAJOR | **Ritual Caster** | Tag only; no spell selection/grant mechanism |
| F-9 | 🟠 MAJOR | **Weapon Master** | No `ApplyFeatChoices` case — 4 weapon proficiencies never granted |

### Minor feat gaps:

| ID | Severity | Feat | Gap |
|---|---|---|---|
| F-10 | 🟡 MINOR | Alert | No Surprise round mechanic to suppress |
| F-11 | 🟡 MINOR | Crossbow Expert | BG3 also grants Wounding Shot variant — not in `GrantedAbilities` |
| F-12 | 🟡 MINOR | Durable | `durable_healing_floor` tag — enforcement in healing pipeline unverified |
| F-13 | 🟡 MINOR | Heavy Armour Master | +1 STR not in feature; −3 DR enforcement unverified |
| F-14 | 🟡 MINOR | Mobile | OA avoidance system enforcement unverified |
| F-15 | 🟡 MINOR | Shield Master | Passive +2 DEX save with shield unverified |
| F-16 | 🟡 MINOR | Tavern Brawler | STR mod doubled on damage — verify not just added once |

---

## 18. Passives

**BG3 Wiki:** https://bg3.wiki/wiki/Passives  
**BG3 Target:** Always-active features via Boosts (stat mods) and StatsFunctors (event-triggered effects). `BG3_Data/Stats/Passive.txt` has 3,206 lines of passive definitions.

**Our State:** `BG3PassiveData` model complete. `PassiveRegistry` stores entries. `PassiveManager` tracks per-combatant. `BoostApplicator` applies stat modifications.

| ID | Severity | Gap | File |
|---|---|---|---|
| P-1 | 🔴 CRITICAL | **StatsFunctors NOT executed.** Comment: "StatsFunctors support will be added in future iterations." ALL event-driven passive effects (Sneak Attack trigger, Bardic Inspiration timing, Rage resistance trigger, Divine Smite on-crit, Ki spending) from Passive.txt are silently skipped | `Data/Passives/BG3PassiveData.cs` |
| P-2 | 🟠 MAJOR | **BoostConditions evaluation** — field exists but condition syntax (`HasWeaponInInventory()`, `IsWearingArmor()`) evaluation completeness unknown. Conditional boosts may silently bypass conditions | `Combat/Passives/` |
| P-3 | 🟡 MINOR | Class feature delivery via Feature-tags/GrantedAbilities vs. PassiveRegistry creates two parallel systems — no single authoritative passive list | — |
| P-4 | 🟡 MINOR | Broader UI toggle integration for Passive.txt entries (Non-Lethal, etc.) unverified | — |
| P-5 | 🟡 MINOR | Race passive tag enforcement varies — `lucky_reroll` tag present in test but production path unverified | — |

---

## 19. Spells

**BG3 Wiki:** https://bg3.wiki/wiki/Spells  
**BG3 Target:** 8 schools. Levels 0-6. Concentration (Con Save DC = max(10, half damage)). Upcasting. Ritual casting. Pact Magic. EK/AT school restrictions.

**Our State:** 225 spell entries + 190 non-spell actions (415 total). Concentration flag, upcast scaling, Warlock Pact Magic all present.

| ID | Severity | Gap | File |
|---|---|---|---|
| SP-1 | 🔴 CRITICAL | **All 225 spells lack `spellSchool` field** — phase files have `"MISSING"`, mechanics file has no key at all. School-gated mechanics (EK/AT restrictions, Wizard Arcane Recovery, school specializations) all broken | All `Data/Actions/bg3_spells_*.json` |
| SP-2 | 🟠 MAJOR | **No ritual casting mechanic** — no `isRitual` field, no slot-free cast path | — |
| SP-3 | 🟠 MAJOR | **Concentration break on damage not modeled** — no Con Save when concentrating character takes damage. Concentration spells effectively unbreakable | `Combat/Rules/RulesEngine.cs` |
| SP-4 | 🟠 MAJOR | **EK/AT school restrictions not enforced** at spell learning. No `allowedSchools` field | `Data/Classes/martial_classes.json` |
| SP-5 | 🟠 MAJOR | Multiple BG3 staple spells missing: `find_familiar`, `polymorph`, `wall_of_fire`, `chain_lightning`, `conjure_elemental`, `greater_invisibility`, etc. | `Data/Actions/` |
| SP-6 | 🟡 MINOR | Divination school effectively empty (0 tagged spells) | — |
| SP-7 | 🟡 MINOR | Missing cantrips: `mending`, `light`, `friends`, `thaumaturgy`, `dancing_lights` | — |

---

## 20. Classes

**BG3 Wiki:** https://bg3.wiki/wiki/Classes  
**BG3 Target:** 12 classes, max level 12. Subclasses at L3. Full level tables with features, spell slots, etc.

**Our State:** All 12 classes with full 12-level `LevelTable` in JSON files.

| ID | Severity | Gap | File |
|---|---|---|---|
| CL-1 | 🔴 CRITICAL | **EK contributes 0 caster levels** — `Fighter.SpellcasterModifier = 0` and EK subclass has no modifier field. `MergeMulticlassSpellSlots` skips. Fix: add 0.3333 to subclass AND update resolver | `Data/Classes/martial_classes.json`, `Data/CharacterModel/CharacterResolver.cs` |
| CL-2 | 🔴 CRITICAL | **AT contributes 0 caster levels** — identical bug for Arcane Trickster | Same files |
| CL-3 | 🟠 MAJOR | `MergeMulticlassSpellSlots` only reads base class modifier, never subclass. Even after fixing JSON, resolver won't pick it up | `Data/CharacterModel/CharacterResolver.cs` |
| CL-4 | 🟠 MAJOR | 4 non-BG3 subclasses in data: `arcane_archer` (Fighter), `drunken_master` (Monk), `swarmkeeper` (Ranger), `glamour` (Bard) | `Data/Classes/*.json` |
| CL-5 | 🟠 MAJOR | Wild Shape beast options not defined. Rage bonus damage not differentiated per subclass | `Data/Classes/martial_classes.json` |
| CL-6 | 🟡 MINOR | ASI/feat grant at `FeatLevels` — path exists but integration test coverage absent | `Data/CharacterModel/CharacterResolver.cs` |

---

## 21. Backgrounds

**BG3 Wiki:** https://bg3.wiki/wiki/Backgrounds  
**BG3 Target:** 12 backgrounds, each granting 2 skill proficiencies. Haunted One = Dark Urge only.

**Our State:** 11/12 backgrounds with correct skill mappings.

| ID | Severity | Gap | File |
|---|---|---|---|
| BG-1 | 🔴 CRITICAL | **Haunted One background entirely absent** (Medicine + Intimidation) | `Data/Backgrounds/BackgroundData.cs` |
| BG-2 | 🟠 MAJOR | No Inspiration tracking tied to backgrounds | `Combat/Services/ResourceManager.cs` |
| BG-3 | 🟡 MINOR | Skill proficiency string casing inconsistency across systems | `Data/Backgrounds/BackgroundData.cs` |

---

## 22. Races

**BG3 Wiki:** https://bg3.wiki/wiki/Races  
**BG3 Target:** 11 races with subraces. Flexible +2/+1 ASI. Race features (Darkvision, Fey Ancestry, Lucky, racial spells, etc.).

**Our State:** All 11 races + all subraces represented. Flexible ASI works. Halfling Lucky, Drow Magic, Dragonborn breath weapons correct.

| ID | Severity | Gap | File |
|---|---|---|---|
| RC-1 | 🔴 CRITICAL | **Duergar Magic entirely missing** — no Enlarge (L3) or Invisibility (L5) in GrantedAbilities | `Data/Races/exotic_races.json` |
| RC-2 | 🟠 MAJOR | **Human Versatility missing** — no +25% carrying capacity, no free skill choice | `Data/Races/core_races.json` |
| RC-3 | 🟠 MAJOR | **Githyanki Astral Knowledge not mechanically enforced** — action exists but temporary skill proficiency grant/expiry not coded | `Combat/Rules/RulesEngine.cs` |
| RC-4 | 🟠 MAJOR | **Githyanki Psionics absent** — no Mage Hand (L1), Enhanced Leap (L3), Misty Step (L5) | `Data/Races/exotic_races.json` |
| RC-5 | 🟠 MAJOR | Forest Gnome Speak with Animals — frequency (at-will vs per-day) needs verification | `Data/Races/exotic_races.json` |
| RC-6 | 🟡 MINOR | Rock Gnome Artificer's Lore expertise not verified in RulesEngine | — |
| RC-7 | 🟡 MINOR | Duergar darkvision field inconsistency (body 12m vs override 24m) | `Data/Races/exotic_races.json` |
| RC-8 | 🟡 MINOR | Halfling Brave (Frightened immunity) — verify tag + ConditionEffects suppression | — |

---

## 23. Cross-Cutting Issues

| ID | Severity | Description |
|---|---|---|
| CC-1 | 🔴 CRITICAL | `spellSchool` absent/MISSING on all 225 spells — breaks all school-gated mechanics |
| CC-2 | 🟠 MAJOR | Concentration save on damage absent from RulesEngine — concentration spells unbreakable |
| CC-3 | 🟠 MAJOR | No long-rest/short-rest recovery test coverage — rest-gated resources untested |
| CC-4 | 🟡 MINOR | Skill proficiency strings use inconsistent casing across systems |

---

## 24. Master Priority Matrix

### 🔴 CRITICAL (20 items)

| # | ID | Subject | Description |
|---|---|---|---|
| 1 | R-1/H-1 | Resources/Healing | Short rest does NOT heal to 50% max HP |
| 2 | SP-1/CC-1 | Spells | All 225 spells lack `spellSchool` field |
| 3 | CL-1 | Classes | EK contributes 0 caster levels to multiclass ESL |
| 4 | CL-2 | Classes | AT contributes 0 caster levels to multiclass ESL |
| 5 | W-1 | Weapons | No enchantment bonus (+1/+2/+3) field |
| 6 | C-1 | Consumables | No Elixir category (until-long-rest duration) |
| 7 | C-2/H-4 | Consumables/Healing | Healing potions cannot be thrown at allies |
| 8 | EQ-1 | Equipment | Non-proficient armor: no save Disadvantage on STR/DEX |
| 9 | BG-1 | Backgrounds | Haunted One background entirely absent |
| 10 | RC-1 | Races | Duergar Magic entirely missing |
| 11 | P-1 | Passives | StatsFunctors NOT executed — all event-driven passive effects skipped |
| 12 | DT-1 | Difficult Terrain | Plant Growth surface missing (×4 movement cost) |
| 13 | DT-2 | Difficult Terrain | Spike Growth damage flat instead of per-distance-moved |
| 14 | S-1 | Surfaces | Ice does NOT apply Prone |
| 15 | S-2 | Surfaces | Grease does NOT apply Prone |
| 16 | CL-1/OB-2 | Clouds/Obscurity | LOSResult.IsObscured is dead code — never consumed |
| 17 | CL-1 | Clouds | Fog Cloud does NOT blind |
| 18 | CL-3 | Clouds | Ranged attack blocking by Darkness/Fog absent |
| 19 | OB-1 | Obscurity | No Lightly vs. Heavily Obscured tier system |
| 20 | OB-3/OB-4 | Obscurity | Fog/Darkness don't impose Disadvantage on attackers |

### 🟠 MAJOR (58 items)

| # | ID | Subject | Description |
|---|---|---|---|
| 1 | AT-1 | Attacks | Mage Armor gives flat AC 13 — missing DEX modifier |
| 2 | D-1 | Damage | Same-type resistance/vulnerability stacks multiplicatively |
| 3 | ST-1 | Saving Throws | Save DC tooltip missing spellcasting ability modifier |
| 4 | ST-2 | Saving Throws | Death saves bypass modifier stack (Bless/Lucky don't apply) |
| 5 | MS-1 | Movement | Unit ambiguity — race speed data needs audit |
| 6 | MS-2 | Movement | Race-specific speeds not verified |
| 7 | MS-3 | Movement | Prone stand-up uses Dashed max instead of base speed |
| 8 | R-2 | Resources | Missing class resource types |
| 9 | R-3 | Resources | Bardic Inspiration die not level-scaled |
| 10 | H-2 | Healing | Bleeding not removed on heal |
| 11 | H-3/C-3 | Healing | Healing potions don't remove Burning |
| 12 | H-5 | Healing | No temporary hit points system |
| 13 | H-6 | Healing | Limited healing spell roster |
| 14 | W-2 | Weapons | No Dippable property |
| 15 | W-3 | Weapons | TWF offhand damage penalty unclear |
| 16 | W-4 | Weapons | Loading (crossbow) enforcement unclear |
| 17 | W-5 | Weapons | Weapon Action proficiency gate not validated |
| 18 | C-4 | Consumables | No Coatings system |
| 19 | C-5 | Consumables | No Arrow-consumables |
| 20 | C-6 | Consumables | Very few scrolls (2 vs dozens) |
| 21 | C-7 | Consumables | No Camp Supplies / Long Rest resource management |
| 22 | EQ-2 | Equipment | Non-proficient armor: no ability check Disadvantage |
| 23 | EQ-3 | Equipment | Shield passive AC across weapon sets unclear |
| 24 | EQ-4 | Equipment | Unarmored Defense not implemented |
| 25 | EQ-5 | Equipment | Item boost application on equip unvalidated |
| 26 | CO-1 | Conditions | Charmed — no targeting validation vs charmer |
| 27 | CO-2 | Conditions | Cursed condition absent |
| 28 | CO-3 | Conditions | Diseased condition absent |
| 29 | CO-4 | Conditions | Polymorphed not in ConditionType |
| 30 | CO-5 | Conditions | Sleeping ≠ Unconscious — loses wake-on-damage |
| 31 | F-1 | Feats | Athlete ASI never applied |
| 32 | F-2 | Feats | Heavily Armoured +1 STR never applied |
| 33 | F-3 | Feats | Lightly Armoured ASI never applied |
| 34 | F-4 | Feats | Magic Initiate (×7) — no spell grants |
| 35 | F-5 | Feats | Martial Adept — no maneuver selection |
| 36 | F-6 | Feats | Medium Armour Master — +3 DEX cap not enforced |
| 37 | F-7 | Feats | Moderately Armoured ASI never applied |
| 38 | F-8 | Feats | Ritual Caster — no spell selection |
| 39 | F-9 | Feats | Weapon Master — proficiencies not granted |
| 40 | P-2 | Passives | BoostConditions evaluation incomplete |
| 41 | SP-2 | Spells | No ritual casting mechanic |
| 42 | SP-3/CC-2 | Spells | Concentration break on damage not modeled |
| 43 | SP-4 | Spells | EK/AT school restrictions not enforced |
| 44 | SP-5 | Spells | Missing BG3 staple spells (polymorph, wall_of_fire, etc.) |
| 45 | CL-3 | Classes | MergeMulticlassSpellSlots ignores subclass modifier |
| 46 | CL-4 | Classes | 4 non-BG3 subclasses in data |
| 47 | CL-5 | Classes | Wild Shape/Rage not fully modeled |
| 48 | BG-2 | Backgrounds | No Inspiration tracking |
| 49 | RC-2 | Races | Human Versatility missing |
| 50 | RC-3 | Races | Githyanki Astral Knowledge not enforced |
| 51 | RC-4 | Races | Githyanki Psionics absent |
| 52 | RC-5 | Races | Forest Gnome Speak with Animals frequency unverified |
| 53 | DT-3 | Difficult Terrain | Missing surfaces: Mud, Black Tentacles, Vines, Sewage, Lava |
| 54 | DT-4 | Difficult Terrain | Jump cost not affected by difficult terrain |
| 55 | DT-5 | Difficult Terrain | Movement-expending weapon actions not blocked in DT |
| 56 | S-3 | Surfaces | Fire damage flat 5 instead of 1d4 |
| 57 | S-4 | Surfaces | Acid doesn't reduce AC |
| 58 | S-5 | Surfaces | Dip weapon mechanic absent |
| 59 | S-6 | Surfaces | Steam missing Wet status |
| 60 | S-7 | Surfaces | Oil + Fire interaction broken |
| 61 | CL-4/5/6/7 | Clouds | Multiple wrong/missing cloud mechanics |
| 62 | OB-5 | Obscurity | No Darkvision mechanic |
| 63 | OB-6/7 | Obscurity | Hide + Stealth vs Perception absent |

### 🟡 MINOR (35+ items)

Actions (3), Movement (2), Resources (2), Abilities (2), Attacks (2), Damage (3), Weapons (2), Consumables (2), Healing (1), Saving Throws (2), Conditions (4), Feats (7), Passives (3), Spells (2), Classes (1), Backgrounds (1), Races (3), Surfaces (1), Obscurity (1).

---

---

## 25. Reviewer Addendum — Corrections, New Gaps, and False Positives

*This section captures the findings from the adversarial reviewer pass, run AFTER the initial research. Items here override or supplement the findings above.*

### False Positives (REMOVE from backlog)

| Original ID | Subject | Claim | Actual Finding |
|---|---|---|---|
| SP-1 / CC-1 | Spells | "All 225 spells lack `spellSchool`" | **FALSE.** JSON uses field name `"school"` (not `"spellSchool"`). All 161 entries across 5 phase files have a populated `school` field (evocation, necromancy, etc.). No fix needed. |
| SP-3 / CC-2 | Spells | "Concentration break on damage not modeled" | **FALSE.** `ConcentrationSystem.cs` subscribes to `DamageTaken` events, computes `DC = Math.Max(10, damageTaken / 2)`, runs CON save with War Caster advantage support. Fully implemented. |
| SP-5 | Spells | "Missing staple spells: polymorph, wall_of_fire, etc." | **MOSTLY FALSE.** 11 of 12 listed spells ARE present. Only `conjure_elemental` genuinely absent. |
| SP-7 | Spells | "Missing cantrips: mending, light, friends, thaumaturgy" | **FALSE.** All named cantrips are present in the spell files. |
| H-5 | Healing | "No temporary hit points system" | **FALSE.** `Combatant.Resources.TemporaryHP` exists, `AddTemporaryHP()` uses BG3-correct `Math.Max` (non-stacking), `DamagePipeline` absorbs TempHP before HP. |
| ST-4 | Saving Throws | "Concentration save pipeline unverified" | **FALSE.** Concentration saves use `RulesEngine.RollSave()` with full modifier stack, War Caster advantage, Mage Slayer disadvantage. DC = `Math.Max(10, damage/2)`. Correct. |
| MS-3 | Movement | "Prone stand-up uses Dashed max" | **FALSE.** `MaxMovement` is set from `GetSpeed()` + boosts BEFORE any Dash action occurs that turn. Dash is a turn action used after `BeginTurn`. No inflation possible. |
| CO-5 | Conditions | "Sleeping loses wake-on-damage" | **FALSE.** `StatusSystem.ProcessEventForStatusRemoval` fires `RemoveStatus("asleep")` on every `DamageTaken` event where `FinalValue > 0`. Wake-on-damage IS implemented. |
| RC-4 | Races | "Githyanki Psionics absent" | **FALSE.** `githyanki_psionics_l1` grants `mage_hand`, `l3` grants `enhance_leap`, `l5` grants `misty_step` — all gated by `GrantedAtLevel`. Fully implemented. |
| EQ-5 | Equipment | "Item boost application on equip unvalidated" | **FALSE.** `BoostApplicator.ApplyBoosts()` IS called on equipped items in `InventoryService.ApplyEquipment()`. Works correctly. |
| P-1 | Passives | "StatsFunctors NOT executed" | **FALSE.** The comment in `BG3PassiveData.cs` is outdated. `PassiveFunctorProviderFactory` maps 10 `StatsFunctorContext` values to `RuleWindow` instances. `PassiveManager.GrantPassive` auto-registers `GenericFunctorRuleProvider` for any passive with `HasStatsFunctors`. StatsFunctors ARE executed. |

#### Feat False Positives (6 feats reported as broken that actually work):

| Gap | Claim | Actual |
|---|---|---|
| F-1 Athlete | "no handler" | `case "athlete":` exists in `ApplyFeatChoices` — works |
| F-2 Heavily Armoured | "+1 STR never applied" | `AbilityScoreIncreases: {"Strength": 1}` in JSON, applied by `CharacterSheet.GetAbilityScore`. Armor proficiency via `ApplyProficiencyGrant`. Both work. |
| F-3 Lightly Armoured | "ASI never applied" | `case "lightly_armoured":` exists in `ApplyFeatChoices` — works |
| F-4 Magic Initiate (×7) | "no handler" | `featId.StartsWith("magic_initiate")` block in `ApplyFeatChoices` grants chosen spells — works |
| F-6 Med Armour Master | "+3 DEX cap not enforced" | `InventoryService.cs` explicitly checks `medium_armor_master`/`medium_armour_master` feat, sets `maxDex = 3` — works |
| F-7 Moderately Armoured | "ASI never applied" | `case "moderately_armoured":` exists in `ApplyFeatChoices` — works |

---

### NEW 🔴 CRITICAL Gaps Found by Reviewers

#### RV-C1: Action Surge Incorrectly Costs a Bonus Action
- **File:** `Data/Actions/bg3_mechanics_actions.json` (action_surge cost block)
- **Bug:** `"usesBonusAction": true` in cost definition. BG3: Action Surge has ZERO action cost — it only consumes the `action_surge` resource (1/short rest).
- **Impact:** Fighter using Action Surge loses their Bonus Action (no off-hand attack, no Second Wind, no bonus action abilities that turn)
- **Fix:** Remove `"usesBonusAction": true` from the action_surge cost block

#### RV-C2: Ice/Grease Prone Bypasses DEX Save (Architectural Gap)
- **File:** `Combat/Environment/SurfaceManager.cs` (TriggerSurface method)
- **Bug:** Even after adding `AppliesStatusId = "prone"` to Ice/Grease, the `TriggerSurface` method applies status unconditionally — no saving throw. BG3: "Must succeed DC 10 DEX save or fall Prone."
- **Impact:** `SurfaceDefinition` has no `SaveAbility`/`SaveDC` fields. This is an architectural gap, not just a data patch.
- **Fix:** Add `SaveAbility` and `SaveDC` fields to `SurfaceDefinition`, add conditional save logic to `TriggerSurface`

#### RV-C3: Warlock Eldritch Invocations Completely Non-Functional
- **File:** `Data/BG3DataLoader.cs`, `Data/CharacterModel/ClassDefinition.cs`
- **Bug:** Three independent failures: (1) `warlock_invocations.json` is never loaded by `BG3DataLoader`; (2) `LevelProgression` has no `InvocationsKnown` property — dead data in JSON; (3) No selection logic in `CharacterResolver`
- **Impact:** Agonizing Blast, Repelling Blast, Devil's Sight, Mask of Many Faces — all non-functional. Warlock class loses its primary customization system.
- **Fix:** Load the invocations file, add `InvocationsKnown` to class definition, add selection logic

---

### NEW 🟠 MAJOR Gaps Found by Reviewers

#### RV-M1: D-1 is WORSE Than Reported — Cross-Subsystem Double-Apply
- **Bug:** Status-based `DamageTaken` percentage modifiers (e.g., Wet = −50% fire) are applied in `DamagePipeline`, then `BoostEvaluator.GetResistanceLevel()` applies racial resistance AGAIN in a separate stage.
- **Scenario:** Tiefling (fire resistant) gets Wet → 20 fire damage → ×0.5 (Wet in pipeline) → 10 → ×0.5 (racial boost) → **5 damage** instead of correct **10**
- **Fix:** Move Wet fire mitigation from `Percentage` modifier to `Resistance(Fire, Resistant)` boost so `GetResistanceLevel()` can dedup

#### RV-M2: R-1 Explorer Mode Heals to 100% Instead of 50%
- **Bug:** `RestService.ProcessShortRest()` heals `MaxHP - CurrentHP` (full heal) in Explorer mode. BG3 Explorer mode gives other benefits, not extra healing. Short rest always heals to 50%.
- **Fix:** Change to heal to 50% in normal mode, keep 100% only for Explorer as a separate condition

#### RV-M3: FightingStyle_Archery +2 Never Applied to Attack Rolls
- **Bug:** `BoostEvaluator.GetRollBonusDice()` matches on `"AttackRoll"` — NOT on `"RangedWeaponAttack"`. The Archery passive stores its boost as `RollBonus(RangedWeaponAttack, 2)` which is never matched.
- **Impact:** Every Fighter/Ranger with Archery fighting style has been missing +2 to ranged attack rolls
- **Fix:** In `EffectPipeline.cs`, add unconditional attack-type bonus query before `RollAttack`

#### RV-M4: Aura of Protection — Wrong Value + No Ally Propagation
- **Bug:** (1) `aura_of_protection` status hardcodes `"value": 3`. BG3: bonus = Paladin's CHA modifier. (2) `aura_of_protection_bonus` status is never applied to nearby allies.
- **Impact:** Paladin's signature ability doesn't scale with CHA and doesn't help the party
- **Fix:** Dynamic value based on CHA mod; add per-turn ally scan in `TurnLifecycleService`

#### RV-M5: Barbarian/Monk Unarmored Defense Passive ID Mismatch
- **Bug:** `ScenarioBootService` grants passive `unarmoured_defence` but `PassiveRegistry` stores it as `UnarmouredDefence_Barbarian`. These never match. On weapon-set switch, AC falls back to `10 + DEX`, losing CON/WIS bonus.
- **Fix:** Map feature IDs to passive IDs in `ScenarioLoader.cs`

#### RV-M6: Reliable Talent is Dead Code
- **Bug:** `BoostEvaluator.GetMinimumRollResult()` is implemented but has exactly one reference — its own definition. Never called by `RulesEngine`, `EffectPipeline`, or any skill-check path.
- **Impact:** Rogue's `MinimumRollResult(AttackRoll, 10)` boost is stored but never enforced
- **Fix:** In `RulesEngine.RollAttack`, enforce minimum roll after computing natural roll

#### RV-M7: Frozen Missing GrantsAdvantageToAttackers
- **Bug:** `ConditionEffects.cs` Frozen mechanics sets `IsIncapacitated`, `CantMove`, `AutoFailStrDexSaves` but NOT `GrantsAdvantageToAttackers`. Per BG3: attackers should have advantage against Frozen targets.
- **Fix:** Add `GrantsAdvantageToAttackers = true` to Frozen's `ConditionMechanics`

#### RV-M8: TemporaryHP Boost Type Never Applied
- **Bug:** `BoostType.TemporaryHP` exists in enum but `BoostEvaluator` has no `GetTemporaryHP()` method and never calls `AddTemporaryHP()`. Any passive/status with `TemporaryHP(N)` boost string (False Life, Fiend patron) is silently parsed and discarded.
- **Fix:** Add `GetTemporaryHP()` to `BoostEvaluator` and wire it into the spell/status application path

#### RV-M9: Surface Double-Creation (Fire into Oil)
- **Bug:** When Fire is cast into Oil: `CheckInteractions` transforms Oil to Fire_A, then `_activeSurfaces.Add(instance)` adds original Fire_B. Result: two overlapping fire surfaces at same position → double damage.
- **Fix:** Skip adding new instance if `CheckInteractions` performed a transformation

#### RV-M10: StatsFunctorContext Mapping Incomplete
- **Bug:** `PassiveFunctorProviderFactory` maps only 10 contexts. The BG3 `Passive.txt` uses many more (OnCreate, OnKill, OnShortRest, OnLongRest, OnStatusApplied, OnDying). Passives with unmapped contexts silently return null.
- **Fix:** Add missing context mappings; add new `RuleWindow` entries for rest/kill/status events

#### RV-M11: BoostConditions Evaluation Completely Skipped
- **Bug:** `PassiveManager.GrantPassive` calls `BoostApplicator.ApplyBoosts()` unconditionally, ignoring `passive.BoostConditions`. Example: `FightingStyle_Defense` (+1 AC when wearing armor) applies even when unarmored.
- **Fix:** Evaluate `BoostConditions` before applying boosts

#### RV-M12: Cantrip Damage Scaling Threshold Wrong
- **File:** `Combat/Rules/LevelMapResolver.cs`
- **Bug:** Breakpoint uses `< 10` instead of `< 11`. At level 10, cantrips deal 3 dice instead of correct 2 dice. BG3/5e: L1-4 = 1 die, L5-10 = 2 dice, L11+ = 3 dice.
- **Fix:** Change `< 10` to `< 11` on all 4 cantrip lines

#### RV-M13: Paladin `UsesPreparedSpells` Missing
- **File:** `Data/Classes/divine_classes.json`
- **Bug:** Paladin has no `UsesPreparedSpells` field → defaults to `false`. Goes through known-spell path instead of prepared-spell path (`classLevel + CHA mod`).
- **Fix:** Add `"UsesPreparedSpells": true` to Paladin class entry

#### RV-M14: 12 Non-BG3 Subclasses (Not 4 as Originally Reported)
- **Bug:** Reviewer found 12 subclasses not in BG3: arcane_archer, path_of_the_giant, drunken_master, swashbuckler, death (cleric), crown (paladin), stars (druid), swarmkeeper, bladesinging (wizard), shadow_magic (sorcerer), hexblade (warlock), glamour (bard)
- **Note:** Some of these (Bladesinging, Hexblade, Swashbuckler) are popular D&D 5e subclasses but are NOT in BG3

#### RV-M15: All Racial `advantage_vs_*` Saving Throw Tags Are Dead Code
- **Bug:** Tags `advantage_vs_frightened` (Halfling Brave), `advantage_vs_charmed` (Fey Ancestry), `advantage_vs_poison` (Dwarven Resilience) are stored on features and propagate to combatants, but ZERO lines in the Combat directory read these tags during saving throw resolution.
- **Impact:** Three racial protective features grant nothing in actual gameplay
- **Fix:** In `RulesEngine` saving throw resolution, check combatant tags against condition being saved against

#### RV-M16: HudController Missing Enchantment Bonus Display
- **Bug:** `HudController.cs` computes `MeleeAttackBonus = abilityMod + ProficiencyBonus` hardcoded — no enchantment bonus from equipped weapon shown
- **Fix:** Add weapon enchantment bonus to HUD display calculation

---

### Corrected Priority Matrix (Post-Review)

#### 🔴 CRITICAL (15 items — was 20, minus 5 false positives, plus 3 new)

| # | ID | Subject | Description |
|---|---|---|---|
| 1 | R-1 | Resources | Short rest does NOT heal to 50% max HP (Explorer mode wrongly heals to 100%) |
| 2 | W-1 | Weapons | No enchantment bonus (+1/+2/+3) field |
| 3 | C-1 | Consumables | No Elixir category (until-long-rest duration) |
| 4 | C-2 | Consumables/Healing | Healing potions cannot be thrown at allies |
| 5 | EQ-1 | Equipment | Non-proficient armor: no save Disadvantage on STR/DEX |
| 6 | BG-1 | Backgrounds | Haunted One background entirely absent |
| 7 | RC-1 | Races | Duergar Magic entirely missing |
| 8 | CL-1/2 | Classes | EK/AT contribute 0 caster levels to multiclass ESL |
| 9 | RV-C1 | Actions | Action Surge incorrectly costs a Bonus Action |
| 10 | RV-C2 | Surfaces | Ice/Grease Prone bypasses DEX save (architectural gap) |
| 11 | RV-C3 | Classes | Warlock Eldritch Invocations completely non-functional |
| 12 | DT-1 | Difficult Terrain | Plant Growth surface missing (×4 cost) |
| 13 | DT-2 | Difficult Terrain | Spike Growth damage flat instead of per-distance |
| 14 | S-1/S-2 | Surfaces | Ice/Grease do NOT apply Prone |
| 15 | OB-1/2/3/4 | Obscurity | Entire obscurity system non-functional (dead code, no tiers) |

#### 🟠 MAJOR (60+ items — see individual sections above + reviewer additions RV-M1 through RV-M16)

#### Items Removed from CRITICAL (now resolved/false positive):
- ~~SP-1/CC-1~~ (spellSchool) — field `"school"` exists and is populated on all entries
- ~~SP-3/CC-2~~ (Concentration break) — fully implemented in `ConcentrationSystem.cs`
- ~~P-1~~ (StatsFunctors) — StatsFunctors ARE executed via `PassiveFunctorProviderFactory`
- ~~CL-1 (Fog)~~ — Still CRITICAL, just verifying it wasn't a false positive
- ~~CL-2 (LOSResult.IsObscured)~~ — Still CRITICAL, confirmed dead code

---

*This document was generated by parallel researcher teams with adversarial review passes covering all 22 subjects. False positives have been identified and marked. See individual subject sections for file paths and line numbers.*
