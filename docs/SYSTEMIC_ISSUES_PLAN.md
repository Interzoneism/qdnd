# Plan: Action/Spell Systemic Issues Fix

## TL;DR
Fix 13 systemic issues in the BG3 action/spell pipeline, ordered by (impact × ease). Six phases, from quick arithmetic/data fixes through major infrastructure changes. Each phase is independently verifiable. Fixes ~63% of broken spell entries, restoring combat balance and upcast fidelity.

---

## Phase 1 — Arithmetic & Double-Application Bugs (3 issues, ~30 min)

These are the highest-impact, lowest-effort fixes. Each is a small, surgical change that immediately restores correct behavior for hundreds of spells.

### Step 1.1: C5 — AC Double-Application
**What:** Remove the legacy Path A (`StatusModifier` for `ArmorClass`) in `Data/Statuses/BG3StatusIntegration.cs`. Path B (boost system via `BoostApplicator` → `BoostEvaluator.GetACBonus()`) is the correct, modern path.
**How:** In `ParseBoosts()` (~L235), stop emitting `StatusModifier(ArmorClass, Flat, N)` entries. The simplest approach: in the switch block inside `ParseBoosts()`, remove or skip the `case "AC"` / `case "ArmorClass"` branch that creates `StatusModifier` objects. Path B already handles AC boosts correctly.
**Risk:** Check if any non-status code path relies on `StatusModifier` for AC. The RulesEngine `GetArmorClass()` sums both modifier-applied AC and `BoostEvaluator.GetACBonus()` — removing the modifier path means only the boost path contributes.
**Files:**
- [Data/Statuses/BG3StatusIntegration.cs](Data/Statuses/BG3StatusIntegration.cs) — remove AC case in `ParseBoosts()`
- [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs) — verify `GetArmorClass()` still works with only boost path
**Tests:** Update/add cases in `Tests/Unit/BG3StatusIntegrationTests.cs` and `Tests/Unit/BoostRulesEngineIntegrationTests.cs` confirming Shield of Faith gives +2 (not +4), Shield gives +5 (not +10).

### Step 1.2: C2 — Double-Halving of Save-Half Damage
**What:** BG3 encodes half-damage in the formula (`DealDamage(3d6/2, Fire)` in `SpellFail`). The parser extracts `/2` as `damageMultiplier=0.5` AND sets `SaveTakesHalf=true`. Runtime applies both → quarter damage.
**How:** In `SpellEffectConverter.ParseEffects()`, when `isFailEffect == true` and effect type is `damage`, remove the `damageMultiplier` key from `effect.Parameters`. The `/2` is BG3's encoding of "save for half", not a separate multiplier.
**Files:**
- [Data/Actions/SpellEffectConverter.cs](Data/Actions/SpellEffectConverter.cs) — in `ParseEffects()` fail-effect block (~L107-115), add `effect.Parameters.Remove("damageMultiplier")` after setting `SaveTakesHalf = true`
**Tests:** Update `Tests/Unit/SaveTakesHalfTests.cs` — add test that Fireball-like spells (with `/2` in SpellFail) produce half damage on save, not quarter.

### Step 1.3: H4 — CombineDiceFormulas Drops Dice on Different Die Types
**What:** When combining `"2d6" + "1d4"`, the method approximates the smaller die as an average flat bonus or falls through returning `formula1` unchanged.
**How:** Change `CombineDiceFormulas` in `EffectPipeline.cs` to concatenate different-die-type formulas as `"2d6+1d4"` string (pass-through) instead of lossy averaging. The downstream `DiceRoller` already handles multi-term dice strings.
**Files:**
- [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) — rewrite the different-die-type branch (~L1580-1606) to return `$"{formula1}+{formula2}"` or equivalent honest concatenation
**Tests:** Add unit test in `Tests/Unit/EffectPipelineIntegrationTests.cs` for combining different die types.

**Phase 1 Verification:**
1. `dotnet test Tests/QDND.Tests.csproj` — all existing + new tests pass
2. `./scripts/ci-build.sh` — clean build
3. `./scripts/run_autobattle.sh --seed 42 --seed 123 --seed 456` — verify combat balance is noticeably changed (enemies should die at plausible rates, no more unkillable AC-buffed units)

---

## Phase 2 — Parser Data Gaps (4 issues, ~1-2 hr)

Small parser additions that unlock large categories of spells.

