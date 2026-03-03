# BG3 Combat Parity Gap Report

**Generated**: 2026-03-01 via CGC analysis + deep codebase exploration  
**Scope**: BG3 tactical combat parity as defined in AGENTS.md  
**Method**: Static analysis of 610 files, 1027 classes, 190k LOC; cross-referenced against BG3 combat mechanics

---

## Executive Summary

The project has strong **data parsing and storage** — nearly all BG3 spell, status, passive, and interrupt data is loaded. The **core turn loop**, **initiative**, **death saves**, **action budget**, and **basic attack flow** work end-to-end in auto-battles. However, the runtime **execution** of that data is riddled with stubs, fail-open paths, and unfinished pipelines. The gap between "data exists" and "mechanic actually works at runtime" is the central deficiency.

**Critical finding**: Many systems look complete from the outside (classes exist, methods exist, data loads) but have stub implementations, hardcoded values, or deferred subsystems that prevent correct BG3-faithful behavior. The ConditionEvaluator alone has a fail-open design where any unrecognized BG3 function returns `true` — meaning conditions silently pass when they should block.

---

## 1. CRITICAL GAPS (Mechanics that are broken or missing entirely)

### 1.1 Surface/Environment Effect Pipeline — NOT FUNCTIONAL

**Status**: Stub. Phase C was never completed.

- `SpawnSurfaceEffect.cs` is explicitly a stub: `"Stub implementation - emits event, surface system in Phase C"`.
- `FunctorExecutor.cs` has `SpawnSurface()` as a no-op stub that logs a warning.
- `ObscurementService.AddZone()` and `RemoveZone()` are marked TODO — spells like Darkness, Fog Cloud do NOT push obscurement zones.
- This means: **Fireball doesn't create fire surfaces. Grease doesn't create slippery ground. Web doesn't create web terrain. No spell dynamically alters the battlefield**.
- 34 surface definitions exist in `SurfaceManager` but can only be placed manually by scenario setup — not created by spell effects during combat.
- Surface interaction chains (ignite grease → fire, electrify water, freeze water → ice) exist in data but cannot be triggered by player actions.

**Impact**: One of BG3's most defining tactical combat features — environmental interaction — is entirely non-functional during gameplay.

### 1.2 Functor Execution — 10+ Stubs

These functor types are parsed from BG3 data and appear in spell definitions but **do nothing at runtime**:

| Functor | What it should do | Current state |
|---------|-------------------|---------------|
| `SpawnSurface` | Create ground/cloud effects | Stub: logs warning |
| `Teleport` | Move unit to position | Stub: logs warning |
| `UseSpell` | Chain-cast another spell | Stub: logs warning |
| `Resurrect` | Revive dead character | Stub: logs warning |
| `Counterspell` | Cancel enemy spell | Stub: logs warning |
| `CreateZone` | Create persistent AOE zone | Stub: logs warning |
| `FireProjectile` | Launch projectile | Stub: logs warning |
| `Douse` | Extinguish fire | Stub: logs warning |
| `SummonInInventory` | Create item in inventory | Stub: logs warning |
| `Explode` | Trigger explosion | Stub: logs warning |

