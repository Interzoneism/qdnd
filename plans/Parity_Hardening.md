# Combat Parity Hardening Plan (Adversarial, Critical+Major, Meter Canonical)

## Summary
This plan fixes all confirmed critical and major BG3-combat parity gaps by hardening the reaction engine, unifying distance/movement to meters, correcting rule behavior (Frozen/autocrit/initiative), closing passive/action coverage gaps, and shipping automatic data migration with compatibility aliases.  
Primary objective: remove exploitable runtime inconsistencies first, then enforce parity correctness and prevent regressions in CI.

## Adversarial Findings To Fix
1. Critical: A single reactor can consume multiple reactions on one trigger due to resolver iteration logic.
2. Critical: OA/counterspell are dual-registered with conflicting ranges/IDs, enabling duplicate eligibility and unpredictable outcomes.
3. Critical: BG3 OA path appears non-executing unless legacy OA also fires (silent no-op risk).
4. Major: `shield_reaction` is granted without guaranteed registration (dead grant / broken UX).
5. Major: Frozen condition lacks full expected attacker-advantage + melee-autocrit behavior.
6. Major: Melee autocrit distance uses hardcoded value inconsistent with parity intent.
7. Major: Initiative often becomes effectively static due to preset values in scenario content.
8. Major: Movement defaults and turn-reset flow are inconsistent (dual-source + dual-reset risk).
9. Major: Passive context provider coverage is narrower than source data contexts.
10. Major: Granted actions exist that are missing from both runtime registries and BG3-only mappings.

## Implementation Plan

### Phase 1: Reaction System Canonicalization and Exploit Closure
1. Create canonical reaction IDs: `reaction.opportunity_attack`, `reaction.counterspell`, `reaction.shield`.
2. Add alias mapping for legacy IDs: `opportunity_attack`, `BG3_OpportunityAttack`, `counterspell_reaction`, `BG3_Counterspell`, `shield_reaction`.
3. Remove duplicate runtime registration paths so each semantic reaction is registered exactly once.
4. Enforce startup validation: grants must reference registered reactions (or mapped aliases), else fail fast in dev/test.
5. Fix resolver consumption logic:
   - Re-check per-reactor reaction availability before each resolve.
   - Track reactor consumption per trigger and stop additional resolves after first successful reaction.
6. Replace OA “context flag only” behavior with explicit reaction execution call into attack pipeline (no implicit magic key dependency).
7. Ensure counterspell/shield execution uses canonical reaction pipeline with deterministic ordering and budget checks.

### Phase 2: Meter Canonical Rules and Constants
1. Introduce one authoritative combat rules constants surface (meters only).
2. Standardize defaults:
   - Default melee reach: `1.5 m`
   - OA trigger range: `1.5 m`
   - Default move budget: `9.0 m` (BG3-style 30 ft equivalent)
3. Replace hardcoded distance literals in combat logic with named constants.
4. Update movement service, arena defaults, action budget defaults, and turn lifecycle fallbacks to read from the same source.

### Phase 3: Rule Behavior Parity Corrections
1. Extend Frozen rule behavior:
   - Attackers get advantage against Frozen targets.
   - Melee-range attacks against Frozen targets auto-crit using canonical melee-autocrit threshold.
2. Replace hardcoded melee autocrit check with canonical constant + shared helper to avoid divergent logic paths.
3. Add explicit scenario-level initiative mode:
   - `RollAtCombatStart` (default)
   - `UsePreset` (opt-in only)
4. Loader behavior:
   - Default to rolling initiative at combat start.
   - Respect `UsePreset` only when explicitly set.
5. Consolidate turn reset path to avoid double replenishment side effects.

### Phase 4: Passive and Action Coverage Closure
1. Expand passive functor provider context coverage to include all contexts found in shipped passive data.
2. For unsupported contexts, register explicit no-op/unsupported handlers with one-time warnings (not silent drops).
3. Resolve missing granted action IDs:
   - Add canonical action entries or aliases for all currently granted missing IDs.
   - Keep allowlist empty for shipped gameplay content unless intentionally disabled by design with explicit annotation.
