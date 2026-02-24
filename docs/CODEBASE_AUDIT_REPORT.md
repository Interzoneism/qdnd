# QDND Codebase Audit Report

**Date:** 2025  
**Scope:** Full system-by-system audit of the BG3-parity combat engine  
**Plan reference:** `plans/DeepMechanicalOverhaulPlan.md` (all 10 phases marked ✅ DONE)  
**Parity baseline:** `docs/BG3_COMBAT_PARITY_AUDIT.md` (10 completed sessions, ~84% overall parity)

---

## Executive Summary

The engine is architecturally sound. All 10 plan phases are complete: legacy classes (`CombatantStats`, `EquipmentLoadout`, `CombatantResourcePool`, `PassiveRuleService`) are deleted, `CombatArena` is decomposed into 8 services (1841 lines, down from ~6600), and the BG3 data pipeline (LSX/TXT → registries) is fully wired. No failing build gates.

**Critical mechanical bugs remaining (not addressed by plan):**

| Severity | Issue | Location |
|----------|-------|----------|
| 🔴 HIGH | Initiative is never rolled — static JSON integers | `TurnQueueService`, all scenario JSON |
| 🔴 HIGH | `MELEE_RANGE = 5f` (5 meters = 16 ft; BG3 = 1.5 m) | `MovementService.cs`, OA registration |
| 🔴 HIGH | Autocrit distance `<= 3f` (10 ft; BG3 = 5 ft) | `EffectPipeline.cs` |
| 🟡 MED | `Frozen` missing `GrantsAdvantageToAttackers` + `MeleeAutocrits` | `ConditionEffects.cs` |
| 🟡 MED | `RegisterDefaultAbilities()` always fires, even over loaded scenario | `CombatArena.cs` L359 |
| 🟡 MED | `DefaultMovePoints = 10f` export contradicts `ActionBudget.DefaultMaxMovement = 30f` | `CombatArena.cs` |
| 🟡 MED | `new Random(42)` fixed seed in production pipeline | `RegistryInitializer.cs` |
| 🟡 MED | Defensive Duelist uses `?? 3` proficiency fallback | `BG3ReactionIntegration.cs` L668 |
| 🟡 MED | Dual action-resource reset system | `TurnLifecycleService.BeginTurn()` |
| 🟡 MED | OA `Range = 5f` hardcoded separately from `MELEE_RANGE` constant | `CombatArena.RegisterServices()` |
| 🟢 LOW | `ability_test_actor:` tag test scaffolding in production AI | `AIDecisionPipeline.cs` |
| 🟢 LOW | Class feature wiring at ~55% | Content gap across BG3 spell files |
| 🟢 LOW | Reaction system at ~70% | 6 reactions not yet registered |

---

## System 1 — CombatArena Scene & Setup

### Key files
- [Combat/Arena/CombatArena.cs](../Combat/Arena/CombatArena.cs) — 1841 lines, thin orchestrator
- [Combat/Services/ScenarioBootService.cs](../Combat/Services/ScenarioBootService.cs) — one-shot boot, ~379 lines

### Current behavior
`_Ready()` runs this sequence:
1. Parse CLI flags (`ConfigureAutoBattleFromCommandLine`)
2. Create all Godot nodes (camera, HUD, movement preview, VFX)
3. `InitializeCombatContext()` → `RegisterServices()` (wires all 8 services + all registries)
4. Attempt to load scenario (dynamic mode → random → `ScenarioPath`)
5. If no scenario loaded → `SetupDefaultCombat()`
6. **Always** call `RegisterDefaultAbilities()` — unconditional, line 359
7. `SpawnCombatantVisuals()` → `StartCombat()`

### BG3-accurate
- 8-service decomposition: `ActionBarService`, `TurnLifecycleService`, `ActionExecutionService`, `CombatCameraService`, `CombatPresentationService`, `CombatMovementCoordinator`, `ReactionCoordinator`, `ScenarioBootService`
- All registry bootstrapping happens before scenario load — correct ordering
- Full-fidelity vs autobattle flags correctly isolated via `DebugFlags`
- `UseRealtimeAIForAllFactions` disables `UseBuiltInAI` to prevent two turn drivers — correct

### Flaws
1. **`RegisterDefaultAbilities()` is unconditional.** It always fires at line 359, even when a real scenario loaded successfully. This registers three hardcoded `ActionDefinition` objects (`Target_MainHandAttack`, `Projectile_MainHandAttack`, `Shout_Dodge`) into `EffectPipeline`. If a BG3-loaded version of these IDs already exists, `RegisterAction()` overwrites or silently duplicates it — the behavior of `RegisterAction()` on collision is not guarded.
2. **`DefaultMovePoints = 10f` export.** `CombatMovementCoordinator` and `TurnLifecycleService` both receive `DefaultMovePoints` in their constructors. `ActionBudget.DefaultMaxMovement = 30f`. The export value is wired as a fallback for combatants whose `GetSpeed() <= 0`. The comment says "fallback" but 10f (10 metres ≈ 33 ft) is also wrong for BG3 and inconsistent with 30f.
3. **`SetupDefaultCombat()` fallback still active.** Creates four combatants with no `ResolvedCharacter`, no ability scores, no class levels. This fires any time a real scenario fails to parse (e.g., corrupt JSON), silently degrading to invented data.

