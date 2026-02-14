# Spell Effect Conversion Implementation Summary

## Overview
Enhanced the BG3 spell conversion pipeline to properly parse raw formula strings (SpellSuccess, SpellFail, SpellRoll) from BG3 data files into working EffectDefinition objects for the combat system.

## Components Implemented

### 1. SpellEffectConverter (`Data/Actions/SpellEffectConverter.cs`)
A new utility class that parses BG3 functor-based formula strings into EffectDefinition lists.

#### Supported Functors
- **DealDamage(dice, damageType, [flags])** → damage effect
  - Example: `DealDamage(1d10, Fire, Magical)` → `Type="damage", DiceFormula="1d10", DamageType="fire"`
  - Handles `:Half` modifier for save spells: `DealDamage(3d6, Fire):Half` → `SaveTakesHalf=true`
  
- **ApplyStatus(statusId, chance, duration)** → apply_status effect
  - Example: `ApplyStatus(BLESSED,100,10)` → applies "blessed" status for 10 turns
  
- **RegainHitPoints(formula)** / **Heal(formula)** → heal effect
  - Example: `RegainHitPoints(1d8+SpellcastingAbilityModifier)` → healing with caster modifier
  
- **RemoveStatus(statusId)** → remove_status effect
  - Example: `RemoveStatus(BURNING)` → removes burning status
  
- **Force(distance)** → forced_move effect (push)
  - Example: `Force(3)` → pushes target 3 units away
  
- **Teleport(distance)** → teleport effect
  - Example: `Teleport(18)` → teleports to target location
  
- **SummonCreature(templateId, duration, hp)** → summon effect
  - Example: `SummonCreature(SKELETON, 10, 20)` → summons skeleton for 10 turns with 20 HP
  
- **CreateSurface(surfaceType, radius, duration)** → spawn_surface effect
  - Example: `CreateSurface(fire, 3, 2)` → creates fire surface with 3m radius for 2 turns

