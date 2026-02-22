# Plan: Deep Mechanical Overhaul & Architecture Cleanup

Every major game system has fundamental mechanical flaws — save-or-half damage is broken, 6/7 scenarios have null stats, weapon abilities never apply on equip, conditions are triple-implemented with conflicts, and CombatArena is a 6600-line god class. This plan eliminates all legacy/parallel systems, consolidates to a single source of truth per domain, decomposes CombatArena into proper services, and fixes every critical mechanical bug.

**Key architectural decisions (confirmed):**
- **Stats**: `ResolvedCharacter` / `CharacterSheet` — sole source of truth. Delete `CombatantStats`.
- **Passives**: BG3 data pipeline (`PassiveRegistry` → `BoostApplicator`) — sole source. Delete `PassiveRuleService` + manual JSON.
- **Conditions**: `ConditionEffects.cs` — sole mechanical authority. Delete JSON AC-hacks and `GetStatusAttackContext`.
- **CombatArena**: Full rewrite into thin orchestrator + 8 dedicated services.
- **Legacy removal**: All old-style units, `CombatantStats`, `EquipmentLoadout` (3-slot), deprecated `CombatantResourcePool`, `DataRegistry` action lookups — all deleted.

---

## Phase 0: Legacy Removal & Single Source of Truth (Foundation)

*Everything depends on this. No features until one path per concept.*

