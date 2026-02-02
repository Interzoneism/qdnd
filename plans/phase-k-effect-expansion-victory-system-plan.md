# Plan: Phase K - Effect Expansion & Victory System

**Created:** 2026-02-02
**Status:** Ready for Atlas Execution

## Summary

This phase expands the effect system with missing effect types (SummonCombatant, GrantAdvantage, SpawnObject, Interrupt/Counter), implements a modular victory condition system to replace hardcoded faction checks, and adds the Counter/Interrupt pattern to reactions. These are high-impact gaps blocking full BG3 parity.

## Context & Analysis

**Relevant Files:**
- [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs): Core effect implementations
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): Combat lifecycle, victory/defeat checks
- [Combat/Reactions/ReactionSystem.cs](Combat/Reactions/ReactionSystem.cs): Reaction eligibility and execution
- [Combat/Reactions/ResolutionStack.cs](Combat/Reactions/ResolutionStack.cs): Nested action resolution
- [Combat/Entities/Combatant.cs](Combat/Entities/Combatant.cs): Entity model for summons
- [Combat/Services/TurnQueue.cs](Combat/Services/TurnQueue.cs): Initiative management

**Key Functions/Classes:**
- `Effect` base class in Effect.cs: Abstract effect pattern
- `EffectPipeline.ExecuteAbility()`: Effect execution entrypoint  
- `CombatArena.EndCombat()`: Hardcoded victory check (needs refactor)
- `ResolutionStack.Push()/Pop()`: Nested resolution with cancellation support
- `TurnQueue.Add()/Remove()`: Dynamic initiative management

**Dependencies:**
- Godot Vector3: Position/movement for summons
- CombatContext: Service locator for all systems
- EventBus (RuleEvent): Event publication

**Patterns & Conventions:**
- Effects inherit from `Effect` base class
- Effects define `Execute(EffectContext)` method
- Victory conditions should be data-driven (JSON)
- All features require test coverage

## Implementation Phases

### Phase 1: SummonCombatant Effect

**Objective:** Enable abilities to spawn new combatants (pets, allies, environmental hazards) that participate in combat.

**Files to Modify/Create:**
- Combat/Abilities/Effects/SummonCombatantEffect.cs: New effect class
- Combat/Abilities/Effects/Effect.cs: Register handler
- Combat/Services/TurnQueue.cs: Add `InsertAfter()` method for initiative placement
- Data/Abilities/sample_abilities.json: Add summon test ability

**Tests to Write:**
- `SummonCombatantEffectTests.cs`: Summon creates combatant with correct stats
- `SummonCombatantEffectTests.cs`: Summon inherits owner's faction
- `SummonCombatantEffectTests.cs`: Summon added to initiative after summoner
- `SummonCombatantEffectTests.cs`: Summon removed from initiative on death
- `SummonCombatantEffectTests.cs`: Summon position is valid (near summoner)

**Steps:**
1. Write unit tests for SummonCombatantEffect behavior
2. Run tests (should fail - no implementation)
3. Create SummonCombatantEffect.cs with:
   - `SummonDefinitionId`: Reference to combatant template
   - `Duration`: How long summon lasts (turns/permanent)
   - `SpawnOffset`: Position relative to caster
   - `OwnerId`: Tracks who owns the summon
4. Implement `Execute()`: Create Combatant, set owner, add to TurnQueue
5. Add `InsertAfter()` to TurnQueue for initiative placement
6. Register effect type in Effect.cs
7. Run tests (should pass)
8. Add sample summon ability to sample_abilities.json

**Acceptance Criteria:**
- [ ] SummonCombatantEffect creates combatants with correct stats
- [ ] Summoned units appear in initiative order after summoner
- [ ] Summoned units have owner tracking
- [ ] Duration-based summons expire correctly
- [ ] All 5+ tests pass
- [ ] Code follows project conventions

---

### Phase 2: GrantAdvantage Effect

**Objective:** Create a dedicated effect for granting advantage/disadvantage that integrates cleanly with the modifier system.

**Files to Modify/Create:**
- Combat/Abilities/Effects/GrantAdvantageEffect.cs: New effect class
- Combat/Rules/Modifier.cs: Ensure ModifierType.Advantage/Disadvantage work correctly
- Combat/Abilities/Effects/Effect.cs: Register handler

**Tests to Write:**
- `GrantAdvantageEffectTests.cs`: Effect adds advantage modifier to target
- `GrantAdvantageEffectTests.cs`: Effect adds disadvantage modifier to target  
- `GrantAdvantageEffectTests.cs`: Duration-based advantage expires
- `GrantAdvantageEffectTests.cs`: Advantage stacking follows D&D 5e cancel-out rules

