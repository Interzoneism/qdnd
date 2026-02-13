# BG3 Reference Data — Agent Developer Guide

> **Source**: Unpacked from Baldur's Gate 3 `Shared.pak`.  
> **Purpose**: Authoritative reference for D&D 5e combat mechanics as implemented by Larian Studios. Use these files to validate or derive game data (class stats, spell formulas, weapon properties, status effects, etc.) when building or modifying systems in this project.  
> **Rule**: These files are **read-only reference**. Never modify them. Extract the data you need into the project's own data layer (`Data/` folder).  
> **Cleaned**: All cosmetic/visual data has been stripped (~31,000 lines removed). This includes Icons, Animations, VFX, Sound events, Material references, RootTemplates, skin/hair/eye color palettes, and character creation pose data. Each file has a comment header listing exactly what was removed. Only mechanical gameplay data is retained.

---

## Table of Contents

1. [Folder Structure](#folder-structure)  
2. [File Formats](#file-formats)  
3. [Core Definition Files (LSX)](#core-definition-files-lsx)  
4. [Stats Data Files (TXT)](#stats-data-files-txt)  
5. [Spell Files](#spell-files)  
6. [Status Files](#status-files)  
7. [Relationships & Cross-References](#relationships--cross-references)  
8. [Key Enumerations](#key-enumerations)  
9. [Practical Examples](#practical-examples)  

---

## Folder Structure

```
BG3_Data/
├── README.md                              ← This file
│
├── ClassDescriptions.lsx                  ← Class & subclass definitions (601 lines)
├── Progressions.lsx                       ← Per-level class progression tables (2,506 lines)
├── ProgressionDescriptions.lsx            ← Display text for progressions (2,028 lines)
├── ActionResourceDefinitions.lsx          ← Resource types (ActionPoint, SpellSlot, Rage, etc.) (277 lines)
├── ActionResourceGroupDefinitions.lsx     ← Groupings of resources (e.g. SpellSlotsGroup) (15 lines)
├── Backgrounds.lsx                        ← Character backgrounds (131 lines)
├── Races.lsx                              ← Race/subrace hierarchy (5,619 lines, cosmetics stripped)
├── FeatDescriptions.lsx                   ← Feat names and IDs (157 lines)
├── DifficultyClasses.lsx                  ← Named DC values (Easy=7, Medium=10, etc.) (160 lines)
├── Rulesets.lsx                           ← Difficulty presets (Explorer, Balanced, Tactician) (62 lines)
├── RulesetModifiers.lsx                   ← Adjustable parameters per difficulty (248 lines)
├── RulesetValues.lsx                      ← Concrete values per ruleset per modifier (244 lines)
│
├── Spells/
│   ├── Spell_Target.txt                   ← Melee attacks, single-target spells (8,980 lines)
│   ├── Spell_Projectile.txt               ← Ranged attacks, projectile spells (4,705 lines)
│   ├── Spell_Shout.txt                    ← Self-buff/AoE-around-self abilities (4,254 lines)
│   ├── Spell_Zone.txt                     ← Cone/AoE zone spells (399 lines)
│   ├── Spell_Rush.txt                     ← Charge/dash-attack abilities (248 lines)
│   ├── Spell_Teleportation.txt            ← Teleport spells like Misty Step (89 lines)
│   ├── Spell_Throw.txt                    ← Throw abilities (86 lines)
│   ├── Spell_ProjectileStrike.txt         ← Multi-hit projectile variants (24 lines)
│   ├── SpellSet.txt                       ← Pre-built spell loadouts (428 lines)
│   └── MetaConditions.lsx                 ← Spell metamagic/condition definitions (18 lines)
│
├── Stats/
│   ├── Character.txt                      ← Base stat blocks: heroes, NPCs, monsters (4,416 lines)
│   ├── Weapon.txt                         ← All weapon definitions (1,655 lines)
│   ├── Armor.txt                          ← All armor definitions (1,990 lines)
│   ├── Passive.txt                        ← Passive abilities & feats (3,042 lines)
│   ├── Interrupt.txt                      ← Reaction system (Counterspell, AoO, etc.) (452 lines)
│   ├── Object.txt                         ← Consumables, scrolls, misc items (6,490 lines)
│   ├── Modifiers.txt                      ← SCHEMA: field definitions for all stat types (646 lines)
│   ├── Equipment.txt                      ← Equipment set definitions (3,811 lines)
│   ├── ItemTypes.txt                      ← Item type category mappings (28 lines)
│   ├── Data.txt                           ← Level scaling tables & misc data (648 lines)
│   ├── XPData.txt                         ← XP thresholds per level (12 lines)
│   └── CriticalHitTypes.txt               ← Critical hit type definitions (16 lines)
│
└── Statuses/
    ├── Status_BOOST.txt                   ← Buff/debuff statuses with Boosts (7,336 lines)
    ├── Status_INCAPACITATED.txt           ← Stunned, Paralyzed, Sleeping (389 lines)
    ├── Status_POLYMORPHED.txt             ← Wild Shape, Polymorph forms (763 lines)
    ├── Status_EFFECT.txt                  ← Visual-only effect statuses (400 lines)
    ├── Status_KNOCKED_DOWN.txt            ← Prone and knockdown variants (112 lines)
    ├── Status_FEAR.txt                    ← Frightened conditions (42 lines)
    ├── Status_DOWNED.txt                  ← Death saving throw state (65 lines)
    ├── Status_HEAL.txt                    ← Healing-over-time statuses (20 lines)
    ├── Status_INVISIBLE.txt               ← Invisibility variants (61 lines)
    ├── Status_SNEAKING.txt                ← Stealth/hiding statuses (14 lines)
    └── Status_DEACTIVATED.txt             ← Deactivation statuses (16 lines)
```

---

## File Formats

### LSX (Larian Studio XML)
Structured XML with typed attributes. Every entity has a `UUID` (guid) used for cross-referencing.

```xml
<node id="ClassDescription">
    <attribute id="Name" type="FixedString" value="Fighter"/>
    <attribute id="BaseHp" type="int32" value="10"/>
    <attribute id="HpPerLevel" type="int32" value="6"/>
    <attribute id="PrimaryAbility" type="uint8" value="1"/>       ← enum index
    <attribute id="SpellCastingAbility" type="uint8" value="0"/>  ← 0 = None
    <attribute id="UUID" type="guid" value="721dfac3-..."/>
    <attribute id="ProgressionTableUUID" type="guid" value="ba4e707e-..."/>  ← FK to Progressions.lsx
</node>
```

Common attribute types: `int32`, `uint8`, `bool`, `FixedString`, `LSString`, `guid`, `TranslatedString` (localized — handle is a lookup key, not actual text).

### TXT (Larian Stats Format)
Flat key-value entries. Each block starts with `new entry "Name"`, declares `type`, and optionally inherits via `using "ParentName"`.

```
new entry "WPN_Longsword"
type "Weapon"
using "_BaseWeapon"
data "Damage" "1d8"
data "Damage Type" "Slashing"
data "VersatileDamage" "1d10"
data "Weapon Properties" "Versatile;Melee;Dippable"
data "Proficiency Group" "Longswords;MartialWeapons"
```

The `using` keyword means inheritance — all fields from the parent apply unless overridden. This is heavily used.

---

## Core Definition Files (LSX)

### ClassDescriptions.lsx
**What**: Defines every class and subclass in the game.  
**Key fields**:

| Field | Type | Meaning |
|-------|------|---------|
| `Name` | string | Internal ID: `Barbarian`, `Fighter`, `BattleMaster`, etc. |
| `BaseHp` | int | Hit points at level 1 (only on base classes, not subclasses) |
| `HpPerLevel` | int | HP gained per level after 1st |
| `PrimaryAbility` | uint8 | Enum: 1=Str, 2=Dex, 3=Con, 4=Int, 5=Wis, 6=Cha |
| `SpellCastingAbility` | uint8 | Same enum. 0=None |
| `MustPrepareSpells` | bool | Whether the class prepares spells (Cleric, Druid, Paladin, Wizard) |
| `MulticlassSpellcasterModifier` | double | 1.0=full, 0.5=half, 0.34=third caster |
| `CanLearnSpells` | bool | Wizard's scroll-learning capability |
| `ParentGuid` | guid | Links subclass → base class |
| `ProgressionTableUUID` | guid | **FK → Progressions.lsx** (same as `TableUUID` there) |
| `SpellList` | guid | Links to spell list definitions |
| `UUID` | guid | Primary key for this class |

**Complete class list with stats**:

| Class | BaseHp | HpPerLevel | Primary | Spellcasting | Caster Mod |
|-------|--------|-----------|---------|-------------|------------|
| Barbarian | 12 | 7 | Str | None | — |
| Bard | 8 | 5 | Cha | Cha | 1.0 |
| Cleric | 8 | 5 | Wis | Wis | 1.0 |
| Druid | 8 | 5 | Wis | Wis | 1.0 |
| Fighter | 10 | 6 | Str | None | — |
| Monk | 8 | 5 | Dex | Wis | — |
| Paladin | 10 | 6 | Str | Cha | 0.5 |
| Ranger | 10 | 6 | Dex | Wis | 0.5 |
| Rogue | 8 | 5 | Dex | None | — |
| Sorcerer | 6 | 4 | Cha | Cha | 1.0 |
| Warlock | 8 | 5 | Cha | Cha | — |
| Wizard | 6 | 4 | Int | Int | 1.0 |

**Subclasses** (linked via `ParentGuid`):
- Barbarian → BerserkerPath, TotemWarriorPath
- Bard → LoreCollege, ValorCollege
- Cleric → LifeDomain, LightDomain, TrickeryDomain
- Druid → CircleOfTheLand, CircleOfTheMoon
- Fighter → BattleMaster, EldritchKnight (1/3 caster, Int)
- Monk → (subclasses exist in SharedDev)
- Paladin → Ancients, Devotion, Oathbreaker
- Ranger → BeastMaster, Hunter
- Rogue → Thief, ArcaneTrickster (1/3 caster, Int)
- Sorcerer → DraconicBloodline, WildMagic
- Warlock → Fiend, GreatOldOne
- Wizard → AbjurationSchool, EvocationSchool

---

### Progressions.lsx
**What**: The level-up table. Defines what each class/subclass gains at every level (1–5 in Shared; higher levels in SharedDev).  
**Linked from**: `ClassDescriptions.lsx` via `ProgressionTableUUID` ↔ `TableUUID`.

**Key fields**:

| Field | Meaning |
|-------|---------|
| `TableUUID` | Links to `ClassDescriptions.ProgressionTableUUID` |
| `Level` | Character level this node applies at |
| `Name` | Class name (matches `ClassDescriptions.Name`) |
| `ProgressionType` | 0 = base class, 1 = subclass |
| `Boosts` | Semicolon-delimited boosts granted at this level |
| `PassivesAdded` | Passive abilities gained |
| `PassivesRemoved` | Passives that get replaced (e.g., Rage → FrenzyRage) |
| `Selectors` | Choices offered (skill picks, spell selections, etc.) |
| `AllowImprovement` | If true, this level grants an ASI/Feat choice |
| `SubClasses` | Child nodes listing available subclass UUIDs at this level |

**Boost syntax examples** (these are BG3's DSL):
```
ActionResource(Rage,2,0)              → Gain 2 Rage charges (level 0 = flat)
ActionResource(SpellSlot,2,1)         → Gain 2 level-1 spell slots
ProficiencyBonus(SavingThrow,Strength)→ Proficiency in STR saves
Proficiency(MartialWeapons)           → Weapon proficiency
Proficiency(MediumArmor)              → Armor proficiency
Attribute(UseMusicalInstrumentForCasting) → Special flag
```

**Selector syntax examples**:
```
SelectSkills(uuid,2)                  → Pick 2 skills from list [uuid]
SelectSpells(uuid,4,0,BardSpells)     → Learn 4 spells from list [uuid], 0 replacements
AddSpells(uuid)                       → Automatically learn specific spells
SelectPassives(uuid,1,GroupName)      → Pick 1 passive from list [uuid]
SelectEquipment(uuid,1,BardInstrument)→ Pick 1 starting instrument
SelectSkillsExpertise(uuid,2)         → Pick 2 expertise skills
ReplacePassives(uuid,1,GroupName)     → Swap out a previously chosen passive
```

**Example — Barbarian level-by-level**:
- **Level 1**: Proficiency(LightArmor, MediumArmor, Shields, SimpleWeapons, MartialWeapons), SaveProf(STR, CON), 2 Rage, passives: RageUnlock + UnarmouredDefence, pick 2 skills
- **Level 2**: PassivesAdded: DangerSense, RecklessAttack
- **Level 3**: +1 Rage, subclass choice (Berserker or Totem)
- **Level 4**: ASI/Feat
- **Level 5**: ExtraAttack, FastMovement

---

### ActionResourceDefinitions.lsx
**What**: Defines every expendable resource type in the game.  
**Referenced by**: Spells (`UseCosts` field), Progressions (`Boosts` field), Interrupts (`Cost` field).

| Resource | ReplenishType | Notes |
|----------|--------------|-------|
| `ActionPoint` | Turn | Standard action; 1 per turn |
| `BonusActionPoint` | Turn | Bonus action; 1 per turn |
| `ReactionActionPoint` | Turn | Reaction; 1 per turn |
| `Movement` | Turn | Movement resource in distance units |
| `SpellSlot` | Rest | Standard caster slots (MaxLevel=9, IsSpellResource, UpdatesSpellPowerLevel) |
| `WarlockSpellSlot` | **ShortRest** | Pact Magic slots (MaxLevel=9, separate from SpellSlot) |
| `Rage` | Rest | Barbarian Rage charges |
| `BardicInspiration` | Rest | DiceType=d6 (scales at higher levels) |
| `ChannelDivinity` | ShortRest | Cleric channel divinity |
| `SuperiorityDie` | ShortRest | Battle Master maneuvers (DiceType=d8) |
| `KiPoint` | ShortRest | Monk Ki points |
| `SorceryPoint` | Rest | Sorcerer metamagic |
| `HitDice` | Rest | Short rest healing (DiceType=d6, class-dependent) |
| `InspirationPoint` | Never | Party-wide, MaxValue=4 |

**ReplenishType values**: `Turn`, `ShortRest`, `Rest` (long rest), `Never`.  
**UseCosts syntax** in spells: `ActionPoint:1`, `SpellSlotsGroup:1:1:2` (1 slot, minimum level 1, slot level 2), `Movement:Distance`.

---

### ActionResourceGroupDefinitions.lsx
**What**: Groups related resources together so the UI and spell system can reference them collectively (e.g., `SpellSlotsGroup` encompasses both `SpellSlot` and `WarlockSpellSlot`).

---

### Backgrounds.lsx
**What**: The 11 character backgrounds available in character creation.

| Background | Passive |
|-----------|---------|
| Acolyte | Background_Acolyte |
| Charlatan | Background_Charlatan |
| Criminal | Background_Criminal |
| Entertainer | Background_Entertainer |
| Folk Hero | Background_FolkHero |
| Guild Artisan | Background_GuildArtisan |
| Noble | Background_Noble |
| Outlander | Background_Outlander |
| Sage | Background_Sage |
| Soldier | Background_Soldier |
| Urchin | Background_Urchin |

Each background grants a passive (defined in `Passive.txt`) and has a tag UUID for narrative triggers.

---

### Races.lsx
**What**: All races and subraces (5,619 lines after cosmetic stripping — originally 30,219 lines).  
**Removed**: ~24,600 lines of color palette data (SkinColors, EyeColors, HairColors, HairHighlightColors, LipsMakeupColors, MakeupColors, TattooColors), ExcludedGods, RaceEquipment, RaceSoundSwitch.  
**Useful data**: Race hierarchy (parent/child via UUIDs), race `Name` values, gameplay Tags. The mechanical racial traits (darkvision, resistances, weapon proficiencies) are applied through the Progression system, not directly in this file.

**Key races**: Human, Elf (High, Wood), Drow (Lolth, Seldarine), Half-Elf (High, Wood, Drow), Dwarf (Gold, Shield), Halfling (Lightfoot, Strongheart), Gnome (Forest, Deep, Rock), Tiefling (Asmodeus, Mephistopheles, Zariel), Githyanki, Half-Orc, Dragonborn.

---

### FeatDescriptions.lsx
**What**: Names and IDs for all available feats. The mechanical effects of feats are defined in `Passive.txt`.

**All feats**: AbilityScoreIncrease, Athlete, DefensiveDuelist, DualWielder, GreatWeaponMaster, HeavilyArmored, LightlyArmored, MagicInitiate (Bard/Cleric/Druid/Sorcerer/Warlock/Wizard), MartialAdept, Mobile, ModeratelyArmored, Performer, ShieldMaster, Skilled, Tough, WeaponMaster.

---

### DifficultyClasses.lsx
**What**: Named DC presets used by the game for skill checks and encounters.

| Name | DC |
|------|-----|
| Zero | 0 |
| Negligible | 2 |
| VeryEasy | 5 |
| Easy | 7 |
| Medium | 10 |
| Challenging | 12 |
| Hard | 15 |
| VeryHard | 18 |
| NearlyImpossible | 20 |
| Impossible | 25 |
| DCCap | 31 |

Also includes `HiddenPerception_*` DCs (10, 15, 20, 25) and `Legacy_*` DCs for backward compatibility.

---

### Rulesets.lsx / RulesetModifiers.lsx / RulesetValues.lsx
**What**: The difficulty system. `Rulesets` defines presets (Explorer, Balanced, Tactician, Custom). `RulesetModifiers` defines adjustable parameters (AI lethality, enemy HP bonuses, camp cost, etc.). `RulesetValues` maps specific values to each preset.

Mostly references GUIDs — useful for understanding the difficulty framework, but low priority for combat data extraction.

---

## Stats Data Files (TXT)

### Stats/Modifiers.txt — THE SCHEMA
**What**: This is the **most important structural file**. It defines every valid field name and type for each stat category. Think of it as the database schema. (646 lines)

**Stat types defined**:
- `Armor` — Fields: ArmorClass, ArmorType, Ability Modifier Cap, ProficiencyGroup, Slot, Shield, Weight, Boosts, etc.
- `Character` — Fields: Strength through Charisma, Vitality, Armor, ActionResources, Passives, SpellCastingAbility, all Resistances, Level, Weight, etc.
- `Weapon` — Fields: Damage, Damage Type, DamageRange, WeaponRange, WeaponProperties, VersatileDamage, ProficiencyGroup, BoostsOnEquipMainHand, etc.
- `SpellData` — Fields: SpellType, Level, SpellSchool, SpellRoll, SpellSuccess, SpellFail, TargetRadius, TargetConditions, UseCosts, SpellFlags, DamageType, Duration, etc.
- `PassiveData` — Fields: Boosts, Properties, ToggleOnFunctors, Conditions, StatsFunctorContext, etc.
- `StatusData` — Fields: StatusType, Boosts, StatusPropertyFlags, OnApplyFunctors, TickType, StackId, etc.
- `InterruptData` — Fields: InterruptContext, Conditions, Properties, Cost, Roll, Success, Failure, etc.
- `Object` — Fields for consumables/misc items.

**When in doubt about what a field means, check this file first.**

---

### Stats/Character.txt — Stat Blocks
**What**: Base stat templates for every character archetype. (4,416 lines)

**Key entries**:
- `_Base` — The root template. All 10s in abilities, AC 10, 5 Vitality, `ActionResources: ActionPoint:1;BonusActionPoint:1;Movement:9;ReactionActionPoint:1`, default passives: `AttackOfOpportunity;DarknessRules`.
- `_Hero` — Player character template. Extends `_Base` with passives: `ShortResting;NonLethal;WeaponThrow;Perform;AttackOfOpportunity;DarknessRules;CombatStartAttack`. Sets Vitality=1 (overridden by class).
- Race/gender variants: `HeroDwarfFemale`, `HeroElfMale`, etc. — mainly adjust `Weight`.
- NPC stat blocks: Hundreds of enemy templates with actual ability scores, HP, resistances, and specific passives.

**Relationship**: Character.txt entries reference passives from `Passive.txt` and action resources from `ActionResourceDefinitions.lsx`.

---

### Stats/Weapon.txt — Weapon Database
**What**: Every weapon in the game with D&D-accurate stats. (1,655 lines)

**Key fields per weapon**:
- `Damage` — Dice expression: `1d8`, `1d6`, `2d6`, etc.
- `Damage Type` — Slashing, Piercing, Bludgeoning
- `Weapon Properties` — Semicolon-delimited: `Finesse;Light;Thrown;Melee;Dippable`
- `Proficiency Group` — e.g., `Longswords;MartialWeapons`
- `VersatileDamage` — Damage when wielded two-handed
- `BoostsOnEquipMainHand` — Weapon-specific abilities unlocked, e.g., `UnlockSpell(Zone_Cleave);UnlockSpell(Target_Slash_New)`
- `WeaponRange` — Range in BG3 distance units (150 = standard melee)
- `Weapon Group` — `SimpleMeleeWeapon`, `MartialMeleeWeapon`, `MartialRangedWeapon`, etc.

**Important base entries**:
- `_BaseWeapon` — Root. UseCosts: ActionPoint:1
- `_Unarmed` — 1d4 Bludgeoning

**Relationship**: Weapons reference spells via `BoostsOnEquipMainHand` (e.g., `UnlockSpell(Zone_Cleave)` links to `Spells/Spell_Zone.txt`).

---

### Stats/Armor.txt — Armor Database
**What**: Every armor piece with AC, type, proficiency, and weight. (1,990 lines)

**Key fields**:
- `ArmorClass` — Base AC number
- `ArmorType` — Padded, Leather, ChainShirt, etc.
- `Armor Class Ability` — `Dexterity` or `None`
- `Ability Modifier Cap` — `2` for medium armor, absent for light, N/A for heavy
- `Proficiency Group` — `LightArmor`, `MediumArmor`, `HeavyArmor`
- `Shield` — `Yes`/`No`
- `Boosts` — Conditional effects, e.g., medium armor stealth disadvantage: `IF(not HasPassive('MediumArmorMaster')):Disadvantage(Skill,Stealth)`

**Standard AC values**: Padded=11, Leather=11, StuddedLeather=12, Hide=12, ChainShirt=13, ScaleMail=14, Breastplate=14, HalfPlate=15, RingMail=14, ChainMail=16, Splint=17, Plate=18, Shield=+2.

---

### Stats/Passive.txt — Passive Abilities
**What**: All passive abilities, racial features, feat effects, and class features. (3,042 lines)

**Entry format**:
```
new entry "GreatWeaponMaster_BonusAttack"
type "PassiveData"
data "DisplayName" "..."
data "Description" "..."
data "Icon" "..."
data "Properties" "Highlighted"
data "Boosts" "UnlockSpell(Target_GreatWeaponMaster_BonusAttack)"
data "StatsFunctorContext" "OnKill;OnCriticalHit"
```

**Key field patterns**:
- `Boosts` — Static effects: `Ability(Strength,1)`, `DarkvisionRangeMin(12)`, `UnlockInterrupt(...)`, `UnlockSpell(...)`, `Resistance(Fire,Resistant)`
- `Properties` — Flags: `IsHidden`, `Highlighted`, `IsToggled`, `ToggledDefaultAddToHotbar`, `ToggleForParty`
- `StatsFunctorContext` — When the passive triggers: `OnKill`, `OnCriticalHit`, etc.
- `ToggleOnFunctors` / `ToggleOffFunctors` — What happens when toggled
- `Conditions` — When the passive is active

**Relationship**: Passives are referenced by:
- `Progressions.lsx` → `PassivesAdded` field
- `Character.txt` → `Passives` field
- `Backgrounds.lsx` → `Passives` field
- `FeatDescriptions.lsx` → `ExactMatch` links feat name to passive name

---

### Stats/Interrupt.txt — Reaction System
**What**: BG3's implementation of D&D reactions — triggered abilities that fire during other entities' actions. (452 lines)

**Key fields**:
- `InterruptContext` — When it can trigger: `OnSpellCast`, `OnPostRoll`, `OnPreDamage`
- `InterruptContextScope` — Who sees it: `Self`, `Nearby`
- `Conditions` — Complex boolean expression determining eligibility
- `Cost` — Resource cost: `ReactionActionPoint:1`, optionally + `SpellSlotsGroup:1:1:3`
- `Properties` — Effects on trigger (for non-roll interrupts)
- `Roll` — Optional roll check (e.g., Counterspell higher level check)
- `Success` / `Failure` — Outcomes of the roll
- `Cooldown` — `OncePerTurn`, `OncePerShortRest`, etc.
- `InterruptDefaultValue` — UI default: `Ask`, `Enabled`

**Important interrupts**:
- `Interrupt_AttackOfOpportunity` (referenced by the `AttackOfOpportunity` passive)
- `Interrupt_Counterspell` — Costs reaction + level 3+ spell slot
- `Interrupt_BardicInspiration_Attack/SavingThrow` — d6/d8/d10 variants
- `Interrupt_CuttingWords` — Subtracts from enemy rolls
- `Interrupt_DefensiveDuelist` — Adds proficiency bonus to AC
- `Interrupt_RecklessAttack` — Grants advantage on melee attacks
- `Interrupt_WardingFlare` — Imposes disadvantage on attacker
- `Interrupt_ShieldMaster` — Reduces AoE damage to zero

---

### Stats/Object.txt — Items & Consumables
**What**: Non-weapon, non-armor items — potions, scrolls, poisons, grenades, camp supplies. (6,490 lines)

---

### Stats/XPData.txt — Experience Table
```
Level 1: 300 XP
Level 2: 600 XP
Level 3: 1800 XP
Level 4: 3800 XP
Level 5: 6500 XP
MaxXPLevel: 5
```
(BG3 caps at level 12 in the full game; this Shared module only defines 1–5.)

---

### Stats/Data.txt — Level Scaling & Lookup Tables
**What**: Miscellaneous level-mapped values used by formulas in spells and passives.

Contains `LevelMapValue()` tables referenced by spell formulas — e.g., `LevelMapValue(BardicInspiration)` returns the die size at each Bard level, `LevelMapValue(WildShapeDamageMedium)` returns scaling bonus damage for wild shape.

---

### Stats/Equipment.txt & ItemTypes.txt
**What**: Starting equipment sets for character creation (referenced by `ClassDescriptions.ClassEquipment`) and item category definitions.

---

## Spell Files

All spell files use the same format but are separated by `SpellType`:

| SpellType | File | Mechanic |
|-----------|------|----------|
| `Target` | Spell_Target.txt | Single-target: melee attacks, touch spells, targeted ranged spells |
| `Projectile` | Spell_Projectile.txt | Ranged with a projectile: arrows, firebolt, magic missile |
| `Shout` | Spell_Shout.txt | Self-targeting: buffs, auras, transformations |
| `Zone` | Spell_Zone.txt | Area effect: cones, spheres with `Shape`, `Range`, `Angle` |
| `Rush` | Spell_Rush.txt | Charge attacks: dash toward target, hit along path |
| `Teleportation` | Spell_Teleportation.txt | Move to a location: Misty Step, Dimension Door |
| `Throw` | Spell_Throw.txt | Throw objects/weapons at targets |
| `ProjectileStrike` | Spell_ProjectileStrike.txt | Multi-hit projectiles (e.g., Scorching Ray) |

### Common Spell Fields

| Field | Description |
|-------|-------------|
| `Level` | Spell level (0 = cantrip) |
| `SpellSchool` | Evocation, Abjuration, Necromancy, etc. |
| `SpellRoll` | Attack roll or saving throw expression |
| `SpellSuccess` | Effects on hit/failed save |
| `SpellFail` | Effects on miss/successful save (often half damage) |
| `SpellProperties` | Effects that always happen regardless of roll |
| `TargetRadius` | Range in BG3 distance units |
| `TargetConditions` | Who can be targeted: `not Self() and not Dead()` |
| `UseCosts` | Resource cost: `ActionPoint:1;SpellSlotsGroup:1:1:1` |
| `DualWieldingUseCosts` | Cost for offhand attack |
| `Cooldown` | `OncePerTurn`, `OncePerShortRest`, `OncePerCombat` |
| `SpellFlags` | Bitflags: `IsAttack;IsMelee;IsHarmful;IsSpell;HasVerbalComponent;HasSomaticComponent;IsConcentration` |
| `DamageType` | Fire, Cold, Radiant, etc. |
| `VerbalIntent` | `Damage`, `Healing`, `Buff`, `Debuff`, `Control`, `Utility` |
| `Requirements` | `Combat`, `!Immobile`, etc. |
| `MemoryCost` | How many preparation slots this costs (`1` for most non-cantrips) |

### Spell Roll Syntax
```
Attack(AttackType.MeleeWeaponAttack)         → d20 + STR/DEX + proficiency
Attack(AttackType.RangedWeaponAttack)        → d20 + DEX + proficiency
Attack(AttackType.MeleeSpellAttack)          → d20 + spellcasting mod + proficiency
Attack(AttackType.RangedSpellAttack)         → d20 + spellcasting mod + proficiency
not SavingThrow(Ability.Dexterity, SourceSpellDC())  → Target must beat caster's spell DC
SavingThrow(Ability.Wisdom, 15)              → Target must beat DC 15
```

### Spell Effect Syntax
```
DealDamage(3d6, Fire, Magical)               → 3d6 fire damage (magical)
DealDamage(MainMeleeWeapon, MainMeleeWeaponDamageType)  → Weapon damage
DealDamage(MainMeleeWeapon+1d8, Radiant)     → Weapon + extra dice
ApplyStatus(BURNING,100,1)                   → Apply BURNING status, 100% chance, 1 turn
ApplyStatus(PRONE,100,2)                     → Prone for 2 turns
RegainHitPoints(2d8+WisdomModifier)          → Heal
Force(4)                                     → Push 4 units away
RemoveStatus(SG_Polymorph)                   → Dispel status group
ExecuteWeaponFunctors(MainHand)              → Trigger weapon on-hit effects
```

### Condition expressions used in targeting/eligibility:
```
Self()                    → Is the caster
Dead()                    → Is dead
Enemy()                   → Is hostile
Ally()                    → Is friendly
Character()               → Is a character (not item)
Item()                    → Is an item
Tagged('UNDEAD')          → Has specific tag
HasPassive('SculptSpells')→ Has specific passive
HasStatus('BURNING')      → Has specific status
InMeleeRange()            → Within melee range
DistanceToTargetGreaterThan(3) → Range check
ClassLevelHigherOrEqualThan(5,'Bard') → Class level check
```

### SpellSet.txt
Pre-built spell loadouts. Useful for seeing which spells are associated with which class archetype:
- `DEBUG_Fighter`: SecondWind, ActionSurge, DisarmingAttack, TripAttack, etc.
- `DEBUG_Common`: Jump, Dash, Shove, Hide, Throw (common to all classes)
- `Demo_Cleric`: Resistance, SacredFlame, CureWounds, ShieldOfFaith, GuidingBolt

---

## Status Files

All status files follow the same format. The `StatusType` field in each entry determines which file it belongs to.

### Status Types

| StatusType | File | Severity | Description |
|-----------|------|----------|-------------|
| `BOOST` | Status_BOOST.txt | Varies | Most statuses live here. Applies `Boosts` while active |
| `INCAPACITATED` | Status_INCAPACITATED.txt | Severe | Cannot act: STUNNED, PARALYZED, SLEEPING |
| `KNOCKED_DOWN` | Status_KNOCKED_DOWN.txt | Moderate | PRONE, various knockdown variants |
| `FEAR` | Status_FEAR.txt | Moderate | FRIGHTENED — cannot approach source |
| `INVISIBLE` | Status_INVISIBLE.txt | Buff | Invisibility variants |
| `SNEAKING` | Status_SNEAKING.txt | Buff | Stealth/hiding |
| `POLYMORPHED` | Status_POLYMORPHED.txt | Transform | Wild Shape forms, Polymorph |
| `DOWNED` | Status_DOWNED.txt | Critical | Death saving throws state |
| `HEAL` | Status_HEAL.txt | Buff | Healing over time |
| `EFFECT` | Status_EFFECT.txt | Visual | Visual-only effects |
| `DEACTIVATED` | Status_DEACTIVATED.txt | Control | Disabled state |

### Common Status Fields

| Field | Description |
|-------|-------------|
| `StatusType` | Category (matches file name) |
| `Boosts` | Active effects while status is applied |
| `StackId` | Stacking group — statuses with same StackId don't stack |
| `TickType` | `StartTurn`, `EndTurn` — when duration decrements |
| `RemoveEvents` | `OnTurn`, etc. — automatic removal triggers |
| `OnApplyFunctors` | Effects that fire when status is first applied |
| `OnRemoveFunctors` | Effects that fire when status expires |
| `StatusPropertyFlags` | UI flags: `DisableOverhead`, `DisableCombatlog`, `ForceOverhead` |
| `StatusGroups` | Category tags: `SG_Condition`, `SG_Blinded`, `SG_Charmed` |

### Important Status Examples

**BOOST statuses** (from Status_BOOST.txt):
- `HASTE` — +2 AC, advantage on DEX saves, extra action
- `BLESSED` — +1d4 to attack rolls and saving throws
- `RAGE` — Bonus melee damage, resistance to physical damage
- `DIPPED_FIRE` — Weapon deals extra 1d4 fire damage
- `DISENGAGE` — Ignore opportunity attacks: `Boosts: IgnoreLeaveAttackRange()`
- `HUNTERS_MARK` — Extra 1d6 damage against marked target
- `ACTION_SURGE` — Grants an extra action point

**INCAPACITATED statuses**:
- `STUNNED` — Can't move or act, auto-fail STR/DEX saves, attacks have advantage
- `PARALYZED` — Like stunned, melee hits are auto-crits
- `SLEEPING` — Like paralyzed but breaks on damage

---

## Relationships & Cross-References

Here is how the files connect to each other:

```
ClassDescriptions.lsx
  │
  ├── ProgressionTableUUID ──────→ Progressions.lsx (TableUUID)
  │                                    │
  │                                    ├── PassivesAdded ────→ Stats/Passive.txt (entry name)
  │                                    ├── Boosts ──────────→ ActionResourceDefinitions.lsx (resource names)
  │                                    │                      Stats/Passive.txt (proficiencies)
  │                                    ├── Selectors ───────→ SpellSet.txt / spell list UUIDs
  │                                    └── SubClasses ──────→ ClassDescriptions.lsx (subclass UUID)
  │
  ├── SpellList UUID ────────────→ (External spell list files, not in Shared)
  └── ParentGuid (subclass) ─────→ ClassDescriptions.lsx (base class UUID)

Stats/Passive.txt
  │
  ├── Boosts: UnlockSpell(X) ───→ Spells/*.txt (spell entry name)
  ├── Boosts: UnlockInterrupt(X)→ Stats/Interrupt.txt (interrupt entry name)
  └── Referenced by:
      ├── Progressions.lsx (PassivesAdded)
      ├── Character.txt (Passives)
      ├── Backgrounds.lsx (Passives)
      └── FeatDescriptions.lsx (ExactMatch = passive name)

Spells/*.txt
  │
  ├── using "Parent_Spell" ─────→ Same or other Spell_*.txt (inheritance)
  ├── SpellSuccess/SpellProperties:
  │   ├── ApplyStatus(X) ───────→ Statuses/*.txt (status entry name)
  │   ├── DealDamage(weapon) ───→ Stats/Weapon.txt (damage formulas)
  │   └── ExecuteWeaponFunctors → Stats/Weapon.txt (on-hit effects)
  ├── UseCosts ─────────────────→ ActionResourceDefinitions.lsx (resource names)
  └── Referenced by:
      ├── Stats/Passive.txt (UnlockSpell)
      ├── Stats/Weapon.txt (BoostsOnEquipMainHand → UnlockSpell)
      └── SpellSet.txt (spell loadouts)

Stats/Weapon.txt
  │
  ├── BoostsOnEquipMainHand ────→ Spells/*.txt (unlocked weapon abilities)
  ├── Proficiency Group ────────→ Progressions.lsx (armor/weapon proficiency boosts)
  └── using "_BaseWeapon" ──────→ Stats/Weapon.txt (inheritance)

Stats/Interrupt.txt
  │
  ├── Conditions ───────────────→ References passives, statuses, spell IDs
  ├── Cost ──────────────────────→ ActionResourceDefinitions.lsx
  ├── Properties/Success ───────→ Spells/*.txt (UseSpell), Statuses/*.txt (ApplyStatus)
  └── Referenced by:
      └── Stats/Passive.txt (UnlockInterrupt)

Statuses/*.txt
  │
  ├── Boosts ───────────────────→ Can unlock spells, grant advantages, etc.
  ├── OnApplyFunctors ──────────→ Can apply other statuses, remove statuses
  ├── StatusGroups ─────────────→ Groups like SG_Condition, SG_Blinded
  └── Referenced by:
      ├── Spells/*.txt (ApplyStatus in SpellSuccess/SpellFail)
      ├── Stats/Passive.txt (Boosts, Conditions)
      └── Character.txt (DifficultyStatuses)

Stats/Modifiers.txt
  │
  └── Defines valid fields for: Armor, Character, Weapon, SpellData,
      PassiveData, StatusData, InterruptData, Object
      (Schema file — does not reference other files)

DifficultyClasses.lsx
  └── Named DC values referenced by spell formulas (e.g., SourceSpellDC())

XPData.txt
  └── Standalone level-up thresholds

Backgrounds.lsx
  └── Passives field ───────────→ Stats/Passive.txt

Races.lsx
  └── Race hierarchy (parent/child UUIDs). Mechanical traits are in Progressions.
```

---

## Key Enumerations

### Ability Indices (used in ClassDescriptions PrimaryAbility/SpellCastingAbility)
```
0 = None
1 = Strength
2 = Dexterity
3 = Constitution
4 = Intelligence
5 = Wisdom
6 = Charisma
```

### Damage Types
`Bludgeoning`, `Piercing`, `Slashing`, `Fire`, `Cold`, `Lightning`, `Thunder`, `Poison`, `Acid`, `Necrotic`, `Radiant`, `Psychic`, `Force`

### Attack Types (used in SpellRoll)
`MeleeWeaponAttack`, `RangedWeaponAttack`, `MeleeOffHandWeaponAttack`, `RangedOffHandWeaponAttack`, `MeleeSpellAttack`, `RangedSpellAttack`, `MeleeUnarmedAttack`

### Weapon Properties
`Finesse`, `Light`, `Heavy`, `Thrown`, `Versatile`, `Reach`, `Melee`, `Ammunition`, `Dippable`, `Twohanded`, `Loading`

### Proficiency Groups
**Weapons**: `SimpleWeapons`, `MartialWeapons`, `Clubs`, `Daggers`, `Longswords`, `Rapiers`, `Shortswords`, `Battleaxes`, `Greataxes`, `Greatswords`, `Handaxes`, `Javelins`, `LightHammers`, `Maces`, `Quarterstaves`, `Sickles`, `Spears`, `Warhammers`, `Flails`, `Glaives`, `Halberds`, `Lances`, `Mauls`, `Morningstars`, `Pikes`, `Scimitars`, `Tridents`, `WarPicks`, `HandCrossbows`, `HeavyCrossbows`, `LightCrossbows`, `Longbows`, `Shortbows`

**Armor**: `LightArmor`, `MediumArmor`, `HeavyArmor`, `Shields`

### Spell Schools
`Abjuration`, `Conjuration`, `Divination`, `Enchantment`, `Evocation`, `Illusion`, `Necromancy`, `Transmutation`

### Spell Flags (common combinations)
```
IsAttack           → Uses attack roll
IsMelee            → Melee range
IsHarmful          → Hostile action
IsSpell            → Uses spell slot (as opposed to weapon attack)
HasVerbalComponent → Blocked by Silence
HasSomaticComponent→ Requires free hand
IsConcentration    → Breaks on damage/new concentration spell
CanDualWield       → Can trigger offhand attack
IsDefaultWeaponAction → Unlocked by weapon proficiency
Temporary          → Disappears after use
```

---

## Practical Examples

### Example 1: How does a Fighter's Action Surge work?

1. **ClassDescriptions.lsx** → Fighter class, `ProgressionTableUUID` = `ba4e707e-...`
2. **Progressions.lsx** → Fighter level 2: `PassivesAdded: ActionSurge` (this is a passive name, but actually the spell is granted via progression's `AddSpells`)
3. **Spells/Spell_Shout.txt** → `Shout_ActionSurge`: SpellProperties = `ApplyStatus(ACTION_SURGE,100,1)`, UseCosts = (none listed, it's Cooldown = OncePerShortRest)
4. **Statuses/Status_BOOST.txt** → `ACTION_SURGE`: Boosts = `ActionResource(ActionPoint,1,0)` — grants 1 extra action point

### Example 2: How does a Battleaxe weapon attack work?

1. **Stats/Weapon.txt** → `WPN_Battleaxe`: Damage=1d8, Type=Slashing, `BoostsOnEquipMainHand: UnlockSpell(Zone_Cleave);UnlockSpell(Target_Slash_New);UnlockSpell(Target_CripplingStrike)`
2. **Spells/Spell_Target.txt** → `Target_MainHandAttack` (the default melee attack): `SpellRoll: Attack(AttackType.MeleeWeaponAttack)`, `SpellSuccess: DealDamage(MainMeleeWeapon, MainMeleeWeaponDamageType)`, UseCosts = `ActionPoint:1`
3. **Spells/Spell_Zone.txt** → `Zone_Cleave`: Shape=Cone, Angle=120, MaxTargets=3, deals half weapon damage to each

### Example 3: How do reactions (Opportunity Attacks) work?

1. **Stats/Passive.txt** → `AttackOfOpportunity`: `Boosts: UnlockInterrupt(Interrupt_AttackOfOpportunity)`
2. **Stats/Interrupt.txt** → `Interrupt_AttackOfOpportunity` (defined in SharedDev, not Shared): InterruptContext=OnLeaveAttackRange, Cost=ReactionActionPoint:1
3. **Character.txt** → `_Base`: `Passives: AttackOfOpportunity` — all characters get this by default

### Example 4: How does a saving throw spell (Burning Hands) work?

1. **Spells/Spell_Zone.txt** → `Zone_BurningHands`: Level=1, SpellSchool=Evocation
2. `SpellRoll: not SavingThrow(Ability.Dexterity, SourceSpellDC())` — targets must beat caster's spell DC with DEX save
3. `SpellSuccess: DealDamage(3d6, Fire, Magical)` — on failed save
4. `SpellFail: DealDamage(3d6/2, Fire, Magical)` — on successful save (half damage)
5. `UseCosts: ActionPoint:1;SpellSlotsGroup:1:1:1` — costs 1 action + 1 spell slot (minimum level 1)
6. `SpellFlags: HasSomaticComponent;HasVerbalComponent;IsSpell;IsHarmful;CanAreaDamageEvade`
7. Shape=Cone, Range=5, Angle=60

---

## Notes for Developers

1. **Inheritance is everywhere.** Always resolve `using "ParentName"` chains when reading TXT entries. A weapon like `WPN_Battleaxe` inherits all defaults from `_BaseWeapon`.

2. **GUIDs are the foreign keys.** LSX files reference each other via `guid` attributes. When you see a UUID in one file, grep other files for it to find the relationship.

3. **TranslatedString handles** (like `h87dd56ecg2a9ag45b0ga933g10795072c609`) are localization keys — they resolve to display text in the game's string tables (not included here). For mechanical purposes, use the `Name`/`ExactMatch` fields, not display names.

4. **BG3 distance units** ≈ 0.3 meters. A `TargetRadius` of 1800 = approximately 18m. Melee `WeaponRange` of 150 = 1.5m standard melee reach.

5. **The Shared module covers levels 1–5.** Higher-level content is in `SharedDev` (separate pak). The data here is complete for levels 1–5 and contains all the foundational systems.

6. **Status groups** (prefixed `SG_`) are used for "remove all X" effects. Example: `RemoveStatus(SG_Polymorph)` removes any polymorph status, regardless of specific form.

7. **Functors** are BG3's scripting primitives. Common patterns:
   - `IF(condition):Effect()` — Conditional effect
   - `GROUND:Effect()` — Only if target is on ground
   - `TARGET:Effect()` — Apply to target
   - `SELF:Effect()` — Apply to caster
   - `SWAP:Effect()` — Apply to opposite party in the interaction

8. **Cosmetic data has been stripped.** Each cleaned file has a `# COSMETIC DATA REMOVED` or `<!-- COSMETIC DATA REMOVED -->` comment header listing exactly what was removed and the count. If you need visual data for any reason, refer back to the originals in `Reference Code DO NOT CHANGE/Shared/`.

### Stripped Visual Fields (TXT files)
The following `data` fields were removed from all `.txt` stat/spell/status files:
- **Icons**: `Icon`
- **Animations**: `SpellAnimation`, `DualWieldingSpellAnimation`, `HitAnimationType`, `SpellAnimationIntentType`, `PrepareAnimation`, `CastAnimation`, `StatusAnimation`, `AnimationEnd`, `InterruptAnimation`, `StillAnimationType`, `StillAnimationPriority`
- **VFX**: `PrepareEffect`, `CastEffect`, `TargetEffect`, `HitEffect`, `PreviewEffect`, `BeamEffect`, `ApplyEffect`, `FallingHitEffect`, `FallingLandEffect`, `StatusEffect`, `StatusEffectOverride`, `StatusEffectOverrideForItems`, `CastEffectTextEvent`, `PreviewCursor`
- **Audio**: `PrepareSound`, `CastSound`, `CastSoundStop`, `SoundStart`, `SoundStop`, `SoundLoop`, `SoundVocalStart`
- **Models/Materials**: `RootTemplate`, `Projectile` (visual template GUIDs), `StatusMaterial`, `Material`
- **Managed VFX**: `ManagedStatusEffectType`, `ManagedStatusEffectGroup`
- **UI**: `CycleConditions`

### Stripped Cosmetic Data (LSX files)
- **Races.lsx**: ~24,600 lines removed — `SkinColors`, `EyeColors`, `HairColors`, `HairHighlightColors`, `LipsMakeupColors`, `MakeupColors`, `TattooColors` nodes; `ExcludedGods`, `RaceEquipment`, `RaceSoundSwitch` attributes
- **ClassDescriptions.lsx**: ~88 attributes removed — `CharacterCreationPose`, `SoundClassType`, `ClassHotbarColumns`, `CommonHotbarColumns`, `ItemsHotbarColumns`, `ValidSomaticEquipmentList`
