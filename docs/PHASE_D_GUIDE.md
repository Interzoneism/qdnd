# Phase D Implementation Guide: AI Parity and Polish

## Overview

Phase D brings the combat system to production-ready AI decision-making and presentation polish. This phase implements a utility-based AI system with archetype-driven behavior, enhanced combat logging with breakdown payloads, HUD data models, and animation/camera hooks.

## Objectives

- [x] AI Decision Pipeline (utility-based scoring, archetype profiles)
- [x] AI Movement Evaluation (threat maps, positioning)
- [x] AI Target Selection (focus fire, priority targeting)
- [x] AI Reaction Policy (opportunity attacks, defensive reactions)
- [x] Combat Log Enhancement (structured entries, filtering, export)
- [x] Breakdown Payloads (attack rolls, damage, saves)
- [x] HUD Data Models (turn tracker, action bar, resource bar)
- [x] Animation Timeline Hooks (markers, callbacks)
- [x] Camera State Machine (focus requests, slow motion)
- [x] Integration Tests (AI + log + breakdowns)

## Architecture

### AI System

```
AIDecisionPipeline
├── AIScorer (utility scoring)
├── AIMovementEvaluator (ThreatMap)
├── AITargetEvaluator (priority targeting)
└── AIReactionPolicy (opportunity attacks)

AIProfile
├── Archetypes: Aggressive, Defensive, Support, Controller, Tactical, Berserker
├── Difficulties: Easy, Normal, Hard, Nightmare
└── Weights: damage, kill_potential, self_preservation, positioning, healing, etc.
```

### Presentation Systems

```
Combat Logging:
CombatLog ─► CombatLogEntry ─► BreakdownPayload
         ─► CombatLogFilter (query/export)

HUD Models (Godot RefCounted with Signals):
├── TurnTrackerModel (turn order, active combatant, rounds)
├── ActionBarModel (cooldowns, charges, slots)
└── ResourceBarModel (health, mp, stamina)

Animation/Camera:
├── ActionTimeline (markers, callbacks, state machine)
└── CameraStateHooks (focus requests, priorities, slow motion)
```

## File Inventory

### AI System (`Combat/AI/`)

| File | Purpose |
|------|---------|
| `AIAction.cs` | Action candidates with type, target, ability data |
| `AIProfile.cs` | Archetype/difficulty configuration, weight system |
| `AIDecisionPipeline.cs` | Main decision-making orchestrator |
| `AIWeights.cs` | Weight categories and calculations |
| `AIScorer.cs` | Utility scoring for actions |
| `AIMovementEvaluator.cs` | Tactical positioning evaluation |
| `ThreatMap.cs` | Grid-based threat tracking |
| `AITargetEvaluator.cs` | Target prioritization |
| `AIReactionPolicy.cs` | Reaction opportunity evaluation |

### Presentation (`Combat/Services/`, `Combat/UI/`, `Combat/Animation/`, `Combat/Camera/`)

| File | Purpose |
|------|---------|
| `CombatLog.cs` | Enhanced logging with structured entries |
| `CombatLogEntry.cs` | Rich log entry with breakdown support |
| `CombatLogFilter.cs` | Query and filter log entries |
| `TurnTrackerModel.cs` | Turn order HUD data model |
| `ActionBarModel.cs` | Action bar with cooldowns |
| `ResourceBarModel.cs` | Health/resource bar model |
| `ActionTimeline.cs` | Animation timeline with markers |
| `TimelineMarker.cs` | Animation marker types |
| `CameraFocusRequest.cs` | Camera focus types and priorities |
| `CameraStateHooks.cs` | Camera state management |

### Breakdown System (`Combat/Rules/`)

| File | Purpose |
|------|---------|
| `BreakdownPayload.cs` | Structured calculation breakdowns |
| `BreakdownComponent.cs` | Individual modifier components |

### Tests

| File | Tests |
|------|-------|
| `Tests/Unit/AIScorerTests.cs` | Utility scoring |
| `Tests/Unit/AIMovementTests.cs` | Movement evaluation |
| `Tests/Unit/AITargetEvaluatorTests.cs` | Target prioritization |
| `Tests/Unit/AIReactionPolicyTests.cs` | Reaction decisions |
| `Tests/Unit/BreakdownPayloadTests.cs` | Breakdown calculations |
| `Tests/Unit/HUDModelTests.cs` | HUD model behavior |
| `Tests/Unit/AnimationTimelineTests.cs` | Timeline system |
| `Tests/Unit/CameraStateTests.cs` | Camera state machine |
| `Tests/Integration/AIIntegrationTests.cs` | Cross-system integration |

## AI Archetypes

