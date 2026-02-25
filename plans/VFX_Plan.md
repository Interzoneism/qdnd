# Generalized Combat VFX Pipeline (Data-Driven, Category + Overrides)

## Summary
Build a single canonical VFX pipeline for combat that is data-driven, pattern-aware (point/circle/cone/line/per-target), and action-context-aware (attack type, damage type, spell intent), while preserving backward compatibility with current `VfxRequest.EffectId` and existing `CombatVFXManager` presets.

Current-state findings this plan addresses:
1. VFX is split between direct `CombatVFXManager` calls and `PresentationRequestBus` events.
2. `VfxRequest`/`SfxRequest` are published but runtime handling currently only processes camera requests.
3. There are almost no authored VFX mappings in `Data/Actions/*.json`, so mapping must come from dedicated VFX data.
4. Existing VFX logic is mostly hardcoded procedural particles and limited type mapping.

## Public API / Interface Changes
1. Extend `Combat/Services/PresentationRequest.cs`:
   1. Add `VfxEventPhase` enum: `Start`, `Projectile`, `Impact`, `Area`, `Status`, `Death`, `Heal`, `Custom`.
   2. Add `VfxTargetPattern` enum: `Point`, `PerTarget`, `Circle`, `Cone`, `Line`, `Path`, `SourceAura`, `TargetAura`.
   3. Expand `VfxRequest` with typed context fields:
      1. `ActionId`, `VariantId`, `SourceId`, `PrimaryTargetId`, `TargetIds`.
      2. `SourcePosition`, `TargetPosition`, `CastPosition`, `Direction`.
      3. `AttackType?`, `TargetType?`, `DamageType?`, `IsCritical`, `DidKill`.
      4. `Phase`, `Pattern`, `Magnitude`, `Seed`.
      5. Keep existing `EffectId` as explicit preset override for backward compatibility.
2. Keep `SfxRequest` contract aligned (same contextual fields) but runtime SFX playback is deferred to phase 2; request schema is added now.
3. Add new pure-C# interfaces in `Combat/Services`:
   1. `IVfxRuleResolver` with `Resolve(VfxRequest request) -> VfxResolvedSpec`.
   2. `IVfxPlaybackService` with `Handle(VfxRequest request)`.

## Data Model Additions
1. Create `Data/VFX/vfx_presets.json`:
   1. Defines reusable presets (`id`, `renderer`, `scenePath` or `particleRecipe`, `lifetime`, `poolKey`, `followMode`, `colorPolicy`).
2. Create `Data/VFX/vfx_rules.json`:
   1. `defaultRules`: category mapping by phase + attackType + targetType + damageType + intent.
   2. `actionOverrides`: per-action/per-variant/per-phase overrides.
   3. `fallbackRule`: guaranteed default preset per phase.
3. Rule precedence (fixed and deterministic):
   1. `EffectId` in request (explicit runtime override).
   2. `actionOverrides` exact match on `actionId + variantId + phase`.
   3. `actionOverrides` match on `actionId + phase`.
   4. `defaultRules` best-specificity match.
   5. `fallbackRule`.

## Runtime Architecture
1. Canonical path:
   1. `ActionExecutionService` stays gameplay-only.
   2. `CombatPresentationService` publishes VFX events through `PresentationRequestBus`.
   3. `VfxPlaybackService` subscribes to bus and executes VFX through `CombatVFXManager`.
2. `CombatPresentationService` changes:
   1. Replace direct `_vfxManager.SpawnEffect/SpawnProjectile` calls with `VfxRequest` publications.
   2. Preserve marker timing behavior exactly (no gameplay timing changes).
3. `CombatArena` changes:
   1. Instantiate/register `VfxPlaybackService` after `CombatPresentationService`.
   2. Change `OnStatusApplied` visual calls to publish `VfxRequest` phase `Status`.
4. `CombatVFXManager` refactor:
   1. Add `Spawn(VfxResolvedSpec spec)` as primary runtime entry.
   2. Keep existing enum-based methods as adapter fallback.
   3. Make active/pool limits configurable from preset config (default active cap `48`).

