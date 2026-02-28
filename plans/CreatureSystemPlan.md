# Creature System Implementation Plan

## Executive Summary

BG3 uses a **unified entity model** — PCs and creatures share the same stat block structure (`type "Character"`). The difference is structural: PCs build stats via race → class → progression, while creatures have **flat stat blocks** with hardcoded abilities, inline passives, and creature-type-based resistances/immunities.

Our codebase already has the right foundation: a single `Combatant` class for all participants, and **hundreds of BG3 creature stat blocks already parsed** into `BG3CharacterData`. The critical gap is a **factory to bridge parsed data → runtime Combatant**, plus supporting systems (creature types, multiattack, death behavior, expanded summons, creature AI).

---

## Architecture Principle

> **Do NOT create a parallel entity hierarchy.** Follow BG3's approach: one `Combatant`, different construction paths. PCs are built via `CharacterSheet → CharacterResolver → Combatant`. Creatures are built via `BG3CharacterData → CreatureFactory → Combatant`.

---

## Phase 1: Foundation — Creature Types & Data Bridge
**Goal:** Enable creatures to be instantiated from BG3 stat blocks with proper type metadata.

### Step 1.1: CreatureType Enum
**File:** `Data/CreatureType.cs` (new)

Create an enum matching BG3's creature type taxonomy (which BG3 models as top-level Races):

```csharp
public enum CreatureType
{
    Humanoid,    // PCs, goblins, guards, bandits
    Beast,       // Wolves, bears, spiders, owlbears
    Undead,      // Skeletons, zombies, vampires
    Fiend,       // Devils, demons, imps
    Celestial,   // Devas, etc.
    Construct,   // Animated armor, golems
    Elemental,   // Mephits, elementals
    Aberration,  // Mind flayers, beholders, intellect devourers
    Monstrosity, // Bulettes, ettercaps, harpies, minotaurs
    Dragon,      // Dragons
    Fey,         // Hags, redcaps
    Plant,       // Myconids, wood woads
    Ooze,        // Gray ooze, ochre jelly
    Giant,       // Ogres
    Critter      // Rats, frogs, crabs (non-combat animals)
}
```

### Step 1.2: Add CreatureType to Combatant
**File:** `Combat/Entities/Combatant.cs`

Add property:
```csharp
public CreatureType CreatureType { get; set; } = CreatureType.Humanoid;
```

This replaces the loose `Tags` list for type-based interactions (Turn Undead, Ranger Favored Enemy, Protection from Evil spells, etc).

### Step 1.3: EntityCategory Enum
**File:** `Data/EntityCategory.cs` (new)

Distinguish how a combatant was created and how it behaves at 0 HP:

```csharp
public enum EntityCategory
{
    PlayerCharacter,  // Full PC build, death saves at 0 HP
    NpcHumanoid,      // NPC with possible class features, dies at 0 HP
    Creature,         // Monster/beast flat stat block, dies at 0 HP
    Summon            // Owned by caster, dies at 0 HP, removed on concentration break
}
```

Add to `Combatant`:
```csharp
public EntityCategory EntityCategory { get; set; } = EntityCategory.PlayerCharacter;
```

### Step 1.4: CreatureFactory — BG3CharacterData → Combatant
**File:** `Data/Stats/CreatureFactory.cs` (new)

The critical missing bridge. Given a BG3 stat entry name, resolves inheritance and produces a `Combatant`:

```
Input:  "Goblin_Melee" (string → StatsRegistry lookup)
Output: Combatant with:
  - HP from Vitality (with inheritance resolution)
  - Ability scores (STR/DEX/CON/INT/WIS/CHA)
  - AC from Armor field
  - CreatureType inferred from inheritance chain (_Beast → Beast, _Undead → Undead, etc.)
  - Resistances/immunities mapped to Boosts
  - Passives from Passives field
  - Actions from SpellSet + inline action grants
  - ActionBudget from ActionResources field
  - AI archetype from parent chain
  - EntityCategory = Creature (or NpcHumanoid if under _Humanoid)
```

Key behaviors:
- Resolve `using` inheritance chain (e.g., `Goblin_Melee` → `_Goblins` → `_Humanoid` → `_Base`)
- Map action resource strings like `"ActionPoint:1;BonusActionPoint:1;Movement:9"` to `ActionBudget`
- Map resistance fields to `BoostContainer` entries
- Map passives string to `PassiveIds` list
- Map `PersonalStatusImmunities` to status immunity data
- Lookup AI archetype from `BG3AIRegistry` if the template specifies one

### Step 1.5: Creature Type ↔ Resistance Defaults
**File:** `Data/Stats/CreatureTypeDefaults.cs` (new)

