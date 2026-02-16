# Plan: Full Baldur's Gate 3 Combat Parity

**Created:** 2026-02-15  
**Status:** Phase 1 Complete — Awaiting User Confirmation for Phase 2  

## Summary

Bring the CombatArena from its current state (a functional D&D 5e combat prototype with ~195 curated actions and ~108 statuses) to **full Baldur's Gate 3 combat parity** — where every class, spell, passive, status, surface interaction, and UI element works as a player would expect from BG3. This plan spans 10 phases touching every subsystem: data pipeline, spell/action coverage, status effects, class progressions, UI/HUD, environment, testing infrastructure, and gameplay polish. Each phase is designed to be independently testable via the existing full-fidelity autobattle system, which itself gets progressively enhanced as the "quality oracle" for the entire effort.

## Context & Analysis

### Current State (Measured 2026-02-15)

| Area | Have | Need | Gap |
|---|---|---|---|
| BG3 Spells parsed | 1,467 entries | 1,467 playable | ~1,270 are registered but use unconverted functors |
| Runtime actions (curated JSON) | 195 (194 unique) | ~500+ core spells/actions | Missing cantrips, most leveled spells |
| Runtime statuses (curated JSON) | 114 (108 unique) | ~300+ core statuses | Most BG3 statuses lack runtime behavior |
| Classes | 12 base | 12 base | ✓ Data-complete |
| Subclasses | 24 extracted | 58 (wiki) | 34 missing from data extract |
| Spell levels | 0-6 framework | 0-6 fully working | Effect execution gaps at every level |
| Status functors (OnApply/OnTick/OnRemove) | Not implemented | Full functor pipeline | Major gap for ~60% of BG3 statuses |
| Surface-concentration link | Partial | Full | Breaking concentration doesn't end surfaces |
| CreateSurface parser | Buggy (arg order) | Correct BG3 signatures | Misparses radius/type args |
| Equipment/Inventory | Layout only | Full item interaction | No equip/unequip affecting combat stats |
| Rest mechanics | Service exists, no UI | Short/long rest with UI | No cooldown/status/functor hooks |
| Death saves | Unclear | Full BG3 downed flow | Needs verification + visual |
| Character creation | None | Full race/class/ability score picker | Missing entirely |
| Spell preparation | Contract states required | Cleric/Druid/Paladin/Wizard prepare | Not verified in UI |
| Weapon actions | 22 unique IDs in data | All weapon actions per equipped weapon | Unclear runtime coverage |
| Difficulty modes | Data exists (Explorer/Balanced/Tactician/Honour) | Toggleable with rule changes | Not wired to gameplay |
| Upcasting | Unknown | Full spell slot upcasting | Needs investigation |

### Relevant Files (Core Architecture)

- `Combat/Arena/CombatArena.cs`: Scene controller, service wiring, CLI parsing, action execution
- `Combat/States/CombatStateMachine.cs`: Turn flow states and transitions
- `Combat/Actions/EffectPipeline.cs`: Action validation, cost consumption, rule windows, effect dispatch
- `Combat/Actions/Effects/Effect.cs`: Effect handler implementations (~1900 lines)
- `Combat/Rules/RulesEngine.cs`: d20 rolls, advantage/disadvantage, damage calc
- `Combat/Entities/Combatant.cs`: Core entity with stats, resources, action budget
- `Combat/Statuses/StatusSystem.cs`: Status application, ticking, removal
- `Combat/Statuses/ConcentrationSystem.cs`: Concentration tracking and break logic
- `Combat/Environment/SurfaceManager.cs`: Surface lifecycle, interactions, triggers
- `Combat/Movement/MovementService.cs`: Pathfinding, difficult terrain, OA triggers
- `Combat/Passives/PassiveManager.cs`: Passive abilities and toggles
- `Combat/Reactions/ReactionSystem.cs`: Reaction registry and prompt generation
- `Combat/AI/AIDecisionPipeline.cs`: AI candidate generation and scoring
- `Combat/UI/HudController.cs`: Component-based HUD orchestrator
- `Combat/Targeting/TargetValidator.cs`: Range, LOS, AoE shape validation
- `Data/Actions/BG3ActionConverter.cs`: BG3 spell → ActionDefinition conversion
- `Data/Actions/SpellEffectConverter.cs`: BG3 functor string → effect parsing
- `Data/Parsers/BG3SpellParser.cs`: Raw BG3 spell TXT parser
- `Data/Parsers/BG3StatusParser.cs`: Raw BG3 status TXT parser
- `Data/CharacterModel/CharacterResolver.cs`: Class/level → abilities resolution
- `Data/CharacterModel/CharacterSheet.cs`: Character build definition
- `Data/BG3DataLoader.cs`: Class/race/feat JSON loader
- `Data/ScenarioGenerator.cs`: Dynamic scenario creation
- `Tools/AutoBattler/UIAwareAIController.cs`: Full-fidelity AI player
- `Tools/AutoBattler/AutoBattleRuntime.cs`: Test logging and watchdog
- `scripts/run_autobattle.sh`: Test runner with Xvfb support
- `docs/BG3_PARITY_CONTRACT.md`: Parity scope and deviations
- `docs/ROADMAP.md`: Current milestones and execution streams

### Key Dependencies

- **Godot 4.5+ with .NET 8**: Engine and build system
- **BG3_Data/ extract**: Raw Larian data (1467 spells, 1082 statuses, 12 classes + 24 subclasses)
- **bg3.wiki**: Behavioral ground truth when conflicting with local data
- **Xvfb**: Virtual display for headless full-fidelity testing

### Patterns & Conventions

- **Wiki-first behavior**: `bg3.wiki` is truth; extracted data is implementation reference
- **Union registry**: Curated JSON + BG3-parsed registries are validated together
- **Effect pipeline**: Data-driven `EffectDefinition.Type` → registered handler dispatch
- **Rule windows**: Events like `BeforeAttackRoll`, `OnDealDamage` let passives/statuses hook into resolution
- **Full-fidelity testing**: Real game with UIAwareAI proving every change works end-to-end
- **CI parity gates**: `Tests/Unit/ParityValidationTests.cs` + `parity_allowlist.json`
- **Iron rule**: Never bypass game systems to make tests pass — fix the game

---

## Implementation Phases

### Phase 1: Testing Infrastructure & Observability Upgrade