**Impact**: Spells that rely on these functors (which is a large portion of BG3's spell list) either fail silently or produce incomplete effects. For example:
- Misty Step/Dimension Door → no teleportation
- Revivify/Raise Dead → no resurrection
- Wall of Fire → no surface creation
- Counterspell → partially works as a reaction but the functor itself is a stub
- Any spell that chains into another spell (e.g., Flame Strike = fire damage + radiant damage via sub-spell) → only partial execution

### 1.3 Condition Evaluator Fail-Open Design

`ConditionEvaluator.cs` (1675 lines, 60+ functions) returns `true` for any unknown BG3 condition function with a warning log. There is **no tracking** of how many conditions silently pass.

**Consequences**:
- Boost conditions that should restrict effects (e.g., "only when wielding a heavy weapon") may always evaluate to `true`
- Status application conditions may be applied when they shouldn't be
- Passive functor conditions may fire at wrong times
- Without an audit of which condition functions are stubs vs. implemented, the entire condition system's correctness is unknown

### 1.4 Barrier/Shield Absorption System — Not Implemented

`RulesEngine.cs` line 1180: `targetBarrier: 0 // Barrier system not yet implemented`. The damage pipeline has a slot for barrier absorption (Arcane Ward, Armor of Agathys temp-HP-like effects) but nothing feeds it.

### 1.5 Exhaustion System — L1 Only

Only Level 1 exhaustion (disadvantage on ability checks) is implemented. `ConditionEffects.cs` line ~313: "require ExhaustionLevel tracking which is not yet implemented."

Missing:
- L2: Speed halved
- L3: Disadvantage on attack rolls and saving throws
- L4: HP maximum halved
- L5: Speed reduced to 0
- L6: Death

---

## 2. MAJOR GAPS (Mechanics that exist but are incomplete or wrong)

### 2.1 Spell Coverage — Parsed ≠ Functional

- **1467 BG3 spell entries** are parsed from `.txt` files
- **427 supplementary JSON actions** are loaded
- But the actual **runtime execution** depends on:
  - Effect types (29 implemented — good)
  - Functor types (22 implemented, 10+ stubs — bad for complex spells)
  - Condition evaluation (fail-open — unknown correctness)
  - Surface spawning (stub — no environmental spells work)

**Unknown**: How many of those 1467 spells actually produce correct BG3-faithful results when cast. There is no parity test that validates spell outcomes against expected BG3 behavior. The `ParityValidator` checks data loading completeness, not mechanical correctness.

### 2.2 Counterspell — Hardcoded Level 3

`BG3ReactionIntegration.cs` line ~372: `context.Data["counterspellerLevel"] = 3; // Base level; higher-slot logic TBD`.

In BG3/5e:
- Counterspell at 3rd level auto-cancels spells of 3rd level or lower
- For higher-level spells, the caster must succeed on an ability check (DC = 10 + spell level)
- Casting Counterspell at a higher slot level increases the auto-cancel threshold

None of this logic exists. Counterspell always succeeds regardless of spell level.

### 2.3 Cutting Words — Hardcoded d8

`BG3ReactionIntegration.cs`: Cutting Words uses `rng.Next(1, 9)` (1d8 hardcoded).

In BG3/5e, Bardic Inspiration die scales:
- Bard levels 1-4: d6
- Bard levels 5-9: d8
- Bard levels 10-14: d10
- Bard levels 15+: d12

### 2.4 Charmed Condition — No Targeting Enforcement

`ConditionEffects.cs` marks charmed as having targeting validation needed (⚠️), but `TargetValidator.cs` does not check if a charmed combatant is targeting their charmer. In BG3, charmed creatures cannot attack the charmer.

### 2.5 Surprise Round — Completely Missing

No evidence of surprise detection, surprise conditions, or surprise round logic anywhere in the codebase. Searches for "surprise" return zero results in Combat/.

In BG3, surprise is a fundamental combat mechanic:
- Characters who don't notice enemies are surprised
- Surprised characters can't move or take actions on their first turn
- Surprised characters can't take reactions until their first turn ends
- Stealth/perception interaction determines surprise

### 2.6 Multiple Concentration Sources

`ConcentrationSystem` correctly enforces one concentration per combatant, but there is no tracking of what happens when an enemy Counterspells a concentration spell mid-cast (does the previous concentration drop before the new spell is confirmed?). The interaction between reaction timing and concentration management may have edge cases.

### 2.7 Wild Magic Surge — No Runtime Implementation

Wild Magic Sorcerer has `wild_magic_surge` as a class feature in the JSON data, but there is no runtime system that rolls on the Wild Magic table after casting a leveled spell. The feature is data-only.

### 2.8 Warlock Invocations — 6 of ~20+ Implemented

Only 6 invocations exist in `warlock_invocations.json`: Agonizing Blast, Repelling Blast, Devil's Sight, Mask of Many Faces, Eldritch Spear, Fiendish Vigor.

Missing BG3 invocations include: Armor of Shadows, Beast Speech, Beguiling Influence, Book of Ancient Secrets, Chains of Carceri, Dreadful Word, Eldritch Sight, Fiendish Resilience, Lifedrinker, Mire the Mind, Minions of Chaos, One with Shadows, Otherworldly Leap, Sign of Ill Omen, Thirsting Blade, Tomb of Levistus, Whispers of the Grave, Witch Sight.

### 2.9 Ability Checks & Skill Checks — Combat Context Missing

`RulesEngine` has `AbilityCheck` and `SkillCheck` roll types defined, but:
- No DC-based success/failure resolution for in-combat skill checks
- No Athletics vs Acrobatics contests (grapple, shove escape)  
- No Perception checks for detecting hidden/invisible enemies
- No Investigation/Insight checks for illusion interaction

### 2.10 Movement System Gaps

- **Jump visualization**: `JumpPathfinder3D` exists but is not integrated with UI — no arc preview for players
- **Cunning Action**: No system for Rogues to Dash/Disengage/Hide as bonus actions with special movement integration
- **Step of the Wind**: Same issue for Monks — bonus action Dash/Disengage not movement-system-aware
- **Ready Action**: No "hold action until trigger" mechanic — the state machine doesn't support deferred actions
- **Acrobatics checks**: No skill check when forced movement occurs (to resist being knocked back)
- **Difficult terrain immunity**: Monks (level 6) and some spells should ignore difficult terrain; no boost/status for this

### 2.11 Height System — AC Modifier Instead of Advantage

`HeightService.cs` applies +2/-2 AC for high/low ground instead of advantage/disadvantage on attack rolls. BG3 uses advantage/disadvantage. The current implementation is a workaround, not BG3-accurate.

---

## 3. MODERATE GAPS (Systems that work but lack BG3 fidelity)

### 3.1 AI Surface/Environment Awareness

`AIMovementEvaluator.cs` has multiple TODOs for environmental awareness:
- Line 572: "BG3 MULTIPLIER_ENDPOS_NOT_IN_DANGEROUS_SURFACE needs real surface detection"
- Line 582: "integrate real smoke detection; placeholder"
- Line 586: "integrate real hazardous item detection"
- Line 590: "integrate real ledge detection"
- Line 871: "integrate real blocker detection"

`AIScorer.cs` TODOs:
- Line 167: "Target obscurement scoring (ObscurementService integration)"
- Line 1422: "Full combo detection requires checking BG3AISurfaceComboDefinition entries"

The AI cannot evaluate surface/environmental tactics.

### 3.2 OA Should Only Apply on Hit

`CombatMovementCoordinator.cs` line 365: `"TODO: this should only apply when the OA attack actually hits."` Currently movement stops when OA is triggered regardless of whether the attack hits. In BG3/5e, only a successful OA should stop movement (via Sentinel); a missed OA should not prevent further movement.

### 3.3 Death System — NPC Behavior Nuances

While death saves work for PCs and NPCs die instantly at 0 HP (correct for BG3), there are edge cases:
- No Spare the Dying cantrip functionality (no stabilize action beyond the effect type)
- No auto-death on massive damage for PCs (damage ≥ remaining HP + max HP in a single hit)
- No tracking of unconscious characters being targeted for melee auto-crits (the condition exists but combat flow may not preserve targeting)

### 3.4 Passive Feature Execution Depth

418 passives are parsed and loaded. PassiveManager grants boosts and registers rule providers. However:
- Boost conditions only handle 2 specific patterns (`WearingArmor()`, `HasHeavyArmor()`); compound/unknown conditions default to `true`
- No audit of how many passives actually produce correct behavior vs. how many silently apply boosts unconditionally
- Toggle passives have state tracking but functor execution on toggle is not auto-trigged through the UI

### 3.5 LOS/Cover Gaps

- No height-based cover reduction (BG3: targets below you get less cover)
- No corner-peeking (BG3: can target around cover from certain angles)
- No cover from AoE (AoE spells currently ignore cover)
- No blind fire into fog/darkness (currently fully blocked instead of disadvantage)
- No concealment vs cover distinction (fog = concealment in D&D, enemies = cover)

### 3.6 Deafened Condition — Stub

`ConditionEffects.cs`: Deafened is registered but has no mechanical effect beyond the status icon. In BG3/5e, deafened creatures auto-fail hearing-based Perception checks and are immune to spells requiring verbal components they can hear.

### 3.7 Fog of War

`DebugPanel.cs` line 656-658: "Fog of War not yet implemented." No fog of war system exists. In BG3, unexplored areas are hidden and enemy positions are only visible within line of sight.

### 3.8 Subclass Feature Execution

46+ subclasses are defined in class JSONs with features, but the **runtime behavior** of most subclass features depends on:
- Passive functor execution (partially working)
- Specific rule providers being registered
- Correct condition evaluation (fail-open)
- Feature-granted abilities existing in `ActionRegistry`

No systematic test validates that each subclass feature actually produces correct combat behavior. The `parity_allowlist.json` only tracks missing action IDs for beast forms and a few niche abilities.

---

## 4. MINOR GAPS (Polish and edge cases)

### 4.1 UI Completion

- Character sheet modal: data structure ready, rendering not implemented
- Character creation: 6-step flow scaffolded, backend integration pending
- Drag-drop in inventory: layout present, operation handlers incomplete
- Tooltip positioning: infrastructure exists, precise BG3-style positioning TBD
- Portrait system: random assignment; proper character-specific portraits not implemented (4+ TODOs)

### 4.2 Save/Load Gaps

- `CombatSaveService.cs` line 381: "TODO: Re-equip items from snapshot.EquipmentSlots"
- Line 388: "TODO: Persist portrait path in save data instead"
- Equipment is not properly restored on load

### 4.3 Persistence/Consumable Tracking

- No tracking of consumed potions, scrolls, or limited-use items across encounters
- No tracking of Wild Shape uses, Channel Divinity uses between encounters (out of scope per "resting outside combat" exclusion, but worth noting)

### 4.4 Roll Forcing

`DebugPanel.cs` line 580: "Roll forcing not yet fully implemented in RulesEngine" — debug panel can't force specific roll outcomes for testing.

---

## 5. SYSTEMIC RISKS

### 5.1 The "Fail-Open" Problem

Multiple systems default to permissive behavior when they encounter unknown data:
- `ConditionEvaluator`: unknown function → `true`
- `PassiveManager`: unknown boost conditions → `true`
- `FunctorExecutor`: unknown functor → log warning, skip execution
- `StatusPresentationPolicy`: unknown display names → stub placeholder

This means the system *appears to work* even when major mechanics are absent. A spell might "cast" successfully but produce incorrect or incomplete effects without any visible error.

### 5.2 No Spell Outcome Validation

There is no test that verifies "Fireball at level 3 targeting these coordinates produces exactly these effects on these targets with correct damage amounts, saving throws, and surface creation." The parity validator checks data presence, not mechanical correctness.

### 5.3 Action Coverage Is Unknowable

With 1467 parsed spells + 427 JSON actions, there's no mechanism to determine how many actually work correctly at runtime. Estimated breakdown:
- **Works correctly**: Basic attacks, simple single-target damage spells, simple healing, basic status application
- **Partially works**: Multi-target, AoE damage (no surface creation), upcasting (scaling exists but data incomplete)
- **Does not work**: Any spell requiring surface spawning, teleportation, chain-casting, resurrection, zone creation, or complex functor chains

### 5.4 Subclass Feature Verification Gap

46+ subclasses with dozens of features each. No test validates that (for example) an Evocation Wizard's Sculpt Spells actually excludes allies from AoE damage, or that a Divination Wizard's Portent actually replaces a roll. These features exist as JSON data and passives but the runtime wiring is untested.

---

## 6. QUANTITATIVE SUMMARY

| Category | Have | Need for BG3 Parity | Gap |
|----------|------|---------------------|-----|
| Spell entries parsed | 1467 | ~1467 | Data complete |
| Spell entries functionally verified | Unknown | ~1467 | **Unknown — no validation** |
| Effect types | 29 | ~29 | Complete |
| Functor types (working) | 22 | ~32+ | **10+ stubs** |
| D&D conditions | 16 | 16 | Mostly complete (exhaustion L2-6 missing) |
| Reactions | 13 | ~20+ | Missing: Riposte, Parry, Shield Master, Divine Smite timing |
| Surfaces (defined) | 34 | 34 | Data complete |
| Surfaces (spawnable by spells) | 0 | 34 | **All missing at runtime** |
| Cover levels | 3 | 3 | Present but missing height interaction |
| Classes | 12 | 12 | Complete |
| Subclasses | 46+ | 46+ | Data complete, runtime unverified |
| Feats | 45+ | 45+ | Data complete, runtime unverified |
| Warlock invocations | 6 | 20+ | **14+ missing** |
| Surprise mechanics | 0 | Full system | **Missing** |
| Fog of war | 0 | Full system | **Missing** |
| Ready action | 0 | Full system | **Missing** |
| Ability/skill contests | 0 | Grapple, shove escape, etc. | **Missing** |

---

## 7. PRIORITY RANKING FOR CLOSING GAPS

### P0 — Without these, combat is fundamentally unlike BG3
1. **Surface spawning pipeline** — wire `SpawnSurfaceEffect` and `CreateZone` functors to `SurfaceManager.CreateSurface()`. This unblocks all environmental spells.
2. **Teleport functor** — wire to `ForcedMovementService.Teleport()` (which already exists). Unblocks Misty Step, Dimension Door, Thunder Step.
3. **Functor execution for remaining stubs** — `UseSpell`, `FireProjectile`, `Explode` at minimum.

### P1 — Major mechanical inaccuracies
4. **ConditionEvaluator audit** — catalog every function that returns `true` as a stub; implement or explicitly mark each one.
5. **Counterspell level scaling** — replace hardcoded level 3 with proper spell level comparison and ability check.
6. **Height advantage → advantage/disadvantage** — replace +2/-2 AC with proper advantage/disadvantage on attack rolls.
7. **Surprise round** — implement surprise detection using stealth vs passive perception.
8. **Exhaustion levels 2-6**.

### P2 — Important for class identity
9. **Warlock invocations** (14+ missing).
10. **Ready action** (hold action until trigger).
11. **OA hit check** (only stop movement on successful attack).
12. **Athletic/Acrobatics contests** (grapple, shove escape).
13. **Subclass feature smoke tests** — at minimum verify the top 10 most-used subclass features work.

### P3 — Polish and completeness
14. **Fog of war**.
15. **LOS/cover refinements** (height-based cover, AoE cover, blind fire).
16. **Character sheet rendering**.
17. **Portrait system**.
18. **Save/load equipment restoration**.
