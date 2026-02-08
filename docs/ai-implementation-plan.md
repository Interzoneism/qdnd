# AI Implementation Plan — From Current State to BG3 Quality

## Overview

This plan breaks the AI improvement into **7 work packages** ordered by dependency and impact. Each package is self-contained and testable. The plan moves from foundational plumbing through intelligent behavior to polish.

---

## Work Package 1: Service Wiring & Real Outcome Prediction
**Priority**: CRITICAL — Everything depends on this  
**Estimated Effort**: Medium  
**Dependencies**: None

### Problem
The AI pipeline hardcodes damage at 10, hit chance at 0.65, and passes `null` for LOSService. All downstream scoring is based on fiction.

### Tasks

#### WP1-A: Wire missing services into AIDecisionPipeline and AIScorer
- Add `RulesEngine`, `EffectPipeline`, `TargetValidator`, `LOSService`, `SurfaceManager`, `MovementService`, `DataRegistry` as constructor parameters to `AIDecisionPipeline`
- Pass `LOSService` (currently null) into `AIScorer` constructor
- Pass `ForcedMovementService` into `AIScorer`
- Update `CombatArena` registration to supply all services to the pipeline
- Update `RealtimeAIController` and `UIAwareAIController` to use the enriched pipeline

#### WP1-B: Replace placeholder calculations in AIScorer
- `CalculateExpectedDamage()` → Use `EffectPipeline.PreviewAbility()` to get `(Min, Max, Avg)` damage
- `CalculateHitChance()` → Use `RulesEngine.CalculateHitChance()` with actor stats, target AC, height/cover modifiers
- `CalculateExpectedHealing()` → Use `EffectPipeline.PreviewAbility()` for heal effects
- `EstimateTargetThreat()` → Use ability loadout, HP, and remaining actions

#### WP1-C: Replace placeholder calculations in AITargetEvaluator
- `CalculateThreatLevel()` → Factor in remaining HP, damage potential, number of abilities available
- `GetAttackRange()` → Look up actual ability ranges from `DataRegistry`
- `IsHealer/IsDamageDealer/IsTank()` → Detect from ability tags and combatant tags
- `IsCurrentlyControlled()` → Query status effects system

#### WP1-D: Replace placeholder calculations in AIReactionPolicy
- `CalculateExpectedDamage()` → Use `RulesEngine.RollDamage()` preview
- `CalculateHitChance()` → Use `RulesEngine.CalculateHitChance()`
- `EstimateSpellValue()` → Look up ability definition and estimate from effects

### Validation
- Unit tests: Verify scorer uses real values (mock services, assert non-placeholder outputs)
- Auto-battle: Run 10 seeds, verify no regressions
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Work Package 2: Full Ability Candidate Generation
**Priority**: CRITICAL — AI currently can only basic attack  
**Estimated Effort**: Medium  
**Dependencies**: WP1

### Tasks

#### WP2-A: Add ability loadout to Combatant
- Add `List<string> Abilities` property to `Combatant` entity
- Ensure `ScenarioLoader` populates abilities from scenario data
- Add default ability loadout for combatants (at minimum: basic_attack)

#### WP2-B: Implement GenerateAbilityCandidates in AIDecisionPipeline
- Enumerate all abilities from combatant's loadout
- Filter by `EffectPipeline.CanUseAbility()` (cooldown, resource, action economy)
- For each valid ability:
  - Get valid targets via `TargetValidator.GetValidTargets()`
  - For AoE abilities: Calculate optimal placement via `AITargetEvaluator.FindBestAoEPlacement()`
  - Create `AIAction` with `ActionType = UseAbility`, ability ID, and target(s)
- Handle different target types: `singleUnit`, `circle` (AoE), `self`, `all`

#### WP2-C: Implement GenerateBonusActionCandidates
- Same pattern as WP2-B but filter for abilities with `Cost.UsesBonusAction`
- Include class-specific bonus actions (dual wield, cunning action, etc.)

