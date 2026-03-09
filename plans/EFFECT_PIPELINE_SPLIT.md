# Plan: Split EffectPipeline into Focused Subsystems

## TL;DR

Split `EffectPipeline.cs` (3,451 lines, 15 injected services, 67 methods) into 7 focused classes. The current class is simultaneously an action store, execution engine, roll calculator, resource validator, reaction dispatcher, cooldown tracker, and effect builder. Each extracted class gets only the 2–4 service dependencies it actually needs. The `EffectPipeline` class survives as a slim orchestrator (~800 lines) that delegates to the new subsystems.

**Net result:** Six new files totalling ~2,600 lines extracted. The remaining `EffectPipeline` shrinks from 3,451 → ~850 lines. Zero public API changes for callers — a thin facade preserves all existing method signatures during migration.

---

## Current State

### Responsibilities (11 groups, mapped by method inventory)

| Group | Methods | Lines | Key Services Used |
|-------|---------|-------|-------------------|
| 1. Action Storage | `RegisterAction`, `GetAction`, `RegisterEffect`, internal `_actions` dict | ~100 | `ActionRegistry` |
| 2. Pre-Execution Validation | `CanUseAbility`, `CanUseAbilityWithCost`, `ValidateBG3ResourceCost`, 4 helpers | ~380 | `Statuses`, `Concentration`, `TestPolicy`, `_cooldowns` |
| 3. Resource Consumption | `ConsumeBG3ResourceCost`, `BuildBudgetCostOverride`, `GetLegacyFallbackCosts` | ~150 | — (operates on Combatant data) |
| 4. Action Execution | `ExecuteAction` (2 overloads), `ExecuteMultiProjectile` | ~1,070 | Nearly all 15 services |
| 5. Effect Building | `BuildEffectiveCost`, `BuildEffectiveEffects`, `BuildEffectiveTags`, 5 helpers | ~350 | — (pure data transforms) |
| 6. Attack Roll Calc | `GetAttackBonus`, `GetAttackRollBonus`, `IsWeaponProficient`, 3 helpers | ~160 | `GetCombatants`, BoostEvaluator (via combatant) |
| 7. Save/Contest Calc | `GetSaveDC`, `GetSaveBonus`, `ComputeSaveDC`, `GetSavingThrowBonus`, 6 helpers | ~200 | `CombatContext` (→CharacterDataRegistry), `Statuses` |
| 8. Roll Window Utilities | `ApplyWindowRollSources`, `MergeParameterSources` | ~35 | — (static) |
| 9. Reaction Triggers | `TryTriggerDamageReactions`, `TryTriggerAttackReactions`, `TryTriggerHitReactions`, 4 more | ~460 | `Reactions`, `ReactionResolver`, `GetCombatants` |
| 10. Cooldown Management | `ProcessTurnStart`, `ProcessRoundEnd`, `Reset`, `Export/ImportCooldowns`, `ConsumeCooldown` | ~140 | `_cooldowns` dict, `ActionRegistry` |
| 11. Preview/UI | `PreviewAbility`, public stat wrappers | ~50 | `_effectHandlers`, `Rules`, `Statuses` |

### Why This Hurts

1. **Every combat feature touches this file.** 15 optional service deps accumulate because each new system gets bolted on as a nullable property.
2. **`ExecuteAction` is 771 lines** — too long to review, test, or safely modify.
3. **Duplicated action store.** EffectPipeline has `_actions` dict that partially duplicates `ActionRegistry`. `RegisterAction` writes to both; `GetAction` reads `_actions` first, falls back to `ActionRegistry`.
4. **Untestable in isolation.** Testing any single responsibility requires constructing the whole class and wiring 3+ services.
5. **Circular coupling.** `EffectContext.Pipeline` back-references EffectPipeline for sub-spell execution. Effect classes in `Combat/Actions/Effects/` import `Combat/Services` for service access.

### Who Calls What (External Caller Map)

