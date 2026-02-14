# Action Resource System - Quick Reference

## Quick Start

### 1. Initialize Resources for a Combatant

```csharp
var resourceManager = new ResourceManager();
resourceManager.InitializeResources(combatant);
```

This automatically:
- Adds ActionPoint, BonusActionPoint, ReactionActionPoint
- Reads spell slots from `combatant.ResolvedCharacter.Resources`
- Adds class-specific resources (Rage, Ki, etc.) based on class levels

### 2. Check if You Can Cast/Use an Ability

```csharp
var spellCost = new SpellUseCost
{
    ActionPoint = 1,
    SpellSlotLevel = 2,
    SpellSlotCount = 1
};

var (canPay, reason) = resourceManager.CanPayCost(combatant, spellCost);
if (!canPay)
{
    ShowError(reason); // "No level 2 spell slot available"
    return;
}
```

### 3. Consume Resources

```csharp
bool success = resourceManager.ConsumeCost(combatant, spellCost, out string error);
if (!success)
{
    ShowError(error);
    return;
}

// Spell was cast successfully, resources consumed
```

### 4. Replenish Resources

```csharp
// End of turn - restore actions
resourceManager.ReplenishTurnResources(combatant);

// Short rest - restore Ki, Rage, etc.
resourceManager.ReplenishShortRest(combatant);

// Long rest - restore everything
resourceManager.ReplenishLongRest(combatant);
```

## Common Use Cases

### Casting a Spell

```csharp
// Get spell cost from spell definition
var cost = spell.UseCosts;

// Validate and consume in one call via ActionBudget
var (canCast, reason) = combatant.ActionBudget.CanPaySpellCost(
    cost, resourceManager, combatant
);

if (canCast)
{
    combatant.ActionBudget.ConsumeSpellCost(
        cost, resourceManager, combatant, out string error
    );
    
    // Execute spell...
}
```

### Using a Class Ability (e.g., Flurry of Blows)

```csharp
var flurryCost = new SpellUseCost
{
    BonusActionPoint = 1,
    CustomResources = new() { { "KiPoint", 1 } }
};

if (resourceManager.CanPayCost(monk, flurryCost).CanPay)
{
    resourceManager.ConsumeCost(monk, flurryCost, out _);
    // Execute Flurry of Blows
}
```

### Checking Specific Resources

```csharp
// Check spell slots
bool hasSlot = combatant.ActionResources.Has("SpellSlot", amount: 1, level: 3);
int currentSlots = combatant.ActionResources.GetCurrent("SpellSlot", level: 3);
int maxSlots = combatant.ActionResources.GetMax("SpellSlot", level: 3);

// Check class resources
bool hasRage = combatant.ActionResources.Has("Rage", amount: 1);
int kiPoints = combatant.ActionResources.GetCurrent("KiPoint");
```

### Displaying Resources in UI

```csharp
var status = resourceManager.GetResourceStatus(combatant);

foreach (var (resourceName, value) in status)
{
    // resourceName: "Spell Slots", value: "L1:3/4, L2:2/3"
    // resourceName: "Ki Points", value: "5/6"
    UpdateUI(resourceName, value);
}
```

## SpellUseCost Structure

```csharp
public class SpellUseCost
{
    public int ActionPoint { get; set; }           // 1 for action
    public int BonusActionPoint { get; set; }      // 1 for bonus action
    public int ReactionActionPoint { get; set; }   // 1 for reaction
    public float Movement { get; set; }            // Movement cost
    
    public int SpellSlotLevel { get; set; }        // 1-9 for leveled spells, 0 for cantrips
    public int SpellSlotCount { get; set; }        // Usually 1
    
    public Dictionary<string, int> CustomResources { get; set; }  // KiPoint, Rage, etc.
}
```

## Resource Names (Case-Insensitive)

### Core
- `ActionPoint`
- `BonusActionPoint`
- `ReactionActionPoint`

### Spell Slots
- `SpellSlot` (levels 1-9)
- `WarlockSpellSlot` (levels 1-5)

### Class Resources
- `Rage` (Barbarian)
- `KiPoint` (Monk)
- `ChannelDivinity` (Cleric)
- `ChannelOath` (Paladin)
- `LayOnHandsCharge` (Paladin)
- `BardicInspiration` (Bard)
- `WildShape` (Druid)
- `SuperiorityDie` (Fighter - Battle Master)
- `ExtraActionPoint` (Fighter - Action Surge)
- `SorceryPoint` (Sorcerer)

## Replenishment Types