**Objective:** Make the full-fidelity testing system the definitive quality oracle so every subsequent phase can be validated automatically. Enhance logging to capture BG3-specific parity metrics.

**Rationale:** Every subsequent phase delivers spells, statuses, and interactions. Without a robust testing system that can report *what works and what doesn't* at BG3 parity level, we'd be flying blind. This phase turns autobattle from a "does it crash?" check into a "does it match BG3?" validator.

**Files to Modify/Create:**
- `Tools/AutoBattler/AutoBattleRuntime.cs`: Add parity metrics collection (abilities used, effects resolved, statuses applied, surfaces created)
- `Tools/AutoBattler/UIAwareAIController.cs`: Add action coverage tracking (which abilities were attempted, which succeeded, which failed validation)
- `Tools/AutoBattler/BlackBoxLogger.cs`: Add structured parity events (`ABILITY_COVERAGE`, `EFFECT_UNHANDLED`, `STATUS_NO_RUNTIME_BEHAVIOR`, `SURFACE_INTERACTION`)
- `Tools/AutoBattler/ParityCoverageReport.cs` (new): Post-battle report generator that outputs a coverage matrix (abilities granted vs used vs failed)
- `scripts/run_autobattle.sh`: Add `--parity-report` flag to generate coverage report after battle
- `scripts/run_parity_sweep.sh` (new): Runs N random short-gameplay scenarios and aggregates coverage reports
- `Tools/AutoBattler/AutoBattleWatchdog.cs`: Add per-ability timeout tracking (which specific ability caused a freeze)
- `Tools/DebugFlags.cs`: Add `ParityReportMode` flag

**Tests to Write:**
- `Tests/Unit/ParityCoverageReportTests.cs`: Verify report generation from mock combat log data
- Full-fidelity run with `--parity-report` producing valid JSON output

**Steps:**
1. Define `ParityCoverageReport` data model (abilities/effects/statuses/surfaces coverage)
2. Add structured parity events to `BlackBoxLogger` (`ABILITY_COVERAGE`, `EFFECT_UNHANDLED`, etc.)
3. Wire `AutoBattleRuntime` to collect parity metrics during combat
4. Enhance `UIAwareAIController` to log when an AI-chosen ability fails for non-tactical reasons (effect unhandled, status missing, etc.)
5. Create `ParityCoverageReport` class that reads `combat_log.jsonl` and produces a summary
6. Add `--parity-report` flag to `run_autobattle.sh`
7. Create `run_parity_sweep.sh` that runs multiple seeds and aggregates
8. Add per-ability freeze tracking to watchdog
9. Run full-fidelity test to verify new logging doesn't break existing flow
10. Run parity sweep to establish the **baseline coverage numbers** for Phase 2+

**Acceptance Criteria:**
- [x] `--parity-report` produces a JSON file with: total abilities granted, total used, total with unhandled effects, total statuses with no runtime behavior
- [x] Parity sweep runs 10+ seeds without crashes
- [x] Coverage baseline document is generated showing current state
- [x] Existing full-fidelity tests still pass
- [x] All new unit tests pass
- [x] Watchdog reports which specific ability caused a freeze (not just "TIMEOUT_FREEZE")

---

### Phase 2: Data Pipeline Fix — Spell Effect Converter & Functor Engine

**Objective:** Fix the broken `CreateSurface` parser, expand `SpellEffectConverter` to handle all major BG3 functor types, and build a functor execution engine for statuses. This is the foundation that unlocks 80% of BG3 spell/status behavior.

**Rationale:** Currently `SpellEffectConverter` only handles 8 functor types. BG3 uses ~40+ distinct functors. The `CreateSurface` arg-order bug silently breaks surface spells. Status functors (OnApply/OnTick/OnRemove) are entirely unimplemented. Fixing these unlocks the vast majority of BG3 spell and status behaviors from *existing parsed data*.

**Files to Modify/Create:**
- `Data/Actions/SpellEffectConverter.cs`: Fix `CreateSurface` signature parsing (radius, duration, surface type order); add handlers for missing BG3 functors: `RegainHitPoints`, `RestoreResource`, `Douse`, `Stabilize`, `Resurrect`, `CreateExplosion`, `SpawnInInventory`, `Counterspell`, `RemoveStatusByGroup`, `GainTemporaryHitPoints`, `FireProjectile`, `Equalize`, `SetStatusDuration`, `ExecuteWeaponFunctors`, `PickupEntity`, `SwapPlaces`, `CreateZoneCloud`, `SetAdvantage`, `SetDisadvantage`, `Grant`, `UseSpell`
- `Data/Actions/FunctorTypes.cs` (new): Enum/registry of all BG3 functor types with parsing metadata
- `Data/Statuses/StatusFunctorEngine.cs` (new): Execute OnApply/OnTick/OnRemove functors on status instances using the same functor parsing as spells
- `Data/Statuses/BG3StatusIntegration.cs`: Wire functor engine into status apply/tick/remove lifecycle
- `Combat/Actions/Effects/Effect.cs`: Add effect handlers for new functor-derived effects
- `Combat/Actions/EffectPipeline.cs`: Register new effect handlers

**Tests to Write:**
- `Tests/Unit/SpellEffectConverterTests.cs`: Test each new functor parser with real BG3 strings
- `Tests/Unit/CreateSurfaceParserTests.cs`: Verify correct arg-order parsing for `GROUND:CreateSurface(4,0,Water)`, `CreateSurface(3,10,Fire)`, etc.
- `Tests/Unit/StatusFunctorEngineTests.cs`: Verify OnApply/OnTick/OnRemove execution for representative statuses
- `Tests/Integration/FunctorCoverageTests.cs`: Verify functor coverage against BG3 parsed data (what % of BG3 spells now convert cleanly)

**Steps:**
1. Write failing tests for `CreateSurface` with real BG3 argument patterns
2. Fix `SpellEffectConverter.ParseCreateSurface()` to handle `(radius, duration, surfaceType)` order
3. Run tests — CreateSurface tests should pass
4. Catalog all BG3 functor types from `BG3_Data/Spells/*.txt` (grep for unique functor names)
5. Prioritize functors by frequency (focus on top 20 that cover 90% of spells)
6. Implement functor parsers in `SpellEffectConverter` one by one with tests
7. Create `StatusFunctorEngine` that reuses the functor parsing infrastructure
8. Wire `StatusFunctorEngine` into `BG3StatusIntegration.ApplyStatus/TickStatus/RemoveStatus`
9. Register new effect handlers in `EffectPipeline`
10. Run parity sweep to measure improvement in functor coverage
11. Update CI parity gates to track functor coverage %