### Step 2.1: M3 — Wall SpellType Not Parsed
**What:** `BG3SpellParser.ParseSpellType()` has no `case "wall"` → falls to `Unknown` → becomes `SingleTarget`.
**How:** Add `"wall" => BG3SpellType.Wall` to the switch expression. The `BG3SpellType.Wall` enum value already exists, and the converter already maps `Wall → TargetType.WallSegment`.
**Files:**
- [Data/Parsers/BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) — add case in `ParseSpellType()` switch
**Tests:** Add test in spell parser tests that `ParseSpellType("Wall")` returns `BG3SpellType.Wall`.

### Step 2.2: M4 — ConcentrationSpellID Never Read
**What:** `SetSpellProperty()` has no `case "ConcentrationSpellID"` — lands in `RawProperties`, never mapped to `ActionDefinition.ConcentrationStatusId`.
**How:** 
1. Add `case "ConcentrationSpellID": spell.ConcentrationSpellID = value; break;` to `SetSpellProperty()` in BG3SpellParser
2. Add `public string? ConcentrationSpellID { get; set; }` to `BG3SpellData`
3. In `BG3ActionConverter.ConvertToAction()`, map `spell.ConcentrationSpellID → action.ConcentrationStatusId`
**Files:**
- [Data/Parsers/BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) — add case
- [Data/Spells/BG3SpellData.cs](Data/Spells/BG3SpellData.cs) — add property
- [Data/Actions/BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs) — add mapping in ConvertToAction
**Tests:** Add test verifying ConcentrationSpellID flows through parser → converter → ActionDefinition.

### Step 2.3: H2 — Shout AoE Range=0 Unplayable by Player
**What:** Shout spells with `AreaRadius > 0` get `Range = 0f` (no `TargetRadius`). `AoECircleMode` rejects all positions as "OUT OF RANGE".
**How:** In `BG3ActionConverter`, when a Shout spell has `AreaRadius > 0` but `Range == 0`, set `Range = AreaRadius` (self-centered AoE should use the area radius as its effective range). Alternatively, fix `AoECircleMode.UpdatePreview()` to treat Range≤0 for self-centered AoEs as "always in range".
**Decision:** Fix in the converter (set Range = AreaRadius for Shout+AreaRadius combos) — this is cleaner than special-casing the targeting mode, and matches BG3 intent (self-centered AoE with defined area).
**Files:**
- [Data/Actions/BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs) — in the Shout targeting type mapping (~L193), when `AreaRadius > 0`, set `Range = AreaRadius`
**Tests:** Add test that Shout spells with AreaRadius get Range = AreaRadius.

### Step 2.4: H3 — Zone Square→StraightLine Fires as Single-Target on Hover
**What:** Zone spells with `ZoneShape="Square"` map to `TargetType.Line` with `AreaRadius=0`. StraightLineMode then fires as single-target when hovering a creature.
**How:** Map ZoneShape="Square" to `TargetType.Cone` or `TargetType.Circle` with appropriate AreaRadius calculated from the BG3 square dimensions. The Zone Base/Range values should give us the square dimensions.
**Decision:** Map to `TargetType.Cone` with `AreaAngle=90` and `AreaRadius` derived from `ZoneBase`/`ZoneRange`. This gives the closest match to BG3's square projection.
**Files:**
- [Data/Actions/BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs) — change Zone Square mapping to use Cone or Circle with proper dimensions
**Tests:** Add test that Thunderwave converts to an area targeting type (not Line).

**Phase 2 Verification:**
1. `dotnet test Tests/QDND.Tests.csproj`
2. `./scripts/ci-build.sh`
3. For M3: Verify wall spells appear in ActionRegistry with TargetType.WallSegment
4. For H2: Run full-fidelity test, cast a Shout AoE spell with player — should target normally

---

## Phase 3 — Runtime Correctness (4 issues, ~2-3 hr)

Fixes for runtime behavior issues that silently produce wrong results.

### Step 3.1: H5 — CreateExplosionEffect Is a No-Op
**What:** `Execute()` dispatches a RuleEvent and returns Success with 0 damage.
**How:** Implement actual explosion logic: resolve the referenced spell, spawn an AoE at the explosion position, deal damage to all targets in radius. Use `DealDamageEffect` as the reference pattern for damage delivery.
**Files:**
- [Combat/Actions/Effects/CreateExplosionEffect.cs](Combat/Actions/Effects/CreateExplosionEffect.cs) — implement real explosion: look up the referenced spell, resolve area targets, deal damage
- May need to reference `ActionRegistry` to resolve the sub-spell
**Depends on:** Nothing in prior phases
**Tests:** Add test that `CreateExplosionEffect` deals damage to targets in radius.

