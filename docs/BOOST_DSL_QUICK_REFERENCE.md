# Boost DSL Quick Reference

**TL;DR**: Parse BG3 boost strings like `"AC(2);Advantage(AttackRoll)"` into structured data.

---

## Quick Start

```csharp
using QDND.Combat.Rules.Boosts;

// Parse a boost string
var boosts = BoostParser.ParseBoostString("AC(2);Advantage(AttackRoll)");

// Access boost data
foreach (var boost in boosts)
{
    Console.WriteLine($"Type: {boost.Type}");
    Console.WriteLine($"Parameters: {string.Join(", ", boost.Parameters)}");
    if (boost.IsConditional)
        Console.WriteLine($"Condition: {boost.Condition}");
}
```

---

## Common Boost Patterns

### AC Modifier
```
AC(2)                  → +2 AC
AC(-1)                 → -1 AC
```

### Advantage/Disadvantage
```
Advantage(AttackRoll)                      → Advantage on attacks
Disadvantage(SavingThrow)                  → Disadvantage on all saves
Advantage(SavingThrow,Dexterity)           → Advantage on Dex saves
```

### Damage Resistance
```
Resistance(Fire,Resistant)     → Half fire damage
Resistance(Poison,Immune)      → Immune to poison damage
Resistance(Slashing,Vulnerable)→ Double slashing damage
```

### Damage Bonus
```
DamageBonus(5,Piercing)        → +5 piercing damage
WeaponDamage(1d4,Fire)         → +1d4 fire damage
```

### Status Immunity
```
StatusImmunity(BURNING)        → Cannot be burned
StatusImmunity(PARALYZED)      → Cannot be paralyzed
```

### Ability Score Modifier
```
Ability(Strength,2)            → +2 Strength
Ability(Intelligence,-1)       → -1 Intelligence
```

### Action Economy
```
ActionResourceBlock(Movement)              → Cannot move
ActionResourceMultiplier(Movement,2,0)     → Double movement (Dash)
ActionResource(Movement,30,0)              → +30ft movement
```

### Conditional Boosts
```
IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll)
IF(HasStatus(RAGING)):DamageBonus(2,Slashing)
IF(IsMeleeAttack()):Advantage(AttackRoll);DamageBonus(1d4,Fire)
```

---

## Real BG3 Examples

| Spell/Status | Boost String |
|--------------|--------------|
| Shield of Faith | `AC(2)` |
| Bless | `RollBonus(AttackRoll,1d4);RollBonus(SavingThrow,1d4)` |
| Barbarian Rage | `DamageBonus(2,Physical);Resistance(Bludgeoning,Resistant);Resistance(Piercing,Resistant);Resistance(Slashing,Resistant)` |
| Haste | `AC(2);ActionResourceMultiplier(Movement,2,0);ActionResource(ActionPoint,1,0)` |
| Poisoned | `Disadvantage(AttackRoll);Disadvantage(AbilityCheck)` |
| Paralyzed | `CriticalHit(AttackRoll,Success);ActionResourceBlock(Movement);Disadvantage(SavingThrow,Dexterity)` |
| Fire Resistance Ring | `Resistance(Fire,Resistant)` |

---

## All Boost Types (Tier 1)

| Boost Type | Syntax | Description |
|------------|--------|-------------|
| AC | `AC(value)` | Modify armor class |
| Advantage | `Advantage(RollType)` or `Advantage(RollType,Ability)` | Grant advantage |
| Disadvantage | `Disadvantage(RollType)` or `Disadvantage(RollType,Ability)` | Impose disadvantage |
| Resistance | `Resistance(DamageType,Level)` | Modify damage resistance |
| StatusImmunity | `StatusImmunity(StatusID)` | Grant status immunity |
| DamageBonus | `DamageBonus(value,DamageType)` | Add bonus damage |
| WeaponDamage | `WeaponDamage(dice,DamageType)` | Add dice damage |
| Ability | `Ability(AbilityName,modifier)` | Modify ability score |

---

## Parameter Access

### Type-Safe
```csharp
var boost = boosts[0];

// Get parameter with type conversion
int value = boost.GetIntParameter(0);          // Returns int or 0
float multiplier = boost.GetFloatParameter(1); // Returns float or 0.0f
string type = boost.GetStringParameter(2);     // Returns string or ""

// Generic access
T param = boost.GetParameter<T>(index);
```

### Default Values
```csharp
int amount = boost.GetIntParameter(0, defaultValue: 10);
string damageType = boost.GetStringParameter(1, defaultValue: "Physical");
```

---

## Error Handling

```csharp
try
{
    var boosts = BoostParser.ParseBoostString("InvalidBoost(2)");
}
catch (BoostParseException ex)
{
    // Error messages:
    // - "Unknown boost type: InvalidBoost"
    // - "Boost missing parameters: AC"
    // - "Boost missing closing parenthesis: AC(2"
    // - "IF() condition missing closing colon: IF(condition)Boost()"
    Console.WriteLine(ex.Message);
}
```

---

## Running Examples

```csharp
using QDND.Tests.Combat.Rules.Boosts;

// Run all test cases
BoostParserExamples.RunAllExamples();

// Show real BG3 examples
BoostParserExamples.ShowBG3RealExamples();
```

---

## Files

| File | Purpose |
|------|---------|
| [Combat/Rules/Boosts/BoostParser.cs](Combat/Rules/Boosts/BoostParser.cs) | Main parser |
| [Combat/Rules/Boosts/BoostDefinition.cs](Combat/Rules/Boosts/BoostDefinition.cs) | Parsed boost data |
| [Combat/Rules/Boosts/BoostType.cs](Combat/Rules/Boosts/BoostType.cs) | All boost types |
| [Combat/Rules/Boosts/ResistanceLevel.cs](Combat/Rules/Boosts/ResistanceLevel.cs) | Resistance enum |
| [Combat/Rules/Boosts/RollType.cs](Combat/Rules/Boosts/RollType.cs) | Roll context enum |
| [Combat/Rules/Boosts/README.md](Combat/Rules/Boosts/README.md) | Full documentation |
| [Tests/Combat/Rules/Boosts/BoostParserExamples.cs](Tests/Combat/Rules/Boosts/BoostParserExamples.cs) | Examples & tests |

---

## Status: Phase 1 Complete ✅

**Implemented**:
- ✅ Parsing boost strings
- ✅ All Tier 1-3 boost types defined
- ✅ Conditional boost support (IF syntax)
- ✅ Error handling
- ✅ Type-safe parameter access

**Not Yet Implemented**:
- 🔲 Boost application (apply to Combatant)
- 🔲 Boost evaluation (query active boosts)
- 🔲 Condition evaluation (evaluate IF conditions)
- 🔲 Integration with RulesEngine/DamagePipeline

See [IMPLEMENTATION_SUMMARY_BOOST_DSL.md](IMPLEMENTATION_SUMMARY_BOOST_DSL.md) for full details.
