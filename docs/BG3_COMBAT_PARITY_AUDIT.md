# BG3 Combat Parity Audit — Comprehensive Gap Analysis

> **Date**: 2026-02-18
> **Method**: Full codebase audit + BG3 wiki cross-reference
> **Scope**: Every Combat/, Data/, BG3_Data/ file read and assessed
> **Purpose**: Identify all remaining work for BG3 combat parity, ordered by foundational impact

---

## Executive Summary

The combat engine core is production-quality. The state machine, action economy, damage pipeline, attack/save resolution, turn queue, death saves, concentration, surfaces, LOS/cover/height, persistence, and AI are all deeply implemented and interconnected.

**However, the "88% parity" estimate from the first-pass inventory is misleading.** While the *engine architecture* is ~88% complete, the *content coverage* (spells, statuses, passives, class feature mechanics) is far lower. There is a large gap between data being *defined* (JSON/class files exist) and being *mechanically wired* (actually affecting combat).

### Honest Parity Assessment

| Layer | Parity | Notes |
|-------|--------|-------|
| **Engine Architecture** | ~90% | State machine, pipelines, services, AI, persistence, rules |
| **Data Definitions** | ~95% | Classes, races, feats, weapons, armor all at 100% |
| **Spell Content** | ~35% | 209 spell actions vs ~300+ BG3 spells; levels 7-9 completely missing |
| **Status Content** | ~19% | 204/1,082 BG3 statuses defined; 153 with actual mechanics |
| **Passive Mechanics** | ~2.5% | 10/418 passives mechanically wired; no general-purpose interpreter |
| **Class Feature Wiring** | ~40% | Data says "Evasion" but combat engine doesn't halve damage |
| **Overall Functional Combat** | ~55% | A fight runs, but most class-specific abilities silently do nothing |

---

## The 17 Remaining Work Areas, Prioritized by Foundation & Impact

### Priority Tier 1: FOUNDATIONAL (Must be done first — everything else builds on these)

---

#### 1. General-Purpose Passive/Boost Interpreter Engine
**Gap**: The single biggest systemic hole. BG3's combat depth comes from 418+ passives using `StatsFunctors`, `Conditions`, and Boost strings like `Resistance(Bludgeoning,Half)`, `Advantage(Ability.Strength)`. Currently only 10 passives are hand-coded as `PassiveRuleProvider` implementations. Every new class feature, feat, subclass ability, and equipment bonus requires manual coding.

**What exists**: `BoostParser` can parse some Boost format strings. `BoostApplicator` applies stat-level boosts. `ConditionEvaluator` handles boolean condition checks (with some stubs). `FunctorExecutor` handles some BG3 functor types.

**What's needed**:
- Extend `BoostEvaluator` to handle `Resistance(Type,Level)` boost strings → feed into DamagePipeline
- Extend `BoostEvaluator` to handle `Advantage(AbilityCheck/SavingThrow)` boosts → feed into RulesEngine
- Wire `PassiveManager` to auto-generate `PassiveRuleProvider` instances from parsed BG3 `StatsFunctors` data
- Fill `ConditionEvaluator` stubs: `HasProficiency()`, `IsConcentrating()`, `IsInMeleeRange()`, `WearingArmor()`
- This is the "force multiplier" — once this works, dozens of passives come alive without individual coding

**Estimated scope**: Large (architectural). ~2000 lines of interpreter + test coverage.

**Why first**: Every class feature, feat, and equipment passive depends on this. Without it, each of the 400+ passives needs individual hand-coding.

**Agent instructions**: 
- Start from `Combat/Rules/Boosts/BoostEvaluator.cs` and `BoostParser.cs`
- Study `BG3_Data/Stats/Passive.txt` for the full format specification
- Study `Combat/Rules/Functors/FunctorExecutor.cs` for existing functor handling
- Key test: After implementation, load `bg3_passive_rules.json` entries and verify they affect combat without hand-coded providers
- Must pass `scripts/ci-build.sh` and `scripts/ci-test.sh`

---

#### 2. Status Effect Content Pipeline — Scale to BG3 Coverage
**Gap**: 204/1,082 BG3 statuses implemented (19%). Many spells apply statuses that don't exist, silently failing. 57 of the 204 existing statuses lack mechanical modifiers.

