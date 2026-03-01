# BG3 Status System Implementation

## Overview

A complete parser and integration system for Baldur's Gate 3 status effects in the Godot 4.6 C# combat engine. This system enables loading BG3 status definitions from data files and automatically applying their boost effects to combatants.

## Architecture

### Components

1. **BG3StatusData.cs** - Data model for BG3 status definitions
2. **BG3StatusParser.cs** - Parser for Status_*.txt files
3. **StatusRegistry.cs** - Centralized registry of all BG3 statuses
4. **BG3StatusIntegration.cs** - Integration layer between statuses and boost system
5. **Examples & Tests** - Usage examples and integration tests

### Data Flow

```
Status_*.txt files
    ↓ (BG3StatusParser)
BG3StatusData objects
    ↓ (StatusRegistry)
Registered status definitions
    ↓ (BG3StatusIntegration)
StatusManager + BoostApplicator
    ↓
Active boosts on combatants
```

## File Format

BG3 status files use the same format as spell files:

```
new entry "STATUS_ID"
type "StatusData"
data "DisplayName" "Status Name"
data "Description" "Status description"
data "StatusType" "BOOST"
data "Boosts" "AC(2);Advantage(AttackRoll)"
data "StackId" "STACK_GROUP"
data "StatusGroups" "SG_Condition;SG_RemoveOnRespec"
```

### Key Fields

- **StatusId**: Unique identifier (entry name)
- **StatusType**: BOOST, INCAPACITATED, INVISIBLE, POLYMORPHED, etc.
- **Boosts**: Boost DSL string (e.g., "AC(2);Resistance(Fire,Resistant)")
- **StackId**: Determines if multiple instances stack or replace
- **StatusGroups**: Categorization (e.g., "SG_Incapacitated;SG_Condition")
- **Passives**: Passive abilities granted while status is active
- **RemoveEvents**: Events that trigger status removal

## Usage

### Loading BG3 Statuses

```csharp
var rulesEngine = new RulesEngine();
var statusManager = new StatusManager(rulesEngine);
var statusRegistry = new StatusRegistry();
var integration = new BG3StatusIntegration(statusManager, statusRegistry);

// Load all Status_*.txt files from BG3_Data/Statuses
int loaded = integration.LoadBG3Statuses("res://BG3_Data/Statuses");
Console.WriteLine($"Loaded {loaded} BG3 statuses");
```

### Applying a Status

```csharp
// Setup combatant resolver so boosts can be applied
statusManager.ResolveCombatant = combatantId => 
    _combatants.TryGetValue(combatantId, out var c) ? c : null;

// Apply BLESS status (grants +1d4 to attacks and saves)
var instance = integration.ApplyBG3Status(
    statusId: "BLESS",
    sourceId: "cleric_1",
    targetId: "fighter_1",
    duration: 10
);

// Boosts are automatically applied to the combatant
```

### Querying the Registry

```csharp
// Get specific status
var bless = statusRegistry.GetStatus("BLESS");
Console.WriteLine($"BLESS boosts: {bless.Boosts}");

// Get all BOOST type statuses
var boostStatuses = statusRegistry.GetStatusesByType(BG3StatusType.BOOST);

// Get all statuses with boost effects
var withBoosts = statusRegistry.GetStatusesWithBoosts();

// Get statistics
var stats = statusRegistry.GetStatistics();
// stats["Total"], stats["WithBoosts"], stats["BOOST"], etc.
```

### Status Lifecycle

When a status is applied:
1. `StatusManager.ApplyStatus()` creates a `StatusInstance`
2. `BG3StatusIntegration.HandleStatusApplied()` is triggered
3. Looks up `BG3StatusData` from registry
4. Parses the `Boosts` field using `BoostParser`
5. Applies boosts using `BoostApplicator.ApplyBoosts()`

When a status is removed:
1. `StatusManager.RemoveStatus()` removes the `StatusInstance`
2. `BG3StatusIntegration.HandleStatusRemoved()` is triggered
3. Removes all boosts using `BoostApplicator.RemoveBoosts()`

### Automatic Boost Management

The system automatically manages boosts—no manual cleanup required:

```csharp
// Apply BLESS - boosts added automatically
integration.ApplyBG3Status("BLESS", "cleric", "fighter", duration: 3);

// Turn processing - status duration decrements
statusManager.ProcessTurnEnd("fighter");  // Duration: 2
statusManager.ProcessTurnEnd("fighter");  // Duration: 1
statusManager.ProcessTurnEnd("fighter");  // Duration: 0, status removed

// Boosts automatically removed when status expires
```

## Example Statuses

### BLESS (Beneficial)

```
new entry "BLESS"
data "StatusType" "BOOST"
data "DisplayName" "Bless"
data "Description" "Gains a +1d4 bonus to Attack Rolls and Saving Throws."
data "Boosts" "RollBonus(Attack,1d4);RollBonus(SavingThrow,1d4);RollBonus(DeathSavingThrow,1d4)"
```

**Effect**: +1d4 to attack rolls, saving throws, and death saves

### BANE (Detrimental)

```
new entry "BANE"
data "StatusType" "BOOST"
data "DisplayName" "Bane"
data "Description" "Has a 1d4 penalty to Attack Rolls and Saving Throws."
data "Boosts" "RollBonus(Attack,-1d4);RollBonus(SavingThrow,-1d4);RollBonus(DeathSavingThrow,-1d4)"
```

**Effect**: -1d4 to attack rolls, saving throws, and death saves