#### WP2-D: Score ability candidates in AIScorer
- Route `UseAbility` actions to appropriate scoring method based on effect types:
  - Damage → `ScoreAttack()` (enhanced with real preview data)
  - Healing → `ScoreHealing()`
  - Status (buff) → `ScoreStatusEffect()` with "buff" type
  - Status (debuff/CC) → `ScoreStatusEffect()` with appropriate type
  - AoE → `ScoreAoE()` with real hit calculations
- Apply resource efficiency weighting: expensive abilities need to clear a higher score bar

### Validation
- Auto-battle: Verify AI uses abilities beyond basic_attack
- Log analysis: Check that heal abilities are used when allies are hurt
- Unit tests for candidate generation with mock ability loadouts
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Work Package 3: Movement Intelligence & Environmental Awareness
**Priority**: HIGH — AI moves blindly without awareness of threats/terrain  
**Estimated Effort**: Medium  
**Dependencies**: WP1

### Tasks

#### WP3-A: Opportunity attack awareness in movement scoring
- Before scoring a move candidate, call `MovementService.DetectOpportunityAttacks()` on the path
- Add OA penalty to movement score: each potential OA from a living enemy creates significant negative scoring
- Exception: if moving to kill or moving from lethal danger, accept OA risk
- Implement Disengage action generation when surrounded and OAs would trigger

#### WP3-B: Surface and hazard awareness
- Query `SurfaceManager.GetSurfacesAt()` for candidate positions
- Penalize movement into damaging surfaces (fire, poison, acid)
- Bonus for movement that avoids surfaces
- Score surface-creating abilities (area denial, combo setup)

#### WP3-C: Cover-seeking behavior
- For each move candidate, evaluate cover from enemies using `LOSService.GetCover()`
- Ranged combatants: strongly prefer positions with cover
- Melee combatants: accept less cover for positioning advantage
- All roles: prefer positions that deny enemy cover

#### WP3-D: Enhanced movement candidate generation
- Use `MovementService.GetPathPreview()` instead of raw position sampling
- Filter unreachable positions (obstacles, walls) early
- Generate candidates at tactically interesting positions (near cover, on high ground, flanking angles)
- Reduce candidate spam: fewer but smarter candidates

### Validation
- Auto-battle: Verify AI doesn't walk through fire surfaces
- Auto-battle: Verify AI doesn't provoke unnecessary opportunity attacks
- Log analysis: Check movement reasoning in AI debug logs
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Work Package 4: Team Coordination & Target Focus
**Priority**: HIGH — Individual optimization ≠ team optimization  
**Estimated Effort**: Medium  
**Dependencies**: WP1, WP2

### Tasks

#### WP4-A: Wire AITargetEvaluator into the decision pipeline
- The pipeline's `GenerateAttackCandidates()` and ability scoring should use `AITargetEvaluator.EvaluateTargets()` for prioritization
- Pass target priority scores into `AIScorer` so attack scoring respects team-level priority

#### WP4-B: Focus fire coordination
- Introduce a `TeamAIState` shared between all AI combatants on the same faction
- Track:
  - Current focus target (lowest HP enemy, or designated by first attacker)
  - Damage dealt to each enemy this round
  - CC applied this round (avoid redundant CC)
  - Role assignments (who tanks, who supports)
- Score bonus for attacking the focus target
- Score penalty for splitting damage when focus fire is active

#### WP4-C: Flanking coordination
- When generating movement candidates, check if moving to a position creates flanking with any ally
- Bonus score for flanking positions (advantage on attacks)
- Coordinate: if ally A moves to flank, ally B should position on the opposite side

#### WP4-D: Role fulfillment scoring
- Add "role duty" scoring layer:
  - Tank: bonus for positioning between enemies and allies, engaging multiple enemies
  - Support: bonus for staying within heal range of injured allies, penalty for being in melee danger
  - Controller: bonus for CCing un-CC'd targets, penalty for CCing already-controlled
  - DPS: bonus for maximizing damage output, attacking focus target