**What exists**: Full `StatusSystem` with duration, stacking, tick effects, trigger effects, repeat saves, condition immunity, and BG3StatusIntegration. 5 JSON files with status definitions.

**What's needed**:
- **Phase 1**: Add modifiers to the 57 data-only statuses
- **Phase 2**: Import the most combat-critical BG3 statuses (those referenced by existing spell actions). Cross-reference `bg3_mechanics_actions.json` and `bg3_spells_*.json` to find every `statusId` that's applied but has no definition → create definitions
- **Phase 3**: Systematically expand from `Status_BOOST.txt` (845 entries) and `Status_EFFECT.txt` (69 entries)
- Priority statuses: all concentration spell effects (Haste, Bless, Shield of Faith, Spirit Guardians, etc.), all condition-granting statuses (Hold Person → Paralyzed, Command → specific behaviors)

**Estimated scope**: Medium-Large. ~100 status definitions needed for core combat parity.

**Agent instructions**:
- Read all `Data/Actions/bg3_*.json` files, extract every `statusId` referenced
- Cross-reference against `Data/Statuses/*.json` to find missing statuses
- For each missing status, check `BG3_Data/Statuses/Status_*.txt` for the raw BG3 definition
- Create new JSON status files with proper modifiers, duration, stacking rules
- Validate with `scripts/ci-build.sh`

---

#### 3. Complete the 16 Stubbed Effect Handlers
**Gap**: 16 effect types in `EffectPipeline.cs` are NoOp stubs. Any spell using these effects silently does nothing.

**Stubbed handlers (in priority order)**:
1. `surface_change` — Critical for surface interaction spells (e.g., Tidal Wave extinguishing fire)
2. `execute_weapon_functors` — Needed for weapon-augmenting spells (Smites, weapon attack spells)
3. `set_advantage` / `set_disadvantage` — Core mechanical effects for many spells/abilities
4. `create_wall` — Wall of Fire, Wall of Stone, Spirit Guardians zone
5. `create_zone` — Persistent area effects
6. `fire_projectile` — Multi-projectile spells (Scorching Ray secondary, Magic Missile)
7. `spawn_extra_projectiles` — Upcast mechanics for multi-projectile spells
8. `douse` — Extinguishing fire surfaces/burning condition
9. `swap_places` — Misty Step variant, some class features
10. `remove_status_by_group` — Dispel Magic, Remove Curse mechanics
11. `spawn_inventory_item` — Conjure/create item spells
12. `set_status_duration` — Extend/reduce status durations
13. `equalize` — Balance HP between targets
14. `pickup_entity` / `grant` / `use_spell` / `switch_death_type` — Niche but referenced

**Agent instructions**:
- Read `Combat/Actions/EffectPipeline.cs` for the NoOp registration pattern
- Implement each handler following the pattern of existing handlers (e.g., `spawn_surface`, `forced_move`)
- `set_advantage`/`set_disadvantage` should apply through `BoostContainer` or `ModifierStack`
- `create_wall` needs a new "wall" surface type with collision/blocking
- `surface_change` should modify existing `SurfaceInstance` definitions via `SurfaceManager`
- Test each handler individually, then integration test with spells that use them

---

### Priority Tier 2: HIGH IMPACT (Enables class combat identity)

---

#### 4. Class Feature Mechanical Wiring — Top 30 Features
**Gap**: Many class features are defined in progression data but have no combat effect. These are the core identity of each class.

**Priority features to wire (grouped by class):**

**Barbarian**:
- Rage: Physical damage resistance (Bludgeoning/Slashing/Piercing halved) — requires Resistance boost interpretation (→ ties into Task 1)
- Rage: Advantage on STR checks/saves
- Rage: Auto-end if turn passes without attacking/taking damage
- Brutal Critical (L9): Extra damage die on critical hits
- Unarmoured Movement (L5+): +10ft → +20ft speed bonus

**Rogue**:
- Evasion (L7): DEX save AoE → half damage on fail, zero on success
- Cunning Action: Dash/Disengage/Hide as bonus action (defined but Hide is the gap)

