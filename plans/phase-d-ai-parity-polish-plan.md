# Plan: Phase D - AI Parity and Polish

**Created:** 2026-01-31
**Status:** Ready for Implementation

## Summary

Phase D implements tactical AI with utility-based decision making, reaction policies, and polish systems including enhanced combat logging with breakdown payloads, HUD data models, and animation/camera integration hooks. This phase makes the combat system feel complete and provides the AI intelligence to make tactical decisions like a BG3/Divinity enemy.

## Context & Analysis

**Phase C Completed:**
- Action economy (action/bonus/movement/reaction) ✅
- Reaction system with triggers and prompts ✅
- Surfaces with interactions ✅
- LOS/Cover/Height systems ✅
- Movement types (jump/climb/teleport/fly) ✅
- Forced movement with collision ✅

**Phase D Scope (from Master TODO):**
- Tactical AI with scoring + reaction policy
- Full HUD data model, combat log, breakdown payloads
- Anim timeline integration hooks + camera state machine hooks
- Testbed: Headless AI runs + deterministic choice tests

## Implementation Phases

### Phase 1: AI Decision Pipeline Core

**Objective:** Create the core AI architecture for generating, evaluating, and selecting actions.

**Files to Create:**
- `Combat/AI/AIAction.cs` - Represents a candidate action with score
- `Combat/AI/AIDecisionPipeline.cs` - Main decision-making orchestrator
- `Combat/AI/AIProfile.cs` - Personality/behavior configuration

**Key Classes:**
```csharp
public class AIAction
{
    public string ActionType { get; set; }      // move, attack, ability, end_turn
    public string AbilityId { get; set; }
    public string TargetId { get; set; }
    public Vector3? TargetPosition { get; set; }
    public float Score { get; set; }
    public Dictionary<string, float> ScoreBreakdown { get; set; }
}

public class AIDecisionPipeline
{
    List<AIAction> GenerateCandidates(Combatant actor, CombatContext context);
    void ScoreCandidates(List<AIAction> candidates, Combatant actor, AIProfile profile);
    AIAction SelectBest(List<AIAction> candidates);
}
```

**Tests:** AIDecisionPipelineTests.cs
- GenerateCandidates returns valid actions
- ScoreCandidates assigns finite scores
- SelectBest chooses highest scoring action

---

### Phase 2: AI Scoring System

**Objective:** Implement utility-based scoring for candidate actions.

**Files to Create:**
- `Combat/AI/AIScorer.cs` - Scoring logic for different action types
- `Combat/AI/AIWeights.cs` - Configurable weight values

**Key Scoring Factors:**
- Damage dealt (weighted by target priority)
- Kill potential (bonus for finishing targets)
- Status value (control effects worth more)
- Self-preservation (avoid danger)
- Resource efficiency (save limited-use abilities)
- Position value (high ground, cover)

**Tests:** AIScorerTests.cs
- Attack scoring based on damage and hit chance
- Movement scoring based on positioning
- Healing prioritizes low HP allies

---

### Phase 3: AI Tactical Movement

**Objective:** AI evaluates positions for threat, cover, height, and objective distance.

**Files to Create:**
- `Combat/AI/AIMovementEvaluator.cs` - Position evaluation
- `Combat/AI/ThreatMap.cs` - Tracks enemy threat zones

**Key Features:**
- Threat zone calculation (opportunity attack ranges)
- Cover seeking behavior
- High ground preference
- Distance-to-target optimization
- Disengage path finding

**Tests:** AIMovementEvaluatorTests.cs
- Prefers positions with cover over exposed
- Prefers high ground
- Avoids opportunity attack zones unless necessary

---

### Phase 4: AI Target Selection

**Objective:** AI selects optimal targets based on tactical evaluation.

**Files to Create:**
- `Combat/AI/AITargetEvaluator.cs` - Target priority scoring

**Key Factors:**
- Current HP (finish wounded)
- Threat level (damage potential)
- Role (prioritize healers/casters)
- Accessibility (can reach/hit)
- Friendly fire avoidance

**Tests:** AITargetEvaluatorTests.cs
- Prioritizes low HP targets
- Avoids friendly fire in AoE
- Targets accessible enemies over unreachable

---

### Phase 5: AI Reaction Policy

**Objective:** AI decides when to use reactions automatically.

**Files to Create:**
- `Combat/AI/AIReactionPolicy.cs` - Reaction decision logic

**Key Features:**
- Opportunity attack: always use vs fleeing enemies
- Counterspell-like: use vs high-value spells
- Defensive reactions: use when damage threshold met
- Save reaction toggle: hold for better opportunity