### Validation
- Auto-battle: Verify focus fire patterns (>60% damage on single target at Hard difficulty)
- Auto-battle: Check for flanking positions being used
- Unit tests for TeamAIState
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Work Package 5: Multi-Action Turn Planning
**Priority**: MEDIUM — Transforms AI from "one action at a time" to coherent turns  
**Estimated Effort**: High  
**Dependencies**: WP1, WP2, WP3

### Tasks

#### WP5-A: Turn plan data structure
- Create `AITurnPlan` class that holds a sequence of actions for a full turn
- Include action + bonus action + movement allocation
- Support re-evaluation: after executing the first action, re-score remaining plan

#### WP5-B: Sequence evaluation
- Common patterns to evaluate:
  - Move → Attack (close then strike)
  - Attack → Move (strike then reposition)  
  - Bonus Action (buff) → Action (attack)
  - Action (heal) → Movement (reposition to safety)
  - Dash (when no targets in range and closing needed)
- Score the full sequence, not just individual actions
- Budget movement: reserve movement for retreat after attacking

#### WP5-C: Action economy maximization
- Penalize plans that waste action economy (ending turn with unused actions)
- Ensure bonus actions are always considered
- Consider "free" actions (object interaction, etc.)

#### WP5-D: Integrate turn planning into pipeline
- Modify `MakeDecision()` to return a `AITurnPlan` instead of single `AIAction`
- Execute plan actions sequentially, re-evaluate between actions if state changes significantly
- Fallback to single-action if planning exceeds time budget

### Validation
- Auto-battle: Verify AI uses both action and bonus action when available
- Auto-battle: Verify AI moves before attacking when out of range
- Log analysis: Verify turn plans in debug output
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Work Package 6: Reaction System Integration
**Priority**: MEDIUM — Reactions are a major tactical lever  
**Estimated Effort**: Medium  
**Dependencies**: WP1

### Tasks

#### WP6-A: Wire AIReactionPolicy into ReactionSystem
- In `ReactionSystem`, when a reaction trigger fires (enemy leaves melee, spell cast, etc.):
  - Check if the eligible reactor is AI-controlled
  - Call `AIReactionPolicy` to evaluate whether to take the reaction
  - Execute reaction if policy says yes
- Register event handlers for all reaction triggers

#### WP6-B: Reaction-aware movement scoring
- When scoring movement, factor in whether the mover has a reaction available
- Score benefit of saving reaction for defensive use vs using it for OA
- Profile-driven: Aggressive profiles always OA, Defensive profiles prefer saving reaction

#### WP6-C: Enemy reaction awareness
- When scoring movement through enemy threat zones, check if enemies have their reaction available
- If enemy has used their reaction, movement through their zone is safer
- Factor into "safe path" calculations

### Validation
- Auto-battle: Verify AI takes opportunity attacks
- Auto-battle: Verify AI uses defensive reactions when low HP
- Unit tests for reaction policy integration
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Work Package 7: Difficulty Scaling & Behavior Polish
**Priority**: MEDIUM — Polish that makes the AI feel right  
**Estimated Effort**: Medium  
**Dependencies**: WP1-WP6

### Tasks

#### WP7-A: Enhanced difficulty profiles
- **Easy**: 
  - 40% random factor
  - No focus fire
  - Use basic attacks 70% of the time, ignoring special abilities
  - Don't seek high ground or cover
  - Don't use reactions
- **Normal**:
  - 10% random factor  
  - Focus wounded targets
  - Use abilities when clearly beneficial
  - Basic positioning awareness
  - Take obvious reactions
- **Hard**:
  - 5% random factor
  - Strong focus fire
  - Optimal ability usage
  - Active positioning (cover, high ground, flanking)
  - Smart reaction management
  - Team coordination active
- **Nightmare**:
  - 0% random factor
  - Perfect focus fire
  - Exploitation of weaknesses (target low saves, break concentration)
  - Full environmental exploitation
  - Counter-strategy based on player composition
  - Maximum team synergy