**Fighter** (Champion):
- Remarkable Athlete (L7): Half proficiency to STR/DEX/CON checks without proficiency
- Superior Critical (L15 → not in BG3 L12 cap, skip)

**Monk**:
- Martial Arts: Use DEX instead of STR for monk weapons + unarmed
- Ki-Empowered Strikes: Unarmed strikes count as magical
- Deflect Missiles: Reaction to reduce ranged attack damage
- Unarmoured Movement: +10ft → +20ft speed bonus
- Stillness of Mind: Action to remove charmed/frightened

**Paladin**:
- Aura of Protection: ✅ Already wired
- Aura of Courage (L10): Immunity to frightened within 10ft
- Improved Divine Smite (L11): Extra 1d8 radiant on every melee hit

**Ranger**:
- Favoured Enemy: Language/skill proficiency (minor in combat)
- Natural Explorer: Difficult terrain immunity
- Colossus Slayer / Horde Breaker / Volley (Hunter subclass features)

**Sorcerer**:
- Careful Spell, Heightened Spell, Distant Spell, Extended Spell, Subtle Spell metamagics
- Draconic Resilience: 13+DEX AC when unarmoured (like Mage Armor permanent)

**Warlock**:
- Eldritch Invocations: Only 6/20+ defined. Key missing: Book of Ancient Secrets, Thirsting Blade (Extra Attack for Blade Pact), Lifedrinker
- Pact Boon mechanics: Blade/Chain/Tome — Blade grants weapon summon, Chain grants familiar, Tome grants ritual casting

**Bard**:
- Song of Rest: Short rest healing improvement
- Countercharm: Advantage vs. frightened/charmed for party

**Druid**:
- Natural Recovery: Recover spell slots on short rest (like Arcane Recovery)

**Agent instructions**:
- For each feature, determine if it's a PassiveRuleProvider (rule window hook), a Boost (stat modifier), an OnHitTrigger (inline check), or a Reaction (interrupt system)
- Wire using the lightest-touch approach: prefer Boost strings where possible (Task 1 dependency), PassiveRuleProvider for rule-window hooks, inline checks as last resort
- Test each feature in isolation, then verify with auto-battle scenarios

---

#### 5. Spell Level 7-9 — High-Level Spells
**Gap**: Zero spells at levels 7, 8, or 9 exist. BG3 has ~25+ high-level spells that are key to the late-game fantasy.

**BG3 Level 7+ spells (must implement)**:
- **Level 7**: Delayed Blast Fireball, Finger of Death, Fire Storm, Prismatic Spray, Project Image (?), Regenerate
- **Level 8**: Abi-Dalzim's to Horrid Wilting, Dominate Monster, Earthquake (?), Feeblemind, Incendiary Cloud, Maze (?), Sunburst
- **Level 9**: Power Word Kill, Wish (BG3 has limited implementation), Blade of Disaster, Astral Projection (?)

**Note**: BG3 level cap is 12, so characters only reach 6th-level spell slots. Level 7-9 spells are NOT available in standard BG3 gameplay. **However**, some are available via scrolls, special items, or Illithid powers. The game data files include level 7+ spell data indicating they were planned/partially available.

**REVISED PRIORITY**: Check if BG3 actually uses level 7+ spells in combat. If not, this drops to Tier 3.

**Agent instructions**:
- Verify which Level 7+ spells are actually usable in BG3 combat (scrolls, items, abilities)
- Create ActionDefinition JSON entries following the pattern in `bg3_spells_high_level.json`
- Ensure corresponding status effects exist (Task 2 dependency)
- Test with scenarios featuring level 11-12 casters

---

#### 6. Expand Spell Content: Levels 4-6 Gap Fill
**Gap**: While 92 leveled spells exist, many important BG3 spells are missing at each level. BG3 has roughly 300+ distinct castable spells.

