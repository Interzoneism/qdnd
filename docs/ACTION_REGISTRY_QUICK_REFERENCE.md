# Action Registry - Quick Reference

## Quick Start

### In CombatArena (Already Integrated)
```csharp
// Automatically initialized during CombatArena._Ready()
// Access via combat context:
var registry = _combatContext.GetService<ActionRegistry>();
```

### Manual Initialization
```csharp
using QDND.Combat.Actions;
using QDND.Data.Actions;

// Quick way
var registry = ActionRegistryInitializer.QuickInitialize();

// Or with full control
var registry = new ActionRegistry();
var result = ActionRegistryInitializer.Initialize(registry, "BG3_Data", verboseLogging: true);
```

## Common Queries

### Get Specific Action
```csharp
var fireball = registry.GetAction("Projectile_Fireball");
```

### Get All Cantrips
```csharp
var cantrips = registry.GetCantrips();
```

### Get Level 1-9 Spells
```csharp
var level3Spells = registry.GetActionsBySpellLevel(3);
```

### Get Damage/Healing Actions
```csharp
var damageSpells = registry.GetDamageActions();
var healingSpells = registry.GetHealingActions();
```

### Get by Tag
```csharp
// Single tag
var fireSpells = registry.GetActionsByTag("fire");

// Multiple tags (AND)
var fireCantrips = registry.GetActionsByAllTags("cantrip", "fire");

// Multiple tags (OR)
var utilitySpells = registry.GetActionsByAnyTag("buff", "debuff", "utility");
```

### Get by School
```csharp
var evocationSpells = registry.GetActionsBySchool(SpellSchool.Evocation);
```

### Get by Casting Time
```csharp
var reactions = registry.GetActionsByCastingTime(CastingTimeType.Reaction);
var bonusActions = registry.GetActionsByCastingTime(CastingTimeType.BonusAction);
```

### Custom Queries
```csharp
var rangedDamageCantrips = registry.Query(a => 
    a.SpellLevel == 0 && 
    a.Range > 5f && 
    a.Effects.Any(e => e.Type == "damage"));
```

## Statistics

### Get Statistics Dictionary
```csharp
var stats = registry.GetStatistics();
Console.WriteLine($"Total: {stats["total"]}");
Console.WriteLine($"Cantrips: {stats["cantrips"]}");
Console.WriteLine($"Damage actions: {stats["damage_actions"]}");
```

### Get Formatted Report
```csharp
Console.WriteLine(registry.GetStatisticsReport());
```

## Register Custom Action

```csharp
var customAction = new ActionDefinition
{
    Id = "custom_spell",
    Name = "Custom Spell",
    SpellLevel = 2,
    School = SpellSchool.Evocation,
    Tags = new HashSet<string> { "damage", "fire" },
    // ... other properties
};

registry.RegisterAction(customAction);
```

## Integration with Combat

### From Combatant Known Actions
```csharp
foreach (var actionId in combatant.KnownActions)
{
    var action = effectPipeline.GetAction(actionId);
    if (action != null)
    {
        // Use action
    }
}
```

### From AI Decision Pipeline
```csharp
var availableActions = registry.GetDamageActions()
    .Where(a => combatant.KnownActions.Contains(a.Id))
    .Where(a => effectPipeline.CanUseAbility(a.Id, combatant).CanUse);
```

## Testing

### Run Verification Test
```csharp
using QDND.Tests;

// Full test
ActionRegistryVerificationTest.RunTest();

// Quick test
ActionRegistryVerificationTest.RunQuickTest();
```

## Common Use Cases

### Find Best Spell for Situation
```csharp
// Find highest level fireball-like spell the combatant knows
var fireballSpells = registry.GetDamageActions()
    .Where(a => combatant.KnownActions.Contains(a.Id))
    .Where(a => a.TargetType == TargetType.Circle)
    .Where(a => a.Effects.Any(e => e.DamageType == "fire"))
    .OrderByDescending(a => a.SpellLevel);

var bestFireball = fireballSpells.FirstOrDefault();
```

### Get All Available Reactions for Character
```csharp
var availableReactions = registry.GetActionsByCastingTime(CastingTimeType.Reaction)
    .Where(a => combatant.KnownActions.Contains(a.Id))
    .Where(a => effectPipeline.CanUseAbility(a.Id, combatant).CanUse);
```

### Find Spells That Don't Require Concentration
```csharp
var nonConcentrationSpells = registry.Query(a => 
    !a.RequiresConcentration && 
    combatant.KnownActions.Contains(a.Id));
```

### Get All Healing Options
```csharp
var healingOptions = registry.GetHealingActions()
    .Where(a => combatant.KnownActions.Contains(a.Id))
    .OrderBy(a => a.SpellLevel); // Order by spell slot cost
```

## File Locations

- **ActionRegistry.cs**: `Combat/Actions/ActionRegistry.cs`
- **ActionDataLoader.cs**: `Data/Actions/ActionDataLoader.cs`
- **ActionRegistryInitializer.cs**: `Data/Actions/ActionRegistryInitializer.cs`
- **Documentation**: `docs/action-registry.md`
- **Test**: `Tests/ActionRegistryVerificationTest.cs`

## Key Properties

### Performance
- Initialization: ~100-500ms for ~500 spells
- Query by ID: O(1)
- Query by tag: O(k) where k = actions with tag
- Memory: ~1.5-2.5 MB for full registry

### Features
- ✅ Centralized action storage
- ✅ Multi-index for fast queries
- ✅ BG3 spell integration
- ✅ Lazy loading support
- ✅ Error handling
- ✅ Statistics reporting
- ✅ Backward compatible

## Troubleshooting

### Actions Not Loading
Check initialization result:
```csharp
if (!initResult.Success)
{
    Console.WriteLine($"Error: {initResult.ErrorMessage}");
    foreach (var error in initResult.Errors)
        Console.WriteLine($"  {error}");
}
```

### Action Not Found
Verify action exists:
```csharp
if (!registry.HasAction("Projectile_Fireball"))
{
    Console.WriteLine("Fireball not in registry!");
    Console.WriteLine($"Total actions: {registry.Count}");
    Console.WriteLine("Available actions:");
    foreach (var id in registry.GetAllActionIds().Take(10))
        Console.WriteLine($"  - {id}");
}
```

### Slow Performance
Use lazy loading:
```csharp
var registry = ActionRegistryInitializer.CreateLazyRegistry();
var loader = new ActionDataLoader();
loader.LoadCantrips("BG3_Data", registry); // Only load what you need
```