### Test / legacy content
- `SetupDefaultCombat()` — Fighter(HP=50,Init=15), Mage(HP=30,Init=12), Goblin(HP=20,Init=14), Orc Brute(HP=40,Init=10), all made with the raw `Combatant(id, name, faction, maxHP, initiative)` constructor. No character build, no ability scores, no equipped items.
- `RegisterDefaultAbilities()` — three hardcoded `ActionDefinition` objects (see detailed stats in System 3 below). Should be replaced by ensuring the JSON/BG3 pipeline always provides these IDs.
- `// TODO: Replace with proper character-specific portraits` — `PortraitAssigner.AssignRandomPortraits()` is still the portrait strategy in the fallback path (`ScenarioBootService.cs` L193).

### What needs to change
- Make `RegisterDefaultAbilities()` conditional on `!scenarioLoaded`, or remove it entirely and ensure the BG3 pipeline always registers `Target_MainHandAttack`, `Projectile_MainHandAttack`, and `Shout_Dodge` from JSON.
- Fix `DefaultMovePoints` export to `30f` and add an `[ExportGroup]` warning comment.
- Remove or gate `SetupDefaultCombat()` behind a developer-only flag — it should never silently run in production.

---

## System 2 — Turn System & Initiative

### Key files
- [Combat/Services/TurnQueueService.cs](../Combat/Services/TurnQueueService.cs)
- [Combat/Services/TurnLifecycleService.cs](../Combat/Services/TurnLifecycleService.cs)
- [Combat/Entities/Combatant.cs](../Combat/Entities/Combatant.cs) — `Initiative` and `InitiativeTiebreaker` fields

### Current behavior

**Initiative:** `Combatant.Initiative` is a plain `int` set from the scenario JSON `"initiative"` field. `TurnQueueService.RecalculateTurnOrder()` sorts combatants:

```csharp
_turnOrder = _combatants
    .OrderByDescending(c => c.Initiative)
    .ThenByDescending(c => c.InitiativeTiebreaker)
    .ThenBy(c => c.Id)
    .ToList();
```

No d20 roll, no DEX modifier, no Feral Instinct advantage. Initiative is deterministic from the scenario file.

**Turn advancement:** `StartNewRound()` rebuilds the turn order from alive combatants each round, which correctly handles mid-round deaths but doesn't re-roll initiative. Round increments trigger reaction reset and status ticks.

### BG3-accurate
- Reactions reset at **round** boundary, not turn boundary — correct
- Unconscious combatants skip their turn (detected by resource state) — correct
- Death saving throws run at turn start for downed combatants — correct
- Round-start triggers: stand from prone (half movement cost), summon expiry, status tick — correct
- `ShouldEndCombat()` checks that both a hostile and a player faction have at least one living combatant — correct

### Flaws
1. **🔴 Initiative is never rolled.** BG3 rolls `d20 + DEX modifier` at combat start for all participants. The only d20 initiative roll in the entire codebase is in the test simulation helper:
   ```csharp
   // Tests/Simulation/SimulationState.cs L40 — test helper only
   Initiative = _dice.RollD20() + config.InitiativeBonus
   ```
   Production combat always uses the static JSON value. As a consequence:
   - Scenarios are deterministic by design, not by BG3's rules
   - Alert feat (+5 initiative) has no effect
   - Feral Instinct (roll twice, take higher) cannot be applied
   - Halfling Lucky does not affect initiative
2. **Turn order is rebuilt each round from alive combatants.** In BG3, when a combatant is summoned mid-combat their initiative is rolled and inserted into the current round at the appropriate position. The current implementation does not support mid-combat insertion — `StartNewRound()` rebuilds from `_combatants` but only the alive ones, and doesn't roll a fresh initiative for new arrivals.

### Test content
- `bg3_replica_test.json` uses `bg3TemplateId` references — initiative values there (player 20, goblin 10) are template placeholders, not BG3-accurate rolls.

### What needs to change
- Add `int RollInitiative(Combatant c, Random rng)` in `TurnQueueService`: `rng.Next(1,21) + c.GetAbilityModifier(Ability.Dexterity)`.
- Call this during `StartCombat()` instead of reading `c.Initiative` directly.
- Wire feat checks: Alert (+5, no surprise), Feral Instinct (roll twice take higher), Jack of All Trades (+half proficiency to DEX initiative if not already proficient).
- For mid-combat summons (SummonCombatantEffect), roll initiative and call `TurnQueueService.InsertAtInitiative()`.

---

## System 3 — Action Economy

### Key files
- [Combat/Actions/ActionBudget.cs](../Combat/Actions/ActionBudget.cs)
- [Combat/Services/TurnLifecycleService.cs](../Combat/Services/TurnLifecycleService.cs) — `BeginTurn()`
- [Data/ActionResources/](../Data/ActionResources/) — per-resource types

### Current behavior
`ActionBudget` tracks: 1 action, 1 bonus action, 1 reaction, movement points (float), and Extra Attack charges (`AttacksRemaining` / `MaxAttacks`). `DefaultMaxMovement = 30f` (BG3-correct). `ResetForTurn()` resets action and bonus action charges to 1, zeroes movement spent, preserves reaction (reset separately by `ResetReactionForRound()`).

### BG3-accurate
- 1 action + 1 bonus action + 1 reaction per turn — BG3-correct
- Movement budget from `BoostEvaluator.GetMovementMultiplier()` and `IsResourceBlocked("Movement")` — honours Haste, Slow, difficult terrain modifiers
- Reaction resets per **round** not per turn — BG3-correct
- Extra Attack pool tracked explicitly (`AttacksRemaining`, `MaxAttacks`) — correct
- Bonus action attacks (off-hand, nick) consume bonus action — correct
- Action Surge grants an extra action charge — correct

