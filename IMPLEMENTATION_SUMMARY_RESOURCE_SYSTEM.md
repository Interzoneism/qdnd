# Action Resource System - Implementation Summary

## What Was Implemented

A comprehensive BG3-style action resource management system for tracking spell slots, class-specific resources (rage, ki, etc.), and action economy integration.

## Files Created

### 1. `Combat/Services/ResourcePool.cs`
Enhanced resource pool supporting:
- **Leveled resources** (spell slots with levels 1-9)
- **Simple resources** (rage, ki, channel divinity, etc.)
- Current/max value tracking per resource
- Resource consumption and restoration
- Event notifications on resource changes

**Key Classes:**
- `ResourceInstance` - Single resource with current/max values
- `ResourcePool` - Collection of all resources for a combatant

### 2. `Combat/Services/ResourceManager.cs`
Service for managing resources across all combatants:
- Loads ActionResourceDefinitions from BG3 data
- Initializes resources based on character build (class, level)
- Validates resource costs (CanPayCost)
- Consumes resources (ConsumeCost)
- Replenishes resources by type (Turn, ShortRest, Rest)
- Provides resource status for UI display

**Supported Classes:**
- Barbarian (Rage charges)
- Monk (Ki Points)
- Cleric (Channel Divinity)
- Paladin (Channel Oath, Lay on Hands)
- Bard (Bardic Inspiration)
- Druid (Wild Shape)
- Fighter (Superiority Die, Action Surge)
- Sorcerer (Sorcery Points)
- Wizard (spell slots)
- Warlock (Pact Magic slots)

### 3. `Data/ActionResources/ResourceSystemExample.cs`
Comprehensive usage examples:
- Initialize wizard with spell slots
- Cast spells and consume resources
- Monk using Ki for Flurry of Blows
- Warlock pact magic
- Rest and replenishment
- Multi-class spellcasting (Paladin/Warlock)
- UI resource status display

### 4. `Data/ActionResources/README_RESOURCE_SYSTEM.md`
Complete documentation covering:
- System overview and architecture
- Core components and their responsibilities
- All resource types (action economy, spell resources, class resources)
- Usage examples for each class
- Integration with combat system
- Backward compatibility notes
- Future enhancement roadmap

## Files Modified

### 1. `Combat/Actions/ActionBudget.cs`
Enhanced with ResourceManager integration:
- Added `CanPaySpellCost()` - validates spell costs including resources
- Added `ConsumeSpellCost()` - consumes both action economy and resources
- Maintains backward compatibility with existing code
- Works seamlessly with or without ResourceManager

### 2. `Combat/Entities/Combatant.cs`
Added new resource system:
- `ActionResources` property (type: `ResourcePool`) - new BG3-style system
- `ResourcePool` property preserved for backward compatibility (deprecated)
- Both systems coexist during migration period

## How It Works

### Initialization Flow
```
1. Create Combatant with ResolvedCharacter (from character creation)
2. ResourceManager.InitializeResources(combatant)
   ├─ Adds core resources (ActionPoint, BonusActionPoint, Reaction)
   ├─ Reads spell slots from ResolvedCharacter.Resources
   │  ├─ "spell_slot_1" → SpellSlot level 1
   │  ├─ "spell_slot_2" → SpellSlot level 2
   │  ├─ "pact_slots" → WarlockSpellSlot
   │  └─ etc.
   └─ Adds class-specific resources based on character.Sheet.ClassLevels
      ├─ Barbarian → Rage
      ├─ Monk → KiPoint
      ├─ Cleric → ChannelDivinity
      └─ etc.
```

### Resource Consumption Flow
```
1. Create SpellUseCost (from spell definition)
   ├─ ActionPoint: 1
   ├─ SpellSlotLevel: 3
   └─ SpellSlotCount: 1

2. Validate: resourceManager.CanPayCost(combatant, useCost)
   ├─ Check action economy (ActionBudget)
   ├─ Check spell slots (ResourcePool)
   └─ Check custom resources (ki, rage, etc.)

3. Consume: resourceManager.ConsumeCost(combatant, useCost)
   ├─ Deduct action points
   ├─ Deduct spell slot
   └─ Fire OnResourcesChanged event
```

### Replenishment Flow
```
End of Turn:
  resourceManager.ReplenishTurnResources(combatant)
    → Restores ActionPoint, BonusActionPoint, ReactionActionPoint

Short Rest:
  resourceManager.ReplenishShortRest(combatant)
    → Restores KiPoint, Rage, ChannelDivinity, WildShape, etc.

Long Rest:
  resourceManager.ReplenishLongRest(combatant)
    → Restores ALL resources (spell slots, class resources, etc.)
```