**Steps:**
1. Write unit tests for GrantAdvantageEffect behavior
2. Run tests (should fail)
3. Create GrantAdvantageEffect.cs with:
   - `IsAdvantage`: bool (true = advantage, false = disadvantage)
   - `Duration`: Turns/rounds
   - `AppliesTo`: Attack rolls, saving throws, ability checks (flags)
4. Implement `Execute()`: Add modifier to target's ModifierStack
5. Ensure modifier is removed on duration expiry (via StatusSystem integration or direct tracking)
6. Register effect type
7. Run tests (should pass)

**Acceptance Criteria:**
- [ ] GrantAdvantageEffect properly applies advantage modifier
- [ ] Disadvantage variant works correctly
- [ ] Duration expiry removes modifiers
- [ ] Stacking rules match D&D 5e (cancel-out)
- [ ] All 4+ tests pass

---

### Phase 3: SpawnObject Effect

**Objective:** Enable abilities to spawn interactable objects (barriers, traps, totems) in the arena.

**Files to Modify/Create:**
- Combat/Abilities/Effects/SpawnObjectEffect.cs: New effect class
- Combat/Environment/CombatObject.cs: New class for spawned objects
- Combat/Environment/CombatObjectManager.cs: Track spawned objects
- Combat/Abilities/Effects/Effect.cs: Register handler

**Tests to Write:**
- `SpawnObjectEffectTests.cs`: Effect spawns object at target location
- `SpawnObjectEffectTests.cs`: Object has owner and faction
- `SpawnObjectEffectTests.cs`: Object can block LOS (if configured)
- `SpawnObjectEffectTests.cs`: Duration-based objects expire
- `SpawnObjectEffectTests.cs`: Objects can be destroyed by damage

**Steps:**
1. Write unit tests for SpawnObjectEffect
2. Run tests (should fail)
3. Create CombatObject.cs:
   - `Id`, `Position`, `OwnerId`, `Faction`
   - `MaxHP`, `CurrentHP` (if destructible)
   - `BlocksLOS`, `BlocksMovement` flags
   - `Duration` (turns or permanent)
4. Create CombatObjectManager.cs to track active objects
5. Create SpawnObjectEffect.cs with:
   - `ObjectDefinitionId`: Template reference
   - `SpawnPosition`: Target location
6. Implement `Execute()`: Create object, register with manager
7. Wire object expiry into turn system
8. Register effect type
9. Run tests (should pass)

**Acceptance Criteria:**
- [ ] SpawnObjectEffect creates objects with correct properties
- [ ] Objects can block LOS/movement when configured
- [ ] Destructible objects track HP
- [ ] Duration-based objects expire
- [ ] All 5+ tests pass

---

### Phase 4: Interrupt/Counter Reaction Pattern

**Objective:** Enable reactions that can cancel or modify the triggering action's effects (counterspells, parrying, etc.).

**Files to Modify/Create:**
- Combat/Reactions/CounterReaction.cs: New reaction type
- Combat/Reactions/ReactionSystem.cs: Add interrupt handling
- Combat/Reactions/ResolutionStack.cs: Enhanced cancellation support
- Combat/Reactions/ReactionDefinition.cs: Add CounterType field

**Tests to Write:**
- `CounterReactionTests.cs`: Counter reaction can cancel ability execution
- `CounterReactionTests.cs`: Counter with save allows target to resist
- `CounterReactionTests.cs`: Partial counter reduces effect magnitude
- `CounterReactionTests.cs`: Multiple counters resolve in priority order
- `CounterReactionTests.cs`: Counter consumes reaction budget

**Steps:**
1. Write unit tests for counter/interrupt behavior
2. Run tests (should fail)
3. Add `CounterType` enum: `FullCancel`, `PartialReduce`, `Redirect`
4. Add counter fields to ReactionDefinition:
   - `CounterType`: Type of interruption
   - `CounterMagnitude`: For partial reductions (percentage)
   - `RequiresSave`: If target gets saving throw to avoid counter
5. Implement counter execution in ReactionSystem:
   - On reaction trigger, check if reaction is counter-type
   - If FullCancel: Set ResolutionStack item's IsCancelled = true
   - If PartialReduce: Modify effect magnitude before execution
6. Update ResolutionStack to support magnitude modification
7. Run tests (should pass)
8. Add sample counter reaction (e.g., "Counterspell")

**Acceptance Criteria:**
- [ ] Counter reactions can fully cancel abilities
- [ ] Counter reactions can partially reduce effects
- [ ] Save-based counters respect saving throw results
- [ ] Multiple counters resolve correctly
- [ ] All 5+ tests pass

---

### Phase 5: Modular Victory Condition System

