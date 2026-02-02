# Phase F Implementation Guide: Presentation, Camera Hooks, and Benchmark Gating

## Overview

Phase F activates the existing presentation primitives (`ActionTimeline`, `TimelineMarker`, `CameraStateHooks`) and wires them into combat execution with full headless verification. This phase also turns benchmark scripts into real performance gates with regression detection.

## Objectives

### Timeline & Presentation ✅ COMPLETE
- [x] Enable existing timeline/camera unit tests
- [x] Create headless-verifiable presentation request surface
- [x] Wire `ActionTimeline` into ability execution
- [x] Camera focus integration via timeline markers
- [x] Data-driven VFX/SFX IDs (non-asset)

### Benchmark Gating ✅ COMPLETE
- [x] Make `scripts/ci-benchmark.sh` a real perf gate
- [x] Fail CI on performance regressions
- [x] Commit baseline for deterministic gating

## Architecture

### Presentation System

```
Combat/Animation/
├── ActionTimeline.cs          # Timeline scheduler + factory
├── TimelineMarker.cs          # Marker types (Sound/VFX/CameraFocus/CameraRelease)
└── (existing, currently dormant)

Combat/Camera/
├── CameraStateHooks.cs        # Camera request queue/state machine
├── CameraFocusRequest.cs      # Camera focus requests
└── (existing, requires Process(delta) driver)

Combat/Services/
├── PresentationRequest.cs     # DTO for VFX/SFX/camera intents (planned in Phase 3)
└── PresentationRequestBus.cs  # Event bus for presentation (planned in Phase 3)

Combat/Arena/
└── CombatArena.cs             # Integration point for timeline creation
```

### Benchmark Infrastructure

```
Tests/Performance/
├── CIBenchmarkGateTests.cs    # Baseline regression tests
├── BenchmarkReporter.cs       # Results formatting
└── CIBenchmarkRunner.cs       # Regression checking

benchmark-results/
└── baseline.json              # Committed baseline for gates
```

## Verification Strategy (Non-Visual)

Phase F explicitly prohibits "verify visually" requirements. All acceptance is via:

### 1. Timeline Marker Assertions
```csharp
// Start timeline, then tick forward headlessly
timeline.Play();
timeline.Process(delta);

// Assert markers fire in expected order
Assert.Equal(MarkerType.Start, firedMarkers[0].Type);
Assert.Equal(MarkerType.Hit, firedMarkers[1].Type);
Assert.Equal(MarkerType.AnimationEnd, firedMarkers[2].Type);
```

### 2. Presentation Request Events

*PSEUDOCODE - Phase 3+ planned API:*

```csharp
// Subscribe to request bus (planned)
var capturedRequests = new List<PresentationRequest>();
presentationBus.Subscribe(capturedRequests.Add);

// Execute ability
arena.ExecuteAbility(attacker, ability, target);

// Assert requests emitted
Assert.Contains(capturedRequests, r => r.Type == "VfxRequested");
Assert.Contains(capturedRequests, r => r.Type == "CameraFocus");
```

### 3. Camera State Transitions

*Note: Camera transitions from Transitioning→Focused over TransitionTime. Tests should either set TransitionTime=0 or assert on Transitioning state.*

```csharp
// PSEUDOCODE: Camera state machine processes requests
var request = CameraFocusRequest.FocusCombatant(targetId, duration: 1f, priority: CameraPriority.Normal);
request.TransitionTime = 0; // Instant for testing
cameraHooks.RequestFocus(request);
cameraHooks.Process(delta);

// Assert state without rendering
Assert.Equal(CameraState.Focused, cameraHooks.State);
Assert.Equal(targetId, cameraHooks.CurrentRequest.TargetId);
```

### 4. Benchmark Regression Detection
```bash
# CI benchmark exits non-zero on regression
./scripts/ci-benchmark.sh
# Returns 1 if any benchmark exceeds threshold
```

## Implementation Phases

### Phase 1: Documentation & Scope Definition ✅ COMPLETE
- [x] Create PHASE_F_GUIDE.md
- [x] Update READY_TO_START.md references
- [x] Fix AGENTS-MASTER-TO-DO.md broken references

### Phase 2: Enable Dormant Tests ✅ COMPLETE
**Files:**
- Tests/Unit/AnimationTimelineTests.cs.skip → .cs
- Tests/Unit/CameraStateTests.cs.skip → .cs

