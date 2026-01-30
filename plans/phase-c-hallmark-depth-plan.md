# Plan: Phase C - Hallmark Depth (BG3-Style Features)

**Created:** 2026-01-30
**Status:** ✅ COMPLETE (2026-01-30)

## Summary

Phase C implements the "hallmark" features that make combat feel like BG3/Divinity: full action economy with action/bonus/move/reaction budgets, a reaction/interrupt system with prompts and priority ordering, surfaces and field effects that interact and transform, LOS/cover/height mechanics, and movement with jump/climb/fall/forced movement.

## Context & Analysis

**Phase B Completed:**
- RulesEngine with modifiers and events ✅
- Effects: damage, heal, status, resource ✅
- Status system with tick effects ✅
- Targeting and validation ✅
- Data-driven content loading ✅

**Phase C Scope (from Master TODO):**
- Full action economy (action/bonus/move/reaction)
- Reactions/interrupt stack + prompts
- Surfaces/field effects + environment interactions
- LOS/cover/height hooks
- Jump/climb/fall/forced movement

## Implementation Phases

### Phase 1: Action Economy System

**Objective:** Implement action/bonus/movement/reaction budgets that reset properly.

**Files to Create:**
- `Combat/Actions/ActionBudget.cs` - Budget tracking per combatant
- `Combat/Actions/ActionType.cs` - Enum for action types

**Files to Modify:**
- `Combat/Entities/Combatant.cs` - Add ActionBudget component
- `Combat/Abilities/AbilityDefinition.cs` - Already has Cost, ensure it uses action types
- `Combat/Abilities/EffectPipeline.cs` - Consume action budget on ability use

**Key Classes:**
```csharp
public class ActionBudget
{
    public bool HasAction { get; set; } = true;
    public bool HasBonusAction { get; set; } = true;
    public bool HasReaction { get; set; } = true;
    public float RemainingMovement { get; set; }
    public float MaxMovement { get; set; }
    
    public void ResetForTurn();
    public void ResetReactionForRound();
    public bool CanUseAction(AbilityCost cost);
    public void ConsumeAction(AbilityCost cost);
}
```

**Tests:**
- Budget decrements correctly
- Action conversion (use action to dash)
- Reset at turn/round boundaries
- Ability use blocked when budget exhausted

**Acceptance Criteria:**
- [ ] ActionBudget class implemented
- [ ] Combatant has budget property
- [ ] EffectPipeline consumes budget
- [ ] Budget resets on turn start
- [ ] Reaction resets on round start
- [ ] Tests pass

---

### Phase 2: Position System Foundation

**Objective:** Add position tracking to combatants for movement, LOS, and AoE.

**Files to Create:**
- `Combat/Movement/PositionComponent.cs` - Position tracking

**Files to Modify:**
- `Combat/Entities/Combatant.cs` - Add Position property
- `Combat/Targeting/TargetValidator.cs` - Use real positions for range/AoE
- `Data/ScenarioLoader.cs` - Load spawn positions from scenarios

**Scenario Update:**
```json
{
    "units": [
        {
            "id": "unit1",
            "position": { "x": 0, "y": 0, "z": 0 }
        }
    ]
}
```

**Tests:**
- Position set from scenario
- Distance calculations correct
- AoE uses real positions

**Acceptance Criteria:**
- [ ] Combatant has Position (Vector3)
- [ ] Scenarios can specify spawn positions
- [ ] TargetValidator uses real distances
- [ ] Tests pass

---

### Phase 3: Movement System

**Objective:** Implement movement with budget consumption and path validation.

**Files to Create:**
- `Combat/Movement/MovementService.cs` - Movement execution
- `Combat/Movement/PathValidator.cs` - Path cost calculation

**Files to Modify:**
- `Combat/Services/CommandService.cs` - Enhance MoveCommand

**Movement Features:**
- Movement budget consumption
- Path cost calculation (straight-line for now)
- Position update
- Movement events

**Tests:**
- MoveTo consumes budget correctly
- Cannot move beyond remaining budget
- Movement events emitted

**Acceptance Criteria:**
- [ ] MovementService implemented
- [ ] Budget consumed on move
- [ ] Position updates correctly
- [ ] Events emitted (MovementStarted, MovementCompleted)
- [ ] Tests pass

---

### Phase 4: Reaction System Core

**Objective:** Implement reaction triggers, eligibility, and prompts.

**Files to Create:**
- `Combat/Reactions/ReactionTrigger.cs` - Trigger types
- `Combat/Reactions/ReactionSystem.cs` - Reaction processing
- `Combat/Reactions/ReactionPrompt.cs` - Prompt for player decisions

