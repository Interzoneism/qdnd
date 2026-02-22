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
- Delete [CombatantStats.cs](Combat/Entities/CombatantStats.cs), remove `combatant.Stats` from [Combatant.cs](Combat/Entities/Combatant.cs)
- Replace all `combatant.Stats.X` reads (EffectPipeline, RulesEngine, PassiveRuleService, BeginTurn) with `ResolvedCharacter` accessors
- Add convenience methods to `Combatant`: `GetAbilityModifier(AbilityType)`, `GetProficiencyBonus()`, `GetArmorClass()`, `GetSpeed()`
- Fix ability modifier formula in all 4 locations: `(int)Math.Floor((score - 10) / 2.0)` — [CharacterSheet.cs](Data/CharacterModel/CharacterSheet.cs#L113), [CharacterSheetModal.cs](Combat/UI/Overlays/CharacterSheetModal.cs#L196), [CharacterTab.cs](Combat/UI/Screens/CharacterTab.cs#L610), and the new unified accessor

**Step 0.3 — Kill `EquipmentLoadout` (3-slot), unify on 12-slot `EquipSlot`**
- Delete `EquipmentLoadout` class and `EquipmentSlot` enum from [EquipmentDefinition.cs](Data/CharacterModel/EquipmentDefinition.cs#L25-L102)
- Migrate `PassiveRuleService` and `ConditionEvaluator` reads to `InventoryService.GetEquippedItem()`
- Update scenario JSON files and `ScenarioLoader` to use 12-slot format

**Step 0.4 — Kill deprecated `CombatantResourcePool`**
- Remove old `combatant.ResourcePool` from `Combatant`
- Fix the critical turn-2 lockout: `ActionBudget.ResetForTurn()` must also restore `ActionResources["ActionPoint"]` and `"BonusActionPoint"`

**Step 0.5 — Kill `DataRegistry` action lookup, unify on `ActionRegistry`**
- Remove `_dataRegistry.GetAction(id)` fallback from all action lookups
- Migrate custom-only actions into the `ActionRegistry` pipeline

**Step 0.6 — Fix race JSON `"Armor"` → `"ArmorCategories"` key mismatch**
- Rename in all files under [Data/Races/](Data/Races/) to match [Feature.cs](Data/CharacterModel/Feature.cs#L71) `ProficiencyGrant.ArmorCategories`

**Verification:** `ci-build.sh` + `ci-test.sh` + autobattle seed 42 — STR 16 Fighter has +3 modifier, not +0

---

## Phase 1: CombatArena Decomposition (Architecture)

*Full rewrite. CombatArena becomes a ~200–400 line scene orchestrator.*

**Step 1.1 — Extract `ActionBarService`** → `Combat/Services/ActionBarService.cs`
- Move: PopulateActionBar, RefreshActionBarUsability, GetActionsForCombatant, GetCommonActions, sort/classify/icon/override helpers (~L4740–L5420)

**Step 1.2 — Extract `TurnLifecycleService`** → `Combat/Services/TurnLifecycleService.cs`
- Move: BeginTurn, EndCurrentTurn, EndCombat, ProcessDeathSave, resource refresh, budget tracking, threatened sync, rule window dispatch (~L1668–L2040, L6249–L6543)

**Step 1.3 — Extract `ActionExecutionService`** → `Combat/Services/ActionExecutionService.cs`
- Move: ExecuteAction (all overloads), ExecuteAbilityAtPosition, ExecuteResolvedAction, UseItem variants, Dash/Disengage/Dip/Hide/Help/Throw (~L2719–L3082, L4362–L4640, L5536–L5660)

**Step 1.4 — Extract `CombatCameraService`** → `Combat/Services/CombatCameraService.cs`
- Move: camera setup, orbit, follow, framing, presentation request handling (~L6057–L6481, L3826)

**Step 1.5 — Extract `CombatPresentationService`** → `Combat/Services/CombatPresentationService.cs`
- Move: timeline building, marker emission, VFX coordination, status visual feedback (~L3418–L3826, L4018–L4230)

**Step 1.6 — Extract `CombatMovementCoordinator`** → `Combat/Services/CombatMovementCoordinator.cs`
- Move: ExecuteMovement, preview, navigation queries, jump paths (~L780–L912, L5457–L5660)

**Step 1.7 — Extract `ReactionCoordinator`** → `Combat/Services/ReactionCoordinator.cs`
- Move: reaction prompts, AI decision, resolution (~L5831–L6000)

**Step 1.8 — Extract `ScenarioBootService`** → `Combat/Services/ScenarioBootService.cs`
- Move: LoadScenario variants, visual spawning, service registration (~L1284–L1652)

**Step 1.9 — Rewrite `CombatArena`** as thin orchestrator
- `_Ready()`: create CombatContext, register services, boot scenario
- Input: delegate to CombatInputHandler
- Event wiring: connect service events to each other

**Verification:** `ci-godot-log-check.sh` + autobattle seed 1–100 — identical results to pre-decomposition

---

## Phase 2: Actions & Spells Mechanical Fixes

*Depends on Phase 0+1*

| Step | Fix | Key File |
|---|---|---|
| 2.1 | Fix save-or-half condition mapping (use `ParseSaveType()`, not `SpellSaveDC` field) | [BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs#L537) |
| 2.2 | Implement dice formula division (`"3d6/2"` → correct half damage) | [SpellEffectConverter.cs](Data/Actions/SpellEffectConverter.cs), [Effect.cs](Combat/Actions/Effects/Effect.cs) |
| 2.3 | Implement cantrip scaling (1/2/3/4× base dice at levels 1/5/11/17) | New in EffectPipeline or ActionExecutionService |
| 2.4 | Fix BG3 ActionResources turn reset (ActionPoint/BonusActionPoint/ReactionActionPoint) | TurnLifecycleService.BeginTurn |
| 2.5 | Fix spell component parsing from `SpellFlags` (not fabricated from school) | [BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs#L237) |
| 2.6 | Implement concentration damage checks (CON save DC=max(10,dmg/2)) | EffectPipeline damage resolution |
| 2.7 | Parse missing BG3 spell fields (MaximumTargets, RootSpellID, PowerLevel, DualWieldingUseCosts) | [BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) |
| 2.8 | Fix SpellFlags inheritance (set-union merge, not overwrite) | [BG3SpellParser.cs](Data/Parsers/BG3SpellParser.cs) |
| 2.9 | Implement short rest vs long rest resource recovery distinction | RestService, ActionResourceDefinition |
| 2.10 | Fix save DC fallback to `8+prof+abilityMod`; read spellcasting ability from ClassDefinition, not hardcoded switch | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L2227) |

**Verification:** Burning Hands deals half damage on save (not full/zero). Fire Bolt scales. Combatants act past turn 1.

---

## Phase 3: Condition & Status System Unification

*Depends on Phase 0. Parallel with Phase 2.*

| Step | Fix | Key File |
|---|---|---|
| 3.1 | Expand `ConditionEffects.cs` to full mechanical authority for all 14 D&D conditions | [ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs) |
| 3.2 | Delete `GetStatusAttackContext()` — replace with `ConditionEffects.GetAggregateEffects()` | [EffectPipeline.cs](Combat/Actions/EffectPipeline.cs#L2301) |
| 3.3 | Remove AC-hack modifiers from JSON statuses (blinded -2, stunned -2, etc.) | `Data/Statuses/*.json` |
| 3.4 | Fix specific conditions: frightened (wrong disadv), asleep/unconscious (missing auto-fail), petrified, stunned, charmed, frozen IDs | Multiple status JSONs + ConditionEffects |
| 3.5 | Instantiate `BG3StatusIntegration` + fix case sensitivity + register bless/bane | [BG3StatusIntegration.cs](Combat/Statuses/BG3StatusIntegration.cs) |
| 3.6 | Fix repeat-save DC: inject caster's spell DC into `StatusInstance.SaveDCOverride` | Status application pipeline |
| 3.7 | Implement prone stand cost (half movement) + frightened movement restriction | MovementService |

**Verification:** Stunned = auto-fail STR/DEX + advantage + autocrit 1.5m. No AC-hack double-dipping. Bless/bane work.

---

## Phase 4: Passives System Unification

*Depends on Phase 0, Phase 3.*

| Step | Fix | Key File |
|---|---|---|
| 4.1 | Add missing BoostTypes: `CharacterWeaponDamage`, `Reroll`, `TwoWeaponFighting`, `ExpertiseBonus` + implement handlers | [BoostType.cs](Combat/Rules/Boosts/BoostType.cs) |
| 4.2 | Fix ConditionEvaluator fail-open → fail-closed; implement critical BG3 pseudo-functions | [ConditionEvaluator.cs](Combat/Rules/Conditions/ConditionEvaluator.cs) |
| 4.3 | Fix `FunctorExecutor.RollDiceExpression()` for dynamic expressions (SpellPowerLevel, StrengthModifier, etc.) | [FunctorExecutor.cs](Combat/Rules/Functors/FunctorExecutor.cs#L548) |
| 4.4 | Wire `PassiveManager.RuleWindowBus` so GenericFunctorRuleProvider auto-registration becomes live | PassiveManager init |
| 4.5 | **Delete** `PassiveRuleService`, `PassiveRuleProviders.cs`, `bg3_passive_rules.json` — migrate all 12 manual passives to BG3 pipeline | Multiple files |
| 4.6 | Implement scaling passives (Rage damage by level, Sneak Attack dice by level) | BG3 passive data + LevelMapValue |
| 4.7 | Copy racial feature tags to combatant.Tags; register BG3 passive entries for racial features | ScenarioLoader/CharacterResolver |

**Verification:** Rage scales +2/+3/+4. Dueling +2 works. Unknown conditions don't fire. Halfling rerolls 1s.

---

## Phase 5: Equipment System Completion

*Depends on Phase 0. Parallel with Phase 4.*

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
- `CombatArena`: **rewritten** as ~200–400 lines. All logic in dedicated services

## Phase Dependencies
```
Phase 0 (Foundation) ──→ Phase 1 (CombatArena decomp) ──→ Phases 2-9
                    ╲                                    ╱
                     ╰─→ Phase 3 (Conditions) ─────────╯
                     ╰─→ Phase 2 (Actions) can start after Phase 0
Phases 2, 3: parallel
Phases 4, 5, 6: parallel (all depend on Phase 0; Phase 4 also needs Phase 3)
Phase 7: depends on Phase 4 + 6
Phases 8, 9: parallel polish (depend on Phase 1)
```
