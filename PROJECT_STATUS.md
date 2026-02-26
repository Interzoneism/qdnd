# QDND — Project Status (Single Source of Truth)

> **Last updated:** 2026-02-26
> **Maintained by:** Technical Director
> **Build status:** GREEN | **Tests:** 2222 pass / 26 skipped / 0 fail | **Parity:** 2/2 pass

---

## Project Identity

| Item | Value |
|---|---|
| **Engine** | Godot 4.6 |
| **Language** | C# (.NET 8.0) |
| **Test framework** | xUnit 2.7.0 |
| **Main scene** | `Combat/Arena/CombatArena.tscn` |
| **Assembly** | QDND |
| **Codebase size** | 498 .cs files · ~170K LOC |
| **Test coverage** | 2240 tests (Unit/Integration/Simulation/Performance) |

---

## System Architecture Overview

```
CombatArena (scene root)
├── RegistryInitializer (bootstrap)
│   └── Data Layer: DataRegistry, ActionRegistry, StatusRegistry,
│       PassiveRegistry, InterruptRegistry, CharacterDataRegistry
├── CombatContext (DI container for all services)
│   ├── TurnQueueService → TurnLifecycleService → CombatStateMachine
│   ├── ActionExecutionService → EffectPipeline → 37 Effect subtypes
│   ├── RulesEngine → BoostEvaluator → ConditionEffects
│   ├── StatusSystem → ConcentrationSystem → StatusTickProcessor
│   ├── ReactionSystem → ReactionCoordinator
│   ├── MovementService → TacticalPathfinder
│   ├── SurfaceManager (environmental hazards)
│   ├── AIDecisionPipeline → AIScorer/AITargetEvaluator
│   ├── InventoryService (12-slot equipment)
│   ├── CombatPresentationService → VfxPlaybackService
│   └── CombatLog, CommandService, ResourceManager, etc.
└── CombatHUD (UI root)
    └── HudController → ActionBar, InitiativeRibbon, Panels, Overlays
```

---

## BG3 Parity Status

| Area | Parity | Notes |
|---|---|---|
| Core Engine | 96% | Combat loop, turns, initiative, death saves |
| Spells (definitions) | 95% | 201/205 spells defined with metadata |
| Spells (AOE/targeting) | 75% | 14 AOE spells have broken targetType, 7 wrong radii |
| Statuses | 85% | 267 statuses, 261 with mechanics |
| Passives | 82% | BoostConditions skipped; racial advantage tags dead code |
| Class Features | 55% | Major gap — metamagic, Wild Shape, invocations |
| Common Actions | 70% | Shove broken (wrong mechanic + AI dead); others OK |
| Reactions | 70% | 13 wired, ~6 remaining |
| Surfaces/Obscurity | 40% | Obscurity dead code; Ice/Grease no Prone |
| **Overall** | **~76%** | Per `docs/BG3_COMBAT_PARITY_AUDIT_2026.md` |

---

## Active Plans (ordered by priority)

1. **[plans/BG3_Combat_Parity_Plan.md](plans/BG3_Combat_Parity_Plan.md)** — Phase 1 CRITICAL, Phase 2 MAJOR, Phase 3 MINOR work packages to close BG3 gaps
2. **[plans/CombatArenaPurityOverhaul.md](plans/CombatArenaPurityOverhaul.md)** — Bug fixes and AI improvements (partially complete)
3. **[inventoryPlan.md](inventoryPlan.md)** — Unified CharacterInventoryScreen UI rewrite (not started)

## Archived Plans (historical reference only)

- ~~`plans/OLD_DeepMechanicalOverhaulPlan.md`~~ — All 10 phases complete
- ~~`plans/VFX_Plan.md`~~ — VFX pipeline implemented

---

## Build Gates (mandatory before any merge)

```bash
scripts/ci-build.sh           # Must: 0 errors
scripts/ci-test.sh             # Must: 0 failures
scripts/ci-godot-log-check.sh  # Must: no ERROR/SCRIPT ERROR lines
```

---

## Authoritative Documentation

