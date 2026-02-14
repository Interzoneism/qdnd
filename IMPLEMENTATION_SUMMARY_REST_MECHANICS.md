# Rest Mechanics Implementation Summary

## Overview
Implemented BG3-style rest mechanics for resource replenishment, allowing combatants to recover resources through short rests, long rests, and per-turn replenishment.

## Files Created

### 1. `Combat/Services/RestService.cs`
- **Purpose**: Central service for handling all rest-related resource replenishment
- **Key Methods**:
  - `ProcessRest(Combatant, RestType)` - Process a rest for a single combatant
  - `ReplenishTurnResources(Combatant)` - Replenish per-turn resources (Action, Bonus, Reaction)
  - `ReplenishRoundResources(Combatant)` - Placeholder for round-based resources
  - `ShortRest(IEnumerable<Combatant>)` - Short rest for multiple combatants
  - `LongRest(IEnumerable<Combatant>)` - Long rest for multiple combatants

### 2. `Tests/Unit/RestServiceTests.cs`
- **Purpose**: Comprehensive unit tests for RestService
- **Test Coverage**: 9 tests covering all aspects of rest mechanics
- **All Tests Passing**: ✅

## Integration Points

### 1. CombatContext Registration
- `ResourceManager` registered as a service in CombatContext
- `RestService` registered as a service in CombatContext (depends on ResourceManager)
- Services available via `CombatContext.GetService<T>()`

### 2. Turn Start Resource Replenishment
**Already Implemented** in `CombatArena.cs` (line ~1564):
```csharp
// Replenish BG3 turn-based resources (ActionPoint, BonusActionPoint, ReactionActionPoint, etc.)
combatant.ActionResources.ReplenishTurn();
```

The existing code already handles:
- Per-turn resource replenishment (ActionPoint, BonusActionPoint, ReactionActionPoint)
- ActionBudget reset (`combatant.ActionBudget.ResetForTurn()`)
- Movement replenishment with boost modifiers (Haste, Longstrider, etc.)
- Movement blocking (Entangled, Restrained)

## Rest Mechanics Rules

### Turn Start (Already Working)
- **Resources**: ActionPoint, BonusActionPoint, ReactionActionPoint, Movement
- **Behavior**: Automatically replenished at the start of each combatant's turn
- **Movement**: Respects boost modifiers (multipliers, flat bonuses, blocking)

### Short Rest
- **Resources**: Resources with `ReplenishType.ShortRest`
  - Warlock pact magic spell slots
  - Ki Points (Monk)
  - Fighter Superiority Dice
  - Bard Inspiration
  - Channel Divinity uses (regains on short rest)
- **Health**: No HP healing (can be extended with Hit Dice in the future)

### Long Rest
- **Resources**: All resources (both `ShortRest` and `Rest`/`FullRest` types)
  - Regular spell slots (Wizard, Sorcerer, etc.)
  - Barbarian Rage
  - Paladin Lay on Hands
  - All class features
  - Warlock pact slots (also restored)
- **Health**: Full HP restoration
- **Note**: Long rest includes short rest benefits

### Never Replenish
- Resources with `ReplenishType.Never` are not automatically replenished
- Examples: Warlock Invocations (always-on), special quest items

## Usage Examples

### Process a Short Rest for the Party
```csharp
var restService = combatContext.GetService<RestService>();
var allCombatants = combatContext.GetAllCombatants();
var playerParty = allCombatants.Where(c => c.Faction == Faction.Player);

restService.ShortRest(playerParty);
```

### Process a Long Rest for a Single Combatant
```csharp
var restService = combatContext.GetService<RestService>();
var wizard = combatContext.GetCombatant("wizard_id");

restService.ProcessRest(wizard, RestType.Long);
// Wizard now has: full HP, all spell slots, all resources restored
```

### Per-Turn Replenishment (Handled Automatically)
```csharp
// Already integrated in CombatArena.cs OnTurnStart
combatant.ActionResources.ReplenishTurn();
combatant.ActionBudget.ResetForTurn();
```

## Design Choices

### Why RestService Instead of Extending ResourceManager?
- **Separation of Concerns**: ResourceManager handles resource initialization and validation
- **RestService** focuses specifically on rest mechanics and replenishment
- **Future Extensions**: Makes it easy to add Hit Dice, rest restrictions, camp UI without bloating ResourceManager

### Why Use Existing RestType Enum?
- `RestType` enum already existed in `QDND.Data.CharacterModel.RestType`
- Reused to avoid duplication and maintain consistency

### Per-Turn Replenishment Already Implemented
- The game already had robust per-turn replenishment in CombatArena
- RestService provides a clean API that wraps existing functionality
- Future refactoring can consolidate all replenishment logic into RestService

## Test Results

All 9 unit tests passing:
- ✅ ShortRest_ReplenishesShortRestResources
- ✅ ShortRest_DoesNotReplenishLongRestResources
- ✅ LongRest_ReplenishesAllResources
- ✅ LongRest_FullyHealsHP
- ✅ ReplenishTurnResources_ReplenishesActionEconomy
- ✅ ReplenishTurnResources_DoesNotReplenishLongRestResources
- ✅ ReplenishRoundResources_Exists
- ✅ ShortRestMultipleCombatants_ReplenishesAll
- ✅ LongRestMultipleCombatants_ReplenishesAll

CI Build: ✅ Passed (0 errors)

## Future Enhancements

1. **Hit Dice System**: Add Hit Dice healing during short rests
2. **Rest UI**: Create camp/rest UI that calls RestService
3. **Rest Restrictions**: Add exhaustion, interrupted rest, partial rest
4. **Resource History**: Track resource usage for analytics
5. **Custom Rest Types**: Support modded/custom rest mechanics
