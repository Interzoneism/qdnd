# BG3 Combat Parity — Implementation Plan

**Generated:** 2026-02-24
**Scope:** Combat-only (no resting, no exploration, no camp mechanics)
**Source:** Full gap analysis with adversarial reviewer pass — false positives removed
**Priority:** 🔴 CRITICAL (13) · 🟠 MAJOR (~55) · 🟡 MINOR (~30)

---

## Table of Contents

- [Phase 1: CRITICAL Work Packages](#phase-1-critical-work-packages)
- [Phase 2: MAJOR Work Packages](#phase-2-major-work-packages)
- [Phase 3: MINOR Items](#phase-3-minor-items)

---

## Phase 1: CRITICAL Work Packages

### WP-C01: Obscurity & Line-of-Sight System Overhaul

**Items:** OB-1, OB-2, OB-3, OB-4, CL-1, CL-2, CL-3
**Files:** `Combat/Environment/LOSService.cs`, `Combat/Actions/EffectPipeline.cs`, `Combat/Environment/SurfaceManager.cs`, `Combat/Rules/RulesEngine.cs`
**Dependencies:** None (foundational system)

The entire obscurity system is non-functional. `LOSResult.IsObscured` is set by `LOSService` but **never read** by any combat system — it's dead code. There are no obscurity tiers, Fog Cloud doesn't blind, Darkness doesn't impose disadvantage on outside attackers, and ranged attacks pass through clouds freely.

**Tasks:**

1. **Implement 3-tier obscurity model** in `LOSService.cs`: Clear / Lightly Obscured / Heavily Obscured. Replace binary `IsObscured` with an enum tier on `LOSResult`.
2. **Wire LOSResult into EffectPipeline.cs** — attack resolution must query obscurity:
   - Attacking from/into Heavily Obscured → Disadvantage
   - Ranged attacks through Darkness/Fog → blocked entirely
3. **Make Fog Cloud apply Blinded** — add `AppliesStatusId = "blinded"` to fog surface, or apply Heavily Obscured tier which grants equivalent mechanical effect.
4. **Fix Darkness for outside attackers** — creatures IN Darkness get Blinded (correct), but attackers TARGETING into Darkness from outside must also get Disadvantage.
5. **Add cloud tag to LOS queries** — `LOSService` must check for cloud/obscure surfaces between attacker and target, not just at target's position.

**Acceptance:** Fog Cloud blinds creatures inside. Ranged attacks cannot pass through Darkness/Fog. Attacking into/from Heavily Obscured zones imposes Disadvantage. Unit tests cover all 3 tiers.

---

### WP-C02: Surface Prone + DEX Save Architecture

**Items:** S-1, S-2, RV-C2
**Files:** `Combat/Environment/SurfaceManager.cs`, `Combat/Environment/SurfaceDefinition.cs`
**Dependencies:** None

Ice and Grease surfaces do not apply Prone. Adding `AppliesStatusId = "prone"` isn't enough — `TriggerSurface` applies status unconditionally with no saving throw. BG3 requires: "DC 10 DEX save or fall Prone."

**Tasks:**

1. **Add `SaveAbility` and `SaveDC` fields to `SurfaceDefinition`** — nullable fields; when set, `TriggerSurface` must run a saving throw before applying the status.
2. **Add conditional save logic to `TriggerSurface`** — if `SaveAbility != null`, call `RulesEngine.RollSave()` with `SaveDC`. Only apply status on failure.
3. **Set Ice surface:** `AppliesStatusId = "prone"`, `SaveAbility = "Dexterity"`, `SaveDC = 10`
4. **Set Grease surface:** `AppliesStatusId = "prone"`, `SaveAbility = "Dexterity"`, `SaveDC = 10`

**Acceptance:** Walking on Ice/Grease triggers DEX DC 10 save; failure = Prone; success = unaffected. Save uses full modifier stack.

---

### WP-C03: Weapon Enchantment Bonus System

**Items:** W-1, RV-M16
**Files:** `Data/CharacterModel/EquipmentDefinition.cs`, `Combat/Actions/EffectPipeline.cs`, `Combat/UI/HudController.cs`
**Dependencies:** None

Neither `WeaponDefinition` nor `BG3WeaponData` has an `EnchantmentBonus` int. Magic weapons (+1/+2/+3) cannot add their bonus to attack rolls and damage rolls.

**Tasks:**

1. **Add `EnchantmentBonus` field** (int, default 0) to `WeaponDefinition` and/or `EquipmentDefinition`.
2. **Wire into attack roll** in `EffectPipeline.cs` — add enchantment bonus to the attack roll alongside ability mod and proficiency.
3. **Wire into damage roll** — add enchantment bonus as flat damage bonus.
4. **Fix HUD display** — `HudController.cs` computes `MeleeAttackBonus = abilityMod + ProficiencyBonus` without enchantment. Add it.
5. **Update weapon data** — ensure any magic weapons in JSON files have correct enchantment values.

**Acceptance:** A +1 Longsword shows correct to-hit and damage in HUD and applies correct values in combat.

---

### WP-C04: EK/AT Caster Levels + Multiclass Spell Slot Fix

**Items:** CL-1, CL-2, CL-3
**Files:** `Data/Classes/martial_classes.json`, `Data/CharacterModel/CharacterResolver.cs`
**Dependencies:** None

Eldritch Knight (Fighter) and Arcane Trickster (Rogue) contribute 0 caster levels because `Fighter.SpellcasterModifier = 0` and subclasses have no modifier field. `MergeMulticlassSpellSlots` only reads the base class modifier, never the subclass.

**Tasks:**

1. **Add `SpellcasterModifier: 0.3333` to EK subclass** in `martial_classes.json`.
2. **Add `SpellcasterModifier: 0.3333` to AT subclass** in `martial_classes.json`.
3. **Fix `CharacterResolver.MergeMulticlassSpellSlots`** to read subclass `SpellcasterModifier` when base class has 0. The resolver must check if the active subclass provides a modifier.
4. **Verify** a Fighter 6 / Wizard 6 multiclass gets correct merged spell slots (EK contributes 2 caster levels from 6 Fighter levels).

**Acceptance:** EK L7 character has correct L1/L2 spell slots. Multiclass EK/Wizard gets correct merged ESL.

---

### WP-C05: Warlock Eldritch Invocations

**Items:** RV-C3
**Files:** `Data/BG3DataLoader.cs`, `Data/CharacterModel/ClassDefinition.cs`, `Data/CharacterModel/CharacterResolver.cs`, `Data/Classes/warlock_invocations.json` (if it exists)
**Dependencies:** None

Three independent failures: (1) `warlock_invocations.json` is never loaded by `BG3DataLoader`; (2) `LevelProgression` has no `InvocationsKnown` property; (3) No selection logic in `CharacterResolver`.

**Tasks:**

1. **Load invocations file** in `BG3DataLoader` — register entries into an invocation registry.
2. **Add `InvocationsKnown` to class level definition** — Warlock gains invocations at L2, L5, L7, L9, L12.
3. **Add invocation selection/grant logic** in `CharacterResolver` — map selected invocation IDs to passive/boost grants.
4. **Verify key invocations work:** Agonizing Blast (+CHA to Eldritch Blast damage), Repelling Blast (push 4.5m), Devil's Sight (see through Darkness), Mask of Many Faces (Disguise Self at will — may be non-combat, skip if so).

**Acceptance:** Warlock at L2+ can have invocations that mechanically function. Agonizing Blast adds CHA mod to Eldritch Blast damage.

---

### WP-C06: Action Surge Cost Fix

**Items:** RV-C1
**Files:** `Data/Actions/bg3_mechanics_actions.json`
**Dependencies:** None

Action Surge has `"usesBonusAction": true` in its cost definition. BG3: Action Surge costs ZERO action economy — it only consumes the `action_surge` resource (1 charge).

**Tasks:**

1. **Remove `"usesBonusAction": true`** from the `action_surge` action's cost block in `bg3_mechanics_actions.json`.
2. **Verify** Fighter can use Action Surge and still use Bonus Action (off-hand attack, Second Wind) in the same turn.

**Acceptance:** Action Surge only consumes its resource charge, not a Bonus Action.

---

### WP-C07: Plant Growth + Spike Growth Surfaces

**Items:** DT-1, DT-2
**Files:** `Combat/Environment/SurfaceManager.cs`, `Combat/Movement/MovementService.cs`
**Dependencies:** None

Plant Growth surface doesn't exist (should quarter movement: ×4 cost). Spike Growth deals flat `DamagePerTrigger = 3` on enter/turn-start, but BG3 deals 2d4 Piercing for every 1.5m (5ft) moved through the zone — damage must scale with distance.

**Tasks:**

1. **Add `plant_growth` surface** — `MovementCostMultiplier = 4.0f`, no damage, Difficult Terrain.
2. **Redesign Spike Growth damage** — damage must be proportional to distance moved through the zone. Instead of flat `DamagePerTrigger`, calculate `2d4 per 1.5m (5ft) of movement through zone`. This likely requires `MovementService` to report distance traveled through each surface to `SurfaceManager`.
3. **Update `SurfaceDefinition`** to support distance-based damage or add a `DamagePerDistanceUnit` field as alternative to `DamagePerTrigger`.

**Acceptance:** Plant Growth zone costs ×4 movement. Spike Growth deals 2d4 per 5ft moved through it, verified with a unit moving 15ft through the zone taking 6d4.

---

### WP-C08: Missing Data — Haunted One + Duergar Magic

**Items:** BG-1, RC-1
**Files:** `Data/Backgrounds/BackgroundData.cs`, `Data/Races/exotic_races.json`
**Dependencies:** None

Haunted One background (Medicine + Intimidation) is entirely absent. Duergar Magic (Enlarge at L3, Invisibility at L5) is entirely missing from `GrantedAbilities`.

**Tasks:**

1. **Add Haunted One background** with skill proficiencies: Medicine, Intimidation.
2. **Add Duergar racial spells** to `exotic_races.json`: Enlarge (L3), Invisibility (L5) gated by `GrantedAtLevel`.
3. **Verify** Duergar character at L5 has both racial spells available.

**Acceptance:** Haunted One selectable and grants correct skills. Duergar L5 character has Enlarge and Invisibility.

---

### WP-C09: Non-Proficient Armor Save Disadvantage

**Items:** EQ-1
**Files:** `Combat/Rules/RulesEngine.cs`
**Dependencies:** None

Wearing armor without proficiency only penalizes attack rolls. BG3: also imposes Disadvantage on STR and DEX **saving throws**.

**Tasks:**

1. **In `RulesEngine` saving throw resolution**, check if the creature is wearing armor they're not proficient with.
2. **If so, and the save is STR or DEX**, apply Disadvantage.
3. **Verify** non-proficient Wizard in heavy armor has Disadvantage on DEX saves.

**Acceptance:** Non-proficient armor wearer gets Disadvantage on STR/DEX saves in addition to existing attack roll penalty.

---

### WP-C10: Healing Potion Ally Targeting

**Items:** C-2, H-4
**Files:** `Data/Actions/consumable_items.json`, `Combat/Targeting/`, `Combat/Actions/EffectPipeline.cs`
**Dependencies:** None

All healing potions are `targetType: "self"`. BG3 allows throwing potions at allies' feet for area healing.

**Tasks:**

1. **Change healing potion `targetType`** to support ally targeting (thrown).
2. **Add thrown potion targeting mode** — select an ally or ground position within throwing range. On impact, apply healing to target or creatures in small radius.
3. **Verify** throwing a Potion of Healing at a downed ally revives them.

**Acceptance:** Healing potions can be used on self (drink) or thrown at allies (throw). Both modes heal correctly.

---

## Phase 2: MAJOR Work Packages

### WP-M01: Damage Pipeline Fixes

**Items:** D-1, RV-M1, RV-M9
**Files:** `Combat/Rules/DamagePipeline.cs`, `Combat/Environment/SurfaceManager.cs`, `Combat/Actions/Effects/Effect.cs`
**Dependencies:** None

Three distinct damage calculation bugs:

1. **Multiple same-type resistance/vulnerability stacks multiplicatively** — two fire resistances → ×0.25 instead of ×0.5. BG3: only one instance of each resistance type counts. Deduplicate before applying.
2. **Cross-subsystem double-apply** — Status-based percentage modifiers (e.g., Wet = −50% fire) are applied in `DamagePipeline`, then `BoostEvaluator.GetResistanceLevel()` applies racial resistance AGAIN. Tiefling (fire resistant) + Wet → ×0.25 instead of ×0.5. Fix: Move Wet fire mitigation to `Resistance(Fire, Resistant)` boost so `GetResistanceLevel()` can dedup with racial resistance.
3. **Surface double-creation** — Fire cast into Oil: `CheckInteractions` transforms Oil→Fire_A, then original Fire_B is added → two overlapping fire surfaces → double damage. Skip adding new instance if `CheckInteractions` performed a transformation.

**Acceptance:** Two fire resistances = ×0.5 (not ×0.25). Wet Tiefling takes half fire damage (not quarter). Fire into Oil creates one fire surface.

---

### WP-M02: Equipment & Armor System

**Items:** AT-1, EQ-2, EQ-3, EQ-4, RV-M5, EQ-6
**Files:** `Data/Statuses/bg3_mechanics_statuses.json`, `Combat/Services/InventoryService.cs`, `Combat/Rules/RulesEngine.cs`
**Dependencies:** None

1. **Mage Armor gives flat AC 13, missing DEX** — status has `modifier: {target: "armorClass", type: "override", value: 13}`. Must be `13 + DEX mod`.
2. **Non-proficient armor: STR/DEX ability checks** don't get Disadvantage (only attack rolls penalized).
3. **Shield passive AC across weapon sets** — unclear if shield contributes when ranged weapon set is active. Verify and fix.
4. **Unarmored Defense not implemented** — Barbarian `10+DEX+CON`, Monk `10+DEX+WIS`. No dedicated code path.
5. **Unarmored Defense passive ID mismatch** — `ScenarioBootService` grants `unarmoured_defence` but `PassiveRegistry` stores `UnarmouredDefence_Barbarian`. Never match. AC falls back to `10+DEX`.
6. **Heavy armor STR requirement** — `StrengthRequirement` field exists but −10ft speed penalty not enforced when STR is too low.

**Acceptance:** Mage Armor = 13+DEX. Non-proficient armor penalizes STR/DEX checks. Shield AC persists across weapon sets. Barbarian/Monk Unarmored Defense works. Heavy armor speed penalty enforced.

---

### WP-M03: Condition System Gaps

**Items:** CO-1, CO-2, CO-3, CO-4, RV-M7
**Files:** `Combat/Statuses/ConditionEffects.cs`, `Combat/Statuses/StatusSystem.cs`
**Dependencies:** None

1. **Charmed** — no targeting validation vs charmer. Charmed creatures can still attack their charmer.
2. **Cursed** — absent from `ConditionType` enum and `StatusToCondition` map. Bestow Curse sub-effects have no mechanics.
3. **Diseased** — absent. Disease statuses have no condition effects.
4. **Polymorphed** — not in `ConditionType`. No incapacitated/no-spell-cast enforcement.
5. **Frozen missing `GrantsAdvantageToAttackers`** — Frozen sets IsIncapacitated, CantMove, AutoFailStrDexSaves but attackers don't get Advantage.

**Acceptance:** Charmed creatures can't target charmer. Cursed/Diseased/Polymorphed conditions added with correct mechanics. Frozen grants advantage to attackers.

---

### WP-M04: Passive & Boost System

**Items:** P-2, RV-M10, RV-M11
**Files:** `Combat/Passives/PassiveManager.cs`, `Data/Passives/PassiveFunctorProviderFactory.cs`, `Combat/Passives/BoostApplicator.cs`
**Dependencies:** None

1. **BoostConditions completely skipped** — `PassiveManager.GrantPassive` calls `BoostApplicator.ApplyBoosts()` unconditionally, ignoring `passive.BoostConditions`. Example: `FightingStyle_Defense` (+1 AC when wearing armor) applies even when unarmored.
2. **StatsFunctorContext mapping incomplete** — `PassiveFunctorProviderFactory` maps only 10 contexts. BG3 `Passive.txt` uses more combat-relevant ones: `OnCreate`, `OnKill`, `OnStatusApplied`, `OnDying`. Passives with unmapped contexts silently return null.
3. **BoostConditions evaluation** — condition syntax (`HasWeaponInInventory()`, `IsWearingArmor()`) evaluation completeness unknown.

**Acceptance:** `FightingStyle_Defense` only applies +1 AC when wearing armor. OnKill/OnStatusApplied/OnDying passive triggers fire correctly.

---

### WP-M05: Saving Throw & Death Save Pipeline

**Items:** ST-1, ST-2, AB-1, RV-M15
**Files:** `Combat/Services/ActionBarService.cs`, `Combat/Services/TurnLifecycleService.cs`, `Combat/Rules/RulesEngine.cs`
**Dependencies:** None

1. **Save DC tooltip missing spellcasting ability modifier** — `ComputeTooltipSaveDC` returns `8 + prof + SaveDCBonus` without spellcasting mod. L5 Wizard INT 18 shows DC 11 instead of 15. UI-only bug (combat `ComputeSaveDC` is correct).
2. **Death saves bypass modifier stack** — uses raw `_getRng()?.Next(1, 21)` instead of `RulesEngine.RollSave`. Bless, Bane, Guidance, Halfling Lucky don't apply.
3. **Death saves don't respect general save bonuses** — raw d20 used instead of going through modifier stack (same root cause as above).
4. **All racial `advantage_vs_*` saving throw tags are dead code** — `advantage_vs_frightened` (Halfling Brave), `advantage_vs_charmed` (Fey Ancestry), `advantage_vs_poison` (Dwarven Resilience) are stored but ZERO lines in Combat read them during save resolution. Three racial protective features grant nothing.

**Acceptance:** Save DC tooltip shows correct value. Death saves go through modifier stack (Bless applies). Halfling Brave grants advantage vs Frightened saves. Fey Ancestry grants advantage vs Charmed.

---

### WP-M06: Combat Pipeline Fixes

**Items:** RV-M3, RV-M6, RV-M8, RV-M12
**Files:** `Combat/Actions/EffectPipeline.cs`, `Combat/Rules/RulesEngine.cs`, `Combat/Passives/BoostEvaluator.cs`, `Combat/Rules/LevelMapResolver.cs`
**Dependencies:** None

Four independent pipeline bugs:

1. **Archery Fighting Style +2 never applied** — `BoostEvaluator.GetRollBonusDice()` matches on `"AttackRoll"` but Archery stores its boost as `RollBonus(RangedWeaponAttack, 2)`. The tag is never matched.
2. **Reliable Talent is dead code** — `BoostEvaluator.GetMinimumRollResult()` is implemented but never called. Rogue's `MinimumRollResult(AttackRoll, 10)` boost is stored but never enforced.
3. **TemporaryHP boost type never applied** — `BoostType.TemporaryHP` exists but `BoostEvaluator` has no `GetTemporaryHP()` method. False Life, Fiend patron TempHP passives are silently discarded.
4. **Cantrip damage scaling threshold wrong** — `LevelMapResolver.cs` uses `< 10` instead of `< 11`. At level 10, cantrips deal 3 dice instead of correct 2. BG3: L1-4 = 1 die, L5-10 = 2 dice, L11+ = 3.

**Acceptance:** Ranger with Archery gets +2 ranged attack rolls. Level 10 cantrips deal 2 dice. Reliable Talent enforces minimum roll. TempHP boosts apply.

---

### WP-M07: Cloud System

**Items:** CL-4, CL-5, CL-6, CL-7, CL-8, CL-9, CL-10
**Files:** `Combat/Environment/SurfaceManager.cs`
**Dependencies:** WP-C01 (Obscurity system must exist first)

1. **No cloud vs. surface architecture** — both are `SurfaceDefinition`; surface interactions can incorrectly transform clouds. Clouds should hover above ground, separate from surfaces.
2. **Cloudkill damage wrong** — flat 5 instead of 5d8.
3. **Steam Cloud missing Wet** — `steam` surface has `obscure` tag but no `AppliesStatusId = "wet"`.
4. **Electrified Steam missing Wet** — same issue.
5. **No Gust of Wind cloud removal** — no mechanic to disperse clouds.
6. **Haste Spores missing** — should grant +2 AC, ×2 movement, extra Action.
7. **Pacifying Spores missing** — should suppress all action types.

**Acceptance:** Clouds are architecturally distinct from ground surfaces. Cloudkill deals 5d8 Poison. Steam applies Wet. Gust of Wind removes clouds.

---

### WP-M08: Surface Existing Fixes

**Items:** S-3, S-4, S-5, S-6, S-7
**Files:** `Combat/Environment/SurfaceManager.cs`
**Dependencies:** WP-C02 (save architecture)

1. **Fire damage flat 5 → 1d4** — `SurfaceDefinition.DamagePerTrigger` is a flat float but should roll dice.
2. **Acid doesn't reduce AC** — BG3 Acid = −2 AC. Currently only deals flat damage.
3. **Dip mechanic absent** — BG3 allows dipping weapons into Fire/Hellfire/Simple Toxin for coating.
4. **Steam missing Wet** — no `AppliesStatusId = "wet"`.
5. **Oil + Fire interaction broken** — `CheckInteractions` only fires on new surface creation, so Fire cast into existing Oil won't trigger.

**Acceptance:** Fire surface deals 1d4. Acid reduces AC by 2. Oil ignites when Fire is cast on it. Steam applies Wet.

---

### WP-M09: Missing Surfaces

**Items:** DT-3, surface table from §14
**Files:** `Combat/Environment/SurfaceManager.cs`
**Dependencies:** WP-C02 (save architecture), WP-M08 (surface system improvements)

Add missing surfaces that create Difficult Terrain or apply conditions:

| Surface | Effect |
|---|---|
| `lava` | 10d6 Fire/turn + Difficult Terrain |
| `mud` | Difficult Terrain |
| `black_tentacles` | 3d6 Bludgeoning/turn + Difficult Terrain |
| `hellfire` | 6d6 Fire/turn, Dippable |
| `sewage` | Prone + Difficult Terrain |
| `shadow_cursed_vines` | Immobilise + 1d4 Necrotic + DT |
| `twisting_vines` | Entangled + Difficult Terrain |

**Acceptance:** All listed surfaces registered with correct damage, conditions, and terrain multipliers.

---

### WP-M10: Movement System

**Items:** MS-1, MS-2, MS-4, MS-5, DT-4, DT-5
**Files:** `Combat/Actions/ActionBudget.cs`, `Data/Races/`, `Combat/Movement/MovementService.cs`, `Combat/Movement/JumpPathfinder3D.cs`, `Combat/Movement/SpecialMovementService.cs`
**Dependencies:** None

1. **Unit ambiguity** — `DefaultMaxMovement = 30f` suggests feet, BG3 uses meters (9m=30ft). Audit all race data for consistency.
2. **Race-specific speeds not verified** — whether Dwarf/Halfling/Gnome return 25ft and Wood Elf returns 35ft. Audit race data pipeline.
3. **Frightened movement check order** — checked after budget-exhausted path, meaning error messages could be wrong.
4. **Mobile feat + Dash terrain ignore** checks `HasStatus("dashing")` — Rogue Cunning Action: Dash may have a different status tag.
5. **Jump cost in difficult terrain** — `JumpPathfinder3D.cs` / `SpecialMovementService.cs` doesn't apply surface cost multiplier to jump cost. BG3: jumping in DT also costs double.
6. **Movement-expending weapon actions** not blocked in DT — BG3 Brace (Ranged/Melee) unavailable in difficult terrain.

**Acceptance:** All races have correct base speeds. Jumping costs double in DT. Mobile feat works with Cunning Action Dash.

---

### WP-M11: Weapons

**Items:** W-2, W-3, W-4, W-5, W-6, W-7
**Files:** `Data/CharacterModel/EquipmentDefinition.cs`, `Combat/Actions/EffectPipeline.cs`, `Combat/Actions/Effects/Effect.cs`, `Combat/Services/ActionExecutionService.cs`, `Combat/Rules/RulesEngine.cs`
**Dependencies:** WP-M08 (Dip mechanic)

1. **Dippable property** — absent from `WeaponProperty` enum and `WeaponDefinition`.
2. **Two-weapon fighting offhand damage** — unclear if ability modifier is correctly suppressed for offhand and only restored with Two-Weapon Fighting style.
3. **Loading property (crossbows)** — defined in enum but enforcement unclear. Can only fire once per action; Extra Attack shouldn't apply to Loading weapons without Crossbow Expert.
4. **Weapon Action proficiency gate** — Lacerate, Concussive Smash etc. should only be available to proficient wielders. Not validated.
5. **Heavy property** — should impose Disadvantage for Small/Tiny creatures. Not in rules engine.
6. **Thrown weapons** — BG3 thrown weapons return after throwing. Not implemented.

**Acceptance:** Offhand attacks don't add ability mod without TWF style. Loading crossbows can only fire once per action without Crossbow Expert. Weapon actions require proficiency.

---

### WP-M12: Healing Pipeline

**Items:** H-2, H-3, H-6
**Files:** `Combat/Actions/Effects/Effect.cs`, `Data/Actions/consumable_items.json`, `Data/Actions/`
**Dependencies:** None

1. **Bleeding not removed on heal** — `HealEffect` only removes Prone on revive, not Bleeding. BG3: ANY healing removes Downed AND Bleeding.
2. **Healing potions don't remove Burning** — no condition cleanup in potion actions.
3. **Limited healing spell roster** — Healing Word, Mass Cure Wounds, Prayer of Healing, Heal (70 HP fixed), Aid, Life Transference all missing.

**Acceptance:** Any healing removes Bleeding. Drinking healing potion removes Burning. At least Healing Word and Mass Cure Wounds added.

---

### WP-M13: Resources

**Items:** R-2, R-3, R-5
**Files:** `Data/ActionResources/ActionResourceType.cs`, `Data/ActionResources/`
**Dependencies:** None

1. **Missing resource types** — `BladeSongPower`, `StarMap`, `CosmicOmen`, `WarPriestCharge`, `FungalInfestationCharge`, `ArcaneArrow`, `LuckPoint`.
2. **Bardic Inspiration die size not level-scaled** — d6→d8 at L5→d10 at L10. Progression logic absent despite `DiceType` field existing.
3. **Missing hidden resources** — `LegendaryResistance` and `DeflectMissiles` absent from enum.

**Acceptance:** All listed resource types added. Bardic Inspiration scales d6→d8→d10 correctly.

---

### WP-M14: Classes & Subclasses

**Items:** CL-5, RV-M14, SP-4, RV-M13
**Files:** `Data/Classes/*.json`, `Data/CharacterModel/CharacterResolver.cs`
**Dependencies:** WP-C04 (multiclass resolver)

1. **Wild Shape beast options not defined. Rage bonus damage not differentiated per subclass.**
2. **12 non-BG3 subclasses in data** — `arcane_archer`, `path_of_the_giant`, `drunken_master`, `swashbuckler`, `death` (Cleric), `crown` (Paladin), `stars` (Druid), `swarmkeeper` (Ranger), `bladesinging` (Wizard), `shadow_magic` (Sorcerer), `hexblade` (Warlock), `glamour` (Bard). These should be removed or flagged.
3. **EK/AT school restrictions not enforced** at spell learning — no `allowedSchools` field.
4. **Paladin missing `UsesPreparedSpells`** — defaults to false, goes through known-spell path instead of prepared-spell path (`classLevel + CHA mod`).

**Acceptance:** Non-BG3 subclasses removed/flagged. EK restricted to Abjuration/Evocation (+ unrestricted at certain levels). Paladin uses prepared spells. Wild Shape has at least basic beast options.

---

### WP-M15: Paladin Fixes

**Items:** RV-M4, RV-M13
**Files:** `Data/Statuses/bg3_mechanics_statuses.json`, `Combat/Services/TurnLifecycleService.cs`, `Data/Classes/divine_classes.json`
**Dependencies:** None

1. **Aura of Protection — wrong value + no ally propagation** — hardcodes `"value": 3`. BG3: bonus = Paladin's CHA modifier. Aura status never applied to nearby allies.
2. **`UsesPreparedSpells` missing** on Paladin (covered in WP-M14 but listed here for Paladin-specific context).

**Acceptance:** Aura of Protection scales with CHA mod and applies to all allies within 3m (10ft).

---

### WP-M16: Races

**Items:** RC-2, RC-3, RC-5
**Files:** `Data/Races/core_races.json`, `Data/Races/exotic_races.json`, `Combat/Rules/RulesEngine.cs`
**Dependencies:** None

1. **Human Versatility missing** — no Civil Militia proficiency (light armor, shields, pikes, spears, halberds, glaives), no free skill choice.
2. **Githyanki Astral Knowledge** — action exists but temporary skill proficiency grant/expiry not coded.
3. **Forest Gnome Speak with Animals** — frequency (at-will vs per-day) needs verification against BG3.

**Acceptance:** Humans get Civil Militia proficiencies. Githyanki Astral Knowledge grants temporary skill proficiency.

---

### WP-M17: Consumables

**Items:** C-4, C-5, C-6, C-8
**Files:** `Data/Actions/consumable_items.json`
**Dependencies:** WP-M08 (Dip/coating system)

1. **No Coatings system** — weapon-applied consumable buffs.
2. **No Arrow consumables** — for ranged characters.
3. **Very few scrolls** — 2 vs dozens in BG3.
4. **Limited Grenades variety.**

**Acceptance:** Basic coating system works. At least 10 scrolls added (covering common combat spells). Arrow types added.

---

### WP-M18: Feats

**Items:** F-5, F-9
**Files:** `Data/Feats/bg3_feats.json`, `Data/CharacterModel/CharacterResolver.cs`
**Dependencies:** None

1. **Martial Adept** — gives 1 `superiority_dice` resource but no mechanism to select 2 maneuvers from Battle Master list.
2. **Weapon Master** — no `ApplyFeatChoices` case. 4 weapon proficiencies never granted.

**Acceptance:** Martial Adept grants 2 selectable maneuvers. Weapon Master grants 4 weapon proficiencies.

---

### WP-M19: Obscurity Extensions

**Items:** OB-5, OB-6, OB-7
**Files:** `Combat/Rules/RulesEngine.cs`, `Combat/Environment/LOSService.cs`
**Dependencies:** WP-C01 (tier system must exist first)

1. **No Darkvision mechanic** — no code reduces obscurity tier for Darkvision holders. Race traits list it but it has no effect.
2. **Hide + Obscurity integration** — Hide action doesn't interact with current obscurity zone.
3. **Stealth vs. Passive Perception** — no such check exists for Lightly Obscured zones.

**Acceptance:** Darkvision reduces Heavily Obscured to Lightly Obscured. Hide in Lightly Obscured triggers Stealth vs. Passive Perception.

---

### WP-M20: Difficult Terrain Extensions

**Items:** DT-4, DT-5
**Files:** `Combat/Movement/JumpPathfinder3D.cs`, `Combat/Movement/SpecialMovementService.cs`, `Combat/Actions/`
**Dependencies:** None

1. **Jump cost in difficult terrain** — jumping in/from DT should also cost double.
2. **Movement-expending weapon actions** — BG3 Brace (Ranged/Melee) unavailable in difficult terrain. No such check exists.

**Acceptance:** Jump from DT costs double. Movement-based weapon actions blocked in DT.

---

## Phase 3: MINOR Items

Lower priority polish items. Tackle after all CRITICAL and MAJOR work packages are complete.

### Actions
| ID | Gap | File |
|---|---|---|
| A-1 | Action Surge has no `ActionSurgeUsed` state or temporary charge cap — only 1 Action tracked | `Combat/Actions/ActionBudget.cs` |
| A-2 | Reaction trigger coverage limited to 8 types; BG3 spell-specific triggers (War Caster "targeted while concentrating") missing | `Combat/Reactions/ReactionTrigger.cs` |
| A-3 | BG3 bars reactions from: concentration saves, death saves, saves to end conditions early, saves/damage inside AoE conditions | `Combat/Reactions/` |

### Movement
| ID | Gap | File |
|---|---|---|
| MS-4 | Frightened movement check order — checked after budget-exhausted, wrong error messages | `Combat/Movement/MovementService.cs` |
| MS-5 | Mobile feat Dash terrain ignore checks `HasStatus("dashing")` — Cunning Action Dash may have different tag | `Combat/Movement/MovementService.cs` |

### Resources
| ID | Gap | File |
|---|---|---|
| R-5 | `LegendaryResistance` and `DeflectMissiles` hidden resources absent from enum | `Data/ActionResources/ActionResourceType.cs` |

### Abilities
| ID | Gap | File |
|---|---|---|
| AB-1 | Death saves don't respect general save bonuses (Bless, Guidance) — raw d20 | `Combat/Services/TurnLifecycleService.cs` |

### Attacks
| ID | Gap | File |
|---|---|---|
| AT-2 | Armor DEX bonus floors at 0 — negative DEX should reduce AC in light/medium armor | `Combat/Services/InventoryService.cs` |
| AT-3 | Crit threshold only reduces to 19 — no stacking below 19 from multiple features | `Combat/Actions/EffectPipeline.cs` |

### Damage
| ID | Gap | File |
|---|---|---|
| D-2 | Resist+Vuln cancel works mathematically but not mechanically explicit — fix after D-1 | `Combat/Rules/DamagePipeline.cs` |
| D-3 | Multi-damage-type effects — per-type pipeline separation depends on call-site discipline | `Combat/Actions/EffectPipeline.cs` |
| D-4 | Cantrip scaling uses total character level (correct) — confirm not class level | `Combat/Actions/Effects/Effect.cs` |

### Saving Throws
| ID | Gap | File |
|---|---|---|
| ST-3 | Weapon action `SaveDCBonus` (+2 inherent) — audit each weapon action in JSON | `Data/Actions/bg3_mechanics_actions.json` |

### Equipment
| ID | Gap | File |
|---|---|---|
| EQ-6 | Heavy armor STR requirement — `StrengthRequirement` field exists but −10ft speed penalty not enforced | `Combat/Services/InventoryService.cs` |

### Conditions
| ID | Gap | File |
|---|---|---|
| CO-6 | Stack ID system not present — two same-ID conditions can coexist | `Combat/Statuses/StatusSystem.cs` |
| CO-7 | Tick type only EndTurn — BG3 default is StartTurn | `Combat/Statuses/StatusSystem.cs` |
| CO-8 | ~19 BG3 status properties not modeled (IsInvulnerable, LoseControl, FreezeDuration, etc.) | `Combat/Statuses/` |

### Feats
| ID | Gap | File |
|---|---|---|
| F-10 | Alert — no Surprise round mechanic to suppress | — |
| F-11 | Crossbow Expert — BG3 also grants Wounding Shot variant | — |
| F-12 | Durable — `durable_healing_floor` tag enforcement unverified | — |
| F-13 | Heavy Armour Master — +1 STR not in feature; −3 DR enforcement unverified | — |
| F-14 | Mobile — OA avoidance system enforcement unverified | — |
| F-15 | Shield Master — Passive +2 DEX save with shield unverified | — |
| F-16 | Tavern Brawler — STR mod doubled on damage; verify not just added once | — |

### Passives
| ID | Gap | File |
|---|---|---|
| P-3 | Class feature delivery via Feature-tags/GrantedAbilities vs. PassiveRegistry — two parallel systems, no single list | — |
| P-4 | UI toggle integration for Passive.txt entries (Non-Lethal, etc.) unverified | — |
| P-5 | Race passive tag enforcement — `lucky_reroll` tag present in test but production path unverified | — |

### Spells
| ID | Gap | File |
|---|---|---|
| SP-6 | Divination school effectively empty (0 tagged spells) | — |

### Classes
| ID | Gap | File |
|---|---|---|
| CL-6 | ASI/feat grant at `FeatLevels` — path exists but integration test coverage absent | `Data/CharacterModel/CharacterResolver.cs` |

### Backgrounds
| ID | Gap | File |
|---|---|---|
| BG-3 | Skill proficiency string casing inconsistency across systems | `Data/Backgrounds/BackgroundData.cs` |

### Races
| ID | Gap | File |
|---|---|---|
| RC-6 | Rock Gnome Artificer's Lore expertise not verified | — |
| RC-7 | Duergar darkvision field inconsistency (body 12m vs override 24m) | `Data/Races/exotic_races.json` |
| RC-8 | Halfling Brave (Frightened immunity) — verify tag + ConditionEffects suppression | — |

### Surfaces
| ID | Gap | File |
|---|---|---|
| S-8 | Fly avoidance not implemented — Fly bypasses surface effects | `Combat/Environment/SurfaceManager.cs` |

### Surfaces — Low-Priority Missing
| Surface | Effect |
|---|---|
| `alcohol` | → Fire (fire dmg), → Ice (cold dmg) |
| `ash` | No effect |
| `blood` | → Ice (cold dmg) |
| `caustic_brine` | 1d4 Acid/turn |
| `holy_fire` | 1d4 Radiant/turn |
| `mind_flayer_blood` | → Ice (cold) |
| `purple_worm_poison` | 1d10 Poison/turn |
| `serpent_venom` | 1d6 Poison/turn |
| `simple_toxin` | 1d4 Poison/turn, Dippable |

### Clouds — Missing Types
| ID | Gap |
|---|---|
| CL-11 | Missing: Crawler Mucus, Drow Poison, Ice Cloud, Malice, Noxious Fumes, Strange Gas, Timmask Spores |

### Obscurity
| ID | Gap | File |
|---|---|---|
| OB-8 | Equipment obscurity bonuses ("+1 AC while obscured") untriggerable since zones not implemented | — |

### Cross-Cutting
| ID | Gap |
|---|---|
| CC-4 | Skill proficiency strings use inconsistent casing across systems |

---

## Dependency Graph

```
WP-C01 (Obscurity)
  └─► WP-M07 (Clouds)
  └─► WP-M19 (Obscurity Extensions)

WP-C02 (Surface Save Architecture)
  └─► WP-M08 (Surface Fixes)
       └─► WP-M09 (Missing Surfaces)
       └─► WP-M11 (Weapons/Dip)
            └─► WP-M17 (Consumables/Coatings)

WP-C04 (EK/AT Caster Levels)
  └─► WP-M14 (Classes & Subclasses)

All other WPs: No dependencies — can be worked in parallel.
```

---

## Quick Reference: Item Count by Phase

| Phase | Count |
|---|---|
| 🔴 CRITICAL | 13 items across 10 work packages |
| 🟠 MAJOR | ~55 items across 20 work packages |
| 🟡 MINOR | ~30 items (flat list) |
| **Total** | **~98 actionable items** |