| Document | Scope |
|---|---|
| [AGENTS.md](AGENTS.md) | Agent rules, architecture constraints, build gates, gotchas |
| [CODING_STANDARDS.md](CODING_STANDARDS.md) | Naming, namespaces, patterns, event policy |
| [docs/BG3_COMBAT_PARITY_AUDIT_2026.md](docs/BG3_COMBAT_PARITY_AUDIT_2026.md) | **Current** comprehensive parity audit (Feb 2026) |
| [docs/BG3_COMBAT_PARITY_AUDIT.md](docs/BG3_COMBAT_PARITY_AUDIT.md) | Living changelog of parity sessions |
| [docs/BG3_FULL_GAP_ANALYSIS.md](docs/BG3_FULL_GAP_ANALYSIS.md) | Gap analysis with reviewer addendum |
| [docs/action-registry.md](docs/action-registry.md) | ActionRegistry architecture |
| [docs/bg3-status-system.md](docs/bg3-status-system.md) | Status pipeline architecture |
| [docs/vfx-pipeline.md](docs/vfx-pipeline.md) | VFX pipeline architecture |
| [docs/bg3-surface-system.md](docs/bg3-surface-system.md) | Surface system architecture |

## Deprecated Documentation (DO NOT FOLLOW)

| Document | Why | Replacement |
|---|---|---|
| ~~`docs/RULES-WINDOWS-PASSIVES.md`~~ | Documents deleted PassiveRuleService | `PassiveRegistry` → `BoostApplicator` pipeline |
| ~~`docs/BG3_GAP_ANALYSIS_2025.md`~~ | Superseded | `docs/BG3_FULL_GAP_ANALYSIS.md` |
| ~~`docs/CODEBASE_AUDIT_REPORT.md`~~ | Date wrong, many items fixed | This document + `CODING_STANDARDS.md` |

---

## Known Technical Debt (prioritized)

### P0 — Fix Now
~~1. **Dangerous stale doc**: `docs/RULES-WINDOWS-PASSIVES.md` describes deleted system~~ — DONE: replaced with tombstone
~~2. **Namespace collision**: `Tools/NewDebugCommands.cs` and `Tools/DebugConsole.cs` use `namespace Tools;`~~ — DONE: moved to `QDND.Tools`, class renamed to `ConsoleDebugCommands`

### P1 — Fix This Sprint
~~3. **God file**: `Combat/Actions/Effects/Effect.cs` — 28 classes in 2,657 lines~~ — DONE: split into 25 per-class files
~~4. **Hardcoded DC**: `TargetValidator.cs` — `sanctuaryDC = 13`~~ — DONE: BG3-accurate hard targeting block (no save)
~~5. **Event pattern inconsistency**: `EffectPipeline.cs` mixes `event Action<T>` and `event EventHandler<T>`~~ — DONE: all 4 events migrated to `Action<T>`

### P2 — Fix When Touching
6. **Nullable annotations**: 40 files opt-in, 458 don't — need project-wide `<Nullable>enable</Nullable>`
~~7. **Three `.csproj.old*` files** in repo root~~ — DONE: deleted
~~8. **Ghost UID files**: `PassiveRuleServiceTests.cs.uid`, `AIReactionPolicyTests.uid`~~ — DONE: deleted
9. **Misplaced test scenario**: `Tests/Scenarios/bg3_replica_test.json` → should be `Tools/Scenarios/`
10. **Loose test files at Tests/ root** — move into `Tests/Unit/` or `Tests/Integration/`
~~11. **`DebugCommands.cs` + `NewDebugCommands.cs`** — merge or clarify which is authoritative~~ — DONE: new class renamed to `ConsoleDebugCommands`
~~12. **`GEMINI.md`** says Godot 4.5~~ — DONE: fixed to 4.6
~~13. **Windows ADS ghost file**: `plans/VFX_Plan.md:Zone.Identifier`~~ — DONE: deleted

### P3 — Track
14. Portrait system TODOs (8 occurrences) — needs art pipeline
15. `HudController` at 2,221 LOC — decomposition candidate
16. `AIDecisionPipeline` at 3,268 LOC — partial extraction candidate
17. OA timing incorrect: fires regardless of hit (`CombatMovementCoordinator` line 365)
18. Save/restore incomplete: equipment re-equip + portrait persistence

---

## Warning Inventory (25 total, 0 errors)

| Category | Count | Source |
|---|---|---|
| CS8632 nullable context | 12 | `AIScorer.cs`, `EncounterService.cs` — using `?` without `#nullable enable` |
| CS0618 obsolete API | 2 | `QDNDEditorPlugin.cs` — `RemoveControlFromDocks` → `RemoveDock` |
| CS0219 unused variable | 1 | `TimelineVerification.cs` — `projCalled` |
| CS0169 unused field | 1 | `HudSnapManager.cs` — `_guideLayer` |
| CS0414 assigned-not-read | 1 | `HudController.cs` — `_pendingTooltipAction` |
| Other CS8632 | 8 | Spread across `AIScorer.cs` parameters |

---

*This document is the single source of truth for project status. Update it when completing work packages or changing architecture.*