**Critical missing spells by level**:
- **Cantrips**: Blade Ward, Chill Touch (partially?), Friends, Dancing Lights, Minor Illusion, Prestidigitation, True Strike (BG3 reworked), Bone Chill
- **Level 1**: Armor of Agathys, Chromatic Orb (has data but verify conversion), Color Spray, Disguise Self, Expeditious Retreat, Faerie Fire, False Life, Fog Cloud, Grease, Guiding Bolt, Healing Word, Hex, Hunter's Mark, Inflict Wounds, Jump, Longstrider, Protection from Evil, Ray of Sickness, Sanctuary, Searing Smite, Speak with Animals (non-combat), Tasha's Hideous Laughter, Thunderous Smite, Wrathful Smite
- **Level 2**: Arcane Lock (non-combat), Branding Smite, Calm Emotions, Cloud of Daggers, Crown of Madness, Darkvision, Enhance Ability, Enlarge/Reduce, Flame Blade, Flaming Sphere, Gust of Wind, Heat Metal, Invisibility, Knock, Lesser Restoration, Magic Weapon, Phantasmal Force, Prayer of Healing, Protection from Poison, Scorching Ray, See Invisibility, Shatter, Silence, Spiritual Weapon, Web, Warding Bond
- **Level 3**: Beacon of Hope, Bestow Curse, Blink, Call Lightning, Counterspell, Crusader's Mantle, Daylight, Elemental Weapon, Feign Death, Glyph of Warding, Haste, Hunger of Hadar, Hypnotic Pattern, Lightning Bolt, Mass Healing Word, Plant Growth, Protection from Energy, Remove Curse, Revivify, Sleet Storm, Slow, Speak with Dead (non-combat), Spirit Guardians, Stinking Cloud, Vampiric Touch
- **Level 4**: Banishment, Blight, Confusion, Conjure Minor Elementals, Dimension Door, Dominate Beast, Evard's Black Tentacles, Freedom of Movement, Greater Invisibility, Guardian of Faith, Ice Storm, Otiluke's Resilient Sphere, Phantasmal Killer, Polymorph, Stoneskin, Wall of Fire
- **Level 5**: Cloudkill, Commune (non-combat), Cone of Cold, Conjure Elemental, Destructive Wave, Flame Strike, Greater Restoration, Hold Monster, Insect Plague, Mass Cure Wounds, Planar Binding (non-combat), Wall of Stone
- **Level 6**: Blade Barrier, Chain Lightning, Circle of Death, Create Undead, Disintegrate, Eyebite, Globe of Invulnerability, Harm, Heal, Heroes' Feast, Otto's Irresistible Dance, Sunbeam, Wall of Ice, Wind Walk

**Agent instructions**:
- Work level-by-level starting from Level 1 (most-used spells)
- For each spell: create ActionDefinition in JSON, create any required status effects, verify effect types are supported (not NoOp stubs)
- Use `BG3_Data/Spells/Spell_*.txt` as the source of truth for mechanics
- Cross-reference https://bg3.wiki/wiki/List_of_all_spells for BG3-specific adaptations
- Prioritize spells that are: (a) class-defining, (b) appear in existing scenarios, (c) have working effect types

---

### Priority Tier 3: MEDIUM IMPACT (Polish, completeness, and robustness)

---

#### 7. Subclass-Specific Spell Lists (Always-Prepared Domain/Circle/Oath Spells)
**Gap**: In BG3, each Cleric domain, Druid circle, Paladin oath, and Ranger subclass gets specific always-prepared spells. These are not in the subclass data.

**Examples**:
- Life Cleric: Bless, Cure Wounds always prepared at L1; Lesser Restoration, Spiritual Weapon at L3; etc.
- Oath of Devotion Paladin: Protection from Evil, Sanctuary at L3; Lesser Restoration, Zone of Truth at L5
- Circle of the Moon Druid: Enhanced Wild Shape options

**Agent instructions**:
- Reference https://bg3.wiki/wiki/Cleric, /Paladin, /Druid, /Ranger for subclass spell lists
- Add `AlwaysPreparedSpells` field to subclass definitions
- Wire `CharacterResolver` to include these in `KnownActions`

---

#### 8. Reaction System Content Expansion
**Gap**: The reaction infrastructure is solid (8 trigger types, resolution stack, AI handling, BG3 interrupt integration). But many BG3 reactions are data-defined in `Interrupt.txt` without runtime execution paths.