| API Surface | Callers | Call Sites |
|---|---|---|
| `GetAction` | AIDecisionPipeline (20+), ActionExecutionService (11), AIScorer (9), CombatInputHandler, SelectionService, ReactionCoordinator, CustomFightLogger, AutoBattler tools | ~60+ sites |
| `RegisterAction` | RegistryInitializer only (character ability load) | ~3 sites |
| `ExecuteAction` | ActionExecutionService (primary), ReactionCoordinator, FunctorExecutor, 2 Effect classes (sub-spells) | ~8 sites |
| `CanUseAbility` | AI (×3), CombatArena, ActionBarService, SelectionService, AutoBattler AI (×2) | ~8 sites |
| `PreviewAbility` | AIDecisionPipeline only (AoE scoring) | 1 site |
| `GetAttackBonus/GetSaveDC/GetSaveBonus` | HudController only (+ SummonCombatantEffect for GetSaveDC) | ~4 sites |
| `ValidateBG3ResourceCost` | 0 external callers (internal use only) | 0 |
| `ConsumeBG3ResourceCost` | 0 external callers (internal use only) | 0 |
| `ProcessTurnStart/RoundEnd` | TurnLifecycleService only | 2 sites |
| `Export/ImportCooldowns` | CombatSaveService only | 2 sites |
| `TryTriggerDamageReactions` | DealDamageEffect (via EffectContext.OnBeforeDamage delegate) | 1 site |
| `TryTriggerAllyDownedReactions` | DealDamageEffect (via `context.Pipeline`) | 1 site |
| `OnAbilityExecuted` event | ActionExecutionService, CustomFightLogger, AutoBattleRuntime | 3 subscribers |
| `OnDamageTrigger/OnAttackTrigger/OnHitTrigger/OnAbilityCastTrigger` | **0 production subscribers** (test inspection hooks only) | 0 |

---

## Target Architecture

### New Class Hierarchy

```
Combat/Actions/
├── EffectPipeline.cs              (~850 lines — orchestration only)
├── ActionValidator.cs             (~450 lines — NEW)
├── CombatRollResolver.cs          (~400 lines — NEW)
├── ReactionTriggerDispatcher.cs   (~500 lines — NEW)
├── CooldownTracker.cs             (~200 lines — NEW)
├── EffectBuilder.cs               (~350 lines — NEW)
├── ResourceCostEngine.cs          (~250 lines — NEW)
├── ActionRegistry.cs              (unchanged — already exists)
├── ActionExecutionResult.cs       (~60 lines — extracted from EffectPipeline.cs)
├── ReactionTriggerEventArgs.cs    (~40 lines — extracted from EffectPipeline.cs)
└── Effects/
    └── (unchanged)
```

### Class Responsibilities

#### 1. `ActionValidator` (NEW, ~450 lines)
**Responsibility:** Answer "can this combatant use this ability right now?"

Extracts from EffectPipeline:
- `CanUseAbility(string actionId, Combatant source)` (public — existing signature preserved)
- `CanUseAbilityWithCost(...)` (internal)
- `IsModifyResourceCapped(...)`
- `GetBlockedByStatusReason(...)`
- `CheckRequirement(...)`
- `ValidateBG3ResourceCost(...)` (moved from internal to public on this class)

**Dependencies (5):**
- `ActionRegistry` — look up action definitions
- `StatusManager` — check silence, blocked actions, status requirements
- `ConcentrationSystem` — check if concentration switch is needed
- `CooldownTracker` — check charge availability
- `IAbilityTestPolicy TestPolicy` — test scenario overrides (defaults to `NoOpAbilityTestPolicy.Instance`)

**Note:** `ValidateBG3ResourceCost` lives here (not in `ResourceCostEngine`) because both `CanUseAbility` and `CanUseAbilityWithCost` call it directly. `BuildBudgetCostOverride` is a static helper that also stays here.

**No Godot dependency.** Fully testable in xUnit.

#### 2. `CombatRollResolver` (NEW, ~400 lines)
**Responsibility:** Compute attack bonuses, save DCs, save bonuses, contest bonuses, critical thresholds, auto-fail conditions.

Extracts from EffectPipeline:
- `GetAttackBonus(Combatant, ActionDefinition)` (public)
- `GetAttackRollBonus(Combatant, ActionDefinition, HashSet<string>)` (internal — used by orchestrator)
- `IsWeaponProficient(Combatant, WeaponDefinition)`
- `GetCriticalThreshold(Combatant, bool)` (static)
- `ShouldApplyMeleeAutoCrit(...)` (static)
- `IsWithinHostileMeleeRange(Combatant)`
- `GetSaveDC(Combatant, ActionDefinition)` (public)
- `GetSaveBonus(Combatant, string)` (public)
- `ComputeSaveDC(Combatant, ActionDefinition, HashSet<string>)` (internal)
- `GetSavingThrowBonus(Combatant, string)`
- `ShouldAutoFailSave(Combatant, string)`
- `GetSpellcastingAbilityModifier(Combatant)`
- `GetContestSkillBonus(Combatant, string)`
- `GetBestContestSkillBonus(Combatant, string)`
- `ParseAbilityType(string)` (static)
- `GetAbilityModifier(Combatant, AbilityType)` (static)
- `ApplyWindowRollSources(...)` (static)
- `MergeParameterSources(...)` (static)