| Archetype | Behavior | Key Weights |
|-----------|----------|-------------|
| Aggressive | Prioritize damage, focus fire | `damage: 1.5`, `kill_potential: 2.0` |
| Defensive | Prioritize survival, careful positioning | `self_preservation: 2.0`, `positioning: 1.2` |
| Support | Prioritize healing, buff allies | `healing: 2.0`, `status_value: 1.5` |
| Controller | Prioritize debuffs, crowd control | `status_value: 2.0` |
| Tactical | Balanced approach, adaptive | Default weights |
| Berserker | Maximum aggression, ignore self | `damage: 2.0`, `self_preservation: 0.1` |

## AI Difficulties

| Difficulty | Behavior |
|------------|----------|
| Easy | High randomness, no focus fire |
| Normal | Moderate randomness |
| Hard | Low randomness, focus fire enabled |
| Nightmare | No randomness, enhanced kill priority |

## Usage Examples

### Creating AI Profiles

```csharp
// Create a tactical profile on hard difficulty
var profile = AIProfile.CreateForArchetype(AIArchetype.Tactical, AIDifficulty.Hard);

// Custom weights
profile.Weights["healing"] = 1.8f;
profile.FocusFire = true;
```

### AI Decision Making

```csharp
var pipeline = new AIDecisionPipeline(context);
var decision = pipeline.MakeDecision(combatant, profile);

if (decision != null)
{
    // Execute the chosen action
    commandService.Execute(new UseAbilityCommand(decision.AbilityId, decision.TargetId));
}
```

### Combat Logging with Breakdowns

```csharp
// Create attack breakdown
var attack = BreakdownPayload.AttackRoll(15, 5, 18);
attack.Add("Bless", 1, "status");
attack.Calculate();

// Log with breakdown
log.LogAttack(source.Id, source.Name, target.Id, target.Name, attack.Success == true, attack.ToDictionary());

// Query specific entries
var damageEntries = log.GetEntries(CombatLogFilter.ForTypes(CombatLogEntryType.DamageDealt));

// Export
var json = log.ExportToJson();
var text = log.ExportToText();
```

### HUD Models (Godot Integration)

```csharp
// Turn tracker
var tracker = new TurnTrackerModel();
tracker.TurnOrderChanged += OnTurnOrderChanged;
tracker.SetTurnOrder(entries);
tracker.SetActiveCombatant(currentId);

// Action bar
var actionBar = new ActionBarModel();
actionBar.ActionStateChanged += OnActionStateChanged;
actionBar.UseAction("fireball");
actionBar.TickCooldowns();

// Resource bar
var resources = new ResourceBarModel();
resources.ResourceChanged += OnResourceChanged;
resources.SetResource("health", 50, 100);
```

### Camera Focus

```csharp
var camera = new CameraStateHooks();
camera.FocusChanged += OnCameraFocusChanged;

// Focus on combatant
camera.FollowCombatant(combatant.Id);

// Critical hit focus with slow motion
camera.RequestFocus(CameraFocusRequest.CriticalHit(target.Id));

// Process in game loop
camera.Process(delta);
```

## Testing Notes

⚠️ **Godot Runtime Required**: Tests using HUD models, Camera hooks, and Animation timelines extend Godot's `RefCounted` class and require the Godot runtime. These tests may crash the test host when run outside Godot.

**Safe to run independently:**
- `AIIntegrationTests` (pure C# integration tests)
- `BreakdownPayloadTests` (pure C# calculations)

**Require Godot runtime:**
- `HUDModelTests`
- `CameraStateTests`
- `AnimationTimelineTests`
- Tests using `Vector3` or other Godot types

## Verification

```bash
# CI Build (0 errors, 0 warnings)
./scripts/ci-build.sh

# Run safe integration tests
dotnet test Tests/QDND.Tests.csproj --filter "FullyQualifiedName~AIIntegrationTests"

# Run breakdown tests
dotnet test Tests/QDND.Tests.csproj --filter "FullyQualifiedName~BreakdownPayloadTests"
```

## Success Criteria

- [x] CI build passes with 0 errors
- [x] AI decision pipeline produces valid actions
- [x] All 6 archetypes behave distinctly
- [x] All 4 difficulty levels scale appropriately
- [x] Combat log exports to JSON and text
- [x] Breakdown payloads calculate correctly
- [x] HUD models emit proper signals
- [x] Camera focus system handles priorities
- [x] Integration tests verify cross-system behavior

## Next Steps

Phase D completes the core combat system. Future work may include:

1. **Visual Integration**: Connect HUD models to actual Godot UI scenes
2. **Animation Binding**: Wire ActionTimeline to actual animation players
3. **Camera Controller**: Implement actual camera node that responds to CameraStateHooks
4. **AI Tuning**: Balance archetype weights based on playtesting
5. **Scenario Testing**: Create complex combat scenarios for AI stress testing

## Notes for Agents

- AI weight tuning should be data-driven (consider external config)
- Breakdown payloads are designed for UI tooltip integration
- Camera slow motion needs actual TimeScale implementation in game
- HUD models use Godot signals - UI binds via Connect()
- Integration tests avoid Godot types to remain portable
