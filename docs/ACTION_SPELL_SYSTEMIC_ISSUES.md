# Action/Spell System — Systemic Issues Report

**Date:** 2026-03-08  
**Scope:** Full audit of the action/spell pipeline from BG3 data ingestion through runtime execution.  
**Finding:** Only ~37% of raw BG3 spell entries register with correct effects. The remaining ~63% are silently dropped, registered with wrong data, or registered with broken runtime behaviour.

---

## Table of Contents

1. [Tier 1 — CRITICAL (each breaks 100+ spells)](#tier-1--critical)
2. [Tier 2 — HIGH (each breaks 10–100 spells)](#tier-2--high)
3. [Tier 3 — MEDIUM (systemic design gaps)](#tier-3--medium)
4. [Tier 4 — LOW (edge cases / cosmetic)](#tier-4--low)
5. [What Works Correctly](#what-works-correctly)
6. [Recommended Fix Order](#recommended-fix-order)

---

## Tier 1 — CRITICAL

### C1 — ID Collision Cascades Destroy 1,260 Upcast Variants

| Metric | Value |
|---|---|
| **Spells affected** | Every multi-level spell (~1,260 upcast entries silently dropped) |
| **Files** | `Data/Spells/SpellUpcastRules.cs` (NormalizeBG3SpellId ~L52), `Data/Actions/ActionDataLoader.cs` (RegisterAllSpells) |

**Mechanism:** `NormalizeBG3SpellId()` strips both the spell type prefix (`Zone_`, `Target_`, etc.) AND the trailing `_N` upcast suffix. All entries normalize to the same base ID. `Register(id, action, overwrite: false)` — first one wins, rest are discarded.

```
Zone_BurningHands       →  burning_hands
Zone_BurningHands_2     →  burning_hands   ← DROPPED
Zone_BurningHands_3     →  burning_hands   ← DROPPED
```

**Scale:** 4,698 raw entries collapse to 2,160 unique IDs. 1,243 collision groups. Top colliders: `hellish_rebuke` (18 entries), `ensnaring_strike` (18), `jump` (13), `scorching_ray` (10).

**What is lost:** Each upcast variant carries different BG3 data — different dice, different statuses, different area sizes. The heuristic in `SpellUpcastRules.CreateUpcastScaling()` applies a table-driven "+1 die per level" approximation which is wrong for spells that change die type, add conditions, or modify area at higher levels.

---

### C2 — Double-Halving of Save-Half Damage (300 AoE Spells Deal ¼ Damage on Save)

| Metric | Value |
|---|---|
| **Spells affected** | ~300 (every save-or-half AoE: Fireball, Burning Hands, Lightning Bolt, Shatter, Cone of Cold, etc.) |
| **Files** | `Data/Actions/SpellEffectConverter.cs` (ParseSingleEffect + ParseEffects), `Combat/Actions/Effects/DealDamageEffect.cs` (~L575, ~L638) |

**Mechanism:** BG3 encodes half-damage in the formula: `DealDamage(3d6/2, Fire)` in `SpellFail`. Two independent halvings occur:

1. `ParseSingleEffect()` extracts `/2` → sets `damageMultiplier = 0.5f`, strips divisor from dice string → `3d6`
2. `ParseEffects()` with `isFailEffect: true` → sets `SaveTakesHalf = true` on every fail-block effect

At runtime: `finalDamage *= 0.5 (multiplier) × 0.5 (SaveTakesHalf) = 0.25` — **quarter damage**.

---

### C3 — Upcasting Consumes Slot But Deals Base-Level Damage for 94% of Spells

| Metric | Value |
|---|---|
| **Spells affected** | ~1,435 (all leveled BG3 spells not in the 32-spell curated list) |
| **Files** | `Data/Actions/BG3ActionConverter.cs` (~L782 CreateUpcastScaling), `Data/Spells/SpellUpcastRules.cs` |

**Mechanism:** Only 32 spells have explicit upcast rules. For all others, `CreateUpcastScaling` tries to read `spell.Damage` — but this BG3 field appears **0 times** in all spell data files. So `DicePerLevel`, `ProjectilesPerLevel`, `TargetsPerLevel` are all null/0. The slot cost correctly escalates, but `ApplyUpcastToEffect` finds nothing to scale. The spell fires at base level from a higher slot.

---

### C4 — ~50 BG3 Functors Missing from SpellEffectConverter (500+ Effect Branches Silently Nil)

| Metric | Value |
|---|---|
| **Spells affected** | 500+ effect branches across the BG3 spell corpus |
| **Files** | `Data/Actions/SpellEffectConverter.cs` (ParseSingleEffect) |

**Mechanism:** `ParseSingleEffect()` returns `null` for unrecognized functors. `IF(condition, functor)` where the functor is unknown drops the **entire branch**.

Top missing functors by usage count:

| Functor | Uses | What breaks |
|---|---|---|
| `Tagged()` | 214 | Creature-type conditionals (Undead, Beast, etc.) |
| `ClassLevelHigherOrEqualThan()` | 178 | Class-level gating (Eldritch Blast invocations) |
| `HasPassive()` | 147 | Passive-gated effects (Potent Cantrip, Empowered Evocation) |
| `Player()` | 60 | Player-only branches |
| `HasMetalWeapon()` | 59 | Metal weapon restrictions |
| `HasActionResource()` | 58 | Resource-check gating |
| `ManeuverSaveDC()` | 54 | Battle Master maneuver DC |
| `WieldingWeapon()` | 48 | Weapon-conditional effects |
| `CharacterLevelGreaterThan()` | 36 | Character level scaling |

---

### C5 — AC Bonus Double-Application (Every AC-Buffing Status Grants 2× Bonus)

| Metric | Value |
|---|---|
| **Spells affected** | Every AC-modifying status (Shield of Faith, Shield, Mage Armor, Haste, Blur, etc.) |
| **Files** | `Data/Statuses/BG3StatusIntegration.cs` (~L235 ParseBoosts + ~L285 HandleStatusApplied), `Combat/Rules/RulesEngine.cs` (~L1587 GetArmorClass) |

**Mechanism:** Two independent paths both apply AC:

- **Path A (Legacy):** `ParseBoosts()` → `StatusModifier(ArmorClass, Flat, 2)` → injected via `RulesEngine.AddModifier()`
- **Path B (Boost):** `HandleStatusApplied()` → `BoostApplicator.ApplyBoosts()` → `ActiveBoost(BoostType.AC)` → `BoostEvaluator.GetACBonus()`

Both are summed in `GetArmorClass()`:
```csharp
var (finalAC, _) = mods.Apply(baseAC, ModifierTarget.ArmorClass, ...); // Path A
finalAC += BoostEvaluator.GetACBonus(combatant);                       // Path B
```

| Spell | Intended AC | Actual AC |
|---|---|---|
| Shield of Faith | +2 | **+4** |
| Shield (reaction) | +5 | **+10** |
| Haste | +2 | **+4** |

Characters are essentially unkillable, masking other bugs.

---

### C6 — 10+ Stub Functors in FunctorExecutor (25–30% of Non-Damage Spells Nonfunctional)

| Metric | Value |
|---|---|
| **Spells affected** | ~25–30% of all non-damage spell effects |
| **Files** | `Combat/Rules/Functors/FunctorExecutor.cs` (~L151–167) |

These functor types exist in the switch but only call `LogStub()`:

| FunctorType | What breaks |
|---|---|
| `Teleport` | Misty Step, all forced teleport effects |
| `SpawnSurface` | Grease, Spike Growth, Fog Cloud — any spell that leaves a surface |
| `CreateZone` | Spirit Guardians zone, Hunger of Hadar, Cloud of Daggers |
| `Explode` | Shatter blast effects, explosion-on-death |
| `Resurrect` | Revivify, Raise Dead |
| `Counterspell` | Counterspell reaction (functor path) |
| `UseSpell` | Spells that trigger sub-spells as effects |
| `FireProjectile` | Projectile-emitting status effects |
| `Douse` | Water-based extinguishing effects |
| `SummonInInventory` | Conjuration item creation |

Every BG3-parsed status whose `OnApply`/`OnStartPlaying`/`OnRemove` script calls any of these does nothing.

---

## Tier 2 — HIGH

### H1 — 95/153 BG3 Data Fields Have No Strongly-Typed Home (62% Parser Blind Spot)

| Metric | Value |
|---|---|
| **Files** | `Data/Parsers/BG3SpellParser.cs` (SetSpellProperty), `Data/Spells/BG3SpellData.cs` |

`SetSpellProperty()` handles 62 of 153 unique BG3 field keys. The other 95 land in `spell.RawProperties` (a `Dictionary<string,string>`) and are never read by `BG3ActionConverter`.

Most impactful unhandled fields:

| Field | Uses | Effect of ignoring |
|---|---|---|
| `SpellContainerID` | 757 | Container spell parent linkage lost |
| `ContainerSpells` | 439 | Container dispatch to variants broken |
| `RechargeValues` | 185 | Resource recharge mechanics missing |
| `CycleConditions` | 163 | Conditional hit cycling not modelled |
| `Requirements` | 149 | Casting gate requirements never enforced |
| `HitCosts` | 144 | On-hit resource cost missing |
| `SurfaceType` | 41 | Zone spells don't spawn surfaces |
| `ConcentrationSpellID` | 37 | Concentration status binding broken |
| `FollowUpOriginalSpell` | 34 | Multi-phase spell chains never chain |
| `AoEConditions` | 22 | AoE target-hit filtering not applied |
| `MaximumTotalTargetHP` | 23 | HP cap for control spells not enforced |

---

### H2 — Self-Centered Shout AoEs Unplayable by Player

| Metric | Value |
|---|---|
| **Spells affected** | ~27 Shout-type spells with AreaRadius (Healing Radiance, Spirit Guardians, Mass Cure Wounds) |
| **Files** | `Data/Actions/BG3ActionConverter.cs` (~L193), `Combat/Targeting/Modes/AoECircleMode.cs` |

**Mechanism:** Shout spells with `AreaRadius > 0` get `Range = 0f` (because `TargetRadius` is absent in Shout data). In `AoECircleMode.UpdatePreview`:
```csharp
bool inRange = distanceToCenter <= _action.Range;  // 0f: always false
```
Every cursor position shows "OUT OF RANGE". The player can never confirm. **AI path is unaffected** (has special-case Range≤0 handling).

---

### H3 — Zone Square Spells Fire as Single-Target When Hovering Creature

| Metric | Value |
|---|---|
| **Spells affected** | Thunderwave, Gust of Wind, and any Zone with Shape="Square" |
| **Files** | `Data/Actions/BG3ActionConverter.cs` (~L173), `Combat/Targeting/Modes/StraightLineMode.cs` |

**Mechanism:** Zone Square spells get `TargetType.Line` with `AreaRadius = 0`. The mode selection then picks `StraightLineMode` (instead of `AoELine`). When the player hovers a creature, `TryConfirm()` returns `ExecuteSingleTarget` with that creature's ID — only one creature takes the effect. Hovering ground correctly uses area targeting.

---

### H4 — CombineDiceFormulas Bug Silently Loses Dice When Die Types Differ

| Metric | Value |
|---|---|
| **Files** | `Combat/Actions/EffectPipeline.cs` (CombineDiceFormulas method) |

When combining `"2d6" + "1d4"` (upcast scaling with different die type): the method approximates the smaller die to an average flat bonus or falls through to `return formula1` — dropping the second formula entirely. Spells that upcast to a different die size silently lose scaling dice.

---

### H5 — CreateExplosionEffect Is a No-Op

| Metric | Value |
|---|---|
| **Files** | `Combat/Actions/Effects/CreateExplosionEffect.cs` (57 lines) |

`Execute()` dispatches a `RuleEvent` with `CustomType = "create_explosion"` and returns `Succeeded` with 0 damage. No damage dealt. No surface spawned. Any spell relying on `create_explosion` inflicts nothing.

---

### H6 — Unknown Effect Types Silently Skipped, Reported as Success

| Metric | Value |
|---|---|
| **Files** | `Combat/Actions/EffectPipeline.cs` (effect dispatch loop) |

```csharp
if (!_effectHandlers.TryGetValue(effectDef.Type, out var handler))
{
    GD.PushWarning($"Unknown effect type: {effectDef.Type}");
    continue;  // no EffectResult added
}
```

The overall `ActionExecutionResult.Success = true` is still returned. AI, HUD, and logging see a "successful" action with no effects. Silent failure masquerades as success.

---

## Tier 3 — MEDIUM

### M1 — Bonus Action Spell + Leveled Action Spell Rule Not Enforced

D&D 5e PHB p.202: if you cast a leveled spell as a bonus action, you can only cast cantrips with your action. **No tracking exists** — no `HasCastBonusActionSpell` flag in `ActionBudget`, no check in `EffectPipeline.CanUseAbility`. A player/AI can cast Healing Word (BA) + Fireball (Action) in the same turn.

**Files:** `Combat/Actions/ActionBudget.cs`, `Combat/Actions/EffectPipeline.cs`

---

### M2 — ConditionEvaluator Fail-Closed for Unknown Functions

Unknown BG3 condition functions in `IF(...)` clauses return `false`, silently preventing conditional boosts/passives from activating. Likely unimplemented functions:

- `HasFightingStyle` — breaks Fighting Style conditional bonuses
- `IsWeaponRangeType` — breaks range-specific weapon conditionals
- `HasLineOfSight` — breaks LOS-gated effects
- `HasWeaponGroup` — alternative weapon category checks

**Files:** `Combat/Rules/Conditions/ConditionEvaluator.cs` (~L1280)

---

### M3 — Wall SpellType Not Parsed (8 Wall Spells Become SingleTarget)

`BG3SpellParser.ParseSpellType()` has no `case "Wall"` — falls through to `BG3SpellType.Unknown` → `TargetType.SingleUnit`. The correct mapping `BG3SpellType.Wall → TargetType.WallSegment` exists in the converter but is unreachable.

**Affected:** Wall of Fire, Wall of Ice, Wall of Stone, Blade Barrier, Wall of Thorns, Cloudkill, Wind Wall, Wall of Force.

**Files:** `Data/Parsers/BG3SpellParser.cs` (ParseSpellType)

---

### M4 — ConcentrationSpellID Never Read (37 Concentration Spells)

`BG3SpellParser` has no `case "ConcentrationSpellID"` — it lands in `RawProperties` and is never mapped to `ActionDefinition.ConcentrationStatusId`. The `ConcentrationSystem` knows concentration is required but cannot identify which status to break.

**Files:** `Data/Parsers/BG3SpellParser.cs`, `Data/Actions/BG3ActionConverter.cs`

---

### M5 — Multi-Target Concentration Only Tracks First Status/Target

When a concentration spell applies statuses to multiple targets, `EffectPipeline` (~L1275) only records the first `apply_status` effect ID and first target. Breaking concentration removes only that first status/target — linked effects on additional targets persist.

**Files:** `Combat/Actions/EffectPipeline.cs` (~L1275)

---

### M6 — Range=0 Means Unlimited Targeting for Most Spells

`IsInAbilityRange()` returns `true` when `range ≤ 0` — unlimited targeting. Most BG3 spells whose `TargetRadius` is absent get `Range = 0f`, allowing targeting of enemies at any distance.

**Files:** `Combat/Targeting/TargetValidator.cs` (~L280)

---

### M7 — Parity Validation Gate Blind to All Above Issues

`ParityValidator` checks: registry init success, action reference resolution, status cross-references. It does **not** check: ID collision rate, upcast coverage, SpellType correctness, effect completeness, damage arithmetic, ConcentrationSpellID binding. Allowlist: 135 missing abilities + 73 missing status IDs suppressed.

**Files:** `Data/Validation/ParityValidator.cs`, `Data/Validation/parity_allowlist.json`

---

## Tier 4 — LOW

### L1 — Comma Separator in SplitFunctors Occasionally Mis-Parses

`SpellEffectConverter.SplitFunctors()` splits on `,` at depth 0. In rare BG3 formats this truncates multi-argument formulas.

### L2 — FunctorType.cs Enum Comments Are Stale

Comments list `BreakConcentration`, `Force`, `Stabilize`, `SetStatusDuration`, `UseAttack` as stubs — all five are actually implemented.

### L3 — Repeat Save DCs Default to 13

Status repeat-saves use DC 13 by default. Override happens in `ApplyStatusEffect` but edge paths (aura-applied, surface DoT) may miss it.

### L4 — Dead Targeting Modes

`BallisticArcMode`, `BezierCurveMode`, `PathfindProjectileMode`, and `ChainMode` are registered but no BG3 TargetType maps to them. Dead code.

### L5 — Path A Console Noise

`Data/Statuses/BG3StatusIntegration.cs` `ParseBoosts()` logs "Unsupported boost" for types that Path B handles correctly (RollBonus, WeaponDamage, UnlockSpell, etc.). Not a bug but floods logs, hiding real errors.

---

## What Works Correctly

| System | Status |
|---|---|
| `ComputeSaveDC` (8 + prof + ability mod) | ✅ Correct |
| `GetSavingThrowBonus` (ability mod + proficiency) | ✅ Correct |
| Auto-fail STR/DEX saves (Paralyzed, Stunned, etc.) | ✅ Correct |
| `DealDamageEffect` (crits, cantrip scaling, sneak attack) | ✅ Correct |
| `ApplyStatusEffect` (condition gates, duration tracking) | ✅ Correct |
| `DamagePipeline.Calculate` (5-stage: base→additive→%→resistance→absorption) | ✅ Correct |
| Concentration break on new cast | ✅ Correct |
| Concentration save DC (max(10, damage/2)) | ✅ Correct |
| Spell slot consumption (explicit + implicit paths) | ✅ Correct |
| Per-target save loop | ✅ Correct |
| Faction filtering in AoE target resolution | ✅ Correct |
| Self/All/None targeting bypass | ✅ Correct |
| Reaction budget handling | ✅ Correct |
| StatusTickProcessor (DoT damage) | ✅ Correct |
| Resistance from boost path | ✅ Correct |
| RollBonus from boost path (Bless/Bane) | ✅ Correct |
| `ResurrectEffect` (full implementation) | ✅ Correct |

---

## Recommended Fix Order

Priority is ranked by (impact × ease of fix):

| Priority | Issue | Est. Scope | Impact |
|---|---|---|---|
| **1** | C5 — AC double-application | 3-line deletion in `Data/Statuses/BG3StatusIntegration.cs` | Fixes all AC-buffing spells, restores combat balance |
| **2** | C2 — Double-halving of save damage | Single-file fix in `SpellEffectConverter.cs` | Fixes 300 AoE spells |
| **3** | M3 — Wall SpellType not parsed | 1-line fix in `BG3SpellParser.cs` | Fixes 8 Wall spells |
| **4** | M4 — ConcentrationSpellID unread | 3-file, ~5-line addition | Fixes 37 concentration spells |
| **5** | H2 — Shout AoE Range=0 | Small fix in AoECircleMode or BG3ActionConverter | Fixes ~27 player-cast Shout spells |
| **6** | H3 — Zone Square → StraightLine | Mode selection tweak | Fixes Thunderwave, Gust of Wind |
| **7** | C4 — Missing SpellEffectConverter functors | Systematic additions (by usage count) | Fixes 500+ effect branches |
| **8** | C6 — Stub functors in FunctorExecutor | Implement Teleport, SpawnSurface, CreateZone first | Fixes 25–30% of non-damage spells |
| **9** | C3 — Upcast scaling broken for 94% | Data extraction from BG3 variant entries or expanded curated rules | Fixes ~1,435 spells when upcasting |
| **10** | C1 — ID collision cascade | Redesign variant storage/linking | Fixes 1,260+ upcast entries |
| **11** | H1 — 95 unhandled BG3 fields | Incremental: add fields by impact (ContainerSpells, SurfaceType first) | Enables container spells, surface zones |
| **12** | M1 — BA spell rule | Add flag to ActionBudget + check in CanUseAbility | Rules accuracy |
| **13** | M7 — Parity gate coverage | Add validators for collision rate, effect completeness | CI coverage |
