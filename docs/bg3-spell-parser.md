# BG3 Spell Parser

A comprehensive parser for Baldur's Gate 3 spell definition files (Stats TXT format) for use in the QDND Godot 4.5 C# game project.

## Overview

The BG3 spell parser reads spell definitions from TXT files using BG3's custom format and converts them into structured C# data models that can be used in the game.

## File Structure

```
Data/
├── Parsers/
│   └── BG3SpellParser.cs          # Main parser implementation
└── Spells/
    ├── SpellType.cs                # Spell type enum
    ├── BG3SpellData.cs             # Spell data model
    └── BG3SpellParserExample.cs    # Usage examples
```

## BG3 TXT File Format

BG3 spell files use a custom text format:

```
new entry "SpellName"
type "SpellData"
data "Field" "Value"
using "ParentSpellName"
```

### Example

```
new entry "Target_MainHandAttack"
type "SpellData"
data "SpellType" "Target"
data "DisplayName" "Main Hand Attack"
data "Description" "Make a melee attack with your equipped weapon."
data "UseCosts" "ActionPoint:1"
data "SpellFlags" "IsAttack;IsMelee;IsHarmful"

new entry "Target_WEAPON ATTACK"
type "SpellData"
data "SpellType" "Target"
using "Target_MainHandAttack"
```

## Features

### Core Capabilities

- **Parse TXT files**: Read BG3's custom spell format
- **Inheritance support**: Handle `using "ParentSpell"` directives
- **Structured data**: Convert to strongly-typed C# classes
- **Resource cost parsing**: Parse `UseCosts` into structured format
- **Error handling**: Track and report parsing errors and warnings
- **Statistics**: Generate parsing statistics and spell distribution

### Parsed Fields

The parser extracts and structures:

- **Identity**: Id, DisplayName, Description, Icon
- **Mechanics**: SpellType, Level, SpellSchool, SpellProperties
- **Targeting**: TargetRadius, AreaRadius, TargetConditions
- **Costs**: UseCosts (Actions, Bonus Actions, Reactions, Spell Slots)
- **Effects**: SpellRoll, SpellSuccess, SpellFail
- **Flags**: SpellFlags, WeaponTypes, VerbalIntent
- **And 40+ more fields...**

### Spell Types

```csharp
public enum BG3SpellType
{
    Target,           // Single target (melee/ranged)
    Projectile,       // Projectile-based
    Shout,            // Self-centered AoE
    Zone,             // Ground-targeted AoE
    Rush,             // Charge/dash
    Teleportation,    // Teleport spell
    Throw,            // Throw object
    ProjectileStrike  // Passive strike
}
```

### Resource Costs

The parser automatically parses cost strings like:
- `"ActionPoint:1"` → Uses 1 action
- `"BonusActionPoint:1"` → Uses 1 bonus action
- `"ReactionActionPoint:1"` → Uses 1 reaction
- `"SpellSlot:3:1"` → Uses 1 level 3 spell slot
- `"ActionPoint:1;Movement:9"` → Uses action + 9m movement

## Usage

### Basic Usage

```csharp
using QDND.Data.Parsers;
using QDND.Data.Spells;

var parser = new BG3SpellParser();

// Parse all spell files from directory
var spells = parser.ParseDirectory("BG3_Data/Spells");

// Resolve inheritance
parser.ResolveInheritance();

// Access parsed spells
foreach (var spell in spells)
{
    Console.WriteLine($"{spell.Id}: {spell.DisplayName} ({spell.SpellType})");
}

// Print statistics
parser.PrintStatistics();
```

### Parse Single File

```csharp
var parser = new BG3SpellParser();
var spells = parser.ParseFile("BG3_Data/Spells/Spell_Target.txt");
parser.ResolveInheritance();
```

### Query Spells

```csharp
var parser = new BG3SpellParser();
parser.ParseDirectory("BG3_Data/Spells");
parser.ResolveInheritance();

// Get specific spell
var mainHand = parser.GetSpell("Target_MainHandAttack");
Console.WriteLine($"Cost: {mainHand.UseCosts}");
Console.WriteLine($"Range: {mainHand.TargetRadius}");

// Query by type
var allSpells = parser.GetAllSpells();
var projectiles = allSpells.Values
    .Where(s => s.SpellType == BG3SpellType.Projectile)
    .ToList();

// Query by flags
var meleeAttacks = allSpells.Values
    .Where(s => s.HasFlag("IsMelee"))
    .ToList();

// Query by cost
var bonusActions = allSpells.Values
    .Where(s => s.UseCosts?.BonusActionPoint > 0)
    .ToList();
```

### Access Spell Properties

