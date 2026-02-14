# BG3 Status System - Quick Reference

## Quick Start

```csharp
using QDND.Combat.Rules;
using QDND.Combat.Statuses;
using QDND.Data.Statuses;

// 1. Setup
var rulesEngine = new RulesEngine();
var statusManager = new StatusManager(rulesEngine);
var statusRegistry = new StatusRegistry();
var integration = new BG3StatusIntegration(statusManager, statusRegistry);

// 2. Load BG3 statuses
integration.LoadBG3Statuses("res://BG3_Data/Statuses");

// 3. Enable combatant resolution (for boost application)
statusManager.ResolveCombatant = combatantId => 
    _combatants.TryGetValue(combatantId, out var c) ? c : null;

// 4. Apply status
integration.ApplyBG3Status("BLESS", "cleric", "fighter", duration: 10);

// Boosts are automatically applied!
```

## Loading Statuses

```csharp
// Load all Status_*.txt files
var count = integration.LoadBG3Statuses("res://BG3_Data/Statuses");

// Or use registry directly
var registry = new StatusRegistry();
registry.LoadStatuses("res://BG3_Data/Statuses");
```

## Querying Statuses

```csharp
// Get specific status
var bless = registry.GetStatus("BLESS");

// Get by type
var boostStatuses = registry.GetStatusesByType(BG3StatusType.BOOST);
var incapacitated = registry.GetStatusesByType(BG3StatusType.INCAPACITATED);

// Get by group
var conditions = registry.GetStatusesByGroup("SG_Condition");

// Get all with boosts
var withBoosts = registry.GetStatusesWithBoosts();

// Statistics
var stats = registry.GetStatistics();
// stats["Total"], stats["WithBoosts"], stats["BOOST"], etc.
```

## Applying Statuses

```csharp
// Apply status (boosts auto-applied)
var instance = integration.ApplyBG3Status(
    statusId: "BLESS",
    sourceId: "cleric_1",
    targetId: "fighter_1",
    duration: 10
);

// Or use StatusManager directly (also works)
statusManager.ApplyStatus("BLESS", "cleric_1", "fighter_1", duration: 10);
```

## Checking Active Statuses

```csharp
// Get all statuses on a combatant
var statuses = statusManager.GetStatuses("fighter_1");

// Check if has specific status
bool hasBlessed = statusManager.HasStatus("fighter_1", "BLESS");

// Get active boosts
var boosts = combatant.Boosts.AllBoosts;
var blessBoosts = combatant.Boosts.GetBoostsFromSource("Status", "BLESS");
```

## Removing Statuses

```csharp
// Remove specific status (boosts auto-removed)
statusManager.RemoveStatus("fighter_1", "BLESS");

// Remove all matching filter
statusManager.RemoveStatuses("fighter_1", s => s.Definition.IsBuff);

// Remove on attack (hidden status, etc.)
statusManager.RemoveStatusesOnAttack("rogue_1");
```

## Status Lifecycle

```csharp
// Turn processing (decrements duration)
statusManager.ProcessTurnEnd("fighter_1");

// Round processing
statusManager.ProcessRoundEnd();

// Expired statuses automatically remove their boosts
```

## Common Status IDs

### Buffs
- `BLESS` - +1d4 to attacks and saves
- `SHIELD_OF_FAITH` - +2 AC
- `RAGE` - Damage bonus, resistance
- `INVISIBILITY` - Invisible status

### Debuffs
- `BANE` - -1d4 to attacks and saves
- `POISONED` - Disadvantage on attacks
- `FRIGHTENED` - Fear effects
- `PARALYZED` - Incapacitated

### Conditions
- `PRONE` - Prone condition
- `BURNING` - Fire damage over time
- `BLEEDING` - Bleed damage
- `KNOCKED_OUT` - Unconscious

## Boost Integration

