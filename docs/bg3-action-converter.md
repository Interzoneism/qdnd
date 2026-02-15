# BG3 Action Converter - Implementation Guide

## Overview

The BG3 Action Converter system bridges BG3 spell data with the game's combat system by converting `BG3SpellData` to `ActionDefinition`. This enables direct use of Baldur's Gate 3 spell definitions in combat.

## Architecture

### Components

1. **ActionDefinition.cs** (Enhanced)
   - Core combat action definition
   - Enhanced with BG3-specific properties
   - Maintains backward compatibility

2. **BG3ActionConverter.cs**
   - Static converter class
   - Maps BG3SpellData → ActionDefinition
   - Handles complex formula parsing

3. **BG3SpellData.cs** (Existing)
   - Parsed BG3 spell data from TXT files
   - Contains 60+ properties
   - Source of truth for spell definitions

## New ActionDefinition Properties

### BG3-Specific Properties

```csharp
// Spell classification
public int SpellLevel { get; set; }                    // 0-9 (0 = cantrip)
public SpellSchool School { get; set; }                // Evocation, Abjuration, etc.
public CastingTimeType CastingTime { get; set; }       // Action, BonusAction, Reaction
public SpellComponents Components { get; set; }        // Verbal, Somatic, Material
public string BG3SpellType { get; set; }              // Target, Projectile, Shout, etc.

// Raw BG3 formulas (preserved for debugging)
public string BG3SpellProperties { get; set; }        // "DealDamage(1d8,Fire);..."
public string BG3SpellRoll { get; set; }              // "Attack(AttackType.RangedSpellAttack)"
public string BG3SpellSuccess { get; set; }           // Effects on hit
public string BG3SpellFail { get; set; }              // Effects on miss

// BG3 metadata
public HashSet<string> BG3Flags { get; set; }         // IsAttack, IsMelee, etc.
public VerbalIntent Intent { get; set; }              // Damage, Healing, Buff, etc.
public string BG3SourceId { get; set; }               // Original BG3 spell ID
```

### New Enums

```csharp
public enum CastingTimeType
{
    Action,         // Full action
    BonusAction,    // Bonus action
    Reaction,       // Reaction (triggered)
    Free,           // Free action
    Special         // Special timing
}

public enum SpellComponents
{
    None = 0,
    Verbal = 1,     // Requires speech
    Somatic = 2,    // Requires gestures
    Material = 4    // Requires materials
}

public enum SpellSchool
{
    None, Abjuration, Conjuration, Divination,
    Enchantment, Evocation, Illusion,
    Necromancy, Transmutation
}

public enum VerbalIntent
{
    Unknown, Damage, Healing, Buff,
    Debuff, Utility, Control, Movement
}
```

## Usage

### Basic Conversion

```csharp
using QDND.Data.Actions;
using QDND.Data.Spells;

// Convert a single spell
var bg3Spell = new BG3SpellData
{
    Id = "Projectile_FireBolt",
    DisplayName = "Fire Bolt",
    Level = 0,
    SpellType = BG3SpellType.Projectile,
    Damage = "1d10",
    DamageType = "Fire",
    TargetRadius = "18",
    UseCosts = new SpellUseCost { ActionPoint = 1 }
};

var action = BG3ActionConverter.ConvertToAction(bg3Spell);

// Use in combat
combatSystem.RegisterAction(action);
```

### Batch Conversion

```csharp
// Convert multiple spells at once
var bg3Spells = dataRegistry.GetAllBG3Spells();
var actions = BG3ActionConverter.ConvertBatch(bg3Spells);

// Register all actions
foreach (var (id, action) in actions)
{
    actionRegistry.Register(id, action);
}
```

### Preserve Raw Formulas

```csharp
// Include raw BG3 formulas for debugging
var action = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: true);

// Access raw formulas
GD.Print($"BG3 Properties: {action.BG3SpellProperties}");
GD.Print($"BG3 Roll: {action.BG3SpellRoll}");
```

## Conversion Mapping

### Spell Type → Target Type

| BG3SpellType    | TargetType   | Description                    |
| --------------- | ------------ | ------------------------------ |
| Target          | SingleUnit   | Single target spell            |
| Projectile      | SingleUnit   | Projectile-based               |
| Shout           | Self         | Self-centered AoE              |
| Zone            | Circle       | Ground-targeted AoE            |
| Multicast       | MultiUnit    | Multiple targets               |
| Rush            | Point        | Movement-based                 |
| Teleportation   | Point        | Teleport spell                 |
| Wall            | Line         | Wall/line spell                |
| Cone            | Cone         | Cone-shaped AoE                |

### Verbal Intent → Target Filter