**Step 0.1 — Kill old-style units**
- Delete `SetupDefaultCombat()` in [CombatArena.cs](Combat/Arena/CombatArena.cs#L1284) and `RegisterDefaultAbilities()` at [L1331](Combat/Arena/CombatArena.cs#L1331)
- Make `classLevels` mandatory: remove the `if (unit.ClassLevels != null)` guard in [ScenarioLoader.cs](Data/ScenarioLoader.cs#L280)
- Update all 7 scenario JSON files with `classLevels` for every unit

**Step 0.2 — Kill `CombatantStats`, unify on `ResolvedCharacter`**

*Sub-step 0.2a — Migrate WildShape stat mutation*: Before deleting `CombatantStats`, add `Dictionary<AbilityType, int> AbilityScoreOverrides` to `Combatant`. Migrate `TransformEffect`/`RevertTransformEffect` in [Effect.cs](Combat/Actions/Effects/Effect.cs#L2105) to use overrides instead of writing to `Stats`. `Combatant.GetAbilityScore()` consults overrides first, then `ResolvedCharacter.AbilityScores`.

*Sub-step 0.2b — Migrate all production consumers* (must be atomic with deletion):
- Replace all `combatant.Stats.X` reads with `ResolvedCharacter` accessors in **all** consuming files:
  - [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L2092) (6 ability modifier reads)
  - [Effect.cs](Combat/Actions/Effects/Effect.cs#L367) (WildShape read+write — handled by 0.2a)
  - [AIScorer.cs](Combat/AI/AIScorer.cs#L670) (STR/DEX/INT/WIS/CHA modifier reads; fix null-guard fall-through)
  - [AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L1644) (Stats?.BaseAC)
  - [OnHitTriggers.cs](Combat/Services/OnHitTriggers.cs#L377) (WIS/CON modifiers)
  - [ConcentrationSystem.cs](Combat/Statuses/ConcentrationSystem.cs#L790) (CON modifier)
  - [StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L1316) (6 ability modifier reads)
  - [InventoryService.cs](Combat/Services/InventoryService.cs#L726) (DEX for AC calc + `Stats.BaseAC` write → need mutable `CurrentAC` on `Combatant`)
  - [CombatArena.cs](Combat/Arena/CombatArena.cs#L1784) (Stats?.Speed, 2 locations)
  - [CombatSaveService.cs](Combat/Persistence/CombatSaveService.cs#L268) (full stats instantiation + restore)
  - [ScenarioLoader.cs](Data/ScenarioLoader.cs#L398) (`CombatantStats.GetModifier()` static calls, 3 locations)
- Add convenience methods to `Combatant`: `GetAbilityModifier(AbilityType)`, `GetProficiencyBonus()`, `GetArmorClass()`, `GetSpeed()`
- Add mutable `CurrentAC` property on `Combatant` (replaces `Stats.BaseAC` write from `InventoryService.RecalculateAC`)

*Sub-step 0.2c — Migrate 22 test files*: Create `TestHelpers.MakeCombatant(str, dex, con, ...)` factory that constructs a `Combatant` with a valid `ResolvedCharacter`. Migrate all test files that construct `new CombatantStats{}` before deleting the class. Key files: `WildShapeTransformationTests.cs`, `EffectPipelineIntegrationTests.cs`, `PassiveRuleServiceTests.cs`, `ExtraAttackIntegrationTest.cs`, `DynamicFormulaResolutionTests.cs`, and ~17 more.

*Sub-step 0.2d — Fix ability modifier formula*: `(int)Math.Floor((score - 10) / 2.0)` in **all 6+ locations**:
  - [CharacterSheet.cs](Data/CharacterModel/CharacterSheet.cs#L113)
  - [CharacterSheetModal.cs](Combat/UI/Overlays/CharacterSheetModal.cs#L196)
  - [CharacterTab.cs](Combat/UI/Screens/CharacterTab.cs#L610)
  - [InventoryService.cs](Combat/Services/InventoryService.cs#L729) (**gameplay-critical** — wrong DEX AC for sub-10 DEX)
  - [HudController.cs](Combat/UI/HudController.cs#L1120) (wrong attack bonus display, 3 occurrences)
  - The new unified `Combatant.GetAbilityModifier()` accessor

*Sub-step 0.2e — Delete `CombatantStats.cs`*: Only after 0.2a–0.2d are complete and `ci-build.sh` + `ci-test.sh` pass.

**Step 0.3 — Kill `EquipmentLoadout` (3-slot), unify on 12-slot `EquipSlot`**
- Delete `EquipmentLoadout` class and `EquipmentSlot` enum from [EquipmentDefinition.cs](Data/CharacterModel/EquipmentDefinition.cs#L25-L102)
- Migrate `PassiveRuleService` and `ConditionEvaluator` reads to `InventoryService.GetEquippedItem()`
- Update scenario JSON files and `ScenarioLoader` to use 12-slot format

**Step 0.4 — Kill deprecated `CombatantResourcePool`, clarify action resource reset**
- Remove old `combatant.ResourcePool` from `Combatant`
- Clarify two parallel reset systems: `ActionBudget.ResetForTurn()` ([ActionBudget.cs:107](Combat/Actions/ActionBudget.cs#L107)) sets `_actionCharges = 1` + `_bonusActionCharges = 1`; separately `combatant.ActionResources.ReplenishTurn()` is called at [CombatArena.cs:1807](Combat/Arena/CombatArena.cs#L1807). **Decide which is canonical and merge.** If `ActionBudget` is the per-turn budget and `ActionResources` is the BG3-style dictionary, unify so only one system tracks action/bonus action availability.
- Verify turn-2 lockout is still reproducible with autobattle seed 42 before applying fixes — the current code may already be correct

**Step 0.5 — Kill `DataRegistry` action lookup, unify on `ActionRegistry`**
- Remove `_dataRegistry.GetAction(id)` fallback from all action lookups
- Migrate custom-only actions into the `ActionRegistry` pipeline

**Step 0.6 — Fix race JSON `"Armor"` → `"ArmorCategories"` key mismatch**
- Rename in all files under [Data/Races/](Data/Races/) to match [Feature.cs](Data/CharacterModel/Feature.cs#L71) `ProficiencyGrant.ArmorCategories`
- Verify `ProficiencyGrant` has a `[JsonProperty]` attribute on `ArmorCategories` that matches the new JSON key

**Step 0.7 — Normalize IDs and casing across all registries**
- Audit and normalize identifier casing across [DataRegistry.cs](Data/DataRegistry.cs), [CharacterDataRegistry.cs](Data/CharacterModel/CharacterDataRegistry.cs), [StatusRegistry.cs](Data/Statuses/StatusRegistry.cs), [BG3StatusIntegration.cs](Combat/Statuses/BG3StatusIntegration.cs)
- Use case-insensitive lookups or canonicalize to a single casing convention at registration time
- Fix cross-registry ID mismatches (action IDs, status IDs, passive IDs) that cause silent lookup failures

**Verification:** `ci-build.sh` + `ci-test.sh` + autobattle seed 42 — STR 16 Fighter has +3 modifier, not +0

---

## Phase 1: CombatArena Decomposition (Architecture)

*Full rewrite. CombatArena becomes a ~300–500 line scene orchestrator (retained: Godot node references, CombatContext, StateMachine, service handles, signal wiring for 8 services).*

**Step 1.0 — Strangler-fig extraction strategy**
- Extract each service one at a time. After each extraction, keep a thin forwarding wrapper in `CombatArena` that delegates to the new service.
- Run `ci-godot-log-check.sh` + autobattle seed 42 after each individual extraction. Do not bulk-rewrite.
- This produces ~8 intermediate commits, each independently buildable and testable.

**Step 1.1 — Extract `ActionBarService`** → `Combat/Services/ActionBarService.cs`
- Move: PopulateActionBar, RefreshActionBarUsability, GetActionsForCombatant, GetCommonActions, sort/classify/icon/override helpers (~L4740–L5420)

**Step 1.2 — Extract `TurnLifecycleService`** → `Combat/Services/TurnLifecycleService.cs`
- Move: BeginTurn, EndCurrentTurn, EndCombat, ProcessDeathSave, resource refresh, budget tracking, threatened sync, rule window dispatch (~L1668–L2040, L6249–L6543)

**Step 1.3 — Extract `ActionExecutionService`** → `Combat/Services/ActionExecutionService.cs`
- Move: ExecuteAction (all overloads), ExecuteAbilityAtPosition, ExecuteResolvedAction, UseItem variants, Dip/Hide/Help/Throw (~L2719–L3082, L4362–L4640)
- **Dash and Disengage belong to Step 1.6** (CombatMovementCoordinator) — they are movement-mode operations consuming an action

**Step 1.4 — Extract `CombatCameraService`** → `Combat/Services/CombatCameraService.cs`
- Move: camera setup, orbit, follow, framing, presentation request handling (~L6057–L6481, L3826)

**Step 1.5 — Extract `CombatPresentationService`** → `Combat/Services/CombatPresentationService.cs`
- Move: timeline building, marker emission, VFX coordination, status visual feedback (~L3418–L3826, L4018–L4230)

**Step 1.6 — Extract `CombatMovementCoordinator`** → `Combat/Services/CombatMovementCoordinator.cs`
- Move: EnterMovementMode (~L5457), ExecuteDash (~L5536), ExecuteDisengage (~L5601), ExecuteMovement (~L5661), preview, navigation queries, jump paths (~L780–L912)
- Dash and Disengage are movement-adjacent actions — they consume an action but modify movement state

**Step 1.7 — Extract `ReactionCoordinator`** → `Combat/Services/ReactionCoordinator.cs`
- Move: reaction prompts, AI decision, resolution (~L5831–L6000)
- **Circular dependency warning**: `ActionExecutionService` calls `ReactionCoordinator` to trigger reactions; `ReactionCoordinator` calls back to execute the reaction action. Break cycle via `Action<string, string, Vector3> executeCallback` injected at construction by the orchestrator. `ReactionCoordinator` never holds a direct reference to `ActionExecutionService`.

**Step 1.8 — Extract `ScenarioBootService`** → `Combat/Services/ScenarioBootService.cs`
- Move: LoadScenario variants, visual spawning, service registration (~L1284–L1652)

**Step 1.9 — Rewrite `CombatArena`** as thin orchestrator
- `_Ready()`: create CombatContext, register services, boot scenario
- Input: delegate to CombatInputHandler
- Event wiring: connect service events to each other

**Step 1.10 — Wire event-driven recomputation spine**
- Any equip/status/passive/known-action mutation must trigger derived-stat recomputation across the board
- Central event bus connecting [InventoryService.cs](Combat/Services/InventoryService.cs), [StatusSystem.cs](Combat/Statuses/StatusSystem.cs), `PassiveRegistry`, and ActionBarService
- Events: `EquipmentChanged`, `StatusApplied`/`StatusRemoved`, `PassiveToggled`, `KnownActionsChanged`, `ResourceConsumed`
- Subscribers: derived stat recalc, hotbar membership refresh, passive provider re-evaluation
- Hotbar membership and action legality are separate concerns; membership must update on state-change events, not only at turn boundaries

**Step 1.11 — Update persistence for new mechanical state**
- Update [CombatantSnapshot.cs](Combat/Persistence/CombatantSnapshot.cs) to serialize: passive toggle states, known actions, resolved build facets, equipment-derived effects, `ActionResources` dict, `AbilityScoreOverrides` (WildShape)
- Update [CombatSaveService.cs](Combat/Persistence/CombatSaveService.cs) to save/restore via `ResolvedCharacter` (not deleted `CombatantStats`)
- **Reconstitution path**: On load, store serialized `CharacterSheet` JSON blob in the snapshot. Call `CharacterResolver.Resolve(sheet)` to reconstruct derived stats. Then re-apply equipment AC via `InventoryService.RecalculateAC()`. Do NOT copy raw stat values into `ResolvedCharacter`.
- Update [DeterministicExporter.cs](Combat/Persistence/DeterministicExporter.cs) to export: ability scores, stat overrides, active statuses, passive toggles, known actions (not just Id/Name/HP/position)
- Add save/load round-trip equivalence test: save → load → re-save → binary-compare

**Verification:** `ci-godot-log-check.sh` + autobattle seed 1–100 — identical results to pre-decomposition. Save/load round-trip passes.

---

## Phase 2: Actions & Spells Mechanical Fixes

*Depends on Phase 0+1*

| Step | Fix | Key File |
|---|---|---|
| 2.1 | Fix save-or-half condition mapping (use `ParseSaveType()`, not `SpellSaveDC` field) | [BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs#L537) |
| 2.2 | Implement dice formula division (`"3d6/2"` → correct half damage) | [SpellEffectConverter.cs](Data/Actions/SpellEffectConverter.cs), [Effect.cs](Combat/Actions/Effects/Effect.cs) |
| 2.3 | Implement cantrip scaling per **BG3 rules** (1/2/3× base dice at character levels 1–4/5–9/10+, **not** the 4-tier 5e progression) | New in EffectPipeline or ActionExecutionService |
| 2.4 | Fix BG3 ActionResources turn reset — unify with Step 0.4 decision. `TurnLifecycleService.BeginTurn` calls whichever system is canonical (ActionBudget or ActionResources dict, not both) | TurnLifecycleService.BeginTurn |
| 2.5 | Fix spell component parsing from `SpellFlags` (not fabricated from school) | [BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs#L237) |
| 2.6 | ~~Implement concentration damage checks~~ **ALREADY IMPLEMENTED** in [ConcentrationSystem.cs](Combat/Statuses/ConcentrationSystem.cs#L219) (`OnDamageTaken` → `CheckConcentration` with `Math.Max(10, dmg/2)`). **Add integration test to Phase 10 instead.** | — |
| 2.7 | Parse missing BG3 spell fields (MaximumTargets, RootSpellID, PowerLevel, DualWieldingUseCosts) | [BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) |
| 2.8 | Fix SpellFlags inheritance (set-union merge, not overwrite) | [BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) |
| 2.9 | Implement short rest vs long rest resource recovery distinction | RestService, ActionResourceDefinition |
| 2.10 | Fix save DC fallback to `8+prof+abilityMod`; read spellcasting ability from ClassDefinition, not hardcoded switch. **Also fix** the `return 10 + proficiency;` branch at [EffectPipeline.cs:~L2231](Combat/Actions/EffectPipeline.cs#L2231) which applies no ability modifier at all (wrong per both BG3 and 5e). | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L2227) |
| 2.11 | Fix death save critical hit: remove 1.5f range restriction from crit→2-failure count in [Effect.cs](Combat/Actions/Effects/Effect.cs#L735). Per D&D 5e and BG3, **any** critical hit on a downed creature = 2 death save failures regardless of range. | [Effect.cs](Combat/Actions/Effects/Effect.cs#L735) |

**Verification:** Burning Hands deals half damage on save (not full/zero). Fire Bolt scales. Combatants act past turn 1.

---

## Phase 3: Condition & Status System Unification

*Depends on Phase 0. Parallel with Phase 2.*

| Step | Fix | Key File |
|---|---|---|
| 3.1 | Expand `ConditionEffects.cs` to full mechanical authority for all **15** D&D 5e SRD conditions (including Exhaustion — implemented in BG3). Current enum has 14; add `Exhaustion` to `GameCondition`. | [ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs) |
| 3.2 | Delete `GetStatusAttackContext()` — replace with `ConditionEffects.GetAggregateEffects()` | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L2301) |
| 3.3 | Remove AC-hack modifiers from JSON statuses (blinded -2, stunned -2, etc.) | `Data/Statuses/*.json` |
| 3.4 | Fix specific conditions: frightened (wrong disadv), asleep/unconscious (missing auto-fail), petrified, stunned, charmed, frozen IDs | Multiple status JSONs + ConditionEffects |
| 3.5 | Instantiate `BG3StatusIntegration` + fix case sensitivity + register bless/bane | [BG3StatusIntegration.cs](Combat/Statuses/BG3StatusIntegration.cs) |
| 3.6 | Fix repeat-save DC: inject caster's spell DC into `StatusInstance.SaveDCOverride` | Status application pipeline |
| 3.7 | **Migrate** prone stand cost from `TurnLifecycleService.BeginTurn` (existing code at [CombatArena.cs:~L1812](Combat/Arena/CombatArena.cs#L1812)) to MovementService. **Remove the existing BeginTurn code** to prevent double-charging movement. Add frightened movement restriction. | MovementService |
| 3.8 | Implement BG3 status remove semantics — deterministic status bridging with composite remove events (e.g., `HOLD_PERSON` removes `SG_Paralyzed` tag chain). Ensure multi-layered statuses (parent spell → child condition tags) are applied and removed atomically. | [StatusSystem.cs](Combat/Statuses/StatusSystem.cs), [BG3StatusIntegration.cs](Combat/Statuses/BG3StatusIntegration.cs) |

**Verification:** Stunned = auto-fail STR/DEX saves + attackers have Advantage. **Paralyzed/Unconscious** within 1.5m = autocrit (test both separately — Stunned does NOT grant autocrit). No AC-hack double-dipping. Bless/bane work. Hold Person removal clears Paralyzed.

---

## Phase 4: Passives System Unification

*Depends on Phase 0, Phase 3.*

| Step | Fix | Key File |
|---|---|---|
| 4.1 | Add missing BoostTypes: `CharacterWeaponDamage`, `Reroll`, `TwoWeaponFighting`, `ExpertiseBonus` + implement handlers | [BoostType.cs](Combat/Rules/Boosts/BoostType.cs) |
| 4.2 | Fix ConditionEvaluator fail-open → fail-closed; implement critical BG3 pseudo-functions | [ConditionEvaluator.cs](Combat/Rules/Conditions/ConditionEvaluator.cs) |
| 4.3 | Fix `FunctorExecutor.RollDiceExpression()` for dynamic expressions (SpellPowerLevel, StrengthModifier, etc.) | [FunctorExecutor.cs](Combat/Rules/Functors/FunctorExecutor.cs#L548) |
| 4.4 | Wire `PassiveRegistry.RuleWindowBus` so GenericFunctorRuleProvider auto-registration becomes live | PassiveRegistry init |
| 4.5 | **Delete** `PassiveRuleService`, `PassiveRuleProviders.cs`, `bg3_passive_rules.json` — migrate all 12 manual passives to BG3 pipeline | Multiple files |
| 4.6 | Implement scaling passives (Rage damage by level, Sneak Attack dice by level) | BG3 passive data + LevelMapValue |
| 4.7 | Copy racial feature tags to combatant.Tags; register BG3 passive entries for racial features | ScenarioLoader/CharacterResolver |

**Verification:** Rage scales +2/+3/+4. Dueling +2 works. Unknown conditions don't fire. Halfling rerolls 1s.

---

## Phase 5: Equipment System Completion

*Depends on Phase 0. Parallel with Phase 4. **Note: Step 5.1 specifically requires Phase 1 (ActionBarService) and Phase 1.10 (event spine).***

| Step | Fix |
|---|---|
| 5.1 | Wire `GrantedActionIds` on equip/unequip → `KnownActions` + fire event → ActionBarService.Refresh |
| 5.2 | Implement weapon set switching (Melee↔Ranged), free interaction hotbar button |
| 5.3 | Implement accessory slot mechanical effects (BoostList on InventoryItem, apply/remove via BoostApplicator) |
| 5.4 | Enforce armor proficiency penalties (disadvantage on attacks+checks, can't cast) |
| 5.5 | Enforce heavy armor STR requirement (speed -10ft if below) |
| 5.6 | Fix off-hand TWF damage (no ability mod unless TwoWeaponFighting style) |
| 5.7 | Enforce dual-wield Light requirement (unless Dual Wielder feat) |
| 5.8 | Implement versatile weapon die switching (2-hand die when off-hand empty) |
| 5.9 | Fix heavy armor negative DEX AC (clamp to 0) |

**Verification:** Longsword equip → weapon abilities on hotbar. Ring of Protection +1 AC. Wizard in plate has disadvantage.

---

## Phase 6: Race & Class System Fixes

*Depends on Phase 4. Parallel with Phase 5.*

| Step | Fix |
|---|---|
| 6.1 | Read `ClassDefinition.SpellcastingAbility` at runtime — delete hardcoded switches. Multiclass: resolve from granting class. |
| 6.2 | Implement prepared/known spell distinction (Wizard/Cleric prepare, Sorcerer/Warlock auto-know) |
| 6.3 | Implement ASI at feat levels (choice between feat or +2/+1+1 to abilities) |
| 6.4 | Implement Unarmored Defense via passive system (Barbarian 10+DEX+CON, Monk 10+DEX+WIS) |
| 6.5 | Fix Warlock pact slot group tracking (group 1 vs group 2, short rest recovery) |

---

## Phase 7: Feats System Completion

*Depends on Phase 4, Phase 6.*

| Step | Fix |
|---|---|
| 7.1 | Enforce prerequisites (fix `Prerequisites` type from `object` → `FeatPrerequisite`) |
| 7.2 | Implement GWM/Sharpshooter -5/+10 toggles (register actions, wire to hotbar) |
| 7.3 | Fix Sharpshooter bonus action grant (remove from `GrantGWMBonusAction`) |
| 7.4 | Implement Lucky feat (register actions, wire luck_points resource) |
| 7.5 | Implement dynamic-choice feats (sub-selection UI for Resilient, Magic Initiate, etc.) |
| 7.6 | Wire remaining ~30 feat tags to mechanical effects via BG3 passive pipeline |

---

## Phase 8: Hotbar & UI Polish

*Depends on Phase 1 (ActionBarService), Phase 5 (equipment). Parallel with Phase 7.*

| Step | Fix |
|---|---|
| 8.1 | Reactive hotbar refresh (subscribe to KnownActionsChanged, StatusApplied, EquipmentChanged, ResourceConsumed) |
| 8.2 | Upcast UI (slot-level picker popup on spell click) |
| 8.3 | Concentration indicator (hotbar icon + unit visual ring) |
| 8.4 | Rich tooltips (range, damage dice, spell school, concentration, save DC, AoE shape) |
| 8.5 | Fix passive toggle usability (check prerequisites/suppression, not unconditional Available) |
| 8.6 | Multi-row hotbar (tabbed layout: Attacks, Spells by level, Class Features, Items) |

---

## Phase 9: Visual Feedback Improvements

*Parallel with Phase 8. Depends on Phase 1 (CombatPresentationService).*

| Step | Fix |
|---|---|
| 9.1 | Multiple floating text labels (pool of independent floaters, concurrent 4+) |
| 9.2 | Damage-type coloring (fire=orange, cold=ice-blue, necrotic=purple, radiant=gold, etc.) |
| 9.3 | Damage-type VFX (per-type impact particles) |
| 9.4 | Saving throw floating text ("DEX SAVE: 14 vs DC 15 — FAIL") |
| 9.5 | Death save tracker (3-pip success/failure on downed units) |
| 9.6 | Roll breakdown in combat log UI (expandable detail showing each modifier) |
| 9.7 | Turn announcement overlay ("YOUR TURN" banner + camera pan) |

---

## Phase 10: Parity Regression Suite

*Runs in parallel with later phases. Start building during Phase 0, expand with each phase.*

| Step | Test Category |
|---|---|
| 10.1 | **Fail-closed coverage**: verify unknown functor/condition/requirement types produce diagnostics and do not silently pass |
| 10.2 | **Equip/status → hotbar sync**: equip weapon → hotbar gains abilities; apply stunned → actions greyed; remove status → restored |
| 10.3 | **DC/attack parity**: exhaustive save DC and attack roll tests against known BG3 reference values per class/level |
| 10.4 | **Save/load round-trip**: save combat state → load → re-save → binary-compare snapshots |
| 10.5 | **Condition mechanical parity**: each of 15 D&D conditions tested for advantage/disadvantage/auto-fail/autocrit/movement per ConditionEffects |
| 10.6 | **Status lifecycle**: composite apply/remove chains (Hold Person → Paralyzed, Bless stack, concentration break) |
| 10.8 | **Concentration integration test**: verify concentration check fires correctly from EffectPipeline's DispatchDamage path (moved from deleted Step 2.6) |
| 10.7 | **Seeded autobattle stress**: seeds 1–1000 with no TIMEOUT_FREEZE or INFINITE_LOOP |

**Verification:** Full test suite green. Zero regressions across seed range.

---

## Files To Delete
- `Combat/Entities/CombatantStats.cs`
- `Data/Passives/bg3_passive_rules.json`
- `Combat/Rules/PassiveRuleService.cs`
- `Combat/Rules/PassiveRuleProviders.cs`
- `EquipmentLoadout` + `EquipmentSlot` from `Data/CharacterModel/EquipmentDefinition.cs`

## Files To Create
- `Combat/Services/ActionBarService.cs`
- `Combat/Services/TurnLifecycleService.cs`
- `Combat/Services/ActionExecutionService.cs`
- `Combat/Services/CombatCameraService.cs`
- `Combat/Services/CombatPresentationService.cs`
- `Combat/Services/CombatMovementCoordinator.cs`
- `Combat/Services/ReactionCoordinator.cs`
- `Combat/Services/ScenarioBootService.cs`

## Decisions
- Old-style units: **deleted**, not migrated. All scenarios must use full character builds
- `CombatantStats`: **deleted**. `ResolvedCharacter` is sole stat authority
- `EquipmentLoadout` (3-slot): **deleted**. 12-slot `EquipSlot` is sole equipment system
- `CombatantResourcePool`: **deleted**. `ActionResources` + `ActionBudget` per-turn
- `DataRegistry` action lookup: **deleted**. `ActionRegistry` is sole action source
- `PassiveRuleService` + manual JSON: **deleted**. BG3 data pipeline is sole passive system
- `GetStatusAttackContext` + JSON AC hacks: **deleted**. `ConditionEffects.cs` is sole condition authority
- `CombatArena`: **rewritten** as ~300–500 lines thin orchestrator. All logic in dedicated services. Retained: Godot node refs, CombatContext, StateMachine, service handles
- **Fail-closed policy** (project-wide): unknown requirement types, functor types, condition evaluator functions, and status IDs must fail closed with a diagnostic surfaced — never silently pass or return true. Applies to `ConditionEvaluator`, `FunctorExecutor`, `BG3StatusIntegration`, and all registry lookups.
- **BG3_Data is canonical** for mechanics; local JSON is transitional and must be validated against LSX/text sources. Wiki is behavioral tie-breaker.
- **Event-driven state**: hotbar membership and action legality are separate concerns; membership must update on state-change events, not only at turn boundaries.

## Future Considerations

**AIDecisionPipeline.cs decomposition**: At 3102 lines, [AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs) is the second god-class in the codebase (action scoring, movement planning, candidate generation, reaction policy). It has 30+ direct `actor.ActionBudget.*` calls and 10+ `actor.Stats.*` calls that Phase 0 will touch. Consider a Phase 1.5 decomposition using the same strangler-fig pattern after Phase 1, or at minimum audit all its CombatArena dependencies during Phase 1.

**Surprise round mechanic**: Neither the plan nor the codebase has surprise round logic. D&D 5e PHB p.189 and BG3 both implement surprise where ambushed combatants lose their first turn's action, bonus action, and reaction. This should be considered for a future phase (add `IsSurprised` flag on `Combatant`, stealth-check roll at combat start, surprised combatants skip round 1 actions).

## Phase Dependencies
```
Phase 0 (Foundation) ──→ Phase 1 (CombatArena decomp) ──→ Phases 2-9
                    ╲                                    ╱
                     ╰─→ Phase 3 (Conditions) ─────────╯
                     ╰─→ Phase 2 (Actions) can start after Phase 0
Phases 2, 3: parallel
Phases 4, 5, 6: parallel (all depend on Phase 0; Phase 4 also needs Phase 3)
  └─ Phase 5 Step 5.1 specifically blocks on Phase 1 (ActionBarService) + Phase 1.10 (event spine)
Phase 7: depends on Phase 4 + 6
Phases 8, 9: parallel polish (depend on Phase 1)
Phase 10: regression suite — starts Phase 0, grows with each phase
```