```csharp
var spell = parser.GetSpell("Target_MainHandAttack");

// Basic info
Console.WriteLine($"Name: {spell.DisplayName}");
Console.WriteLine($"Type: {spell.SpellType}");
Console.WriteLine($"Level: {spell.Level}");

// Costs
if (spell.UseCosts != null)
{
    Console.WriteLine($"Action: {spell.UseCosts.ActionPoint}");
    Console.WriteLine($"Bonus: {spell.UseCosts.BonusActionPoint}");
    Console.WriteLine($"Spell Slot: L{spell.UseCosts.SpellSlotLevel}");
}

// Flags
var flags = spell.GetFlags(); // Returns List<string>
if (spell.HasFlag("IsAttack"))
{
    Console.WriteLine("This is an attack");
}

// Inheritance
if (!string.IsNullOrEmpty(spell.ParentId))
{
    Console.WriteLine($"Inherits from: {spell.ParentId}");
}

// Raw properties (for fields not mapped to properties)
var customField = spell.RawProperties.GetValueOrDefault("SomeCustomField");
```

## Testing

A test tool is provided to verify the parser:

```csharp
// In Tools/TestSpellParser.cs
QDND.Tools.TestSpellParser.Run();
```

This will:
1. Parse individual files
2. Parse entire directory
3. Resolve inheritance
4. Check specific spells
5. Print statistics

## Implementation Details

### Parser Architecture

1. **Lexical Analysis**: Reads TXT files line-by-line
2. **Pattern Matching**: Uses regex to extract entry names and data fields
3. **Object Building**: Constructs BG3SpellData objects
4. **Inheritance Resolution**: Applies parent properties recursively
5. **Type Conversion**: Parses strings into appropriate types (int, enum, etc)

### Inheritance Resolution

The parser handles multi-level inheritance:

```
Target_MainHandAttack (base)
  ↓ using
Target_WEAPON ATTACK (child)
  ↓ using  
Target_SomeSpecificAttack (grandchild)
```

Properties are inherited from parent → child, with child values taking precedence.

### Error Handling

The parser tracks:
- **Errors**: Critical issues (file not found, malformed entries)
- **Warnings**: Non-critical issues (unknown parent, missing fields)

Access via:
```csharp
parser.Errors    // List<string>
parser.Warnings  // List<string>
```

## Integration with DataRegistry

To integrate with the existing DataRegistry system:

```csharp
// In DataRegistry.cs, add:
public void LoadBG3SpellsFromDirectory(string path)
{
    var parser = new BG3SpellParser();
    var spells = parser.ParseDirectory(path);
    parser.ResolveInheritance();
    
    foreach (var bg3Spell in spells)
    {
        // Convert BG3SpellData to ActionDefinition
        var action = ConvertBG3SpellToAction(bg3Spell);
        RegisterAction(action);
    }
}

private ActionDefinition ConvertBG3SpellToAction(BG3SpellData bg3Spell)
{
    // Conversion logic here
    return new ActionDefinition
    {
        Id = bg3Spell.Id,
        Name = bg3Spell.DisplayName,
        Description = bg3Spell.Description,
        // ... map other fields
    };
}
```

## Example Output

Counts below are illustrative and will change as source packs/change-sets evolve.

```
=== BG3 Spell Parser Test ===

Test 1: Parsing Spell_Target.txt...
  Parsed <varies> spells

Test 2: Parsing all spell files...
  Parsed <varies> total spells

Test 3: Resolving inheritance...

Test 4: Checking 'Target_MainHandAttack' spell...
  ID: Target_MainHandAttack
  DisplayName: Main Hand Attack
  Type: Target
  Icon: res://assets/Images/Icons Weapon Actions/Main_Hand_Attack_Unfaded_Icon.png
  Cost: Action:1
  Flags: IsAttack, IsMelee, IsHarmful, CanDualWield
  Description: Make a melee attack with your equipped weapon.

=== Statistics ===
[BG3SpellParser] Parsed <varies> spells
[BG3SpellParser] Errors: <varies>, Warnings: <varies>

=== Spell Type Distribution ===
  Target: <varies>
  Projectile: <varies>
  Shout: <varies>
  Zone: <varies>
  Rush: <varies>
  Teleportation: <varies>
  Throw: <varies>
  ProjectileStrike: <varies>
```

## Next Steps

1. **Convert to ActionDefinition**: Create a converter from BG3SpellData to ActionDefinition
2. **Load at startup**: Add spell loading to game initialization
3. **Data validation**: Add validation rules for parsed spells
4. **Cache**: Implement caching for faster subsequent loads
5. **Export to JSON**: Option to export parsed data to JSON for inspection

## See Also

- [ActionDefinition.cs](../Combat/Actions/ActionDefinition.cs) - Target data model
- [DataRegistry.cs](../Data/DataRegistry.cs) - Central data registry
- BG3_Data/Spells/ - Source spell files
