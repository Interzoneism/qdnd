# Plan: Fix Floor-Only Launch (JSON Loading + Scenario Validation)

**Created:** 2026-02-01
**Status:** Ready for Atlas Execution

## Summary

The game launches showing only the floor because `CombatArena` initializes `DataRegistry` and then calls `ValidateOrThrow()`, which throws due to 9 scenario files using an older/alternate JSON schema (`combatants`/`team`) that doesn’t populate `ScenarioDefinition.Units`. The registry also reports ability/status JSON enum conversion failures in the provided log, which can result in 0 abilities/statuses loaded.

This plan fixes the launch by preventing incompatible scenario JSON from breaking runtime validation, hardens JSON loading (especially enum parsing) to avoid “all-or-nothing” pack failures, and (optionally but recommended) makes data loading export-safe by supporting `res://` enumeration and reads.

## Context & Analysis

**Observed log symptoms**
- Abilities/statuses: enum conversion errors, resulting in `0 abilities` and `0 statuses` loaded.
- Scenarios: 15 loaded, but 9 validation errors “No units defined”.
- `ValidateOrThrow()` throws, halting initialization before combatants are spawned → visually only the arena floor appears.

**Root cause (floor-only)**
- 9 files under `Data/Scenarios/*.json` contain a `combatants` array (older/test schema). Current runtime schema expects `ScenarioDefinition.Units`.
- These files deserialize into `ScenarioDefinition` with an empty `Units` list and then fail validation.

**Secondary risk (export builds)**
- `DataRegistry` uses `System.IO.Directory.GetFiles` + `File.ReadAllText`, which is not compatible with packaged `res://` data in Godot exports. `ScenarioLoader` already demonstrates the correct `FileAccess` approach.

**Relevant Files**
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): calls `_dataRegistry.LoadFromDirectory(...)` and `_dataRegistry.ValidateOrThrow()` during `_Ready()`.
- [Data/DataRegistry.cs](Data/DataRegistry.cs): loads abilities/statuses/scenarios; validates and throws.
- [Data/ScenarioLoader.cs](Data/ScenarioLoader.cs): defines `ScenarioDefinition` (runtime schema) and uses `Godot.FileAccess` for `res://` reads.
- Data content:
  - [Data/Abilities/sample_abilities.json](Data/Abilities/sample_abilities.json)
  - [Data/Statuses/sample_statuses.json](Data/Statuses/sample_statuses.json)
  - Scenario schema mismatch set (9 files): `height_los_test.json`, `movement_test.json`, `reaction_test.json`, `surface_test.json`, `test_ai_decisions.json`, `test_reaction_chain.json`, `test_save_load.json`, `test_status_tick.json`, `test_surface_transform.json`
- Tests:
  - [Tests/Unit/DataRegistryTests.cs](Tests/Unit/DataRegistryTests.cs) (validation expectations for scenarios with no units)

## Implementation Phases

### Phase 1: Add a Repro Test for “Repo Data Loads Cleanly”

**Objective:** Make the floor-only regression reproducible in CI by asserting that the repository’s `Data/` folder loads and validates without throwing.

**Files to Modify/Create:**
- Create `Tests/Unit/DataRegistryRepoDataSmokeTests.cs`

**Tests to Write:**
- `DataRegistry_LoadFromRepoData_DoesNotThrowAndLoadsCoreContent`
  - Locates repo root by walking up from `AppContext.BaseDirectory` until `project.godot` is found.
  - Calls `registry.LoadFromDirectory(<repoRoot>/Data)`.
  - Asserts: `registry.GetAllScenarios().Count > 0` and `registry.ValidateOrThrow()` does not throw.
  - Optionally asserts: abilities/statuses are non-zero (or at least that load failures are reported explicitly; see Phase 3).

**Steps:**
1. Write the new test (red).
2. Run `scripts/ci-test.sh` (should fail today due to 9 scenario validation errors).

**Acceptance Criteria:**
- [ ] New test fails before the fix and will pass after Phase 2.

---

### Phase 2: Prevent Incompatible Scenario JSON from Breaking Runtime Validation

**Objective:** Stop the 9 `combatants`-schema scenario files from being registered as runtime `ScenarioDefinition` objects (or otherwise exempt them from “must have units” validation).

**Recommended approach (minimal churn, keeps validation semantics):**
- In `DataRegistry.LoadScenarioFromFile`, inspect the JSON root before deserializing:
  - If the root contains `units`, proceed with `ScenarioDefinition` deserialization and registration.
  - Else if it contains `combatants` (or lacks `units`), skip registering this scenario and log an info message explaining it’s a non-runtime/test schema file.

**Files to Modify/Create:**
- [Data/DataRegistry.cs](Data/DataRegistry.cs)

**Implementation Notes:**
- Use `JsonDocument.Parse(json)` and `TryGetProperty("units", out ...)`.
- When skipping, log something like:
  - `[Registry] Skipping scenario file (no 'units' field; likely test schema): {path}`
- Keep scenario validation as-is (still errors for registered runtime scenarios that truly have no units).

**Steps:**
1. Implement JSON pre-scan + skip behavior.
2. Run `scripts/ci-test.sh` (Phase 1 smoke test should turn green).

**Acceptance Criteria:**
- [ ] `DataRegistry.LoadFromDirectory(Data)` no longer registers the 9 `combatants`-schema scenario files.
- [ ] `registry.ValidateOrThrow()` does not throw when loading repo `Data/`.
- [ ] Godot launch no longer halts during registry validation; `CombatArena` proceeds to spawn combatants.

**Alternative approaches (documented, not recommended unless desired):**
- Migrate the 9 scenario JSON files to the new schema (`units`/`faction` + initiative fields), possibly moving test-only fields to a separate file/type.
- Downgrade “No units defined” from error to warning globally (risk: hides real content errors).

