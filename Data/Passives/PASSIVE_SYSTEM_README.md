# BG3 Passive Ability System - Implementation Guide

## Overview

The Passive Ability System integrates BG3's permanent character abilities (Passives) into the combat system. Passives grant permanent boosts and event-driven effects from races, classes, feats, and equipment.

## Architecture

### Core Components

1. **BG3PassiveData** (`Data/Passives/BG3PassiveData.cs`)
   - Data model for passive definitions
   - Properties: PassiveId, DisplayName, Description, Boosts, StatsFunctors, etc.
   - Helper methods: IsHidden, IsHighlighted, IsToggleable, HasBoosts, HasStatsFunctors

2. **BG3PassiveParser** (`Data/Parsers/BG3PassiveParser.cs`)
   - Parses `BG3_Data/Stats/Passive.txt` file
   - Handles inheritance via "using" directive
   - Extracts all passive properties including Boosts and StatsFunctors

3. **PassiveRegistry** (`Data/Passives/PassiveRegistry.cs`)
   - Centralized registry of all BG3 passives
   - Methods: LoadPassives(), GetPassive(), SearchPassives()
   - Query methods: GetHighlightedPassives(), GetToggleablePassives()
   - Indexing by properties for fast lookups

4. **PassiveManager** (`Combat/Passives/PassiveManager.cs`)
   - Manages passives on individual combatants
   - Methods: GrantPassive(), RevokePassive(), HasPassive()
   - Integrates with BoostApplicator to apply/remove boosts

5. **Combatant Integration** (`Combat/Entities/Combatant.cs`)
   - Added `List<string> PassiveIds` - what passives this combatant has
   - Added `PassiveManager PassiveManager` - manages passive lifecycle
   - PassiveManager is initialized in constructor with Owner set

## Data Flow

```
BG3_Data/Stats/Passive.txt
         ↓
   BG3PassiveParser
         ↓
   PassiveRegistry (load all passives)
         ↓
   PassiveManager.GrantPassive(passiveId)
         ↓
   Look up passive definition → Apply Boosts → Track as active
```

## Usage Examples

### Loading Passives

```csharp
var passiveRegistry = new PassiveRegistry();
int count = passiveRegistry.LoadPassives("BG3_Data/Stats/Passive.txt");
// Loads ~3000 passives from BG3 data
```

### Granting Passives to Combatants

```csharp
var elf = new Combatant("elf1", "Astarion", Faction.Player, 100, 15);

// Grant racial passives
elf.PassiveManager.GrantPassive(passiveRegistry, "Darkvision");
elf.PassiveManager.GrantPassive(passiveRegistry, "FeyAncestry");
elf.PassiveManager.GrantPassive(passiveRegistry, "Elf_WeaponTraining");

// Or grant multiple at once
var racialPassives = new List<string> { "Darkvision", "FeyAncestry", "Elf_WeaponTraining" };
elf.PassiveManager.GrantPassives(passiveRegistry, racialPassives);
```

### Querying Active Passives

```csharp
// Check if combatant has a specific passive
bool hasDarkvision = elf.PassiveManager.HasPassive("Darkvision");

// Get all active passive IDs
var passiveIds = elf.PassiveManager.ActivePassiveIds;

// Get full passive definitions
var activePassives = elf.PassiveManager.GetActivePassives(passiveRegistry);
foreach (var passive in activePassives)
{
    Console.WriteLine($"{passive.DisplayName}: {passive.Description}");
}
```

### Querying Boosts from Passives

```csharp
// Get all passive-sourced boosts
var passiveBoosts = elf.Boosts.GetBoostsFromSource("Passive", sourceId: null);

// Get boosts from a specific passive
var darkvisionBoosts = elf.Boosts.GetBoostsFromSource("Passive", "Darkvision");

// Query boost effects using BoostEvaluator
int acBonus = BoostEvaluator.GetACBonus(elf);
var immunities = BoostEvaluator.GetStatusImmunities(elf);
```

### Revoking Passives

```csharp
// Revoke a single passive (removes its boosts)
bool revoked = elf.PassiveManager.RevokePassive("Darkvision");

// Clear all passives
elf.PassiveManager.ClearAllPassives();
```

## BG3 Passive Examples

### Racial Passives

**Darkvision**
- Boosts: `DarkvisionRangeMin(12);ActiveCharacterLight(...)`
- Grants vision in darkness up to 12m
- Common to Elves, Dwarves, Half-Elves, etc.

**Elven Weapon Training** (`Elf_WeaponTraining`)
- Boosts: `Proficiency(Longswords);Proficiency(Shortswords);Proficiency(Longbows);Proficiency(Shortbows)`
- Grants weapon proficiencies
- High Elves and Wood Elves get this

**Dwarven Resilience** (`Dwarf_DwarvenResilience`)
- Boosts: `Resistance(Poison, Resistant);Tag(POISONED_ADV)`
- Advantage vs Poison, Resistance to Poison damage
- All Dwarves get this

