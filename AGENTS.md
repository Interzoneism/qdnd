# Agent Rules (Godot 4.6 C#)

## Governance
- **[CODING_STANDARDS.md](CODING_STANDARDS.md)** — Mandatory coding standards (naming, namespaces, patterns, events, error handling)
- **[.github/copilot-instructions.md](.github/copilot-instructions.md)** — AI agent architecture guide

## Project scope
BG3 combat parity. Explicitly excluded: resting outside combat, ambush, dialog, quests, world map, journal, illithid powers, any gameplay outside tactical combat.

## Scope & safety
- Operate strictly within /workspace (repo root). Never write outside the repo.
- Prefer minimal diffs and reversible changes.
- Never mass-reformat or rename large sets of files unless explicitly required.

## Build gates
- Before declaring done, always run:
  - scripts/ci-build.sh
  - scripts/ci-test.sh (if a test project exists)
  - scripts/ci-godot-log-check.sh — lightweight Godot startup smoke test; runs the engine headless for ~60 frames and fails if any `ERROR:`, `SCRIPT ERROR:`, or `Unhandled Exception:` lines appear in the log. Much faster than a full autobattle or headless test suite. Catches script parse errors, autoload failures, and `_Ready()` exceptions introduced by a change. Set `GODOT_BIN` if godot is not on PATH.
- If you introduce new systems, update documentation in /docs.

## Git / Source
- Do not commit, the human user will do this
- Do not push to git, the human user will do this

## Godot location
If you are working in a Windows environment: `C:\HQ\Godot\Godot_v4.6-stable_mono_win64\Godot_v4.6-stable_mono_win64.exe`
If you are working in a WSL Ubuntu environment: `\martin\godot\Godot_v4.6-stable_mono_linux_x86_64`

## Godot-specific commands
### Automation helpers
- `./scripts/run_headless_tests.sh` runs the headless verification suite that uses `Tools/HeadlessTestRunner.cs` to validate services, registries, and scenarios without rendering.
- `./scripts/run_screenshots.sh` builds a HUD scene capture under Xvfb and drops fresh images into `artifacts/screens/`; pair it with `./scripts/compare_screenshots.sh` to diff against `artifacts/baseline/`.

## Game testing & debugging
### Full-fidelity testing (primary method for verifying the game works)
- **Purpose**: Run the game exactly as a player would experience it — full HUD, animations, visuals, camera — with a UI-aware AI playing like a human.
- **When to use**: To verify that the game works end-to-end after any change. If it breaks here, it would break for a real player.
- **Quick start**: `./scripts/run_autobattle.sh --full-fidelity --seed 42`
- **Full guide**: See [AGENTS-FULL-FIDELITY-TESTING.md](AGENTS-FULL-FIDELITY-TESTING.md)
- **Iron rule**: NEVER disable systems or bypass components to make the test pass. Fix the game code.

### Fast auto-battle (quick iteration on combat logic)
- **Purpose**: Run the real CombatArena.tscn scene headless with AI-controlled units to expose state machine bugs, action budget issues, turn queue problems, and victory condition failures.
- **When to use**: Quick iteration on combat logic bugs, or stress-testing with many seeds.
- **Quick start**: `./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20`
- **Debug guide**: See [AGENTS-AUTOBATTLE-DEBUG.md](AGENTS-AUTOBATTLE-DEBUG.md) for:
  - How to interpret failures (TIMEOUT_FREEZE, INFINITE_LOOP)
  - Creating custom scenarios to target specific bug categories
  - Log analysis workflow (combat_log.jsonl + stdout)
  - Iterative debugging loop (run → analyze → fix → verify → stress-test)
- **Key insight**: Unlike simulation tests, auto-battles use the *real game code paths* and will trigger bugs that manual testing might miss.

## Parallel work
- Use when the sub-agents can work on very different systems

## Vision bridge tools
- `mcp_vision-bridge_vision_ask`: submit a screenshot path plus a concrete request (e.g., describe characters, UI, and layout) to get a natural-language summary of what is visible.
- `mcp_vision-bridge_vision_ocr`: supply the screenshot path and ask for any readable text; useful when you need labels or log output captured in the image.
- `mcp_vision-bridge_vision_ui_spec`: send the screenshot and request the UI structure; it returns a JSON-style spec that you can use to reconstruct or compare layouts.