**Reaction Flow:**
1. Event occurs (enemy leaves melee range, spell cast, etc.)
2. ReactionSystem queries eligible reactors
3. For each eligible reactor:
   - If AI: auto-decide based on policy
   - If player: emit ReactionPrompt event
4. Execute reaction (may modify/cancel triggering event)

**Key Triggers:**
- `OnEnemyLeavesReach` - Opportunity attacks
- `OnAllyTakesDamage` - Protection abilities
- `OnSpellCast` - Counterspell
- `OnAttacked` - Shield, parry

**Tests:**
- Trigger detection works
- Eligible reactors found
- Prompt emitted for player units
- Reaction consumes reaction budget

**Acceptance Criteria:**
- [ ] ReactionTrigger enum defined
- [ ] ReactionSystem queries eligibility
- [ ] Prompts emitted for player decisions
- [ ] Reaction budget consumed
- [ ] Tests pass

---

### Phase 5: Interrupt Stack (Resolution Stack)

**Objective:** Allow reactions to modify or cancel triggering events.

**Files to Create:**
- `Combat/Reactions/ResolutionStack.cs` - Nested execution

**How It Works:**
```
1. Attack declared
2. Push attack to stack
3. Query reactions → defender can Shield
4. Push Shield reaction to stack
5. Resolve Shield (raise AC temporarily)
6. Pop, continue attack resolution
7. Attack misses due to higher AC
```

**Integration:**
- EffectPipeline uses ResolutionStack
- Events are cancellable (IsCancellable property exists)
- Reactions can modify event values

**Tests:**
- Reaction interrupts attack
- Counterspell cancels spell
- Multiple reactions resolve in priority order

**Acceptance Criteria:**
- [ ] ResolutionStack manages nested execution
- [ ] Reactions can cancel events
- [ ] Reactions can modify event values
- [ ] Priority ordering works
- [ ] Tests pass

---

### Phase 6: Surfaces System (Stubs → Real)

**Objective:** Implement surfaces/field effects that persist and interact.

**Files to Create:**
- `Combat/Environment/SurfaceDefinition.cs` - Surface types
- `Combat/Environment/SurfaceInstance.cs` - Active surface
- `Combat/Environment/SurfaceManager.cs` - Tracks surfaces

**Surface Features:**
- Position and radius
- Duration (turns/permanent)
- Effects on enter/leave/turn start
- Surface interactions (fire + oil = bigger fire, water + electricity = electrify)

**Sample Surfaces:**
- Fire: Damage on enter/turn start
- Poison: Apply poison status
- Ice: Difficult terrain + slip chance
- Water: Makes wet, conducts electricity

**Tests:**
- Surface created at position
- Unit entering triggers effect
- Turn start in surface triggers effect
- Surface transforms on interaction

**Acceptance Criteria:**
- [ ] SurfaceDefinition and SurfaceInstance classes
- [ ] SurfaceManager tracks active surfaces
- [ ] Enter/leave/turn effects work
- [ ] Surface interactions work
- [ ] Tests pass

---

### Phase 7: LOS/Cover System

**Objective:** Add line-of-sight and cover mechanics.

**Files to Create:**
- `Combat/Environment/LOSService.cs` - LOS queries
- `Combat/Environment/CoverType.cs` - Cover levels

**Features:**
- `HasLineOfSight(source, target)` - Raycast check
- `GetCover(source, target)` - Returns None/Half/Full
- Cover modifiers applied to attacks

**Integration:**
- TargetValidator uses LOS for ability validation
- RulesEngine applies cover modifiers

**Tests:**
- LOS blocked by obstacles
- Cover reduces hit chance
- Full cover blocks targeting

**Acceptance Criteria:**
- [ ] LOSService with HasLOS and GetCover
- [ ] Cover modifiers applied to attacks
- [ ] Full cover prevents targeting
- [ ] Tests pass

---

### Phase 8: Height/Verticality

**Objective:** Add height advantage/disadvantage and fall damage.

**Files to Modify:**
- `Combat/Movement/MovementService.cs` - Fall damage
- `Combat/Rules/RulesEngine.cs` - Height modifiers

**Features:**
- High ground: Attack advantage
- Low ground: Attack disadvantage
- Fall damage: 1d6 per 10 units
- Forced fall when pushed off ledge

**Tests:**
- Height difference applies advantage/disadvantage
- Fall damage calculated correctly
- Forced push off ledge causes fall

**Acceptance Criteria:**
- [ ] Height advantage/disadvantage modifiers
- [ ] Fall damage system
- [ ] Tests pass

---

### Phase 9: Movement Types (Jump/Climb/Teleport)

