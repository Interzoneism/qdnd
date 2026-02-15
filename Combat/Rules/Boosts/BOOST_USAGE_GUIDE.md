# Boost Application and Evaluation System

## Overview

The boost system provides runtime stat modifiers that affect combat calculations. Boosts are applied from various sources (statuses, passives, equipment, spells) and automatically removed when their sources expire.

## Architecture

### Core Components

1. **BoostDefinition** - Parsed representation of a boost
   - Contains type, parameters, and optional condition
   - Created by BoostParser from DSL strings

2. **ActiveBoost** - A boost currently active on a combatant
   - Wraps BoostDefinition with source tracking
   - Used for removal when source expires

3. **BoostContainer** - Manages active boosts on a combatant
   - Add/remove/query operations
   - Attached to each Combatant via `Boosts` property

4. **BoostApplicator** - Static utility for applying/removing boosts
   - Parses boost strings and creates ActiveBoosts
   - Handles source tracking

5. **BoostEvaluator** - Static utility for querying boost effects
   - Answers questions like "Has advantage?" or "What's the AC bonus?"
   - Used by combat systems to calculate final values

## Usage Flow

### 1. Applying Boosts

```csharp
// From a status effect
BoostApplicator.ApplyBoosts(combatant, "Advantage(AttackRoll)", "Status", "BLESSED");

// From a passive ability
BoostApplicator.ApplyBoosts(combatant, "AC(2);DamageBonus(2, Slashing)", "Passive", "RAGE");

// From equipment
BoostApplicator.ApplyBoosts(combatant, "AC(8)", "Equipment", "PLATE_ARMOR");
```

### 2. Querying Boost Effects

```csharp
// Check for advantage/disadvantage
bool hasAdvantage = BoostEvaluator.HasAdvantage(combatant, RollType.AttackRoll);
bool hasDisadvantage = BoostEvaluator.HasDisadvantage(combatant, RollType.SavingThrow, AbilityType.Dexterity);

// Get AC bonus
int acBonus = BoostEvaluator.GetACBonus(combatant);
int effectiveAC = combatant.Stats.BaseAC + acBonus;

// Get damage bonus
int damageBonus = BoostEvaluator.GetDamageBonus(combatant, DamageType.Fire);

// Check damage resistance
ResistanceLevel resistance = BoostEvaluator.GetResistanceLevel(combatant, DamageType.Slashing);
// Returns: Vulnerable, Normal, Resistant, or Immune

// Check status immunities
HashSet<string> immunities = BoostEvaluator.GetStatusImmunities(combatant);
bool immuneToPoison = immunities.Contains("POISONED");
```

### 3. Removing Boosts

```csharp
// Remove boosts when a specific source expires
BoostApplicator.RemoveBoosts(combatant, "Status", "BLESSED");

// Remove all boosts (e.g., end of combat, long rest)
BoostApplicator.RemoveAllBoosts(combatant);

// Get boosts from a specific source before removing
var rageBoosts = BoostApplicator.GetActiveBoosts(combatant, "Passive", "RAGE");
```

### 4. Direct Container Access

```csharp
// Access the boost container directly
BoostContainer boosts = combatant.Boosts;

// Check boost count
int count = boosts.Count;

// Get all boosts of a type
List<ActiveBoost> acBoosts = boosts.GetBoosts(BoostType.AC);

// Get summary for UI/debugging
string summary = boosts.GetSummary(); // "AC(1), Advantage(2), Resistance(3)"
```

## Common Patterns

### Status Effects

```csharp
public class BlessedStatus : StatusEffect
{
    public override void OnApply(Combatant target)
    {
        // Apply advantage on attack rolls
        BoostApplicator.ApplyBoosts(target, "Advantage(AttackRoll)", "Status", "BLESSED");
    }

    public override void OnExpire(Combatant target)
    {
        // Remove all boosts from this status
        BoostApplicator.RemoveBoosts(target, "Status", "BLESSED");
    }
}
```

### Passive Abilities

```csharp
public class RageAbility : PassiveAbility
{
    public override void Activate(Combatant combatant)
    {
        string boosts = "DamageBonus(2, Slashing);DamageBonus(2, Piercing);DamageBonus(2, Bludgeoning);" +
                        "Resistance(Slashing, Resistant);Resistance(Piercing, Resistant);Resistance(Bludgeoning, Resistant)";
        
        BoostApplicator.ApplyBoosts(combatant, boosts, "Passive", "RAGE");
    }

    public override void Deactivate(Combatant combatant)
    {
        BoostApplicator.RemoveBoosts(combatant, "Passive", "RAGE");
    }
}
```

### Equipment

```csharp
public void EquipArmor(Combatant combatant, ArmorDefinition armor)
{
    // Remove old armor boosts
    BoostApplicator.RemoveBoosts(combatant, "Equipment", "ARMOR");
    
    // Apply new armor boosts
    if (!string.IsNullOrEmpty(armor.Boosts))
    {
        BoostApplicator.ApplyBoosts(combatant, armor.Boosts, "Equipment", "ARMOR");
    }
}
```

### Combat Calculations