## Key Features

### 1. Leveled Resources (Spell Slots)
```csharp
// Set max spell slots by level
pool.SetMax("SpellSlot", count: 4, level: 1);
pool.SetMax("SpellSlot", count: 3, level: 2);

// Check availability
bool hasSlot = pool.Has("SpellSlot", amount: 1, level: 3);

// Consume
pool.Consume("SpellSlot", amount: 1, level: 3);
```

### 2. Simple Resources (Rage, Ki, etc.)
```csharp
// Set max
pool.SetMax("Rage", count: 4);
pool.SetMax("KiPoint", count: 6);

// Check and consume
bool hasKi = pool.Has("KiPoint", amount: 2);
pool.Consume("KiPoint", amount: 2);
```

### 3. Automatic Replenishment
```csharp
// Each resource has a ReplenishType from BG3 data:
// - Turn: ActionPoint, BonusActionPoint, Reaction
// - ShortRest: Rage, KiPoint, ChannelDivinity, WildShape
// - Rest: SpellSlot, SorceryPoint, LayOnHandsCharge

// Replenish all Turn resources
pool.ReplenishResources(ReplenishType.Turn);

// Or use ResourceManager helpers
resourceManager.ReplenishTurnResources(combatant);
resourceManager.ReplenishShortRest(combatant);
resourceManager.ReplenishLongRest(combatant);
```

### 4. Integration with SpellUseCost
```csharp
var cost = new SpellUseCost
{
    ActionPoint = 1,
    SpellSlotLevel = 2,
    SpellSlotCount = 1,
    CustomResources = new() { { "KiPoint", 1 } }
};

// Validate and consume in one call
var (canPay, reason) = resourceManager.CanPayCost(combatant, cost);
if (canPay)
{
    resourceManager.ConsumeCost(combatant, cost, out string error);
}
```

## Resource Type Summary

### Action Economy (ReplenishType.Turn)
| Resource | Description | Max Value |
|----------|-------------|-----------|
| ActionPoint | Main action | 1 per turn |
| BonusActionPoint | Bonus action | 1 per turn |
| ReactionActionPoint | Reaction | 1 per turn |

### Spell Resources (ReplenishType.Rest)
| Resource | Description | Levels |
|----------|-------------|--------|
| SpellSlot | Standard slots | 1-9 |
| WarlockSpellSlot | Pact magic | 1-5 |
| SorceryPoint | Metamagic | 0 (simple) |

### Class Resources
| Class | Resource | Replenish | Calculation |
|-------|----------|-----------|-------------|
| Barbarian | Rage | ShortRest | 2-6 based on level |
| Monk | KiPoint | ShortRest | = Monk level |
| Cleric | ChannelDivinity | ShortRest | 1-3 based on level |
| Paladin | ChannelOath | ShortRest | 1 charge |
| Paladin | LayOnHandsCharge | Rest | Level × 5 |
| Bard | BardicInspiration | ShortRest | = Charisma mod |
| Druid | WildShape | ShortRest | 2 charges |
| Fighter | ExtraActionPoint | ShortRest | 1-2 based on level |
| Sorcerer | SorceryPoint | Rest | = Sorcerer level |

## Testing

Run examples:
```csharp
ResourceSystemExample.RunAllExamples();
```

Individual examples:
```csharp
ResourceSystemExample.Example_InitializeWizard();
ResourceSystemExample.Example_CastSpell();
ResourceSystemExample.Example_InitializeMonk();
ResourceSystemExample.Example_RestAndReplenish();
ResourceSystemExample.Example_InitializeWarlock();
ResourceSystemExample.Example_DisplayResourceStatus();
ResourceSystemExample.Example_MultiClassSpellcasting();
```

## Backward Compatibility

The system maintains full backward compatibility:

### Old System (Still Works)
```csharp
// CombatantResourcePool - simple key-value tracking
combatant.ResourcePool.SetMax("ki", 6);
combatant.ResourcePool.Consume(new() { { "ki", 2 } }, out _);
```

### New System (Recommended)
```csharp
// ResourcePool with full BG3 definitions
combatant.ActionResources.SetMax("KiPoint", 6);
combatant.ActionResources.Consume("KiPoint", 2);
```

Both coexist, allowing gradual migration.

## Integration Points

### 1. Combat Context Service Registration
```csharp
var resourceManager = new ResourceManager();
CombatContext.Instance.RegisterService(resourceManager);
```