4. Add validation rule: any granted action in shipped scenarios must resolve to a loadable runtime action.

### Phase 5: Automatic Migration and Compatibility
1. Add schema versioning for scenario/save payloads: introduce `schemaVersion: 2` and `units: "m"`.
2. Automatic migration on load for legacy schema:
   - Convert legacy feet-like numeric combat distances/movement/radii to meters.
   - Normalize reaction IDs through alias map to canonical IDs.
   - Set initiative mode defaults (`RollAtCombatStart` unless explicitly preserved by prior metadata).
3. Idempotency guarantee:
   - Migration runs once per payload version.
   - Re-loading migrated data performs no additional conversion.
4. Add migration report logging in dev/test to expose converted fields and alias remaps.

### Phase 6: Documentation and Guardrails
1. Update parity audit docs to reflect current engine truth and resolved issues.
2. Add “reaction uniqueness + migration + unit canon” section to contributor guidance.
3. Add CI validations for:
   - Duplicate semantic reaction registration
   - Unknown reaction grants
   - Missing granted action IDs
   - Unmapped passive contexts

## Public API / Interface / Type Changes
1. Add `InitiativeMode` enum to scenario schema with values `RollAtCombatStart` and `UsePreset`.
2. Add scenario/save metadata fields: `schemaVersion` and `units`.
3. Add reaction alias resolver interface used by loader and runtime registries.
4. Expose canonical combat rule constants via a shared rules class consumed by movement, reactions, and attack resolution.
5. Add migration service interfaces for scenario/save upgrade to schema v2.

## Test Cases and Scenarios

### Unit Tests
1. Reaction resolver enforces max one reaction per reactor per trigger even with multiple eligible reactions.
2. Alias mapping resolves all legacy reaction IDs to canonical IDs.
3. OA reaction executes a real attack action and consumes one reaction.
4. Frozen grants attacker advantage and melee auto-crit only within threshold.
5. Distance constants are consumed by all relevant subsystems (no divergent literals).
6. Migration converts legacy distances once and is idempotent.
7. Initiative mode defaults to rolling unless explicitly preset.
8. Passive context validator fails on unmapped contexts unless explicitly marked unsupported.
9. Granted action validation fails when scenario grants unknown action IDs.

### Integration/Gameplay Tests
1. Headless parity tests pass with no reaction duplicates and no missing grants.
2. Auto-battle stress run across multiple seeds shows no multi-reaction exploit and no freeze/loop regressions.
3. Godot log smoke test shows no script/runtime errors at startup.
4. Full-fidelity auto-battle confirms OA/counterspell/shield interactions behave consistently in UI + combat log.

### Required Build Gates
1. `scripts/ci-build.sh`
2. `scripts/ci-test.sh` (if test project exists)
3. `scripts/ci-godot-log-check.sh`

## Acceptance Criteria
1. Each reactor can spend at most one reaction per trigger by engine guarantee.
2. OA/counterspell/shield each exist as one canonical reaction path with working execution.
3. No granted reaction/action in shipped scenarios points to unresolved IDs.
4. Distances and movement are meter-canonical with no conflicting defaults.
5. Frozen/autocrit/initiative behaviors match declared parity rules.
6. Migration upgrades legacy content automatically and safely, with idempotent behavior.
7. CI blocks regressions for registry coverage, passive contexts, and startup errors.

## Assumptions and Defaults
1. Internal canonical distance unit is meters (selected).
2. Scope includes all confirmed Critical + Major findings (selected).
3. Compatibility strategy is automatic migration with alias support (selected).
4. Default initiative behavior is `RollAtCombatStart`; preset initiative is opt-in only.
5. Default movement budget baseline is `9.0 m` unless overridden by explicit unit stats.
6. Unsupported passive contexts are surfaced explicitly (warning/error policy), never silently ignored.