### Flaws
1. **🟡 Dual reset system.** `TurnLifecycleService.BeginTurn()` calls both:
   ```csharp
   combatant.ActionBudget.ResetForTurn();            // resets action/bonus charges
   combatant.ActionResources.ReplenishTurn();         // replenishes the resource pool
   ```
   Plan Step 0.4 claimed to unify these but both still fire. The result is correct (both reset the right things) but the parallel system is fragile — a future edit to one path won't automatically update the other.
2. **`DefaultMovePoints = 10f` export on CombatArena** (see System 1). `TurnLifecycleService` receives this value and uses it when `combatant.GetSpeed() <= 0`. Combatants without a valid speed get 10f movement instead of BG3's 30f.

### What needs to change
- Merge `ResetForTurn()` and `ReplenishTurn()` into a single method (or make one call the other) to eliminate the dual-reset fragility.
- Fix `DefaultMovePoints` export to `30f`.

---

## System 4 — Combat State Machine

### Key files
- [Combat/States/CombatStateMachine.cs](../Combat/States/CombatStateMachine.cs)
- [Combat/States/CombatSubstate.cs](../Combat/States/CombatSubstate.cs)

### Current behavior
State sequence: `CombatStart` → `TurnStart` → `PlayerDecision` (or `AIDecision`) → `TurnEnd` → `RoundEnd` → loop.

`CombatArena.CanPlayerControl()` gates input only when:
- It is a player turn
- The queried combatant is the active combatant
- State is `PlayerDecision`

### BG3-accurate
- `PlayerDecision` / `AIDecision` states correctly separate input routing
- State guards prevent double-turn execution
- `ActionExecution` substate with `_actionExecutionService.TickSafetyTimeout()` prevents freeze

### Flaws
- No audit findings. The state machine is lean and correct.

---

## System 5 — Spells & Cantrips

### Key files
- [Data/Spells/](../Data/Spells/) — JSON spell definitions
- [BG3_Data/Spells/](../BG3_Data/Spells/) — raw BG3 LSX/TXT source
- [Combat/Actions/EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs) — 3115 lines, all 43 effect handlers
- [Data/Actions/ActionRegistry.cs](../Data/Actions/ActionRegistry.cs)

### Current behavior
201 of 205 canonical BG3 spells implemented (per parity audit Session 6). All spell levels 0–6 covered. Levels 7–9 do not exist in BG3's L12 cap. Cantrip scaling: ×1/×2/×3 at L1–4/L5–9/L10+ (BG3-accurate). Save-or-half implemented. All 43 effect types have non-stub handlers (0 `NoOp` stubs remaining after Session 2).

### BG3-accurate
- Concentration: new concentration spell breaks previous one — correct
- Upcasting: `options.UpcastLevel` flows through `EffectPipeline.ExecuteAction()` — correct
- Spell attack rolls use spellcasting ability modifier (resolved per class via `ClassDefinition`) — correct after Phase 2.10 fix
- Save DC formula: `8 + proficiency + abilityMod` — correct
- Multi-projectile spells (Scorching Ray, Magic Missile, Eldritch Blast): each projectile gets an independent attack or hit — correct

### Flaws
1. **4 of 205 BG3 spells not yet implemented.** Not identified by name in audit; marked as outstanding in Session 6.
2. **Class feature wiring at ~55%.** Many spells that key off class features (e.g., Agonizing Blast modifying Eldritch Blast damage, Improved Divine Smite) are in the pipeline but the class feature check may not fire correctly. This is a content gap, not an infrastructure gap.
3. **`EffectPipeline.Rng = new Random(42)`** — fixed seed (see System 10).

### What needs to change
- Identify and implement the 4 missing BG3 spells.
- Track class feature wiring coverage per spell in the parity audit.

---

## System 6 — Conditions & Status Effects

### Key files
- [Combat/Statuses/ConditionEffects.cs](../Combat/Statuses/ConditionEffects.cs) — 612 lines, sole mechanical authority
- [Combat/Statuses/StatusSystem.cs](../Combat/Statuses/StatusSystem.cs) — 1490 lines
- [BG3_Data/Statuses/](../BG3_Data/Statuses/) — 270+ BG3 status definitions
- [Data/Statuses/](../Data/Statuses/) — curated integration statuses

### Current behavior
16 conditions defined (14 D&D 5e SRD + Exhaustion + Frozen). Each `ConditionMechanics` struct specifies: `IsIncapacitated`, `CantMove`, `SpeedZero`, `GrantsAdvantageToAttackers`, `GrantsDisadvantageToAttackers`, `GrantsAdvantageToCasterSaves`, `AutoFailStrDexSaves`, `AutoFailAllSaves`, `MeleeAutocrits`, `Blinded`, `Deafened`, `GivesDisadvantageOnAttacks`, `Paralyzed`, `Petrified`, `Poisoned`, `Frightened`, `Charmed`.

`ConditionEffects.GetAggregateEffects(statuses)` is the single query point used by `EffectPipeline` for advantage/disadvantage and special flags.

