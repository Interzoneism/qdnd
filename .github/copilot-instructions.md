# QDND Copilot Instructions

**Godot 4.6 C# tactical RPG with BG3-faithful combat rules.** Read [AGENTS.md](../AGENTS.md) and [CODING_STANDARDS.md](../CODING_STANDARDS.md) before any change; they are the authority. Do not commit or push — the user does that!

---

## Codebase Scale

~190k lines of C# across 610 files (1027 classes, 8325 functions). Core breakdown:

| Directory | LOC | Files | Purpose |
|-----------|-----|-------|---------|
| `Combat/` | ~99k | ~280 | Runtime combat systems |
| `Data/` | ~19k | ~70 | BG3 data parsing, registries, character model |
| `Tests/` | ~59k | ~100+ | xUnit unit/integration/simulation tests |

**Largest files** (read these cautiously — they are complex):
- `Combat/AI/AIDecisionPipeline.cs` (~3.7k lines) — AI turn planning
- `Combat/Actions/EffectPipeline.cs` (~3.3k lines, ~15 injected services) — action execution core
- `Combat/Arena/CombatArena.cs` (~2.5k lines) — composition root
- `Combat/UI/HudController.cs` (~2.7k lines) — new UI controller
- `Combat/Arena/CombatHUD.cs` (~2.4k lines) — legacy UI (being replaced)
- `Combat/Services/InventoryService.cs` (~2.3k lines) — equipment/inventory

---

## Architecture Overview

```
Combat/
├── Services/       ← 30 service files; all registered via ICombatContext
├── Actions/        ← ActionRegistry + EffectPipeline + 29 Effect types
├── AI/             ← AIDecisionPipeline + BG3ArchetypeProfile scoring (17 files)
├── Targeting/      ← 3-layer: 12 Modes → TargetingSystem → 9 Visual renderers
├── States/         ← CombatStateMachine (10 states) + CombatSubstate (7 substates)
├── Rules/          ← RulesEngine + BoostEvaluator (57 boost types) + ConditionEvaluator (60+ functions) + 5 Functors
├── Statuses/       ← StatusSystem + ConcentrationSystem + AuraSystem + StatusTickProcessor (10 files)
├── Reactions/      ← ReactionSystem + BG3ReactionIntegration (13 reactions, 9 trigger types)
├── Passives/       ← PassiveManager → PassiveFunctorProviderFactory → GenericFunctorRuleProvider
├── Movement/       ← MovementService + TacticalPathfinder (A*, 0.5m cells) + ForcedMovementService
├── Environment/    ← SurfaceManager (34 surfaces) + LOSService (cover levels) + HeightService
├── UI/             ← HudController (new) + 9 Panels + 5 Overlays + CharacterCreation (6-step)
├── Arena/          ← CombatArena.tscn (composition root) + CombatHUD (legacy) + CombatInputHandler
├── Entities/       ← Combatant (core entity, life states, death saves)
├── Persistence/    ← CombatSaveService (15 files)
├── VFX/            ← VfxPlaybackService + VfxRuleResolver (5 files)
├── Animation/      ← ActionTimeline + WeaponVisualAttachment
└── Camera/         ← CameraStateHooks + CameraFocusRequest

Data/
├── Parsers/        ← BG3SpellParser, BG3StatsParser, BG3PassiveParser, BG3StatusParser, etc.
├── Actions/        ← ActionRegistryInitializer + BG3ActionConverter + 9 JSON action files (427 actions)
├── CharacterModel/ ← CharacterSheet + CharacterResolver + CharacterBuilder (12 classes, 11 races, 46+ subclasses)
├── Statuses/       ← StatusRegistry + BG3StatusIntegration (data-layer)
├── Passives/       ← PassiveRegistry (418 BG3 passives parsed)
├── Interrupts/     ← InterruptRegistry (54 BG3 interrupts parsed)
├── Classes/        ← 3 class definition JSONs (arcane/divine/martial)
├── Feats/          ← bg3_feats.json (45+ feats), warlock_invocations.json
└── Validation/     ← ParityValidator + parity_allowlist.json
```

---

## Data Pipeline

`BG3_Data/` → `Parsers` → `Registries` → Runtime systems — all wired by `RegistryInitializer.Bootstrap()` in `CombatArena._Ready()`.

**Spell data flow**: 8 BG3 `.txt` files (1467 raw entries) → `BG3SpellParser` → `BG3ActionConverter` → `ActionDefinition` → `ActionRegistry`. Then 9 supplementary JSON files (427 more actions) loaded with `overwrite=false` (BG3 data takes precedence).

**Status data**: 1082 BG3 status entries across 11 `.txt` files → `BG3StatusParser` → `StatusRegistry` → runtime `StatusSystem`.