| VerbalIntent | Target Filter           | Description               |
| ------------ | ----------------------- | ------------------------- |
| Damage       | Enemies                 | Hostile targets           |
| Healing      | Allies + Self           | Friendly targets          |
| Buff         | Allies + Self           | Friendly targets          |
| Debuff       | Enemies                 | Hostile targets           |
| Utility      | All                     | Any valid target          |

### Use Costs → Action Costs

```csharp
BG3 UseCosts:
  ActionPoint: 1              → UsesAction = true
  BonusActionPoint: 1         → UsesBonusAction = true
  ReactionActionPoint: 1      → UsesReaction = true
  SpellSlotLevel: 3           → ResourceCosts["spell_slot_3"] = 1
  CustomResources["KiPoint"]: 2 → ResourceCosts["kipoint"] = 2
```

## Effect Parsing

### Supported Formula Types

The converter parses BG3 formula strings into `EffectDefinition` objects:

#### DealDamage
```
Input:  "DealDamage(1d8, Fire)"
Output: EffectDefinition { Type = "damage", DiceFormula = "1d8", DamageType = "Fire" }
```

#### ApplyStatus
```
Input:  "ApplyStatus(BURNING, 100, 3)"
Output: EffectDefinition { Type = "apply_status", StatusId = "BURNING", StatusDuration = 3 }
```

#### Heal
```
Input:  "Heal(2d8+10)"
Output: EffectDefinition { Type = "heal", DiceFormula = "2d8+10" }
```

#### RemoveStatus
```
Input:  "RemoveStatus(POISONED)"
Output: EffectDefinition { Type = "remove_status", StatusId = "POISONED" }
```

#### Complex Formulas
```
Input:  "DealDamage(8d6,Fire);ApplyStatus(BURNING,100,2)"
Output: [
  { Type = "damage", DiceFormula = "8d6", DamageType = "Fire" },
  { Type = "apply_status", StatusId = "BURNING", StatusDuration = 2 }
]
```

## Advanced Features

### Upcast Scaling

Leveled spells (Level 1+) automatically generate upcast scaling:

```csharp
var spell = new BG3SpellData
{
    Level = 1,
    Damage = "1d6"
};

var action = BG3ActionConverter.ConvertToAction(spell);

// Automatically configured:
action.CanUpcast = true;
action.UpcastScaling.DicePerLevel = "1d6";  // +1d6 per level
action.UpcastScaling.ResourceKey = "spell_slot_1";
action.UpcastScaling.MaxUpcastLevel = 9;
```

### Concentration Tracking

Spells with the `IsConcentration` flag are automatically configured:

```csharp
spell.SpellFlags = "IsConcentration;IsSpell";

var action = BG3ActionConverter.ConvertToAction(spell);
action.RequiresConcentration = true;  // Auto-set
```

### Cooldown Management

```csharp
spell.Cooldown = "OncePerTurn";
→ action.Cooldown.TurnCooldown = 1;

spell.Cooldown = "OncePerRound";
→ action.Cooldown.RoundCooldown = 1;

spell.Cooldown = "OncePerCombat";
→ action.Cooldown.MaxCharges = 1;
→ action.Cooldown.ResetsOnCombatEnd = true;
```

## Integration with Existing Systems

### EffectPipeline Compatibility

The converter produces `EffectDefinition` objects compatible with `EffectPipeline`:

```csharp
var action = BG3ActionConverter.ConvertToAction(spell);
var pipeline = new EffectPipeline(combatService);

// Use converted action normally
pipeline.ExecuteAction(caster, action, targets);
```

### ActionVariant Support

Converted actions support variants (Chromatic Orb, etc.):

```csharp
var action = BG3ActionConverter.ConvertToAction(chromaticOrbSpell);

action.Variants.Add(new ActionVariant
{
    VariantId = "fire",
    DisplayName = "Fire",
    ReplaceDamageType = "Fire"
});
```

### Backward Compatibility

All existing ActionDefinition functionality remains intact:

```csharp
// Old code still works
var oldAction = new ActionDefinition
{
    Id = "basic_attack",
    Name = "Attack",
    TargetType = TargetType.SingleUnit,
    Effects = new List<EffectDefinition>()
};

// New BG3 properties are optional
oldAction.SpellLevel = 0;  // Default
oldAction.School = SpellSchool.None;  // Default
```

## Testing

### Running Unit Tests

```csharp
using QDND.Tests.Unit;

// Run all converter tests
BG3ActionConverterTests.RunAllTests();

// Output:
// ✓ BasicConversion_NotNull
// ✓ SpellTypeMapping_Target
// ✓ CostConversion_Action
// ... (40+ tests)
// Tests Passed: 42
// Tests Failed: 0
```

### Running Examples