**Acceptance Criteria:**
- [ ] `CreateSurface` correctly parses all BG3 surface creation patterns
- [ ] Top 20 BG3 functors by frequency are parsed and converted to effects
- [ ] Status OnApply/OnTick/OnRemove functors execute for statuses that define them
- [ ] Functor coverage (% of BG3 spells with all functors handled) reaches 70%+
- [ ] CI parity gate reports functor coverage metric
- [ ] Full-fidelity runs show surface spells actually creating surfaces
- [ ] All new unit tests pass

---

### Phase 3: Action/Spell Coverage Expansion

**Objective:** Expand from ~195 curated actions to ~500+ working spells covering all cantrips and spells through level 6 that BG3 classes actually grant. Prioritize spells that classes use, not obscure item-only spells.

**Rationale:** With Phase 2's functor engine in place, many BG3 spells should auto-convert. This phase focuses on verifying auto-converted spells actually work, manually curating the ones that need special handling, and ensuring every class's granted spells are playable.

**Files to Modify/Create:**
- `Data/Actions/SpellEffectConverter.cs`: Handle remaining edge-case functors specific to granted spells
- `Data/Actions/BG3ActionConverter.cs`: Improve conversion for: multi-hit spells (Eldritch Blast, Scorching Ray, Magic Missile), area spells (Fireball, Spirit Guardians), concentration spells (Bless, Hold Person), ritual spells, reaction spells (Shield, Counterspell, Absorb Elements), bonus action spells (Healing Word, Misty Step, Spiritual Weapon)
- `Data/Actions/common_actions.json`: Update/expand with verified common actions (Dash, Dodge, Disengage, Hide, Help, Shove, Throw)
- `Data/Actions/bg3_spells_cantrips.json` (new or expand existing): All cantrips verified working
- `Data/Actions/bg3_spells_level1-2.json` (new or expand): Level 1-2 spells by class
- `Data/Actions/bg3_spells_level3-4.json` (new or expand): Level 3-4 spells by class
- `Data/Actions/bg3_spells_level5-6.json` (new or expand): Level 5-6 spells by class
- `Data/Spells/SpellUpcastRules.cs` (new): Upcasting logic (extra dice/targets per slot level above base)
- `Combat/Actions/Effects/Effect.cs`: New effect types for: multi-projectile, wall creation, terrain modification, teleportation variations, conjuration, illusion effects
- `Data/CharacterModel/CharacterResolver.cs`: Validate all granted abilities resolve to working actions

**Tests to Write:**
- `Tests/Integration/SpellCoverageByClassTests.cs`: For each of the 12 classes, verify all granted spells at levels 1-12 exist in the union registry and have effect handlers
- `Tests/Integration/UpcastingTests.cs`: Verify upcast damage/effect scaling for representative spells
- `Tests/Unit/MultiProjectileTests.cs`: Verify multi-hit spells (Magic Missile, Scorching Ray, Eldritch Blast)
- Per-spell full-fidelity action tests: `--ff-action-test <spell_id>` for critical spells

**Steps:**
1. Generate a "class spell matrix" — for each class at each level (1-12), list granted spells and check which exist in ActionRegistry
2. Write integration tests that verify every granted spell has a working action definition
3. Run tests — capture the gap list of missing/broken spells
4. Auto-convert as many as possible using improved `BG3ActionConverter` + `SpellEffectConverter`
5. For spells requiring special handling (multi-hit, concentration+area, wall creation), implement dedicated effect handlers
6. Implement upcasting rules: map BG3 `SpellProperties` upcast fields to runtime scaling
7. Curate remaining spells into JSON packs organized by level
8. Run `CharacterResolver.Resolve()` for every class/level combo and verify all granted abilities resolve
9. Run parity sweep — measure ability usage success rate
10. Run `--ff-action-test` for each spell category (attack cantrip, save cantrip, level 1 attack, level 1 save, level 1 buff, etc.)

**Acceptance Criteria:**
- [ ] Every class's granted cantrips and spells (levels 1-6) exist in the union registry
- [ ] 90%+ of granted spells have fully handled effects (no `EFFECT_UNHANDLED` in parity report)
- [ ] Upcasting works for damage spells (extra dice) and target spells (extra targets)
- [ ] Multi-hit spells fire correct number of projectiles
- [ ] Concentration spells start concentration, and new concentration spells break the old one
- [ ] Reaction spells (Shield, Counterspell) work through the reaction prompt system
- [ ] Full-fidelity parity sweep shows >80% ability success rate across 50+ seeds
- [ ] CI class-spell coverage gate passes

---

### Phase 4: Status Effect Completeness & Conditions

**Objective:** Expand runtime status coverage from ~108 to ~300+ statuses, implement all BG3 conditions (Prone, Blinded, Frightened, Stunned, etc.), and wire status immunities.

**Rationale:** BG3 combat is heavily status-driven. Spells apply statuses, statuses modify rolls, block actions, force saves, tick damage, etc. Without complete status behavior, spells that "work" at the effect level still produce wrong gameplay because their follow-on statuses don't behave correctly.

**Files to Modify/Create:**
- `Data/Statuses/*.json`: Expand curated status definitions for all conditions and common spell statuses
- `Data/Statuses/BG3StatusIntegration.cs`: Implement status immunity checks (`StatusImmunity` boost), conditional removal (`RemoveEvents` parsing), and status group semantics (`SG_Incapacitated` prevents actions, etc.)
- `Combat/Statuses/StatusSystem.cs`: Add immunity checking before apply, add `RemoveEvent` trigger handling (on save success, on damage, on turn start/end), add status group behavior enforcement
- `Combat/Statuses/ConditionEffects.cs` (new): Hard-coded condition effects that map to BG3 behavior: Prone (advantage on melee attacks against, disadvantage on ranged, costs half movement to stand), Blinded (disadvantage on attacks, advantage against), Frightened (disadvantage on ability checks, can't move closer), Stunned (incapacitated + auto-fail STR/DEX saves + advantage against), Paralyzed (stunned + melee hits are auto-crits), Charmed (can't attack charmer), Invisible (advantage on attacks, disadvantage against), Poisoned (disadvantage on attacks and ability checks), Restrained (disadvantage on attacks and DEX saves, advantage against, speed 0), Petrified (incapacitated, auto-fail STR/DEX saves, resistance to all damage)
- `Combat/Rules/RulesEngine.cs`: Wire condition effects into attack/save/damage roll modifiers via rule windows
- `Combat/Statuses/ConcentrationSystem.cs`: Fix surface-concentration link (breaking concentration removes associated surfaces)
- `Data/Statuses/StatusRegistry.cs`: Add Boost DSL parsing for immunity boosts