**Key missing reaction implementations**:
- **Counterspell**: Parsing exists, runtime execution path needed (cancel spell, ability check for higher-level spells)
- **Shield** (spell): +5 AC until next turn as reaction to being attacked
- **Hellish Rebuke**: Reaction damage when hit
- **Cutting Words** (Bard): Reduce enemy attack/ability roll (reactive, not pre-applied)
- **Deflect Missiles** (Monk): Reduce ranged attack damage
- **Sentinel** feat: OA when enemy attacks ally, target speed → 0
- **Mage Slayer** feat: Attack reaction when enemy casts within melee, advantage on saves vs adjacent casters
- **War Caster** feat: Cast spell instead of OA (partially wired — concentration advantage exists)
- **Warding Flare** (Light Cleric): Disadvantage on attacker's roll
- **Destructive Wrath** (Tempest Cleric): Max thunder/lightning damage

**Agent instructions**:
- Review `BG3_Data/Stats/Interrupt.txt` for all interrupt definitions
- Map each to a `ReactionDefinition` in `ReactionSystem`
- Implement execution effects using existing `EffectPipeline` handlers
- Key: Counterspell needs a new trigger type (`SpellCast`) and ability check resolution

---

#### 9. Inventory & Item Use in Combat
**Gap**: `CommandService` has `UseItem` as a stub enum value. No item consumption (potions, scrolls, throwables) during combat.

**BG3 combat items**:
- **Potions**: Healing, Greater Healing, Superior Healing, Speed, Invisibility, Resistance, etc.
- **Scrolls**: Any spell scroll usable by any class (with Arcana check for off-list)
- **Throwables**: Alchemist Fire, Acid Vial, Holy Water, throwable weapons
- **Coatings**: Poisons applied to weapons
- **Consumables**: Camp supplies (non-combat), but Elixirs persist through long rest

**What's needed**:
- Item data model: `ItemDefinition` with use action, charges, weight
- `InventoryService` expansion: Per-combatant inventory, add/remove/use
- `UseItemCommand` implementation in `CommandService`
- Integration with `EffectPipeline` (items trigger effects like spells do)
- Throw action using `ForcedMovementService` for throwables

**Agent instructions**:
- Design `ItemDefinition` model mirroring `ActionDefinition` (an item "use" is essentially casting a spell)
- Extend `InventoryService` (currently exists but scope unclear)
- Implement `UseItemCommand` patterned after ability execution
- Start with potions (simplest: heal effect) then scrolls (cast action with spell slot bypass)

---

#### 10. Difficulty Mode Combat Modifiers
**Gap**: `DifficultyService` exists but combat-affecting difficulty modifiers may be incomplete.

**BG3 difficulty modifiers (Tactician/Honour mode)**:
- Enemies have +2 to all stats (Tactician)
- Enemies have more HP (Tactician: +50% or more)
- Enemies have better AI decisions
- Honour mode: Legendary actions on bosses, single save file
- Explorer mode: Advantage on attack rolls, enemies deal less damage
- Custom mode: All modifiers independently adjustable

**Agent instructions**:
- Read `Data/Difficulty/` and `BG3_Data/DifficultyClasses.lsx` + `RulesetModifiers.lsx`
- Ensure `DifficultyService` modifies enemy stats, HP, and AI profile based on mode
- Add Honour mode legendary action support (Legendary action resource in ResourceManager)

---

#### 11. Condition Evaluator Stub Completion
**Gap**: `ConditionEvaluator` has stub functions that log warnings instead of evaluating.

**Stubbed functions**:
- `HasProficiency(type)` — Should check `ProficiencySet`
- `IsConcentrating()` — Should check `ConcentrationSystem`
- `WearingArmor(type)` — Should check equipped armor
- `IsInMeleeRange()` — Should check distance to nearest enemy
- `HasWeaponEquipped(type)` — Should check weapon slots

**Agent instructions**:
- Each function has a clear implementation target: query the relevant service via `CombatContext`
- Wire `HasProficiency` → `Combatant.Proficiencies`
- Wire `IsConcentrating` → `ConcentrationSystem.GetConcentrationTarget()`
- Wire `WearingArmor` → `Combatant.EquippedArmor.ArmorType`
- These enable many BG3 conditional boosts to work automatically