```csharp
// Statuses with Boosts field automatically apply boosts
// Example: BLESS has:
// Boosts: "RollBonus(Attack,1d4);RollBonus(SavingThrow,1d4)"

// Applied as:
combatant.Boosts.GetBoostsFromSource("Status", "BLESS")
// Returns: 3 boosts (Attack, SavingThrow, DeathSavingThrow)

// Removed when status expires or is manually removed
```

## Status Types

```csharp
public enum BG3StatusType
{
    BOOST,          // Stat modifiers (buffs/debuffs)
    INCAPACITATED,  // Can't take actions
    POLYMORPHED,    // Shape changed
    INVISIBLE,      // Hidden from view
    KNOCKED_DOWN,   // Prone/knocked down
    SNEAKING,       // Stealthed
    DOWNED,         // Death saves
    FEAR,           // Frightened
    HEAL,           // Healing over time
    EFFECT          // Special effects
}
```

## Status Data Fields

```csharp
var status = registry.GetStatus("BLESS");

status.StatusId         // "BLESS"
status.DisplayName      // "Bless"
status.Description      // "Gains a +1d4 bonus..."
status.StatusType       // BG3StatusType.BOOST
status.Boosts           // "RollBonus(Attack,1d4);..."
status.StackId          // "BLESS"
status.StatusGroups     // "SG_RemoveOnRespec"
status.Icon             // "res://assets/..."
```

## Error Handling

```csharp
// Unknown status returns null
var instance = integration.ApplyBG3Status("UNKNOWN", src, tgt);
// Returns: null, logs error

// Check errors/warnings
Console.WriteLine($"Errors: {registry.Errors.Count}");
Console.WriteLine($"Warnings: {registry.Warnings.Count}");
foreach (var error in registry.Errors)
    Console.WriteLine($"  - {error}");
```

## Examples

### Example: Apply BLESS
```csharp
var instance = integration.ApplyBG3Status("BLESS", "cleric", "fighter", 10);
// fighter now has +1d4 to attacks and saves for 10 turns
```

### Example: Apply BANE
```csharp
integration.ApplyBG3Status("BANE", "wizard", "enemy", 10);
// enemy now has -1d4 to attacks and saves for 10 turns
```

### Example: Multiple Statuses
```csharp
integration.ApplyBG3Status("BLESS", "cleric", "fighter");
integration.ApplyBG3Status("RAGE", "self", "fighter");
// fighter has both BLESS and RAGE boosts active
```

### Example: Status Expiration
```csharp
integration.ApplyBG3Status("BLESS", "cleric", "fighter", duration: 2);
statusManager.ProcessTurnEnd("fighter"); // Turn 1
statusManager.ProcessTurnEnd("fighter"); // Turn 2, status expires
// Boosts automatically removed
```

## Performance Tips

- Load statuses once at startup, reuse registry
- Use indexed queries (`GetStatusesByType`) over filtering all
- Enable combatant resolver only when needed
- Clear registry with `registry.Clear()` if reloading

## Debugging

```csharp
// Enable detailed logging (already built-in)
// Output example:
// [BG3StatusIntegration] Applied 3 boosts from status 'BLESS' to fighter_1
// [BG3StatusIntegration] Removed 3 boosts from status 'BLESS' on fighter_1

// Check boost sources
foreach (var boost in combatant.Boosts.AllBoosts)
{
    Console.WriteLine($"{boost.Source}/{boost.SourceId}: {boost.Definition.Type}");
}
// Output:
// Status/BLESS: RollBonus
// Status/BLESS: RollBonus
// Status/RAGE: DamageBonus
```

## See Also

- [bg3-status-system.md](bg3-status-system.md) - Complete documentation
- [IMPLEMENTATION_SUMMARY_BG3_STATUS_SYSTEM.md](../IMPLEMENTATION_SUMMARY_BG3_STATUS_SYSTEM.md) - Implementation details
- [BOOST_DSL_QUICK_REFERENCE.md](../BOOST_DSL_QUICK_REFERENCE.md) - Boost syntax reference