**Dependencies (3):**
- `ICombatContext` — access `CharacterDataRegistry` for spellcasting ability lookup
- `StatusManager` — check auto-fail conditions
- `Func<IEnumerable<Combatant>> GetCombatants` — for melee range checks

**No Godot dependency.** Fully testable in xUnit.

#### 3. `ReactionTriggerDispatcher` (NEW, ~500 lines)
**Responsibility:** Fire reaction triggers at the correct pipeline points and collect modifier results. 

Extracts from EffectPipeline:
- `TryTriggerAbilityCastReactions(...)` (legacy overload — mark deprecated)
- `TryTriggerAbilityCastReactionsWithTags(...)`
- `TryTriggerDamageReactions(...)` (public — used by DealDamageEffect via delegate)
- `TryTriggerAllyDownedReactions(...)`
- `TryTriggerAttackReactions(...)`
- `TryTriggerHitReactions(...)`

Also extracts the 4 trigger events:
- `OnDamageTrigger`, `OnAttackTrigger`, `OnHitTrigger`, `OnAbilityCastTrigger`

**Dependencies (3):**
- `ReactionSystem` — query eligible reactors
- `IReactionResolver` — execute immediate reactions
- `Func<IEnumerable<Combatant>> GetCombatants` — find eligible allies/enemies

**No Godot dependency.** Fully testable in xUnit.

#### 4. `CooldownTracker` (NEW, ~200 lines)
**Responsibility:** Track per-combatant per-action charge counts and turn/round-based recovery.

Extracts from EffectPipeline:
- `ConsumeCooldown(string combatantId, string actionId, ActionDefinition)`
- `ProcessTurnStart(string combatantId)` (public)
- `ProcessRoundEnd()` (public)
- `Reset()` (public)
- `ExportCooldowns()` / `ImportCooldowns(...)` (public — persistence)
- `HasAvailableCharges(string combatantId, string actionId)` — NEW: extracted from `CanUseAbility` inline logic
- `ActionCooldownState` record (internal, moved from bottom of EffectPipeline.cs)

**Dependencies (1):**
- `ActionRegistry` — look up `MaxCharges` and `CooldownType` for newly-encountered actions

**No Godot dependency.** Zero service coupling. Trivially testable.

#### 5. `EffectBuilder` (NEW, ~350 lines)
**Responsibility:** Build runtime effect lists from base definitions + variant overrides + upcast scaling.

Extracts from EffectPipeline:
- `BuildEffectiveCost(ActionDefinition, ActionVariant, int)`
- `BuildEffectiveEffects(List<EffectDefinition>, ActionVariant, int, UpcastScaling)`
- `BuildEffectiveTags(HashSet<string>, ActionVariant)`
- `ApplyVariantToEffect(EffectDefinition, ActionVariant)`
- `ApplyUpcastToEffect(EffectDefinition, int, UpcastScaling)`
- `CloneEffectDefinition(EffectDefinition)`
- `CombineDiceFormulas(string, string)` (public — also used by tests)
- `ParseDiceFormula(string)` (public — also used by tests)

**Dependencies: None.** Pure data transformations. Fully testable, no services needed.

#### 6. `ResourceCostEngine` (NEW, ~250 lines)
**Responsibility:** Validate and consume BG3 `ActionResources` + legacy `ResourcePool` costs.

Extracts from EffectPipeline:
- `ConsumeBG3ResourceCost(Combatant, ActionDefinition, ActionCost)` → `Consume(...)`
- `GetLegacyFallbackCosts(Combatant, ActionCost)` (static helper)