**Tests to Write:**
- `Tests/Unit/ConditionEffectTests.cs`: For each D&D condition, verify mechanical effects (advantage/disadvantage/incapacitated/etc.)
- `Tests/Unit/StatusImmunityTests.cs`: Verify immune targets reject status application
- `Tests/Unit/ConcentrationSurfaceTests.cs`: Verify breaking concentration removes the caster's surfaces
- `Tests/Unit/RemoveEventTests.cs`: Verify statuses with save-end/damage-end/turn-end conditions
- `Tests/Integration/StatusBehaviorTests.cs`: End-to-end status application → effect → removal for representative statuses
- Full-fidelity test: Hold Person → target is Paralyzed → melee hits are auto-crits

**Steps:**
1. Write failing tests for each BG3 condition's mechanical effects
2. Implement `ConditionEffects` class with per-condition rule window hooks
3. Wire conditions into `RulesEngine` (attack roll advantage/disadvantage, save modifiers, movement restrictions)
4. Run tests — condition effects should work
5. Implement status immunity checking in `StatusSystem.ApplyStatus()`
6. Implement `RemoveEvents` parsing and trigger hooks (save-on-turn-end, remove-on-damage, etc.)
7. Fix concentration → surface link in `ConcentrationSystem`
8. Expand curated status definitions in JSON for all common BG3 spell statuses
9. Wire `StatusFunctorEngine` (from Phase 2) into more statuses
10. Run parity sweep — verify statuses are applying and behaving correctly
11. Run `--ff-action-test hold_person` to verify the full chain: spell → status → condition → mechanical effect

**Acceptance Criteria:**
- [ ] All 14 D&D 5e conditions are implemented with correct mechanical effects
- [ ] Status immunities prevent application and log the prevention
- [ ] RemoveEvent triggers (save-end, damage-end, turn-end) work for statuses that define them
- [ ] Breaking concentration removes associated surfaces and statuses
- [ ] 300+ BG3 statuses have runtime definitions with correct boosts/conditions
- [ ] Parity sweep shows status application events in >90% of combat rounds
- [ ] Full-fidelity Hold Person test passes end-to-end

---

### Phase 5: Class Progression & Feature Completeness

**Objective:** Ensure all 12 classes (and as many subclasses as data supports) have correct progression tables, class features, and granted abilities at every level 1-12. Add missing subclass data.

**Rationale:** A player starting a game expects their Level 5 Fighter to have Extra Attack, their Level 3 Wizard to have their chosen subclass features, their Level 9 Cleric to have Divine Intervention. The data layer parses progressions, but the gap between "parsed" and "fully working at runtime" must be closed.

**Files to Modify/Create:**
- `Data/Classes/martial_classes.json`, `Data/Classes/arcane_classes.json`, `Data/Classes/divine_classes.json`: Verify and expand progression tables to level 12 for all classes
- `Data/CharacterModel/CharacterResolver.cs`: Add validation that all granted features resolve to working runtime behaviors; handle subclass feature injection correctly
- `Data/CharacterModel/CharacterSheet.cs`: Add spell preparation support (prepared caster lists), proficiency tracking, feature choice tracking (Fighting Styles, Eldritch Invocations, Metamagic, etc.)
- `Data/CharacterModel/SubclassData.cs` (new or expand): Define feature progressions for all 24 extracted subclasses
- `Data/Feats/FeatSystem.cs` (new or expand): Implement feat effects (ASI, GWM, Sharpshooter, War Caster, Lucky, Alert, etc.) — the 21 feats in `FeatDescriptions.lsx`
- `Combat/Entities/Combatant.cs`: Add Extra Attack handling (multiple attacks per Attack action), Spellcasting modifier, Proficiency bonus, Ability Score Improvements
- `Combat/Actions/EffectPipeline.cs`: Support multi-attack resolution (Extra Attack at level 5+), class feature triggers (Action Surge, Cunning Action, Wild Shape placeholder, Channel Divinity, etc.)
- `Data/Passives/bg3_passive_rules.json`: Expand passive definitions for all class features that modify combat behavior

**Tests to Write:**
- `Tests/Integration/ClassProgressionTests.cs`: For each class, verify levels 1-12 grant correct features, spells, and ability increases
- `Tests/Unit/ExtraAttackTests.cs`: Verify Fighters/Paladins/Rangers/etc. get multiple attacks at level 5
- `Tests/Unit/FeatEffectTests.cs`: Verify each implemented feat's mechanical effect
- `Tests/Unit/SpellPreparationTests.cs`: Verify prepared casters can only use prepared spells
- Full-fidelity: Run Level 5 Fighter and verify Extra Attack fires twice per Attack action

**Steps:**
1. Audit `CharacterResolver.Resolve()` output for every class at levels 1, 3, 5, 7, 9, 11, 12
2. Write integration tests that verify granted features match bg3.wiki progression tables
3. Implement Extra Attack in `EffectPipeline` — when Attack action is taken and combatant has Extra Attack feature, allow additional attacks
4. Implement class-specific action features: Action Surge (Fighter), Cunning Action (Rogue), Channel Divinity (Cleric/Paladin), Rage (Barbarian), Wild Shape (Druid — limited/placeholder OK), Ki Points (Monk), Bardic Inspiration (Bard), Metamagic (Sorcerer), Eldritch Invocations (Warlock), Favored Enemy/Natural Explorer (Ranger)
5. Implement feat effects for the 21 feats in data
6. Add spell preparation tracking for prepared casters
7. Expand passive definitions for all class features that hook into rule windows
8. Validate all 24 extracted subclasses have correct feature progressions
9. Run full-fidelity with every class at level 5 to verify core features work
10. Run parity sweep with --character-level 1, 5, 10, 12 to cover progression range