```csharp
public int CalculateAC(Combatant combatant)
{
    int baseAC = combatant.Stats.BaseAC;
    int boostAC = BoostEvaluator.GetACBonus(combatant);
    return baseAC + boostAC;
}

public int RollAttack(Combatant attacker, Combatant target)
{
    // Check for advantage/disadvantage
    bool hasAdvantage = BoostEvaluator.HasAdvantage(attacker, RollType.AttackRoll, target: target);
    bool hasDisadvantage = BoostEvaluator.HasDisadvantage(attacker, RollType.AttackRoll, target: target);
    
    // Roll d20 with advantage/disadvantage
    int roll = RollD20(hasAdvantage, hasDisadvantage);
    
    // Add modifiers
    int attackBonus = attacker.Stats.GetAttackBonus();
    return roll + attackBonus;
}

public int CalculateDamage(Combatant attacker, DamageType damageType, int baseDamage)
{
    // Add damage bonus from boosts
    int damageBonus = BoostEvaluator.GetDamageBonus(attacker, damageType);
    return baseDamage + damageBonus;
}

public int ApplyDamageResistance(Combatant defender, DamageType damageType, int damage)
{
    ResistanceLevel resistance = BoostEvaluator.GetResistanceLevel(defender, damageType);
    
    return resistance switch
    {
        ResistanceLevel.Immune => 0,
        ResistanceLevel.Resistant => damage / 2,
        ResistanceLevel.Vulnerable => damage * 2,
        _ => damage
    };
}
```

## Boost Types Supported

### Core Combat Boosts

- **AC(value)** - Armor class modifier
  - Example: `AC(2)` adds +2 AC
  
- **Advantage(RollType)** - Grant advantage on rolls
  - Examples: `Advantage(AttackRoll)`, `Advantage(SavingThrow, Dexterity)`
  
- **Disadvantage(RollType)** - Impose disadvantage on rolls
  - Examples: `Disadvantage(AttackRoll)`, `Disadvantage(AllAbilityChecks)`
  
- **Resistance(DamageType, ResistanceLevel)** - Damage resistance modifier
  - Examples: `Resistance(Fire, Resistant)`, `Resistance(Poison, Immune)`
  
- **DamageBonus(value, DamageType)** - Extra damage on attacks
  - Example: `DamageBonus(5, Fire)` adds +5 fire damage
  
- **StatusImmunity(StatusID)** - Immunity to status effects
  - Example: `StatusImmunity(BURNING)` prevents burning status

### Multiple Boosts

Combine multiple boosts with semicolons:

```csharp
string boosts = "AC(2);Advantage(AttackRoll);Resistance(Fire, Resistant)";
BoostApplicator.ApplyBoosts(combatant, boosts, "Status", "HOLY_AURA");
```

## Conditional Boosts (Future)

Conditional boosts have an `IF()` clause that limits when they apply:

```csharp
// Only applies when within 3 meters of target
"IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll)"

// Only applies when raging
"IF(HasStatus(RAGING)):DamageBonus(2, Slashing)"
```

**Note:** Condition evaluation is not yet implemented. Conditional boosts are currently skipped by BoostEvaluator.

## Testing

Run the example to see the boost system in action:

```csharp
using QDND.Examples;

// Basic usage examples
BoostSystemExample.RunExample();

// Combat scenario with boosts
BoostSystemExample.RunCombatScenario();
```

## Performance Considerations

1. **Boost Queries are O(n)** - BoostEvaluator scans all active boosts
   - Typical combatants have 5-15 boosts
   - Query overhead is negligible for this range
   
2. **Avoid Per-Frame Queries** - Cache boost results when possible
   - AC typically doesn't change mid-action
   - Query once at action start, cache result
   
3. **Batch Operations** - Remove/apply multiple boosts together
   - Use semicolon-delimited boost strings
   - Single parse + apply is faster than multiple calls

## Best Practices

1. **Use Unique Source IDs** - Ensures clean removal
   ```csharp
   // Good: Unique ID per instance
   ApplyBoosts(combatant, "AC(2)", "Status", "BLESSED_1234");
   
   // Bad: Generic ID shared across instances
   ApplyBoosts(combatant, "AC(2)", "Status", "BLESSED");
   ```

2. **Clean Up on Expiry** - Always remove boosts when source expires
   ```csharp
   RemoveBoosts(combatant, "Status", statusInstance.Id);
   ```

3. **Document Boost Strings** - Use constants for complex boosts
   ```csharp
   public const string RAGE_BOOSTS = 
       "DamageBonus(2, Slashing);DamageBonus(2, Piercing);DamageBonus(2, Bludgeoning);" +
       "Resistance(Slashing, Resistant);Resistance(Piercing, Resistant);Resistance(Bludgeoning, Resistant)";
   ```

4. **Use BoostEvaluator in Combat Logic** - Don't access boosts directly
   ```csharp
   // Good: Use evaluator for combat calculations
   int ac = baseAC + BoostEvaluator.GetACBonus(combatant);
   
   // Bad: Manual boost iteration
   foreach (var boost in combatant.Boosts.AllBoosts) { ... }
   ```

## Future Enhancements

1. **Condition Evaluation** - Implement IF() condition checking
2. **Stacking Rules** - Define how same-type boosts combine
3. **Duration Tracking** - Automatic expiry after X rounds
4. **Boost Events** - Notify when boosts are applied/removed
5. **Proficiency Boosts** - Add proficiency bonus to specific rolls
6. **Ability Score Boosts** - Temporary ability score increases

## See Also

- [README.md](README.md) - Boost parser and architecture overview
- [BoostParser.cs](BoostParser.cs) - Boost string parsing
- [BoostDefinition.cs](BoostDefinition.cs) - Boost data structure
- [Examples/BoostSystemExample.cs](../../../Examples/BoostSystemExample.cs) - Complete usage examples