---

### Priority Tier 4: POLISH & COMPLETENESS (Nice-to-have for full parity)

---

#### 12. Warlock Invocation Expansion
**Gap**: Only 6/20+ Warlock Invocations defined. Many are combat-relevant.

**Missing key invocations**:
- Thirsting Blade (Extra Attack for Pact of the Blade)
- Lifedrinker (add CHA damage to pact weapon)
- Eldritch Smite (expend warlock slot for force damage + prone)
- Book of Ancient Secrets (ritual casting)
- One with Shadows (invisible in dim light)
- Minions of Chaos (summon elemental)
- Sign of Ill Omen (free Bestow Curse)

**Agent instructions**:
- Reference https://bg3.wiki/wiki/Eldritch_Invocations
- Add invocations to `warlock_invocations.json`
- Wire as PassiveRuleProviders or Boosts depending on type

---

#### 13. VFX & Animation Polish
**Gap**: Timeline system is complete, but actual visual effects are basic. This is cosmetic but impacts game feel significantly.

**Missing VFX**:
- Spell-specific particle effects (fireball explosion, lightning bolt arc, healing glow)
- Concentration visual indicator on caster
- Status effect visual indicators (burning aura, frozen crystals, blur shimmer)
- Surface visual improvements (currently basic overlays)
- Damage number popups with type coloring
- Death/downed visual state

**Agent instructions**:
- This is Godot scene/shader work, not C# logic
- Use `CombatVFXManager` and `PresentationRequestBus` as integration points
- Create `.tscn` particle scenes per effect category

---

#### 14. Portrait System
**Gap**: All portraits are random placeholders. 6+ TODO comments about portrait replacement.

**Agent instructions**:
- Design portrait assignment based on race/class/gender from CharacterSheet
- Create or source portrait assets
- Wire `PortraitAssigner` to use CharacterSheet data

---

#### 15. Multiclass Builder UI
**Gap**: `CharacterBuilder` is single-class only. Multi-class requires direct CharacterSheet construction.

