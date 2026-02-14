# LSX Parser for BG3 Data

This directory contains the parser implementation for BG3's LSX (Larian Studio XML) files and the parsed data models.

## Overview

The LSX parser reads BG3's XML-based data files and converts them into C# objects for use in the game. The first implementation focuses on **ActionResourceDefinitions.lsx**, which defines action resources like Action Points, Spell Slots, Rage, Ki Points, etc.

## Files

### Core Parser
- **`Parsers/LsxParser.cs`** - Generic LSX XML parser with helper methods
  - `ParseActionResourceDefinitions(filePath)` - Parse action resource definitions
  - Helper methods for extracting typed attributes from XML nodes

### Data Models
- **`ActionResources/ActionResourceDefinition.cs`** - Main data class for action resources
- **`ActionResources/ActionResourceType.cs`** - Enum of all resource types
- **`ActionResources/ReplenishType.cs`** - Enum for when resources replenish (Turn, Rest, ShortRest, etc.)
- **`ActionResources/ActionResourceLoader.cs`** - High-level API for loading and querying resources

### Testing
- **`../Tools/LsxParserTest.cs`** - Godot test node for validating the parser

## Usage

### Basic Loading

```csharp
using QDND.Data;
using QDND.Data.ActionResources;

// Load all action resources
var resources = ActionResourceLoader.LoadActionResources();

// Get a specific resource
var spellSlot = ActionResourceLoader.GetResource("SpellSlot", resources);
Console.WriteLine($"{spellSlot.Name}: MaxLevel = {spellSlot.MaxLevel}");

// Get all spell resources
var spellResources = ActionResourceLoader.GetSpellResources(resources);

// Get resources by replenish type
var shortRestResources = ActionResourceLoader.GetShortRestResources(resources);
```

### Using the Parser Directly

```csharp
using QDND.Data.Parsers;

string lsxPath = "BG3_Data/ActionResourceDefinitions.lsx";
var definitions = LsxParser.ParseActionResourceDefinitions(lsxPath);

foreach (var def in definitions)
{
    Console.WriteLine($"{def.Name} - Replenish: {def.ReplenishType}");
}
```

### Querying Resources

```csharp
// Get all per-turn resources
var turnResources = resources.Values
    .Where(r => r.ReplenishType == ReplenishType.Turn)
    .ToList();

// Get all leveled resources (spell slots)
var leveledResources = resources.Values
    .Where(r => r.MaxLevel > 0)
    .ToList();

// Get resources with dice
var diceResources = resources.Values
    .Where(r => r.DiceType.HasValue)
    .ToList();
```

## Data Model

### ActionResourceDefinition Properties

| Property | Type | Description |
|----------|------|-------------|
| `UUID` | Guid | Unique identifier from BG3 |
| `Name` | string | Internal resource name (e.g., "ActionPoint") |
| `DisplayName` | string | Player-facing name |
| `Description` | string | Resource description |
| `ReplenishType` | ReplenishType | When resource replenishes |
| `MaxLevel` | uint | Max level for leveled resources (0 = not leveled) |
| `MaxValue` | uint? | Max value cap (optional) |
| `DiceType` | uint? | Dice type if resource uses dice (6, 8, 12) |
| `IsSpellResource` | bool | True for spell slots |
| `UpdatesSpellPowerLevel` | bool | True if affects spell power |
| `ShowOnActionResourcePanel` | bool | Display in UI |
| `IsHidden` | bool | Hidden from players |
| `PartyActionResource` | bool | Shared across party |
| `ResourceType` | ActionResourceType | Parsed resource type enum |

### ReplenishType Values

- **Turn** - Replenished at start of each turn (Action, Bonus Action, Reaction, Movement)
- **Rest** - Replenished on long rest (Spell Slots, Rage, Sorcery Points)
- **ShortRest** - Replenished on short rest (Warlock Slots, Ki Points, Channel Divinity)
- **FullRest** - Same as Rest, alternative term used by BG3
- **Never** - Never auto-replenishes (Inspiration Points)

### ActionResourceType Examples

Core combat resources:
- `ActionPoint` - Primary action (1/turn)
- `BonusActionPoint` - Bonus action (1/turn)
- `ReactionActionPoint` - Reaction (1/turn)
- `Movement` - Movement speed

Spellcasting:
- `SpellSlot` - Standard spell slots (levels 1-9)
- `WarlockSpellSlot` - Warlock pact magic slots
- `SorceryPoint` - Sorcerer metamagic points

Class-specific:
- `Rage` - Barbarian rage charges
- `BardicInspiration` - Bard inspiration dice
- `KiPoint` - Monk ki points
- `SuperiorityDie` - Battle master maneuvers
- `ChannelDivinity` - Cleric channel divinity

## LSX File Format

LSX files are XML with strongly-typed attributes:

```xml
<node id="ActionResourceDefinition">
    <attribute id="UUID" type="guid" value="734cbcfb-8922-4b6d-8330-b2a7e4c14b6a"/>
    <attribute id="Name" type="FixedString" value="ActionPoint"/>
    <attribute id="DisplayName" type="TranslatedString" handle="Action" version="3"/>
    <attribute id="ReplenishType" type="FixedString" value="Turn"/>
    <attribute id="MaxLevel" type="uint32" value="0"/>
    <attribute id="ShowOnActionResourcePanel" type="bool" value="true"/>
</node>
```

### Attribute Types

- **guid** - GUID value
- **FixedString** - String value
- **TranslatedString** - Localized text (uses `handle` attribute, not `value`)
- **uint32** - Unsigned 32-bit integer
- **int32** - Signed 32-bit integer
- **bool** - Boolean (true/false)

## Testing

### In Godot Editor

Add `LsxParserTest.cs` to a scene:

```gdscript
# Create a test scene
var test = new LsxParserTest()
add_child(test)
# Check console output
```

### CLI Test

```bash
pwsh scripts/test_lsx_parser.ps1
```

### Validation

The parser validates:
- All resources have non-empty UUIDs
- All resources have valid names
- Enum values parse correctly
- ReplenishType values are recognized

## Extending the Parser

To parse additional LSX files:

1. Create data model classes in appropriate subfolder (e.g., `Data/Classes/`)
2. Add parsing method to `LsxParser.cs`:
   ```csharp
   public static List<YourDataClass> ParseYourLsxFile(string filePath)
   {
       // Similar to ParseActionResourceDefinitions
   }
   ```
3. Use helper methods:
   - `GetAttributeValue(node, "id")` - Get string value
   - `GetGuidAttribute(node, "id")` - Get GUID
   - `GetBoolAttribute(node, "id")` - Get boolean
   - `GetIntAttribute(node, "id")` - Get integer
   - `GetTranslatedString(node, "id")` - Get localized text

## Reference

See [BG3_Data/README.md](../../BG3_Data/README.md) for:
- Complete LSX file format specification
- List of all available data files
- Cross-reference relationships
- Enum value mappings

## Implementation Notes

- Parser uses `System.Xml.Linq` for XML parsing
- All GUID attributes are stored as `System.Guid`
- TranslatedString attributes use the `handle` attribute for text content
- Unknown resource names default to `ActionResourceType.Unknown`
- Missing optional attributes use sensible defaults (false for bools, 0 for numbers)