**Acceptance Criteria:**
- [ ] All 12 classes resolve correct features at every level 1-12
- [ ] Extra Attack works correctly for martial classes at level 5+
- [ ] Class-specific action resources (Ki, Sorcery Points, Channel Divinity, Rage, etc.) are tracked and consumed
- [ ] All 21 feats have implemented mechanical effects
- [ ] Prepared casters can only use prepared spells (Cleric, Druid, Paladin, Wizard)
- [ ] 24 extracted subclasses have working feature progressions
- [ ] Full-fidelity tests pass for all 12 classes at level 5
- [ ] CI class-progression test suite passes

---

### Phase 6: Equipment, Weapons & Inventory System

**Objective:** Implement a functional equipment system where weapons, armor, and items affect combat stats, grant abilities (weapon actions), and can be managed through a UI.

**Rationale:** BG3 combat is deeply tied to equipment. Weapons determine damage dice and grant weapon actions (Pommel Strike, Lacerate, Topple, etc.). Armor affects AC. Magic items grant spells, resistances, and stat bonuses. Without equipment, combat stats are static and don't reflect the BG3 experience.

**Files to Modify/Create:**
- `Combat/Entities/EquipmentComponent.cs` (new): Manages equipped items (main hand, off hand, armor, helmet, gloves, boots, amulet, ring x2, cloak)
- `Combat/Entities/Combatant.cs`: Wire `EquipmentComponent` into stat calculation (AC, damage dice, proficiency, granted abilities)
- `Data/Stats/WeaponData.cs` (new or expand): Parse `Stats/Weapon.txt` into typed weapon records with damage dice, properties (finesse, light, heavy, two-handed, reach, thrown), proficiency requirements, and `BoostsOnEquipMainHand` (weapon action grants)
- `Data/Stats/ArmorData.cs` (new or expand): Parse `Stats/Armor.txt` into typed armor records with AC calculation, disadvantage on stealth, etc.
- `Data/Stats/StatsRegistry.cs`: Register weapon/armor data for runtime lookup
- `Combat/UI/InventoryPanel.cs` (new): Equipment display showing slots, stats, and equip/unequip
- `Combat/UI/HudController.cs`: Wire inventory panel into HUD
- `Combat/Actions/ActionRegistry.cs`: Dynamically add/remove weapon actions when equipment changes
- `Data/Actions/WeaponActionLoader.cs` (new): Convert weapon action spell refs (from `BoostsOnEquipMainHand`) into usable actions

**Tests to Write:**
- `Tests/Unit/EquipmentComponentTests.cs`: Verify equip/unequip modifies stats correctly
- `Tests/Unit/WeaponActionTests.cs`: Verify weapon actions are granted on equip, removed on unequip
- `Tests/Unit/ArmorACTests.cs`: Verify AC calculation for light/medium/heavy armor
- `Tests/Integration/WeaponActionCombatTests.cs`: Verify weapon actions work in full combat flow
- Full-fidelity: Combatant with equipped longsword shows Pommel Strike in action bar

**Steps:**
1. Parse `Stats/Weapon.txt` and `Stats/Armor.txt` into typed data records
2. Create `EquipmentComponent` with slot management
3. Wire equipment into combatant stat calculation (damage dice from weapon, AC from armor)
4. Implement weapon action grants from `BoostsOnEquipMainHand`
5. Create weapon action definitions for the 22 unique weapon action spell IDs
6. Add inventory panel UI to HUD
7. Wire equipment changes to action bar updates (add/remove weapon actions)
8. Add equipment to scenario definitions (units can specify equipped items)
9. Run full-fidelity to verify weapon actions appear and work
10. Run parity sweep to verify equipment doesn't break balance

**Acceptance Criteria:**
- [ ] Weapons affect damage dice and attack roll modifiers
- [ ] Armor affects AC calculation correctly (light/medium/heavy formulas)
- [ ] Weapon actions are granted by equipped weapons and appear in action bar
- [ ] Equipped items display in inventory panel
- [ ] Equipment changes dynamically update combatant stats and available actions
- [ ] Scenarios can define equipped items per unit
- [ ] Full-fidelity test passes with equipped units

---

### Phase 7: Environment, Surfaces & Interactive Objects

**Objective:** Complete the surface interaction system, add environmental objects (barrels, crates, traps), and implement forced movement interactions (push into surfaces, fall damage from heights).

**Rationale:** BG3's tactical combat is defined by environmental interaction. Grease + Fire = explosion. Standing in water when hit by Lightning = splash damage. Pushing enemies off ledges. Exploding barrels. These interactions are core gameplay, not flavor.

**Files to Modify/Create:**
- `Data/Actions/SpellEffectConverter.cs`: Fix all remaining `CreateSurface` patterns from BG3 data
- `Combat/Environment/SurfaceManager.cs`: Complete interaction matrix (all BG3 surface combinations), add cloud surfaces (Fog, Stinking Cloud), add surface visual representation improvements
- `Combat/Environment/SurfaceDefinition.cs`: Add all BG3 surface types with correct damage/status/movement effects
- `Combat/Environment/EnvironmentalObjects.cs` (new): Destructible objects (barrels — fire/water/poison/explosive), interactable objects (levers, traps, doors), physics containers (can be thrown/pushed)
- `Combat/Movement/ForcedMovementService.cs` (verify/expand): Collision with obstacles, push into surfaces, fall damage calculation from height differences, knockback distance calculation
- `Combat/Environment/HeightService.cs`: Complete fall damage integration with forced movement
- `Combat/Environment/LOSService.cs`: Surfaces affect line of sight (smoke, fog)
- `Combat/Arena/CombatArena.cs`: Wire environmental objects into scene, support pre-placed objects in scenarios

**Tests to Write:**
- `Tests/Unit/SurfaceInteractionMatrixTests.cs`: Test every surface combination (Fire+Water=Steam, Water+Lightning=Electrified, Grease+Fire=Explosion, Ice+Fire=Water, Poison+Fire=Explosion, etc.)
- `Tests/Unit/ForcedMovementSurfaceTests.cs`: Verify pushing into surfaces triggers surface effects
- `Tests/Unit/FallDamageTests.cs`: Verify correct fall damage from forced movement off heights
- `Tests/Unit/EnvironmentalObjectTests.cs`: Verify barrel destruction, explosion radius, trap trigger
- Full-fidelity: Fireball hits a grease surface and creates fire, then fire barrel in range explodes

