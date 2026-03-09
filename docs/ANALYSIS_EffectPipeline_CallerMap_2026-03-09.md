# EffectPipeline Caller Map
**Generated:** 2026-03-09  
**Purpose:** Complete external dependency map for EffectPipeline refactoring/split planning.  
**Method:** `grep -rn` across all `.cs` files, excluding the class file itself. All line numbers verified against repo HEAD.

---

## Quick Summary

EffectPipeline is depended on by **16 production files** and **20+ test files**.  
Its public surface splits cleanly into five responsibility areas:

| Area | External Callers (prod) | Methods |
|---|---|---|
| **Storage** (action registry) | 8 files | `RegisterAction`, `GetAction` |
| **Execution** | 6 files | `ExecuteAction`, `CanUseAbility`, `PreviewAbility` |
| **Calculation** | 2 files | `GetAttackBonus`, `GetSaveDC`, `GetSaveBonus` |
| **Lifecycle** | 2 files | `ProcessTurnStart`, `ProcessRoundEnd`, `ExportCooldowns`, `ImportCooldowns` |
| **Events** | 2 files | `OnAbilityExecuted` (subscribe/unsubscribe) |
| **Property injection** | 3 files | `Rng`, `TestPolicy`, `ActionRegistry`, `OnHitTriggerService` |

---

## Part 1 — Who Holds a Reference to EffectPipeline

### 1a. Constructor / dependency injection (receives as ctor param)