**Note:** `ValidateBG3ResourceCost` stays in `ActionValidator` (where it's called). `BuildBudgetCostOverride` stays in `ActionValidator` as a static helper. Only the consumption side lives here.

**Dependencies: None.** Operates on Combatant's ActionBudget and ResourcePool data. No services.

#### 7. `EffectPipeline` (SLIMMED, ~850 lines)
**Retains:**
- `ExecuteAction(...)` — the two public overloads (slim: delegates roll calculation, validation, resource consumption, reaction triggers, cooldown tracking, and effect building to extracted classes)
- `ExecuteMultiProjectile(...)` — refactored to call `CombatRollResolver` instead of duplicating roll logic inline
- `RegisterEffect(Effect)` / `GetRegisteredEffectTypes()` — effect handler registry
- `PreviewAbility(...)` — damage preview
- `NotifyAbilityExecuted(...)` — `OnAbilityExecuted` event
- `OnAbilityExecuted` event (kept — has 3 production subscribers)
- `OnEffectUnhandled` event (kept — diagnostic)

**Removed from EffectPipeline:**
- `_actions` dict entirely — all action lookups go through `ActionRegistry` (removes the duplicated store)
- `RegisterAction(ActionDefinition)` — **preserved as a method on EffectPipeline** (see note below). Contains ~65 lines of critical merge logic: preserves existing `ResourceCosts`, `SpellLevel`, and `Effects` when re-registering; calls `FindMatchingBg3Action` / `NormalizeBg3IdToGameId` to backfill BG3 spell costs into legacy JSON actions. The two private helpers move with it. Internally calls `ActionRegistry.RegisterAction()` as a side-effect.
- `GetAction(string)` — callers use `ActionRegistry.GetAction()` directly (biggest migration — ~60 call sites, trivial find-and-replace)

> **CRITICAL:** The `RegisterAction` merge logic must NOT be simplified to a thin wrapper. Legacy JSON actions (e.g., `hunters_mark`) rely on BG3 cost inheritance via `FindMatchingBg3Action`. Dropping this breaks action economy for all hand-authored actions. Future cleanup: move the merge logic into `ActionRegistry` as a `RegisterWithMerge(ActionDefinition, ActionRegistry bg3Source)` overload.
- `_cooldowns` dict — owned by `CooldownTracker`
- All 15 nullable property-injected services — replaced by 7 concrete constructor dependencies (the extracted subsystems)

**New constructor signature:**
```csharp
public EffectPipeline(
    // Extracted subsystems
    ActionRegistry actionRegistry,
    ActionValidator validator,
    CombatRollResolver rollResolver,
    ReactionTriggerDispatcher reactionDispatcher,
    CooldownTracker cooldownTracker,
    EffectBuilder effectBuilder,
    ResourceCostEngine resourceCostEngine,
    // Core services used directly by ExecuteAction
    RulesEngine rules,
    StatusManager statuses,
    Random rng,
    // Pass-through to EffectContext (consumed by Effect subclasses)
    ICombatContext combatContext,
    TurnQueueService turnQueue,
    SurfaceManager surfaces,
    HeightService heights,
    LOSService los,             // also used directly in ExecuteAction/ExecuteMultiProjectile
    ForcedMovementService forcedMovement,
    OnHitTriggerService onHitTriggerService,
    DataRegistry dataRegistry)
```

**Total: 18 parameters.** The 7 EffectContext pass-throughs are stored as `readonly` fields. `LOSService` is special — it's used directly in the orchestrator for cover checks (lines 859 and 1778) AND passed through to EffectContext.

**EffectContext change:** `EffectContext.Pipeline` (currently used for sub-spell execution by `UseSpellEffect` and `CreateExplosionEffect`) is preserved; those effects call `Pipeline.ExecuteAction(...)` which is still on EffectPipeline.

### Supporting Type Extractions

Two types currently defined at the top of `EffectPipeline.cs` move to their own files:

- `ReactionTriggerEventArgs` (L20–52) → `Combat/Actions/ReactionTriggerEventArgs.cs`
- `ActionExecutionResult` (L56–106) → `Combat/Actions/ActionExecutionResult.cs`
- `ActionCooldownState` (L3444–3450) → stays internal in `CooldownTracker.cs`

---

## Migration Strategy

### Principle: No Big-Bang Rewrite

Each phase extracts one class, wires it in, and passes all build gates before the next extraction begins. At every phase boundary the game works identically to before.

### Phase 0: Extract Supporting Types (30 min)
**Goal:** Move `ReactionTriggerEventArgs` and `ActionExecutionResult` to their own files. Zero behavior change.

**Steps:**
1. Create `Combat/Actions/ReactionTriggerEventArgs.cs` — move the class verbatim, keep namespace `QDND.Combat.Actions`.
2. Create `Combat/Actions/ActionExecutionResult.cs` — move the class verbatim.
3. Remove both from `EffectPipeline.cs`.
4. Build gate: `ci-build.sh` + `ci-test.sh`.

**Risk:** None. Pure file move.

---

### Phase 1: Extract `CooldownTracker` (1 hr)
**Goal:** Lowest coupling, easiest extraction. Cooldown management has zero service dependencies and a clean interface.

**Steps:**
1. Create `Combat/Actions/CooldownTracker.cs`:
   - Move `_cooldowns` dict, `ActionCooldownState` record, all 6 cooldown methods.
   - Constructor takes `ActionRegistry` (for `MaxCharges` lookup on first encounter).
   - Add `HasAvailableCharges(string combatantId, string actionId) → bool` extracted from inline logic in `CanUseAbility`.
2. In `EffectPipeline`:
   - Add `CooldownTracker Cooldowns { get; set; }` property (initially property-injected, same as today's pattern).
   - Replace all `_cooldowns` references with `Cooldowns.XXX()` calls.
   - Remove `_cooldowns` field, `ActionCooldownState` record.
3. In `CombatArena.RegisterServices()`:
   - Create `CooldownTracker` instance, wire to EffectPipeline.
4. In `TurnLifecycleService`:
   - Change `_effectPipeline.ProcessTurnStart(...)` → `_cooldownTracker.ProcessTurnStart(...)` (or keep the facade — see Migration Facade below).
5. Build gate: `ci-build.sh` + `ci-test.sh` + `ci-godot-log-check.sh`.

**Caller impact:** `ProcessTurnStart` (1 caller), `ProcessRoundEnd` (1 caller), `Export/ImportCooldowns` (1 caller each). All in known locations.

---

### Phase 2: Extract `EffectBuilder` (1 hr)
**Goal:** Pure data-transform extraction. No service dependencies, no events.

**Steps:**
1. Create `Combat/Actions/EffectBuilder.cs`:
   - Move all 8 methods from Group 5: `BuildEffectiveCost`, `BuildEffectiveEffects`, `BuildEffectiveTags`, `ApplyVariantToEffect`, `ApplyUpcastToEffect`, `CloneEffectDefinition`, `CombineDiceFormulas`, `ParseDiceFormula`.
   - No constructor dependencies. All methods can be static or instance — prefer instance for consistency.
2. In `EffectPipeline`:
   - Add `EffectBuilder Builder { get; set; }` property.
   - Replace all calls to moved methods with `Builder.XXX()`.
3. Build gate.

**Caller impact:** Zero external callers — all methods are private, called only from `ExecuteAction`. Tests that reference `CombineDiceFormulas` need updating to call on `EffectBuilder` instead.

---

### Phase 3: Extract `ResourceCostEngine` (1 hr)
**Goal:** Move resource validation and consumption logic out. No service dependencies.

**Steps:**
1. Create `Combat/Actions/ResourceCostEngine.cs`:
   - Move `ValidateBG3ResourceCost`, `ConsumeBG3ResourceCost`, `BuildBudgetCostOverride`, `GetLegacyFallbackCosts`.
   - Rename to cleaner API: `CanAfford(...)`, `Consume(...)`.
   - No constructor dependencies — operates on Combatant's `ActionBudget` and `ResourcePool`.
2. In `EffectPipeline`:
   - Add `ResourceCostEngine Resources { get; set; }` property.
   - Replace calls.
3. Build gate.

**Caller impact:** Zero external callers — both methods were `internal` and called only from `ExecuteAction`.

---

### Phase 4: Extract `CombatRollResolver` (2 hr)
**Goal:** Extract all attack roll, save throw, and contest calculation logic. This is the most cross-referenced extraction.

**Steps:**
1. Create `Combat/Actions/CombatRollResolver.cs`:
   - Move all 18 methods from Groups 6 + 7 + 8.
   - Constructor takes: `ICombatContext` (for CharacterDataRegistry), `StatusManager`, `Func<IEnumerable<Combatant>>`.
2. In `EffectPipeline`:
   - Add `CombatRollResolver Rolls { get; set; }` property.
   - In `ExecuteAction`: replace inline `GetAttackRollBonus(...)` → `Rolls.GetAttackRollBonus(...)`.
   - In `ExecuteMultiProjectile`: replace duplicated roll logic → `Rolls.GetAttackRollBonus(...)`.
   - Preserve public wrappers `GetAttackBonus`, `GetSaveDC`, `GetSaveBonus` on EffectPipeline as thin pass-throughs (facade pattern) to avoid breaking HudController.
3. In `HudController`:
   - **No changes needed** if facade preserved. Later (Phase 7) these callers can migrate directly to `CombatRollResolver`.
4. Build gate.

**Caller impact:** `GetAttackBonus` (1 external caller: HudController), `GetSaveDC` (2 callers: HudController, SummonCombatantEffect), `GetSaveBonus` (1 caller: HudController). All preserved via facade.

**Key win:** `ExecuteMultiProjectile` currently duplicates ~100 lines of roll logic from `ExecuteAction`. After extraction, both call `Rolls.GetAttackRollBonus()` — eliminating the duplication.

---

### Phase 5: Extract `ReactionTriggerDispatcher` (2 hr)
**Goal:** Move all reaction trigger logic and the 4 trigger events.

**Steps:**
1. Create `Combat/Actions/ReactionTriggerDispatcher.cs`:
   - Move all 7 methods from Group 9.
   - Move 4 events: `OnDamageTrigger`, `OnAttackTrigger`, `OnHitTrigger`, `OnAbilityCastTrigger`.
   - Constructor takes: `ReactionSystem`, `IReactionResolver`, `Func<IEnumerable<Combatant>>`.
2. In `EffectPipeline`:
   - Add `ReactionTriggerDispatcher ReactionTriggers { get; set; }`.
   - In `ExecuteAction`: replace `TryTriggerAbilityCastReactionsWithTags(...)` → `ReactionTriggers.TryTriggerAbilityCastReactionsWithTags(...)`.
   - Do same for attack/hit triggers.
   - The `OnBeforeDamage` delegate passed into `EffectContext`: change from `this.TryTriggerDamageReactions` → `ReactionTriggers.TryTriggerDamageReactions`.
3. In `DealDamageEffect`:
   - `context.Pipeline.TryTriggerAllyDownedReactions(...)` (line 762) — EffectPipeline keeps a thin facade that delegates to `ReactionTriggers.TryTriggerAllyDownedReactions(...)`. No change needed at the call site.
4. **Event facades during migration:** EffectPipeline retains facade event properties for `OnDamageTrigger`, `OnAttackTrigger`, `OnHitTrigger`, `OnAbilityCastTrigger` that forward to `ReactionTriggers`. This keeps all test subscribers working without migration during Phase 5. The facades are removed in Phase 7d along with the other facades.
5. Build gate.

**Caller impact:** `TryTriggerDamageReactions` (passed as delegate via EffectContext.OnBeforeDamage — 1 wiring site in ExecuteAction), `TryTriggerAllyDownedReactions` (1 external caller: DealDamageEffect via `context.Pipeline`). The 4 trigger events have 0 production subscribers but have test subscribers in `EffectPipelineReactionTests.cs` (12 subscriptions) and `MultiProjectileTests.cs` (1 subscription) — handled by facade events.

---

### Phase 6: Extract `ActionValidator` (2 hr)
**Goal:** Move all pre-execution validation. This is the most external-caller-heavy extraction.

**Steps:**
1. Create `Combat/Actions/ActionValidator.cs`:
   - Move all 6 methods from Group 2.
   - Constructor takes: `ActionRegistry`, `StatusManager`, `ConcentrationSystem`, `CooldownTracker`.
2. In `EffectPipeline`:
   - Add `ActionValidator Validator { get; set; }`.
   - In `ExecuteAction`: replace `CanUseAbilityWithCost(...)` → `Validator.CanUseAbilityWithCost(...)`.
   - Preserve public `CanUseAbility(string, Combatant)` as a facade pass-through.
3. Migrate external callers (8 sites):
   - `AIDecisionPipeline`: already accesses EffectPipeline — can continue via facade, or switch to `ActionValidator` directly.
   - `CombatArena`, `ActionBarService`, `SelectionService`: same.
   - Leave facade in place for this phase; callers migrate in Phase 7.
4. Build gate.

---

### Phase 7: Remove Action Store Duplication & Facades (3 hr)
**Goal:** The big cleanup. Remove `_actions` dict from EffectPipeline, migrate `GetAction` callers to `ActionRegistry`, convert EffectPipeline from property injection to constructor injection.

**Steps:**

#### 7a. Remove `_actions` dict
1. Verify that every action registered via `EffectPipeline.RegisterAction()` is also registered in `ActionRegistry` (researcher confirmed: `RegisterAction` writes to both).
2. Replace `RegisterAction` on EffectPipeline: either remove entirely or make it a one-liner that calls `ActionRegistry.RegisterAction()`.
3. Remove `_actions` dict. All `GetAction` calls inside EffectPipeline now go to `ActionRegistry.GetAction()`.

#### 7b. Migrate `GetAction` external callers (~60+ sites)
This is the largest mechanical change. It's a safe find-and-replace:
```
// Before (in AIDecisionPipeline, ActionExecutionService, etc.)
_effectPipeline.GetAction(actionId)

// After
_actionRegistry.GetAction(actionId)
```

Most callers already have an `ActionRegistry` reference or can receive one. The few that don't (e.g., `CombatInputHandler`) can get one via `ICombatContext.GetService<ActionRegistry>()` or constructor injection.

**Breakdown by caller file (effort estimate):**
| File | `GetAction` calls | Already has ActionRegistry? | Migration |
|---|---|---|---|
| `AIDecisionPipeline.cs` | ~20 | No → add to LateInitialize | Medium |
| `ActionExecutionService.cs` | ~11 | Yes (via constructor) | Trivial |
| `AIScorer.cs` | ~9 | No → add constructor param | Medium |
| `CombatInputHandler.cs` | ~3 | No → add via constructor | Trivial |
| `SelectionService.cs` | ~3 | No → add via constructor | Trivial |
| `ReactionCoordinator.cs` | ~3 | No → add constructor param | Trivial |
| `CustomFightLogger.cs` | ~3 | No → add via GetService | Trivial |
| AutoBattler tools | ~5 | Varies | Trivial |
| Tests | ~15 | Direct construction | Trivial |

#### 7c. Convert to constructor injection
Replace EffectPipeline's 15 nullable property-injected services with the new constructor:

```csharp
public EffectPipeline(
    ActionRegistry actionRegistry,
    ActionValidator validator,
    CombatRollResolver rollResolver,
    ReactionTriggerDispatcher reactionDispatcher,
    CooldownTracker cooldownTracker,
    EffectBuilder effectBuilder,
    ResourceCostEngine resourceCostEngine,
    RulesEngine rules,
    StatusManager statuses,
    Random rng)
```

Services that were passed through to `EffectContext` (`TurnQueue`, `CombatContext`, `Surfaces`, `ForcedMovement`, `OnHitTriggerService`, `Heights`, `DataRegistry`) become constructor parameters too, stored as `readonly` fields.

#### 7d. Remove facades
Once all external callers are migrated, remove the facade pass-throughs:
- `GetAction(string)` — callers use `ActionRegistry` directly
- `CanUseAbility(...)` — callers that need it use `ActionValidator` directly
- `GetAttackBonus/GetSaveDC/GetSaveBonus` — HudController uses `CombatRollResolver` directly
- `ProcessTurnStart/ProcessRoundEnd` — TurnLifecycleService uses `CooldownTracker` directly
- `Export/ImportCooldowns` — CombatSaveService uses `CooldownTracker` directly

**Build gate:** `ci-build.sh` + `ci-test.sh` + `ci-godot-log-check.sh` + `run_autobattle.sh --full-fidelity --seed 42`.

---

### Phase 8: Collapse MultiProjectile Duplication (1 hr)
**Goal:** Now that `CombatRollResolver` exists, refactor `ExecuteMultiProjectile` to use it instead of duplicating roll logic.

**Steps:**
1. In `ExecuteMultiProjectile`, replace inline attack roll calculation (~100 lines) with calls to `Rolls.GetAttackRollBonus(...)`.
2. Replace inline reaction trigger calls with `ReactionTriggers.TryTriggerAttackReactions(...)` / `TryTriggerHitReactions(...)`.
3. This should reduce `ExecuteMultiProjectile` from ~285 lines to ~150 lines.
4. Build gate + autobattle stress test (multi-projectile spells like Eldritch Blast, Scorching Ray, Magic Missile).

---

## Test Strategy

### New Test Files (one per extracted class)

| New Test File | Tests | Pattern |
|---|---|---|
| `Tests/Unit/CooldownTrackerTests.cs` | Charge consumption, turn/round recovery, export/import round-trip | Direct construction, no mocks |
| `Tests/Unit/EffectBuilderTests.cs` | Variant application, upcast scaling, `CombineDiceFormulas` edge cases | Pure functions, no services |
| `Tests/Unit/ResourceCostEngineTests.cs` | SpellSlot validation, ActionPoint/BonusAction costs, over-spend rejection | Combatant stubs with ResourcePool |
| `Tests/Unit/CombatRollResolverTests.cs` | Attack bonus, save DC, weapon proficiency, auto-fail saves, critical threshold | Real RulesEngine + StatusManager |
| `Tests/Unit/ReactionTriggerDispatcherTests.cs` | Eligible reactor filtering, damage modifier accumulation, reaction execution | Mock ReactionSystem |
| `Tests/Unit/ActionValidatorTests.cs` | Silence blocking, requirement checks, concentration validation, cooldown blocking | Real StatusManager + CooldownTracker |

### Existing Test Migration

| Existing Test File | Change Needed |
|---|---|
| `Tests/Integration/UpcastingTests.cs` | `CreatePipeline()` adds `EffectBuilder` wiring |
| `Tests/Integration/DynamicFormulaResolutionTests.cs` | Same |
| `Tests/Integration/ExtraAttackIntegrationTest.cs` | Same |
| `Tests/Integration/ShoveActionTests.cs` | Same |
| `Tests/Unit/ReactionParityPlanTests.cs` | Switch to `ReactionTriggerDispatcher` construction |
| `Tests/Unit/EffectPipelineReactionTests.cs` | 12 event subscriptions → use facade events (Phase 5), migrate to ReactionTriggerDispatcher (Phase 7d) |
| `Tests/Unit/MultiProjectileTests.cs` | 1 `OnAttackTrigger` subscription → use facade event (Phase 5), migrate (Phase 7d) |
| `Tests/Unit/OnHitTriggerServiceTests.cs` | `CreatePipeline()` adds new wiring |
| `Tests/Integration/CombatArenaSystemsWiringTests.cs` | Verify new subsystem wiring |
| `Tests/Simulation/ActionComprehensiveTests.cs` | Update `TestCombatHarness.Effects` construction |
| `Tests/Simulation/MultiRoundStabilityTests.cs` | Same |
| `Tests/Helpers/ParityDataValidator.cs` | `GetRegisteredEffectTypes()` stays on EffectPipeline — no change |

---

## Verification Checklist (Per Phase)

```
□ ci-build.sh passes
□ ci-test.sh passes (dotnet test)
□ ci-godot-log-check.sh passes
□ Autobattle seed 42 completes without TIMEOUT_FREEZE or INFINITE_LOOP
□ No new GD.PushError / ERROR lines in autobattle log
□ Diff combat_log.jsonl against pre-refactor baseline — outcomes should be identical
  (same seed = same random rolls = same combat result, unless the refactor changed execution order)
```

---

## Phase Summary

| Phase | Extracts | New File | Lines Moved | Callers Affected | Est. Effort |
|---|---|---|---|---|---|
| 0 | Supporting types | 2 files | ~110 | 0 | 30 min |
| 1 | CooldownTracker | 1 file | ~200 | 3 (TurnLifecycle, SaveService) | 1 hr |
| 2 | EffectBuilder | 1 file | ~350 | 0 (all internal) | 1 hr |
| 3 | ResourceCostEngine | 1 file | ~250 | 0 (all internal) | 1 hr |
| 4 | CombatRollResolver | 1 file | ~400 | 4 (HudController via facade) | 2 hr |
| 5 | ReactionTriggerDispatcher | 1 file | ~500 | 3 (EffectContext delegate, CombatArena) | 2 hr |
| 6 | ActionValidator | 1 file | ~450 | 8 (via facade) | 2 hr |
| 7 | Remove duplication + facades | 0 new files | 0 | ~60+ GetAction migrations | 3 hr |
| 8 | Collapse MultiProjectile | 0 new files | -135 net | 0 | 1 hr |
| **Total** | **7 classes** | **8 new files** | **~2,600 extracted** | — | — |

**Final state:** EffectPipeline drops from 3,451 lines → ~850 lines, with 10 constructor parameters (all concrete, no nullable property injection). Each extracted class is independently testable with 1–4 dependencies.

---

## What This Does NOT Change

- **ActionRegistry.cs** — untouched. It already exists as the canonical action store.
- **Effect classes** in `Combat/Actions/Effects/` — untouched. They receive `EffectContext` which still works.
- **EffectContext** — `Pipeline` property remains, pointing to the slimmed `EffectPipeline`.
- **CombatArena composition root** — still creates all services; the wiring just becomes cleaner (7 subsystem constructions + 1 EffectPipeline construction instead of 15 property assignments).
- **Public API semantics** — Every caller gets the exact same behavior. Facades ensure backward compatibility during migration.