**Agent instructions**:
- Extend `CharacterBuilder` with `AddClassLevel(className, subclassName)` method
- Handle prerequisite checks (BG3 multiclass requirements: minimum 13 in class's primary ability)
- Wire into character creation UI

---

#### 16. Save Migration System
**Gap**: `SaveMigrator` is a placeholder class with no actual migrations.

**Agent instructions**:
- Implement version tracking in save files
- Create migration pattern (v1→v2 transformation functions)
- Handle backwards compatibility for saves from before schema changes

---

#### 17. Missing Common Actions
**Gap**: Some BG3 common actions are missing or incomplete.

**Missing/incomplete common actions**:
- **Dip**: Coat weapon in nearby surface element (fire = +1d4 fire damage for 2 hits)
- **Hide**: Stealth in combat (bonus action, contested check, grants advantage on next attack)
- **Help**: Advantage on next ally's ability check/attack, or help downed ally
- **Ready**: Hold action until triggered (not in BG3 combat, skip)
- **Dodge**: Attacks against you have disadvantage (currently defined?)
- **Improvised Weapon**: Throw nearby objects for damage

**Agent instructions**:
- Reference https://bg3.wiki/wiki/Actions for the full common action list
- Verify each exists in `common_actions.json`
- Wire any missing ones: Dip needs surface proximity check + weapon coating status, Hide needs stealth check system

---

## Cross-Cutting Concerns

### Testing Strategy for Agents
Every implementation task should follow this verification chain:
1. `scripts/ci-build.sh` — Compiles cleanly
2. `scripts/ci-test.sh` — Unit tests pass
3. `scripts/ci-godot-log-check.sh` — No runtime errors on startup
4. `scripts/run_autobattle.sh --seed 42` — Auto-battle completes without freeze/loop
5. `scripts/run_autobattle.sh --full-fidelity --ff-short-gameplay` — Full visual test passes

### Dependency Graph

```
Task 1 (Passive Interpreter) ──────── unlocks ──► Task 4 (Class Feature Wiring)
         │                                              │
         └─── partially unlocks ──► Task 2 (Status Content)
                                         │
Task 3 (Effect Handler Stubs) ────── unlocks ──► Task 6 (Spell Content)
         │                                              │
         └─── unlocks ──► Task 8 (Reaction Content)     │
                                                        │
Task 2 (Status Content) ──────────── unlocks ──► Task 6 (Spell Content)
                                                        │
Task 7 (Subclass Spells) ◄──────── depends on ──── Task 6
                                                        │
Task 5 (Level 7-9 Spells) ◄──────── depends on ──── Task 6 + Task 2 + Task 3
```

### Data Pipeline vs Hand-Authoring Decision

The project has two approaches to content:
1. **BG3 Data Pipeline**: Parse raw BG3 `.txt`/`.lsx` files → auto-generate game data
2. **Hand-Authored JSON**: Manually create `ActionDefinition`, `StatusDefinition` files

**Recommendation**: For content scaling (Tasks 2, 5, 6), invest in improving the automated pipeline from BG3 raw data. The parsers (`BG3SpellParser`, `BG3StatusParser`, `BG3PassiveParser`) already exist — they need a converter stage that outputs game-ready JSON. This is faster than hand-authoring 200+ statuses.

---

## Numeric Summary

| Category | BG3 Target | Implemented | Gap |
|----------|-----------|-------------|-----|
| Classes | 12 | 12 | 0 |
| Subclasses | 46 | 46 (+12 bonus) | 0 |
| Races | 11 | 11 | 0 |
| Feats | 41 | 41 | 0 |
| Weapons | ~34 | 34 | 0 |
| Armor | 13 | 13 | 0 |
| Weapon Actions | ~22 | 21+ | ~0 |
| Spell Actions (tagged) | ~300+ | 209 | ~100+ |
| Spell Levels 7-9 | ~25 | 0 | ~25 |
| Statuses (with mechanics) | ~300 critical | 153 | ~150 |
| Passives (mechanically wired) | ~100 critical | 10 | ~90 |
| Effect Handlers | 38 | 22 real + 16 NoOp | 16 |
| Reaction Implementations | ~20 | ~5 (OA + Uncanny Dodge + Divine Smite + limited others) | ~15 |
| Class Features (wired) | ~80 critical | ~30 | ~50 |
| Combat Items | ~50+ types | 0 | ~50 |
| Common Actions | ~10 | ~7 | ~3 |
| Scenarios Tested | All 12 classes | 7 classes | 5 classes |
| Conditions (D&D 5e base) | 14 | 14 | 0 |
| Surfaces | ~20 | 19+ | ~0 |
| Death Save System | Complete | Complete | 0 |
| Concentration | Complete | Complete | 0 |
| AI System | Full | Full | 0 |
| Persistence | Full | Full | 0 |

---

## Implementation Order Recommendation

For an agent tackling this work, the optimal sequence is:

1. **Task 1** → Passive/Boost Interpreter (2-3 sessions, unlocks everything)
2. **Task 3** → Effect Handler Stubs (1-2 sessions, unblocks spells)
3. **Task 11** → Condition Evaluator Stubs (1 session, quick win)
4. **Task 2** → Status Content Pipeline (2-3 sessions, unlocks spells)
5. **Task 4** → Class Feature Wiring, Top 30 (3-4 sessions, class identity)
6. **Task 6** → Spell Content Gap Fill, Levels 1-3 first (3-4 sessions)
7. **Task 8** → Reaction Content Expansion (2 sessions)
8. **Task 7** → Subclass Spell Lists (1 session)
9. **Task 6 continued** → Spell Content, Levels 4-6 (2-3 sessions)
10. **Task 9** → Inventory & Items (2-3 sessions)
11. **Task 10** → Difficulty Modes (1 session)
12. **Task 17** → Missing Common Actions (1 session)
13. **Task 5** → Level 7-9 Spells if applicable (1-2 sessions)
14. **Task 12** → Warlock Invocations (1 session)
15. **Task 15** → Multiclass Builder (1 session)
16. **Task 13** → VFX Polish (ongoing)
17. **Task 14** → Portraits (1 session)
18. **Task 16** → Save Migration (when needed)
