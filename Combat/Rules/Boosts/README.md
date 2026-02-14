# BG3 Boost DSL Parser

**Location**: `Combat/Rules/Boosts/`  
**Purpose**: Parse and represent BG3's boost strings for stat modifications

---

## Overview

The Boost DSL (Domain Specific Language) is BG3's foundational system for modifying combat stats. Boosts are atomic modifiers that affect everything from AC to damage to status immunity.

**Examples from BG3:**
```
"AC(2);Advantage(AttackRoll)"
"Resistance(Fire,Resistant);StatusImmunity(BURNING)"
"IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackTarget)"
"WeaponDamage(1d4,Fire);DamageBonus(5,Piercing)"
```

---

## Architecture

### 1. **BoostType.cs** - All supported boost types
- Enum defining all boost types (AC, Advantage, Resistance, etc.)
- Organized in 3 tiers:
  - **Tier 1**: Core combat (AC, Advantage, Resistance, DamageBonus, etc.)
  - **Tier 2**: Action economy (ActionResourceBlock, movement modifiers)
  - **Tier 3**: Advanced (UnlockSpell, CriticalHit, Attribute)

### 2. **BoostDefinition.cs** - Parsed boost data
- Represents a single parsed boost
- Properties:
  - `BoostType Type` - The boost type
  - `object[] Parameters` - Parsed parameters (int, float, string)
  - `string Condition` - Optional IF() condition
  - `string RawBoost` - Original text for debugging
- Helper methods:
  - `GetParameter<T>(int index)` - Type-safe parameter access
  - `GetStringParameter(int index)` - Get as string
  - `GetIntParameter(int index)` - Get as int
  - `GetFloatParameter(int index)` - Get as float

### 3. **BoostParser.cs** - The parser
- `ParseBoostString(string boosts)` → `List<BoostDefinition>`
- Handles:
  - Semicolon-delimited lists: `"AC(2);Advantage(AttackRoll)"`
  - Function-style syntax: `"FunctionName(arg1, arg2, ...)"`
  - IF() conditions: `"IF(condition):Boost1;Boost2"`
  - Nested parentheses: `"IF(HasStatus(RAGING)):DamageBonus(2,Slashing)"`
- Throws `BoostParseException` on malformed input

### 4. **ResistanceLevel.cs** - Damage resistance enum
- `Vulnerable` - 2x damage (200%)
- `Normal` - 1x damage (100%)
- `Resistant` - 0.5x damage (50%)
- `Immune` - 0x damage (0%)

### 5. **RollType.cs** - Roll context enum
- `AttackRoll` - To-hit rolls
- `SavingThrow` - Defensive saves
- `AbilityCheck` - Raw ability checks
- `SkillCheck` - Proficiency-based checks
- `Damage` - Damage dice
- `Initiative` - Turn order rolls
- `DeathSave` - Unconscious saves

---

## Usage Examples

### Basic Parsing

```csharp
using QDND.Combat.Rules.Boosts;

// Parse a simple boost
var boosts = BoostParser.ParseBoostString("AC(2)");
// Result: [BoostDefinition { Type=AC, Parameters=[2] }]

// Parse multiple boosts
var boosts = BoostParser.ParseBoostString("AC(2);Advantage(AttackRoll)");
// Result: 2 boosts

// Parse resistance
var boosts = BoostParser.ParseBoostString("Resistance(Fire,Resistant)");
// Result: [BoostDefinition { Type=Resistance, Parameters=["Fire", "Resistant"] }]
```

### Conditional Boosts

```csharp
// Parse IF() condition
var boosts = BoostParser.ParseBoostString(
    "IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackRoll)"
);
// Result: [BoostDefinition { 
//   Type=Advantage, 
//   Parameters=["AttackRoll"],
//   Condition="not DistanceToTargetGreaterThan(3)"
// }]

// Multiple boosts with shared condition
var boosts = BoostParser.ParseBoostString(
    "IF(IsMeleeAttack()):Advantage(AttackRoll);DamageBonus(1d4,Fire)"
);
// Result: 2 boosts, both with condition "IsMeleeAttack()"
```

### Accessing Parameters

```csharp
var boosts = BoostParser.ParseBoostString("DamageBonus(5,Piercing)");
var boost = boosts[0];

// Type-safe parameter access
int amount = boost.GetIntParameter(0);        // 5
string damageType = boost.GetStringParameter(1);  // "Piercing"

// Generic access
var value = boost.GetParameter<int>(0);       // 5

// Check if conditional
if (boost.IsConditional)
{
    string condition = boost.Condition;
    // Evaluate condition (requires ConditionEvaluator - future work)
}
```

### Error Handling