## CodeGraphContext (CGC) — code graph queries
CGC builds a graph of all classes, functions, and call relationships across the 610-file codebase and lets you query it. Use it for impact analysis, tracing call chains, and finding integration points — much faster than grep for structural questions.

### IMPORTANT: use the CLI, not the MCP tools
The MCP tools (`mcp_codegraphcont_*`) work, but `add_code_to_graph` starts a background job you then have to poll with `check_job_status` repeatedly — it will appear to hang. **Prefer the CLI** which is synchronous:

```bash
CGC=".mcp/cgc/.venv/bin/cgc"
```

The repo is already indexed (610 files, 8325 functions, 1027 classes). You only need to re-index if you add/change many files:
```bash
.mcp/cgc/.venv/bin/cgc list          # verify it's indexed
.mcp/cgc/.venv/bin/cgc stats         # see counts
.mcp/cgc/.venv/bin/cgc index .       # re-index if needed (synchronous, ~90s)
```

### Key commands

**Find code:**
```bash
cgc find pattern "SurfaceManager"          # substring match across all element names
cgc find name MyClass                      # exact name
cgc find name MyFunc --type function       # exact name, filtered by type
cgc find content "some string in source"   # full-text search of source/docstrings
```

**Analyze relationships:**
```bash
cgc analyze callers CreateSurface          # who calls this function?
cgc analyze calls CreateSurface            # what does this function call?
cgc analyze chain StartTurn ProcessTurnStart --depth 8   # call path between two functions
cgc analyze tree SurfaceManager            # inheritance hierarchy
cgc analyze overrides Execute              # all implementations of a method
cgc analyze dead-code                      # find unreachable code
cgc analyze complexity MyClass            # cyclomatic complexity
```

**Raw Cypher queries** (most flexible — FalkorDB/Neo4j):
```bash
cgc query "MATCH (c:Class) WHERE c.name CONTAINS 'Surface' MATCH (f:File)-[:CONTAINS]->(c) RETURN c.name, f.path ORDER BY c.name"
cgc query "MATCH (f:Function)-[:CALLS]->(g:Function) WHERE g.name = 'CreateSurface' RETURN f.name, f.file"
```
Node labels: `File`, `Class`, `Function`, `Module`. Relationships: `[:CONTAINS]`, `[:CALLS]`, `[:IMPORTS]`, `[:INHERITS]`.

**Note:** `c.file` / `f.file` are often `null` in direct property access — join via `(file:File)-[:CONTAINS]->(node)` to get paths.

### Watch directory (auto-update index)
The MCP server is configured with `ENABLE_AUTO_WATCH: true`, so it automatically watches the repo and re-indexes changed files whenever VS Code is open. No action needed by agents.

If for some reason the watch is not running (e.g. after a server restart), you can verify with:
```bash
.mcp/cgc/.venv/bin/cgc watching   # in MCP mode — check via list_watched_paths MCP tool
```
Or trigger a one-shot re-index via the CLI if many files changed:
```bash
.mcp/cgc/.venv/bin/cgc index .
```

## Common Gotchas
- Ring mesh orientation: `TorusMesh` is already ground-aligned in Godot. Do **not** rotate range/selection/target torus indicators by 90 degrees unless you have verified the mesh orientation in-scene. A forced X-axis 90 rotation will put rings on the wrong axis.
- Test-host interop: In `dotnet test` (`testhost`/`vstest`) processes, direct Godot interop calls like `Godot.GD.Print/PrintErr` (and sometimes `Godot.FileAccess`/`DirAccess`) can crash the host. For parser/data paths exercised by unit tests, guard for testhost and fall back to `Console` + `System.IO`.
- Phase references: Code comments reference "Phase A/B/C" from an earlier implementation plan. Phase C (surfaces spawned by effects, movement validation) is largely **incomplete** — stubs exist but the full pipeline does not execute. Don't assume phase labels represent current status.
- Functor stubs: 10+ functor types in `FunctorExecutor.cs` are stubs that log warnings but do nothing: `SpawnSurface`, `Teleport`, `UseSpell`, `Resurrect`, `Counterspell`, `SummonInInventory`, `Explode`, `CreateZone`, `FireProjectile`, `Douse`. Check before assuming a functor works end-to-end.
- ConditionEvaluator fail-closed: Unknown BG3 condition functions return `false` with a warning. This means conditions with unrecognised functions deny the boost/passive; check logs for `[ConditionEvaluator] Unknown function` warnings if an effect is not applying.
- Dual BG3StatusIntegration: `Combat/Statuses/BG3StatusIntegration.cs` handles runtime integration; `Data/Statuses/BG3StatusIntegration.cs` handles data-layer conversion. Different roles, same name, different namespaces.
- EffectPipeline property injection: At ~3.3k lines with ~15 optional service dependencies. Follow the existing pattern of nullable property setters when adding services.
- Reaction execution flags: Reactions set `SkipRangeValidation=true` and `IgnoreReactionBudgetCheck=true` during execution because eligibility is checked at prompt time, not execution time.
- Exhaustion incomplete: Only Level 1 exhaustion is implemented (disadvantage on attacks/checks). Levels 2-6 are not tracked — no speed halving, no save disadvantage, no max HP reduction, no death.
- ObscurementService not wired: `AddZone()`/`RemoveZone()` are TODOs — spell effects (Darkness, Fog Cloud) do not push zones into the obscurement service. Only surface-based obscurement works.
- Barrier system: Referenced in `RulesEngine.cs` as "not yet implemented" (`targetBarrier: 0`). Damage pipeline has the absorption slot but no system feeds it.
- Keep this section updated: when you discover a recurring engine/UI pitfall that can waste debugging time, add it here as a concise rule for future agents.