Map creature type base entries to default resistances/immunities/status immunities, derived from `_Undead`, `_Construct`, `_Elemental`, etc. base entries in Character.txt:

| Type | Status Immunities | Special |
|------|------------------|---------|
| Undead | BLEEDING, GAPING_WOUND, CHEST_TRAUMA | BlockRegainHP(Living;Construct) |
| Construct | BLEEDING, GAPING_WOUND, CHEST_TRAUMA | BlockRegainHP(Undead;Living), no Class |
| Elemental | BLEEDING, GAPING_WOUND, CHEST_TRAUMA | Element-specific resistances |
| Fiend | SG_Poisoned | — |

---

## Phase 2: Death Behavior & Combat Rules
**Goal:** NPCs and creatures die instantly at 0 HP; only PCs get death saves.

### Step 2.1: Instant Death for Non-PCs
**Files:** `Combat/States/` (death save state machine), `Combat/Rules/CombatRules.cs`

When a combatant reaches 0 HP:
- If `EntityCategory == PlayerCharacter` → enter death save state (current behavior)
- If `EntityCategory != PlayerCharacter` → transition directly to `Dead`

This is explicitly called out in BG3's design and referenced in our `BG3_DEEP_COMBAT_AUDIT.md`.

### Step 2.2: Massive Damage Rule
When a single hit's excess damage (after reaching 0 HP) ≥ the creature's max HP, the creature/PC dies outright. BG3 applies this to PCs too.

---

## Phase 3: Action Economy Differences
**Goal:** Creatures get appropriate action sets, including multiattack.

### Step 3.1: SpellSet Tiers
**File:** `Data/Actions/SpellSetDefinitions.cs` (new or extend existing)

BG3 defines four tiers of base actions:

| SpellSet | Actions | Used By |
|----------|---------|---------|
| CommonPlayerActions | Jump, Dip, Hide, Shove, Throw, Dash, Help, Disengage | PCs |
| CommonNPCActions | Jump, Shove, Dash_NPC, Throw | NPC Humanoids |
| CommonBeastActions | Dash_NPC only | Beasts, creatures |
| CommonSummonActions | Dash_NPC, Jump, Hide, Disengage | Summons |

`CreatureFactory` assigns the correct base action set based on the creature's inheritance chain.

### Step 3.2: Multiattack
**File:** `Combat/Actions/MultiattackAction.cs` (new)

Many BG3 creatures get Multiattack — a single Action that executes 2+ attacks. This is NOT the same as Extra Attack (which PCs get from Fighter/etc.).

Implementation:
- New action type `MultiattackAction` containing a list of sub-attack IDs
- When executed, performs each sub-attack sequentially against the same or different targets
- Consumes 1 Action Point (the parent action), sub-attacks don't consume resources
- Populated from BG3 passive data (e.g., `MultiAttack(2)` passive)

### Step 3.3: Restricted Action Resources for Special Summons
Some summons have non-standard action budgets:
- **Flaming Sphere**: BonusActionPoint:1 + Movement:9 only (no Action, no Reaction)
- **Mage Hand**: Action:1 + BonusAction:1 + Movement:9 (no Reaction)
- **Minor Illusion**: No action resources at all

`CreatureFactory` must parse the `ActionResources` string and build the correct `ActionBudget`.

---

## Phase 4: Expanded Summon System
**Goal:** Populate summon templates for all BG3 summon categories.

### Step 4.1: Expand SummonTemplate Model
**File:** `Data/SummonTemplateRegistry.cs`

Add fields to `SummonTemplate`:
```csharp
public int HP { get; set; }                           // Base HP
public Dictionary<string, int> AbilityScores { get; set; }  // STR/DEX/CON/INT/WIS/CHA
public int AC { get; set; }                           // Armor class
public float Speed { get; set; }                      // Movement speed
public CreatureType CreatureType { get; set; }        // For type-based interactions
public string AIArchetype { get; set; }               // AI behavior profile
public string ActionResources { get; set; }           // Non-standard action budgets
public List<string> Resistances { get; set; }         // Damage resistances
public List<string> Immunities { get; set; }          // Status immunities
public List<string> PassiveIds { get; set; }          // Passive abilities
public bool RequiresConcentration { get; set; }       // Whether losing concentration kills it
public bool BlocksDialogue { get; set; } = true;      // DialogueBlock() boost
public string ScalesWithLevel { get; set; }           // If stats scale (e.g., Ranger companion)
```

### Step 4.2: Populate Summon Templates from BG3 Data
**File:** `Data/Scenarios/summon_templates.json`

Add templates for all BG3 summon categories, sourced from `_Summons` entries in Character.txt:

