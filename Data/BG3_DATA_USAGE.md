# BG3 Character Data Usage Guide

## Overview

The BG3 character data system provides complete character creation and building data for all 12 BG3 classes, 11 races (with subraces), weapons, and armor. Data is loaded via the `BG3DataLoader` class.

## Quick Start

```csharp
using QDND.Data;
using QDND.Data.CharacterModel;

// Create the character data registry
var characterRegistry = new CharacterDataRegistry();

// Load all BG3 data (classes, races, equipment)
BG3DataLoader.LoadAll(characterRegistry);

// Now you can lookup classes, races, and equipment
var fighter = characterRegistry.GetClass("fighter");
var highElf = characterRegistry.GetRace("elf"); // Base race
var longsword = characterRegistry.GetWeapon("longsword");
var chainMail = characterRegistry.GetArmor("chain_mail");
```

## Available Data

### Classes (12 total)
**Martial Classes** (`Data/Classes/martial_classes.json`):
- Fighter (Champion, Battle Master, Eldritch Knight, Arcane Archer)
- Barbarian (Berserker, Wildheart, Wild Magic, Path of the Giant)
- Monk (Open Hand, Shadow, Four Elements, Drunken Master)
- Rogue (Thief, Assassin, Arcane Trickster, Swashbuckler)

**Arcane Classes** (`Data/Classes/arcane_classes.json`):
- Wizard (8 schools: Abjuration, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation, Bladesinging)
- Sorcerer (Draconic Bloodline, Wild Magic, Storm Sorcery, Shadow Magic)
- Warlock (Archfey, Fiend, Great Old One, Hexblade)
- Bard (Lore, Valour, Swords, Glamour)

**Divine Classes** (`Data/Classes/divine_classes.json`):
- Cleric (Life, Light, Trickery, War, Knowledge, Nature, Tempest, Death)
- Paladin (Devotion, Ancients, Vengeance, Crown, Oathbreaker)
- Druid (Land, Moon, Spores, Stars)
- Ranger (Hunter, Beast Master, Gloom Stalker, Swarmkeeper)

### Races (11 total)
**Core Races** (`Data/Races/core_races.json`):
- Human
- Elf (High Elf, Wood Elf)
- Drow (Lolth-Sworn, Seldarine)
- Half-Elf (High, Wood, Drow heritage variants)
- Half-Orc
- Halfling (Lightfoot, Strongheart)

**Exotic Races** (`Data/Races/exotic_races.json`):
- Dwarf (Gold, Shield, Duergar)
- Gnome (Forest, Rock, Deep)
- Tiefling (Asmodeus, Mephistopheles, Zariel)
- Githyanki
- Dragonborn (10 dragon types)

### Equipment
**Weapons** (33 total) - Simple and Martial weapons covering all BG3 weapon types
**Armor** (13 total) - Light, Medium, Heavy armor plus shields

## Selective Loading

You can load data selectively if needed:

```csharp
// Load only classes
BG3DataLoader.LoadClasses(characterRegistry);

// Load only races
BG3DataLoader.LoadRaces(characterRegistry);

// Load only equipment
BG3DataLoader.LoadEquipment(characterRegistry);

// Load only feats (if available)
BG3DataLoader.LoadFeats(characterRegistry);
```

## Integration with CharacterResolver

The `CharacterResolver` uses the registry to build complete character sheets:

```csharp
var resolver = new CharacterResolver(characterRegistry);

// Create a character sheet
var sheet = new CharacterSheet
{
    Name = "Shadowheart",
    RaceId = "half_elf",
    SubraceId = "high_half_elf",
    ClassLevels = new List<ClassLevel>
    {
        new ClassLevel { ClassId = "cleric", Level = 1, SubclassId = "trickery" }
    },
    BaseAbilityScores = new Dictionary<AbilityType, int>
    {
        { AbilityType.Strength, 13 },
        { AbilityType.Dexterity, 13 },
        { AbilityType.Constitution, 14 },
        { AbilityType.Intelligence, 10 },
        { AbilityType.Wisdom, 17 },
        { AbilityType.Charisma, 8 }
    }
};

// Resolve the character (applies all race/class features, calculates stats)
var resolved = resolver.Resolve(sheet);

Console.WriteLine($"{resolved.Name} - Level {sheet.TotalLevel} {sheet.ClassLevels[0].ClassId}");
Console.WriteLine($"HP: {resolved.MaxHP}, AC: {resolved.BaseAC}");
Console.WriteLine($"Proficiencies: {resolved.Proficiencies.Skills.Count} skills");
```

## Data File Structure

All data files follow the JSON pack pattern:

```json
{
  "Classes": [ ... ],  // For classes
  "Races": [ ... ],    // For races
  "Weapons": [ ... ],  // For weapons
  "Armors": [ ... ]    // For armors
}
```

## Character Features

Each class, race, and feat grants features that provide:
- Proficiencies (weapons, armor, skills, saves)
- Abilities (spells, actions, special powers)
- Resistances/Immunities
- Resource pools (rage charges, ki points, spell slots)
- Passive bonuses (speed, HP, AC)

The CharacterResolver applies all features from all sources to create the final combat-ready character.

## Spell Slots

Full casters (Wizard, Cleric, Bard, Sorcerer, Druid) get:
- Level 1: 2 slots
- Level 2: 3 slots (+ 0/0/2/3/3 L2 slots at L3/4/5)
- Level 3: 4 slots (+ 2/3/3 L2, 0/0/2 L3 at L5/6/7)

Half casters (Paladin, Ranger) get:
- Level 2: 2 L1 slots
- Level 3: 3 L1 slots
- Level 5: 4 L1 + 2 L2 slots

Warlocks use Pact Magic slots that recharge on short rest.

## FAQ

**Q: How do I add custom classes/races?**
A: Create a new JSON file following the same structure and call the appropriate Load method.

**Q: Can I modify existing data?**
A: Yes, edit the JSON files directly. Changes take effect on next load.

**Q: How do multiclass characters work?**
A: Add multiple ClassLevel entries to the CharacterSheet. The first class provides full proficiencies, subsequent classes provide limited multiclass proficiencies.

**Q: Where are the ability scores defined?**
A: Base ability scores come from point buy (CharacterSheet.BaseAbilityScores). Race/feat bonuses are applied by features during resolution.
