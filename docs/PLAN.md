# Plan: BG3 Combat Parity Program

**Objective**: Evolve the combat system from a playable approximation to a robust, verifiable core that mirrors Baldur's Gate 3 mechanics. This document outlines the staged, executable plan to achieve that goal, prioritizing stability and architectural soundness before expanding content coverage.

## 1. Guiding Principles & Definitions

- **BG3 as Ground Truth**: The `bg3.wiki` is the primary source for behavior. D&D 5e Player's Handbook (PHB) is the fallback where the game is silent.
- **Determinism is Mandatory**: All combat outcomes must be reproducible. Gameplay logic must not use ad-hoc/direct RNG.
- **Fail Fast, Fail Loud**: Parity gaps, data errors, and unsupported mechanics should be explicit CI failures, not silent runtime warnings.
- **Clarity Over Brittleness**: This plan is a living document. Prefer updating it to reflect new discoveries over forcing adherence to an outdated view.

### Key BG3/D&D 5e Concepts

- **Action**: Used for main combat maneuvers (e.g., attacking, casting most spells). A character gets one per turn.
- **Bonus Action**: Used for secondary tasks (e.g., off-hand attacks, jumping, drinking potions). A character gets one per turn.
- **Reaction**: A special action taken in response to a specific trigger, even outside a character's turn (e.g., `Attack of Opportunity`). A character gets one per round.
- **Concentration**: A required state for maintaining many spells with ongoing effects. It can be broken by taking damage, being incapacitated, or casting another concentration spell.

## 2. The Plan: Workstreams

### Workstream 1: Foundational Reliability & Parity Guardrails

*Goal: Ensure a stable development environment where failures are deterministic and data inconsistencies are caught automatically.*

1.  **Verify CI Stability**: Confirm that `scripts/ci-test.sh` runs without intermittent host crashes or process aborts. If instability persists, this is the highest priority fix.
2.  **Implement Parity Validation CI Gate**: Create a new, mandatory CI stage (`parity-validate`) that fails the build for data-to-runtime mismatches.
    - **Fail on Duplicate IDs**: Reject any duplicate action, status, or other registered data IDs.
    - **Fail on Missing Links**: Detect and fail when a feature (e.g., from a class or race) grants an action ID that does not exist.
    - **Fail on Unregistered Effect Types**: Ensure every `Effect` type in game data has a corresponding, registered handler in the C# runtime.
    - **Fail on Schema Mismatches**: Validate all loaded JSON/data files against their expected C# class schemas.
3.  **Exit Criteria**:
    - `scripts/ci-test.sh` produces a clean, deterministic pass or fail result.
    - The `parity-validate` gate provides actionable error messages, pointing to the specific files, IDs, or fields causing the failure.

### Workstream 2: Declarative Rules Core (Passives & Triggers)

*Goal: Replace hardcoded, imperative logic with a declarative, data-driven rules engine based on canonical event triggers.*

1.  **Define Canonical Trigger Windows**: Establish a non-exhaustive, extensible set of event windows for the rules engine to hook into. Minimum set:
    - `BeforeAttackRoll`, `AfterAttackRoll`
    - `BeforeDamage`, `AfterDamage`
    - `BeforeSavingThrow`, `AfterSavingThrow`
    - `OnTurnStart`, `OnTurnEnd`
    - `OnMove`, `OnLeaveThreateningArea`, `OnEnterSurface`
    - `OnConcentrationCheck` (triggered by damage, prone, etc.), `OnConcentrationBroken`
    - `OnDeclareAction`, `OnActionComplete`
2.  **Develop Passive Registration Pipeline**: Create a system that sources passive abilities from character data (class, race, feats, items, statuses) and registers them with the rules engine to listen for the appropriate triggers.
3.  **Migrate Existing Passives**: Refactor all hardcoded passive behaviors (e.g., `StatusSystem` logic) into data-backed passives that use the new trigger system.
4.  **Exit Criteria**:
    - All major hardcoded passive logic is removed from the core combat loop.
    - A representative set of passives (e.g., `Fighting Style: Dueling`, `Savage Attacker`) are implemented and function correctly through the new system.

### Workstream 3: Interrupt-Driven Reaction System

*Goal: Integrate a formal, interrupt-based reaction system that respects BG3's trigger windows and player/AI choice.*

1.  **Implement Interruptible Action Pipeline**: Modify the core action resolution sequence to be pausable and resumable by a new `ReactionResolver` service.
2.  **Develop Reaction Stack & Priority Logic**: When a trigger fires that one or more characters can react to (e.g., `Attack of Opportunity`), the `ReactionResolver` should:
    - Identify all legal reactions.
    - Order them based on a deterministic priority system.
    - Present the choice to the appropriate controller (human UI or AI).
    - Execute the chosen reaction, potentially creating a nested chain (e.g., `Counterspell` vs. `Counterspell`).
3.  **Add Policy Hooks for Reaction Choice**: Expose configuration for players to set reactions to "always ask," "always use," or "never use." The AI will need its own policy hooks for decision-making.
4.  **Exit Criteria**:
    - The system correctly handles canonical reaction chains, including:
        - Movement leaving threat -> `Attack of Opportunity`
        - Spell cast -> `Counterspell`
        - Ally attacked -> `Hellish Rebuke` / `Shield`
    - Reactions can only be executed within their valid trigger windows.

#### Workstream 3 Implementation Notes

