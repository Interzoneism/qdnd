# CombatArena Decomposition Plan

## Summary
Refactor `CombatArena` in three bounded steps, in this order: extract service wiring into a plain C# `CombatArenaComposer`, replace the arena-owned `_combatants` list with a single `ICombatantRegistry` authority, and introduce a narrow `ICombatController` surface for non-scene consumers. Keep `CombatArena` as the scene/lifecycle host and visual manager for this pass so behavior stays stable while the god-object responsibilities are split.

## Implementation Changes
- Add `ICombatantRegistry` + `CombatantRegistry` as the sole combatant owner.
  - API: `Get(string id)`, `GetAll()`, `Add(Combatant)`, `Remove(string id)`, `ReplaceAll(IEnumerable<Combatant>)`, `Clear()`.
  - Events: `CombatantAdded`, `CombatantRemoved`, `CombatantsReplaced` or `Cleared`.
  - Store both ordered list and ID lookup internally so it replaces `CombatArena._combatants` and `CombatContext`’s private combatant dictionary.
- Change `CombatContext` so its combatant methods delegate to `ICombatantRegistry`.
  - Keep the service-locator portion unchanged in this refactor.
  - Preserve `OnCombatantRegistered` as a compatibility event, but source it from the registry’s add event.
  - Update `HeadlessCombatContext` and any test doubles to use the same registry-backed contract.
- Replace shared `List<Combatant>` injection with registry-backed access.
  - `ActionExecutionService`, `ReactionCoordinator`, `CombatMovementCoordinator` move from `List<Combatant>` constructor args to `ICombatantRegistry`.
  - Existing callback-style dependencies that only need reads become `registry.GetAll()` or `Func<IReadOnlyList<Combatant>>` backed by the registry.
  - `ScenarioBootService.SyncFromBootService()` stops doing `_combatants.Clear(); AddRange(...)` and instead calls `registry.ReplaceAll(...)`.
- Add `CombatArenaComposer` as a plain C# composition root.
  - Input: `CombatArenaCompositionArgs` containing config flags, node refs, logging delegates, arena-owned callbacks, and already-created scene objects like `_cameraService`, `_movementPreview`, `_rangeIndicator`, `_vfxManager`.
  - Output: `CombatArenaComposition` containing the created services/models (`CombatContext`, registries, pipelines, coordinators, UI models, boot service, registry, etc.).
  - Move the contents of `RegisterServices()` into the composer; `CombatArena._Ready()` becomes “create scene nodes -> initialize context -> call composer -> load scenario -> start combat”.
  - Add a disposal/teardown handle to the composition so service event subscriptions can be unwired during `ReloadWithScenario` and `_ExitTree`.
- Introduce `ICombatController` and migrate non-scene consumers to it.
  - Initial interface surface: `Context`, `ActionBarModel`, `TurnTrackerModel`, `ResourceBarModel`, `ActiveCombatantId`, `SelectedCombatantId`, `IsPlayerTurn`, `IsAutoBattleMode`, `GetCombatants()`, `GetActionById()`, `GetActionsForCombatant()`, `SelectAction(...)`, `EndCurrentTurn()`, `ReorderActionBarSlots(...)`, `GetVisual(...)`, `CombatantHoverChanged`, `OnAIAbilityUsed`.
  - `CombatArena` implements `ICombatController`.
  - Migrate `HudController` and `CharacterInventoryScreen` to depend on `ICombatController`.
  - Leave scene-local classes on concrete `CombatArena` for now: `CombatInputHandler`, `ScenarioSelector`, `DebugPanel`, `CombatantVisual`, `CustomFightLogger`, `ActionEditorArena`.
- Keep visual responsibilities in `CombatArena` for this pass.
  - No separate visual manager extraction yet.
  - `ScenarioBootService` may keep arena-specific visual spawning callbacks, but they should flow through composer-provided delegates instead of holding the full arena where practical.

## Public API / Type Additions
- New: `ICombatantRegistry`, `CombatantRegistry`.
- New: `ICombatController`.
- New: `CombatArenaComposer`, `CombatArenaCompositionArgs`, `CombatArenaComposition`.
- Changed: `CombatContext` combatant storage implementation becomes registry-backed.
- Changed constructor signatures:
  - `ActionExecutionService(..., ICombatantRegistry registry, ...)`
  - `ReactionCoordinator(..., ICombatantRegistry registry, ...)`
  - `CombatMovementCoordinator(..., ICombatantRegistry registry, ...)`
- `CombatArena` remains the scene script and now implements `ICombatController`.

## Test Plan
- Unit tests for `CombatantRegistry`:
  - add/remove/replace/clear semantics
  - stable ID lookup + ordered enumeration
  - event firing order for replace/add/remove
- Unit tests for `CombatContext`:
  - combatant methods delegate to registry
  - compatibility event still fires on add
- Composer tests:
  - `CombatArenaComposer` returns all core services/models
  - critical cross-wiring is preserved (`EffectPipeline`, reactions, targeting, movement, presentation)
  - teardown unsubscribes arena-facing events
- Integration tests:
  - existing arena loading tests still pass with composer bootstrap
  - reload path swaps registry contents without exposing a transient shared empty list to services
  - `HudController` works through `ICombatController` only
- End-to-end gates before done:
  - `scripts/ci-build.sh`
  - `scripts/ci-test.sh`
  - `scripts/ci-godot-log-check.sh`

## Assumptions and Defaults
- Use an incremental rollout with compatibility shims; this is not a “big bang” rewrite.
- Scope for this refactor is composer + controller facade + combatant registry ownership. Full visual-manager extraction is deferred.
- Only broad/non-scene consumers move to `ICombatController` now; arena-internal child nodes can remain concrete until a later cleanup pass.
- `TurnQueueService` keeps its current queue/order responsibilities; registry becomes the sole general combatant source of truth, not the turn-order owner.

## Implemented Notes
- `CombatArena` now composes services through `CombatArenaComposer`, applies the returned `CombatArenaComposition`, and disposes composer-managed subscriptions on `_ExitTree()`.
- `ICombatantRegistry` / `CombatantRegistry` are the sole combatant authority for `CombatArena`, `CombatContext`, `ScenarioBootService`, and the execution/reaction/movement coordinators.
- `HudController` and `CharacterInventoryScreen` now depend on `ICombatController` instead of the concrete `CombatArena` scene type.
- `ScenarioBootService` replaces the registry contents in one step and then re-registers LOS / forced-movement occupants from that registry-backed source.
- `UnsummonCombatantEffect` now removes summons from the shared combatant registry after they leave the turn queue.
- `Tests/Unit/CombatantRegistryTests.cs` covers registry ordering, lookup, replacement, and clear semantics.
- Direct xUnit coverage for `CombatContext` was intentionally not kept because constructing the Godot `CombatContext : Node` in `dotnet test` can crash `testhost` in this repo; that contract is instead validated through registry tests plus the required Godot smoke gate.