**Animate Dead (3 templates)**
- `animate_dead_skeleton` — Skeleton with shortbow/shortsword
- `animate_dead_zombie` — Zombie with melee
- `animate_dead_zombie_horde` — Weaker zombie variant

**Ranger Companions (8 templates, 2 tiers each)**
- `companion_bear` / `companion_bear_5` (level 5+ upgrade)
- `companion_wolf` / `companion_wolf_5`
- `companion_boar` / `companion_boar_5`
- `companion_spider` / `companion_spider_5`
- `companion_raven` / `companion_raven_5`
- etc.

**Find Familiar (6 templates)**
- `familiar_cat`, `familiar_crab`, `familiar_frog`, `familiar_spider`, `familiar_rat`, `familiar_raven`
- Low HP (1-5), utility-focused (Help action, touch spell delivery)

**Warlock Chain Pact (3 templates)**
- `chain_imp`, `chain_mephit`, `chain_quasit`
- Based on base creature stats with imp attacks, mephit breath, quasit claw

**Spell Summons (5+ templates)**
- `flaming_sphere` (already exists)
- `spiritual_weapon` — floating weapon, Bonus Action attacks
- `mage_hand` — utility, limited actions
- `minor_illusion` — no actions
- `dancing_lights` — no actions

### Step 4.3: Concentration vs Non-Concentration Summons
**Important BG3 deviation from 5e:** Ranger Companions and Find Familiar are NOT concentration in BG3. Mark `RequiresConcentration = false` for these.

Update `ConcentrationSystem.RemoveSummonsByOwner` to only remove summons where `RequiresConcentration == true`.

---

## Phase 5: Creature-Specific AI
**Goal:** Creatures use appropriate AI profiles based on their archetype.

### Step 5.1: Wire BG3 AI Archetypes to Creature Factory
**Files:** `Data/AI/BG3AIRegistry.cs`, `Combat/AI/BG3AIProfileFactory.cs`

The BG3 archetype data is already parsed. Wire it so:
1. `CreatureFactory` looks up the creature's archetype from its template or inheritance chain
2. `BG3AIProfileFactory` converts the archetype multipliers to a runtime `AIProfile`
3. The `AIProfile` is stored on the `Combatant`

### Step 5.2: Creature Intelligence Spectrum
Map archetype → behavioral traits:

| Archetype | Hit Evaluation | AoO Avoidance | Target Summons | Special |
|-----------|---------------|---------------|----------------|---------|
| zombie | Random | None | Equal to real targets | Ignores surfaces |
| beast | Random | Default | Equal to real targets | No weapon pickup |
| goblin_melee | Slightly better | Default | Deprioritized | Reduced chasm exploit |
| melee | Good | Default | Deprioritized | Weapon pickup |
| mage_smart | Best | Default | Deprioritized | Stays at range |
| mindflayer | Good | Default | Deprioritized | Prioritizes kills for Extract Brain |

### Step 5.3: Morale & Behavioral Flags
Creatures should have behavioral flags:
- `Fearless` (undead, constructs) — never flee
- `PackTactics` (wolves, goblins) — advantage when ally adjacent
- `SunlightSensitivity` (drow, some undead) — disadvantage in sunlight
- `MagicResistance` (mind flayer) — advantage on saves vs spells

These are already passives in BG3 data — the passive system should evaluate them.

---

## Phase 6: Scenario & Encounter Integration
**Goal:** Scenarios can reference BG3 creature stat blocks instead of faking PC builds.

### Step 6.1: Scenario Unit - Creature Reference Mode
**File:** `Data/Scenarios/` (scenario JSON schema)

Add a new unit definition mode to scenarios:
```json
{
  "type": "creature",
  "statBlock": "Goblin_Melee",
  "faction": "Hostile",
  "position": { "x": 5, "y": 0, "z": 3 },
  "overrides": {
    "hp": 12,
    "name": "Goblin Ambusher"
  }
}
```

`ScenarioLoader` detects `"type": "creature"` and uses `CreatureFactory` instead of the PC build pipeline.

### Step 6.2: Encounter Difficulty Calculator
**File:** `Data/Scenarios/EncounterDifficulty.cs` (new)

Use creature level/XP values to calculate encounter difficulty:
- Easy / Medium / Hard / Deadly thresholds based on party level
- Sum creature XP rewards for the encounter
- Adjust for creature count multipliers (5e/BG3 DMG rules)

### Step 6.3: ScenarioGenerator - Monster Encounters
**File:** `Data/ScenarioGenerator.cs`

Add `GenerateCreatureEncounter()` method:
- Select appropriate creatures from `StatsRegistry` based on desired difficulty
- Place creatures in tactically interesting positions
- Assign AI archetypes from BG3 data

---