**Acceptance:**
- [x] Timeline tests pass in CI
- [x] Camera state tests pass in CI
- [x] No Godot runtime dependencies

### Phase 3: Presentation Request Layer ✅ COMPLETE
**New Files:**
- Combat/Services/PresentationRequest.cs
- Combat/Services/PresentationRequestBus.cs
- Tests/Unit/PresentationRequestBusTests.cs

**Acceptance:**
- [x] Bus publishes VFX/SFX/camera requests
- [x] Tests verify publish/subscribe determinism
- [x] No `res://` asset dependencies

### Phase 4: Timeline Integration ✅ COMPLETE
**Modified Files:**
- Combat/Arena/CombatArena.cs

**New Files:**
- Tests/Integration/AbilityPresentationTimelineIntegrationTests.cs

**Acceptance:**
- [x] Abilities create timelines based on metadata
- [x] Markers trigger at correct times
- [x] Damage/heal presentation scheduled at Hit marker
- [x] Tests assert marker ordering headlessly

### Phase 5: Camera Focus Integration ✅ COMPLETE
**Modified Files:**
- Combat/Arena/CombatArena.cs
- Combat/Camera/CameraStateHooks.cs (clarify semantics)

**New Files:**
- Tests/Integration/TimelineCameraIntegrationTests.cs

**Acceptance:**
- [x] CameraFocus/Release markers map to hooks
- [x] Camera state transitions validated in tests
- [x] Event callbacks fire in expected order

### Phase 6: Data-Driven VFX/SFX ✅ COMPLETE
**Modified Files:**
- Data/Abilities/sample_abilities.json
- Combat/Arena/CombatArena.cs (marker generation)

**New Files:**
- Tests/Integration/AbilityVfxSfxRequestTests.cs

**Acceptance:**
- [x] VFX/SFX IDs flow from data → markers → requests
- [x] Tests verify ID propagation

### Phase 7: Benchmark Gating ✅ COMPLETE
**Modified Files:**
- scripts/ci-benchmark.sh

**New Files:**
- Tests/Performance/CIBenchmarkGateTests.cs
- benchmark-results/baseline.json (committed)

**Acceptance:**
- [x] CI fails on performance regression
- [x] JSON results saved to benchmark-results/
- [x] Baseline policy documented

## API Reference

### Creating Timelines

*PSEUDOCODE - Phase 3+ planned API:*

```csharp
// In CombatArena.ExecuteAbility
var timeline = ActionTimeline.MeleeAttack(
    onHit: () => ApplyDamage(target, amount),
    hitTime: 0.3f,
    totalDuration: 0.6f
);

// Subscribe to markers
timeline.MarkerTriggered += (markerId, markerType) =>
{
    switch (markerType)
    {
        case MarkerType.Start:
            // Trigger attack animation
            break;
        case MarkerType.Hit:
            // Emit damage presentation request
            presentationBus.Publish(new { Type = "DamagePresentation", TargetId = target.Id, Amount = amount });
            break;
        case MarkerType.AnimationEnd:
            // Cleanup
            break;
    }
};

// Start and drive timeline
timeline.Play();
timeline.Process(deltaTime);
```

### Presentation Requests

*PSEUDOCODE - Phase 3+ planned types:*

```csharp
// Define request types (planned for Phase 3)
public record VfxPresentationRequest(string VfxId, Vector3 Position, float Scale);
public record SfxPresentationRequest(string SfxId, Vector3 Position, float Volume);
public record CameraActionRequest(string TargetId, int Priority);

// Publish requests (planned API)
presentationBus.Publish(new VfxPresentationRequest("fireball_impact", hitPos, 1.0f));
presentationBus.Publish(new SfxPresentationRequest("sword_hit", hitPos, 0.8f));
presentationBus.Publish(new CameraActionRequest(target.Id, priority: 1));
```

### Camera State Driving

```csharp
// In arena per-frame update (existing timelines already call Play() at creation)
public override void _Process(double delta)
{
    var dt = (float)delta;
    
    // Drive timelines
    foreach (var timeline in activeTimelines)
    {
        timeline.Process(dt);
    }
    
    // Drive camera state machine
    cameraHooks.Process(dt);
}
```

## Test Coverage

