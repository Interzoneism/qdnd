# Plan: Phase I - Combat Rules Completion

**Created:** 2026-02-02
**Status:** Ready for Atlas Execution

## Summary

Phase I wires existing services (LOS, Height, Cover) into combat resolution and adds missing features (Concentration, Contested Checks). These are "last mile" integrations to make combat feel complete.

## Context & Analysis

**Existing Services Not Wired:**
| Service | Exists | Used By | Missing Integration |
|---------|--------|---------|---------------------|
| LOSService | ✅ | AI scoring | TargetValidator, ability execution |
| HeightService | ✅ | AI scoring | RulesEngine.RollAttack modifiers |
| CoverLevel | ✅ | LOSResult | Combat math (AC bonus) |

**Missing Features:**
- Concentration (no system)
- Contested Checks (no framework)
- Cancellation semantics (ResolutionStack bug)

## Implementation Phases

### Phase 1: Wire LOS into TargetValidator

**Objective:** Prevent targeting through walls/obstacles

**Files to Modify:**
- Combat/Targeting/TargetValidator.cs

**Changes:**
1. Add optional LOSService dependency
2. In Validate(), check LOS to each proposed target
3. In GetValidTargets(), filter by LOS
4. In ResolveAreaTargets(), filter AoE hits by LOS

**Acceptance Criteria:**
- [ ] Cannot target enemy behind full cover
- [ ] AoE doesn't hit targets without line of effect
- [ ] Tests verify LOS filtering

---

### Phase 2: Wire Height Modifiers into Combat Math

**Objective:** High ground gives attack bonus, low ground gives penalty

**Files to Modify:**
- Combat/Abilities/EffectPipeline.cs (or wherever attacks resolve)
- Combat/Rules/RulesEngine.cs (add modifier support)

**Changes:**
1. Before rolling attack, get HeightService.GetAttackModifier(attacker, target)
2. Add modifier to attack roll
3. Log height advantage/disadvantage in breakdown

**Acceptance Criteria:**
- [ ] Attacker on high ground gets +2 to attack
- [ ] Attacker on low ground gets -2 to attack
- [ ] Modifier appears in roll breakdown

---

### Phase 3: Wire Cover into Defense

**Objective:** Cover provides AC bonus to defenders

**Files to Modify:**
- Combat/Abilities/EffectPipeline.cs
- Combat/Rules/RulesEngine.cs

**Changes:**
1. Before rolling attack, check LOSService for cover level
2. Apply GetACBonus() to defender's effective AC
3. Half cover: +2 AC, Three-quarters: +5 AC, Full: cannot target

**Acceptance Criteria:**
- [ ] Half cover gives +2 AC
- [ ] Three-quarters cover gives +5 AC
- [ ] Full cover blocks targeting
- [ ] Cover bonus appears in miss/hit explanation

---

### Phase 4: Add Concentration System

**Objective:** Implement BG3/5e concentration mechanics

**Files to Create:**
- Combat/Statuses/ConcentrationSystem.cs

**Changes:**
1. Create ConcentrationSystem that tracks one effect per combatant
2. On casting concentration ability, break previous concentration
3. On taking damage, make concentration save (DC 10 or half damage)
4. On failed save, break concentration and end effect
5. Mark abilities/statuses as requiring concentration

**Acceptance Criteria:**
- [ ] Only one concentration effect at a time
- [ ] Damage triggers concentration save
- [ ] Failed save breaks concentration
- [ ] Effect ends when concentration breaks

---

### Phase 5: Fix ResolutionStack Cancellation

**Objective:** Make cancellation actually prevent resolution

**Files to Modify:**
- Combat/Reactions/ResolutionStack.cs

**Changes:**
1. In Pop(), check IsCancelled before invoking OnResolve
2. Skip resolution for cancelled items
3. Optionally invoke OnCancelled callback

**Acceptance Criteria:**
- [ ] Cancelled items don't invoke OnResolve
- [ ] Counterspell-style reactions can actually cancel spells
- [ ] Tests verify cancellation behavior

---

### Phase 6: Add Contested Checks

**Objective:** Framework for opposed rolls (shove, grapple, etc.)

**Files to Modify:**
- Combat/Rules/RulesEngine.cs

**Changes:**
1. Add Contest(combatantA, combatantB, skillA, skillB) method
2. Roll both, apply modifiers, determine winner
3. Handle ties (defender wins or attacker wins, configurable)
4. Return structured result with both rolls and breakdown

**Acceptance Criteria:**
- [ ] Contest roll resolves with winner
- [ ] Both rolls visible in breakdown
- [ ] Ties handled correctly

---

## Success Criteria

- [ ] LOS prevents targeting through walls
- [ ] Height gives combat bonuses
- [ ] Cover provides AC bonuses
- [ ] Concentration mechanics work
- [ ] Reaction cancellation works
- [ ] Contested checks available
- [ ] All CI gates pass