**Service wiring**: All services register via `ICombatContext.RegisterService<T>()` and resolve via `GetService<T>()`. Never pull services from anywhere else; do not use `GetNode<T>()` for services.

**Stats authority**: `CharacterSheet` / `CharacterResolver` only. Ability modifier = `Math.Floor((score - 10) / 2.0)`. Save DC = `8 + proficiency + abilityModifier`. Never hardcode DC or modifier values.

---

## Key Systems Quick Reference

| System | Entry Point | Key Files | Scale |
|---|---|---|---|
| Actions | `ActionRegistry`, `EffectPipeline` | `Combat/Actions/` | 29 effect types, 22 functor types, 4 deferred |
| AI | `AIDecisionPipeline` | `Combat/AI/` | `BG3ArchetypeProfile` scoring, 17 files |
| Environment | `SurfaceManager`, `LOSService`, `HeightService` | `Combat/Environment/` | 34 surfaces, 3 cover levels, height advantage |
| Movement | `MovementService`, `TacticalPathfinder` | `Combat/Movement/` | 8 movement types, A* pathfinding |
| Reactions | `ReactionSystem`, `BG3ReactionIntegration` | `Combat/Reactions/` | 13 reactions, 9 trigger types |
| Rules | `RulesEngine`, `BoostEvaluator`, `ConditionEvaluator` | `Combat/Rules/` | 57 boost types, 60+ condition functions |
| States | `CombatStateMachine` | `Combat/States/` | 10 states, 7 substates |
| Statuses | `StatusSystem`, `ConcentrationSystem`, `AuraSystem` | `Combat/Statuses/` | 16 D&D conditions, tick processing |
| Targeting | `TargetingSystem` (3-layer) | `Combat/Targeting/` | 12 modes, pool-based visuals |
| UI | `HudController` (new), `CombatHUD` (legacy) | `Combat/UI/` | 9 panels, 5 overlays, character creation |
| VFX | `VfxPlaybackService`, `PresentationRequestBus` | `Combat/VFX/` | Rule-based VFX resolution |
| Character | `CharacterSheet`, `CharacterResolver`, `CharacterBuilder` | `Data/CharacterModel/` | 12 classes, 46+ subclasses, multiclass |

---

## Combat State Machine

```
NotInCombat → CombatStart → TurnStart → {PlayerDecision|AIDecision} → ActionExecution
    ↓                                                    ↓
CombatEnd ← RoundEnd ← TurnEnd ← {PlayerDecision|AIDecision}
                                       ↑
                              ReactionPrompt ←┘
```

**Turn lifecycle** (`TurnLifecycleService.BeginTurn`): death saves → wake unconscious → reset budget (action/bonus/movement/reaction) → auto-stand from prone (50% movement) → dispatch OnTurnStart rule window → process surface/status effects → incapacitation skip check.

**Action budget per turn**: 1 Action + 1 Bonus Action + Movement (speed-based) + attacks (Extra Attack). Reaction resets at start of own turn (not round boundary).

**Combat ends** when only one faction has active combatants (`TurnQueueService.ShouldEndCombat()`).

---

## Communication Patterns

| Context | Mechanism | Example |
|---|---|---|
| Service ↔ Service | `event Action<T>` | `public event Action<Combatant, int>? OnDamageDealt;` |
| UI model → UI panel | Godot `[Signal]` | `[Signal] delegate void TurnEndedEventHandler()` |
| Camera / VFX / SFX | `PresentationRequestBus` | Fired from `CombatPresentationService` |
| Passive effects | `RuleWindowBus` → `IRuleProvider` | `GenericFunctorRuleProvider` fires at `OnAttack`, `OnDamage`, etc. |

**Never use** `event EventHandler<T>` — use `event Action<T>` throughout.

---

## Targeting System (3-layer contract)

1. **Modes** (`Combat/Targeting/Modes/`) — 12 pure C# modes: SingleTarget, MultiTarget, FreeAimGround, StraightLine, AoECircle, AoECone, AoELine, AoEWall, BallisticArc, BezierCurve, PathfindProjectile, Chain. Write into `TargetingPreviewData`, no nodes/meshes.
2. **TargetingSystem** — orchestrator; fires `OnPreviewUpdated`; owns phase lifecycle (Inactive → Previewing → MultiStep).
3. **Visuals** (`Combat/Targeting/Visuals/`) — reads `TargetingPreviewData`, pool-based rendering via `TargetingNodePool<T>`.

Style constants live in `TargetingStyleTokens` — no per-skill magic numbers in renderers.

---

## Character Model