## Pattern-Based Emission (Targeting-Aware)
1. Add `Combat/VFX/VfxPatternSampler.cs`:
   1. `SamplePoint`, `SampleCircle`, `SampleCone`, `SampleLine`, `SamplePerTarget`, `SamplePath`.
2. Pattern input source:
   1. `TargetType`, `AreaRadius`, `ConeAngle`, `LineWidth`, actor/target/cast positions from request context.
3. Emission defaults:
   1. `SingleUnit` -> `PerTarget`.
   2. `Circle` -> `Circle`.
   3. `Cone` -> `Cone`.
   4. `Line` -> `Line`.
   5. `Point` -> `Point`.
   6. `MultiUnit` -> `PerTarget`.

## V1 Preset Rollout (Core Library + Broad Use)
1. Cast presets:
   1. `cast_arcane_generic`, `cast_divine_generic`, `cast_martial_generic`.
2. Projectile presets:
   1. `proj_physical_generic`, `proj_arcane_generic`, `proj_fire`, `proj_lightning`.
3. Impact presets:
   1. Reuse and formalize existing typed impacts (`fire`, `cold`, `lightning`, `poison`, `acid`, `necrotic`, `radiant`, `force`, `psychic`, `physical`).
4. Area presets:
   1. `area_circle_blast`, `area_cone_sweep`, `area_line_surge`.
5. Status presets:
   1. `status_buff_apply`, `status_debuff_apply`, `status_heal`, `status_death_burst`.

## Implementation Slices
1. Slice 1: Contracts + Resolver + Playback plumbing.
   1. Add request/context enums and fields.
   2. Add VFX JSON loaders and resolver with precedence logic.
   3. Add playback service wired to bus.
2. Slice 2: Migrate presentation callsites.
   1. Move all VFX emission in `CombatPresentationService` and `CombatArena.OnStatusApplied` to requests.
   2. Keep camera behavior unchanged.
3. Slice 3: Pattern sampler + preset rollout.
   1. Implement pattern sampling and spec-driven spawn in manager.
   2. Populate `vfx_presets.json` and `vfx_rules.json`.
4. Slice 4: Documentation + verification.
   1. Add `docs/vfx-pipeline.md`.
   2. Update `docs/automation-visual-tests.md` with VFX baseline workflow.

## Tests and Scenarios
1. Unit tests:
   1. `VfxRuleResolverTests`: precedence, specificity, fallback behavior.
   2. `VfxPatternSamplerTests`: circle/cone/line/per-target sampling correctness and determinism.
   3. `VfxRequestFactoryTests`: marker/effect-result -> request context mapping.
2. Integration tests:
   1. Update/add tests around timeline marker emission to validate phase/pattern context fields.
   2. Add bus-to-playback integration test with fake `CombatVFXManager` to assert resolved preset IDs.
3. Visual verification:
   1. `./scripts/run_screenshots.sh` and `./scripts/compare_screenshots.sh`.
   2. `./scripts/run_autobattle.sh --full-fidelity --seed 42` for end-to-end smoke.
4. Mandatory gates before completion:
   1. `./scripts/ci-build.sh`
   2. `./scripts/ci-test.sh`
   3. `./scripts/ci-godot-log-check.sh`

## Acceptance Criteria
1. All combat VFX flows use bus requests as canonical trigger path; no direct timeline-time spawning in `CombatPresentationService`.
2. Category + override rule resolution works with deterministic fallback and no null-path failures.
3. At least one representative action per targeting shape (`SingleUnit`, `Circle`, `Cone`, `Line`, `MultiUnit`) produces correctly patterned particle placement.
4. Damage-type impacts are chosen by rule resolver rather than hardcoded branch logic in presentation layer.
5. Existing tests pass and no Godot startup errors are introduced.

## Assumptions and Defaults
1. Chosen by you:
   1. V1 scope is core library + broad rollout.
   2. Authoring model is dedicated data-driven preset/rule registry.
   3. Pipeline contract includes SFX fields now, with VFX runtime first.
   4. Mapping granularity is category defaults plus per-action overrides.
2. Default technical assumptions:
   1. Existing `VfxId`/`SfxId` remain supported as compatibility overrides.
   2. Initial presets are procedural and/or packed-scene hybrid, with procedural fallback always available.
   3. No gameplay resolution timing changes; this is presentation-only.