**Objective:** Replace hardcoded faction-check victory logic with a data-driven, extensible victory condition system.

**Files to Modify/Create:**
- Combat/Arena/VictoryCondition.cs: New condition system
- Combat/Arena/CombatArena.cs: Refactor EndCombat to use conditions
- Data/Scenarios/: Update scenarios with victory conditions

**Tests to Write:**
- `VictoryConditionTests.cs`: LastFactionStanding condition works
- `VictoryConditionTests.cs`: ObjectiveReached condition (reach zone)
- `VictoryConditionTests.cs`: ProtectTarget condition (NPC survives N turns)
- `VictoryConditionTests.cs`: KillTarget condition (boss dies)
- `VictoryConditionTests.cs`: Multiple conditions combine (AND/OR)
- `VictoryConditionTests.cs`: Defeat conditions trigger correctly

**Steps:**
1. Write unit tests for victory conditions
2. Run tests (should fail)
3. Create VictoryCondition.cs:
   ```csharp
   public enum VictoryType { LastFactionStanding, KillTarget, ReachZone, SurviveTurns, ProtectTarget }
   public class VictoryCondition {
       public VictoryType Type { get; set; }
       public string TargetId { get; set; }  // For KillTarget, ProtectTarget
       public Vector3? Zone { get; set; }    // For ReachZone
       public float ZoneRadius { get; set; }
       public int TurnCount { get; set; }    // For SurviveTurns
   }
   ```
4. Create VictoryChecker.cs:
   - `Check(VictoryCondition, CombatContext)`: Returns true if met
   - Support AND/OR combination of multiple conditions
5. Refactor CombatArena.EndCombat():
   - Load VictoryConditions from scenario
   - Call VictoryChecker each turn end
   - Trigger EndCombat when any victory/defeat condition met
6. Update sample scenarios with explicit victory conditions
7. Run tests (should pass)

**Acceptance Criteria:**
- [ ] VictoryCondition system is data-driven
- [ ] All 5 victory types implemented
- [ ] Conditions load from scenario JSON
- [ ] Combat ends correctly based on conditions
- [ ] All 6+ tests pass

---

### Phase 6: Integration Testing & Documentation

**Objective:** Verify all new systems work together and update documentation.

**Files to Modify/Create:**
- Tests/Integration/PhaseKIntegrationTests.cs: Cross-system tests
- docs/PHASE_K_GUIDE.md: Phase K documentation
- READY_TO_START.md: Update with Phase K info

**Tests to Write:**
- `PhaseKIntegrationTests.cs`: Summon + counter interaction
- `PhaseKIntegrationTests.cs`: Victory condition with spawned objects
- `PhaseKIntegrationTests.cs`: Full combat scenario with all new effects

**Steps:**
1. Write integration tests
2. Run ci-build.sh and ci-test.sh
3. Create docs/PHASE_K_GUIDE.md
4. Update READY_TO_START.md
5. Create phase-k-complete.md

**Acceptance Criteria:**
- [ ] All integration tests pass
- [ ] CI build passes
- [ ] CI tests pass (all ~800+ tests)
- [ ] Documentation complete

---

## Open Questions

1. **Summon initiative placement?**
   - **Option A:** Always after summoner in current round
   - **Option B:** Roll initiative for summon
   - **Recommendation:** Option A (simpler, BG3-like behavior)

2. **Object destruction events?**
   - **Option A:** Fire generic DamageApplied events
   - **Option B:** Fire specific ObjectDestroyed event
   - **Recommendation:** Option B for cleaner event handling

3. **Counter priority ordering?**
   - **Option A:** First-declared-first-resolved (stack)
   - **Option B:** Initiative order
   - **Recommendation:** Option A (matches MTG stack rules, intuitive)

## Risks & Mitigation

- **Risk:** Summon spam could degrade performance
  - **Mitigation:** Add summon limits per caster (configurable)

- **Risk:** Victory conditions add complexity to scenarios
  - **Mitigation:** Default to LastFactionStanding if none specified

- **Risk:** Counter reactions could create infinite loops
  - **Mitigation:** Add counter-counter prevention (can't counter a counter)

## Success Criteria

- [ ] 4 new effect types implemented (Summon, GrantAdvantage, SpawnObject, Counter)
- [ ] Modular victory condition system replaces hardcoded checks
- [ ] 30+ new tests added
- [ ] All CI gates pass
- [ ] Documentation updated

## Notes for Atlas

- Effect classes follow the existing pattern in Effect.cs
- All effects must support preview/undo where applicable
- Victory conditions should be optional in scenarios (default to LastFactionStanding)
- Counter reactions are a special case of the existing reaction system
- Test in isolation first, then integration