## Codebase Quick Reference

**Scale**: ~190k lines C#, 610 files, 1027 classes. Combat/ is ~99k lines, Data/ ~19k, Tests/ ~59k.

**Data volumes**: 1467 BG3 spell entries, 1082 status entries, 418 passives, 54 interrupts → parsed at startup. 427 supplementary JSON actions. 12 D&D classes with 46+ subclasses, 11 races with subraces, 45+ feats.

**Key subsystem sizes** (files → largest file):
- `Combat/Services/` — 30 files → InventoryService (2.3k lines)
- `Combat/Actions/Effects/` — 29 files → DealDamageEffect
- `Combat/AI/` — 17 files → AIDecisionPipeline (3.7k lines)
- `Combat/Rules/` — 14 files + 10 Boosts + 5 Functors + 3 Conditions → RulesEngine (1.5k lines), BoostEvaluator (1.2k lines), ConditionEvaluator (1.7k lines)
- `Combat/Targeting/` — 12 modes + 9 visuals + 8 core files
- `Combat/Statuses/` — 10 files → StatusSystem (1.6k lines)
- `Combat/Reactions/` — 9 files → BG3ReactionIntegration

**State machine**: 10 states (NotInCombat → CombatStart → TurnStart → PlayerDecision/AIDecision → ActionExecution → ReactionPrompt → TurnEnd → RoundEnd → CombatEnd → NotInCombat). 7 substates (None, TargetSelection, MultiTargetPicking, AoEPlacement, MovementPreview, ReactionPrompt, AnimationLock).

**Character model**: `CharacterSheet` (properties) → `CharacterResolver` (computed stats: HP, AC, spell slots, multiclass merging) → `CharacterBuilder` (creation flow). Resolution order: Race → Background → Class → Feat → Multiclass spell merge.

**Damage pipeline**: 5 stages in `DamagePipeline` — base → additive modifiers → percentage modifiers → resistance/vulnerability/immunity → reduction/absorption (temp HP).

**Boost system**: 57 types across 4 tiers, evaluated by `BoostEvaluator`. Core: AC, Advantage, Disadvantage, Resistance, DamageBonus. Advanced: UnlockSpell, RollBonus, CriticalHit. Extended: SpellSaveDC, IncreaseMaxHP, Tag.

**Conditions**: 16 D&D 5e conditions in `ConditionEffects.cs` (the sole authority). `ConditionEvaluator.cs` has 60+ BG3 condition functions for boost/passive/status evaluation.

**Surfaces**: 34 definitions in SurfaceManager. Elemental (fire, water, ice, acid, lightning, steam). Hazards (grease, oil, web, entangle, spike growth, plant growth). Clouds (fog, darkness, stinking cloud, cloudkill). Interactions: freeze/electrify/ignite/melt/douse event transforms.

**Reactions**: 13 registered (OA, Shield, Counterspell, Uncanny Dodge, Deflect Missiles, Hellish Rebuke, Cutting Words, Sentinel ×2, Mage Slayer, War Caster, Warding Flare, Defensive Duelist). 9 trigger types. AI: 5 policies (Always, Never, DamageThreshold, Random, PriorityTargets).