| Test Suite | Count | Status |
|------------|-------|--------|
| AnimationTimelineTests | 19 | ✅ Passing |
| CameraStateTests | 14 | ✅ Passing |
| PresentationRequestBusTests | 15 | ✅ Passing |
| AbilityPresentationTimelineIntegrationTests | 5 | ✅ Passing |
| TimelineCameraIntegrationTests | 7 | ✅ Passing |
| AbilityVfxSfxRequestTests | 4 | ✅ Passing |
| CIBenchmarkGateTests | 1 | ✅ Passing |
| **Total (New)** | **65** | ✅ |

## Design Decisions

### Presentation-Only Sequencing (Chosen)

**Decision:** Keep gameplay resolution immediate; delay only presentation.

**Rationale:**
- Maintains determinism
- Lower risk (no changes to core resolution)
- UI/state may show before visual impact (acceptable trade-off)

**Alternative:** Delay gameplay until Hit marker (deferred to future phase if needed)

### Baseline Policy

**Development branches:** Missing baseline warns but passes (Option B - soft)
**Release branches:** Missing baseline fails (Option A - strict)

**Rationale:**
- Easy onboarding during dev
- Strict gates before release

### Camera Release Semantics

**Decision:** Standardize on `CameraStateHooks.ReleaseFocus()` only

**Rationale:**
- Simpler API surface
- Avoids ambiguity with `CameraFocusRequest.Release()` method

**Alternative:** Fix semantics to make release requests work (deferred)

## CI Gates

All three scripts must pass before merging:

```bash
# Build gate
./scripts/ci-build.sh
# Returns 0 on success

# Test gate
./scripts/ci-test.sh
# Returns 0 if all tests pass

# Benchmark gate (NEW in Phase F)
./scripts/ci-benchmark.sh
# Returns 0 if no regressions detected
```

## Performance Targets

Phase F does not change existing performance targets from Phase E. Benchmark gate enforces:
- No regressions > 10% from baseline
- Manual approval required for intentional slowdowns

## Key Principles (CombatArena-First)

1. **Headless-Verifiable** - Assert via events/state, never "looks right"
2. **Deterministic** - Timeline markers fire at predictable times
3. **Presentation-Decoupled** - Gameplay resolution independent of visual timing
4. **No Asset Dependencies** - VFX/SFX IDs are strings; actual assets optional
5. **Regression-Gated** - Benchmarks fail CI on performance degradation

## Open Questions & Risks

### Q1: Should gameplay delay until Hit marker?
**Current:** No (presentation-only)
**Revisit:** If Phase F acceptance demands tighter "feel" coupling

### Q2: How to handle multiple concurrent timelines?
**Current:** Arena maintains active timeline list, processes all per-frame
**Risk:** Order-dependent marker triggers if timelines interact

### Q3: Camera priority conflicts?
**Current:** Higher priority wins; ties use insertion order
**Risk:** Undefined behavior if multiple max-priority requests

## Acceptance Criteria

Phase F is complete when:

- [x] All dormant tests re-enabled and passing
- [x] Abilities emit deterministic presentation markers (tests validate)
- [x] Camera focus/release flow from markers to state machine (tests validate)
- [x] VFX/SFX IDs flow from data through timeline to requests (tests validate)
- [x] `scripts/ci-benchmark.sh` fails on regressions
- [x] All new integration tests pass headlessly
- [x] `scripts/ci-build.sh`, `scripts/ci-test.sh`, `scripts/ci-benchmark.sh` all green

**Status:** ✅ COMPLETE - See [plans/phase-f-presentation-polish-benchmark-gating-complete.md](../plans/phase-f-presentation-polish-benchmark-gating-complete.md)

## Documentation Links

- **Master Plan**: [AGENTS-MASTER-TO-DO.md](../AGENTS-MASTER-TO-DO.md)
- **Implementation Plan**: [plans/phase-f-presentation-polish-benchmark-gating-plan.md](../plans/phase-f-presentation-polish-benchmark-gating-plan.md)
- **Phase A Guide**: [PHASE_A_GUIDE.md](PHASE_A_GUIDE.md)
- **Phase B Guide**: [PHASE_B_GUIDE.md](PHASE_B_GUIDE.md)
- **Phase C Guide**: [PHASE_C_GUIDE.md](PHASE_C_GUIDE.md)
- **Phase D Guide**: [PHASE_D_GUIDE.md](PHASE_D_GUIDE.md)
- **Phase E Guide**: [PHASE_E_GUIDE.md](PHASE_E_GUIDE.md)