### BG3-accurate
- `Stunned`: `IsIncapacitated=true, CantMove=true, AutoFailStrDexSaves=true, GrantsAdvantageToAttackers=true` — does **not** set `MeleeAutocrits`. This is BG3-correct: only `Paralyzed` and `Unconscious` grant automatic crits.
- `Paralyzed`: sets both `GrantsAdvantageToAttackers=true` and `MeleeAutocrits=true` — correct
- `Unconscious`: sets `MeleeAutocrits=true` and `AutoFailAllSaves=true` — correct
- `Prone`: `GrantsAdvantageToAttackers=true` (melee only), `GivesDisadvantageOnAttacks=true` — correct. Ranged attacker disadvantage is handled separately in `EffectPipeline` via the position check.
- `Petrified`: resistance to all damage — correct
- 261 of 270+ BG3 statuses have mechanical wiring (per parity audit Session 4)

### Flaws
1. **🟡 `Frozen` condition is missing two flags:**
   ```csharp
   // Current — ConditionEffects.cs
   ConditionType.Frozen, new ConditionMechanics {
       IsIncapacitated = true,
       CantMove = true,
       SpeedZero = true,
       AutoFailStrDexSaves = true,
       // MISSING:
       GrantsAdvantageToAttackers = true,  // BG3: frozen targets are easier to hit
       MeleeAutocrits = true               // BG3: frozen = treated like paralyzed for autocrit
   }
   ```
   In BG3, the Frozen condition (applied by Ray of Frost, Ice Storm surface, etc.) treats the target like a Paralyzed target for attack advantage and autocrit purposes.

### What needs to change
- Add `GrantsAdvantageToAttackers = true` and `MeleeAutocrits = true` to `ConditionType.Frozen` in `ConditionEffects.cs`.
- Verify against: https://bg3.wiki/wiki/Frozen

---

## System 7 — Targeting

### Key files
- [Combat/Targeting/TargetValidator.cs](../Combat/Targeting/TargetValidator.cs)
- [Combat/Targeting/](../Combat/Targeting/) — includes AoE, LOS, Height services

### Current behavior
`TargetValidator` handles: `SingleUnit`, `AoE`, `Self`, `All`, `None` target types; `Enemies`, `Allies`, `Self`, `Any` target filters. LOS and height checks wired via `LOSService` and `HeightService`. Range checks compare grid distance against `ActionDefinition.Range`.

### BG3-accurate
- LOS required for most offensive spells — correct
- `TargetType.None` (targetless) does not require a target click — correct
- AoE radius correctly passed from `ActionDefinition` through to `AoEIndicator` — correct
- Friendly-fire AoE (Fireball hits allies) — correct, uses `TargetFilter.Any`

### Flaws
- No critical flaws found. Surface-touch targeting (stepping on a surface to trigger) is handled by `SurfaceManager` separately and is not part of `TargetValidator` — this is the correct separation.

---

## System 8 — Movement

### Key files
- [Combat/Movement/MovementService.cs](../Combat/Movement/MovementService.cs)
- [Combat/UI/MovementPreview.cs](../Combat/UI/MovementPreview.cs)
- [Combat/Movement/SpecialMovementService.cs](../Combat/Movement/SpecialMovementService.cs)

### Current behavior
`MovementService` handles: pathed movement with `TacticalPathfinder`, opportunity attack triggering on leaving melee range, difficult terrain (×0.5 cost), Disengage immunity to OA. Prone stand costs half movement. Jump is separate via `SpecialMovementService`.

### BG3-accurate
- Prone stand at turn start costs half movement budget — correct
- Disengage (`disengaged` status) suppresses `EnemyLeavesReach` reaction — correct
- Difficult terrain surfaces multiply movement cost — correct
- Jump distance formula based on STR modifier — correct

### Flaws
1. **🔴 `MELEE_RANGE = 5f` (5 metres ≈ 16 ft).** BG3 standard melee reach is 1.5 m (5 ft). The constant in `MovementService` is 5f, meaning every melee combatant threatens a 5-metre radius (~16 ft circle). Opportunity attacks fire when an enemy moves outside this 5m bubble, which is far larger than BG3's 5 ft reach:
   ```csharp
   // MovementService.cs
   public const float MELEE_RANGE = 5f; // should be 1.5f
   ```
2. **🔴 The OA `ReactionDefinition.Range` is hardcoded to `5f` in `CombatArena.RegisterServices()`**, separately from `MovementService.MELEE_RANGE`:
   ```csharp
   reactionSystem.RegisterReaction(new ReactionDefinition {
       Id = "opportunity_attack",
       Range = 5f, // hardcoded, does NOT reference MovementService.MELEE_RANGE
       ...
   });
   ```
   A comment says `// Melee range (must match MovementService.MELEE_RANGE)` but there is no actual linkage. Fixing one does not fix the other.

### What needs to change
- Change `MovementService.MELEE_RANGE = 1.5f`.
- Change OA `ReactionDefinition.Range` to use `MovementService.MELEE_RANGE` as a constant reference (or a shared constant in a `BG3Constants` class).
- Wiki ref: https://bg3.wiki/wiki/Melee_attack — "Melee attacks can target creatures within 1.5 m / 5 ft"
- Reassess Reach weapons (Polearms etc.) — BG3 extends reach to 3 m / 10 ft. A `WeaponReach` modifier should feed into OA range at trigger time.

---

## System 9 — AI System

### Key files
- [Combat/AI/AIDecisionPipeline.cs](../Combat/AI/AIDecisionPipeline.cs) — 3102 lines (secondary god class)
- [Combat/AI/RealtimeAIController.cs](../Combat/AI/RealtimeAIController.cs)
- [Combat/AI/UIAwareAIController.cs](../Combat/AI/UIAwareAIController.cs)

