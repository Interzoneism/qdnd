# Plan: Align Documentation + Entrypoint to CombatArena (Remove Testbed)

**Created:** 2026-02-02
**Status:** Ready for Atlas Execution

## Summary

The repo has drift between documentation that mandates `Testbed.tscn` as the integration scene and the actual Godot boot configuration, which now uses `CombatArena.tscn` as `run/main_scene`. This plan (1) updates the authoritative docs to match the real entrypoint and current phase completion (notably Phase F), and (2) removes the legacy `Testbed.tscn`/`TestbedBootstrap` by migrating any remaining “headless harness” behavior into the `CombatArena` pipeline.

## Context & Analysis

**Key Findings:**
- `project.godot` boots `res://Combat/Arena/CombatArena.tscn`.
- A legacy headless harness still exists: `Scripts/Tools/Testbed.tscn` + `Scripts/Tools/TestbedBootstrap.cs`.
- Authoritative docs still claim “Testbed-first” and instruct running Testbed.
- Phase F is marked “ready to start” in `READY_TO_START.md` and checkboxes are unchecked in `docs/PHASE_F_GUIDE.md`, but the repo contains a Phase F completion record (`plans/phase-f-presentation-polish-benchmark-gating-complete.md`) and Phase F code is already wired into `CombatArena.cs`.
- CI (`scripts/ci-test.sh`) is dotnet-only; it does **not** run Godot headless.

**Relevant Files (authoritative docs):**
- `AGENTS-MASTER-TO-DO.md`: Replace “Testbed-first rule” with “CombatArena-first rule”, update Phase F status, and update the one-line agent rule.
- `READY_TO_START.md`: Update “running the testbed” guidance to `CombatArena.tscn`, update phase statuses to include Phase F complete.
- `docs/PHASE_A_GUIDE.md`, `docs/PHASE_B_GUIDE.md`, `docs/PHASE_C_GUIDE.md`: Replace Testbed references with CombatArena integration.
- `docs/PHASE_F_GUIDE.md`: Mark Phase F complete and reconcile checkbox/status drift.
- `agents/*.md`: Replace `scenes/testbeds/...` guidance with `Combat/Arena/CombatArena.tscn`.
- `Data/Scenarios/README.md`, `Tests/README.md`: Remove Testbed framing and document the real verification paths.

**Relevant Files (runtime / scenes):**
- `project.godot`: `run/main_scene` already points to `Combat/Arena/CombatArena.tscn`.
- `Combat/Arena/CombatArena.tscn` + `Combat/Arena/CombatArena.cs`: Current integration scene and primary wiring.
- `Scripts/Tools/Testbed.tscn` + `Scripts/Tools/TestbedBootstrap.cs`: Legacy harness to be removed.

**Patterns / Conventions:**
- “No visual verification” is enforced via dotnet tests and simulation scenarios (`Tests/Simulation/*`), not by running Godot.
- Scene/resource edits must be text-safe and minimal.

## Implementation Phases

### Phase 1: Establish Baseline + Inventory (No Code Changes)

**Objective:** Ensure we understand the current working state and prevent regressions during doc + entrypoint cleanup.

**Steps:**
1. Run `./scripts/ci-build.sh`.
2. Run `./scripts/ci-test.sh`.
3. Run `./scripts/ci-benchmark.sh` (Phase F claims benchmark gating is live).
4. Run a repo-wide search for `Testbed.tscn`, `TestbedBootstrap`, and `scenes/testbeds` to confirm the full change surface.

**Acceptance Criteria:**
- [ ] All existing CI gates pass (or failures are documented as pre-existing and kept out of scope).
- [ ] A list of “authoritative docs to update” is confirmed.

---

### Phase 2: Update Authoritative Docs to CombatArena

**Objective:** Make documentation match reality so subsequent agents do not build on incorrect assumptions.

**Files to Modify:**
- `AGENTS-MASTER-TO-DO.md`
- `READY_TO_START.md`
- `docs/PHASE_A_GUIDE.md`
- `docs/PHASE_B_GUIDE.md`
- `docs/PHASE_C_GUIDE.md`
- `docs/PHASE_F_GUIDE.md`
- `agents/03-gameplay-systems.md`
- `agents/04-ai-agent.md`
- `agents/06-tools-devex.md`
- `Data/Scenarios/README.md`
- `Tests/README.md`

**Edits (specific):**
1. Replace “Testbed-first rule” language with “CombatArena-first rule” language.
   - Define `CombatArena.tscn` as the *single always-current integration scene*.
   - Replace “included in Testbed” with “included in CombatArena”.
   - Preserve the **non-visual verification contract**: features must be verifiable via tests/logs/state hashes.
2. Update the “default boot scene in dev” rule to match `project.godot` (CombatArena is already main).
3. Replace “RunScenario(seed)” and “ready event/log” requirements previously attributed to Testbed with equivalent language:
   - **Preferred**: describe dotnet-first verification (`Tests/Simulation/ScenarioRegressionTests`) as the authoritative headless contract.
   - **Optional**: if we keep an in-Godot harness, it must be attached to CombatArena (not a separate Testbed scene).
4. Update Phase F status everywhere:
   - `READY_TO_START.md`: Phase F ✅ COMPLETE
   - `AGENTS-MASTER-TO-DO.md`: Phase F section should reflect completion (and link to the completion doc).
   - `docs/PHASE_F_GUIDE.md`: Mark objectives/phases complete and remove “planned” language that contradicts the current codebase.