---

### Phase 3: Harden Ability/Status JSON Loading (Enum Compatibility + Partial Pack Loading)

**Objective:** Fix the reported enum conversion errors and prevent a single bad entry from zeroing the entire pack.

**Files to Modify/Create:**
- [Data/DataRegistry.cs](Data/DataRegistry.cs)
- Create `Data/Json/LenientEnumConverterFactory.cs` (or similar utility location consistent with repo layout)
- Create additional tests in `Tests/Unit/`.

**Implementation Option A (recommended): Per-entry load with good diagnostics**
- For abilities:
  - Parse file with `JsonDocument`, locate `abilities` array, and attempt to deserialize each element independently using shared `JsonSerializerOptions`.
  - On `JsonException`, skip that entry and log an error with a best-effort identifier.
- Same pattern for statuses.

**Implementation Option B (recommended in addition): Lenient enum converter**
- Add a `JsonConverterFactory` for enums that:
  - Accepts camelCase, PascalCase, snake_case, kebab-case by normalizing to a canonical form.
  - Uses `Enum.TryParse(..., ignoreCase: true)` after normalization.
  - (Optional) supports alias mapping if tools historically emitted values like `single` → `SingleUnit`.

**Tests to Write:**
- `DataRegistry_LoadsSampleAbilitiesJson`
- `DataRegistry_LoadsSampleStatusesJson`
- `DataRegistry_EnumParsing_AcceptsSnakeCaseAliases` (if lenient converter is implemented)

**Steps:**
1. Add tests asserting the sample JSON loads and yields non-zero counts.
2. Implement shared `JsonSerializerOptions` used consistently by all loads.
3. Implement per-entry deserialization and/or lenient enum converter.
4. Run `scripts/ci-test.sh`.

**Acceptance Criteria:**
- [ ] No startup log errors for `TargetType` / `DurationType` conversion when using repo sample JSON.
- [ ] A single bad ability/status entry does not prevent others from loading.
- [ ] Tests demonstrate the intended compatibility behavior.

---

### Phase 4 (Recommended): Make DataRegistry Export-Safe for `res://` Data

**Objective:** Ensure data loads in exported builds (where `System.IO.Directory.GetFiles` cannot enumerate `res://` packaged content).

**Approach (low-risk): add an explicit `res://` codepath**
- Add `DataRegistry.LoadFromResDirectory(string resBasePath = "res://Data")` that:
  - Enumerates `res://Data/Abilities`, `res://Data/Statuses`, `res://Data/Scenarios` using `DirAccess`.
  - Reads files using `FileAccess`.
- Keep existing `LoadFromDirectory(string basePath)` for OS filesystem paths (tests/tools).
- Update runtime call sites:
  - [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): call `LoadFromResDirectory()` (stop `GlobalizePath`).
  - [Scripts/Tools/TestbedBootstrap.cs](Scripts/Tools/TestbedBootstrap.cs): same.

**Tests to Write:**
- None required if Godot runtime isn’t available in CI; rely on unit tests for OS-path loading.

**Acceptance Criteria:**
- [ ] Runtime scenes use `res://` loading for data.
- [ ] Editor run still works.
- [ ] Export run loads data successfully (manual verification step).

---

### Phase 5: Developer Experience & Documentation

**Objective:** Make it hard to regress and easy to understand.

**Files to Modify/Create:**
- Add a brief note to `docs/` (or `READY_TO_START.md`) describing:
  - Runtime scenario schema (`ScenarioDefinition` expects `units`).
  - Test-schema scenario files and how they’re handled (skipped by registry / stored separately).

**Acceptance Criteria:**
- [ ] Clear guidance on where runtime scenarios live and required fields.

## Open Questions

1. Should the 9 `combatants`-schema files be migrated to runtime schema instead of skipped?
   - **Option A (Skip):** Minimal change; keeps them as “test-only content”.
   - **Option B (Migrate):** More churn but unifies scenario format; requires mapping `team`→`faction` and adding initiative fields.
   - **Recommendation:** Skip for runtime now (Phase 2), consider migration later.

2. How strict should enum parsing be?
   - **Option A (Strict):** Fail entry with clear diagnostics.
   - **Option B (Lenient):** Accept historical formats (snake_case, aliases) and validate canonicalization.
   - **Recommendation:** Per-entry strict + lenient enum normalization (Phase 3) to support tool-written JSON.

3. Do we need export-safe loading immediately?
   - **Recommendation:** Yes—Phase 4 is the safest way to avoid “works in editor, fails in export.”

## Risks & Mitigation

- **Risk:** Skipping scenario files surprises tests or tools.
  - **Mitigation:** Log explicit skip reasons and keep runtime schema validation strict for registered scenarios.

- **Risk:** Lenient enum parsing could hide typos.
  - **Mitigation:** Only accept normalization/aliases you explicitly support; log warnings when normalization occurs.

- **Risk:** `DirAccess` enumeration differs in exports.
  - **Mitigation:** If enumeration is unreliable, switch to a manifest-based approach (single `manifest.json`).

## Success Criteria

- [ ] Godot launch shows combatants, not just floor.
- [ ] Registry no longer throws due to test-schema scenario files.
- [ ] Abilities/statuses load reliably (no enum conversion errors for repo data).
- [ ] `scripts/ci-build.sh` passes.
- [ ] `scripts/ci-test.sh` passes.

## Notes for Atlas

- Keep diffs minimal; avoid mass reformatting JSON.
- Implement Phase 2 first to restore editor launch quickly.
- Prefer adding a single smoke test (Phase 1) to prevent regressions.
- If you implement Phase 4, keep `LoadFromDirectory` unchanged to avoid breaking unit tests.