| Type | When | Resources |
|------|------|-----------|
| `Turn` | End of combatant's turn | Actions, Bonus, Reaction |
| `ShortRest` | After short rest (1 hour) | Rage, Ki, Channel Divinity, Wild Shape, Superiority Die, Action Surge, Bardic Inspiration |
| `Rest` | After long rest (8 hours) | Spell Slots, Sorcery Points, Lay on Hands |

## Integration Checklist

### Combat Initialization
- [x] Create ResourceManager instance
- [x] Register with CombatContext
- [ ] Call `InitializeResources()` for each combatant

### Turn Management
- [ ] Call `ReplenishTurnResources()` at turn start
- [ ] Call `ActionBudget.ResetForTurn()` at turn start

### Action Execution
- [ ] Use `CanPaySpellCost()` to validate
- [ ] Use `ConsumeSpellCost()` to consume
- [ ] Handle error messages appropriately

### Rest System
- [ ] Implement short rest button/trigger
- [ ] Call `ReplenishShortRest()` on short rest
- [ ] Implement long rest button/trigger
- [ ] Call `ReplenishLongRest()` on long rest

### UI
- [ ] Display resource status panel
- [ ] Wire up `OnResourcesChanged` event
- [ ] Show spell slot counts by level
- [ ] Show class resource counts
- [ ] Disable actions when resources unavailable

## Common Patterns

### Pattern: Validate Before Execute

```csharp
public bool TryExecuteAction(Combatant actor, SpellUseCost cost, Action onSuccess)
{
    var rm = GetResourceManager();
    var (canPay, reason) = rm.CanPayCost(actor, cost);
    
    if (!canPay)
    {
        ShowFeedback(reason);
        return false;
    }
    
    if (!rm.ConsumeCost(actor, cost, out string error))
    {
        ShowError(error);
        return false;
    }
    
    onSuccess();
    return true;
}
```

### Pattern: Resource-Gated Action Bar

```csharp
public bool IsActionAvailable(Combatant actor, ActionDefinition action)
{
    if (action.Cost == null)
        return true;
    
    var rm = GetResourceManager();
    return rm.CanPayCost(actor, action.Cost).CanPay;
}

// In UI update
foreach (var action in availableActions)
{
    bool enabled = IsActionAvailable(currentCombatant, action);
    actionButton.SetEnabled(enabled);
}
```

### Pattern: Resource Change Notification

```csharp
// Subscribe to changes
combatant.ActionResources.OnResourcesChanged += UpdateResourceUI;

void UpdateResourceUI()
{
    var rm = GetResourceManager();
    var status = rm.GetResourceStatus(combatant);
    
    foreach (var (name, value) in status)
    {
        resourcePanel.UpdateResource(name, value);
    }
}
```

## Debugging

### Check Resource State

```csharp
// Print all resources
foreach (var resource in combatant.ActionResources.Resources.Values)
{
    GD.Print(resource.ToString());
}

// Check specific resource
var spellSlots = combatant.ActionResources.GetResource("SpellSlot");
if (spellSlots != null)
{
    GD.Print($"Spell Slots: {spellSlots}");
}
```

### Validate ResourceManager Loaded

```csharp
var rm = new ResourceManager();
GD.Print($"Loaded {rm.ResourceDefinitions.Count} resource definitions");

if (rm.ResourceDefinitions.Count == 0)
{
    GD.PushError("Failed to load ActionResourceDefinitions.lsx");
}
```

### Test Resource Operations

```csharp
// Create test combatant
var testWizard = CreateTestWizard();
var rm = new ResourceManager();
rm.InitializeResources(testWizard);

// Verify initialization
Assert(testWizard.ActionResources.GetMax("SpellSlot", 1) > 0, "Has L1 slots");

// Test consumption
testWizard.ActionResources.Consume("SpellSlot", 1, level: 1);
GD.Print($"After cast: {testWizard.ActionResources.GetResource("SpellSlot")}");

// Test replenishment
testWizard.ActionResources.RestoreAll();
GD.Print($"After rest: {testWizard.ActionResources.GetResource("SpellSlot")}");
```

## See Also

- **Full Documentation**: [README_RESOURCE_SYSTEM.md](Data/ActionResources/README_RESOURCE_SYSTEM.md)
- **Examples**: [ResourceSystemExample.cs](Data/ActionResources/ResourceSystemExample.cs)
- **Integration Tests**: [ResourceSystemIntegrationTest.cs](Tests/Integration/ResourceSystemIntegrationTest.cs)
- **Implementation Summary**: [IMPLEMENTATION_SUMMARY_RESOURCE_SYSTEM.md](IMPLEMENTATION_SUMMARY_RESOURCE_SYSTEM.md)