### Current behavior
Custom tactical scoring AI. Generates candidate actions for the active combatant, scores each by: behavioral heuristics (melee prefers adjacent targets, healers prefer low-HP allies), adaptive modifiers (failure history), team coordination (focus-fire bonuses). Executes the highest-scoring valid action.

### BG3-accurate aspects
- Incapacitated check via `ConditionEffects.GetAggregateEffects()` — correct
- Resource validation before execution (no phantom actions) — correct
- Targeting re-validation after scoring (stale plan invalidation) — correct
- `UIAwareAIController` simulates human UI interaction in full-fidelity mode — correct architectural choice

### Flaws
1. **🟡 `ability_test_actor:` tag is test scaffolding in production code.** Both `MakeDecision()` and `CanUseAbility()` check:
   ```csharp
   if (testTag.StartsWith("ability_test_actor:", StringComparison.OrdinalIgnoreCase))
   ```
   This forces the AI to use a specific action ID for automated ability testing. It is fine that it exists; the flaw is it is in the main `AIDecisionPipeline` class rather than a test-only override. If left in production it will interfere with any combatant inadvertently tagged `ability_test_actor:*`.
2. **`AIDecisionPipeline.cs` is 3102 lines** — a secondary god class. The plan notes this as "Future Considerations" but it represents a maintenance risk. No immediate fix required; flagged for awareness.
3. **AI is not BG3-authentic.** Larian's AI uses behavioral trees and goal-oriented action planning. The current custom scoring AI produces plausible behavior but will not replicate specific BG3 AI quirks (hold action, AoE reluctance when allied in blast radius, etc.).

### What needs to change
- Move `ability_test_actor:` handling to a test-only `AIDecisionFilter` or `ITestActionOverride` interface injected only during headless tests.
- Long-term: decompose `AIDecisionPipeline` into a scoring sub-pipeline, candidate generator, and executor.

---

## System 10 — Damage & Rules Engine

### Key files
- [Combat/Actions/EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs) — 3115 lines
- [Combat/Rules/RulesEngine.cs](../Combat/Rules/RulesEngine.cs)
- [Data/CharacterModel/CharacterSheet.cs](../Data/CharacterModel/CharacterSheet.cs)

### Current behavior

**Attack resolution:** `EffectPipeline` queries `ConditionEffects.GetAggregateEffects()` for advantage/disadvantage → applies weapon/spell attack bonus → rolls d20 → compares to target AC. Crits on natural 20 (or 18/19/20 with Improved Critical). Autocrit on Paralyzed/Unconscious within melee range.

**Save resolution:** `ComputeSaveDC()` reads spellcasting ability from `ClassDefinition` → `8 + proficiency + abilityMod`. Target rolls d20 + save modifier. Damage halved on success if `effect.SaveTakesHalf`.

**Modifiers:** `(int)Math.Floor((score - 10) / 2.0)` — BG3-correct formula. Proficiency bonus at L12 = +4 (BG3 cap).

**Rules Engine:** `QueryType` enum includes: `AttackRoll`, `DamageRoll`, `SavingThrow`, `SkillCheck`, `HitChance`, `CriticalChance`, `ArmorClass`, `Initiative`, `MovementSpeed`, `Contest`, `Custom`. Contest has dual roll support (A vs B).

### BG3-accurate
- `GetModifier()` formula: `(int)Math.Floor((score - 10) / 2.0)` — correct (not integer division)
- Save DC: `8 + proficiency + abilityMod` — correct
- Resistance/vulnerability: applied from status modifiers list — correct
- Extra Attack: `AttacksRemaining > 0` decrements correctly — correct
- Crossbow Expert: ranged melee disadvantage exemption wired — correct
- Great Weapon Master: -5/+10 opt-in — correct

### Flaws
1. **🔴 Autocrit distance `<= 3f` should be `<= 1.5f`:**
   ```csharp
   // EffectPipeline.cs
   if (tgtEffects.MeleeAutocrits && isMeleeAttack && attackDistance <= 3f)
       autoCritOnHit = true;
   ```
   `3f` = 3 metres = ~10 ft. BG3's autocrit (Paralyzed, Unconscious) applies only within 1.5 m (5 ft). An attacker at 3m with a ranged weapon would incorrectly receive autocrit against a Paralyzed target.
2. **🟡 `EffectPipeline.Rng = new Random(42)` — fixed seed.** `RegistryInitializer.Bootstrap()` sets a hardcoded seed:
   ```csharp
   r.EffectPipeline = new EffectPipeline { Rng = new Random(42) };
   ```
   This means all production combats use the same die-roll sequence regardless of `RandomSeed` or `--seed` CLI flag. In autobattle scenarios, `SetupDefaultCombat()` and `ScenarioBootService.LoadScenario()` separately call `effectPipeline.Rng = new Random(seed)` which correctly re-seeds after the fact — but only if the scenario loads successfully. If the arena runs without a scenario (debug mode), seed 42 is always used.
3. **🟡 Defensive Duelist proficiency fallback:**
   ```csharp
   // BG3ReactionIntegration.cs L668
   int profBonus = reactor?.ProficiencyBonus ?? 3;
   ```
   The fallback of `3` is an arbitrary guess. Should be `combatant.GetProficiencyBonus()` computed from level, or defensive behaviour is to use `2` (minimum D&D proficiency). A null `reactor` means the reaction fires with wrong AC bonus.

