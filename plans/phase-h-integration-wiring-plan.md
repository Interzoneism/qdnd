# Plan: Phase H - Integration Wiring and System Connections

**Created:** 2026-02-02
**Status:** Ready for Atlas Execution

## Summary

Phase H connects existing but currently disconnected combat systems. The infrastructure exists (reactions, surfaces, action economy) but lacks the wiring to make them work together in the main game loop. This phase focuses on integration rather than new feature development.

## Context & Analysis

**Key Insight:** Most "unchecked" master TO-DO items are actually implemented but not integrated. The systems exist in isolation.

**Gaps Identified:**

| System | What Exists | What's Missing |
|--------|-------------|----------------|
| Reactions | ReactionSystem, triggers, prompts | Wiring to movement/damage events |
| Action Economy | ActionBudget with all types | Reset not called from turn system |
| Opportunity Attacks | Enum defined | No detection/trigger on movement |
| Surfaces | Create/remove/transform, effects | Not called from normal movement |
| Status Effects | Full stacking/duration system | UntilEvent not wired |
| Difficult Terrain | MovementCostMultiplier field | Not applied in pathing |

**Files to Modify:**
- Combat/Arena/CombatArena.cs (main integration point)
- Combat/Movement/MovementService.cs (surface/OA integration)
- Combat/States/* (turn lifecycle integration)

## Implementation Phases

### Phase 1: Action Budget Reset Integration

**Objective:** Wire ActionBudget.ResetForTurn() into turn lifecycle

**Files to Modify:**
- Combat/States/TurnStartState.cs (or wherever turn starts)
- Combat/Arena/CombatArena.cs

**Changes:**
1. In turn start handler, call `currentCombatant.ActionBudget.ResetForTurn()`
2. In round start handler, call `ResetReactionForRound()` on all combatants
3. Add test to verify budget resets

**Acceptance Criteria:**
- [ ] ActionBudget resets automatically each turn
- [ ] Reactions reset each round
- [ ] Existing tests still pass

---

### Phase 2: Surface Integration with Movement

**Objective:** Wire SurfaceManager into normal movement flow

**Files to Modify:**
- Combat/Movement/MovementService.cs
- Combat/Arena/CombatArena.cs

**Changes:**
1. In MovementService.MoveTo, after position update:
   - Call SurfaceManager.ProcessEnter() if entering new surface
   - Call SurfaceManager.ProcessLeave() if leaving surface
2. Apply Status IDs from surface effects
3. Execute TriggerEffects

**Steps:**
1. Add SurfaceManager dependency to MovementService
2. Track previous position before move
3. Check surfaces at old/new positions
4. Trigger enter/leave effects

**Acceptance Criteria:**
- [ ] Walking into fire surface deals damage
- [ ] Walking out of surface triggers OnLeave effects
- [ ] Surface status effects are applied

---

### Phase 3: Difficult Terrain Movement Cost

**Objective:** Apply MovementCostMultiplier from surfaces

**Files to Modify:**
- Combat/Movement/MovementService.cs

**Changes:**
1. Before consuming movement budget, check surfaces at destination
2. Apply highest MovementCostMultiplier from all surfaces
3. Reject move if insufficient budget after multiplier

**Acceptance Criteria:**
- [ ] Moving through difficult terrain costs extra movement
- [ ] Movement rejection includes reason about terrain

---

### Phase 4: Opportunity Attack Triggers

**Objective:** Wire ReactionSystem to detect enemies leaving melee range

**Files to Modify:**
- Combat/Movement/MovementService.cs
- Combat/Reactions/ReactionSystem.cs

**Changes:**
1. In MovementService.MoveTo, before moving:
   - Get all enemies in melee range of mover
   - For each enemy, check if mover's new position is outside melee range
   - If leaving range, create ReactionTriggerContext with EnemyLeavesReach
   - Query ReactionSystem for eligible reactors
   - Wait for reaction resolution before completing move

**Acceptance Criteria:**
- [ ] Moving out of enemy melee range triggers opportunity attack check
- [ ] Eligible reactors are queried
- [ ] Reaction can interrupt/modify movement

---

### Phase 5: Reaction Event Wiring

**Objective:** Wire reactions to damage/cast events

**Files to Modify:**
- Combat/Abilities/EffectPipeline.cs (for damage/cast events)

**Changes:**
1. Before applying damage:
   - Create trigger context for DamageTaken/DamageDealt
   - Query eligible reactors
   - Allow reactions to modify/cancel
2. Before ability cast:
   - Create trigger context for AbilityCast
   - Query for counterspell-style reactions

**Acceptance Criteria:**
- [ ] Damage events can trigger reactions
- [ ] Spell cast events can trigger reactions
- [ ] Reactions can modify/cancel events

---

### Phase 6: UntilEvent Status Duration

**Objective:** Wire status removal for event-based durations

**Files to Modify:**
- Combat/Statuses/StatusSystem.cs
- Combat/Rules/RuleEventBus.cs

**Changes:**
1. Subscribe StatusSystem to relevant events
2. On event, check all statuses for matching UntilEvent conditions
3. Remove matching statuses

**Acceptance Criteria:**
- [ ] Status with "until attacked" duration removes on attack
- [ ] Status with "until saves" duration removes on successful save

---

## Open Questions

1. **How to handle reaction timing in non-interactive (AI) turns?**
   - **Option A:** AI always auto-reacts based on policy
   - **Option B:** Configurable per-reaction setting
   - **Recommendation:** Option A for now, simpler

2. **Should opportunity attacks consume movement mid-step?**
   - **Option A:** Movement completes first, then reaction
   - **Option B:** Movement pauses, reaction resolves, then continues
   - **Recommendation:** Option B for fidelity to BG3/D&D

## Risks & Mitigation

- **Risk:** Reaction wiring creates infinite loops (reaction triggers reaction)
  - **Mitigation:** ResolutionStack.MaxDepth already enforced

- **Risk:** Performance impact from surface checks every movement
  - **Mitigation:** Spatial partitioning for surface queries

## Success Criteria

- [ ] ActionBudget resets automatically each turn
- [ ] Surface effects trigger on normal movement
- [ ] Difficult terrain costs extra movement
- [ ] Leaving enemy reach triggers opportunity attack eligibility
- [ ] `./scripts/ci-test.sh` passes
- [ ] New integration tests verify wiring

## Notes for Atlas

- This phase is about wiring existing systems, not creating new ones
- Focus on minimal integration changes
- Add integration tests for each wiring point
- Do not change existing system APIs unless necessary
