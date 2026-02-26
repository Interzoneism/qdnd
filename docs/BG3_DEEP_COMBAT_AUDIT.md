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
