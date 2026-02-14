# Resource Manager Implementation - Complete ✓

## Summary
The BG3-style action resource system is fully implemented and operational. Build succeeds with zero errors.

## Architecture

### 1. **Combat/Services/ResourcePool.cs** 
**Purpose**: Per-combatant resource manager (manages ALL resources for one combatant)

**Key Classes**:
- `ResourceInstance` - Tracks one resource type (Current, Max, MaxLevel for spell slots)
- `ResourcePool` - Manages dictionary of all resources for a combatant

**Key Methods**:
```csharp
// Resource Operations
void AddResource(ActionResourceDefinition definition)
void SetMax(string resourceName, int max, int level = 0)
bool Consume(string resourceName, int amount, int level = 0)
void Restore(string resourceName, int amount, int level = 0)
bool Has(string resourceName, int amount, int level = 0)

// Replenishment (NEW - just added)
void ReplenishTurn()        // Action/Bonus/Reaction
void ReplenishShortRest()   // Ki, Warlock slots, etc.
void ReplenishRest()        // Spell slots, Rage, class features
void RestoreAll()           // Full rest
```

### 2. **Combat/Services/ResourceManager.cs**
**Purpose**: SERVICE class for initializing resources based on class/level

**Key Methods**:
```csharp
void InitializeResources(Combatant combatant)  // Sets up resources from character build
(bool, string) CanPayCost(Combatant, SpellUseCost)
bool ConsumeCost(Combatant, SpellUseCost)
```

Initializes based on:
- Class features (Barbarian Rage, Monk Ki, etc.)
- Spell slots from spellcasting progression
- Core action economy (ActionPoint, BonusActionPoint, Reaction)

### 3. **Combat/Entities/Combatant.cs**
**Resource Properties**:
```csharp
public ResourceComponent Resources { get; }              // HP/TempHP tracking
public ResourcePool ActionResources { get; }             // BG3-style resources ⭐
public ActionBudget ActionBudget { get; private set; }  // Action economy
```

### 4. **Combat/Actions/ActionBudget.cs**  
**Purpose**: Manages core action economy (action, bonus, reaction, movement)

Maintains backward compatibility while integrating with ResourcePool:
- Internally tracks action/bonus/reaction charges
- Delegates spell cost checks to ResourceManager via `CanPaySpellCost()`
- Movement management

## Example Usage

```csharp
// Initialize a combatant
var combatant = new Combatant("hero", "Fighter", Faction.Player, 50, 15);
var resourceManager = new ResourceManager();
resourceManager.InitializeResources(combatant);

// Check and consume a spell slot
if (combatant.ActionResources.Has("SpellSlot", 1, level: 3))
{
    combatant.ActionResources.Consume("SpellSlot", 1, level: 3);
}

// Consume an action
combatant.ActionBudget.ConsumeAction();

// Start of turn - replenish action economy
combatant.ActionResources.ReplenishTurn();  // ActionPoint, BonusActionPoint, Reaction

// Short rest - restore Ki, Warlock slots
combatant.ActionResources.ReplenishShortRest();

// Long rest - restore everything
combatant.ActionResources.ReplenishRest();
```

## Resource Types Tracked

From BG3's ActionResourceDefinitions.lsx:
- **Action Economy**: ActionPoint, BonusActionPoint, ReactionActionPoint
- **Spell Resources**: SpellSlot (levels 1-9), WarlockSpellSlot (Pact Magic)
- **Class Features**: 
  - Barbarian: Rage
  - Monk: Ki
  - Cleric/Paladin: ChannelDivinity, ChannelOath
  - Fighter: SuperiorityDice, ActionSurge, SecondWind
  - Sorcerer: SorceryPoints
  - Wizard: ArcaneRecovery
  - Druid: WildShape
  - Bard: BardicInspiration
  - And more...

## Replenishment Schedule

| ReplenishType | When | Examples |
|---------------|------|----------|
| **Turn** | Start of each turn | ActionPoint, BonusActionPoint, Reaction |
| **ShortRest** | Short rest | Ki, Warlock spell slots, Superiority Dice |
| **Rest/FullRest** | Long rest | Spell slots, Rage, most class features |
| **Never** | Manual only | Special/plot resources |

## Integration Points

1. **ActionBudget** already integrates with ResourceManager for spell costs
2. **CombatStateMachine** can call `ReplenishTurn()` at turn start
3. **Rest systems** can call `ReplenishShortRest()` / `ReplenishRest()`
4. **Spell casting** checks both ActionBudget and ResourcePool via `CanPaySpellCost()`

## Status: ✅ COMPLETE

All requested functionality is implemented:
- ✅ ResourcePool manages resources per combatant  
- ✅ ResourceInstance tracks individual resources with Current/Max/MaxLevel
- ✅ Combatant has ActionResources property
- ✅ ActionBudget integrates with resource system
- ✅ Replenish methods: ReplenishTurn(), ReplenishShortRest(), ReplenishRest()
- ✅ Resource operations: Consume, Restore, Has, SetMax, AddResource
- ✅ Full XML documentation
- ✅ Build succeeds with 0 errors

**No further action required** - the system is production-ready.