## Phase 7: Wild Shape & Transformation Polish
**Goal:** Ensure beast form transformations work with the new creature type system.

### Step 7.1: Beast Form ↔ CreatureType Integration
When transforming via Wild Shape:
- `CreatureType` changes to `Beast`
- Physical ability scores (STR/DEX/CON) are replaced
- Mental ability scores (INT/WIS/CHA) are kept
- HP is replaced with beast form HP (acts as temp HP pool)
- Speed changes to beast form's speed
- Available actions change to beast form's actions

Most of this is already in `TransformEffect.cs` and `BeastForm.cs` — validate completeness.

### Step 7.2: Wild Shape Scaling
BG3 has separate beast form entries per druid level (e.g., `Badger_Giant_Wildshape_4` through `_12`). Ensure `beast_forms.json` includes level-scaled variants.

---

## Phase 8: Damage Resistance & Immunity Wiring
**Goal:** Creature resistances from BG3 data are mechanically effective.

### Step 8.1: Map BG3 Resistance Strings to Boost System
`CreatureFactory` should convert:
- `"FireResistance" = "Immune"` → `DamageImmunityBoost(Fire)`
- `"PoisonResistance" = "Resistant"` → `DamageResistanceBoost(Poison)`
- `"BludgeoningResistance" = "ResistantToNonMagical"` → `DamageResistanceBoost(Bludgeoning, NonMagical)`

### Step 8.2: Type-Based Status Immunities
Convert `PersonalStatusImmunities` string:
- `"BLEEDING;GAPING_WOUND;SG_Poisoned"` → status immunity entries on the combatant

---

## Implementation Priority & Dependencies

```
Phase 1 (Foundation) ← MUST be first, everything depends on this
  ├── Step 1.1–1.3: Types & enums (no deps)
  ├── Step 1.4: CreatureFactory (depends on 1.1–1.3)
  └── Step 1.5: Type defaults (depends on 1.1)

Phase 2 (Death) ← independent of Phases 3-8
  └── Steps 2.1–2.2 (depends on Phase 1.3 for EntityCategory)

Phase 3 (Actions) ← needs Phase 1 for CreatureFactory
  ├── Step 3.1: SpellSets (independent)
  ├── Step 3.2: Multiattack (independent)
  └── Step 3.3: Action resources (independent)

Phase 4 (Summons) ← needs Phase 1 for CreatureType
  ├── Steps 4.1–4.2: Template expansion (depends on 1.1)
  └── Step 4.3: Concentration fix (independent)

Phase 5 (AI) ← needs Phase 1 for factory wiring
  └── Steps 5.1–5.3 (depends on 1.4)

Phase 6 (Scenarios) ← needs Phase 1 for CreatureFactory
  └── Steps 6.1–6.3 (depends on 1.4)

Phase 7 (Wild Shape) ← independent polish
  └── Steps 7.1–7.2 (depends on 1.1)

Phase 8 (Resistances) ← needs Phase 1 for factory wiring
  └── Steps 8.1–8.2 (depends on 1.4)
```

**Recommended order:** Phase 1 → Phase 2 → Phase 6.1 → Phase 3.1 → Phase 4 → Phase 8 → Phase 3.2 → Phase 5 → Phase 6.2–6.3 → Phase 7 → Phase 3.3

---

## Effort Estimates

| Phase | Scope | Estimate |
|-------|-------|----------|
| Phase 1 | CreatureType enum, EntityCategory, CreatureFactory, type defaults | Large — core infrastructure |
| Phase 2 | Death behavior split | Small — conditional in death state machine |
| Phase 3 | SpellSets, Multiattack, action resource parsing | Medium — new action type |
| Phase 4 | Expand summon templates (30+ entries), concentration fix | Medium — mostly data |
| Phase 5 | AI archetype wiring, behavioral flags | Medium |
| Phase 6 | Scenario creature refs, difficulty calc, generator | Medium-Large |
| Phase 7 | Wild Shape polish | Small — mostly validation |
| Phase 8 | Resistance/immunity wiring | Small-Medium |

---

## Key Design Decisions

1. **CreatureFactory reads from StatsRegistry** (already parsed BG3CharacterData) — no new parser needed
2. **Inheritance resolution in factory** — follow `using` chains to build complete stat blocks
3. **CreatureType from inheritance chain** — `_Beast` → Beast, `_Undead` → Undead, etc.
4. **One Combatant class** — no CreatureCombatant subclass. EntityCategory + CreatureType properties are sufficient
5. **Summon templates gain stat data** — expand beyond just name + actions to include full stat blocks
6. **Ranger Companions & Familiars are NOT concentration** — BG3 deviation from 5e RAW
7. **Multiattack is a new action type**, not Extra Attack — they're mechanically different
