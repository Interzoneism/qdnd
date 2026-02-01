# Plan: Phase F — Presentation, Camera Hooks, and Benchmark Gating

**Created:** 2026-02-01
**Status:** Ready for Atlas Execution

## Summary

Phase E is marked complete and the repo is ready to move into Phase F (visual presentation, audio integration, polish/release prep). The core need is to activate the existing (currently dormant) presentation primitives (`ActionTimeline`, `TimelineMarker`, `CameraStateHooks`) and wire them into combat execution in a way that is fully verifiable headlessly (tests + Testbed), without requiring the Godot editor.

This plan also closes a confidence gap in CI by turning `scripts/ci-benchmark.sh` into a real perf gate using the already-present baseline regression infrastructure.

## Context & Analysis

**Relevant Files:**
- AGENTS-MASTER-TO-DO.md: Master rules (testbed-first) and parity checklist; appears partially out of sync with READY status.
- READY_TO_START.md: Declares Phase E complete; defines Phase F at a high level but points to master TODO for scope.
- Combat/Arena/CombatArena.cs: Current integration seam; executes gameplay and calls visuals immediately.
- Combat/Arena/CombatantVisual.cs: Current “animations” are tweens; suitable to trigger from markers.
- Combat/Animation/ActionTimeline.cs: Timeline scheduler + factory methods; currently used only by Tools/TimelineVerification.
- Combat/Animation/TimelineMarker.cs: Marker types include Sound/VFX/CameraFocus/CameraRelease.
- Combat/Camera/CameraStateHooks.cs + Combat/Camera/CameraFocusRequest.cs: Camera request queue/state machine; requires a per-frame `Process(delta)` driver.
- Tools/TimelineVerification.cs: Existing manual verification harness for timeline.
- Tests/Unit/AnimationTimelineTests.cs.skip and Tests/Unit/CameraStateTests.cs.skip: Prewritten tests currently disabled.
- scripts/ci-benchmark.sh + Tests/Performance/*: Baseline regression infra exists but script currently doesn’t gate.

**Key Functions/Classes:**
- `CombatArena.ExecuteAbility(...)` in Combat/Arena/CombatArena.cs: best place to create/play timelines and schedule presentation.
- `ActionTimeline` in Combat/Animation/ActionTimeline.cs: deterministic scheduler; headless testable via `Process(delta)`.
- `TimelineMarker` + `MarkerType` in Combat/Animation/TimelineMarker.cs: includes `Sound`, `VFX`, `CameraFocus`, `CameraRelease`.
- `CameraStateHooks.RequestFocus(...)` / `ReleaseFocus()` / `Process(delta)` in Combat/Camera/CameraStateHooks.cs: camera request processing.
- `CIBenchmarkRunner` + `BenchmarkReporter.CompareToBaseline(...)`: baseline regression logic that can be wired into CI.

**Dependencies:**
- .NET tests via `dotnet test` invoked by scripts.
- Godot editor is assumed unavailable; verification must be via tests/Testbed headless.

**Patterns & Conventions:**
- Testbed-first: no “verify visually”; assertions must be via logs/events/state.
- Many systems are “headless-friendly” and validate behavior via events/state rather than Godot scene graph.
- Avoid large churn in `.tscn` files; keep text-safe minimal edits.

## Implementation Phases

### Phase 1: Define Phase F scope + fix doc drift

**Objective:** Make Phase F scope authoritative and remove contradictions that can block shipping.

**Files to Modify/Create:**
- docs/PHASE_F_GUIDE.md (create): define Phase F “done” criteria, verification strategy, and checklists.
- READY_TO_START.md: link to PHASE_F_GUIDE.md and clarify scope ownership.
- AGENTS-MASTER-TO-DO.md: add Phase F section or adjust references; reconcile checkbox drift (only if safe/minimal).

**Tests to Write:**
- None (docs-only).

**Steps:**
1. Create `docs/PHASE_F_GUIDE.md` with (a) explicit non-visual verification requirements and (b) a scoped list: timeline markers, camera focus, VFX/SFX request mapping, optional UI bindings.
2. Update `READY_TO_START.md` Phase F section to point to the new guide.
3. If the master TODO is treated as a ship gate, add a minimal Phase F section and note that older checkboxes are historical (or update them to match reality).

**Acceptance Criteria:**
- [ ] Phase F has an explicit “done means” doc with verification steps.
- [ ] READY points to the correct Phase F guide.

---

### Phase 2: Enable existing timeline/camera unit tests (fast confidence)

**Objective:** Turn the dormant unit test suites for `ActionTimeline` and `CameraStateHooks` back on and get them passing in CI.

**Files to Modify/Create:**
- Tests/Unit/AnimationTimelineTests.cs.skip → Tests/Unit/AnimationTimelineTests.cs (rename)
- Tests/Unit/CameraStateTests.cs.skip → Tests/Unit/CameraStateTests.cs (rename)

**Tests to Write:**
- Extend/adjust those tests only as needed to reflect current behavior.

**Steps:**
1. Rename both `.skip` files to `.cs`.
2. Run `scripts/ci-test.sh` (expect failures initially).
3. Fix tests in-place to avoid any Godot runtime dependence (should remain pure C# / data-structure tests).
4. Run `scripts/ci-test.sh` until green.

**Acceptance Criteria:**
- [ ] Timeline tests pass in CI.
- [ ] Camera state tests pass in CI.

---

### Phase 3: Introduce a headless-verifiable “presentation request” surface

**Objective:** Create a minimal, testable interface for “presentation intents” (VFX/SFX/camera focus) that does not require actual Godot assets.

**Files to Modify/Create:**
- Combat/Services/ (new): `PresentationRequest.cs` (DTO) and `PresentationRequestBus.cs` (or equivalent)
- Combat/Arena/CombatArena.cs: instantiate and publish requests during combat execution.

**Tests to Write:**
- Tests/Unit/PresentationRequestBusTests.cs (new): subscribes, captures requests, asserts ordering.

**Steps:**
1. Add a small bus/collector that publishes requests like `SfxRequested`, `VfxRequested`, `CameraFocusRequested`, `CameraReleaseRequested` with correlation IDs.
2. Add unit tests verifying publish/subscribe and determinism.

**Acceptance Criteria:**
- [ ] Presentation intent emission is testable headlessly.
- [ ] No dependency on `res://` asset loading.

---

### Phase 4: Wire `ActionTimeline` into ability execution (presentation-only)

**Objective:** Use `ActionTimeline` to sequence presentation (tweens, damage popups, camera) while keeping gameplay resolution deterministic and immediate.

**Files to Modify/Create:**
- Combat/Arena/CombatArena.cs: create an `ActionTimeline` for `ExecuteAbility` and drive it over time.
- Combat/Animation/ActionTimeline.cs (optional): add a richer marker event payload OR keep marker callbacks and use closure capture.
- Combat/Arena/CombatantVisual.cs: ensure current tween methods can be triggered at marker callbacks (no functional changes expected).

**Tests to Write:**
- Tests/Integration/AbilityPresentationTimelineIntegrationTests.cs (new):
  - Executes an ability in a headless harness.
  - Asserts the bus emits `CameraFocus`/`VFX`/`SFX` and that damage/heal visuals are scheduled on the `Hit` marker.

**Steps:**
1. In `CombatArena.ExecuteAbility`, build a timeline using existing factories (`MeleeAttack`, `RangedAttack`, `SpellCast`) based on ability metadata.
2. Subscribe to marker triggers and schedule:
   - `Start`: attacker attack tween.
   - `Hit`: target hit tween + emit damage/heal “presentation requests” for each effect result.
   - `AnimationEnd`: cleanup.
3. Add an arena-level timeline runner that calls `timeline.Process(delta)` (and handles multiple concurrent timelines if needed).
4. Add tests that tick the timeline forward (no scene tree required) and assert marker ordering and emitted requests.

**Acceptance Criteria:**
- [ ] Abilities emit deterministic presentation markers in tests.
- [ ] Damage/heal presentation occurs at `Hit` marker time (even if gameplay state changes earlier).

---

### Phase 5: Camera focus integration (timeline-driven)

**Objective:** Connect `TimelineMarker.CameraFocus`/`CameraRelease` to `CameraStateHooks` via the presentation request layer.

**Files to Modify/Create:**
- Combat/Camera/CameraStateHooks.cs (optional): clarify/guard `CameraFocusRequest.Release()` semantics or ensure code uses `ReleaseFocus()`.
- Combat/Arena/CombatArena.cs: map camera markers to hook requests.
- Tests/Integration/TimelineCameraIntegrationTests.cs (new): timeline ticks → camera state transitions asserted.

**Tests to Write:**
- Timeline+camera integration test that:
  - creates a timeline with focus/release markers,
  - processes it over time,
  - asserts `CameraStateHooks.State` and the event callbacks fire in the expected order.

**Steps:**
1. Prefer marker callbacks or markerId→marker lookup to access marker payload (`TargetId`, `Position`).
2. Convert markers into `CameraFocusRequest`:
   - if `TargetId` is null, define a convention (e.g., focus attacker).
   - on `CameraRelease`, call `ReleaseFocus()` (avoid `CameraFocusRequest.Release()` unless fixed).
3. Drive `CameraStateHooks.Process(delta)` alongside the timeline runner.

**Acceptance Criteria:**
- [ ] Camera focus/release requests are emitted deterministically.
- [ ] Unit/integration tests validate queueing/priority behavior.

---

### Phase 6: Data-driven VFX/SFX IDs (non-asset)

**Objective:** Start using existing `AbilityDefinition.{VfxId,SfxId,AnimationId}` fields to produce requests/markers, without requiring real asset loading.

**Files to Modify/Create:**
- Combat/Abilities/AbilityDefinition.cs: confirm fields are serialized/loaded (already present).
- Data/Abilities/sample_abilities.json: add a couple representative `vfxId`/`sfxId` values.
- Combat/Arena/CombatArena.cs (or builder utility): map these IDs into `MarkerType.VFX` and `MarkerType.Sound` markers.

**Tests to Write:**
- Tests/Integration/AbilityVfxSfxRequestTests.cs (new): ability with ids → corresponding requests emitted.

**Steps:**
1. Update sample ability JSON with ids.
2. Ensure DataRegistry loads them.
3. Add marker generation that emits `VfxRequested/SfxRequested` at consistent times (windup/hit).

**Acceptance Criteria:**
- [ ] VFX/SFX IDs flow from data → timeline markers → emitted requests.

---

### Phase 7: Make `scripts/ci-benchmark.sh` a real perf gate

**Objective:** CI benchmarks must fail on regressions (relative to a committed baseline) or meet explicit targets.

**Files to Modify/Create:**
- scripts/ci-benchmark.sh: update to run the correct gating test and exit non-zero on failure.
- Tests/Performance/ (new): add a dedicated `CIBenchmarkGateTests` that uses repo output dir and baseline compare.
- benchmark-results/baseline.json (optional but recommended): commit a baseline for deterministic gating.

**Tests to Write:**
- Tests/Performance/CIBenchmarkGateTests.cs (new):
  - runs `CIBenchmarkRunner.RunWithRegressionCheck()` against `QDND_BENCH_OUTPUT_DIR`.
  - saves current JSON results.
  - fails if regressions exceed threshold (decide baseline-missing behavior).

**Steps:**
1. Add gate test that writes JSON into `benchmark-results/` and asserts no regressions vs baseline.
2. Update `scripts/ci-benchmark.sh` to run only the gate test filter and stop printing the “manual baseline” stub.
3. Decide and document baseline policy:
   - Option A: Missing baseline fails CI (strict).
   - Option B: Missing baseline passes but warns (soft).
4. Run `scripts/ci-benchmark.sh` to verify behavior.

**Acceptance Criteria:**
- [ ] CI benchmark script fails on regressions.
- [ ] JSON results are produced as artifacts in `benchmark-results/`.

## Open Questions

1. Should gameplay effect application be delayed until the `Hit` marker, or is presentation-only sequencing acceptable for Phase F?
   - **Option A (presentation-only):** simpler; maintains determinism; may show UI/state changes before impact.
   - **Option B (delay gameplay):** closer to “feel” accuracy; higher risk (touches core resolution, reactions, interrupts).
   - **Recommendation:** Start with presentation-only; revisit delaying gameplay only if Phase F acceptance demands it.

2. Baseline policy for benchmarks?
   - **Option A:** Missing baseline fails (forces a committed baseline; strongest gate).
   - **Option B:** Missing baseline passes (easier onboarding; weaker gate).
   - **Recommendation:** Option A for release branches; Option B for early dev, but document both.

3. Camera “release” semantics: fix `CameraFocusRequest.Release()` or forbid it?
   - **Option A:** Fix semantics so a “release request” actually releases.
   - **Option B:** Standardize on `CameraStateHooks.ReleaseFocus()` only.
   - **Recommendation:** Option B short-term; Option A if API is intended for general use.

## Risks & Mitigation

- **Risk:** Enabling `.skip` tests reveals hidden CI/runtime issues.
  - **Mitigation:** Keep tests pure C#; avoid Node/SceneTree use; adjust initialization patterns.

- **Risk:** Status tick handling is applied by consumers (arena/testbed); new subscribers may double-apply.
  - **Mitigation:** Keep timeline/presentation decoupled from tick application; assert no gameplay mutations occur in presentation layer.

- **Risk:** `.tscn` edits cause large diffs.
  - **Mitigation:** Prefer wiring runners in C# without scene edits; if needed, keep the minimal node/script attach only.

## Success Criteria

- [ ] Phase F scope documented and referenced correctly.
- [ ] Timeline + camera tests enabled and passing.
- [ ] Ability execution emits deterministic presentation requests (VFX/SFX/camera) with headless verification.
- [ ] `scripts/ci-build.sh`, `scripts/ci-test.sh`, and `scripts/ci-benchmark.sh` all pass.

## Notes for Atlas

- Follow repo build gates: run `scripts/ci-build.sh` and `scripts/ci-test.sh` before declaring done; include `scripts/ci-benchmark.sh` once Phase 7 is implemented.
- Prefer minimal diffs and avoid broad formatting changes.
- Avoid requiring the Godot editor; keep verification headless via tests/Testbed.