### What needs to change
- Change autocrit distance to `1.5f` in `EffectPipeline`.
- Ensure `EffectPipeline.Rng` is always seeded from the scenario seed, not hardcoded to 42. Best fix: remove seed from `RegistryInitializer.Bootstrap()`, require callers (ScenarioBootService) to always set `Rng` after Bootstrap.
- Fix Defensive Duelist to use `reactor?.GetProficiencyBonus() ?? 2`.

---

## System 11 — Classes, Races & Backgrounds

### Key files
- [Data/Classes/](../Data/Classes/) — JSON class definitions
- [Data/Races/](../Data/Races/) — JSON race definitions
- [BG3_Data/ClassDescriptions.lsx](../BG3_Data/ClassDescriptions.lsx)
- [BG3_Data/Progressions.lsx](../BG3_Data/Progressions.lsx)
- [Data/CharacterModel/CharacterDataRegistry.cs](../Data/CharacterModel/CharacterDataRegistry.cs)
- [Data/CharacterModel/CharacterResolver.cs](../Data/CharacterModel/CharacterResolver.cs)
- [Data/CharacterModel/CharacterSheet.cs](../Data/CharacterModel/CharacterSheet.cs)

### Current behavior
`BG3DataLoader` parses `ClassDescriptions.lsx`, `Progressions.lsx`, `Races.lsx`, `Backgrounds.lsx` into `CharacterDataRegistry`. `CharacterResolver.Resolve(sheet)` builds a `ResolvedCharacter` (the canonical stats authority) from `CharacterSheet` (base spec). 12 base classes, all BG3 races, 15 subclasses with always-prepared spells wired.

`ProficiencyBonus` by level (capped at L12):

