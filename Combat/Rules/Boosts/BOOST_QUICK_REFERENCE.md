# Boost System Quick Reference

## 🎯 Quick Start

```csharp
using QDND.Combat.Rules.Boosts;

// Apply boosts
BoostApplicator.ApplyBoosts(combatant, "AC(2);Advantage(AttackRoll)", "Status", "BLESSED");

// Query boosts
int acBonus = BoostEvaluator.GetACBonus(combatant);
bool hasAdvantage = BoostEvaluator.HasAdvantage(combatant, RollType.AttackRoll);

// Remove boosts
BoostApplicator.RemoveBoosts(combatant, "Status", "BLESSED");
```

## 📦 Core Classes

| Class | Purpose | Usage |
|-------|---------|-------|
| **BoostContainer** | Stores active boosts on a combatant | `combatant.Boosts` |
| **BoostApplicator** | Apply/remove boosts | `BoostApplicator.ApplyBoosts(...)` |
| **BoostEvaluator** | Query boost effects | `BoostEvaluator.GetACBonus(...)` |
| **ActiveBoost** | Active boost instance | Returned by queries |
| **BoostDefinition** | Parsed boost data | Inside ActiveBoost |

## 🔧 Common Operations

### Apply Boosts
```csharp
// Single boost
BoostApplicator.ApplyBoosts(combatant, "AC(2)", "Equipment", "PLATE_ARMOR");

// Multiple boosts (semicolon-separated)
BoostApplicator.ApplyBoosts(combatant, "AC(2);Advantage(AttackRoll)", "Status", "BLESSED");

// Complex boost string
string rageBoosts = "DamageBonus(2, Slashing);Resistance(Slashing, Resistant)";
BoostApplicator.ApplyBoosts(combatant, rageBoosts, "Passive", "RAGE");
```

### Query Boost Effects
```csharp
// Advantage/Disadvantage
bool hasAdvantage = BoostEvaluator.HasAdvantage(combatant, RollType.AttackRoll);
bool hasDisadvantage = BoostEvaluator.HasDisadvantage(combatant, RollType.SavingThrow, AbilityType.Dexterity);

// AC Bonus
int acBonus = BoostEvaluator.GetACBonus(combatant);
int totalAC = combatant.Stats.BaseAC + acBonus;

// Damage Bonus
int damageBonus = BoostEvaluator.GetDamageBonus(combatant, DamageType.Fire);

// Resistance
ResistanceLevel resistance = BoostEvaluator.GetResistanceLevel(combatant, DamageType.Slashing);

// Status Immunity
HashSet<string> immunities = BoostEvaluator.GetStatusImmunities(combatant);
bool immuneToPoison = immunities.Contains("POISONED");
```

### Remove Boosts
```csharp
// Remove from specific source
BoostApplicator.RemoveBoosts(combatant, "Status", "BLESSED");

// Remove all boosts
BoostApplicator.RemoveAllBoosts(combatant);

// Query before removing
var rageBoosts = BoostApplicator.GetActiveBoosts(combatant, "Passive", "RAGE");
BoostApplicator.RemoveBoosts(combatant, "Passive", "RAGE");
```

### Direct Container Access
```csharp
// Get container
BoostContainer boosts = combatant.Boosts;

// Check count
if (boosts.Count > 0) { ... }

// Get boosts by type
List<ActiveBoost> acBoosts = boosts.GetBoosts(BoostType.AC);

// Check for specific boost
bool hasAC = boosts.HasBoost(BoostType.AC);

// Get summary
string summary = boosts.GetSummary(); // "AC(1), Advantage(2)"
```

## 🎲 Boost Types

| Boost | Syntax | Example |
|-------|--------|---------|
| **AC** | `AC(value)` | `AC(2)` → +2 AC |
| **Advantage** | `Advantage(RollType)` | `Advantage(AttackRoll)` |
| **Disadvantage** | `Disadvantage(RollType)` | `Disadvantage(SavingThrow)` |
| **Resistance** | `Resistance(DamageType, Level)` | `Resistance(Fire, Resistant)` |
| **DamageBonus** | `DamageBonus(value, Type)` | `DamageBonus(5, Fire)` |
| **StatusImmunity** | `StatusImmunity(StatusID)` | `StatusImmunity(BURNING)` |