| File | Line | How injected |
|---|---|---|
| [Combat/Services/ActionBarService.cs](../Combat/Services/ActionBarService.cs#L32) | 32 | `EffectPipeline effectPipeline` ctor param → stored at L22 |
| [Combat/Services/ActionExecutionService.cs](../Combat/Services/ActionExecutionService.cs#L77) | 77 | `EffectPipeline effectPipeline` ctor param → stored at L38 |
| [Combat/Services/ReactionCoordinator.cs](../Combat/Services/ReactionCoordinator.cs#L45) | 45 | `EffectPipeline effectPipeline` ctor param → stored at L32 |
| [Combat/Services/SelectionService.cs](../Combat/Services/SelectionService.cs#L58) | 58 | `EffectPipeline effectPipeline` ctor param → stored at L47 |
| [Combat/Services/TurnLifecycleService.cs](../Combat/Services/TurnLifecycleService.cs#L95) | 95 | `EffectPipeline effectPipeline` ctor param → stored at L31 |

### 1b. Property injection (set after construction)

| File | Line | How set |
|---|---|---|
| [Combat/Services/CombatPresentationService.cs](../Combat/Services/CombatPresentationService.cs#L911) | 911 | Method call wires field at L38 |
| [Combat/Rules/Functors/FunctorExecutor.cs](../Combat/Rules/Functors/FunctorExecutor.cs#L59) | 59 | `public EffectPipeline EffectPipeline { get; set; }` — wired from `CombatArena.cs:1154` |

### 1c. Resolved via `ICombatContext.GetService<EffectPipeline>()` (on-demand)

| File | Lines | Pattern |
|---|---|---|
| [Combat/AI/AIDecisionPipeline.cs](../Combat/AI/AIDecisionPipeline.cs#L163) | 163 | Resolved once in `Initialize()`, stored in `_effectPipeline` field (L108) |
| [Combat/AI/AIScorer.cs](../Combat/AI/AIScorer.cs#L213) | 213, 253, 443, 1118, 1140, 1214, 1234, 1529, 1578 | Resolved inline at each call site (no stored field) |
| [Combat/Arena/CombatInputHandler.cs](../Combat/Arena/CombatInputHandler.cs#L662) | 662, 721, 760 | Resolved inline at each call site |
| [Combat/Services/ScenarioBootService.cs](../Combat/Services/ScenarioBootService.cs#L231) | 231, 317 | Resolved inline; sets `Rng` and `TestPolicy` properties |
| [Combat/UI/HudController.cs](../Combat/UI/HudController.cs#L2678) | 2678, 2700 | Resolved inline for preview overlay |
| [Combat/Persistence/CombatSaveService.cs](../Combat/Persistence/CombatSaveService.cs#L43) | 43, 121 | Resolved inline for save/load |
| [Tools/AutoBattler/AutoBattleRuntime.cs](../Tools/AutoBattler/AutoBattleRuntime.cs#L107) | 107 | Resolved once in `Initialize()`, stored at L55 |
| [Tools/AutoBattler/RealtimeAIController.cs](../Tools/AutoBattler/RealtimeAIController.cs#L446) | 446, 504 | Resolved inline at each call site |
| [Tools/AutoBattler/UIAwareAIController.cs](../Tools/AutoBattler/UIAwareAIController.cs#L520) | 520, 825, 864, 963, 1059, 1105 | Resolved inline at each call site |

### 1d. Construction site

| File | Line | Note |
|---|---|---|
| [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs#L110) | 110 | `new EffectPipeline { ... }` — the sole construction site |
| [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs#L37) | 37 | `public EffectPipeline EffectPipeline;` — field on `Registries` struct passed everywhere |

### 1e. Via `context.Pipeline` (Effect base class)

| File | Line | Note |
|---|---|---|
| [Combat/Actions/Effects/Effect.cs](../Combat/Actions/Effects/Effect.cs#L116) | 116 | `public EffectPipeline Pipeline { get; set; }` — every `EffectContext` carries a back-reference |
| [Combat/Actions/Effects/CreateExplosionEffect.cs](../Combat/Actions/Effects/CreateExplosionEffect.cs#L60) | 60 | Uses it for sub-spell execution |
| [Combat/Actions/Effects/ExtendedEffects.cs](../Combat/Actions/Effects/ExtendedEffects.cs#L480) | 480, 570 | Uses it for sub-spell execution |
| [Combat/Actions/Effects/SummonCombatantEffect.cs](../Combat/Actions/Effects/SummonCombatantEffect.cs#L58) | 58 | Reads `GetSaveDC` for summon inherit |
| [Combat/Actions/Effects/DealDamageEffect.cs](../Combat/Actions/Effects/DealDamageEffect.cs#L762) | 762 | Calls `context.Pipeline.TryTriggerAllyDownedReactions` |

---

## Part 2 — Storage Area: `RegisterAction` / `GetAction`

### Important architecture point

`EffectPipeline` maintains its **own internal `_actions` dictionary** (line 111) in addition to holding a reference to the shared `ActionRegistry`. `GetAction` checks `_actions` first, then falls back to `ActionRegistry`. `RegisterAction` writes to both.

Callers using `ActionRegistry.GetAction` directly (bypassing EffectPipeline) are tracked separately in §2c below.

### 2a. `EffectPipeline.RegisterAction` callers (production)

| File | Lines | Context |
|---|---|---|
| [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs#L212) | 212 | Registers all abilities built by `CharacterBuilder` for each combatant |
| [Combat/Services/ScenarioBootService.cs](../Combat/Services/ScenarioBootService.cs#L231) | (implicit via above) | Does not call directly; calls through RegistryInitializer bootstrap |

### 2b. `EffectPipeline.GetAction` callers (production)

| File | Lines | Context |
|---|---|---|
| [Combat/Arena/CombatArena.cs](../Combat/Arena/CombatArena.cs#L225) | 225 | `_effectPipeline?.GetAction(actionId)` — public `GetActionById()` facade |
| [Combat/AI/AIDecisionPipeline.cs](../Combat/AI/AIDecisionPipeline.cs#L994) | 994, 1282, 1427, 1545, 1784, 2193, 2394, 2446, 2475, 2975, 3439, 3447, 3475, 3501, 3755, 3849, 3860, 3881, 3890 | Massive usage — reads action metadata for scoring, range, target type, spell level |
| [Combat/AI/AIScorer.cs](../Combat/AI/AIScorer.cs#L1125) | 1125, 1141, 1215, 1235, 1530, 1583, 214, 254, 444 | Same — reads action metadata for damage/target type scoring |
| [Combat/Arena/CombatInputHandler.cs](../Combat/Arena/CombatInputHandler.cs#L663) | 663, 722, 761 | Reads targeting mode from action definition |
| [Combat/Arena/CustomFight/CustomFightLogger.cs](../Combat/Arena/CustomFight/CustomFightLogger.cs#L235) | 235 | Reads action name for logging |
| [Combat/Services/ActionExecutionService.cs](../Combat/Services/ActionExecutionService.cs#L221) | 221, 268, 301, 346, 396, 422, 468, 542, 616, 681, 1152 | Validates action exists before dispatch; reads action def for dispatch routing |
| [Combat/Services/ReactionCoordinator.cs](../Combat/Services/ReactionCoordinator.cs#L203) | 203 | Verifies action still valid before reaction execution |
| [Combat/Services/SelectionService.cs](../Combat/Services/SelectionService.cs#L116) | 116 | Reads action targeting mode to configure UI |
| [Tools/AutoBattler/AutoBattleRuntime.cs](../Tools/AutoBattler/AutoBattleRuntime.cs#L198) | 198, 572 | Reads action name for logging |
| [Tools/AutoBattler/RealtimeAIController.cs](../Tools/AutoBattler/RealtimeAIController.cs#L448) | 448, 505 | Validates action and item action before dispatch |
| [Tools/AutoBattler/UIAwareAIController.cs](../Tools/AutoBattler/UIAwareAIController.cs#L826) | 826, 865, 1061, 1106 | Validates action before dispatch |

### 2c. `ActionRegistry.GetAction` callers (bypass EffectPipeline — via separately-injected ActionRegistry)

These files hold `ActionRegistry` directly AND call `GetAction` on it. They do NOT go through EffectPipeline:

| File | Lines | Context |
|---|---|---|
| [Combat/Services/ActionBarService.cs](../Combat/Services/ActionBarService.cs#L79) | 79, 102, 375, 436, 814 | Hotbar — reads actions for display, uses `_actionRegistry` field |
| [Data/CharacterModel/CharacterResolver.cs](../Data/CharacterModel/CharacterResolver.cs#L334) | 334, 477, 526 | Character building — validates ability IDs |
| [Data/Items/BG3ConsumableResolver.cs](../Data/Items/BG3ConsumableResolver.cs#L164) | 164, 172 | Consumable → action linking |
| [Data/ScenarioGenerator.cs](../Data/ScenarioGenerator.cs#L182) | 182, 273, 592 | Scenario setup — validates action IDs |
| [Data/Actions/ActionRegistryInitializer.cs](../Data/Actions/ActionRegistryInitializer.cs#L161) | 161, 187 | `RegisterAction` + `GetAction` on ActionRegistry directly |
| [Data/Actions/ActionDataLoader.cs](../Data/Actions/ActionDataLoader.cs#L137) | 137 | `RegisterAction` on ActionRegistry directly |
| [Combat/UI/Panels/ActionEditorPanel.cs](../Combat/UI/Panels/ActionEditorPanel.cs#L1052) | 1052, 1069 | Editor tool — `RegisterAction` on ActionRegistry directly |

### 2d. Query methods: `GetActionsByTag`, `GetActionsByLevel`, `GetActionsBySchool`

These are **not on EffectPipeline** — they exist only on `ActionRegistry`. No callers go through EffectPipeline for these.

---

## Part 3 — Execution Area: `ExecuteAction`, `CanUseAbility`, `PreviewAbility`

### 3a. `ExecuteAction` callers (production)

| File | Lines | Method signature used | Context |
|---|---|---|---|
| [Combat/Services/ActionExecutionService.cs](../Combat/Services/ActionExecutionService.cs#L981) | 981 | `ExecuteAction(id, actor, targets, options)` | **Primary production execution path** for all player/AI actions |
| [Combat/Services/ReactionCoordinator.cs](../Combat/Services/ReactionCoordinator.cs#L226) | 226 | `ExecuteAction(id, reactor, targets, reactionOptions)` | Reaction execution after prompt |
| [Combat/Rules/Functors/FunctorExecutor.cs](../Combat/Rules/Functors/FunctorExecutor.cs#L1014) | 1014 | `EffectPipeline.ExecuteAction(spellId, castSource, targets, options)` | Functor-triggered sub-spell (UseSpell stub) |
| [Combat/Actions/Effects/CreateExplosionEffect.cs](../Combat/Actions/Effects/CreateExplosionEffect.cs#L60) | 60 | `context.Pipeline.ExecuteAction(spellId, source, targets, options)` | Chain explosion effect |
| [Combat/Actions/Effects/ExtendedEffects.cs](../Combat/Actions/Effects/ExtendedEffects.cs#L480) | 480, 570 | `context.Pipeline.ExecuteAction(...)` | Sub-spell triggers (e.g. Wall of Fire per-entry damage) |

**Key routing note:** `CombatArena.ExecuteAction()` (lines 1861–1876) is a public facade that **delegates to `ActionExecutionService.ExecuteAction`**, not directly to `EffectPipeline.ExecuteAction`. The Arena's public API is therefore one level up from EffectPipeline.

### 3b. `CanUseAbility` callers (production)

| File | Lines | Context |
|---|---|---|
| [Combat/AI/AIDecisionPipeline.cs](../Combat/AI/AIDecisionPipeline.cs#L278) | 278, 1031, 1278 | AI plan filtering — skip actions that fail usability |
| [Combat/Arena/CombatArena.cs](../Combat/Arena/CombatArena.cs#L1007) | 1007 | Validate reaction eligibility before prompting |
| [Combat/Services/ActionBarService.cs](../Combat/Services/ActionBarService.cs#L943) | 943 | Hotbar greying-out |
| [Combat/Services/SelectionService.cs](../Combat/Services/SelectionService.cs#L120) | 120 | Pre-select validation |
| [Tools/AutoBattler/RealtimeAIController.cs](../Tools/AutoBattler/RealtimeAIController.cs#L454) | 454 | AI dispatch gate |
| [Tools/AutoBattler/UIAwareAIController.cs](../Tools/AutoBattler/UIAwareAIController.cs#L522) | 522, 832, 871, 971, 1067 | AI dispatch gate (multiple action categories) |

### 3c. `PreviewAbility` callers (production)

| File | Line | Context |
|---|---|---|
| [Combat/AI/AIDecisionPipeline.cs](../Combat/AI/AIDecisionPipeline.cs#L3103) | 3103 | AoE damage estimation for target-selection scoring |

Only one production call site.

---

## Part 4 — Calculation Area: `GetAttackBonus`, `GetSaveDC`, `GetSaveBonus`

### 4a. `GetAttackBonus`

| File | Line | Context |
|---|---|---|
| [Combat/UI/HudController.cs](../Combat/UI/HudController.cs#L2679) | 2679 | Attack preview overlay ("To Hit: +5") |

### 4b. `GetSaveDC`

| File | Line | Context |
|---|---|---|
| [Combat/UI/HudController.cs](../Combat/UI/HudController.cs#L2703) | 2703 | Save DC preview overlay |
| [Combat/Actions/Effects/SummonCombatantEffect.cs](../Combat/Actions/Effects/SummonCombatantEffect.cs#L58) | 58 | Summon inherits summoner's spell save DC |

### 4c. `GetSaveBonus`

| File | Line | Context |
|---|---|---|
| [Combat/UI/HudController.cs](../Combat/UI/HudController.cs#L2704) | 2704 | Target's save bonus for preview overlay |

### 4d. Not called externally in production

These are **internal only** — called from within EffectPipeline itself:
- `GetCriticalThreshold`
- `GetAttackRollBonus`
- `IsWeaponProficient`
- `ValidateBG3ResourceCost`
- `ConsumeBG3ResourceCost`
- `ShouldAutoFailSave`
- `CombineDiceFormulas` (also tested via reflection in `EffectPipelineIntegrationTests.cs:194`)
- `ParseDiceFormula`
- `ComputeSaveDC` (public wrapper `GetSaveDC` is called externally; `ComputeSaveDC` is the internal implementation)

---

## Part 5 — Lifecycle Area: `ProcessTurnStart`, `ProcessRoundEnd`, `ExportCooldowns`, `ImportCooldowns`, `Reset`

### 5a. `ProcessTurnStart`

| File | Line | Context |
|---|---|---|
| [Combat/Services/TurnLifecycleService.cs](../Combat/Services/TurnLifecycleService.cs#L354) | 354 | Called in `BeginTurn()` — ticks per-turn cooldowns (charges restored by turn) |

Called in this sequence at L353–361:  
`_effectPipeline.ProcessTurnStart` → `_surfaceManager.ProcessTurnStart` → `_auraSystem.ProcessTurnStartAuras` → `_statusManager.ProcessTurnStart`

### 5b. `ProcessRoundEnd`

| File | Lines | Context |
|---|---|---|
| [Combat/Services/TurnLifecycleService.cs](../Combat/Services/TurnLifecycleService.cs#L690) | 690, 771 | Called at end of each round and at combat end — ticks per-round cooldowns |

### 5c. `ExportCooldowns`

| File | Line | Context |
|---|---|---|
| [Combat/Persistence/CombatSaveService.cs](../Combat/Persistence/CombatSaveService.cs#L86) | 86 | Serializes cooldown state into `CombatSnapshot.ActionCooldowns` |

### 5d. `ImportCooldowns`

| File | Line | Context |
|---|---|---|
| [Combat/Persistence/CombatSaveService.cs](../Combat/Persistence/CombatSaveService.cs#L161) | 161 | Restores cooldown state from `CombatSnapshot.ActionCooldowns` |

### 5e. `Reset`

No production callers found. Only called in unit tests:
- [Tests/Unit/EffectPipelineIntegrationTests.cs](../Tests/Unit/EffectPipelineIntegrationTests.cs) (line 1391 area — `pipeline.Reset()` test)

---

## Part 6 — Events Area

### 6a. `OnAbilityExecuted` (the only event with production subscribers)

| File | Lines | Type | Context |
|---|---|---|---|
| [Combat/Arena/CombatArena.cs](../Combat/Arena/CombatArena.cs#L1337) | 1337 | `+=` subscribe | Wires `_actionExecutionService.OnAbilityExecuted` — the primary combat log callback |
| [Combat/Arena/CustomFight/CustomFightLogger.cs](../Combat/Arena/CustomFight/CustomFightLogger.cs#L84) | 84, 155 | `+=` / `-=` | Subscribes on enable, unsubscribes on disable — JSONL combat log writer |
| [Tools/AutoBattler/AutoBattleRuntime.cs](../Tools/AutoBattler/AutoBattleRuntime.cs#L110) | 110, 184, 237, 238 | `+=` / `-=` | Two subscribers (detail logger + damage logger) with paired unsubscribes |

**Where the event fires internally:** `EffectPipeline.cs:1350` (end of `ExecuteAction` main path).  
**Public fire method:** `NotifyAbilityExecuted(result)` at line 195 — called from outside to synthesize fire for validation failures.

### 6b. `OnDamageTrigger`, `OnAttackTrigger`, `OnHitTrigger`, `OnAbilityCastTrigger`

**Production subscribers: NONE.**

These four events exist but have no production-code subscribers. They are:
1. Fired from within EffectPipeline's `TryTrigger*` methods (which call `ReactionSystem`/`ReactionResolver` directly)
2. Only subscribed to by test code in [Tests/Unit/EffectPipelineReactionTests.cs](../Tests/Unit/EffectPipelineReactionTests.cs) and [Tests/Unit/MultiProjectileTests.cs](../Tests/Unit/MultiProjectileTests.cs)

The production reaction flow is: `TryTrigger*()` → calls `Reactions.GetEligibleReactors()` or `ReactionResolver.ResolveTrigger()` → then *also* fires the event → but no subscriber in production acts on it.

**Conclusion:** These events are test-inspection hooks. A split can demote them or replace with direct callbacks without breaking production behavior.

### 6c. `OnEffectUnhandled`

**Production subscribers: NONE.** Only fired internally when an `Effect` type is not registered.

---

## Part 7 — Property Injection (non-method API surface)

| Property | Who Sets It | Line | Purpose |
|---|---|---|---|
| `Rules` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | The `RulesEngine` |
| `Statuses` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | `StatusManager` |
| `Rng` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | Seeded RNG |
| `Rng` (override) | [Combat/Services/ScenarioBootService.cs](../Combat/Services/ScenarioBootService.cs#L483) | 483 | Re-seeds with scenario seed |
| `ActionRegistry` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs#L139) | 139 | Wires the shared ActionRegistry post-construction |
| `CombatContext` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | Service locator access |
| `TurnQueue` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | For turn-order context |
| `Heights` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | Height advantage |
| `LOS` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | Cover/obstruction |
| `Reactions` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | ReactionSystem |
| `ReactionResolver` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | IReactionResolver |
| `Concentration` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | ConcentrationSystem |
| `Surfaces` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | SurfaceManager |
| `ForcedMovement` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | ForcedMovementService |
| `OnHitTriggerService` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs#L223) | 223 | Wired after service construction |
| `DataRegistry` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | Item/template lookup |
| `GetCombatants` | [Data/RegistryInitializer.cs](../Data/RegistryInitializer.cs) | At construction | `Func<IEnumerable<Combatant>>` |
| `TestPolicy` | [Combat/Services/ScenarioBootService.cs](../Combat/Services/ScenarioBootService.cs#L235) | 235 | Overrides dice roll behaviour for tests |

---

## Part 8 — Test Files

### Direct `new EffectPipeline()` instantiations

| File | Lines | What is tested |
|---|---|---|
| [Tests/Unit/EffectPipelineIntegrationTests.cs](../Tests/Unit/EffectPipelineIntegrationTests.cs) | L28ff | Full pipeline: attacks, saves, damage formulas, LOS, fog, healing, enchantments, multi-target, sub-spell, concentration, cooldowns, PreviewAbility, Reset |
| [Tests/Unit/EffectPipelineReactionTests.cs](../Tests/Unit/EffectPipelineReactionTests.cs) | L27ff | Reaction triggers (OnAbilityCastTrigger, OnDamageTrigger, OnAttackTrigger, OnHitTrigger), Cancel/Modify, Shield, Uncanny Dodge |
| [Tests/Unit/MultiProjectileTests.cs](../Tests/Unit/MultiProjectileTests.cs) | L316ff | Magic Missile (3/4 darts), Scorching Ray (3 beams), auto-hit, per-projectile reaction triggers |
| [Tests/Unit/MultiTargetSaveTests.cs](../Tests/Unit/MultiTargetSaveTests.cs) | L21ff | Multi-target save spells (Fireball), per-target SaveResultsByTarget |
| [Tests/Unit/SaveTakesHalfTests.cs](../Tests/Unit/SaveTakesHalfTests.cs) | L23ff | Half-damage on successful save |
| [Tests/Unit/TollTheDeadTests.cs](../Tests/Unit/TollTheDeadTests.cs) | L23ff | Toll the Dead formula switching (d8 vs d12) |
| [Tests/Unit/DodgeAndThreatenedTests.cs](../Tests/Unit/DodgeAndThreatenedTests.cs) | L22ff | Dodge condition, threatened disadvantage |
| [Tests/Unit/OnHitTriggerServiceTests.cs](../Tests/Unit/OnHitTriggerServiceTests.cs) | L63ff | OnHitTriggerService wired into pipeline |
| [Tests/Unit/SaveDCCalculationTests.cs](../Tests/Unit/SaveDCCalculationTests.cs) | L36 | GetSaveDC stat-derived and fixture-configured cases |
| [Tests/Unit/SaveDebugTest.cs](../Tests/Unit/SaveDebugTest.cs) | L28 | Save debug/inspection |
| [Tests/Unit/SpawnObjectEffectTests.cs](../Tests/Unit/SpawnObjectEffectTests.cs) | L22ff | SpawnObjectEffect via pipeline |
| [Tests/Unit/WildShapeTransformationTests.cs](../Tests/Unit/WildShapeTransformationTests.cs) | L361, 397 | WildShape action execution |
| [Tests/Unit/Phase2RuntimeSemanticsTests.cs](../Tests/Unit/Phase2RuntimeSemanticsTests.cs) | L219 | Checks phase 2 no-op handlers registered |
| [Tests/Unit/ReactionParityPlanTests.cs](../Tests/Unit/ReactionParityPlanTests.cs) | L146, 269, 311 | Reaction eligibility checks |
| [Tests/Unit/CombatSaveServiceTests.cs](../Tests/Unit/CombatSaveServiceTests.cs) | L51, 101, 175, 475, 510, 574, 609 | ExportCooldowns, ImportCooldowns round-trip |
| [Tests/Unit/AIDecisionPipelineTests.cs](../Tests/Unit/AIDecisionPipelineTests.cs) | L235ff | AI decisions wired through EffectPipeline |
| [Tests/Integration/ActionPresentationTimelineIntegrationTests.cs](../Tests/Integration/ActionPresentationTimelineIntegrationTests.cs) | L30 | RegisterAction + ExecuteAction + timeline events |
| [Tests/Integration/DynamicFormulaResolutionTests.cs](../Tests/Integration/DynamicFormulaResolutionTests.cs) | L124 | Dynamic formula resolution |
| [Tests/Integration/ExtraAttackIntegrationTest.cs](../Tests/Integration/ExtraAttackIntegrationTest.cs) | L329 | Extra Attack, two-weapon, attack budget |
| [Tests/Integration/ShoveActionTests.cs](../Tests/Integration/ShoveActionTests.cs) | L62 | Shove action |
| [Tests/Integration/UpcastingTests.cs](../Tests/Integration/UpcastingTests.cs) | L161 | Upcasting (Cure Wounds, Burning Hands, Magic Missile, Scorching Ray) |
| [Tests/Integration/CombatArenaSystemsWiringTests.cs](../Tests/Integration/CombatArenaSystemsWiringTests.cs) | L67, 144, 169 | LOS wiring, height wiring, reaction wiring |
| [Tests/Simulation/ActionComprehensiveTests.cs](../Tests/Simulation/ActionComprehensiveTests.cs) | L38 | Full action simulation suite |
| [Tests/Simulation/MultiRoundStabilityTests.cs](../Tests/Simulation/MultiRoundStabilityTests.cs) | L39 | Multi-round stability, ProcessTurnStart |
| [Tests/Helpers/ParityDataValidator.cs](../Tests/Helpers/ParityDataValidator.cs) | L922 | `new EffectPipeline().GetRegisteredEffectTypes()` — parity check |

### Test files using a `TestEffectPipeline` subclass

| File | Lines | Nature of subclass |
|---|---|---|
| [Tests/Unit/ActionVariantTests.cs](../Tests/Unit/ActionVariantTests.cs) | L108 | `TestEffectPipeline` has no registered effects; tests variant selection logic only |
| [Tests/Unit/EffectSystemTests.cs](../Tests/Unit/EffectSystemTests.cs) | L72 | `TestEffectPipeline` — minimal harness for individual Effect types |

---

## Part 9 — Complete Method → Caller Cross-Reference

| Method / Property / Event | Production callers | Test callers |
|---|---|---|
| `ExecuteAction(string, Combatant, List<Combatant>)` | ActionExecutionService, ReactionCoordinator, FunctorExecutor, CreateExplosionEffect, ExtendedEffects(×2) | Most unit/integration test files |
| `CanUseAbility(string, Combatant)` | AIDecisionPipeline(×3), CombatArena, ActionBarService, SelectionService, RealtimeAI, UIAwareAI(×5) | ExtraAttackIntegrationTest, EffectPipelineIntegrationTests, ActionComprehensiveTests |
| `PreviewAbility(string, Combatant, List<Combatant>)` | AIDecisionPipeline(×1) | EffectPipelineIntegrationTests(×2) |
| `RegisterAction(ActionDefinition)` | RegistryInitializer | Many test helpers |
| `GetAction(string)` | CombatArena, AIDecisionPipeline(×20), AIScorer(×9), CombatInputHandler(×3), CustomFightLogger, ActionExecutionService(×11), ReactionCoordinator, SelectionService, AutoBattleRuntime(×2), RealtimeAI(×2), UIAwareAI(×4) | ExtraAttackIntegration, ActionComprehensiveTests, etc. |
| `GetSaveDC(Combatant, ActionDefinition)` | HudController, SummonCombatantEffect | SaveDCCalculationTests |
| `GetSaveBonus(Combatant, string)` | HudController | SaveDCCalculationTests |
| `GetAttackBonus(Combatant, ActionDefinition)` | HudController | (none directly) |
| `ProcessTurnStart(string)` | TurnLifecycleService | MultiRoundStabilityTests, EffectPipelineIntegrationTests |
| `ProcessRoundEnd()` | TurnLifecycleService(×2) | (none) |
| `ExportCooldowns()` | CombatSaveService | CombatSaveServiceTests |
| `ImportCooldowns(List<CooldownSnapshot>)` | CombatSaveService | CombatSaveServiceTests |
| `Reset()` | (none) | EffectPipelineIntegrationTests |
| `NotifyAbilityExecuted(result)` | (none found externally; internal-only use) | — |
| `GetRegisteredEffectTypes()` | (none) | ParityDataValidator, SpellCoverageByClassTests |
| `TryTriggerDamageReactions(...)` | DealDamageEffect(via context.Pipeline) | — |
| `TryTriggerAllyDownedReactions(...)` | DealDamageEffect(via context.Pipeline) | — |
| `TryTriggerAttackReactions(...)` | (internal only from ExecuteAction) | — |
| `TryTriggerHitReactions(...)` | (internal only from ExecuteAction) | — |
| `OnAbilityExecuted +=` | CombatArena(×1), CustomFightLogger(×1), AutoBattleRuntime(×2) | — |
| `OnDamageTrigger +=` | **none** | EffectPipelineReactionTests |
| `OnAttackTrigger +=` | **none** | MultiProjectileTests |
| `OnHitTrigger +=` | **none** | — |
| `OnAbilityCastTrigger +=` | **none** | EffectPipelineReactionTests |

---

## Part 10 — Refactoring Risk Assessment

### High coupling — must preserve public contract exactly

| Method | Why |
|---|---|
| `GetAction(string)` | 20+ call sites across AI, input handling, service layer. Critical path. |
| `ExecuteAction(...)` | Primary chain from ActionExecutionService through FunctorExecutor and sub-spell Effects. |
| `CanUseAbility(...)` | AI planning hotpath and UI gating. Any signature change breaks both layers. |
| `RegisterAction(ActionDefinition)` | Single registration path for character abilities; both EffectPipeline internal dict AND ActionRegistry must be kept in sync. |

### Medium coupling — manageable decoupling

| Method/Event | Why manageable |
|---|---|
| `GetSaveDC`, `GetSaveBonus`, `GetAttackBonus` | Only HudController + SummonCombatantEffect. Could be moved to RulesEngine facade. |
| `ProcessTurnStart`, `ProcessRoundEnd` | Single caller each (TurnLifecycleService). Easy to re-route if extracted to CooldownService. |
| `ExportCooldowns`, `ImportCooldowns` | Single caller (CombatSaveService). Self-contained serialization. |
| `OnAbilityExecuted` event | 3 production subscribers, all subscribing at setup time. Bridge with `NotifyAbilityExecuted()` covers validation paths. |

### Low coupling — can change freely

| Method/Event | Why safe |
|---|---|
| `OnDamageTrigger`, `OnAttackTrigger`, `OnHitTrigger`, `OnAbilityCastTrigger` | **No production subscribers.** Test-inspection hooks only. |
| `Reset()` | No production callers. Test cleanup only. |
| `PreviewAbility` | Single production caller (AIDecisionPipeline AoE scoring). |
| `GetRegisteredEffectTypes()` | Tests only. |
| `TryTrigger*` public methods | Only called from `DealDamageEffect` via `context.Pipeline`; not called externally by any service. |

### Architecture observation: dual-registry split

`EffectPipeline._actions` (internal dict) and `ActionRegistry` (shared) have a dual-write/fallback-read relationship. If you split EffectPipeline, the storage concern must make a hard choice: **one canonical source** rather than both. Currently every `GetAction` call first hits `_actions`, which is only populated via `EffectPipeline.RegisterAction`; `ActionRegistry` is populated from both `EffectPipeline.RegisterAction` and direct `ActionRegistry.RegisterAction` calls. A split that removes the internal dict and points everything to `ActionRegistry` would simplify this significantly, but requires all callers of `EffectPipeline.GetAction` to be updated to `ActionRegistry.GetAction`.