### Step 3.2: H6 — Unknown Effect Types Silently Reported as Success
**What:** Unknown effect types are skipped with `continue`, but `result.Success` stays `true`.
**How:** After the effect dispatch loop, check if `result.EffectResults.Count == 0` and all effects were unknown — if so, set `result.Success = false` and add a warning. This ensures AI and HUD see the failure.
**Files:**
- [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) — after the dispatch loop (~L1260), add check: if all effects were unhandled, mark result as failed
**Risk:** Some legitimate actions may have 0 effects (pure status-toggle actions?). Check for this edge case.
**Tests:** Add test that an action with only unknown effect types returns `Success = false`.

### Step 3.3: M5 — Multi-Target Concentration Only Tracks First Target
**What:** `ConcentrationInfo` stores single `TargetId`, losing tracking for multi-target concentration spells.
**How:** Change `ConcentrationInfo` to store `List<string> TargetIds` instead of single `TargetId`. Update `EffectPipeline` (~L1275) to pass all target IDs. Update `ConcentrationSystem.BreakConcentration()` to remove statuses from all tracked targets.
**Files:**
- `Combat/Statuses/ConcentrationSystem.cs` — update `ConcentrationInfo` struct, update break logic
- [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) — pass all targets into concentration tracking
**Tests:** Add test that breaking concentration on a multi-target spell removes status from all targets.

### Step 3.4: M1 — Bonus Action Spell + Leveled Action Spell Rule
**What:** No enforcement of D&D 5e rule: if you cast a leveled spell as BA, you can only cast cantrips as your action.
**How:** Add `HasCastLeveledBonusActionSpell` bool to `ActionBudget`. Set it in `EffectPipeline` when a leveled spell is cast as bonus action. Check it in `CanUseAbility` — if true, only cantrips (Level=0) allowed for action-cost spells.
**Files:**
- [Combat/Actions/ActionBudget.cs](Combat/Actions/ActionBudget.cs) — add tracking flag
- [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) — set flag when casting leveled BA spell, check in CanUseAbility
**Tests:** Add test in `Tests/Unit/ActionBudgetTests.cs` verifying the rule is enforced.

**Phase 3 Verification:**
1. `dotnet test Tests/QDND.Tests.csproj`
2. `./scripts/ci-build.sh`
3. `./scripts/run_autobattle.sh --full-fidelity --seed 42` — full round of combat completes without regression

---

## Phase 4 — SpellEffectConverter Functor Expansion (C4, ~3-5 hr)

Systematic addition of missing BG3 condition functors in `SpellEffectConverter.ParseSingleEffect()`. Prioritized by usage count.

### Step 4.1: Condition/Boolean Functors (top 9 by usage)
These are NOT effect functors — they are **condition check functions** used in `IF(condition, effect)` branches. When unrecognized, the entire branch is dropped.

**Implementation approach:** Most of these should be recognized as condition-only tokens that evaluate to `true` (allowing the effect branch) or should be wired to the existing `ConditionEvaluator`. They don't produce effects themselves.

| Functor | Uses | Implementation |
|---------|------|----------------|
| `Tagged()` | 214 | Check creature tags against `Combatant.Tags` |
| `ClassLevelHigherOrEqualThan()` | 178 | Query `CharacterSheet.GetClassLevel()` |
| `HasPassive()` | 147 | Check `PassiveManager.HasPassive()` |
| `Player()` | 60 | Check `combatant.IsPlayer` |
| `HasMetalWeapon()` | 59 | Check equipped weapon material properties |
| `HasActionResource()` | 58 | Check `ResourcePool` |
| `ManeuverSaveDC()` | 54 | Compute 8 + prof + STR/DEX mod |
| `WieldingWeapon()` | 48 | Check `InventoryService.GetEquippedWeapon()` |
| `CharacterLevelGreaterThan()` | 36 | Query `CharacterSheet.Level` |

**Files:**
- [Data/Actions/SpellEffectConverter.cs](Data/Actions/SpellEffectConverter.cs) — add recognition for each functor in the condition-parsing section
- May need to update `ConditionEvaluator.cs` if these are evaluated at runtime rather than parse time

### Step 4.2: Effect Functors Not Yet Handled in Parser
Some of these ARE effect-producing functors that need to generate `EffectDefinition` objects:
- Consider which ones already have matching `EffectType` handlers vs which need both parser + runtime implementation

**Files:**
- [Data/Actions/SpellEffectConverter.cs](Data/Actions/SpellEffectConverter.cs)

**Phase 4 Verification:**
1. `dotnet test` — no regressions
2. Run a data audit: log how many spells now have all effects parsed vs before the change. Compare total `null` returns from `ParseSingleEffect`.

