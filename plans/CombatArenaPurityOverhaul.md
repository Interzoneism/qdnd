## Plan: CombatArena BG3 Purity Overhaul

CombatArena must run as authentic Baldur's Gate 3 combat with random characters — zero test, legacy, debug, or placeholder content active during gameplay. Three parallel research passes across every major system found **14 test/legacy artifacts** still in production code, **13 mechanical bugs** deviating from BG3 rules, **4 AI behavioral flaws**, and several missing BG3 mechanics.

---

### Phase A: Purge Test & Legacy Content from Production Paths

*No test/debug/placeholder code executes when CombatArena loads.*

1. **Delete `SetupDefaultCombat()` + call site** — [ScenarioBootService.cs L164](Combat/Services/ScenarioBootService.cs#L164), [CombatArena.cs L353](Combat/Arena/CombatArena.cs#L353). 4 hardcoded no-build combatants. Replace with fatal error on scenario load failure.
2. **Delete `RegisterDefaultAbilities()`** — [CombatArena.cs L359](Combat/Arena/CombatArena.cs#L359). Runs unconditionally and silently overwrites BG3-authentic `Target_MainHandAttack` (1d4 placeholder → replaces real data). Ensure ActionRegistry JSON has canonical versions of these 3 IDs.
3. **Delete `GetDefaultAbilities()` / `GetDefaultTags()` / `GetDefaultEquipment()`** — [ScenarioLoader.cs L598, L645, L935](Data/ScenarioLoader.cs#L598). Name-string matching fallbacks ("wizard" → hardcoded spell list). Make empty-after-resolve a hard error.
4. **Extract `ability_test_actor:` bypass to injectable interface** — [EffectPipeline.cs L398, L1829](Combat/Actions/EffectPipeline.cs#L398) + [AIDecisionPipeline.cs L309, L466, L902, L1178](Combat/AI/AIDecisionPipeline.cs#L309). 6 bypass sites across 2 files that skip requirements, resource checks, and budget for tagged actors. Inject no-op in production.
5. **Remove `EffectPipeline.Rng = new Random(42)`** — [RegistryInitializer.cs L110](Data/RegistryInitializer.cs#L110). Add null guard; require ScenarioBootService to set seed.
6. **Gate `test_dummy` class/race** behind `#if DEBUG` — [BG3DataLoader.cs L29](Data/BG3DataLoader.cs#L29). Non-BG3 class/race loaded unconditionally into production registry.
7. **Move test scenario files** — `action_editor_scenario.json`, `action_test_batches.json`, `bg3_replica_test.json` out of `Data/Scenarios/` to `Tools/` or `Tests/`.
8. **Fix bg3_duel.json** — goblin boss with `classId: fighter` + Splint armor (non-BG3 creature modeling).

### Phase B: Fix Mechanical Bugs (BG3 Rule Deviations)

*Every combat mechanic matches BG3.*

**Distance & Scale:**
1. **Fix `MELEE_RANGE = 5f` → `1.5f`** — [MovementService.cs L93](Combat/Movement/MovementService.cs#L93). BG3 melee reach = 5ft = 1.5m. Also fix OA reaction `Range = 5f` at [CombatArena.cs L835](Combat/Arena/CombatArena.cs#L835) to reference the constant.
2. **Fix autocrit distance `3f` → `1.5f`** — [EffectPipeline.cs L852, L1666](Combat/Actions/EffectPipeline.cs#L852). Paralyzed/Unconscious/Frozen melee autocrit fires too far.
3. **Fix `DefaultMovePoints = 10.0f` → `9.0f`** — [CombatArena.cs L49](Combat/Arena/CombatArena.cs#L49). BG3 standard walk = 9m. Audit `ActionBudget.DefaultMaxMovement = 30f` for unit consistency.

**Initiative:**
4. **Fix ScenarioGenerator initiative** — [ScenarioGenerator.cs L375](Data/ScenarioGenerator.cs#L375): fake `10+DEX+rand` → proper `d20+DEX+bonuses`.
5. **Remove hardcoded initiative from scenario JSONs** — all `bg3_*.json` files. Let ScenarioLoader roll.
6. **Implement Jack of All Trades** for initiative — Bard L2+: `floor(proficiency/2)` bonus.

**Conditions:**
7. **Fix Frozen** — [ConditionEffects.cs L313](Combat/Statuses/ConditionEffects.cs#L313): add `GrantsAdvantageToAttackers = true`, `MeleeAutocrits = true`.
8. **Wire Petrified resistance** — `HasResistanceToAllDamage` set in ConditionEffects but never consumed by damage pipeline in [Effect.cs](Combat/Actions/Effects/Effect.cs) or [RulesEngine.cs](Combat/Rules/RulesEngine.cs). Apply halving.
9. **Fix save-half minimum** — [Effect.cs L791](Combat/Actions/Effects/Effect.cs#L791): `Math.Max(1, ...)` → no minimum. BG3 allows 0 damage on half.
10. **Fix Exhaustion levels 3+** — only disad on checks/attacks implemented; missing speed halving, HP max halving for higher levels.

**Action Economy:**
11. **Fix reaction reset timing** — [TurnLifecycleService.cs L231](Combat/Services/TurnLifecycleService.cs#L231): resets ALL reactions at round start; BG3 resets at start of YOUR OWN turn.
12. **Delete `RestService.ReplenishTurnResources` dead code** — [RestService.cs L75](Combat/Services/RestService.cs#L75): duplicate logic with no production caller.
13. **Implement Haste action economy** — currently only movement multiplier; BG3 Haste also grants extra action (Attack/Dash/Disengage/Hide/Use Object).

**Concentration:**
14. **Remove Prone concentration trigger** — [ConcentrationSystem.cs L263](Combat/Statuses/ConcentrationSystem.cs#L263): fires DC 10 save on Prone even without damage. BG3: only damage triggers concentration checks.

**Other:**
15. **Extract duplicate autocrit logic** — [EffectPipeline.cs L840–883 and L1640–1697](Combat/Actions/EffectPipeline.cs#L840): identical copy-pasted condition-scanning in two paths.
16. **Fix `?? 3` proficiency fallback** — [BG3ReactionIntegration.cs L668](Combat/Reactions/BG3ReactionIntegration.cs#L668): remove, log error if null.
17. **Remove legacy `spell_slot_N` fallback** — [EffectPipeline.cs L1930](Combat/Actions/EffectPipeline.cs#L1930): two competing spell-slot deduction systems.
18. **Fix Bardic Inspiration charges** — [ResourceManager.cs L257](Combat/Services/ResourceManager.cs#L257): level formula → CHA modifier.

### Phase C: AI Quality Fixes

*AI plays like BG3 — tactically appropriate for each class.*

1. **Fix healing scoring** — [AIScorer.cs L649](Combat/AI/AIScorer.cs#L649): `return 15f` placeholder → parse `DiceFormula` average + ability mod.
2. **Fix movement scorer** — [AIScorer.cs L255](Combat/AI/AIScorer.cs#L255): `isMelee = true` hardcode → check weapon type.
3. **Add friendly fire avoidance** for AoE — subtract `alliesHit × damage × penalty` from score.
4. **Add downed ally healing priority** — `ScoreHealing()` for `LifeState == Downed` target gets large bonus (≥7.0).

### Phase D: Random BG3-Authentic Parties

*ScenarioGenerator produces fully valid BG3 parties with no fallback paths.*

1. Fix initiative formula (*depends on B.4*)
2. Add Background selection from registry
3. Remove hardcoded HP from all scenario JSONs — let `ResolvedCharacter` compute
4. Fix Mind Flayer in `bg3_boss_fight.json` — aberration stat block, not wizard class
5. Add validation assertion: every resolved unit has `AllAbilities.Count > 0` and equipment

### Phase E: Missing BG3 Mechanics (Lower Priority)

1. **Polearm Master OA** — `EnemyEntersReach` trigger + weapon-based reach modifier
2. **Teleport OA immunity** — `isTeleport` flag skips OA detection
3. **Jump bypasses surface effects** — skip surface damage/cost for jump segments
4. **Readied Actions** — `ReadiedActionTrigger` reaction type
5. **Sorcerer Flexible Casting** — slot ↔ Sorcery Point conversion actions
6. **Wild Shape HP model** — separate HP pool, not temp HP ([Effect.cs L2196](Combat/Actions/Effects/Effect.cs#L2196))

### Relevant Files

- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs) — purge fallbacks, fix constants
- [Combat/Services/ScenarioBootService.cs](Combat/Services/ScenarioBootService.cs) — delete SetupDefaultCombat
- [Data/ScenarioLoader.cs](Data/ScenarioLoader.cs) — delete name-based fallbacks
- [Data/ScenarioGenerator.cs](Data/ScenarioGenerator.cs) — fix initiative, add background
- [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) — autocrit, test bypass, spell slot legacy, duplicate code
- [Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs) — test bypass
- [Combat/AI/AIScorer.cs](Combat/AI/AIScorer.cs) — healing, movement, AoE, downed scoring
- [Combat/Movement/MovementService.cs](Combat/Movement/MovementService.cs) — MELEE_RANGE
- [Combat/Statuses/ConditionEffects.cs](Combat/Statuses/ConditionEffects.cs) — Frozen, Exhaustion
- [Combat/Statuses/ConcentrationSystem.cs](Combat/Statuses/ConcentrationSystem.cs) — Prone trigger
- [Combat/Actions/Effects/Effect.cs](Combat/Actions/Effects/Effect.cs) — save-half, death save crit, Wild Shape HP
- [Combat/Services/TurnLifecycleService.cs](Combat/Services/TurnLifecycleService.cs) — reaction reset
- [Data/RegistryInitializer.cs](Data/RegistryInitializer.cs) — fixed seed
- [Data/BG3DataLoader.cs](Data/BG3DataLoader.cs) — test_dummy gate
- [Combat/Reactions/BG3ReactionIntegration.cs](Combat/Reactions/BG3ReactionIntegration.cs) — proficiency fallback
- [Combat/Services/ResourceManager.cs](Combat/Services/ResourceManager.cs) — Bardic Inspiration

### Verification

1. `scripts/ci-build.sh` — compiles clean
2. `scripts/ci-test.sh` — all tests pass
3. `scripts/ci-godot-log-check.sh` — no errors on headless startup
4. `./scripts/run_autobattle.sh --seed 42` — completes without TIMEOUT_FREEZE/INFINITE_LOOP
5. `./scripts/run_autobattle.sh` seeds 1–100 — stress test passes
6. Grep production code for `SetupDefaultCombat`, `GetDefaultAbilities`, `GetDefaultTags`, `ability_test_actor`, `new Random(42)`, `MELEE_RANGE = 5f`, `return 15f`, `?? 3` — zero hits
7. ScenarioGenerator 10 random seeds — all produce valid scenarios, zero fallback log lines

### Decisions

- `SetupDefaultCombat` deleted (not gated) — scenario load failure is fatal
- `ability_test_actor:` extracted to injectable interface, zero tag checks in production paths
- Distance scale: 1 Godot unit = 1 meter (BG3 standard)
- Initiative always rolled (d20 + DEX); no hardcoded values in JSONs
- HP always computed from class progression; no hardcoded values in JSONs
- Monster stat blocks (Mind Flayer etc.) deferred as a design decision

### Phase Dependencies

```
Phase A (Purge Legacy) ──→ Phase B (Mechanical Fixes) ──→ Phase C (AI Fixes)
                       ╲                               ╱
                        ╰─→ Phase D (Random Parties) ─╯
Phase E (Missing Mechanics) — parallel with C/D after B
```