- Added `Combat/Reactions/IReactionResolver.cs` and `Combat/Reactions/ReactionResolver.cs` as the central interrupt-resolution service.
- Wired `EffectPipeline` to use the resolver for `SpellCastNearby`, `YouTakeDamage`, and `AllyTakesDamage` trigger windows.
- Wired `MovementService` to route opportunity attacks through the resolver with deterministic priority ordering.
- Added player policy hooks (`AlwaysAsk`, `AlwaysUse`, `NeverUse`) and AI decision hooks at resolver level.
- Routed reaction execution through `ReactionSystem.OnReactionUsed` so reaction actions (`counterspell`, `shield`, opportunity attacks) execute through the real action pipeline with trigger context propagation.

### Workstream 4: Unified Concentration & Sustained Effects

*Goal: Centralize the entire lifecycle of concentration and other sustained effects to prevent bugs and ensure consistent behavior.*

1.  **Create a Standard Concentration Contract**: Define a single, reusable data structure and action component for all spells/actions that require concentration.
2.  **Route All Logic Through Shared Triggers**: Ensure all concentration events are managed by the Workstream 2 rules engine.
    - A `OnConcentrationCheck` must be triggered when a concentrating creature:
        - Takes damage (DC is `10` or `half damage taken`, whichever is higher).
        - Falls `Prone`.
        - Is subject to specific incapacitating effects.
    - Concentration ends *immediately* if the creature:
        - Casts another concentration spell.
        - Is `Incapacitated` or `Killed`.
        - Manually ends it (a free action).
3.  **Exit Criteria**:
    - All concentration spells use the new, unified contract.
    - Breaking concentration, whether by failing a save or by rule, correctly and immediately removes all associated magical effects. No orphaned effects remain.

#### Workstream 4 Implementation Notes

- Expanded `ConcentrationInfo` into a unified concentration contract with tracked linked effects (`LinkedEffects`) for precise sustained-effect cleanup.
- Added a new `ConcentrationEffectSnapshot` payload and persisted linked concentration effects via `ConcentrationSnapshot.LinkedEffects`.
- Routed concentration startup through the contract API (`StartConcentration(ConcentrationInfo)`) from `EffectPipeline`.
- Extended `ConcentrationSystem` event handling to include `StatusApplied`:
  - `prone` now triggers a concentration save via `RuleWindow.OnConcentrationCheck` and normal save windows.
  - incapacitating statuses trigger `RuleWindow.OnConcentrationCheck` context and then immediate concentration break.
- Added `EndConcentration` helper for explicit manual concentration termination semantics.
- Concentration break now prioritizes exact linked-instance cleanup and falls back to source/status sweep for backward compatibility with older save states.

### Workstream 5: Tactical Fidelity & Environment

*Goal: Improve the accuracy of environmental and positional mechanics to better match BG3's tactical depth.*

1.  **Refine Movement & Verticality**:
    - Implement a `Jump` legality model that accounts for character clearance, vertical distance, and surface stability. Standard character movement is ~9 meters/turn.
    - Make action legality (e.g., for targeting) aware of height differences and line of sight.
2.  **Expand Surface & Cover System**:
    - Formalize the rules for how surfaces are created, propagated (e.g., water + electricity), and removed.
    - Improve Line of Sight (LOS) checks to correctly model partial and full cover from obstructions.
3.  **Exit Criteria**:
    - Key tactical scenarios pass in automated tests:
        - A character on a cliff has an advantage against a target below.
        - An archer correctly identifies that an enemy behind a pillar has full cover.
        - Casting a water spell then a lightning spell creates an electrified water surface.
    - `Jump` is a distinct, reliable movement option, not a fallback for generic pathfinding.

## 3. Public API & Data Schema Changes

This program will necessitate significant, but controlled, changes.

1.  **New Core Types**:
    - `Combat/Rules/RuleWindow.cs` (enum for triggers)
    - `Combat/Rules/RuleEventContext.cs` (payload for events)
    - `Combat/Rules/IRuleProvider.cs` (interface for passives)
    - `Combat/Reactions/IReactionResolver.cs`
2.  **Modified Interfaces**:
    - `DataRegistry` must enforce strict validation by default.
    - `StatusSystem`, `ReactionSystem`, etc., must be refactored to consume the new `IRuleProvider` interface and trigger windows, removing their internal hardcoded logic.
3.  **Data Schema Additions**:
    - **Passives**: New metadata to specify trigger window, conditions, and priority.
    - **Reactions**: New metadata for legal windows, resource costs, and AI policy hints.

## 4. Test & Validation Strategy

1.  **Unit Tests**: Focus on the rules engine itself—trigger ordering, reaction priority, concentration DC calculation, etc.
2.  **Integration Tests (Headless)**: Use `run_autobattle.sh` with specific scenarios to validate mechanics end-to-end.
    - `ff_reaction_hellish_rebuke.json`: Test simple damage-triggered reaction.
    - `ff_concentration_break.json`: Test concentration save mechanics.
    - `ff_jump_height_advantage.json`: Test verticality rules.
3.  **Full-Fidelity Tests**: Use `run_autobattle.sh --full-fidelity` to catch bugs that only appear with the full UI and animation systems active.
4.  **CI Acceptance Criteria**: A merge is only accepted if `ci-build.sh`, `ci-test.sh`, and the new `parity-validate` stage all pass.