**Steps:**
1. Catalog all BG3 surface types and their interaction rules from bg3.wiki
2. Complete the surface interaction matrix in `SurfaceManager`
3. Add cloud surface support (blocks LOS, applies effects while inside)
4. Implement environmental objects (barrels as scene entities with HP and explosion effects)
5. Wire forced movement through surfaces (sliding on ice, pushed into fire)
6. Verify fall damage from forced movement (push off ledge)
7. Add environmental objects to scenario definitions
8. Fix `CreateSurface` spell effects to produce correct surface types (depends on Phase 2)
9. Run full-fidelity with environmental objects in scenario
10. Run parity sweep to verify surface interactions are stable

**Acceptance Criteria:**
- [ ] All BG3 surface combinations produce correct results (Fire+Water=Steam, etc.)
- [ ] Cloud surfaces block LOS and apply effects
- [ ] Destructible objects (barrels) can be destroyed and produce area effects
- [ ] Forced movement through surfaces triggers surface effects
- [ ] Fall damage applies correctly from forced movement
- [ ] Surfaces affect LOS (fog, smoke clouds)
- [ ] Full-fidelity tests pass with complex surface scenarios
- [ ] Parity sweep with environmental objects runs 50+ seeds without crashes

---

### Phase 8: UI/UX BG3 Parity

**Objective:** Make the player-facing UI look and feel like BG3 combat. Spellbook, character sheet, tooltips, targeting feedback, action bar organization, hotbar, and reaction prompts should match BG3's UX patterns.

**Rationale:** A player opening the game should immediately recognize the BG3 combat UI paradigm. The action bar should organize spells by level, show spell slots remaining, display cooldowns, differentiate between available/unavailable actions. Tooltips should show detailed effect descriptions. Targeting should show AoE previews, range indicators, and hit probability.

