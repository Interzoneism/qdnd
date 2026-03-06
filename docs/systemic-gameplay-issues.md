Deep Spell Audit: 5 Complex BG3 Spells
1. HOLD PERSON
What it should do: L2 Enchantment, WIS save, Paralyzed (can't act, auto-fail STR/DEX saves, attackers have advantage, melee hits auto-crit within 3m), repeat WIS save at end of each turn. Concentration. Humanoid-only. Upcast: +1 target per level.

What works
WIS saving throw with correct DC formula (8 + prof + ability mod)
Repeat saves at end of target's turn with caster's actual spell DC
Concentration tracking — breaking concentration removes the status
BG3 data parsed correctly from Spell_Target.txt
Critical bugs
#	Bug	Impact
1	Humanoid restriction not enforced — Tagged('HUMANOID') stored in BG3TargetConditions but never evaluated at runtime	Hold Person can target undead, constructs, fiends
2	BG3-path status hold_person not recognized as incapacitating — ConditionEffects.IsIncapacitating("hold_person") returns false	Paralyzed targets still take normal turns
3	Actions not blocked — BG3 StatusType=INCAPACITATED isn't mapped to BlockedActions	Paralyzed target can still act and react
4	No attacker advantage — Advantage(AttackTarget) boost format not parsed by BG3StatusIntegration	Attacks against paralyzed targets don't get advantage
5	No melee auto-crit — CriticalHit(AttackTarget,Success,Always,3) boost not parsed	Melee hits within range aren't auto-crits
6	Target concentration not broken — hold_person not in ExplicitIncapacitatingStatusIds and has no hard_control tag	Paralyzed casters keep concentrating
7	Melee auto-crit range wrong — MeleeAutocritRangeMeters = 1.5f vs BG3's 3m	Even for JSON paralyzed path, range is half correct
8	BG3-path upcast broken — SpellUpcastRules["hold_person"] doesn't match "Target_HoldPerson"	No extra targets when upcasting
Root cause: Two parallel definitions exist — BG3-parsed hold_person (incomplete boost parsing) and JSON paralyzed (missing BlockDexFromAC). Neither is fully correct alone.

2. FIREBALL
What it should do: L3 Evocation, 8d6 fire, 4m radius AoE, DEX save for half, ignites surfaces, +1d6 per upcast level.

What works
AoE targeting with AoECircleMode, correct 4m radius from BG3 data
Per-target DEX saves with correct DC
Fire resistance/vulnerability/immunity via DamagePipeline
Evasion (Rogue/Monk) works correctly
Line of effect from explosion center to targets
Surface ignite/melt effects fire (grease → fire, etc.)
Critical bugs
#	Bug	Impact
1	Half-damage on save BROKEN — CleanDiceFormula discards /2 from (8d6)/2; SaveTakesHalf flag never triggers for on_save_success condition	Targets who succeed DEX save take FULL 8d6 instead of 4d6
2	Upcast scaling missing for BG3 entry — SpellUpcastRules["fireball"] doesn't match "Projectile_Fireball"	Casting at 4th-9th level adds zero extra damage
3	Surface change radius too small — default 2.5m instead of 4m explosion radius	Only inner half of explosion ignites surfaces
4	No fire surface created — ExplodeRadius field never generates a CreateSurface(fire) effect	No persistent fire hazard after Fireball
5	Sculpt Spells non-functional — BG3TargetConditions stored but never evaluated in ResolveAreaTargets	Evocation Wizard allies always take full AoE damage
Bug #1 is especially severe — it means every successful DEX save against Fireball results in full damage rather than half. This affects every AoE save spell that uses the same SpellFail = "DealDamage((XdY)/2,..." pattern.

3. SPIRIT GUARDIANS
What it should do: L3 Conjuration, Concentration, 4.5m aura around caster, enemies take 3d8 radiant/necrotic (WIS save for half) on entry and at start of their turn. Half movement speed in aura. +1d8 per upcast level.

What works
Concentration tracking — start/break handled correctly
JSON status definitions exist for both caster buff and enemy debuff
FATAL: The spell is a complete no-op
#	Bug	Severity
1	spirit_guardians status missing auraRadius/auraStatusId — AuraSystem filter s.Definition.AuraRadius > 0f silently drops it	FATAL — entire mechanic dead
2	No movement-entry detection — MovementService has zero aura mentions	Enemies walking into aura take no damage
3	AuraSystem processes at turn END, not turn START — BG3 damages at start of enemy's turn	Timing wrong even if wired
4	No necrotic variant — only radiant child status exists	No damage type choice
5	Wrong status ID applied by action — spirit_guardians instead of the aura emitter status	Even if aura data were fixed, wrong status applied
6	Upcast rule key mismatch — "spirit_guardians" vs action ID "spirit_guardians_radiant"	Upcast never scales damage
7	No double-damage prevention marker	Could be hit on entry AND turn start in same turn
What actually happens today: Cleric casts Spirit Guardians → status applied to caster → AuraSystem silently drops it → enemies take zero damage, have full movement speed. The spell does nothing except occupy the concentration slot.

4. COUNTERSPELL
What it should do: Reaction, L3 Abjuration, 18m range. Auto-cancels spells of level ≤ slot used. Higher-level spells: Intelligence check DC = 10 + target spell level.

What works
Reaction registration with SpellCastNearby trigger, 18m range
Spell slot requirement check (needs ≥ L3 slot)
Player gets a reaction prompt when enemy casts
CounterEffect correctly sets WasCancelled = true → target spell never executes
AI has score-based heuristic for when to counter
Concentration interaction correct (Counterspell isn't concentration, doesn't break existing)
Critical gaps
#	Bug	Impact
1	No spell level comparison — always auto-succeeds regardless of relative levels	Counterspell at L3 slot auto-cancels L9 spells
2	No ability check — TryCounterspellHigherLevel has no QDND equivalent	No roll when countering higher-level spells
3	Upcast threshold not tracked — counterspellerLevel hardcoded to 3	Upcasting Counterspell provides zero benefit
4	UI shows no spell level — player can't make informed upcast decisions	Missing information in reaction prompt
5	AI has no slot-level awareness — doesn't consider probability of success	AI wastes slots on uncounterable spells
The FunctorExecutor.Counterspell stub is harmless — it's a dead code path since the real execution goes through CounterEffect.

5. HASTE
What it should do: L3 Transmutation, Concentration, target ally. Grants: +2 AC, advantage on DEX saves, doubled movement, +1 action per turn. On end: Lethargy (can't move or act for 1 turn).

What works
+2 AC via modifier stack
Movement speed doubled (100% increase modifier)
DEX save advantage via conditional modifier
Concentration fully wired (start, break, damage saves)
Lethargy triggers correctly on Haste removal (dual-path: JSON triggerEffects + StatusInteractionRules)
Lethargy blocks all actions via BlockedActions
No upcast scaling (correct — Haste doesn't scale)
TargetFilter: "Self, Allies" correctly restricts targets
Critical bugs
#	Bug	Impact
1	Extra action NOT GRANTED — ActionBudget.ResetForTurn() hardcodes _actionCharges = 1, never reads ModifierTarget.ActionPoints	The core benefit of Haste (extra action) doesn't work
2	BG3 HASTE boost parse gaps — ActionResource(Movement/ActionPoint) and Advantage(SavingThrow,Dexterity) silently dropped	Potion of Speed path only gets +2 AC
3	Lethargic not in IsIncapacitating() — turn not auto-skipped	AI may freeze/loop during lethargic turn
4	Duplicate lethargy trigger — both JSON and C# paths fire, guarded only by HasStatus() check	Fragile
5	Status ID split — spell uses "hasted", Potion of Speed uses "haste"	Changes must be made in two places
Systemic Issues Revealed
The 5 spell audits reveal four deep structural problems that affect far more than just these spells:

1. BG3 ID / JSON ID Mismatch (affects ALL BG3-parsed spells)
SpellUpcastRules is keyed to short names ("fireball", "hold_person", "spirit_guardians") but BG3-parsed actions use prefixed IDs ("Projectile_Fireball", "Target_HoldPerson"). The lookup always fails for BG3 entries, falling back to a heuristic that often produces wrong scaling. This means upcast scaling is broken for most BG3-parsed spells — not just Fireball and Hold Person.

2. Save-for-Half Damage Broken System-Wide
The CleanDiceFormula function silently discards /2 divisors from BG3's SpellFail = "DealDamage((XdY)/2,...)" pattern. The SaveTakesHalf flag is set but the condition check (on_save_fail vs on_save_success) prevents it from ever activating. Every AoE save-for-half spell in the game likely deals full damage on successful saves. This affects Fireball, Lightning Bolt, Cone of Cold, Ice Storm, Shatter, and dozens more.

3. BG3StatusIntegration Boost Parser Is Incomplete
BG3StatusIntegration.ParseBoosts() is a separate, simpler parser than BoostParser.ParseBoostString(). It fails on:

Multi-param boosts: Advantage(SavingThrow, Dexterity) — two-arg format doesn't match one-arg regex
Advantage(AttackTarget) / CriticalHit(AttackTarget,...) — "attacktarget" not in ParseModifierTarget switch
ActionResource(Movement/ActionPoint,N,0) — no handler at all
This means any BG3 status that uses these boost formats has silently incomplete effects. Paralyzed, Haste, and likely many other conditions are affected.

4. BG3TargetConditions Never Evaluated at Runtime
action.BG3TargetConditions (containing creature type restrictions like Tagged('HUMANOID') and feature interactions like HasPassive('SculptSpells')) is stored but never read by any targeting or effect system. This means:

Hold Person can target non-humanoids
Sculpt Spells (Evocation Wizard) is non-functional for all AoE spells
Any spell with creature type restrictions (Hold Monster vs Hold Person, etc.) has no enforcement
Priority Ranking of Systemic Fixes
Priority	Systemic Issue	Spells Affected	Fix Scope
P0	Save-for-half damage broken	Every AoE save spell (~30+)	DealDamageEffect.cs condition check logic
P0	SpellUpcastRules ID mismatch	Every BG3-parsed spell with upcast	ID normalization in BG3ActionConverter or SpellUpcastRules
P1	BG3StatusIntegration boost parser gaps	Hold Person, Haste, and ~50+ BG3 statuses	Extend or replace ParseBoosts() with BoostParser
P1	BG3TargetConditions not evaluated	Hold Person, Sculpt Spells, creature-type spells	Wire ConditionEvaluator into TargetValidator
P2	Spirit Guardians completely non-functional	Spirit Guardians	Wire aura data, add movement entry detection
P2	Counterspell no level check	Counterspell	Add level comparison + ability check
P2	Haste extra action not granted	Haste	Wire ModifierTarget.ActionPoints into TurnLifecycleService
Completed: Synthesize findings & systemic issues (7/7)

Bottom line: Of the 5 spells audited, Spirit Guardians is completely non-functional, Haste is missing its core benefit (extra action), Fireball deals full damage to targets who succeed their save, Hold Person's BG3-path doesn't actually paralyze (turn still taken, actions not blocked), and Counterspell auto-succeeds against any spell level. The two most damaging systemic issues — save-for-half broken and upcast ID mismatch — likely affect 30-50+ spells beyond just these five.

---

# Deep Spell Audit: 5 Non-Damage BG3 Spells (Batch 2)

## 6. BLESS
What it should do: L1 Enchantment, Concentration, 9m range. Up to 3 allies gain +1d4 to all attack rolls and saving throws. Upcast: +1 target per spell level above 1st.

### What works
- Concentration tracking — breaking concentration removes the buff
- Status applies a roll modifier to attack rolls and saving throws
- Multi-target ally targeting works (JSON path)
- BG3 data parsed from Spell_Target.txt
- Death save interaction — Bless bonus applies to death saving throws ✓

### Critical bugs
| # | Bug | Impact |
|---|---|---|
| 1 | Range 2× too wide — JSON has `"range": 18` but BG3 is 9m (30ft) | Bless can be cast from double the intended range |
| 2 | Upcast target scaling broken — JSON uses `"targetIncrease"` field but EffectPipeline reads `"targetsPerLevel"` from SpellUpcastRules; no SpellUpcastRules entry for "bless" exists | Upcasting Bless at higher slots provides zero extra targets |
| 3 | BG3-path `Target_Bless` single-target — `MapSpellTypeToTargetType()` returns SingleUnit; BG3's `AmountOfTargets` field never parsed | BG3-parsed Bless targets 1 creature instead of 3 |
| 4 | JSON status modifier is flat +2, not 1d4 — `"type": "flat", "value": 2` instead of `"type": "dice", "diceFormula": "1d4"` | Bonus is deterministic +2 instead of stochastic 1d4 (wrong average AND wrong variance) |

Root cause: Bug #2 is a systemic JSON schema mismatch — 4 actions in the JSON files use `"targetIncrease"` but the upcast system reads `"targetsPerLevel"` from SpellUpcastRules. Only hold_person is compensated by having a manual SpellUpcastRules entry. Bug #3 is part of the systemic issue where `AmountOfTargets` from BG3 data is never parsed into `MaxTargets`.

## 7. MISTY STEP
What it should do: L2 Conjuration, Bonus Action, 18m range. Teleport to an unoccupied visible space. No concentration. Bypasses opportunity attacks, difficult terrain, restraints.

### What works
- Bonus action cost correctly parsed (`UseCosts = "BonusActionPoint:1"`)
- Level 2 spell slot consumed
- No concentration flag
- Verbal component check (blocked while silenced)
- 18m range enforced
- `ForcedMovementService.Teleport()` correctly bypasses OA, difficult terrain, and movement budget
- Surface interactions at destination (fire, ice, etc.)
- Fall damage on height difference
- Counterspell reaction trigger fires before execution
- Scroll of Misty Step works correctly (hand-authored with `"targetType": "point"`)

### Critical bugs
| # | Bug | Impact |
|---|---|---|
| 1 | Wrong TargetType: entity-picker instead of ground-picker — `BG3SpellType.Target` maps to `TargetType.SingleUnit`, ignoring `TargetConditions = "not Character()"` which signals ground targeting | Player must click an entity, cannot click empty ground. **Spell is fundamentally unusable as intended** |
| 2 | `ExecuteAbilityAtPosition` guard rejects SingleUnit — the position-execution guard only whitelists Circle/Cone/Line/Point/Charge/WallSegment | Even if ground-click were routed, execution silently returns with zero effect |
| 3 | TeleportEffect fails: "No target position" — when triggered via entity-picker, `TargetPosition` is null → effect returns Failed | Spell expends bonus action + spell slot with zero teleportation |
| 4 | No unoccupied-space validation — `BG3TargetConditions = "CanStand('') and not Character()"` stored but never evaluated | When fixed, teleporting into occupied tiles won't be prevented |

Root cause: `MapSpellTypeToTargetType()` ignores `TargetConditions`. BG3's `"not Character() and not Self()"` pattern is the canonical signal that a Target-type spell targets empty ground rather than an entity. This affects any BG3-parsed self-repositioning spell (Thunder Step, Far Step, etc.). The Scroll works because it was hand-authored with `"targetType": "point"`.

**FATAL**: Misty Step is completely non-functional via the BG3-parsed path. The spell takes your bonus action and spell slot, then does nothing.

## 8. ENTANGLE
What it should do: L1 Conjuration, Concentration, 18m range, 3m radius AoE. Creates grasping vines — creatures make STR save or become Restrained. Area is difficult terrain. End-of-turn STR save to escape. Fire destroys the vines.

### What works
- Action definition exists with Concentration, STR save, level 1
- SpawnSurfaceEffect is functional (NOT a stub — contrary to what might be expected)
- Surface definition exists with `MovementCostMultiplier = 2f` (difficult terrain)
- Restrained mechanics correct: speed 0, disadvantage on attacks, advantage to attackers, disadvantage on DEX saves
- "entangled" status registered with "restrained" tag → ConditionEffects fires correctly via tag expansion
- Concentration break removes the surface
- Initial cast save uses real DC

### Critical bugs
| # | Bug | Impact |
|---|---|---|
| 1 | AoE radius is DOUBLE — JSON has `"areaRadius": 6` but BG3 is 3m | Zone covers **4× the correct area** (113m² vs 28m²). Massively overpowered |
| 2 | Range 50% too large — JSON has `"range": 27` but BG3 is 18m | Castable from 27m instead of 18m |
| 3 | Surface save DC is hardcoded to 12 — `SurfaceManager.cs` line 1658: `entangle.SaveDC = 12` | DC doesn't scale with caster level. A Druid 7 (DC 15) uses DC 12 |
| 4 | No per-turn escape save — "entangled" status has no RepeatSave/RemoveConditions; surface retriggers at start of next turn instead of end-of-turn STR save | Creatures can never actively try to escape; timing is wrong (start-of-turn re-entry vs end-of-turn escape) |
| 5 | Fire does not destroy vines — surface has no `EventReactions` dictionary | Major tactical interaction missing (Fireball + Entangle combo doesn't work) |
| 6 | "entangled" missing from ConditionEffects alias map — currently works only via tag expansion | Fragile; any code doing direct `HasStatus()` lookup for Restrained will miss it |

Root cause: Bug #3 is a systemic issue — `SurfaceDefinition.SaveDC` is static, there's no mechanism to inject the caster's spell DC into the surface instance at creation time. This affects ALL concentration surface spells (Spike Growth, Plant Growth, etc.). Bug #4 reveals the same per-turn-save gap found in Blindness (see below).

## 9. MIRROR IMAGE
What it should do: L2 Illusion, Self-target, **No concentration**, 10 rounds. Creates 3 duplicates. BG3 implementation: each duplicate grants +3 AC. When an attack misses (because of the AC bonus), one duplicate is destroyed. 3 duplicates = +9 AC, 2 = +6, 1 = +3.

### What works
- Spell registered correctly: L2 Illusion, self-target
- No concentration (can coexist with other concentration spells)
- Duration 10 turns
- Available on correct class lists (Wizard, Sorcerer, Arcane Trickster, Eldritch Knight, Trickery Domain)
- Spell slot consumed

### Critical bugs
| # | Bug | Impact |
|---|---|---|
| 1 | **Duplicate destruction mechanic entirely absent** — spell starts at 3 stacks but stacks NEVER decrement; no "on attacked" trigger exists | Mirror Image becomes a permanent +AC buff for 10 rounds regardless of incoming attacks. **Core mechanic missing** |
| 2 | Wrong AC bonus per duplicate — `value: 2, valuePerStack: 2` gives +6 at 3 stacks; BG3 is `AC(3)` per duplicate = +9 | AC bonus is 33% too low even as a static buff |
| 3 | `RemoveEvents: "OnAttacked"` silently dropped by BG3StatusIntegration — only OnTurn/OnMove/OnDamage are handled | Any BG3 status with "remove when attacked" is silently broken |
| 4 | `IsMiss()` in `RemoveConditions` not parsed — regex only matches SavingThrow pattern | Even if OnAttacked were wired, removal would fire on every attack (hits AND misses) instead of misses only |

Root cause: The StatusSystem was designed for statuses that respond to the bearer's own actions (OnAttack, OnCast, OnMove) and damage-to-bearer (OnDamageTaken). There is **no infrastructure for "fire when the bearer is the target of an attack roll"**. `StatusTriggerType` has no `OnAttackedAsTarget` entry. `AttackResolved` is subscribed in `SubscribeToEvents()` but `MapEventToTriggerType` maps it to `null` — the event fires but triggers nothing.

**STATUS**: Mirror Image is a slightly-too-weak permanent AC buff for 10 rounds. The iconic "duplicates absorb misses" mechanic does not exist at any level.

## 10. BLINDNESS
What it should do: L2 Necromancy, 9m range, CON save. **No concentration!** Target is Blinded for 10 rounds. End-of-turn CON save to end early. Upcast: +1 target per level above 2nd.

### What works
- CON save on initial cast
- No concentration (correctly not set)
- 9m range, enemies-only targeting
- Blinded condition effects work via dual-path:
  - Disadvantage on blinded attacker's rolls ✓
  - Advantage on attacks against blinded target ✓
  - Ranged attack range clamped to 3m while blinded ✓

### Critical bugs
| # | Bug | Impact |
|---|---|---|
| 1 | Wrong status applied: `"blinded"` (BLINDED) instead of `"BLINDNESS"` — BLINDED is the generic condition with no RepeatSave; BLINDNESS is the spell-specific status with end-of-turn CON save | **Target can NEVER save out of blindness**. The blind is permanent for the status duration |
| 2 | Duration wrong: 3 turns instead of 10 — BG3 data says `ApplyStatus(BLINDNESS,100,10)` | Combined with Bug #1: permanently blinded for 3 turns (should be 10 turns with end-of-turn saves) |
| 3 | No upcast targets — no SpellUpcastRules entry for "blindness" | Upcasting adds zero extra targets despite `canUpcast: true` |
| 4 | `SaveDCOverride` fragile — fallback DC is hardcoded 13 in BG3StatusIntegration | If Bug #1 fix doesn't thread SaveDC properly, repeat saves use DC 13 regardless of caster |
| 5 | `"blindness"` not mapped in `ConditionEffects.StatusToCondition` — only `"blinded"` is mapped | If fixed to BLINDNESS status, the ConditionEffects path would miss it (BG3 boost path still works as mitigation) |

Root cause: **This is a pattern, not an isolated defect.** The JSON spell files apply generic condition statuses (`"blinded"`, `"poisoned"`, `"frightened"`) instead of the BG3 spell-specific counterparts (`BLINDNESS`, `POISONED_ARROW`, `YOURFRIGHTENSTATUS`) that carry the `RepeatSave`/`RemoveConditions` data. Every spell that relies on end-of-turn saves likely has this same structural gap.

---

## New Systemic Issues Revealed (Batch 2)

The 5 spell audits in Batch 2 reveal four additional structural problems beyond those found in Batch 1:

### 5. Ground-Targeting Spells Misclassified as Entity-Pickers (affects teleportation spells)
`MapSpellTypeToTargetType()` maps all `BG3SpellType.Target` to `TargetType.SingleUnit`, ignoring `TargetConditions = "not Character()"` which signals ground-targeting. Any BG3-parsed spell that should target empty ground (Misty Step, Thunder Step, Far Step, Dimension Door) instead presents an entity picker and then fails at execution. **Misty Step is completely non-functional.**

### 6. Generic vs Spell-Specific Status ID Mismatch (affects ALL debuff spells with repeat saves)
JSON spell files apply generic condition statuses (e.g., `"blinded"`, `"paralyzed"`, `"frightened"`) rather than spell-specific statuses (e.g., `"BLINDNESS"`, `"HOLD_PERSON"`, `"YOURFRIGHTENSTATUS"`) that carry RepeatSave/RemoveConditions data in BG3. This means **end-of-turn saves to shake off effects are silently absent** for most debuff spells. Affected: Blindness, Hold Person (via JSON path), and likely every condition-applying spell.

### 7. No "On Attacked As Target" Status Trigger (affects reactive defensive spells)
`StatusTriggerType` has no entry for "the bearer is targeted by an attack roll." `AttackResolved` is subscribed but maps to `null` in `MapEventToTriggerType`. BG3's `RemoveEvents: "OnAttacked"` is silently dropped by `BG3StatusIntegration`. This breaks Mirror Image's core duplicate-destruction mechanic and affects any future status that reacts to being attacked (Blur variants, defensive illusions).

### 8. Surface Save DCs Are Static, Not Caster-Scaled (affects ALL surface-creating spells)
`SurfaceDefinition.SaveDC` is a static field with no mechanism to inject the caster's spell DC at creation time. Entangle (DC 12), and likely Spike Growth, Web, and all other surface spells use hardcoded DCs instead of `8 + proficiency + ability modifier`. This means surface saves don't scale with caster level at all.

---

## Updated Priority Ranking (Combined Batches 1 & 2)
| Priority | Systemic Issue | Spells Affected | Fix Scope |
|---|---|---|---|
| P0 | Save-for-half damage broken | Every AoE save spell (~30+) | DealDamageEffect.cs condition check logic |
| P0 | SpellUpcastRules ID mismatch | Every BG3-parsed spell with upcast | ID normalization in BG3ActionConverter or SpellUpcastRules |
| P1 | BG3StatusIntegration boost parser gaps | Hold Person, Haste, and ~50+ BG3 statuses | Extend or replace ParseBoosts() with BoostParser |
| P1 | BG3TargetConditions not evaluated | Hold Person, Sculpt Spells, creature-type spells | Wire ConditionEvaluator into TargetValidator |
| P1 | Generic vs spell-specific status IDs — no repeat saves | Blindness, Hold Person, all debuff spells | Audit all JSON statusIds; map to spell-specific BG3 statuses |
| P1 | Ground-targeting spells misclassified | Misty Step, Thunder Step, Far Step | Add `TargetConditions` check in `MapSpellTypeToTargetType()` |
| P2 | Spirit Guardians completely non-functional | Spirit Guardians | Wire aura data, add movement entry detection |
| P2 | Counterspell no level check | Counterspell | Add level comparison + ability check |
| P2 | Haste extra action not granted | Haste | Wire ModifierTarget.ActionPoints into TurnLifecycleService |
| P2 | No "OnAttackedAsTarget" status trigger | Mirror Image, Blur, defensive illusions | Add StatusTriggerType + wire AttackResolved |
| P2 | Surface save DCs are static | Entangle, Spike Growth, Web, all surface spells | Add CasterSpellSaveDC to SurfaceInstance |
| P3 | JSON upcast field name mismatch (`targetIncrease` vs `targetsPerLevel`) | Bless, Invisibility | Rename JSON fields or add SpellUpcastRules entries |
| P3 | Numerous incorrect range/radius values in JSON | Bless (range 2×), Entangle (range 1.5×, radius 2×) | Data audit pass across all JSON spell files |

Bottom line (Batch 2): Of the 5 additional spells audited, **Misty Step is completely non-functional** (teleports nowhere, wastes your slot), **Mirror Image is missing its core mechanic** (duplicates never break, it's just a static AC buff), **Blindness has no end-of-turn saves** (permanent blind for wrong duration), **Entangle has wrong area/range/DC** (double-sized zone with static DC), and **Bless gives a flat +2 instead of 1d4** (wrong bonus type with wrong multi-target upcast). The most damaging new systemic finding is that generic-vs-spell-specific status IDs silently eliminate end-of-turn saves across likely all debuff spells.