---

## Phase 5 — FunctorExecutor Stub Implementation (C6, ~5-10 hr)

Implement the 10 stub functors in priority order. Each is a substantial piece of work.

### Step 5.1: SpawnSurface (highest value — enables Grease, Spike Growth, Fog Cloud)
**What:** Create a surface via `SurfaceManager.CreateSurface()` at the target position with the specified type, radius, and duration.
**Files:**
- [Combat/Rules/Functors/FunctorExecutor.cs](Combat/Rules/Functors/FunctorExecutor.cs)
- [Combat/Environment/SurfaceManager.cs](Combat/Environment/SurfaceManager.cs)

### Step 5.2: CreateZone (Spirit Guardians, Cloud of Daggers, Hunger of Hadar)
**What:** Create a persistent zone that moves with its owner or stays at a location.
**Depends on:** SurfaceManager integration patterns from Step 5.1

### Step 5.3: Teleport (Misty Step, forced teleports)
**What:** Relocate a combatant to a target position, respecting collision and movement rules.
**Files:**
- [Combat/Rules/Functors/FunctorExecutor.cs](Combat/Rules/Functors/FunctorExecutor.cs)
- [Combat/Movement/MovementService.cs](Combat/Movement/MovementService.cs)

### Step 5.4: UseSpell (spells triggering sub-spells)
**What:** Look up a spell in ActionRegistry and execute it as a sub-action via EffectPipeline.
**Risk:** Recursion depth — need a max-depth guard.

### Step 5.5: Explode (Shatter blast, explosion-on-death)
**What:** Similar to CreateExplosionEffect (Step 3.1). Deal AoE damage at a point.
**Depends on:** Step 3.1 (CreateExplosionEffect implementation can be reused)

### Step 5.6: Resurrect (Revivify, Raise Dead)
**Note:** The issues doc lists `ResurrectEffect` as ✅ working in the "What Works" section. The stub may only be the functor path (status `OnApply` triggering resurrect). Verify whether `ResurrectEffect` and the `Resurrect` functor are different code paths.

### Step 5.7–5.10: Remaining stubs (Counterspell, FireProjectile, Douse, SummonInInventory)
Lower priority — implement as time permits.

**Phase 5 Verification:**
1. `dotnet test` for each implemented functor
2. `./scripts/run_autobattle.sh --full-fidelity --seed 42` — verify surface/zone spells work
3. `./scripts/ci-godot-log-check.sh` — no new script errors

---

## Phase 6 — Upcast & ID Collision Infrastructure (C1, C3, ~10-20 hr)

The largest and most complex changes. These require design decisions about data structures.

### Step 6.1: C3 — Fix Upcast Scaling for 94% of Spells
**What:** `CreateUpcastScaling()` can't read `spell.Damage` (field never populated). Need an alternative data source.
**Approach options:**
- **Option A (data-driven):** Extract upcast data from BG3 variant entries BEFORE they're collapsed by NormalizeBG3SpellId. Compare level N+1 variant to level N to compute deltas. Store these as upcast rules.
- **Option B (heuristic expansion):** Expand the 32-spell curated list to cover the top 100 most-used spells. Manual but accurate.
- **Option C (hybrid):** Use Option A to auto-generate rules, then manually verify/override the top spells.
**Recommendation:** Option C — auto-extract from variants, human-verify top 50.
**Files:**
- [Data/Spells/SpellUpcastRules.cs](Data/Spells/SpellUpcastRules.cs)
- [Data/Actions/BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs)
- [Data/Actions/ActionDataLoader.cs](Data/Actions/ActionDataLoader.cs) — need to process variants before collapsing

### Step 6.2: C1 — ID Collision Cascade Redesign
**What:** `NormalizeBG3SpellId()` strips level suffixes, collapsing 4,698 entries to 2,160. Need to preserve variant data.
**Approach:** 
1. Keep the base ID normalization for the primary action lookup
2. Store variant entries in a `Dictionary<string, List<BG3SpellData>>` keyed by base ID
3. Use variant data to compute upcast deltas (feeds Step 6.1)
4. Optionally register variants as separate actions with qualified IDs (`burning_hands_2`, `burning_hands_3`)
**Files:**
- [Data/Spells/SpellUpcastRules.cs](Data/Spells/SpellUpcastRules.cs) — NormalizeBG3SpellId behavior change
- [Data/Actions/ActionDataLoader.cs](Data/Actions/ActionDataLoader.cs) — variant collection before registration
- New: variant storage structure (could be in ActionRegistry or a companion registry)

