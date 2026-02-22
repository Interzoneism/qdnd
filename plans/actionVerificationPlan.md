## Plan: Full-Fidelity Batch Action/Spell Verification System

Build a 3v3 full-fidelity test mode where each of 6 characters has exactly 1 forced action. After one round (all have acted), combat ends and the log is analyzed against bg3wiki. This enables systematic verification of all ~200 combat actions, 6 at a time, with wiki as source of truth.

---

### Phase 1: TestDummy Race + Class (Data-only)

1. **Create TestDummy race JSON** in [Data/Races/](Data/Races/) — ID `test_dummy`, grants proficiency with ALL weapon types (simple, martial, battleaxe, greataxe, greatsword, halberd, etc.), all armor, high base stats (16+ all), high HP
2. **Create TestDummy class JSON** in [Data/Classes/](Data/Classes/) — ID `test_dummy`, d12 hit dice, INT-based spellcasting, all saving throw proficiencies, no granted abilities (overridden by scenario)
3. **Verify auto-registration** — confirm `CharacterDataRegistry` glob-loads new files from those directories

**Relevant files:**
- [Data/Races/core_races.json](Data/Races/core_races.json) — structure template
- [Data/Classes/martial_classes.json](Data/Classes/martial_classes.json) — structure template
- [Data/CharacterModel/CharacterDataRegistry.cs](Data/CharacterModel/CharacterDataRegistry.cs) — verify auto-load

---

### Phase 2: 3v3 Multi-Action Test Scenario Generator

4. **Add `GenerateMultiActionTestScenario()`** to [Data/ScenarioGenerator.cs](Data/ScenarioGenerator.cs) — takes 6 action IDs, creates 3 Player + 3 Hostile TestDummy units, each tagged `ability_test_actor:<actionId>`, `ReplaceResolvedActions=true`, interleaved initiative (99/98/97/96/95/94), all clustered within 2m for AoE reach
5. **Add `DynamicScenarioMode.ActionBatch`** enum value in [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs)
6. **Add CLI flag `--ff-action-batch id1,id2,id3,id4,id5,id6`** — parse comma-separated IDs, store as list, set mode
7. **Add scenario loading case** for `ActionBatch` in `LoadDynamicScenario()` — validate all IDs, call generator
8. **Update [scripts/run_autobattle.sh](scripts/run_autobattle.sh)** — forward `--ff-action-batch`, auto-inject `--max-rounds 1`
9. **Smart weapon auto-equip** per action type — Cleave needs greataxe/greatsword, ranged weapon actions need bow, generic melee → longsword. Extend existing weapon logic at [ScenarioGenerator.cs line 170](Data/ScenarioGenerator.cs#L170)

**Relevant files:**
- [Data/ScenarioGenerator.cs](Data/ScenarioGenerator.cs) — existing `GenerateActionTestScenario()` as template
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs) — CLI parsing at [line ~508](Combat/Arena/CombatArena.cs#L508), `LoadDynamicScenario()` at [line ~1418](Combat/Arena/CombatArena.cs#L1418)
- [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs) — existing tag bypass at [line ~289](Combat/Actions/EffectPipeline.cs#L289) (no changes needed)

---

### Phase 3: Combat Log Enhancements (if needed)

10. **Audit `ACTION_DETAIL` events** after first test run — check what's missing for wiki verification
11. **Add fields to `ActionDetailCollector`** as needed: `max_targets`, `actual_targets_hit`, `aoe_shape`, `aoe_radius`, `damage_modifier` (half/double), `attack_type`, `required_weapon`
12. **Add `ACTION_BATCH_SUMMARY` event** — emitted at round end in action-batch mode, summarizes all 6 actions (used?, targets hit, damage, statuses, healing)
13. **Update documentation** in [AGENTS-FULL-FIDELITY-TESTING.md](AGENTS-FULL-FIDELITY-TESTING.md) with new fields

**Relevant files:**
- [Tools/AutoBattler/ActionDetailCollector.cs](Tools/AutoBattler/ActionDetailCollector.cs)
- [Tools/AutoBattler/BlackBoxLogger.cs](Tools/AutoBattler/BlackBoxLogger.cs)
- [Tools/AutoBattler/AutoBattleRuntime.cs](Tools/AutoBattler/AutoBattleRuntime.cs)

---

### Phase 4: Per-Batch Testing Workflow (for executing agents)

14. **Select 6 actions** from the batch manifest
15. **Research each on bg3wiki** using `fetch_webpage` on `https://bg3.wiki/wiki/<ActionName>` — extract: description, cost, damage formula, AoE shape/radius, max targets, save type, recharge, required weapon, special behavior
16. **Run the test**:
    ```
    ./scripts/ci-build.sh
    ./scripts/run_autobattle.sh --full-fidelity --ff-action-batch cleave,lacerate,smash,topple,pommel_strike,fireball --max-rounds 1 --verbose-detail-logs
    ```
17. **Analyze combat log** — for each action verify: was it used, damage type/amount, target count vs max, AoE correctness, save type, status effects, attack rolls
18. **Fix discrepancies** — if behavior doesn't match wiki, fix in action JSON, `BG3ActionConverter`, or `EffectPipeline`, then rerun
19. **Broaden fixes** — reason about whether similar actions share the same problem (e.g., all weapon actions with half-damage, all cone abilities). Apply broader fix and rerun affected batches

---

### Phase 5: Action Batching & Tracking

20. **Create batch manifest** at `Data/Scenarios/action_test_batches.json` — ~200 actions grouped into ~34 batches of 6, ordered: weapon actions → cantrips → level 1 → level 2 → level 3+ → class features → self-buffs
21. **Create progress tracker** at `artifacts/autobattle/action_test_progress.json` — per-batch status (pending/pass/fail), notes, dates

---

### Verification

1. `./scripts/ci-build.sh` passes after all code changes
2. `./scripts/ci-godot-log-check.sh` — no startup errors from TestDummy race/class
3. `./scripts/ci-test.sh` — no regressions
4. Existing `--ff-ability-test fire_bolt` still works (backwards-compatible)
5. New batch mode: `./scripts/run_autobattle.sh --full-fidelity --ff-action-batch fire_bolt,ray_of_frost,sacred_flame,magic_missile,cure_wounds,bless --max-rounds 1 --verbose-detail-logs` — all 6 actions appear in log, each character used their action
6. First real weapon actions batch against wiki specs

---

### Decisions

- **TestDummy is data-only** — existing `ability_test_actor:` tag bypass handles all cost/resource bypassing; the race/class provides proficiencies and stats
- **`--max-rounds 1`** ends combat cleanly after all 6 characters act
- **Reuse existing tag system** — no new bypass code in EffectPipeline needed
- **Clustered positioning** — all 6 within 2m so melee/AoE actions reach targets
- **Interleaved initiative** — Player/Hostile alternate turns for natural cross-faction targeting
- **Combat actions only** (~200) — skip toggles, passives, consumables

### Further Considerations

1. **Self-targeting actions** (Dodge, Dash, Rage, Second Wind) — group into dedicated "self-buff" batches, verify buff/status applied to self instead of damage dealt
2. **Concentration conflicts** — multiple concentration spells from different casters should coexist. Worth verifying during spell batches.
3. **Healing spells** — all units start at full HP so healing shows 0 effect. For healing batches, consider setting TestDummy starting HP to 50% max, or accept the cast was correct and verify heal *attempt* in log.