## 📝 Source Tracking

Boosts are identified by **source type** and **source ID**:

```csharp
// Source: "Status", ID: "BLESSED"
BoostApplicator.ApplyBoosts(combatant, "...", "Status", "BLESSED");

// Source: "Passive", ID: "RAGE"
BoostApplicator.ApplyBoosts(combatant, "...", "Passive", "RAGE");

// Source: "Equipment", ID: "PLATE_ARMOR"
BoostApplicator.ApplyBoosts(combatant, "...", "Equipment", "PLATE_ARMOR");
```

Common source types:
- `Status` - From status effects
- `Passive` - From class features, racial traits
- `Equipment` - From armor, weapons, items
- `Spell` - From temporary spell effects
- `Feat` - From feats

**Important:** Use unique IDs per instance to prevent accidental removal!

## ⚡ Integration Examples

### Status Effect
```csharp
public override void OnApply(Combatant target)
{
    BoostApplicator.ApplyBoosts(target, "Advantage(AttackRoll)", "Status", this.Id);
}

public override void OnExpire(Combatant target)
{
    BoostApplicator.RemoveBoosts(target, "Status", this.Id);
}
```

### Combat Calculation
```csharp
public int CalculateFinalAC(Combatant combatant)
{
    int baseAC = combatant.Stats.BaseAC;
    int boostAC = BoostEvaluator.GetACBonus(combatant);
    return baseAC + boostAC;
}

public bool RollWithAdvantage(Combatant combatant)
{
    bool adv = BoostEvaluator.HasAdvantage(combatant, RollType.AttackRoll);
    bool dis = BoostEvaluator.HasDisadvantage(combatant, RollType.AttackRoll);
    
    if (adv && !dis) return true;  // Advantage
    if (dis && !adv) return false; // Disadvantage (roll normally with disadvantage mechanic)
    return false; // They cancel out
}
```

## 🧪 Testing

Run the example to test the system:

```csharp
using QDND.Examples;

BoostSystemExample.RunExample();           // Basic usage
BoostSystemExample.RunCombatScenario();    // Combat scenario
```

## 📚 Documentation

- **Full Guide**: [BOOST_USAGE_GUIDE.md](BOOST_USAGE_GUIDE.md)
- **Boost Parser Overview**: [README.md](README.md)
- **Example Code**: [Examples/BoostSystemExample.cs](../../../Examples/BoostSystemExample.cs)

## ⚠️ Important Notes

1. **Always remove boosts when source expires** - Memory leaks otherwise
2. **Use unique source IDs** - Prevents accidental removal of other instances
3. **Query through BoostEvaluator** - Don't iterate boosts manually
4. **Conditional boosts are not yet evaluated** - IF() syntax parsed but skipped
5. **Boosts don't auto-expire** - Manual removal required

## 🐛 Common Pitfalls

```csharp
// ❌ BAD: Generic ID shared across instances
ApplyBoosts(combatant, "AC(2)", "Status", "BLESSED");

// ✅ GOOD: Unique ID per instance
ApplyBoosts(combatant, "AC(2)", "Status", $"BLESSED_{instance.Id}");

// ❌ BAD: Forgetting to remove boosts
ApplyBoosts(combatant, "AC(2)", "Status", id);
// ... status expires but boosts remain!

// ✅ GOOD: Always clean up
ApplyBoosts(combatant, "AC(2)", "Status", id);
// ... later when status expires:
RemoveBoosts(combatant, "Status", id);

// ❌ BAD: Manual iteration
foreach (var boost in combatant.Boosts.AllBoosts) { ... }

// ✅ GOOD: Use evaluator methods
int bonus = BoostEvaluator.GetACBonus(combatant);
```
