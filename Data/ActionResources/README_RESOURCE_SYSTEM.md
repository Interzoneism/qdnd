# Action Resource System

Comprehensive BG3-style resource management system for tracking spell slots, class resources (rage, ki, etc.), and action economy.

## Overview

The Action Resource System provides:

- **Full BG3 resource definitions** loaded from `BG3_Data/ActionResourceDefinitions.lsx`
- **Leveled resources** (spell slots with levels 1-9)
- **Simple resources** (rage, ki, channel divinity, etc.)
- **Automatic replenishment** based on ReplenishType (Turn, ShortRest, Rest)
- **Resource validation** before actions
- **Integration with combat** via ResourceManager service

## Core Components

### 1. ResourcePool (`Combat/Services/ResourcePool.cs`)

Enhanced resource pool that tracks all action resources for a combatant.

```csharp
// Access via combatant
var pool = combatant.ActionResources;

// Check resource availability
bool hasSlot = pool.Has("SpellSlot", amount: 1, level: 3);
bool hasKi = pool.Has("KiPoint", amount: 2);

// Consume resources
bool consumed = pool.Consume("SpellSlot", amount: 1, level: 3);

// Restore resources
pool.Restore("Rage", amount: 1);
pool.RestoreAll(); // Full restoration (long rest)

// Replenish by type
pool.ReplenishResources(ReplenishType.Turn); // Actions, bonus, reaction
pool.ReplenishResources(ReplenishType.ShortRest); // Ki, rage, etc.
```

### 2. ResourceManager (`Combat/Services/ResourceManager.cs`)

Service for managing resources across combatants.

```csharp
var resourceManager = new ResourceManager();

// Initialize combatant resources from character build
resourceManager.InitializeResources(combatant);

// Validate spell/action costs
var (canPay, reason) = resourceManager.CanPayCost(combatant, spellUseCost);

// Consume resources
bool success = resourceManager.ConsumeCost(combatant, spellUseCost, out string error);

// Replenish resources
resourceManager.ReplenishTurnResources(combatant);     // End of turn
resourceManager.ReplenishShortRest(combatant);        // Short rest
resourceManager.ReplenishLongRest(combatant);         // Long rest

// Get resource status for UI
var status = resourceManager.GetResourceStatus(combatant);
```

### 3. ActionBudget Integration (`Combat/Actions/ActionBudget.cs`)

Enhanced ActionBudget with ResourceManager integration.

```csharp
// Check if spell cost can be paid (includes resource validation)
var (canPay, reason) = combatant.ActionBudget.CanPaySpellCost(
    useCost, 
    resourceManager, 
    combatant
);

// Consume spell cost (action economy + resources)
bool success = combatant.ActionBudget.ConsumeSpellCost(
    useCost, 
    resourceManager, 
    combatant, 
    out string error
);
```

## Resource Types

### Action Economy (Replenish: Turn)
- **ActionPoint** - Main action (1 per turn)
- **BonusActionPoint** - Bonus action (1 per turn)
- **ReactionActionPoint** - Reaction (1 per turn)
- **Movement** - Movement speed

### Spell Resources

#### Spell Slots (Replenish: Rest)
- **SpellSlot** - Standard spell slots (levels 1-9)
- **WarlockSpellSlot** - Warlock pact magic slots
- **ShadowSpellSlot** - Shadow monk slots

#### Other Spell Resources
- **SorceryPoint** - Sorcerer metamagic (Replenish: Rest)
- **ArcaneRecoveryPoint** - Wizard recovery (Replenish: Rest)
- **RitualPoint** - Ritual casting points

### Class Resources

#### Barbarian
- **Rage** - Rage charges (Replenish: Rest)
  - Level 3: 3 charges
  - Level 6: 4 charges
  - Level 12: 5 charges
  - Level 17: 6 charges

#### Monk
- **KiPoint** - Ki points (Replenish: ShortRest)
  - Charges = Monk level

#### Cleric
- **ChannelDivinity** - Channel divinity (Replenish: ShortRest)
  - Level 2: 1 charge
  - Level 6: 2 charges
  - Level 18: 3 charges

#### Paladin
- **ChannelOath** - Channel oath (Replenish: ShortRest)
- **LayOnHandsCharge** - Lay on hands pool (Replenish: Rest)
  - Charges = Paladin level × 5

#### Bard
- **BardicInspiration** - Inspiration dice (Replenish: ShortRest)

#### Druid
- **WildShape** - Wild shape charges (Replenish: ShortRest)
  - 2 charges