### Step 6.3: H1 — Add Priority BG3 Parser Fields
**What:** 95/153 BG3 fields unhandled. Add the highest-impact ones incrementally.
**Priority fields:** `ContainerSpells` (439 uses), `SpellContainerID` (757 uses), `SurfaceType` (41 uses), `ConcentrationSpellID` (37, done in Phase 2), `AoEConditions` (22), `MaximumTotalTargetHP` (23).
**Files:**
- [Data/Parsers/BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) — add cases to SetSpellProperty
- [Data/Spells/BG3SpellData.cs](Data/Spells/BG3SpellData.cs) — add typed properties
- [Data/Actions/BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs) — map new fields to ActionDefinition

**Phase 6 Verification:**
1. `dotnet test` — all tests pass
2. Registry audit: count registered actions before/after. Expect increase from ~2,160 toward ~4,000+
3. Upcast integration test: cast Fireball at level 4, verify 9d6 (not 8d6)
4. `./scripts/run_autobattle.sh --full-fidelity --seed 42`

---

## Cross-Phase: Parity Validation (M7)

After each phase, expand `ParityValidator` to catch the class of bug just fixed:
- Phase 1: Add validator for AC single-application, save-half arithmetic
- Phase 2: Add validator for Wall spell type, ConcentrationSpellID coverage
- Phase 4: Add validator for effect completeness (% of spells with all effects parsed)
- Phase 6: Add validator for ID collision rate, upcast coverage

**Files:**
- [Data/Validation/ParityValidator.cs](Data/Validation/ParityValidator.cs)
- [Data/Validation/parity_allowlist.json](Data/Validation/parity_allowlist.json) — update as issues are fixed

---

## Relevant Files Summary

| File | Phases | Changes |
|------|--------|---------|
| `Data/Statuses/BG3StatusIntegration.cs` | 1 | Remove AC legacy path |
| `Data/Actions/SpellEffectConverter.cs` | 1, 4 | Fix double-halving; add missing functors |
| `Combat/Actions/EffectPipeline.cs` | 1, 3 | Fix CombineDiceFormulas; fix unknown-effect success; fix concentration tracking |
| `Data/Parsers/BG3SpellParser.cs` | 2, 6 | Add Wall case; add ConcentrationSpellID; add priority fields |
| `Data/Spells/BG3SpellData.cs` | 2, 6 | Add typed properties |
| `Data/Actions/BG3ActionConverter.cs` | 2, 6 | Fix Shout range; fix Zone Square; map new fields; upcast |
| `Combat/Actions/Effects/CreateExplosionEffect.cs` | 3 | Implement real explosion |
| `Combat/Actions/ActionBudget.cs` | 3 | Add BA spell tracking |
| `Combat/Statuses/ConcentrationSystem.cs` | 3 | Multi-target concentration |
| `Combat/Rules/Functors/FunctorExecutor.cs` | 5 | Implement 10 stub functors |
| `Data/Spells/SpellUpcastRules.cs` | 6 | Variant-aware upcast extraction |
| `Data/Actions/ActionDataLoader.cs` | 6 | Variant collection before registration |
| `Data/Validation/ParityValidator.cs` | All | Add validators per phase |

---

## Verification Strategy

After each phase:
1. `./scripts/ci-build.sh` — clean compile
2. `dotnet test Tests/QDND.Tests.csproj` — all tests pass
3. `./scripts/ci-godot-log-check.sh` — no script errors
4. Phase-specific verification (listed per phase above)

End-to-end after all phases:
5. `./scripts/run_autobattle.sh --full-fidelity --seed 42` — full combat completes
6. `./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20` — stress test 10 seeds
7. Run ParityValidator — reduced allowlist size, increased coverage metrics

---

## Decisions & Scope

- **Included:** All 13 issues from the systemic report (C1-C6, H1-H6, M1-M7 — noting L1-L5 are low priority, tracked but not planned)
- **Excluded:** L1-L5 (low priority edge cases/cosmetic) — can be addressed opportunistically
- **Excluded:** New targeting modes (BallisticArc, Bezier, etc.) — dead code, no BG3 mapping
- **Assumption:** The boost/BoostEvaluator AC path is the correct sole authority for AC bonuses from statuses
- **Assumption:** `DiceRoller` already handles multi-term dice strings like `"2d6+1d4"` for CombineDiceFormulas fix
- **Risk:** Phase 6 (upcast/ID collision) is the riskiest — may need iterative design. Start with variant collection and validate before committing to full redesign.
