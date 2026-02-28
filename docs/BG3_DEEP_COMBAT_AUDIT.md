# BG3 Deep Combat Mechanics Audit

**Date:** 2025-01-XX  
**Scope:** Seven areas NOT well-covered by `docs/BG3_FULL_GAP_ANALYSIS.md`  
**Method:** Line-by-line source read of core combat files  

---

## Executive Summary

| Severity | Count | New (not in existing audit) |
|----------|-------|-----------------------------|
| CRITICAL | 1     | 1                           |
| MAJOR    | 6     | 6                           |
| MINOR    | 4     | 4                           |
| VERIFIED OK | 12 | —                           |

---

## Area 1: Attack Mechanics Deep-Dive

### BUG C1 — Massive Damage Instant Death Never Triggers ⛔ CRITICAL

**BG3/5e rule:** When damage reduces a creature to 0 HP and the remaining (overflow) damage ≥ the creature's HP maximum, the creature dies outright instead of falling unconscious.

**Code behavior:** [DealDamageEffect.cs](Combat/Actions/Effects/DealDamageEffect.cs#L637) checks:
```csharp
if (actualDamageDealt > target.Resources.MaxHP)
```
But `actualDamageDealt` is the return value of `Resources.TakeDamage()` ([Combatant.cs](Combat/Entities/Combatant.cs#L43-L67)), which caps at `currentHP + tempHP` — overflow damage is discarded. Since `currentHP ≤ MaxHP`, the result can never exceed MaxHP for a character without temp HP. Additionally, the comparison uses `>` instead of `>=`.

**Example:** A creature with 100 max HP and 30 current HP takes 200 raw damage. `TakeDamage(200)` absorbs 30 HP, returns 30. Check: `30 > 100` → false. Creature goes Downed instead of dying outright. Correct behavior: overflow = 170 ≥ 100 → instant death.

**Fix:** Compare the raw `finalDamage` against `preHitCurrentHP + MaxHP` (or store overkill from TakeDamage), and use `>=`.

**In existing audit:** No.

---

### BUG M1 — Shield Spell Cannot Block Magic Missile ⚠️ MAJOR

**BG3/5e rule:** The Shield spell specifically states it negates all Magic Missile darts hitting the caster.

**Code behavior:** Magic Missile is defined with no `attackType` ([bg3_mechanics_actions.json](Data/Actions/bg3_mechanics_actions.json#L2028)) and tags `["auto_hit"]`. In [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L858), the attack reaction path (`TryTriggerAttackReactions`) only fires when `action.AttackType.HasValue` is true. Auto-hit multi-projectile spells bypass this entirely — Shield's reaction trigger never activates against Magic Missile.

**Fix:** Add a special-case check: if the incoming action has the `auto_hit` tag and the target has a Shield reaction prepared, trigger the Shield reaction to cancel the projectile.

**In existing audit:** No.

---

### VERIFIED OK — Prone Attack Rules

Prone correctly uses `ProneAttackRules` flag in [ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs#L242): melee attacks gain advantage, ranged attacks suffer disadvantage. The `GetAggregateEffects` method ([line 519](Combat/Statuses/ConditionEffects.cs#L519)) properly branches on `isMeleeAttack`.

### VERIFIED OK — Crossbow Expert Ranged-in-Melee Exemption

[EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L917-L923) correctly checks `hasCrossbowExpert && isAttackingWithCrossbow` before applying the "Threatened" ranged disadvantage. Only crossbow weapon types qualify — ranged spells and bows still get disadvantage. BG3-correct.

### VERIFIED OK — Height Modifier (+2/−2)

[HeightService.cs](Combat/Environment/HeightService.cs) returns +2 (high ground) / −2 (low ground), passed to RulesEngine as a flat modifier via `heightModifier` parameter ([EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L876)). BG3-correct (BG3 uses a flat bonus, not advantage/disadvantage like 5e RAW).

### VERIFIED OK — Auto-Crit on Paralyzed/Unconscious

[ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs#L219-L223): Paralyzed has `MeleeAutocrits = true`. [Line 256](Combat/Statuses/ConditionEffects.cs#L256): Unconscious has `MeleeAutocrits = true`. Stunned correctly does NOT have `MeleeAutocrits` ([line 241](Combat/Statuses/ConditionEffects.cs#L241)). BG3/5e-correct.

### VERIFIED OK — Concentration Breaks Before New Concentration Spell's Attack Roll

[EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L851-L855): Old concentration is broken BEFORE the attack roll so that advantage/disadvantage from the old spell's effects (e.g., Hold Person paralysis) are properly removed. BG3-correct.

---

## Area 2: Damage Pipeline Accuracy

### BUG M2 — Petrified Resistance-to-All Not Integrated into DamagePipeline ⚠️ MAJOR

**BG3/5e rule:** Petrified creatures have resistance to all damage.

**Code behavior:** [ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs#L228) sets `HasResistanceToAllDamage = true` on Petrified, and [GetAggregateEffects](Combat/Statuses/ConditionEffects.cs#L548) propagates it to `AggregateConditionEffects.HasResistanceToAllDamage`. However, [DealDamageEffect.cs](Combat/Actions/Effects/DealDamageEffect.cs) and [RulesEngine.RollDamage](Combat/Rules/RulesEngine.cs#L1118) only check `BoostEvaluator.GetResistanceLevel()` for specific damage types — they never query the Petrified condition's blanket resistance flag.

**Fix:** In the damage application path, check `ConditionEffects.GetAggregateEffects(targetStatuses).HasResistanceToAllDamage` and halve damage if true (unless already immune or a specific vulnerability overrides).

**In existing audit:** No.

---

### VERIFIED OK — Damage Pipeline Stages

[DamagePipeline.cs](Combat/Rules/DamagePipeline.cs): 7-stage pipeline (Base → Additive DamageDealt → % DamageDealt → % DamageTaken → Flat DamageTaken → Floor at 0 → Layer absorption). Math is correct. Multiple same-type resistances stack multiplicatively — already flagged as D-1 in existing audit.

### VERIFIED OK — Critical Hit Damage

The critical hit path doubles dice correctly (handled via `isCritical` flag in DealDamageEffect). Auto-crit from Unconscious/Paralyzed feeds through the `autoCritOnHit` parameter chain.

---

## Area 3: Turn Order and Initiative

### BUG m1 — No Surprise Round ℹ️ MINOR

**BG3 rule:** Attacking from stealth grants surprise. Surprised creatures cannot act on their first turn and cannot take reactions until their turn ends.

**Code behavior:** No surprise mechanic exists. `grep -r "surprise" Combat/` returns zero results. [TurnQueueService.cs](Combat/Services/TurnQueueService.cs) has no surprise flag, skip logic, or reaction suppression for round 1.

**Fix:** Add a `Surprised` flag per combatant. In round 1, surprised combatants skip their turn and cannot use reactions until after their turn in the initiative order.

**In existing audit:** No. (Note: Surprise is relatively rarely encountered in BG3 tactical combat; severity is Minor because the core initiative and turn cycling work correctly.)

---

### VERIFIED OK — Initiative Tiebreaker

[ScenarioLoader.cs](Data/ScenarioLoader.cs#L467): `combatant.InitiativeTiebreaker = resolved.AbilityScores[AbilityType.Dexterity]` — uses raw DEX score. [TurnQueueService.cs](Combat/Services/TurnQueueService.cs) sorts by `.ThenByDescending(c => c.InitiativeTiebreaker)`. BG3/5e-correct (higher DEX wins ties).

### VERIFIED OK — Alert Feat and Feral Instinct

[ScenarioLoader.cs](Data/ScenarioLoader.cs#L449): Alert grants +5 initiative bonus. [Line 461](Data/ScenarioLoader.cs#L461): Feral Instinct grants advantage (rolls twice, takes higher). Both BG3-correct.

### VERIFIED OK — Dead/Downed Exclusion

[TurnQueueService.cs](Combat/Services/TurnQueueService.cs): Dead combatants excluded from turn order; Downed combatants included (for death saves). Correct.

---

## Area 4: Movement Edge Cases

### BUG M3 — Allies Block Movement Like Enemies ⚠️ MAJOR

**BG3 rule:** You can freely move through allied creatures' spaces (but cannot end your turn on the same tile). Enemy creatures block movement unless there's a significant size difference.

**Code behavior:** [MovementService.cs `GetBlockingCombatant()`](Combat/Movement/MovementService.cs#L940-L955) treats ALL active combatants as movement blockers, with no faction check:
```csharp
foreach (var other in GetCombatants())
{
    if (other == null || other.Id == mover.Id || !other.IsActive)
        continue;
    // No faction check — allies block just like enemies
    if (other.Position.DistanceTo(destination) < COMBATANT_COLLISION_RADIUS)
        return other;
}
```
Both the pathfinder and `IsPositionBlockedForNavigation` use this method, so allies are navigation obstacles.

**Fix:** Add `if (other.Faction == mover.Faction) continue;` (for pass-through), or only block destination tiles at movement end. Also add size-based pass-through rules for enemies per BG3.

**In existing audit:** No.

---

### VERIFIED OK — Opportunity Attacks and Disengage

[MovementService.cs](Combat/Movement/MovementService.cs): OA triggers on `EnemyLeavesReach` with proper exemptions for Disengage status and Mobile feat (exempts targets attacked this turn). Correct.

### VERIFIED OK — Frightened Blocks All Movement (BG3-Correct)

[MovementService.cs](Combat/Movement/MovementService.cs#L203-L210): Frightened completely prevents movement. The code comment correctly notes "BG3: Frightened completely prevents movement (bg3.wiki/wiki/Frightened)". This differs from 5e RAW (can't move closer to fear source) but is BG3-accurate.

---

## Area 5: Specific Spell Mechanic Bugs

### BUG m2 — Prone Automatically Triggers Concentration Check ℹ️ MINOR

**BG3/5e rule:** Concentration checks trigger from: (1) taking damage, (2) becoming incapacitated, (3) dying. Being knocked prone does NOT trigger a concentration check.

**Code behavior:** [ConcentrationSystem.cs](Combat/Statuses/ConcentrationSystem.cs#L255-L270):
```csharp
if (IsProneStatus(statusDefinition))
{
    var result = CheckConcentrationAgainstDc(
        evt.TargetId,
        MinimumConcentrationDc,
        ConcentrationCheckTrigger.Prone, ...);
    if (!result.Maintained)
        BreakConcentration(evt.TargetId, "failed concentration save (prone)");
    return;
}
```
A DC 10 CON save is forced whenever a concentrating caster is knocked prone. This can break concentration from effects like Grease or a Shove that shouldn't interact with concentration at all.

**Fix:** Remove the `IsProneStatus` block from the status-applied concentration handler. Prone-inducing effects that also deal damage will still trigger concentration checks via the damage path.

**In existing audit:** No.

---

### BUG m3 — Magic Missile Per-Dart Damage Not Simultaneous ℹ️ MINOR

**5e RAW / BG3 rule:** Magic Missile darts hit simultaneously. For concentration checks, the total damage from all darts triggers ONE concentration check (not one per dart).

**Code behavior:** [EffectPipeline.cs `ExecuteMultiProjectile()`](Combat/Actions/EffectPipeline.cs#L1622) fires each projectile sequentially. Each dart deals damage independently, which dispatches a separate `DamageTaken` event, causing a separate concentration check per dart in [ConcentrationSystem.cs](Combat/Statuses/ConcentrationSystem.cs#L228-L239).

**Impact:** A 3-dart Magic Missile dealing 3×(1d4+1) forces 3 separate DC 10 concentration checks instead of 1 check at DC max(10, totalDamage/2). Three DC 10 checks are MUCH harder to pass than one.

**Fix:** Aggregate damage from auto-hit multi-projectile spells before dispatching the DamageTaken event, or batch concentration checks per source action.

**In existing audit:** No. (Note: This is a known 5e rules debate. BG3 actually does separate concentration checks per dart in some implementations. Marking as Minor.)

---

### VERIFIED OK — Magic Missile Auto-Hit

[bg3_mechanics_actions.json](Data/Actions/bg3_mechanics_actions.json#L2028): Magic Missile has no `attackType` and tag `auto_hit`. EffectPipeline skips attack rolls when `action.AttackType` is null. Multi-projectile path ([line 1660](Combat/Actions/EffectPipeline.cs#L1660)) also skips per-projectile attack rolls when `AttackType` is null. Correct: Magic Missile auto-hits.

### VERIFIED OK — Upcast Scaling for Multi-Projectile Spells

[SpellUpcastRules.cs](Data/Spells/SpellUpcastRules.cs#L118): Magic Missile scales `ProjectilesPerLevel = 1` (+1 dart per upcast level). [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L702): `effectiveProjectileCount += options.UpcastLevel * action.UpcastScaling.ProjectilesPerLevel`. Correct.

---

## Area 6: Death and Dying

### BUG M4 — Death Saves Bypass RulesEngine Modifier Stack ⚠️ MAJOR

**BG3/5e rule:** Death saving throws can benefit from bonuses: Bless (+1d4), Aura of Protection (+CHA mod for Paladin allies nearby), Diamond Soul (proficiency), Halfling Lucky (reroll nat 1), etc.

**Code behavior:** [TurnLifecycleService.cs](Combat/Services/TurnLifecycleService.cs#L413) rolls death saves via raw RNG:
```csharp
int roll = _getRng()?.Next(1, 21) ?? 10;
```
This bypasses `RulesEngine.RollSave()` entirely. No modifier stack, no boost evaluation, no advantage/disadvantage resolution. Bless, Guidance, Halfling Lucky, Paladin Aura of Protection — none apply to death saves.

**Fix:** Route death saves through `RulesEngine.RollSave()` with `QueryType.SavingThrow`, `saveType: "death"`, targeting the downed combatant.

**In existing audit:** Yes — flagged as ST-2 in existing gap analysis. **Included here because of cross-cutting severity** across bonus action economy (Bless as bonus action investment), passive features, and racial traits.

---

### BUG M5 — No Fear Source Tracking for Frightened ⚠️ MAJOR

**BG3/5e rule:** Frightened creature has disadvantage on ability checks and attack rolls only while the source of its fear is within line of sight. Additionally, the creature can't willingly move closer to the source (5e) / can't move at all (BG3). In BG3, the frightened creature also cannot attack the specific creature that frightened it.

**Code behavior:** [ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs#L190) defines Frightened with:
```csharp
HasDisadvantageOnAttacks = true,
HasDisadvantageOnAbilityChecks = true,
```
These apply unconditionally — no source tracking. The comment at [line 196](Combat/Statuses/ConditionEffects.cs#L196) says `"Can't willingly move closer to fear source — handled by movement validation"` but MovementService blocks ALL movement for Frightened, not directional restriction relative to a source.

Missing mechanics:
1. No "fear source" ID stored on the status instance
2. Disadvantage applies even if fear source is dead, banished, or out of sight
3. Can't enforce "can't attack the frightener" rule

**Fix:** Store fear source combatant ID on the Frightened status instance. Gate disadvantage behind LoS check to fear source. Block attacks targeting the fear source specifically.

**In existing audit:** No.

---

### VERIFIED OK — Damage While Downed Causes Death Save Failures

[DealDamageEffect.cs](Combat/Actions/Effects/DealDamageEffect.cs#L575-L586): When a Downed combatant takes damage, 1 death save failure is added (2 if critical). Capped at 3 failures. 3 failures → Dead. Correct.

### VERIFIED OK — Nat 20/Nat 1 Death Save Results

[TurnLifecycleService.cs](Combat/Services/TurnLifecycleService.cs): Nat 20 → revive with 1 HP. Nat 1 → 2 failures (capped at 3). 3 successes → stabilized (Unconscious). All correct per BG3/5e.

### VERIFIED OK — NPCs Die Instantly at 0 HP

[DealDamageEffect.cs](Combat/Actions/Effects/DealDamageEffect.cs#L622): Hostile/Neutral faction combatants go directly to Dead at 0 HP — no death saves. BG3-correct.

---

## Area 7: Bonus Action Economy

### BUG m4 — Non-Weapon Action Resets Attack Pool Irreversibly ℹ️ MINOR

**BG3/5e rule:** With Extra Attack, a Fighter can make multiple weapon attacks per Attack action. If they use Action Surge to gain an additional action and use one action for a spell, they should still be able to use the other action for weapon attacks.

**Code behavior:** [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L653-L656):
```csharp
if (source.ActionBudget != null && effectiveCost.UsesAction)
{
    source.ActionBudget.ResetAttacks();  // Sets AttacksRemaining = 0
}
```
When a non-weapon action consumes an action, `ResetAttacks()` zeroes the attack pool. [GrantAdditionalAction](Combat/Actions/ActionBudget.cs#L253) from Action Surge does restore the pool, but only if Action Surge is used AFTER the spell. If a Fighter: (1) uses Action Surge, (2) casts a spell (action), (3) tries to attack (remaining action) — the spell resets `AttacksRemaining = 0` AND consumes one `_actionCharges`. The remaining action charge has 0 attacks remaining.

**Fix:** Only zero the attack pool for the specific action being consumed, or restore the pool when there are remaining action charges.

**In existing audit:** No.

---

### BUG M6 — Action Surge Confirmed Fixed (Was RV-C1) ✅ RESOLVED

**Previous finding (RV-C1):** "Action Surge burns a bonus action instead of being free."

**Current state:** [bg3_mechanics_actions.json](Data/Actions/bg3_mechanics_actions.json#L727-L745) defines Action Surge with:
```json
"cost": { "resourceCosts": { "action_surge": 1 } }
```
No `usesAction`, `usesBonusAction`, or `usesReaction` flags. [GrantActionEffect.cs](Combat/Actions/Effects/GrantActionEffect.cs#L48) correctly calls `GrantAdditionalAction(1)` which adds an action charge without consuming any economy resource.

**Status:** FIXED — no longer costs a bonus action.

---

### VERIFIED OK — Extra Attack Pool System

[ActionBudget.cs](Combat/Actions/ActionBudget.cs#L36-L44): `MaxAttacks` tracks total attacks per action, `AttacksRemaining` decrements per weapon attack. [ConsumeAttack()](Combat/Actions/ActionBudget.cs#L291) only consumes the action charge when `AttacksRemaining` hits 0. [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L631-L637) correctly distinguishes weapon attacks (consume from pool) vs non-weapon actions (consume full action). Core Extra Attack logic is correct.

### VERIFIED OK — No Bonus-Action-Spell Cantrip Restriction

BG3 removes the 5e rule that casting a bonus action spell restricts your action to cantrips. The code has no such enforcement — `CanUseAbility` checks only resource availability, not spell-level restrictions based on prior bonus action spells. BG3-correct.

### VERIFIED OK — Reaction Budget Resets Per Round

[ActionBudget.cs](Combat/Actions/ActionBudget.cs#L119): `ResetForTurn()` resets action, bonus, movement, and attacks but NOT reaction. [ResetReactionForRound()](Combat/Actions/ActionBudget.cs#L127) is a separate method. This matches the 5e/BG3 design: reaction refreshes at round start, not turn start.

---

## Appendix: Existing Audit Cross-Reference

| ID | Description | Status |
|----|-------------|--------|
| RV-C1 | Action Surge costs bonus action | **FIXED** — verified JSON has no economy cost |
| RV-M7 | Frozen missing GrantsAdvantageToAttackers | **FIXED** — [ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs#L281) now has `GrantsAdvantageToAttackers = true` and `MeleeAutocrits = true` |
| ST-2 | Death saves bypass modifier stack | **STILL OPEN** — confirmed at [TurnLifecycleService.cs](Combat/Services/TurnLifecycleService.cs#L413) |
| D-1 | Multiple same-type resistances stack multiplicatively | **STILL OPEN** — confirmed in [DamagePipeline.cs](Combat/Rules/DamagePipeline.cs) |

---

## Summary Table

| ID | Area | Description | Severity | File | In Existing Audit? |
|----|------|-------------|----------|------|---------------------|
| C1 | Attack | Massive damage instant death never triggers | CRITICAL | DealDamageEffect.cs#L637 | No |
| M1 | Attack | Shield can't block Magic Missile | MAJOR | EffectPipeline.cs (multi-proj) | No |
| M2 | Damage | Petrified resistance-to-all not applied in damage path | MAJOR | RulesEngine.cs / DealDamageEffect.cs | No |
| M3 | Movement | Allies block movement like enemies | MAJOR | MovementService.cs#L940 | No |
| M4 | Death | Death saves bypass modifier stack | MAJOR | TurnLifecycleService.cs#L413 | Yes (ST-2) |
| M5 | Death | No fear source tracking for Frightened | MAJOR | ConditionEffects.cs | No |
| M6 | Economy | Resolved: Action Surge no longer costs bonus action | RESOLVED | bg3_mechanics_actions.json | Was RV-C1 |
| m1 | Initiative | No surprise round | MINOR | TurnQueueService.cs | No |
| m2 | Spells | Prone triggers concentration check | MINOR | ConcentrationSystem.cs#L255 | No |
| m3 | Spells | Magic Missile darts trigger separate concentration checks | MINOR | EffectPipeline.cs (multi-proj) | No |
| m4 | Economy | Non-weapon action resets attack pool irreversibly | MINOR | EffectPipeline.cs#L653 | No |

---

## Area 8: Creature Mechanics — How Creatures Differ from Player Characters

> **Research source:** bg3.wiki (Creatures, Wild Shape, Companions, Find Familiar, Beast Master, Ranger's Companion, Actions, List of Creature Types, Summon disambiguation, Conjuration spells)

### 8.1 Unified Creature Model

In BG3, **every living entity is a "creature"** — PCs, NPCs, companions, summons, familiars, wild shape forms, and enemies all share the same stat block framework:

| Property | Shared by all creatures |
|----------|------------------------|
| Creature type | Yes (Humanoid, Beast, Fiend, etc.) |
| Race | Yes |
| Hit Points (HP) | Yes |
| Armour Class (AC) | Yes |
| Level | Yes |
| Size | Yes (Tiny → Huge) |
| Weight | Yes |
| Movement speed | Yes (base from race, modified by form) |
| 6 Ability scores | Yes (STR, DEX, CON, INT, WIS, CHA) |
| Proficiency bonus | Yes (determined by level) |
| Conditions | Yes (same condition system) |
| Resistances/vulnerabilities | Yes (often by creature type) |

**Key implication for implementation:** There is NOT a separate "Creature" class vs "PlayerCharacter" class in the game rules. The differences are in *what features, actions, and resources* a creature has access to — not in its stat block structure.

---

### 8.2 Creature Types (14 categories)

BG3 has 14 creature types. These matter mechanically because:
- Spells target by type (e.g., Hold Person → Humanoid only, Hold Monster → any)
- Resistances/vulnerabilities are often type-inherited
- Some passives key off creature type

| Type | Description | Combat relevance |
|------|-------------|-----------------|
| **Aberration** | Alien beings (Mind Flayers, Intellect Devourers) | Often psionic abilities |
| **Beast** | Natural animals (bears, rats, wolves) | Wild Shape forms, familiars, ranger companions |
| **Celestial** | Upper Planes natives (Deva, Hollyphant) | Radiant damage, healing |
| **Construct** | Artificial creatures (Steel Watcher, Bernard) | Often immune to many conditions |
| **Dragon** | Ancient reptilians | Breath weapons, frightful presence |
| **Elemental** | Elemental Plane natives | Elemental damage/immunity |
| **Fey** | Nature-magical creatures (Dryads, Redcaps) | Familiars are Fey type |
| **Fiend** | Lower Planes natives (Cambions, Imps) | Fire resistance typical |
| **Giant** | Enormous humanoid-like (Ogres, Trolls) | High STR, large size |
| **Humanoid** | Bipedal races (all playable races, goblins, etc.) | Hold Person target |
| **Monstrosity** | Magical monsters (Ettercaps, Owlbears) | Varied special abilities |
| **Ooze** | Gelatinous creatures | Often split on damage, acid |
| **Plant** | Vegetative/fungal creatures | Nature magic interaction |
| **Undead** | Reanimated (Zombies, Vampires, Liches) | Necrotic resistant, radiant vulnerable |

---

### 8.3 Action Economy — Identical Framework, Different Actions

**All creatures share the same 3-resource action economy:**

| Resource | Per-turn refresh | Notes |
|----------|-----------------|-------|
| **Action** | 1/turn (reset at turn start) | Can gain extra via Action Surge, Haste |
| **Bonus Action** | 1/turn (reset at turn start) | |
| **Reaction** | 1/round (reset at round start) | Used for Opportunity Attacks, Shield, Counterspell, etc. |

**What differs between creatures and PCs is the action *list*:**

- **Common actions** (available to ALL creatures): Dash, Disengage, Main Hand Attack, Ranged Attack, Throw, Unarmed Strike, Shove (BA), Jump (BA), Dip (BA), Off-Hand Attack (BA)
- **Class actions** (PCs + classed NPCs): Action Surge, Extra Attack, Sneak Attack, Divine Smite, Wild Shape, etc.
- **Racial actions** (from race): Dragonborn breath weapons, Githyanki Psionics, etc.
- **Creature-specific actions** (NPC/monster only): Beast form attacks (Claws, Bite, Tusk Attack), breath weapons, special abilities
- **Weapon actions** (from equipped weapons): Pommel Strike, Lacerate, etc.

**Extra Attack mechanics:**
- PCs: Extra Attack at class level 5 (1→2 attacks), Fighter Improved Extra Attack at 11 (1→3)
- Beast Master companions: Bestial Fury at Ranger 11 grants extra attack to all companions
- NPCs/monsters: May have Multiattack as a custom action (multiple attacks in one action)

**Legendary Actions (Honour Mode bosses):**
- Triggered like reactions but use a separate resource pool (not the Reaction resource)
- Unaffected by conditions that prevent reactions (Dazed, Shocked, Slowed, Surprised)
- Only available to specific boss NPCs in Honour Mode difficulty

---

### 8.4 Summon/Companion Categories and Control Model

BG3 has several distinct summoning mechanics, each with different rules:

#### A. Ranger's Companion (Beast Master)

| Property | Detail |
|----------|--------|
| **Source** | Beast Master subclass, Level 3 |
| **Cost** | Action + Recharges on Short Rest |
| **Duration** | Permanent until killed or resummoned |
| **Concentration** | **NO** — not a concentration spell |
| **Control** | Fully player-controlled (own turn in initiative) |
| **Stacking** | Independent of Find Familiar; can have both simultaneously |
| **Scaling** | HP/AC/abilities upgrade at Ranger levels 3, 5, 8, 11 |

**Companion stat progression:**

| Companion | HP 3→5→8→11 | AC 3→5→8→11 | Key abilities |
|-----------|-------------|-------------|---------------|
| Bear | 19→39→69→99 | 11→15→16→18 | Goading Roar, Claws, Honeyed Paws, Ursine Reinforcements |
| Boar | 11→27→51→85 | 11→14→15→18 | Tusk, Charge, Rage, Frenzied Strike, Kick Up Muck |
| Dire Raven | 13→21→32→44 | 16→19→19→22 | Beak, Rend Vision, Fly, Bad Omen, On Black Wings |
| Wolf | 11→31→61→91 | 13→16→16→21 | Bite, Lunging Bite, Pack Tactics, Infectious Bite |
| Wolf Spider | 10→24→42→60 | 14→18→18→21 | Venomous Bite, Web, Cocoon, Bursting Brood |

**Level 7 — Exceptional Training:** Companions can Dash, Disengage, and Hide as bonus actions.
**Level 11 — Bestial Fury:** Companions get an extra attack.
**All companions learn:** Prey's Scent, Companion's Bond, Exceptional Training, Bestial Fury (progressively).

#### B. Find Familiar

| Property | Detail |
|----------|--------|
| **Source** | Level 1 Conjuration spell |
| **Cost** | Action + Level 1 Spell Slot (can be cast as Ritual) |
| **Duration** | Until killed, dismissed, or caster leaves active party |
| **Concentration** | **NO** — not a concentration spell |
| **Control** | Fully player-controlled (own turn in initiative) |
| **Limit** | Only one familiar at a time (all Find Familiar variants mutually exclusive) |
| **Restrictions** | Cannot interact with environment (no buttons/levers), CAN drink elixirs/potions, attacks always use DEX |

**Familiar variants:**
- Standard: Cat, Crab, Frog, Rat, Raven, Spider
- Pact of the Chain (Warlock): Imp, Quasit (stronger combat familiars)
- Special: Boo (Minsc only), Cheeky Quasit (quest), Scratch (conditional)
- Available to: Wizard L1, Ranger (Beast Tamer), Eldritch Knight L3, Arcane Trickster L3, Warlock (Pact of Chain)

#### C. Conjuration Summons (Concentration-based)

Many summoning spells **do** require concentration:

| Spell | Level | Concentration? | Summon type |
|-------|-------|----------------|-------------|
| Find Familiar | 1 | No | Fey spirit (beast form) |
| Animate Dead | 3 | No | Undead (zombie/skeleton) |
| Conjure Minor Elemental | 4 | No* | Azer / Mephits |
| Conjure Woodland Being | 4 | No* | Fey creature |
| Conjure Elemental | 5 | No* | Elemental |
| Create Undead | 6 | No | Greater undead |
| Planar Ally | 6 | No | Celestial/Fiend |
| Flaming Sphere | 2 | **Yes** | Fire sphere |
| Guardian of Faith | 4 | No | Guardian spirit |
| Spiritual Weapon | 2 | No | Floating weapon |

*\*BG3 removes concentration from several summon spells that require it in 5e RAW — this is a major BG3-specific deviation.*

**Focused Conjuration (Wizard Conjuration School):** Concentration can't be broken by taking damage while concentrating on a Conjuration spell. Key subclass feature for summoner builds.

#### D. Attached Followers (Narrative)

These are NPC-controlled allies that join temporarily during quests. They fight alongside the party but are NOT directly controlled:
- Us (Intellect Devourer), Scratch (Dog), Boo, Glut (Myconid), Armoured Owlbear, Strange Ox
- Powerful NPCs: Aylin (Paladin), Mizora (Cambion), The Emperor (Mind Flayer), Orpheus (Monk)

---

### 8.5 Wild Shape — Stat Replacement Rules

Wild Shape is the most complex creature-transformation mechanic:

| Property | Rule |
|----------|------|
| **Physical stats** | STR, DEX, CON → replaced by beast form's scores |
| **Mental stats** | INT, WIS, CHA → retained from original character |
| **HP** | Replaced by beast form HP (separate pool) |
| **AC** | Replaced by beast form AC |
| **Movement** | Racial base speed + form-specific bonus |
| **Spellcasting** | **Cannot cast spells** while Wild Shaped |
| **Concentration** | Spells cast BEFORE Wild Shape **persist** (keep concentrating) |
| **Equipment** | Merges into form, no effect until revert |
| **Proficiencies** | Retain skill and saving throw proficiencies (use new physical scores) |
| **On 0 HP** | Revert to original form; overflow damage applies to original HP |
| **Instant death** | If overflow damage kills original form → instant death |
| **Actions** | Limited to beast form actions only |

**Beast form HP scaling:**
- Base HP increases every 2 Druid levels
- Form-specific multipliers: Wolf 1.4x, Bear/Badger/Spider 1.3x, Myrmidons/Rothe/Owlbear 1.2x, Sabre-Tooth/Panther 1.15x

**Attack damage scaling:**
- Level 4: +1d4 or +1d6 bonus (form-dependent)
- Scales up at levels 8 and 12
- Special attack DC = 8 + proficiency + WIS modifier

**Form unlock levels:**
- Level 2: Badger, Cat, Spider, Wolf, Bear* 
- Level 4: Dire Raven*, Deep Rothé
- Level 6: Panther, Owlbear
- Level 8: Sabre-Toothed Tiger*
- Level 10: Dilophosaurus, Myrmidons*
- (* = Circle of the Moon only)

---

### 8.6 AI Control vs Player Control

| Entity type | Controlled by | Turn in initiative? |
|-------------|--------------|---------------------|
| Player characters | Player | Yes |
| Companions | Player | Yes (when in party) |
| Ranger's Companion | Player | Yes (own turn) |
| Familiars | Player | Yes (own turn) |
| Conjured summons | Player | Yes (own turn) |
| Wild Shape form | Player (replaces PC turn) | Same initiative as PC |
| Attached followers | AI (NPC) | Yes (own turn) |
| Enemy creatures | AI (NPC) | Yes |
| Neutral creatures | AI (NPC) | Yes (if in combat) |

**Key distinction:** Player-controlled summons/companions get their OWN turn in initiative order. They are separate entities from the summoner. The summoner and the summon act independently on different initiative counts.

---

### 8.7 Creature-Only Mechanics (Not Available to Standard PCs)

| Mechanic | Description | Who has it |
|----------|-------------|-----------|
| **Multiattack** | Custom action: multiple attacks in one action (not Extra Attack) | Boss NPCs, some monsters |
| **Legendary Actions** | Reaction-like actions using separate resource (Honour Mode) | Boss NPCs only |
| **Frightful Presence** | AoE fear on encounter start | Dragons, some bosses |
| **Breath Weapons (innate)** | Recharging AoE damage (not from Dragonborn race) | Dragons, elementals |
| **NPC-only spells** | Spells unavailable to players (NPC Conjuration list) | Specific boss NPCs |
| **Pack Tactics** | Advantage on attack if ally within 1.5m of target | Wolves, some beasts |
| **Web Walker** | Immune to web movement penalty | Spiders |
| **Fly** | Permanent flight movement | Ravens, some forms |
| **Instant death at 0 HP** | No death saves for non-party creatures | All hostile/neutral NPCs |
| **Condition immunities** | Blanket immune to multiple conditions | Constructs, Undead, Oozes |

**NPC-only conjuration spells (sampled):**
- Summon Gilded Hellsboar (L1), Astral Rift (L3), Invigorating Flame (L4), Ethel's Insect Plague (L5), Invocation of Eternal Debt (L6), The Closed Fist of Bane (L6), Vampiric Swarm (L6)

---

### 8.8 Implementation Implications for QDND

Based on this research, the key architectural decisions for creature mechanics are:

1. **Single `Combatant` class** — Do NOT create separate PC/Creature hierarchies. Use composition: every combatant has the same stat block; features/actions are data-driven.

2. **Creature type as enum/tag** — Used for spell targeting validation (Hold Person checks Humanoid), resistance defaults, and some passive feature gates.

3. **Summon lifecycle management:**
   - Track summon→summoner relationship (for auto-dismiss when caster leaves party)
   - Some summons are concentration-linked (breaks concentration → dismiss summon)
   - Some summons are permanent (Ranger's Companion, Animate Dead)
   - Enforce mutual exclusion where needed (only one Find Familiar variant active)

4. **Wild Shape as stat overlay** — Store original stats, overlay beast form stats. On revert, restore original stats and apply overflow damage.

5. **Action list is data-driven** — Creatures don't need a different action system; they just have different action IDs in their action list. Common actions are universal.

6. **NPC instant death** — Non-party creatures skip death saves entirely at 0 HP.

7. **Legendary Actions are a separate resource** — Not reactions. Not blocked by reaction-preventing conditions. Only available to designated boss entities.
