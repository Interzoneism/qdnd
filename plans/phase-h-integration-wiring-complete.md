# Phase H Complete: Integration Wiring and System Connections

**Completed:** 2026-02-02
**Status:** All 6 phases implemented

## Summary

Phase H connected existing but previously disconnected combat systems. The infrastructure for reactions, surfaces, action economy, and status effects now works together in the main game loop.

## Phases Completed

### Phase 1: Action Budget Reset Integration ✅
- Wired `ActionBudget.ResetForTurn()` into turn lifecycle
- On turn start, current combatant's budget is automatically reset
- On round start, ALL combatants' reaction budget is reset
- Added `_previousRound` tracking to detect round changes

**Files Modified:**
- Combat/Arena/CombatArena.cs (turn/round reset logic)

**Tests Added:**
- Tests/Unit/TurnLifecycleBudgetTests.cs (6 tests)

### Phase 2: Surface Integration with Movement ✅
- Wired SurfaceManager into MovementService
- Moving into fire surfaces now automatically deals damage
- Moving out of surfaces triggers OnLeave effects
- Surface status effects are applied on enter

**Files Modified:**
- Combat/Movement/MovementService.cs (added SurfaceManager dependency, ProcessSurfaceTransition method)

**Tests Added:**
- 4 surface movement tests in MovementServiceTests.cs

### Phase 3: Difficult Terrain Movement Cost ✅
- MovementCostMultiplier from surfaces now applied to movement cost
- Moving through 2x terrain uses double movement budget
- Failure messages include terrain cost information

**Files Modified:**
- Combat/Movement/MovementService.cs (GetMovementCostMultiplier, adjusted CanMoveTo/MoveTo)

**Tests Added:**
- 6 terrain cost tests in MovementServiceTests.cs

### Phase 4: Opportunity Attack Triggers ✅
- Movement out of enemy melee range now triggers opportunity attack detection
- `OnOpportunityAttackTriggered` event fired for CombatArena to handle
- OpportunityAttackInfo contains reactor, reaction definition, and context
- MovementResult includes list of triggered opportunity attacks

**Files Modified:**
- Combat/Movement/MovementService.cs (DetectOpportunityAttacks, GetEnemiesInMeleeRange)

**Tests Added:**
- 14 opportunity attack tests in MovementServiceTests.cs

### Phase 5: Reaction Event Wiring ✅
- EffectPipeline now triggers reactions for damage and spell casts
- `OnDamageTrigger` event before damage is dealt
- `OnAbilityCastTrigger` event for counterspell-type reactions
- Reactions can cancel abilities or modify damage

**Files Modified:**
- Combat/Abilities/EffectPipeline.cs (reaction detection, events)
- Combat/Abilities/Effects/Effect.cs (OnBeforeDamage callback)

**Tests Added:**
- Tests/Unit/EffectPipelineReactionTests.cs (14 tests)

### Phase 6: UntilEvent Status Duration ✅
- StatusSystem now subscribes to RuleEventBus events
- Statuses with DurationType.UntilEvent are automatically removed when condition is met
- Supports: DamageTaken, AttackDeclared, MovementCompleted, HealingReceived, TurnStart/End, etc.

**Files Modified:**
- Combat/Statuses/StatusSystem.cs (event subscription, ProcessEventForStatusRemoval)

**Tests Added:**
- 6 UntilEvent tests in StatusSystemTests.cs

## Test Summary

| Category | Tests Added |
|----------|-------------|
| TurnLifecycleBudgetTests | 6 |
| MovementService (surface) | 4 |
| MovementService (terrain) | 6 |
| MovementService (opportunity attacks) | 14 |
| EffectPipelineReactionTests | 14 |
| StatusSystem (UntilEvent) | 6 |
| **Total new tests** | **50** |

## Integration Points Added

| System A | System B | Integration |
|----------|----------|-------------|
| TurnQueue | ActionBudget | Reset on turn/round start |
| MovementService | SurfaceManager | Enter/leave effects on move |
| MovementService | SurfaceDefinition | Terrain cost multiplier |
| MovementService | ReactionSystem | Opportunity attack detection |
| EffectPipeline | ReactionSystem | Damage/cast reaction triggers |
| StatusSystem | RuleEventBus | UntilEvent status removal |

## Events Added

| Event | Source | Purpose |
|-------|--------|---------|
| `OnOpportunityAttackTriggered` | MovementService | When enemy leaves melee range |
| `OnDamageTrigger` | EffectPipeline | Before damage is dealt |
| `OnAbilityCastTrigger` | EffectPipeline | When spell ability is cast |

## CI Status

After Phase H, run:
```bash
./scripts/ci-build.sh    # Should pass
./scripts/ci-test.sh     # Should pass with ~668 enabled tests
./scripts/ci-benchmark.sh # Should pass
```

## Notes

- All integration is event-driven and non-blocking
- CombatArena can subscribe to events to implement user-facing prompts
- Reaction resolution (actual player prompts) is handled at arena level, not in services
- All integrations are backward compatible (optional dependencies)