#### SpellRoll Parsing
The `ParseSpellRoll` method extracts:
- **Attack Type**: `Attack(AttackType.MeleeSpellAttack)` → `AttackType.MeleeSpell`
- **Save Type**: `not SavingThrow(Ability.Dexterity, SourceSpellDC())` → `saveType="dexterity"`
- **Save DC**: Parses fixed DC or null for `SourceSpellDC()` (use caster's spell DC)

#### Features
- **Multiple Effects**: Handles semicolon-separated effects: `DealDamage(2d6, Radiant);ApplyStatus(BLINDED,100,1)`
- **Conditional Unwrapping**: Strips BG3 wrappers like `TARGET:`, `GROUND:`, `IF()`, etc.
- **Nested Parentheses**: Properly splits functors respecting parenthesis depth
- **Generous Parsing**: Logs warnings for unparseable functors but continues processing
- **Headless-Safe**: Uses try-catch for Godot logging to support test environments

### 2. Enhanced BG3ActionConverter (`Data/Actions/BG3ActionConverter.cs`)
Modified existing converter to use SpellEffectConverter:

#### ParseEffectsFromSpellProperties
- Now calls `SpellEffectConverter.ParseEffects()` for SpellProperties, SpellSuccess, and SpellFail
- Properly marks effects with conditions:
  - Attack spells: effects get `Condition="on_hit"`
  - Save spells: success effects get `Condition="on_save_fail"`
  - Fail effects: get `Condition="on_miss"` and `SaveTakesHalf=true`

#### DetermineAttackType
- Enhanced to parse SpellRoll field first using `SpellEffectConverter.ParseSpellRoll()`
- Falls back to flag-based detection for legacy support
- Correctly maps BG3 attack types:
  - `MeleeSpellAttack` → `AttackType.MeleeSpell`
  - `RangedSpellAttack` → `AttackType.RangedSpell`
  - `MeleeWeaponAttack` → `AttackType.MeleeWeapon`
  - `RangedWeaponAttack` → `AttackType.RangedWeapon`

#### ParseSaveType
- Enhanced to parse SpellRoll field first
- Extracts save ability from `SavingThrow(Ability.X, ...)` declarations
- Falls back to SpellSaveDC field parsing
- Returns lowercase ability name (e.g., "dexterity", "wisdom")

## Examples

### Fireball (8d6 fire damage, Dex save for half)
**Input:**
```
SpellRoll: "not SavingThrow(Ability.Dexterity, SourceSpellDC())"
SpellSuccess: "DealDamage(8d6, Fire, Magical)"
```

**Output:**
```csharp
AttackType = null
SaveType = "dexterity"
Effects = [
    { Type="damage", DiceFormula="8d6", DamageType="fire", 
      Condition="on_save_fail", SaveTakesHalf=true }
]
```

### Burning Hands (3d6 fire, Dex save for half)
**Input:**
```
SpellRoll: "not SavingThrow(Ability.Dexterity, SourceSpellDC())"
SpellSuccess: "DealDamage(3d6, Fire,Magical)"
SpellFail: "DealDamage(3d6/2, Fire,Magical)"
```

**Output:**
```csharp
SaveType = "dexterity"
Effects = [
    { Type="damage", DiceFormula="3d6", DamageType="fire", Condition="on_save_fail" },
    { Type="damage", DiceFormula="3d6/2", DamageType="fire", Condition="on_miss", SaveTakesHalf=true }
]
```

### Bless (buff spell)
**Input:**
```
SpellSuccess: "ApplyStatus(BLESSED,100,10)"
```

**Output:**
```csharp
Effects = [
    { Type="apply_status", StatusId="blessed", StatusDuration=10 }
]
```

### Magic Missile (ranged spell attack)
**Input:**
```
SpellRoll: "Attack(AttackType.RangedSpellAttack)"
SpellSuccess: "DealDamage(1d4+1, Force, Magical)"
```

**Output:**
```csharp
AttackType = AttackType.RangedSpell
Effects = [
    { Type="damage", DiceFormula="1d4+1", DamageType="force", Condition="on_hit" }
]
```

## Integration

The SpellEffectConverter is automatically used during spell loading:

1. **ActionDataLoader** loads BG3 spells from `BG3_Data/Spells/*.txt`
2. **BG3ActionConverter** converts each spell to ActionDefinition
3. **SpellEffectConverter** parses formula strings into EffectDefinition lists
4. **ActionRegistry** stores the fully populated ActionDefinitions
5. **EffectPipeline** executes effects during combat using the effect handlers

## Testing

Comprehensive unit tests in `Tests/SpellEffectConverterTests.cs` covering:
- Simple damage effects
- Complex multi-effect formulas
- Status application/removal
- Healing effects
- Movement effects (force, teleport)
- Summon and surface creation
- SpellRoll parsing (attack types and saves)
- Real spell examples (Fireball, Bless, Burning Hands)

## Build Status

✅ Compiles successfully with `dotnet build`
✅ Passes `scripts/ci-build.sh`
✅ Zero compilation errors
✅ Integrates cleanly with existing ActionRegistry and EffectPipeline systems

## Files Modified

- **New:** `Data/Actions/SpellEffectConverter.cs` (329 lines)
- **Modified:** `Data/Actions/BG3ActionConverter.cs`
  - Enhanced `ParseEffectsFromSpellProperties()` to use SpellEffectConverter
  - Enhanced `DetermineAttackType()` to parse SpellRoll
  - Enhanced `ParseSaveType()` to parse SpellRoll
- **New:** `Tests/SpellEffectConverterTests.cs` (282 lines of unit tests)

## Next Steps (Recommendations)

1. **Formula Evaluation**: Implement evaluator for complex formulas like `MainMeleeWeapon+1d8`, `SpellcastingAbilityModifier`
2. **Division Handling**: Properly handle division in formulas (e.g., `3d6/2` → apply half damage)
3. **Upcast Parsing**: Parse upcast-specific formulas for damage scaling
4. **Conditional Effects**: Parse and handle more complex IF() conditionals
5. **Surface Integration**: Connect spawn_surface effects to SurfaceManager
6. **Summon Templates**: Create summon template definitions for common summons