**Tests:** AIReactionPolicyTests.cs
- Uses opportunity attacks on fleeing enemies
- Holds reaction when no good triggers
- Uses defensive reaction when damage exceeds threshold

---

### Phase 6: Combat Log Enhancement

**Objective:** Enhance combat log with rich event data and filtering.

**Files to Modify:**
- `Combat/Services/CombatLog.cs` - Add structured event types

**Files to Create:**
- `Combat/Services/CombatLogEntry.cs` - Rich log entry model
- `Combat/Services/CombatLogFilter.cs` - Filtering capabilities

**Key Features:**
- Structured entries with type, source, target, values
- Roll breakdown attached to entries
- Timestamp and turn/round tracking
- Filter by type, combatant, severity
- Export to JSON/text

**Tests:** CombatLogEnhancementTests.cs
- Entries include breakdown data
- Filtering works correctly
- Export produces valid format

---

### Phase 7: Breakdown Payloads

**Objective:** Every roll and calculation exposes its component breakdown.

**Files to Create:**
- `Combat/Rules/BreakdownPayload.cs` - Detailed calculation breakdown

**Key Features:**
- Attack roll breakdown: base + modifiers + advantage
- Damage breakdown: base + bonuses + multipliers
- AC breakdown: base + armor + shield + cover + height
- Save DC breakdown: base + stat + proficiency

**Tests:** BreakdownPayloadTests.cs
- Attack breakdown includes all modifiers
- Damage breakdown shows vulnerability/resistance
- Breakdown is serializable

---

### Phase 8: HUD Data Model

**Objective:** Create UI-bindable data models for all HUD elements.

**Files to Create:**
- `Combat/UI/Models/TurnTrackerModel.cs` - Turn order display data
- `Combat/UI/Models/ActionBarModel.cs` - Available actions display
- `Combat/UI/Models/ResourceBarModel.cs` - HP/resources display
- `Combat/UI/Models/CombatantInspectModel.cs` - Detailed combatant view

**Key Features:**
- Observable properties for data binding
- Update events when state changes
- Disable reasons for unavailable actions
- Tooltip data structures

**Tests:** HUDDataModelTests.cs
- Models update when combat state changes
- Disable reasons populated correctly
- All required fields accessible

---

### Phase 9: Animation Timeline Hooks

**Objective:** Create action timeline system for animation integration.

**Files to Create:**
- `Combat/Animation/ActionTimeline.cs` - Timeline phases
- `Combat/Animation/TimelineMarker.cs` - Event markers
- `Combat/Animation/ActionTimelineService.cs` - Timeline execution

**Timeline Phases:**
- WindUp: Preparation before action
- Release: Action begins
- Impact: Effect applies
- Recovery: Action completes

**Tests:** ActionTimelineTests.cs
- Markers fire in correct order
- Impact marker triggers damage application
- Timeline can be skipped for instant mode

---

### Phase 10: Camera State Machine Hooks

**Objective:** Create camera focus system for combat events.

**Files to Create:**
- `Combat/Camera/CameraFocusRequest.cs` - Focus request model
- `Combat/Camera/CameraStateHooks.cs` - Integration hooks

**Key Features:**
- Focus on active combatant
- Focus on target during action
- Track projectiles (hook only)
- Return to tactical view
- Stable during reaction interrupts

**Tests:** CameraStateHooksTests.cs
- Focus requests emit for turn changes
- Action targeting emits focus request
- Reaction doesn't break camera focus

---

### Phase 11: Integration and AI Scenarios

**Objective:** Create test scenarios for AI decision making.

**Scenarios to Create:**
- `Data/Scenarios/ai_tactical_test.json` - AI movement and positioning
- `Data/Scenarios/ai_target_selection_test.json` - Target priority
- `Data/Scenarios/ai_reaction_test.json` - Reaction policy

**Integration Tests:**
- Full combat with AI making all decisions
- AI uses abilities appropriately
- AI reacts to threats

---

### Phase 12: Final Verification

**Objective:** Run CI, update documentation, create PHASE_D_GUIDE.md.

**Tasks:**
- Run `scripts/ci-build.sh`
- Run `scripts/ci-test.sh`
- Create `docs/PHASE_D_GUIDE.md`
- Update `READY_TO_START.md`
- Mark plan complete

---

## Open Questions

1. **AI Difficulty Levels?** Should AI have configurable difficulty (affects scoring weights)? Recommend: Yes, via AIProfile weights.

2. **Turn Time Budget?** Should AI have a time limit for decisions? Recommend: Yes, 500ms default with fallback to simple action.

3. **Debug Visualization?** Should AI expose its decision reasoning? Recommend: Yes, via structured logs and optional overlay data.