| Levels | Bonus |
|--------|-------|
| 1–4    | +2    |
| 5–8    | +3    |
| 9–12   | +4    |
| >12    | +4 (capped — levels don't exist in BG3) |

### BG3-accurate
- Level cap of 12 hard-coded into `ProficiencyBonus` computation — correct
- `GetModifier()` uses `Math.Floor` — correct
- Subclass always-prepared spells triggered from `CharacterResolver` — correct
- All 12 BG3 classes defined — correct

### Flaws
1. **🟢 Class feature wiring at ~55%.** The engine can load class features from progressions, but many class-specific passive/active features are not wired to their mechanical effect. Examples: Thief's Fast Hands (bonus action item use), Berserker's Frenzy Attacks, Spore Druid's Halo of Spores. These are content gaps, not infrastructure bugs.
2. **Ability score cap of 20 applies at build time** via `Math.Min(baseScore, 20)` in `CharacterSheet`. BG3 allows temporary scores above 20 (Enlarge, Storm Sorcerer capstone) and item bonuses can interact. The cap is applied correctly at the base-score level; transient bonuses from statuses/passives are applied separately — no immediate flaw, but this edge is worth tracking.

### What needs to change
- Systematically audit each class's progressions entry and ensure each feature has a corresponding `BoostApplicator` or `FunctorExecutor` entry.
- Track coverage in parity audit.

---

## System 12 — Reactions

### Key files
- [Combat/Reactions/BG3ReactionIntegration.cs](../Combat/Reactions/BG3ReactionIntegration.cs)
- [Combat/Reactions/ReactionSystem.cs](../Combat/Reactions/ReactionSystem.cs)
- [Combat/Reactions/ReactionCoordinator.cs](../Combat/Reactions/ReactionCoordinator.cs)
- [Data/Interrupts/InterruptRegistry.cs](../Data/Interrupts/InterruptRegistry.cs)

### Current behavior
13 reactions registered (parity audit Session 7: ~70% coverage). Registered:

| Reaction | Trigger | Source |
|----------|---------|--------|
| Opportunity Attack | EnemyLeavesReach | CombatArena.RegisterServices |
| Counterspell | SpellCastNearby | CombatArena.RegisterServices |
| Shield (+5 AC) | YouAreAttacked | BG3ReactionIntegration |
| Uncanny Dodge (half damage) | YouAreHit | BG3ReactionIntegration |
| Deflect Missiles | YouAreHit (ranged) | BG3ReactionIntegration |
| Hellish Rebuke | YouAreHit | BG3ReactionIntegration |
| Cutting Words | YouAreAttacked | BG3ReactionIntegration |
| Sentinel OA | EnemyLeavesReach (any direction) | BG3ReactionIntegration |
| Sentinel Ally Defense | AllyTakesDamage | BG3ReactionIntegration |
| Mage Slayer | SpellCastNearby | BG3ReactionIntegration |
| War Caster | EnemyLeavesReach (spellcaster) | BG3ReactionIntegration |
| Warding Flare | YouAreAttacked | BG3ReactionIntegration |
| Defensive Duelist | YouAreAttacked | BG3ReactionIntegration |

### BG3-accurate
- OA triggers on `EnemyLeavesReach`, exhausted by using reaction — correct
- Shield grants +5 AC to the triggering attack — correct
- Counterspell can cancel a spell being cast (CanCancel=true) — correct
- Uncanny Dodge halves the damage of one hit per round — correct
- Reaction resource is consumed on use and not recharged until next round — correct

### Flaws
1. **🔴 OA `Range = 5f` in `CombatArena.RegisterServices()` is hardcoded, not linked to `MovementService.MELEE_RANGE`.** See System 8 for detail. Same root cause: both should be `1.5f` and one should reference the other.
2. **🟡 Defensive Duelist `?? 3` proficiency fallback** — see System 10.
3. **🟢 ~30% of BG3 reactions not yet registered.** Not yet covered: Riposte (Fighter/Dueling), Giant Killer, Shield Master shove, Githyanki Parry, Combat Inspiration, Bardic Inspiration (half-damage variant), Uncanny Dodge for Monk (separate from Rogue). These require feat/class prerequisite checks before registration.

### What needs to change
- Link OA `Range` to `MovementService.MELEE_RANGE` (after fixing to 1.5f).
- Fix Defensive Duelist proficiency fallback.
- Register the 6 outstanding reactions with proper prerequisite guards.

---

## System 13 — Passives

### Key files
- [Combat/Passives/BoostApplicator.cs](../Combat/Passives/BoostApplicator.cs)
- [Data/Passives/PassiveRegistry.cs](../Data/Passives/PassiveRegistry.cs)
- [BG3_Data/Stats/Passive.txt](../BG3_Data/Stats/Passive.txt) — 334+ boost-only entries

### Current behavior
BG3 data pipeline: `Passive.txt` entries parsed → `PassiveRegistry` → `BoostApplicator` applies stat modifiers at character-resolve time. ~58 functor passives with custom logic registered separately. ~13 hand-coded `PassiveRuleProvider` entries for complex cases.

Legacy `PassiveRuleService` and `bg3_passive_rules.json` are deleted ✅.

### BG3-accurate
- Boost-only passives (flat AC, save bonus, initiative, etc.) auto-wired from BG3 data — correct
- Functor passives (Sneak Attack damage add, Great Weapon Master damage, Savage Attacker reroll) — correct
- Passive stacking: passives accumulate into `ResolvedCharacter` at resolve time, not at ability-use time — correct

### Flaws
- No critical flaws. Coverage gap exists (~10% functor passives not yet connected per parity audit Session 1) but the infrastructure is sound.

### What needs to change
- Audit remaining unconnected functor passives against `BG3_Data/Stats/Passive.txt`.

---

## System 14 — Data Loading & Registries

### Key files
- [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) — bootstrap orchestrator
- [Data/Actions/ActionRegistry.cs](../Data/Actions/ActionRegistry.cs)
- [Data/Passives/PassiveRegistry.cs](../Data/Passives/PassiveRegistry.cs)
- [Data/Statuses/BG3StatusRegistry.cs](../Data/Statuses/BG3StatusRegistry.cs)
- [Data/BG3DataLoader.cs](../Data/BG3DataLoader.cs)

### Current behavior
`RegistryInitializer.Bootstrap()` loads in order:
1. `DataRegistry` (curated JSON)
2. `CharacterDataRegistry` via `BG3DataLoader` (LSX: classes, races, backgrounds, progressions, feats)
3. `RulesEngine` + `StatusManager` + `ConcentrationSystem`
4. `EffectPipeline` (with hardcoded `Rng = new Random(42)`)
5. `ActionRegistry` via `ActionRegistryInitializer.Initialize` (BG3 spells from JSON/LSX)
6. `StatsRegistry` (BG3 Stats TXT)
7. `BG3StatusRegistry` + `BG3StatusIntegration` (270+ statuses)
8. `PassiveRegistry` (Passive.txt → BoostApplicator)
9. `InterruptRegistry` (Interrupt.txt → reflex reactions)
10. `FunctorExecutor`

Returns `RegistryBundle` with all handles wired into `CombatContext`.

### BG3-accurate
- All registries wired to `CombatContext` for service-locator access — correct
- No `DataRegistry.GetAction()` calls remain — confirmed deleted
- LSX parser used for BG3 source data; no hand-transcription — correct

### Flaws
1. **🟡 `EffectPipeline.Rng = new Random(42)` in `Bootstrap()`.** This is the wrong place to set the RNG seed. Bootstrap should not know the game seed; that is scenario-specific. Callers override it but only after Bootstrap returns, creating a window where rolls have the wrong seed.

### What needs to change
- Remove `Rng` assignment from `RegistryInitializer.Bootstrap()`.
- Require `ScenarioBootService` to always call `effectPipeline.Rng = new Random(seed)` as part of scenario/default setup, which it already does for the success path — extend to cover all paths.

---

## System 15 — Scenarios & Test Content

### Key files
- [Data/Scenarios/](../Data/Scenarios/) — all scenario JSON files
- [Data/ScenarioLoader.cs](../Data/ScenarioLoader.cs) — loader + fallback logic
- [Data/ScenarioGenerator.cs](../Data/ScenarioGenerator.cs) — random 2v2 generator

### Scenario inventory

| File | Purpose | Status |
|------|---------|--------|
| `bg3_party_vs_goblins.json` | Default arena scenario (seed 42) | ✅ Production |
| `bg3_duel.json` | 1v1 fighter vs goblin boss (seed 1) | ⚠️ Partly inaccurate |
| `bg3_boss_fight.cs` | Party vs Mind Flayer boss (seed 9999) | ✅ Production |
| `bg3_underdark_ambush.json` | Party ambush/environment scenario | ✅ Production (not deeply audited) |
| `bg3_replica_test.json` | Template-reference test scenario | 🧪 Test only — not for arena |
| `action_editor_scenario.json` | Action editor training dummy | 🧪 Dev tool |
| `action_test_batches.json` | Batch action test specifications | 🧪 Test infra |

### Current behavior

**`bg3_party_vs_goblins.json`** — 6 units, all with `classLevels` ✅, proper ability scores ✅, equipped items ✅. Initiative hardcoded: Fighter=12, Shadowheart=10, Astarion=16, Goblin Warrior=8, Goblin Shaman=6, Goblin Scout=9. Not rolled.

**`bg3_duel.json`** — `goblin_boss` has `classLevels: [{"classId": "fighter"}]` — a goblin is modeled as a Fighter, which is BG3-inaccurate (goblins have their own stat block). Also: `goblin_boss` wears `ARM_Splint_Body` (Splint armour, AC 17 + DEX mod, Heavy Armor — implausible for a goblin boss). Initiative hardcoded at Boss=18, Fighter=15.

**`bg3_replica_test.json`** — Uses `bg3TemplateId` references (`"POC_Player_Fighter"`, `"Goblin_Melee"`, `"Goblin_Caster"`). These are template IDs, not resolved character builds. This scenario is a proof-of-concept from the template system and should not be used as a combat scenario. Initiative hardcoded at 20/1/1/10.

**`action_editor_scenario.json`** — Training Dummy (HP=999, Init=1) vs Tav (Fighter L5, Init=20). Dev tool for testing individual abilities; not a game scenario. Unaffected by combat balance.

### ScenarioLoader fallback paths (test/legacy)

1. **`GetDefaultAbilities(string name)`** — Name-based string matching fallback for units that have no `classLevels`. Active code at [Data/ScenarioLoader.cs](../Data/ScenarioLoader.cs) L598–L642:
   - `name.Contains("wizard")` → hardcoded list: `["Target_MainHandAttack", "Projectile_Fireball", "Projectile_MagicMissile", "Projectile_FireBolt"]`
   - `name.Contains("fighter")` → `["Target_MainHandAttack", "Shout_SecondWind", "Shout_ActionSurge"]`
   - `name.Contains("goblin")` → `["Target_MainHandAttack", "Shout_Disengage"]`
   - (etc. for all archetypes)
   This is invoked when a unit has no `classLevels` (original character build path) or as a secondary fallback within the resolved-character path. It assigns abilities by name-guessing, bypassing the BG3 data pipeline entirely.

2. **`GetDefaultTags(string name)`** — Same name-matching approach for tags when none are specified.

3. **`EnsureBasicAttack(list, resolver)`** — Runs for every combatant, every scenario. Appends `main_hand_attack` (the canonical attack ID) to any combatant that doesn't already have one. This is mostly harmless but is also a legacy crutch — units built from `classLevels` should always have an attack from their class build.

### Flaws
1. **`GetDefaultAbilities()` and `GetDefaultTags()` are active production code.** Any unit loaded without `classLevels` falls through to name-guessing. If a new scenario JSON author forgets `classLevels`, the unit silently gets wrong abilities. The error is printed but execution continues.
2. **`bg3_duel.json` goblin modeled as fighter.** BG3's Goblin Boss is a `goblin` creature type with unique actions (Multiattack, Nimble Escape, etc.), not a Fighter. AC with Splint armour (17) is also too high — goblin bosses in BG3 are AC 15 (Shield + DEX).
3. **`bg3_replica_test.json` is a dead scenario.** The `bg3TemplateId` resolution path may not be wired in current `ScenarioLoader`, making this silently fail or produce incorrect units if accidentally selected.

### What needs to change
- Delete or gate `GetDefaultAbilities()` / `GetDefaultTags()` — all units in production scenarios must have `classLevels` (enforce with a hard error, not a warning).
- Remove `EnsureBasicAttack()` once class builds reliably produce a main-hand attack action.
- Fix `bg3_duel.json` goblin boss: give it `classLevels: [{"classId": "goblin"}]` (if a goblin creature class exists) or a proper monster stat block.
- Move `bg3_replica_test.json` to `Tests/Scenarios/` to prevent accidental use.

---

## Appendix — Confirmed Deletions (Legacy Cleanup Complete)

The following architectural debts from earlier versions are confirmed **fully deleted** from the codebase:

| Deleted symbol | Plan step | Evidence |
|----------------|-----------|---------|
| `CombatantStats` class | Phase 0.1 | No `.cs` file; no references in grep |
| `EquipmentLoadout` / `EquipmentSlot` (3-slot) | Phase 0.2 | No `.cs` file; 12-slot `EquipSlot` is sole authority |
| `CombatantResourcePool.cs` | Phase 0.3 | Only `.uid` file remains |
| `PassiveRuleService.cs` | Phase 0.4 | No `.cs` file; no `bg3_passive_rules.json` |
| `DataRegistry.GetAction()` | Phase 0.5 | `ActionRegistry` is sole source; no `GetAction` call sites |
| `GetStatusAttackContext()` | Phase 3 | Replaced by `ConditionEffects.GetAggregateEffects()` |

---

## Appendix — BG3 Wiki Reference URLs

| Topic | URL |
|-------|-----|
| Frozen condition | https://bg3.wiki/wiki/Frozen |
| Melee attack reach | https://bg3.wiki/wiki/Melee_attack |
| Initiative | https://bg3.wiki/wiki/Initiative |
| Conditions overview | https://bg3.wiki/wiki/Conditions |
| Opportunity Attack | https://bg3.wiki/wiki/Opportunity_Attack |
| Defensive Duelist feat | https://bg3.wiki/wiki/Defensive_Duelist |
| Paralysed condition | https://bg3.wiki/wiki/Paralysed |
| Spells list | https://bg3.wiki/wiki/Spells |
| Proficiency Bonus | https://bg3.wiki/wiki/Proficiency_bonus |