**Objective:** Complete the movement system with special movement types.

**Files to Modify:**
- `Combat/Movement/MovementService.cs` - Movement types

**Movement Types:**
- Walk (normal)
- Jump (costs movement, clears gaps/low obstacles)
- Climb (slow, vertical)
- Teleport (no path required)
- Swim (if water surface)

**Tests:**
- Jump clears gap
- Climb rate is slower
- Teleport ignores obstacles

**Acceptance Criteria:**
- [ ] Movement types implemented
- [ ] Jump validation works
- [ ] Tests pass

---

### Phase 10: Forced Movement

**Objective:** Make ForcedMoveEffect actually move units with collision.

**Files to Modify:**
- `Combat/Abilities/Effects/Effect.cs` - ForcedMoveEffect
- `Combat/Movement/MovementService.cs` - Push/pull logic

**Features:**
- Push/pull direction from source
- Collision with walls stops movement
- Push off ledge causes fall
- Opportunity attack on forced leave (optional toggle)

**Tests:**
- Push moves target away
- Pull moves target closer
- Wall collision stops movement
- Ledge fall triggers

**Acceptance Criteria:**
- [ ] ForcedMoveEffect actually moves units
- [ ] Collision detection works
- [ ] Fall damage from ledge push
- [ ] Tests pass

---

### Phase 11: Integration + Scenarios

**Objective:** Add Phase C scenarios and regression tests.

**Files to Create:**
- `Data/Scenarios/reaction_test.json`
- `Data/Scenarios/surface_test.json`
- `Data/Scenarios/movement_test.json`
- `Data/Surfaces/sample_surfaces.json`

**Integration Tests:**
- Opportunity attack scenario
- Surface interaction scenario
- Forced movement with fall scenario

**Acceptance Criteria:**
- [ ] Scenarios created
- [ ] Integration tests pass
- [ ] All Phase C features testable via scenarios

---

### Phase 12: Final Verification

**Objective:** Run CI, update docs, verify completion.

**Steps:**
1. Run `scripts/ci-build.sh`
2. Run `scripts/ci-test.sh`
3. Update READY_TO_START.md
4. Create docs/PHASE_C_GUIDE.md

**Acceptance Criteria:**
- [ ] CI passes
- [ ] Documentation updated
- [ ] Phase C marked complete

---

## Open Questions

1. **Movement Grid vs Free**
   - **Option A:** Pure free movement with distance budget
   - **Option B:** Hybrid with optional grid snap
   - **Recommendation:** Option A for now, grid overlay later

2. **Opportunity Attack Triggers**
   - **Option A:** Any movement out of reach
   - **Option B:** Only voluntary movement (not forced)
   - **Recommendation:** Option B (D&D 5e style)

3. **Surface Stacking**
   - **Option A:** Only one surface per area
   - **Option B:** Multiple surfaces can overlap
   - **Recommendation:** Option B with interaction rules

## Risks & Mitigation

- **Risk:** Position system complexity with Godot integration
  - **Mitigation:** Abstract position behind interface, test without Godot

- **Risk:** Resolution stack infinite loops (reaction triggers reaction)
  - **Mitigation:** Limit reaction depth, prevent self-triggering

- **Risk:** Surface interactions exponential complexity
  - **Mitigation:** Define transform table, limit to core interactions

## Success Criteria

- [ ] Full action economy with budget tracking
- [ ] Reaction system with prompts and priority
- [ ] Surfaces that interact and transform
- [ ] LOS and cover mechanics
- [ ] Height advantage/disadvantage
- [ ] Movement with jump/climb/fall
- [ ] Forced movement with collision
- [ ] 50+ new tests
- [ ] CI passes
- [ ] Documentation complete

## Estimated Effort

- Phase 1: 45 min (Action Economy)
- Phase 2: 30 min (Position System)
- Phase 3: 45 min (Movement Service)
- Phase 4: 1 hr (Reaction Core)
- Phase 5: 1 hr (Resolution Stack)
- Phase 6: 1 hr (Surfaces)
- Phase 7: 45 min (LOS/Cover)
- Phase 8: 30 min (Height)
- Phase 9: 30 min (Movement Types)
- Phase 10: 45 min (Forced Movement)
- Phase 11: 30 min (Scenarios)
- Phase 12: 15 min (Verification)

**Total: ~9 hours**

## Notes for Implementation

1. **Start with Phases 1-2** - Foundation for everything else
2. **Phases 3-5 can interleave** - Movement and reactions interact
3. **Phases 6-8 are mostly independent** - Can parallelize
4. **Test continuously** - Run CI after each phase
5. **Keep stubs when needed** - Real geometry comes with 3D integration
