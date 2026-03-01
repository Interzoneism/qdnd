# Action Registry System

## Overview

The Action Registry is a centralized service for managing all action definitions in the game, including BG3 spells, class abilities, weapon attacks, and custom actions. It provides efficient querying, filtering, and lookup capabilities for the combat system.

## Architecture

### Core Components

1. **ActionRegistry** (`Combat/Actions/ActionRegistry.cs`)
   - Centralized storage for all action definitions
   - Indexed by ID, tags, spell level, school, and other properties
   - Provides query methods for filtering actions by various criteria

2. **ActionDataLoader** (`Data/Actions/ActionDataLoader.cs`)
   - High-level API for loading actions from BG3 data files
   - Handles parsing via BG3SpellParser
   - Converts BG3SpellData to ActionDefinition via BG3ActionConverter
   - Reports errors, warnings, and statistics

3. **ActionRegistryInitializer** (`Data/Actions/ActionRegistryInitializer.cs`)
   - Main entry point for initializing the registry
   - Called during CombatArena startup
   - Provides timing and diagnostic reporting

4. **EffectPipeline Integration** (`Combat/Actions/EffectPipeline.cs`)
   - Enhanced to work with ActionRegistry
   - Falls back to registry when action not found locally
   - Maintains backward compatibility with existing code

## Usage

### Initialization

The action registry is automatically initialized during `CombatArena._Ready()`:

```csharp
// Automatic initialization in CombatArena
var actionRegistry = new ActionRegistry();
var initResult = ActionRegistryInitializer.Initialize(
    actionRegistry, 
    "BG3_Data", 
    verboseLogging: true);
```

### Querying Actions

#### Get Action by ID
```csharp
var fireball = registry.GetAction("Projectile_Fireball");
```

#### Get Actions by Tag
```csharp
// Get all healing spells
var healingSpells = registry.GetActionsByTag("healing");

// Get actions with multiple tags (AND logic)
var fireCantrips = registry.GetActionsByAllTags("cantrip", "fire");

// Get actions with any tag (OR logic)
var utilitySpells = registry.GetActionsByAnyTag("buff", "debuff", "utility");
```

#### Get Actions by Spell Level
```csharp
// Get all cantrips
var cantrips = registry.GetCantrips();

// Get level 3 spells
var level3Spells = registry.GetActionsBySpellLevel(3);
```

#### Get Actions by School
```csharp
var evocationSpells = registry.GetActionsBySchool(SpellSchool.Evocation);
```

#### Get Actions by Intent
```csharp
var damageActions = registry.GetActionsByIntent(VerbalIntent.Damage);
var healingActions = registry.GetActionsByIntent(VerbalIntent.Healing);
```

#### Get Actions by Casting Time
```csharp
var reactions = registry.GetActionsByCastingTime(CastingTimeType.Reaction);
var bonusActions = registry.GetActionsByCastingTime(CastingTimeType.BonusAction);
```

#### Specialized Queries
```csharp
// Get all damage-dealing actions
var damageActions = registry.GetDamageActions();

// Get all healing actions
var healingActions = registry.GetHealingActions();

// Get all concentration spells
var concentrationSpells = registry.GetConcentrationActions();

// Get all upcastable spells
var upcastableSpells = registry.GetUpcastableActions();
```

#### Custom Queries
```csharp
// Query with custom predicate
var rangedDamageCantrips = registry.Query(a => 
    a.SpellLevel == 0 && 
    a.Range > 5f && 
    a.Effects.Any(e => e.Type == "damage"));
```

### Registering Custom Actions

```csharp
var customAction = new ActionDefinition
{
    Id = "custom_lightning_strike",
    Name = "Lightning Strike",
    Description = "Call down a bolt of lightning",
    SpellLevel = 2,
    School = SpellSchool.Evocation,
    Tags = new HashSet<string> { "damage", "lightning" },
    // ... other properties
};

registry.RegisterAction(customAction);
```

### Statistics and Reporting

```csharp
// Get statistics dictionary
var stats = registry.GetStatistics();
Console.WriteLine($"Total actions: {stats["total"]}");
Console.WriteLine($"Cantrips: {stats["cantrips"]}");
Console.WriteLine($"Level 1 spells: {stats["level_1_spells"]}");

// Get formatted statistics report
Console.WriteLine(registry.GetStatisticsReport());
```

Example output (illustrative; values depend on currently loaded data packs):
```
=== Action Registry Statistics ===
Total Actions: <varies>

By Spell Level:
  Cantrips (0): <varies>
  Level 1: <varies>
  Level 2: <varies>
  Level 3: <varies>
  Level 4: <varies>
  Level 5: <varies>
  Level 6: <varies>
  Level 7: <varies>
  Level 8: <varies>
  Level 9: <varies>

By Type:
  Damage: <varies>
  Healing: <varies>
  Concentration: <varies>
  Upcastable: <varies>
  Reactions: <varies>
  Bonus Actions: <varies>

Top Tags:
  damage: <varies>
  spell: <varies>
  cantrip: <varies>
  concentration: <varies>
  aoe: <varies>
```

## Integration with Combat System

### EffectPipeline

The EffectPipeline automatically uses the ActionRegistry as a fallback:

```csharp
// In EffectPipeline
public ActionDefinition GetAction(string actionId)
{
    // Check local cache first
    if (_actions.TryGetValue(actionId, out var action))
        return action;
    
    // Fallback to centralized registry
    return ActionRegistry?.GetAction(actionId);
}
```

### Combatant Known Actions

Combatants reference action IDs that are resolved via the registry:

```csharp
var combatant = new Combatant(...);
combatant.KnownActions = new List<string> 
{ 
    "Projectile_Fireball", 
    "Target_CureWounds",
    "Shout_Bless"
};

// Actions are resolved from registry when needed
foreach (var actionId in combatant.KnownActions)
{
    var action = effectPipeline.GetAction(actionId);
    if (action != null)
    {
        // Use action...
    }
}
```

### AI Decision Making

The AI can query the registry to find appropriate actions:

```csharp
// Find best damage action for current situation
var availableDamageActions = registry.GetDamageActions()
    .Where(a => combatant.KnownActions.Contains(a.Id))
    .Where(a => effectPipeline.CanUseAbility(a.Id, combatant).CanUse);
```

## Data Loading

### ActionDataLoader API

The `ActionDataLoader` provides convenience methods for loading specific subsets:

```csharp
var loader = new ActionDataLoader();

// Load all spells
loader.LoadAllSpells("BG3_Data", registry);

// Load specific spell levels
loader.LoadCantrips("BG3_Data", registry);
loader.LoadLevel1Spells("BG3_Data", registry);
loader.LoadLevel2Spells("BG3_Data", registry);
// ... up to LoadLevel9Spells()

// Load by type
loader.LoadDamageSpells("BG3_Data", registry);
loader.LoadHealingSpells("BG3_Data", registry);

// Load by school
loader.LoadSpellsBySchool("BG3_Data", registry, SpellSchool.Evocation);

// Get diagnostics
Console.WriteLine(loader.GetLoadingSummary());
Console.WriteLine($"Loaded: {loader.LoadedCount}");
Console.WriteLine($"Failed: {loader.FailedCount}");
```

### Error Handling

```csharp
var loader = new ActionDataLoader();
loader.LoadAllSpells("BG3_Data", registry);

if (loader.Errors.Count > 0)
{
    Console.WriteLine("Errors encountered:");
    foreach (var error in loader.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}

if (loader.Warnings.Count > 0)
{
    Console.WriteLine("Warnings:");
    foreach (var warning in loader.Warnings)
    {
        Console.WriteLine($"  - {warning}");
    }
}
```

## Performance Considerations

### Indexing

The registry maintains multiple indices for fast lookup:
- **ID Index**: O(1) lookup by action ID
- **Tag Index**: Fast retrieval of actions by tag
- **Spell Level Index**: Fast retrieval by spell level
- **School Index**: Fast retrieval by spell school

### Lazy Loading

For optimal startup time, lazy loading is supported:

```csharp
// Create empty registry
var registry = ActionRegistryInitializer.CreateLazyRegistry();

// Load specific subsets as needed
var loader = new ActionDataLoader();
loader.LoadCantrips("BG3_Data", registry);  // Only load cantrips

// Load more later
loader.LoadLevel1Spells("BG3_Data", registry);
```

### Memory Usage

- Average memory per action: ~2-4 KB
- 500 actions ≈ 1-2 MB
- Indices add ~10-20% overhead
- Total memory for full registry: ~1.5-2.5 MB

## Testing

### Quick Initialization for Tests

```csharp
// Quick initialization with defaults
var registry = ActionRegistryInitializer.QuickInitialize();

// Use in tests
var fireball = registry.GetAction("Projectile_Fireball");
Assert.NotNull(fireball);
Assert.Equal(3, fireball.SpellLevel);
```

### Manual Registration for Unit Tests

```csharp
var registry = new ActionRegistry();
registry.RegisterAction(new ActionDefinition 
{ 
    Id = "test_action",
    Name = "Test Action"
});

Assert.True(registry.HasAction("test_action"));
```

## Backward Compatibility

The system maintains full backward compatibility:

1. **Existing Code**: Works unchanged - actions registered via `EffectPipeline.RegisterAction()` still work
2. **DataRegistry**: Legacy actions from JSON files are still loaded and registered
3. **Fallback Chain**: Local EffectPipeline cache → ActionRegistry → null

## Future Enhancements

Potential future improvements:

1. **Hot Reload**: Support runtime reloading of action definitions
2. **Modding Support**: Load custom actions from mod directories
3. **Validation**: Enhanced validation for action definitions
4. **Serialization**: Save/load action customizations
5. **Analytics**: Track action usage statistics
6. **Dynamic Actions**: Generate actions procedurally at runtime
7. **Caching**: Pre-filtered caches for common queries

## Troubleshooting

### Actions Not Found

If an action isn't found, check:

1. Is the BG3 spell file parsed correctly?
2. Did initialization complete successfully?
3. Are there errors in the initialization result?
4. Is the action ID correct (case-sensitive)?

```csharp
// Debug action loading
var initResult = ActionRegistryInitializer.Initialize(registry, "BG3_Data", true);
if (!initResult.Success)
{
    Console.WriteLine($"Init failed: {initResult.ErrorMessage}");
}

// Check if action exists
if (!registry.HasAction("Projectile_Fireball"))
{
    Console.WriteLine("Fireball not found!");
    Console.WriteLine($"Total actions: {registry.Count}");
}
```

### Performance Issues

If initialization is slow:

1. Use lazy loading for specific subsets
2. Disable verbose logging
3. Check for duplicate file parsing
4. Profile with Stopwatch

```csharp
var stopwatch = Stopwatch.StartNew();
var initResult = ActionRegistryInitializer.Initialize(
    registry, 
    "BG3_Data", 
    verboseLogging: false);
stopwatch.Stop();
Console.WriteLine($"Init took {stopwatch.ElapsedMilliseconds}ms");
```

## Related Documentation

- [BG3 Action Converter](bg3-action-converter.md)
- [BG3 Spell Parser](bg3-spell-parser.md)