```csharp
try
{
    var boosts = BoostParser.ParseBoostString("InvalidBoost(2)");
}
catch (BoostParseException ex)
{
    // "Unknown boost type: InvalidBoost"
    Console.WriteLine(ex.Message);
}

try
{
    var boosts = BoostParser.ParseBoostString("AC(2"); // Missing )
}
catch (BoostParseException ex)
{
    // "Boost missing closing parenthesis: AC(2"
    Console.WriteLine(ex.Message);
}
```

---

## Real BG3 Examples

### Bless Spell
```csharp
var boosts = BoostParser.ParseBoostString(
    "RollBonus(AttackRoll,1d4);RollBonus(SavingThrow,1d4)"
);
// +1d4 to attack rolls and saving throws
```

### Barbarian Rage
```csharp
var boosts = BoostParser.ParseBoostString(
    "DamageBonus(2,Physical);Resistance(Bludgeoning,Resistant);Resistance(Piercing,Resistant);Resistance(Slashing,Resistant)"
);
// +2 damage, resistance to physical damage
```

### Haste Spell
```csharp
var boosts = BoostParser.ParseBoostString(
    "AC(2);ActionResourceMultiplier(Movement,2,0);ActionResource(ActionPoint,1,0)"
);
// +2 AC, doubled movement, extra action
```

### Poisoned Condition
```csharp
var boosts = BoostParser.ParseBoostString(
    "Disadvantage(AttackRoll);Disadvantage(AbilityCheck)"
);
// Disadvantage on attacks and ability checks
```

---

## Implementation Status

### ✅ Completed (Phase 1: Parsing)
- [x] BoostType enum with Tier 1-3 types
- [x] BoostDefinition data class
- [x] BoostParser with full syntax support
- [x] ResistanceLevel enum
- [x] RollType enum
- [x] Comprehensive error handling
- [x] XML documentation
- [x] Example/test file

### 🔲 Not Yet Implemented (Future Phases)
- [ ] **BoostApplicator.cs** - Apply boosts to combatants
- [ ] **BoostEvaluator.cs** - Evaluate boosts in combat context
- [ ] **ConditionEvaluator.cs** - Evaluate IF() conditions
- [ ] Integration with RulesEngine (RollAttack, RollSavingThrow)
- [ ] Integration with DamagePipeline (apply resistances)
- [ ] Integration with StatusSystem (track boost sources)
- [ ] Active boost tracking on Combatant

---

## Next Steps

When you're ready to make boosts functional:

1. **Create BoostApplicator.cs**
   - Apply parsed boosts to a Combatant
   - Track which status/passive granted each boost
   - Remove boosts when source expires

2. **Create BoostEvaluator.cs**
   - Query active boosts for a specific context (e.g., "get all AC boosts")
   - Evaluate conditional boosts (check IF conditions)
   - Aggregate multiple boosts of the same type

3. **Create ConditionEvaluator.cs**
   - Parse IF() condition strings
   - Evaluate conditions in combat context (source, target, observer)
   - Support BG3's condition functions (HasStatus, IsMeleeAttack, etc.)

4. **Integrate with RulesEngine**
   - `RollAttack()` checks for Advantage/Disadvantage boosts
   - `RollSavingThrow()` checks for save modifiers
   - All rolls query active boosts before calculating

5. **Integrate with DamagePipeline**
   - Check Resistance boosts before applying damage
   - Apply DamageBonus and WeaponDamage boosts

6. **Add to Status/Passive System**
   - Parse "Boosts" field from status definitions
   - Store and apply boosts when status is applied
   - Remove boosts when status expires

---

## Testing

Run the example file to verify parsing:

```csharp
using QDND.Tests.Combat.Rules.Boosts;

// Run all parsing examples
BoostParserExamples.RunAllExamples();

// Show real BG3 examples
BoostParserExamples.ShowBG3RealExamples();
```

---

## References

- [docs/BG3_COMBAT_AUTHENTICITY_ROADMAP.md](../../docs/BG3_COMBAT_AUTHENTICITY_ROADMAP.md) - Full roadmap
- BG3_Data/ - Real BG3 boost strings from game data
- Combat/Rules/Modifier.cs - Existing modifier system (will integrate with boosts)
- Combat/Rules/DamageResistance.cs - Current resistance implementation (will be replaced by boost-based system)

---

## Design Decisions

### Why object[] for Parameters?
Boost parameters can be: int (5), float (1.5), string ("Fire"), or dice notation ("1d4"). Using object[] allows heterogeneous types while maintaining simplicity.

### Why separate parsing from evaluation?
Parsing converts strings → data structures (can be done at load time).  
Evaluation applies boosts in combat context (done at runtime with full game state).  
This separation allows:
- Parsing errors caught early (during data load)
- Fast runtime evaluation (no re-parsing every frame)
- Easier testing (parse once, evaluate many times)

### Why not use Modifier directly?
Modifiers are low-level multipliers/additions.  
Boosts are high-level semantic effects ("Advantage on attacks", "Immune to fire").  
Boosts will be CONVERTED to Modifiers by the evaluator, but the boost abstraction makes data easier to read/write.