#### Fighter
- **SuperiorityDie** - Battle master dice (Replenish: ShortRest)
- **ExtraActionPoint** - Action surge (Replenish: ShortRest)
  - Level 2: 1 charge
  - Level 17: 2 charges

## Usage Examples

### Example 1: Initialize a Wizard

```csharp
var resourceManager = new ResourceManager();
var wizard = new Combatant("wizard1", "Gale", Faction.Player, 38, 15);

wizard.ResolvedCharacter = new ResolvedCharacter
{
    ClassLevels = new Dictionary<string, int> { { "Wizard", 5 } },
    TotalLevel = 5
};

// Initialize resources
resourceManager.InitializeResources(wizard);

// Set spell slots (Level 5: 4/3/2)
wizard.ActionResources.SetMax("SpellSlot", 4, level: 1);
wizard.ActionResources.SetMax("SpellSlot", 3, level: 2);
wizard.ActionResources.SetMax("SpellSlot", 2, level: 3);
```

### Example 2: Cast a Spell

```csharp
// Create spell cost (Magic Missile - 1st level, action)
var magicMissileCost = new SpellUseCost
{
    ActionPoint = 1,
    SpellSlotLevel = 1,
    SpellSlotCount = 1
};

// Validate
var (canCast, reason) = resourceManager.CanPayCost(wizard, magicMissileCost);

if (canCast)
{
    // Consume resources
    resourceManager.ConsumeCost(wizard, magicMissileCost, out string error);
    Console.WriteLine("Cast Magic Missile!");
}
```

### Example 3: Monk Using Ki

```csharp
var monk = new Combatant("monk1", "Shadow", Faction.Player, 40, 18);
monk.ResolvedCharacter = new ResolvedCharacter
{
    ClassLevels = new Dictionary<string, int> { { "Monk", 6 } },
    TotalLevel = 6
};

resourceManager.InitializeResources(monk);

// Flurry of Blows (1 ki + bonus action)
var flurryCost = new SpellUseCost
{
    BonusActionPoint = 1,
    CustomResources = new Dictionary<string, int> { { "KiPoint", 1 } }
};

if (resourceManager.CanPayCost(monk, flurryCost).CanPay)
{
    resourceManager.ConsumeCost(monk, flurryCost, out _);
    Console.WriteLine("Used Flurry of Blows!");
}
```

### Example 4: Warlock Pact Magic

```csharp
var warlock = new Combatant("warlock1", "Wyll", Faction.Player, 35, 12);
warlock.ResolvedCharacter = new ResolvedCharacter
{
    ClassLevels = new Dictionary<string, int> { { "Warlock", 5 } },
    TotalLevel = 5
};

resourceManager.InitializeResources(warlock);

// Warlocks get 2 slots at 3rd level
warlock.ActionResources.SetMax("WarlockSpellSlot", 2, level: 3);

// Cast Hex (using 3rd level pact slot)
var hexCost = new SpellUseCost
{
    BonusActionPoint = 1,
    SpellSlotLevel = 3,
    SpellSlotCount = 1
};

resourceManager.ConsumeCost(warlock, hexCost, out _);
```

### Example 5: Rest and Replenishment

```csharp
// End of turn - replenish action economy
resourceManager.ReplenishTurnResources(fighter);

// Short rest - replenish short rest resources (ki, rage, superiority dice, etc.)
resourceManager.ReplenishShortRest(fighter);

// Long rest - restore all resources
resourceManager.ReplenishLongRest(fighter);
```

### Example 6: Multi-class Spellcasting

```csharp
var paladin = new Combatant("paladin1", "Custom", Faction.Player, 55, 16);
paladin.ResolvedCharacter = new ResolvedCharacter
{
    ClassLevels = new Dictionary<string, int>
    {
        { "Paladin", 6 },
        { "Warlock", 2 }
    },
    TotalLevel = 8
};

resourceManager.InitializeResources(paladin);

// Paladin slots (from level 6)
paladin.ActionResources.SetMax("SpellSlot", 4, level: 1);
paladin.ActionResources.SetMax("SpellSlot", 2, level: 2);

// Warlock pact slots (from level 2)
paladin.ActionResources.SetMax("WarlockSpellSlot", 2, level: 1);

// Lay on Hands (5 per paladin level)
paladin.ActionResources.SetMax("LayOnHandsCharge", 30);
```

## Integration with Combat

### CombatContext Registration

Register ResourceManager as a service:

```csharp
var resourceManager = new ResourceManager();
CombatContext.Instance.RegisterService(resourceManager);
```

### Turn Queue Integration