### 2. Turn Queue Integration
```csharp
public void OnTurnStart(Combatant combatant)
{
    var rm = CombatContext.Instance.GetService<ResourceManager>();
    rm.ReplenishTurnResources(combatant);
    combatant.ActionBudget.ResetForTurn();
}
```

### 3. Action Execution
```csharp
public bool ExecuteSpell(Combatant caster, SpellUseCost cost)
{
    var rm = CombatContext.Instance.GetService<ResourceManager>();
    
    // Unified validation (action economy + resources)
    var (canPay, reason) = caster.ActionBudget.CanPaySpellCost(
        cost, rm, caster
    );
    
    if (!canPay)
        return false;
    
    // Unified consumption
    caster.ActionBudget.ConsumeSpellCost(cost, rm, caster, out _);
    return true;
}
```

## Build Status

✅ **Build: SUCCESS**
- 0 Errors
- 23 Warnings (pre-existing, unrelated to resource system)

All files compile successfully:
- ✅ ResourcePool.cs
- ✅ ResourceManager.cs
- ✅ ActionBudget.cs (enhanced)
- ✅ Combatant.cs (enhanced)
- ✅ ResourceSystemExample.cs

## Next Steps

### Recommended Integration Tasks

1. **Register ResourceManager in Combat Initialization**
   - Add to CombatContext service initialization
   - Initialize resources for all combatants at encounter start

2. **Update Turn Logic**
   - Call `ReplenishTurnResources()` on turn start
   - Integrate with existing `ActionBudget.ResetForTurn()`

3. **Update Action Execution**
   - Replace manual resource checks with `CanPaySpellCost()`
   - Use `ConsumeSpellCost()` for unified resource consumption

4. **Add UI Display**
   - Use `GetResourceStatus()` for resource panel
   - Show spell slots, class resources, etc.
   - Wire up `OnResourcesChanged` event for real-time updates

5. **Add Rest System**
   - Implement short rest mechanics
   - Implement long rest mechanics
   - Call appropriate replenish methods

### Future Enhancements

- [ ] Automatic spell slot calculation for multiclass characters
- [ ] Subclass-specific resources (Battle Master maneuvers, etc.)
- [ ] Resource-based action filtering in action bar
- [ ] Resource cost tooltips
- [ ] Resource restoration items/spells
- [ ] Custom resource types for homebrew
- [ ] Resource event logging
- [ ] Save/load resource state

## Documentation

Complete documentation available in:
- **System Overview**: [README_RESOURCE_SYSTEM.md](Data/ActionResources/README_RESOURCE_SYSTEM.md)
- **Usage Examples**: [ResourceSystemExample.cs](Data/ActionResources/ResourceSystemExample.cs)
- **API Reference**: See XML comments in source files

## Architecture Diagram

```
┌─────────────────────────────────────┐
│         CombatContext               │
│  (Service Locator)                  │
│                                      │
│  Services:                           │
│  └─ ResourceManager ◄────────┐      │
└──────────────────┬────────────┘      │
                   │                   │
                   ▼                   │
         ┌──────────────────┐          │
         │ ResourceManager  │          │
         ├──────────────────┤          │
         │ - Initialize     │          │
         │ - CanPayCost     │          │
         │ - ConsumeCost    │          │
         │ - Replenish      │          │
         └────────┬─────────┘          │
                  │                    │
                  ▼                    │
      ┌────────────────────┐           │
      │    Combatant       │           │
      ├────────────────────┤           │
      │ - ActionResources ─┼─┐         │
      │ - ActionBudget ◄───┼─┼─────────┘
      │ - ResolvedCharacter│ │
      └────────────────────┘ │
                             │
                ┌────────────▼──────────┐
                │    ResourcePool       │
                ├───────────────────────┤
                │ Resources:            │
                │ - ActionPoint         │
                │ - SpellSlot (L1-9)    │
                │ - Rage                │
                │ - KiPoint             │
                │ - etc.                │
                └───────────────────────┘
```

## Summary

Successfully implemented a complete BG3-style action resource management system with:
- ✅ Full resource definition loading from BG3 data
- ✅ Leveled resources (spell slots)
- ✅ Simple resources (rage, ki, etc.)
- ✅ Automatic replenishment by type
- ✅ ResourceManager service
- ✅ ActionBudget integration
- ✅ Comprehensive examples
- ✅ Complete documentation
- ✅ Backward compatibility
- ✅ Zero compilation errors

The system is ready for integration into the combat workflow.