#### WP7-B: Adaptive behavior triggers
- HP threshold: Switch from aggressive to defensive archetype when HP < 30%
- Numerical advantage: Increase aggression when outnumbering, increase caution when outnumbered
- Ally death: If healer dies, DPS/tank should be more conservative

#### WP7-C: Anti-exploit safeguards
- AI shouldn't use information a player couldn't have (e.g., exact HP numbers of hidden enemies)
- Don't perfectly predict random outcomes
- Add intentional "thinking time" variation so AI feels natural

### Validation
- Auto-battle across all difficulty levels: verify win rate scaling
- Stress test: 50 seeds per difficulty
- Full-fidelity test: visual verification AI behavior looks intelligent
- Build: `scripts/ci-build.sh && scripts/ci-test.sh`

---

## Implementation Order & Dependencies

```
WP1: Service Wiring & Outcome Prediction
  ├── WP2: Ability Candidate Generation      (depends on WP1)
  ├── WP3: Movement Intelligence             (depends on WP1)
  └── WP6: Reaction Integration              (depends on WP1)
       │
WP4: Team Coordination                       (depends on WP1, WP2)
       │
WP5: Multi-Action Planning                   (depends on WP1, WP2, WP3)
       │
WP7: Difficulty & Polish                     (depends on all above)
```

**Parallelization opportunities:**
- WP2 and WP3 can proceed in parallel after WP1
- WP6 can proceed in parallel with WP2/WP3
- WP4 can start once WP2 is complete
- WP5 requires WP2 and WP3
- WP7 should be last

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Performance regression from real calculations | Cache within decision, time-budget cutoff |
| Candidate explosion with full ability enumeration | Early pruning by CanUseAbility + range |
| Breaking existing auto-battle tests | Run regression after each WP |
| Over-complex turn planning | Start simple (2-action sequences), expand |
| AI feels too strong on Normal | Tune random factor and intentional suboptimality |

---

## Implementation Status

All 7 work packages have been implemented and verified. Build: 0 errors, 0 warnings. Tests: 1151 passed, 0 failed.

| Work Package | Status | Key Files |
|---|---|---|
| WP1: Service Wiring | ✅ Done | AIDecisionPipeline (LateInitialize), AIScorer, AITargetEvaluator, AIReactionPolicy |
| WP2: Ability Candidates | ✅ Done | AIDecisionPipeline (GenerateAbilityCandidates, GenerateBonusActionCandidates), Combatant.Abilities |
| WP3: Movement Intelligence | ✅ Done | AIDecisionPipeline (ScoreMovement: OA, surfaces, cover, flanking, retreat, disengage) |
| WP4: Team Coordination | ✅ Done | TeamAIState (new), AIDecisionPipeline (focus fire, redundant CC avoidance) |
| WP5: Multi-Action Planning | ✅ Done | AITurnPlan (new), AIDecisionPipeline (BuildTurnPlan, plan caching/validation) |
| WP6: Reaction Integration | ✅ Done | AIReactionHandler (new), AIDecisionPipeline (ReactionHandler, enemy reaction awareness) |
| WP7: Difficulty & Polish | ✅ Done | AdaptiveBehavior (new), AIDecisionPipeline (GetEffectiveWeight, enhanced SelectBest, anti-exploit) |

### New Files Created
- `Combat/AI/TeamAIState.cs` — Team-level coordination state (focus target, damage tracking, CC tracking)
- `Combat/AI/AITurnPlan.cs` — Multi-action turn plan with validation and sequencing
- `Combat/AI/AIReactionHandler.cs` — Bridge between ReactionSystem and AIReactionPolicy
- `Combat/AI/AdaptiveBehavior.cs` — Dynamic behavior modifiers based on combat state

### Key Architectural Decisions
- **LateInitialize pattern**: Services pulled from CombatContext after all are registered, not at construction time
- **Turn plan caching**: Plans are cached and revalidated; if a target dies, the plan is re-created
- **Adaptive weight overrides**: Temporary weight multipliers applied during scoring, not mutating the AIProfile
- **Reaction handler bridge**: Separate class mapping ReactionTriggerType to AIReactionPolicy methods