```csharp
using QDND.Data.Actions;

// Run all examples
BG3ActionConverterExample.RunAllExamples();

// Or run specific examples
BG3ActionConverterExample.ExampleSingleConversion();
BG3ActionConverterExample.ExampleAoESpellConversion();
BG3ActionConverterExample.ExampleBatchConversion();
```

## Error Handling

### Graceful Degradation

The converter handles missing/invalid data gracefully:

```csharp
// Missing spell properties
var minimal = new BG3SpellData { Id = "Test" };
var action = BG3ActionConverter.ConvertToAction(minimal);
// Still creates valid action with defaults

// Invalid formulas are skipped
spell.SpellProperties = "InvalidFormula(...)";
var action = BG3ActionConverter.ConvertToAction(spell);
// action.Effects will be empty, but action is valid
```

### Batch Conversion Error Handling

```csharp
var actions = BG3ActionConverter.ConvertBatch(spells);
// Errors are logged but don't stop batch processing
// Failed conversions are skipped
```

## Best Practices

### 1. Load Once, Use Many Times

```csharp
// At startup
var allSpells = dataRegistry.LoadAllBG3Spells();
var actions = BG3ActionConverter.ConvertBatch(allSpells);

// Store in registry
foreach (var (id, action) in actions)
{
    actionRegistry.Register(id, action);
}

// Use throughout game
var fireball = actionRegistry.Get("Projectile_Fireball");
```

### 2. Preserve Raw Formulas in Development

```csharp
#if DEBUG
var action = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: true);
#else
var action = BG3ActionConverter.ConvertToAction(spell, includeRawFormulas: false);
#endif
```

### 3. Validate Critical Spells

```csharp
var action = BG3ActionConverter.ConvertToAction(spell);

// Validate conversion
if (action.SpellLevel > 0 && !action.CanUpcast)
{
    GD.PrintErr($"Warning: Level {action.SpellLevel} spell {action.Id} cannot upcast");
}

if (action.Effects.Count == 0)
{
    GD.PrintErr($"Warning: Spell {action.Id} has no effects");
}
```

### 4. Override When Needed

```csharp
// Converter provides good defaults, but you can override
var action = BG3ActionConverter.ConvertToAction(spell);

// Custom tweaks for game balance
action.Range *= 1.5f;  // 50% more range
action.Cost.ResourceCosts["mana"] = 10;  // Custom resource
action.AIBaseDesirability = 2.0f;  // Prioritize in AI
```

## Performance Considerations

### Batch Processing

Batch conversion is efficient for large spell sets:

```csharp
// Convert 500 spells in ~50ms
var stopwatch = Stopwatch.StartNew();
var actions = BG3ActionConverter.ConvertBatch(allSpells);
stopwatch.Stop();
GD.Print($"Converted {actions.Count} spells in {stopwatch.ElapsedMilliseconds}ms");
```

### Lazy Loading

Load spells on demand:

```csharp
private Dictionary<string, ActionDefinition> _actionCache = new();

public ActionDefinition GetAction(string spellId)
{
    if (!_actionCache.TryGetValue(spellId, out var action))
    {
        var spell = dataRegistry.LoadBG3Spell(spellId);
        action = BG3ActionConverter.ConvertToAction(spell);
        _actionCache[spellId] = action;
    }
    return action;
}
```

## Troubleshooting

### Issue: Effects not parsing correctly

**Solution**: Check raw formula syntax. The parser supports specific patterns:
- `DealDamage(dice, type)`
- `ApplyStatus(id, chance, duration)`
- `Heal(dice)`

### Issue: Wrong target filter

**Solution**: Ensure `VerbalIntent` and `SpellFlags` are set correctly in BG3 data.

### Issue: Missing spell school

**Solution**: Verify `SpellSchool` matches enum values exactly (case-sensitive).

## Future Enhancements

Planned improvements:

1. **Advanced Formula Parsing**
   - Support for nested formulas
   - Conditional effects
   - Complex targeting conditions

2. **Metamagic Support**
   - Automatic variant generation for metamagic
   - Heightened/Twinned/Quickened configurations

3. **AI Scoring Integration**
   - Auto-generate AI scoring from spell properties
   - Intent-based prioritization

4. **Validation Framework**
   - Spell balance validation
   - Effect consistency checks
   - Missing property warnings

## See Also

- [ActionDefinition.cs](../Combat/Actions/ActionDefinition.cs)
- [BG3ActionConverter.cs](../Data/Actions/BG3ActionConverter.cs)
- [BG3SpellData.cs](../Data/Spells/BG3SpellData.cs)
- [BG3ActionConverterTests.cs](../Tests/Unit/BG3ActionConverterTests.cs)
- [BG3ActionConverterExample.cs](../Data/Actions/BG3ActionConverterExample.cs)