- **12 D&D classes** (Barbarian, Bard, Cleric, Druid, Fighter, Monk, Paladin, Ranger, Rogue, Sorcerer, Warlock, Wizard) with 46+ subclasses, level tables up to L12.
- **11 races** with subraces (Human, Elf, Dwarf, Halfling, Gnome, Half-Elf, Half-Orc, Tiefling, Dragonborn, Drow, Githyanki).
- **45+ feats** (GWM, Sharpshooter, Sentinel, War Caster, Lucky, etc.).
- **Multiclass fully implemented**: spell slot merging, prerequisite validation, proficiency grants.
- **Equipment**: 34 weapon types, 12 equip slots, armor categories, weapon properties (Finesse, Heavy, etc.).
- **Resources**: Two-tier system — `ActionBudget` (action/bonus/reaction/movement) + `ResourcePool` (spell slots, ki, rage, etc.).

---

## Rules Engine

- **Saving throws**: d20 + ability mod + proficiency + boosts. Conditions auto-fail STR/DEX (paralyzed, stunned, petrified, unconscious).
- **Advantage/disadvantage**: Multi-source resolution across conditions, boosts, and modifiers. Both present = cancel.
- **Damage pipeline**: 5 stages — base → additive modifiers → percentage modifiers → resistance/vulnerability → reduction/absorption (temp HP, barriers).
- **Boost system**: 57 boost types in 4 tiers. `BoostEvaluator` queries AC, damage, advantage, crit range, roll bonuses.
- **Condition evaluator**: Recursive-descent parser for BG3 condition strings. 60+ functions (attack type checks, status checks, distance, equipment, class level, spellcasting ability, etc.). Unknown functions return `true` with warning (fail-open).

---

## Namespaces & File Rules

- All code: `QDND.*` matching directory path (e.g. `Combat/Actions/` → `QDND.Combat.Actions`).
- One primary class per file; filename matches class name.
- Godot node classes must be `public partial class`.
- `.cs.uid` files are auto-generated by Godot — never create/delete manually.

---

## Build Gates (run before declaring done)

```bash
./scripts/ci-build.sh                  # dotnet build
./scripts/ci-test.sh                   # xUnit + parity validation gate
./scripts/ci-godot-log-check.sh        # headless ~60-frame smoke test; fails on ERROR/SCRIPT ERROR
```

---

## Testing Workflows

```bash
# xUnit unit/integration tests (no Godot runtime)
dotnet test Tests/QDND.Tests.csproj

# Fast headless auto-battle (combat logic iteration)
./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20

# Full-fidelity test (primary end-to-end verification)
./scripts/run_autobattle.sh --full-fidelity --seed 42
```

- Auto-battle failures: `TIMEOUT_FREEZE` or `INFINITE_LOOP` — see [AGENTS-AUTOBATTLE-DEBUG.md](../AGENTS-AUTOBATTLE-DEBUG.md).
- **Iron rule**: never disable systems to make tests pass; fix the game code.
- Test class naming: `{SystemUnderTest}Tests`; method naming: `Method_Condition_ExpectedResult`.
- Parity allowlist for known gaps: `Data/Validation/parity_allowlist.json`.

---

## Common Gotchas

- **TorusMesh**: already ground-aligned in Godot. Do NOT add a 90° X-axis rotation to range/selection/target rings.
- **testhost interop**: `Godot.GD.Print/PrintErr` and `FileAccess`/`DirAccess` can crash `dotnet test`. Use `RuntimeSafety` helpers (falls back to `Console`/`System.IO`) in any data-layer code exercised by unit tests.
- **Targetless abilities** (`Self`, `All`, `None`): prime on hotbar click, execute on battlefield click — do not short-circuit this flow.
- **Deprecated docs**: check `AGENTS.md § Governance` for the banned documentation list before consulting any doc in `/docs`.
- **Dual HUD**: `CombatHUD` (legacy, in Arena/) and `HudController` (new, in UI/) coexist. New work should target `HudController`.
- **EffectPipeline size**: At ~3.3k lines, it uses property injection for ~15 optional services. When adding new service dependencies, follow the existing pattern of nullable property setters.
- **BG3StatusIntegration**: exists in both `Combat/Statuses/` and `Data/Statuses/` with different roles — data-layer conversion vs runtime integration.
- **PROJECT_STATUS.md**: Does not exist. Do not reference it. Use AGENTS.md as the governance doc.
- **Functor stubs**: 10+ functor types in `FunctorExecutor` are stubs that log warnings but do nothing (SpawnSurface, Teleport, UseSpell, Resurrect, Counterspell, etc.). Check before assuming a functor works.
- **ConditionEvaluator fail-open**: Unknown BG3 condition functions return `true` with a warning. This means conditions may silently pass when they shouldn't — verify condition strings are actually evaluated.
- **Reaction budget**: Reactions set `SkipRangeValidation=true` and `IgnoreReactionBudgetCheck=true` during execution because eligibility is checked at prompt time, not execution time.
- **Phase references**: Code comments refer to "Phase A/B/C" from an earlier implementation plan. Phase C (surfaces, movement validation) is largely incomplete. Don't assume phase labels indicate current status.