**Files to Modify/Create:**
- `Combat/UI/HudController.cs`: Add spellbook panel, hotbar customization, tooltip system, hit probability display
- `Combat/UI/ActionBarModel.cs`: Organize actions by category (Common, Cantrips, Level 1-6 Spells, Class Features, Weapon Actions, Items), show spell slot indicators, cooldown tracking
- `Combat/UI/SpellbookPanel.cs` (new): Full spellbook showing all known spells, preparation interface for prepared casters, spell descriptions, upcast options
- `Combat/UI/TooltipSystem.cs` (new): Rich tooltips for abilities (damage dice, save DC, range, duration, description, components), for combatants (stats, conditions, HP), for surfaces (effect, duration)
- `Combat/UI/HitProbabilityDisplay.cs` (new): Show % hit chance when hovering a target with an attack selected (like BG3's hit chance indicator)
- `Combat/UI/InitiativeRibbon.cs` (verify/expand): Show conditions on portraits, death save indicators, concentration indicator
- `Combat/Arena/ReactionPromptUI.cs`: Add details to reaction prompt (what triggered it, what the reaction does, timer)
- `Combat/Targeting/TargetingUI.cs` (new or expand): AoE ground preview, valid/invalid target coloring, range circle, movement path preview with remaining distance

**Tests to Write:**
- `Tests/Unit/ActionBarModelOrganizationTests.cs`: Verify actions are correctly categorized and spell slot indicators are accurate
- `Tests/Unit/TooltipContentTests.cs`: Verify tooltip content matches ability definitions
- `Tests/Unit/HitProbabilityCalculationTests.cs`: Verify displayed hit % matches `RulesEngine` calculation
- Full-fidelity: UIAwareAI verifies action bar shows correct abilities for the loaded character

**Steps:**
1. Refactor `ActionBarModel` to organize by BG3 categories
2. Add spell slot indicators per spell level
3. Implement tooltip system with rich content
4. Implement hit probability calculation and display
5. Add condition indicators to initiative ribbon portraits
6. Implement spellbook panel for prepared casters
7. Improve targeting feedback (AoE preview, range indicator, valid/invalid coloring)
8. Add details to reaction prompt (trigger description, reaction name, timer)
9. Improve initiative ribbon with death save and concentration indicators
10. Run full-fidelity to verify UI displays correct data

**Acceptance Criteria:**
- [ ] Action bar organizes abilities by type (attacks, cantrips, leveled spells 1-6, class features, weapon actions)
- [ ] Spell slot indicators show remaining slots per level
- [ ] Tooltips show: name, description, damage/effect, range, duration, save type, components
- [ ] Hit probability displays as percentage when hovering targets
- [ ] Initiative ribbon shows conditions, death saves, and concentration on portraits
- [ ] Spellbook panel allows spell preparation for prepared casters
- [ ] AoE previews show affected area on ground
- [ ] Reaction prompts include trigger context and reaction description
- [ ] Full-fidelity UIAwareAI can interact with all new UI elements

---

### Phase 9: Death, Rest, Difficulty & Game Flow

**Objective:** Implement complete death/downed mechanics, short/long rest with UI, difficulty mode selection (Explorer/Balanced/Tactician/Honour), and the between-combat game flow.

**Rationale:** BG3 combat doesn't exist in isolation. Characters get downed and make death saves. Between fights, parties rest to recover resources. Difficulty modes change enemy HP, damage, and rules. These systems define the *game experience* beyond individual combat encounters.

**Files to Modify/Create:**
- `Combat/Entities/DeathSaveComponent.cs` (new or verify): Full death save flow: 3 successes → stabilize, 3 failures → death, natural 20 → regain 1 HP, damage while downed → death save failure, healing while downed → conscious at healed HP
- `Combat/Entities/Combatant.cs`: Wire death save component, add `Downed` state with correct inability rules, NPC instant death at 0 HP
- `Combat/Services/RestService.cs`: Implement short rest (hit dice healing, resource recovery, weapon action recharge), long rest (full HP, all resources, spell slots, remove most statuses)
- `Combat/UI/RestPanel.cs` (new): UI for triggering short/long rest, showing what recovers, hit dice management
- `Combat/UI/DeathSaveUI.cs` (new): Visual death save tracker on downed unit's portrait/panel
- `Data/DifficultySettings.cs` (new): Parse `Rulesets.lsx` + `RulesetModifiers.lsx` into difficulty profiles (HP multiplier, damage multiplier, special rules)
- `Combat/Arena/CombatArena.cs`: Apply difficulty modifiers to combat initialization
- `Combat/UI/DifficultySelector.cs` (new): Pre-combat difficulty selection UI

**Tests to Write:**
- `Tests/Unit/DeathSaveTests.cs`: All death save outcomes (3 success, 3 fail, nat 20, damage while downed, heal while downed, stabilize)
- `Tests/Unit/RestServiceTests.cs`: Verify short rest resource recovery, long rest full recovery
- `Tests/Unit/DifficultyModifierTests.cs`: Verify difficulty profiles change HP/damage correctly
- Full-fidelity: Unit drops to 0 HP, enters downed state, makes death saves, and either dies or is healed

**Steps:**
1. Write death save tests covering all BG3 outcomes
2. Implement `DeathSaveComponent` with full death save logic
3. Wire downed state into combat flow (skip turn, allow healing, death save on turn)
4. Add death save UI to HUD
5. Implement NPC instant death at 0 HP (configurable per entity type)
6. Expand `RestService` with short rest hit dice healing and resource recovery
7. Add long rest full recovery
8. Create rest UI panel
9. Parse difficulty settings from BG3 data
10. Implement difficulty modifier application to combat
11. Add difficulty selector to pre-combat UI
12. Run full-fidelity with death save scenarios

**Acceptance Criteria:**
- [ ] PCs enter Downed at 0 HP and make death saves on their turns
- [ ] 3 successes → stabilize, 3 failures → death, nat 20 → 1 HP
- [ ] Damage while downed counts as death save failure; melee crits = 2 failures
- [ ] NPCs die at 0 HP (no death saves)
- [ ] Short rest recovers hit dice health and short-rest resources
- [ ] Long rest recovers all HP, spell slots, and resources
- [ ] Difficulty modes visibly change enemy stats and combat behavior
- [ ] Full-fidelity death save scenario passes

---

### Phase 10: Character Creation, Scenario Builder & Polish

**Objective:** Add character creation UI, scenario builder for custom encounters, and final polish: performance, visual effects, sound stubs, and the "first five minutes" experience.

**Rationale:** The user wants to "start the game in Godot and have access to all BG3 combat features." That means a character creation screen (pick race, class, ability scores), a way to start encounters, and a polished enough experience that it feels like a game, not a tech demo.

**Files to Modify/Create:**
- `Combat/UI/CharacterCreation/CharacterCreationUI.cs` (new): Race selection (all BG3 races from `Races.lsx`), class selection (12 classes), subclass selection, ability score assignment (point buy), feat selection, spell preparation for prepared casters, name input
- `Combat/UI/CharacterCreation/RaceSelectionPanel.cs` (new): Visual race picker with racial traits
- `Combat/UI/CharacterCreation/ClassSelectionPanel.cs` (new): Visual class picker with feature preview
- `Combat/UI/CharacterCreation/AbilityScorePanel.cs` (new): Point buy ability score allocation
- `Data/CharacterModel/CharacterBuilder.cs` (new): Programmatic character construction from UI choices
- `Combat/UI/ScenarioBuilder/ScenarioBuilderUI.cs` (new): Custom encounter setup: party composition, enemy selection, environment, difficulty
- `Combat/Arena/CombatArena.cs`: Support launch from character creation → scenario → combat flow
- `assets/Scenes/MainMenu.tscn` (new or expand): Main menu with: New Game, Quick Battle, Scenario Builder, Settings
- Various VFX/shader improvements for spell visuals, surface rendering, hit/miss indicators

**Tests to Write:**
- `Tests/Unit/CharacterBuilderTests.cs`: Verify character creation produces valid `CharacterSheet` for all race/class combinations
- `Tests/Integration/CharacterCreationFlowTests.cs`: Verify full creation → combat flow
- `Tests/Integration/EndToEndExperienceTests.cs`: From main menu → character creation → battle → victory
- Full-fidelity: Start from main menu, create a character, enter combat, use class abilities, win/lose

**Steps:**
1. Design character creation flow (race → class → subclass → abilities → feats → spells → name)
2. Implement race selection panel with racial trait display
3. Implement class selection panel with feature preview and subclass picker
4. Implement ability score point buy (27 points, BG3 standard)
5. Implement feat selection at appropriate levels
6. Implement spell preparation for prepared casters during creation
7. Create `CharacterBuilder` that assembles a valid `CharacterSheet`
8. Create scenario builder UI (party + enemies + environment + difficulty)
9. Create main menu scene with navigation to all modes
10. Polish: spell VFX improvements, surface rendering, hit/miss floating text, combat log panel
11. Run full end-to-end flow: menu → create → fight → result
12. Final parity sweep: 100 seeds at various levels and classes

**Acceptance Criteria:**
- [ ] Player can create a character choosing race, class, subclass, ability scores, and feats
- [ ] Created character has all correct abilities for their build
- [ ] Scenario builder allows custom party vs enemy encounters
- [ ] Main menu navigates to character creation, quick battle, and scenario builder
- [ ] Full end-to-end flow works: menu → create → fight → victory/defeat
- [ ] Visual polish: spell effects, surface rendering, floating damage numbers, combat log
- [ ] Final parity sweep: 100+ seeds across all 12 classes at levels 1, 5, 10, 12 pass without crashes
- [ ] Parity coverage report shows >90% ability resolution success across all tested scenarios

---

## Open Questions

1. **Subclass data gap (24 vs 58)?**  
   - **Option A:** Refresh BG3 data extraction to include all 58 subclasses. Requires re-running extraction tools against current BG3 version.
   - **Option B:** Scope parity to the 24 extracted subclasses and document the 34 missing ones as known limitations.
   - **Recommendation:** Start with Option B (scope to 24) for velocity, but track Option A as a future data refresh task. 24 subclasses covering 2 per class is already a strong offering.

2. **Summon spells — include or exclude?**  
   - **Option A:** Implement summoned creature lifecycle (spawn entity, AI control, turn integration, despawn on concentration break/duration end).
   - **Option B:** Keep summons forbidden in parity scope initially; focus on direct combat spells.
   - **Recommendation:** Option B for initial parity. Summons require a complete sub-entity lifecycle. Add as Phase 11 stretch goal.

3. **Wild Shape — full transform or simplified?**  
   - **Option A:** Full stat block replacement with beast forms, new action bars, revert on 0 HP.
   - **Option B:** Simplified bonus HP pool with flavor but same action bar.
   - **Recommendation:** Option A (correct behavior) but limit to 2-3 beast forms initially. It's a class-defining feature for Druid.

4. **XP table gap (only to level 5)?**  
   - **Option A:** Source from bg3.wiki and extend to level 12.
   - **Option B:** Use milestone leveling (no XP, just set level).
   - **Recommendation:** Option A for completeness, but since combat parity doesn't require XP tracking *during* a fight, this is low priority and can use milestone leveling as interim.

5. **Dialogue and exploration — in scope?**  
   - **Recommendation:** Out of scope. This plan is combat parity only. Exploration/dialogue is a separate epic.

6. **BG3 data version pinning?**  
   - **Recommendation:** Pin to current extraction. Document the date and version in `BG3_DATA_PROVENANCE.md` before Phase 1 completes. All parity is against this pinned version.

---

## Risks & Mitigation

- **Risk:** Functor coverage plateau — some BG3 functors may be deeply engine-specific and hard to replicate  
  - **Mitigation:** Track functor coverage %. Allowlist truly impossible functors. Focus on the ~20 functors that cover 90% of spells.

- **Risk:** Performance degradation with 500+ actions and 300+ statuses loaded  
  - **Mitigation:** Lazy-load actions by class. Only register statuses that are referenced by loaded spells. Profile with parity sweep.

- **Risk:** Full-fidelity test instability as more systems interact  
  - **Mitigation:** Phase 1 establishes robust per-ability timeout tracking. Every subsequent phase runs parity sweeps. Regressions are caught early.

- **Risk:** UI complexity explosion — 12 classes × 12 levels × equipment × conditions  
  - **Mitigation:** Focus on data-driven UI (generate panels from data, don't hardcode per class). Use BG3 wiki screenshots as reference.

- **Risk:** Subclass features that require systems not yet built (e.g., Familiar, Beast Companion)  
  - **Mitigation:** Document unsupported features in allowlist. Prioritize subclass features that work within existing combat systems.

- **Risk:** Equipment system creates massive balance/testing surface  
  - **Mitigation:** Start with base weapons/armor only (no magic items). Add magic items incrementally.

---

## Success Criteria

- [ ] All 12 BG3 classes are playable at levels 1-12 with correct progression
- [ ] 500+ spells/actions work correctly with verifiable effects
- [ ] 300+ status effects have correct runtime behavior
- [ ] All BG3 conditions (Prone, Blinded, Stunned, etc.) have correct mechanical effects
- [ ] Surface interactions work (Fire+Water=Steam, Grease+Fire=Explosion, etc.)
- [ ] Equipment affects combat stats and grants weapon actions
- [ ] Death saves work for PCs, NPCs die at 0 HP
- [ ] Short/long rest recover appropriate resources
- [ ] Character creation screen allows race/class/ability score selection
- [ ] Action bar, tooltips, and spellbook match BG3 UX patterns
- [ ] Full-fidelity parity sweep passes 100+ seeds across all classes
- [ ] CI parity gates enforce data correctness and coverage thresholds
- [ ] A user can start the game, create a character, and fight a BG3-quality combat encounter

---

## Notes for Atlas

### Execution Order & Dependencies

```
Phase 1 (Testing) ──────────────────────────────────────────────────────────────┐
    │                                                                            │
Phase 2 (Functor Engine) ───── Phase 3 (Spell Coverage) ──── Phase 5 (Classes) │ All use
    │                               │                              │            │ parity
Phase 4 (Statuses/Conditions) ─────┘                              │            │ sweep
    │                                                              │            │ from
Phase 6 (Equipment) ──────────────────────────────────────────────┘            │ Phase 1
    │                                                                            │
Phase 7 (Environment) ──────── Phase 8 (UI/UX) ──── Phase 9 (Death/Rest/Diff) │
    │                               │                              │            │
    └───────────────────────── Phase 10 (Character Creation & Polish) ──────────┘
```

- **Phase 1 is a prerequisite for everything** — it establishes the testing harness
- **Phase 2 before 3** — functor engine must exist before expanding spell coverage
- **Phase 2 before 4** — status functors depend on functor engine
- **Phase 3 and 4 can partially overlap** — spell coverage and status coverage are somewhat independent
- **Phase 5 depends on 3** — class features grant spells that must work
- **Phase 6 can start after 5** — equipment augments characters that have correct progression
- **Phase 7 depends on 2** — environment depends on working CreateSurface
- **Phase 8 can start after 5** — UI needs working actions/classes to display
- **Phase 9 is relatively independent** — death saves and rest are standalone systems
- **Phase 10 is the final integration** — everything must work before polish

### Testing Strategy Per Phase

Every phase MUST:
1. Run `./scripts/ci-build.sh` (build gate)
2. Run `./scripts/ci-test.sh` (unit/integration tests)
3. Run `./scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay` (basic full-fidelity)
4. Run parity sweep (from Phase 1) to measure coverage improvement
5. Compare parity metrics to previous phase baseline

### Iron Rule Reminder

**NEVER** bypass game systems in autobattle/testing code. If a test fails, the game has a bug. Fix the game, not the test. See `AGENTS-FULL-FIDELITY-TESTING.md` for the complete list of forbidden workarounds.

### Existing Roadmap Alignment

This plan supersedes and expands the existing `docs/ROADMAP.md` streams:
- Stream 1 (CI parity gates) → Phase 1 + throughout
- Stream 2 (BG3 templates) → Phase 5 
- Stream 3 (Forced movement) → Phase 7
- Stream 4 (Toggleable passives) → Phase 5
- Stream 5 (Shove/AoE UX) → Phase 7 + Phase 8
- Stream 6 (Coverage hardening) → Phase 3 + Phase 4

### Context Budget

Each phase should be treated as an independent Atlas execution. Do not try to implement multiple phases at once. Complete one phase, verify with parity sweep, then proceed to the next. Long phases (3, 5, 8) can be split into sub-phases if needed.