### DIPPED_FIRE

```
new entry "DIPPED_FIRE"
data "StatusType" "BOOST"
data "DisplayName" "Dipped in Fire"
data "Description" "Weapon deals additional Fire damage."
data "Boosts" "WeaponDamage(1d4, Fire);IF(Item(context.Source)):WeaponProperty(Unstowable)"
```

**Effect**: Weapon deals +1d4 fire damage

### KNOCKED_OUT (Incapacitated)

```
new entry "KNOCKED_OUT_BASE"
data "StatusType" "INCAPACITATED"
data "DisplayName" "Knocked Out"
data "Boosts" "Lootable();AbilityFailedSavingThrow(Strength);AbilityFailedSavingThrow(Dexterity);Advantage(AttackTarget);CriticalHit(AttackTarget,Success,Always,3)"
```

**Effect**: Automatic failed STR/DEX saves, attackers have advantage, automatic crits within 3m

## Boost System Integration

The status system integrates seamlessly with the existing boost system:

### Boost Types Supported

- **AC(value)**: Armor class bonus
- **RollBonus(type, value)**: Bonus to rolls (Attack, SavingThrow, etc.)
- **Advantage(type)**: Advantage on rolls
- **Disadvantage(type)**: Disadvantage on rolls
- **Resistance(damageType, level)**: Damage resistance
- **WeaponDamage(dice, type)**: Extra weapon damage
- **StatusImmunity(statusId)**: Immunity to specific statuses
- **Conditional boosts**: `IF(condition):Boost1;Boost2`

### Boost Source Tracking

All boosts from statuses are tracked with:
- Source: `"Status"`
- SourceId: The status ID (e.g., `"BLESS"`)

This enables:
- Querying which boosts come from which statuses
- Automatic removal when status expires
- Debugging and inspection

## Testing

### Run Examples

```csharp
using QDND.Examples;

// Run all example scenarios
BG3StatusExamples.RunAllExamples();

// Or run specific examples
BG3StatusExamples.Example2_ApplyBlessStatus();
```

### Run Integration Tests

```csharp
using QDND.Tests.Integration;

BG3StatusIntegrationTests.RunAllTests();
```

### Test Coverage

- Status registry loading
- Status parser inheritance resolution
- BLESS status boost application
- BANE status negative boost application
- Status removal removes boosts
- Multiple statuses stack boosts correctly
- Status expiration auto-removes boosts
- Statuses without boosts don't error

## Error Handling

The system handles errors gracefully:

```csharp
// Unknown status - returns null, logs error
var instance = integration.ApplyBG3Status("UNKNOWN_STATUS", src, tgt);
// Output: [BG3StatusIntegration] Unknown BG3 status: UNKNOWN_STATUS

// Malformed boost string - logs error, doesn't crash
// Boost parse errors are caught and logged via GD.PrintErr

// Status without boosts - no error, just no boosts applied
```

## Performance

- **Lazy loading**: Statuses only loaded on demand
- **Fast lookups**: Dictionary-based registry with type/group indices
- **Efficient boost management**: Boosts added/removed in O(n) where n is boost count
- **Minimal overhead**: Event-driven integration, no polling

## Statistics

Query system statistics at runtime:

```csharp
var stats = integration.GetStatistics();

// Example output:
// Total: 1247
// WithBoosts: 423
// WithPassives: 156
// BOOST: 891
// INCAPACITATED: 45
// INVISIBLE: 12
// ActiveBoostSources: 8
```

## Future Enhancements

Potential improvements:

1. **OnApplyFunctors**: Parse and execute functor scripts
2. **OnRemoveFunctors**: Status removal effects
3. **OnTickFunctors**: Per-turn effects (damage over time, etc.)
4. **Conditional removal**: Parse RemoveEvents more thoroughly
5. **Passive integration**: Apply passive abilities from Passives field
6. **Status immunity**: Honor StatusImmunity boosts
7. **Visual effects**: Map status Icon paths to actual icons in UI

## Debugging

Enable detailed logging:

```csharp
// Boost application logged via GD.Print
// [BG3StatusIntegration] Applied 3 boosts from status 'BLESS' to fighter_1

// Boost removal logged
// [BG3StatusIntegration] Removed 3 boosts from status 'BLESS' on fighter_1

// Parser errors/warnings available
Console.WriteLine($"Errors: {statusRegistry.Errors.Count}");
Console.WriteLine($"Warnings: {statusRegistry.Warnings.Count}");
```

## Related Systems

- **Action Registry**: [action-registry.md](action-registry.md)
- **BG3 Spell Parser**: Similar parsing pattern for spells

## Files Created

### Data Layer
- `Data/Statuses/BG3StatusData.cs` - Status data model
- `Data/Parsers/BG3StatusParser.cs` - Status file parser
- `Data/Statuses/StatusRegistry.cs` - Status registry

### Integration Layer
- `Combat/Statuses/BG3StatusIntegration.cs` - Status-boost integration

### Examples & Tests
- `Examples/BG3StatusExamples.cs` - Usage examples
- `Tests/Integration/BG3StatusIntegrationTests.cs` - Integration tests

## Summary

This implementation provides:
✅ Complete BG3 status data parsing
✅ Inheritance resolution
✅ Automatic boost application/removal
✅ Registry with indexed queries
✅ Seamless StatusManager integration
✅ Comprehensive examples and tests
✅ Error handling and logging
✅ Zero-configuration boost lifecycle management