When advancing turns, replenish turn resources:

```csharp
public void AdvanceTurn()
{
    var next = GetNextCombatant();
    var resourceManager = CombatContext.Instance.GetService<ResourceManager>();
    
    // Replenish turn resources
    resourceManager.ReplenishTurnResources(next);
    
    // Also reset action budget
    next.ActionBudget.ResetForTurn();
}
```

### Action Execution

When executing spells/actions:

```csharp
public bool ExecuteSpell(Combatant caster, SpellUseCost cost)
{
    var resourceManager = CombatContext.Instance.GetService<ResourceManager>();
    
    // Validate
    var (canPay, reason) = resourceManager.CanPayCost(caster, cost);
    if (!canPay)
    {
        ShowError(reason);
        return false;
    }
    
    // Consume
    if (!resourceManager.ConsumeCost(caster, cost, out string error))
    {
        ShowError(error);
        return false;
    }
    
    // Execute spell...
    return true;
}
```

## Backward Compatibility

The system maintains backward compatibility:

- **Old system**: `combatant.ResourcePool` (type: `CombatantResourcePool`)
  - Simple key-value resource tracking
  - Still works for basic scenarios
  - Marked as deprecated

- **New system**: `combatant.ActionResources` (type: `ResourcePool`)
  - Full BG3-style resource management
  - Supports leveled resources
  - Integrates with ResourceManager

Both systems coexist, allowing gradual migration.

## Resource Definition Loading

Resources are loaded from `BG3_Data/ActionResourceDefinitions.lsx`:

```csharp
// Automatic loading in ResourceManager constructor
var resourceManager = new ResourceManager();

// Access definitions
var rageDef = resourceManager.GetDefinition("Rage");
Console.WriteLine($"{rageDef.DisplayName}: {rageDef.Description}");
Console.WriteLine($"Replenishes: {rageDef.ReplenishType}");
```

## UI Display

Get formatted resource status:

```csharp
var status = resourceManager.GetResourceStatus(combatant);

foreach (var (name, value) in status)
{
    Console.WriteLine($"{name}: {value}");
}

// Example output:
// Spell Slots: L1:3/4, L2:2/3, L3:1/2
// Ki Points: 6/6
// Rage: 3/4
```

## Testing

See `Data/ActionResources/ResourceSystemExample.cs` for comprehensive examples.

Run all examples:

```csharp
ResourceSystemExample.RunAllExamples();
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                  CombatContext                       │
│  (Service locator for combat subsystems)            │
│                                                      │
│  Services:                                           │
│  - ResourceManager                                   │
│  - TurnQueueService                                  │
│  - EncounterService                                  │
│  - etc.                                              │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│              ResourceManager                         │
│  - Initialize resources from character data         │
│  - Validate costs (CanPayCost)                      │
│  - Consume resources (ConsumeCost)                  │
│  - Replenish resources (by ReplenishType)           │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│                  Combatant                           │
│                                                      │
│  Properties:                                         │
│  - ActionResources (ResourcePool)                   │
│  - ActionBudget (action economy)                    │
│  - ResolvedCharacter (character build)              │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│               ResourcePool                           │
│  - Dictionary<string, ResourceInstance>             │
│  - Consume/Restore/Replenish resources              │
│  - Event: OnResourcesChanged                        │
└──────────────────┬──────────────────────────────────┘
                   │
                   ▼
┌─────────────────────────────────────────────────────┐
│            ResourceInstance                          │
│  - ActionResourceDefinition (from BG3 data)         │
│  - Current/Max values (simple resources)            │
│  - CurrentByLevel/MaxByLevel (spell slots)          │
└─────────────────────────────────────────────────────┘
```

## Future Enhancements

- [ ] Automatic resource initialization from class progression
- [ ] Subclass-specific resources (Battle Master, etc.)
- [ ] Resource-based ability filtering in action bar
- [ ] Resource cost tooltips in UI
- [ ] Resource restoration items/abilities
- [ ] Custom resource types for homebrew content
- [ ] Resource event logging for combat log
- [ ] Multiclass spell slot calculation
- [ ] Resource persistence in save files

## See Also

- [ActionResourceDefinition.cs](ActionResourceDefinition.cs) - Resource definition model
- [ActionResourceLoader.cs](ActionResourceLoader.cs) - Loading BG3 data
- [ResourceSystemExample.cs](ResourceSystemExample.cs) - Usage examples
- [BG3_Data/ActionResourceDefinitions.lsx](../../BG3_Data/ActionResourceDefinitions.lsx) - BG3 resource data