**Fey Ancestry**
- Boosts: `StatusImmunity(SLEEP);StatusImmunity(POISON_DROW_CONDITION);Tag(CHARMED_ADV);StatusImmunity(SG_Sleeping_Magical)`
- Advantage vs Charm, immune to magical sleep
- Elves and Half-Elves

### Ability Improvements

**AbilityImprovement_Intelligence**
- Boosts: `Ability(Intelligence, 1)`
- Adds +1 to Intelligence score
- From ASI (Ability Score Improvement) feats

### Toggleable Passives

**NonLethal**
- Properties: `IsToggled;ToggledDefaultAddToHotbar;ToggleForParty`
- ToggleOnFunctors: `ApplyStatus(NON_LETHAL,100,-1)`
- ToggleOffFunctors: `RemoveStatus(NON_LETHAL)`
- Allows knocking enemies unconscious instead of killing

## Implementation Status

### ✅ Implemented
- Passive data model (BG3PassiveData)
- Parser for Passive.txt files (BG3PassiveParser)
- Passive registry with indexing and queries
- Passive manager for combatants
- Boost application/removal from passives
- Integration with Combatant class
- Comprehensive examples and documentation

### 🔄 Current Scope
- **Boosts only**: Passives currently apply their Boosts field
- Boosts are permanent (active as long as passive is active)
- Passive lifecycle: Grant → Apply Boosts → Revoke → Remove Boosts

### 🚧 Future Iterations

**StatsFunctors Support** (Event-driven effects)
- OnAttack, OnDamaged, OnCast, OnShortRest, etc.
- Conditional execution (IF clauses)
- Effect application (DealDamage, ApplyStatus, etc.)
- Examples:
  - `Backstab`: "IF(IsBehindTarget()):Advantage(AttackRoll)"
  - `Overwhelm`: "IF(IsMiss()):DealDamage(max(1,StrengthModifier), Bludgeoning)"

**Toggle Support**
- ToggleOnFunctors/ToggleOffFunctors
- ToggleGroup exclusivity
- UI integration for toggling passives

**Conditional Boosts**
- BoostConditions evaluation
- BoostContext (OnCreate, OnInventoryChanged, etc.)

## File Structure

```
Data/
├── Passives/
│   ├── BG3PassiveData.cs          # Passive data model
│   └── PassiveRegistry.cs          # Passive registry & queries
├── Parsers/
│   └── BG3PassiveParser.cs         # Parser for Passive.txt
Combat/
├── Passives/
│   └── PassiveManager.cs           # Per-combatant passive management
└── Entities/
    └── Combatant.cs                # Passive integration (PassiveIds, PassiveManager)
Examples/
├── PassiveSystemExamples.cs        # Basic examples
└── PassiveSystemIntegrationExample.cs  # Full integration example
```

## Testing

Run the examples to verify the system:

```csharp
// Basic examples
PassiveSystemExamples.RunAllExamples();

// Integration example
PassiveSystemIntegrationExample.RunExample();
```

## Integration with Character Creation

When creating characters from BG3 data:

1. **Race**: Grant racial passives from `Races.lsx`
2. **Class**: Grant class passives from `ProgressionDescriptions.lsx`
3. **Feats**: Grant feat passives from `FeatDescriptions.lsx`
4. **Equipment**: Grant equipment passives (future)

Example:
```csharp
// Wood Elf Ranger, Level 5
var ranger = new Combatant("ranger1", "Ranger", Faction.Player, 100, 14);

// Racial passives
ranger.PassiveManager.GrantPassives(passiveRegistry, new[] {
    "Darkvision",
    "FeyAncestry",
    "Elf_WeaponTraining"
});

// Class passives (from level progression)
ranger.PassiveManager.GrantPassives(passiveRegistry, new[] {
    "ExtraAttack"               // Level 5 Ranger/Fighter
});

// Feat passives
ranger.PassiveManager.GrantPassives(passiveRegistry, new[] {
    "AbilityImprovement_Dexterity",  // ASI at level 4
    "AbilityImprovement_Dexterity"   // +2 total
});
```

## Notes

- Passives are permanent (no duration)
- Passives can be granted/revoked at runtime
- Boosts from passives are automatically removed when passive is revoked
- PassiveManager is automatically initialized in Combatant constructor
- Passive source is always "Passive/{PassiveId}" for boost tracking
- Parser handles inheritance ("using" directive) automatically
- Registry supports fast property-based queries (Highlighted, IsToggled, etc.)

## Related Systems

- **Boost System** (`Combat/Rules/Boosts/`): Applies numeric/flag modifiers
- **Status System** (`Combat/Statuses/`, `Data/Statuses/`): Temporary timed effects
- **Action Registry** (`Combat/Actions/`, `Data/Actions/`): Available actions/spells
- **Character Model** (`Data/CharacterModel/`): Character creation and progression