5. Update agent-facing paths:
   - Replace `scenes/testbeds/...` references with `Combat/Arena/CombatArena.tscn`.
6. Update “How to run”:
   - “Human run”: open `Combat/Arena/CombatArena.tscn`.
   - “Automated verification”: `./scripts/ci-build.sh`, `./scripts/ci-test.sh`, `./scripts/ci-benchmark.sh`.

**Acceptance Criteria:**
- [ ] No authoritative docs instruct opening or wiring `Testbed.tscn`.
- [ ] Phase F is consistently marked complete across status docs.
- [ ] Paths in agent docs match actual repo locations.

---

### Phase 3: Migrate/Replace Testbed Harness Behavior into CombatArena

**Objective:** Remove `Testbed.tscn` while retaining any useful deterministic harness features.

**Implementation Options (choose one):**

**Option A (recommended): Add a headless harness component to CombatArena**
- Create a new script like `Combat/Arena/CombatArenaHarness.cs` (or similar) that provides the behaviors currently in `TestbedBootstrap`:
  - scenario selection
  - deterministic autorun for N turns
  - deterministic final state hash output
  - optional “quit on complete”
- Make it togglable via exported flags so it can be used in the editor for quick checks.

**Option B: Keep harness as a separate scene but not named Testbed**
- Create `Combat/Arena/CombatArenaHeadless.tscn` as a minimal scene which instantiates `CombatArena` with harness enabled.
- This satisfies “remove Testbed” while keeping an explicit harness entrypoint.

**Required regardless of option:**
1. Extract duplicated service registration logic between `TestbedBootstrap.Register*` and `CombatArena.RegisterServices()` into a shared helper (e.g., `Combat/Services/CombatContextBootstrap.cs`).
   - This prevents drift and ensures the arena has the same deterministic setup as any harness.
2. Delete legacy files after migration:
   - `Scripts/Tools/Testbed.tscn`
   - `Scripts/Tools/TestbedBootstrap.cs`
3. Confirm nothing references those files (code, docs, tests, or scene includes).

**Tests to Add/Update (only if harness is kept):**
- Add/extend an integration test that validates the deterministic “final hash” behavior is still reachable via the new harness API (without requiring Godot runtime).
  - If the hash computation is Godot-free already, keep it in pure C# and test it there.

**Acceptance Criteria:**
- [ ] `Scripts/Tools/Testbed.tscn` and `Scripts/Tools/TestbedBootstrap.cs` are removed.
- [ ] CombatArena remains the `project.godot` main scene.
- [ ] Any harness capability formerly in Testbed is available via CombatArena (or `CombatArenaHeadless.tscn`).

---

### Phase 4: Verification + Drift Guard

**Objective:** Ensure the rename/removal didn’t break build/test gates and prevent future doc drift.

**Steps:**
1. Run `./scripts/ci-build.sh`.
2. Run `./scripts/ci-test.sh`.
3. Run `./scripts/ci-benchmark.sh`.
4. Add a lightweight “doc drift guard” (optional): a test or CI grep check that fails if `Testbed.tscn` reappears in authoritative docs.

**Acceptance Criteria:**
- [ ] All CI gates pass.
- [ ] No references to `Testbed.tscn` remain in authoritative docs.

## Open Questions

1. Should we update historical plan/completion docs that mention Testbed?
   - **Option A:** Leave them as historical (recommended to minimize churn). Add a short note in `READY_TO_START.md` that older plans mention “Testbed” as legacy terminology.
   - **Option B:** Sweep-update all markdown files to remove “Testbed” (higher churn, risk of noisy diffs).
   - **Recommendation:** Option A; prioritize correctness for authoritative docs and keep historical records intact.

2. Do we need a Godot headless run path in CI?
   - **Option A:** Keep CI dotnet-only (current). Rely on `Tests/Simulation/*` for headless validation.
   - **Option B:** Add Godot `--headless` job (bigger infra change, may not be available in CI environment).
   - **Recommendation:** Option A; document clearly that “headless verification” == dotnet simulation tests.

## Risks & Mitigation

- **Risk:** Large diffs in `AGENTS-MASTER-TO-DO.md` due to many “Testbed:” callouts.
  - **Mitigation:** Apply minimal wording changes; avoid restructuring; mechanical rename of labels only.

- **Risk:** Removing Testbed breaks an implicit manual workflow.
  - **Mitigation:** Provide a clear `CombatArena` harness toggle or a minimal `CombatArenaHeadless.tscn` entrypoint.

- **Risk:** Shared bootstrap refactor introduces service registration regressions.
  - **Mitigation:** Refactor behind identical behavior; verify via CI gates; avoid changing registration order unless required.

## Success Criteria

- [ ] Authoritative docs consistently describe `CombatArena.tscn` as the integration scene.
- [ ] Phase F completion status is reflected everywhere.
- [ ] Legacy `Testbed.tscn` and `TestbedBootstrap` are removed from the repo.
- [ ] `./scripts/ci-build.sh`, `./scripts/ci-test.sh`, and `./scripts/ci-benchmark.sh` pass.

## Notes for Atlas

- Keep diffs minimal; avoid reformatting Markdown or reorganizing sections.
- Prefer editing only authoritative docs first; treat older `plans/*` files as historical unless the user explicitly wants a repo-wide rename.
- When deleting `Testbed` artifacts, confirm no `.tscn` or `.csproj` references remain.
