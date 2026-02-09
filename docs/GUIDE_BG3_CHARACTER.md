# Race and Class in Baldur’s Gate 3: A Data‑Driven Implementation Guide for Game Developers

## Scope, version baseline, and where BG3 diverges from tabletop

This guide models the **player-facing Race + Class build system** in *Baldur’s Gate 3* as of **Patch 8 (“The Final Patch”)**, released **April 15, 2025**, which introduced **12 new subclasses (one per class)** and was stated to be the last major content patch (hotfixes continue). citeturn16view1turn16view0turn13search2

The scope is:

- **Included:** races + subraces (and their traits), classes + subclasses (and their level progression through the game’s level cap), ability scores and modifiers, proficiency and expertise, skills, saving throws, initiative, multiclassing rules, and the complete feat list with prerequisites and mechanical effects. citeturn9view0turn2view0turn3view0turn20view3  
- **Excluded by request:** the **full spell catalog** and detailed spell mechanics (too large). However, because BG3 attaches *spell grants* to races, classes, subclasses, and feats, this guide defines **how to reference spells as data IDs** and includes **slot/known/prepared counts** where applicable, without enumerating or re-explaining all spells. citeturn16view1turn6view1turn20view2turn31view1  

Implementation note: BG3 is *D&D‑like* but not identical. Several “house rules” materially affect Race/Class/Feat value and therefore must be reflected in any faithful implementation, notably:

- **Initiative uses a d4, not a d20**, and is **not treated as a Dexterity ability check**; adjacent allied initiatives may effectively “share” turns, allowing interleaving actions between characters. citeturn18view0turn18view1  
- **Tool, vehicle, and language proficiencies are not implemented**; BG3 focuses on **weapon, armor, skill, and musical instrument proficiencies**. citeturn18view1turn3view0  
- **Ability checks**: natural 1 always fails and natural 20 always succeeds (even if modifiers would change the outcome). Dialogue saving throws have similar crit behavior; most regular combat saving throws do not. citeturn18view1turn17search14  

## Core stat model: abilities, skills, proficiencies, saving throws, and initiative

### Ability scores and modifiers

BG3 uses the six standard abilities: **Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma**. citeturn2view0turn6view1  

An **ability modifier** is derived exactly as:

- `mod = floor((score - 10) / 2)` citeturn2view0  

Ability scores are largely built via **Point Buy** at character creation (and when respeccing):

- **27 points** total
- Base value **8** in each ability
- Increases cost **1 point per +1** up to 13, but **14 and 15 cost 2 points each**
- Point buy cannot exceed **15** in any ability
- BG3 then applies two flexible bonuses: **+2 to one ability and +1 to another**, raising the maximum starting score to **17** citeturn6view0turn6view1turn2view0  

### Proficiency bonus and expertise

A character’s **proficiency bonus** is based on **total character level** (not class level). In BG3’s level‑12 cap, it progresses:

- Levels **1–4:** +2  
- Levels **5–8:** +3  
- Levels **9–12:** +4 citeturn3view0turn15view0turn22view0turn31view0  

**Proficiency applies** to: weapons, armor, skills, instrument proficiency, and saving throw proficiencies granted by class start (and some feats). citeturn3view0turn18view1  

Key stacking rule: **proficiencies do not stack**—if multiple sources grant the same proficiency, you still add proficiency bonus only once. To go beyond proficiency, BG3 uses **Expertise**, which increases the proficiency contribution for a skill beyond normal proficiency. citeturn6view1turn2view0  

### Skills list and their governing abilities

BG3 implements the standard **18 skills**, mapped to abilities as follows (no Constitution skills): citeturn6view1turn2view0  

| Ability | Skills |
|---|---|
| Strength | Athletics |
| Dexterity | Acrobatics, Sleight of Hand, Stealth |
| Constitution | *(none)* |
| Intelligence | Arcana, History, Investigation, Nature, Religion |
| Wisdom | Animal Handling, Insight, Medicine, Perception, Survival |
| Charisma | Deception, Intimidation, Performance, Persuasion |

**Skill check formula (typical):**  
`d20 + ability_mod + (proficiency_bonus if proficient) + other_mods` citeturn2view0turn6view1turn17search14  

Where **other_mods** are effects from feats, class features, items, conditions, and situational advantage/disadvantage. citeturn17search14turn18view1turn20view1  

### Saving throws

BG3 has a saving throw for each ability (STR/DEX/CON/INT/WIS/CHA). A saving throw roll is generally:

`d20 + ability_mod + (proficiency_bonus if proficient) + other_mods` citeturn2view0turn3view0turn18view1  

Important BG3‑specific behavior to implement:

- **Multiclassing never grants additional saving throw proficiencies**; only a character’s first class determines baseline save proficiencies, unless a feat later adds one. citeturn9view0turn20view0  
- Dialogue saving throws treat natural 1/20 as automatic fail/success; most “regular” combat saving throws do not. citeturn18view1  

### Initiative and “shared turns”

BG3 initiative is:

- `initiative = d4 + Dexterity modifier` citeturn18view0turn18view1  

Tie-breaking and shared sequencing:

- If two participants roll the same initiative, the one with higher Dexterity score goes first. citeturn18view0  
- Multiple characters controlled by the same player with the same initiative effectively act “together,” letting the player interleave movement/actions in a shared block; BG3 also supports shared initiative when allied turns are adjacent (a key systemic difference from tabletop). citeturn18view0turn18view1  

## Data architecture blueprint: how to implement BG3’s Race/Class/Feat stack

A faithful implementation is easiest if you treat Race, Class, Subclass, Background, and Feats as **data-defined “feature sources”** that grant a standardized set of effects:

- **Passive features** (always on or conditionally active)  
- **Actions / Bonus Actions / Reactions** (combat resources)  
- **Proficiencies** (weapons/armor/skills/saving throws/instruments)  
- **Scaling resources** (e.g., Rage Charges, Ki Points, Sorcery Points)  
- **Spell grants** (by ID only; spell logic external) citeturn16view1turn15view0turn26view0turn30view1  

Patch 8’s official toolkit notes are a strong hint at the internal partitioning BG3 expects: it explicitly calls out support for **Action Resources, Race passives, Racial spells at LevelUp, and Feat passives** as distinct modifiable categories. citeturn16view1  

### Recommended core records

Below is an **implementation-oriented schema** (format-agnostic). The goal is: if you can apply and remove “feature sources” deterministically, you can support respec and multiclass cleanly. citeturn9view0turn6view1turn20view3  

```text
AbilityScore: { STR, DEX, CON, INT, WIS, CHA }

Character:
  id
  level_total (1..12)
  race_id
  subrace_id (nullable)
  background_id
  classes: list of { class_id, class_level (1..12) }
  ability_scores: map AbilityScore -> int
  proficiencies:
    skills: set<SkillId>
    saving_throws: set<AbilityScore>
    weapons: set<WeaponCategoryId>
    armor: set<ArmorCategoryId>
    shields: bool
    instruments: bool
  expertise: set<SkillId>  // treat as "double proficiency" for skill math
  features: set<FeatureId> // includes passives and granted actions
  resources: map<ResourceId -> ResourceState>
  spell_grants: set<SpellGrant> // references SpellId; spell definitions out-of-scope
  tags: set<TagId> // for dialogue gating by race/class, if you implement narrative hooks

RaceDefinition:
  race_id
  display_name
  base_speed_m
  size (Small/Medium)
  darkvision_m (0, 12, 24)
  prof_grants (skills/weapons/armor/shields/instruments)
  resistances: set<DamageType>
  feature_grants_by_level: map<character_level -> list<FeatureGrant>>
  spell_grants_by_level: map<character_level -> list<SpellGrant>>
  notes: "racial spells typically 1/Long Rest unless noted"

ClassDefinition:
  class_id
  hit_points:
    level1_base
    per_level_base
  saving_throw_proficiencies: set<AbilityScore>
  starting_proficiencies (weapons/armor/shields)
  skill_choices:
    choose_n
    from_list
  subclass_unlock_level (1/2/3)
  subclasses: list<subclass_id>
  level_features: map<class_level -> list<FeatureGrant>>
  resource_progression: map<class_level -> ResourceDelta> // e.g., Ki points +1
  spellcasting_progression (if any):
    casting_type (prepared/known/pact)
    slots_by_level (counts only)
    prepared_formula (if any)

FeatDefinition:
  feat_id
  prerequisites:
    ability_minimums
    required_proficiencies
    other (weapon tags, etc.)
  grants:
    ability_increases
    proficiencies
    expertise
    features/actions
    spell_grants (by SpellListRef, without enumerating spells)
```

### Deterministic build resolution order

A robust way to compute the final character sheet:

1. Start from base character (level, base ability scores from point buy, empty proficiencies/resources/features). citeturn6view0turn6view1  
2. Apply **race/subrace** grants (including passives and racial spells by level). citeturn4view0turn6view1turn14view5  
3. Apply **background** grants (BG3 backgrounds always provide two skill proficiencies; no tool proficiencies). citeturn6view1turn18view1  
4. Apply **class level progression** (including subclass grants at unlock level, then ongoing subclass features at later levels). citeturn16view1turn15view0turn23view0turn31view1  
5. At each ASI/feat level, validate feat prerequisites, then apply feat grants. citeturn20view3turn19view0turn20view2  
6. Compute derived stats:
   - proficiency bonus from total level citeturn3view0turn15view0  
   - skill modifiers / save modifiers from ability mods + proficiency/expertise citeturn2view0turn6view1  
   - initiative from d4 + DEX mod (runtime roll) citeturn18view0  

## Race system: full playable race list and implementable trait payloads

Race represents lineage and “innate abilities,” and in BG3 it also gates some **permanent choices** (e.g., racial cantrip selection) that cannot be changed after character creation. citeturn6view1  

image_group{"layout":"carousel","aspect_ratio":"16:9","query":["Baldur's Gate 3 character creation race selection screen","Baldur's Gate 3 character creation class selection screen"],"num_per_query":1}

### Global race-linked character-creation rules

- BG3 uses point buy plus flexible **+2 and +1 bonuses** applied at the end of ability score assignment (maximum starting score 17). citeturn6view0turn6view1  
- BG3 documents that, unlike fixed racial ASIs in tabletop, it awards **freely allocated bonuses** (presented as part of race/creation rules). citeturn4view0turn6view1turn6view0  
- **Racial spells** are generally **1/Long Rest** unless explicitly noted otherwise. citeturn4view0turn14view2  
- **Darkvision ranges** are reduced relative to tabletop: **Darkvision 12m (40ft)** and **Superior Darkvision 24m (80ft)**. citeturn18view1turn14view5  

### Complete race and subrace roster

BG3’s character creation includes **11 playable races**: Human, Elf, Drow, Half‑Elf, Half‑Orc, Halfling, Dwarf, Gnome, Tiefling, Githyanki, Dragonborn. citeturn6view1turn4view0turn17search21  

Below is an **implementation payload** summary per race: what data you should store and what it does mechanically. (Spell references are named for clarity but must be implemented as external SpellIds.) citeturn4view0turn14view5turn14view3  

**Human (Standard)**  
- Base speed: 9m (30ft). citeturn4view0turn6view1  
- Racial features and proficiencies are defined in the race table and should be applied as **weapon/armor proficiencies + a flexible skill proficiency** payload. citeturn4view0turn6view1  

**Elf** *(subraces: High Elf, Wood Elf)*  
- Expect: Darkvision 12m; Fey-themed resistances/advantages; weapon training; and subrace-specific feature hooks (notably “High Elf cantrip choice”). citeturn4view0turn6view1turn18view1  

**Drow** *(subraces: Lolth‑Sworn, Seldarine)*  
- Mechanically identical in the race table; subrace mainly affects narrative/deity constraints (e.g., Lolth‑Sworn clerics). citeturn4view0turn23view0  
- Typically includes **Superior Darkvision (24m)** and **Drow Magic** spell grants by level (treat as SpellGrant entries). citeturn18view1turn4view0  

**Half‑Elf** *(subraces: High Half‑Elf, Wood Half‑Elf, Drow Half‑Elf)*  
- Hybrid payload: Darkvision + elven passives + “human-like” versatility for skill proficiencies, with subrace-specific spell/cantrip hooks. citeturn4view0turn6view1  

**Half‑Orc**  
- Martial durability + crit synergy features are provided as passives in the race’s feature list (implement as on‑trigger effects). citeturn4view0turn6view1  

**Halfling** *(subraces: Lightfoot, Strongheart)*  
- Core: “Halfling Luck” style die manipulation and bravery-style fear resilience are represented as passive features. citeturn4view0turn18view1  

**Dwarf** *(subraces: Gold Dwarf, Shield Dwarf, Duergar)*  
- Darkvision 12m baseline; subraces add toughness, armor training, and/or duergar-specific resilience and spell-like abilities. citeturn4view0turn14view4turn18view1  
- Explicitly shown: **Dwarven Toughness** increases maximum HP by **+1 per level** (store as scaling HP modifier). citeturn14view4  
- Shield Dwarf shows armor training hooks; **Duergar** includes **Duergar Resilience** and additional trait payloads. citeturn14view4  

**Gnome** *(subraces: Forest, Rock, Deep Gnome)*  
- Core: gnome defensive passives (cunning-style saving throw advantages) and subrace utility. citeturn4view0turn18view1  

**Tiefling** *(subraces: Asmodeus, Mephistopheles, Zariel)*  
- Core: **Fire resistance**, Darkvision 12m, and subrace-specific **Tiefling Magic** spells at character levels **1, 3, 5**. citeturn14view5turn14view0  
  - **Asmodeus Tiefling:** Produce Flame (L1), Hellish Rebuke (L3), Darkness (L5). citeturn14view5  
  - **Mephistopheles Tiefling:** Mage Hand (L1), Burning Hands (L3), Flame Blade (L5). citeturn14view0  
  - **Zariel Tiefling:** Thaumaturgy (L1), Searing Smite (L3), Branding Smite (L5). citeturn14view0  

**Githyanki**  
- Base speed: 9m. citeturn14view2  
- Weapon/armor proficiencies (shortswords, longswords, greatswords; light+medium armor). citeturn14view2  
- **Astral Knowledge:** each long rest, gain proficiency in **all skills of a chosen ability** (implement as a per‑rest selectable proficiency bundle). citeturn14view2  
- **Githyanki Psionics:** Mage Hand (L1), Enhance Leap (L3), Misty Step (L5) (SpellGrant references). citeturn14view3turn14view2  

**Dragonborn** *(10 ancestries: Black, Blue, Brass, Bronze, Copper, Gold, Green, Red, Silver, White)*  
- Base speed: 9m (30ft). citeturn14view3  
- **Draconic Ancestry** determines:
  - a **damage resistance**, and  
  - a **breath weapon action** with **AoE 5m / 17ft** (line or cone depending on ancestry), dealing the ancestry’s damage type. citeturn14view3turn14view6turn14view7  

From the race table excerpt, examples include:  
- **Black Dragonborn:** Acid resistance; Acid Breath, 5m line. citeturn14view3turn14view7  
- **Brass Dragonborn:** Fire resistance; Fire Breath (Line), 5m line. citeturn14view6turn14view3  
- **Gold/Red/Green/Silver/White** variants attach the corresponding resistance and cone breath entries. citeturn14view3  

## Class system: full class list, subclass roster, and level progression payload

### Structural rules: classes, subclasses, level cap, and respec

- BG3 has **12 playable classes** and a **maximum character level of 12**. citeturn9view0turn6view1turn17search21  
- A class provides most combat and non-combat capabilities; classes also gate class-based dialogue options. citeturn9view0turn16view1  
- Patch 8 added **one new subclass per class** (12 total): Path of the Giant Barbarian, College of Glamour Bard, Death Domain Cleric, Circle of the Stars Druid, Arcane Archer Fighter, Way of the Drunken Master Monk, Oath of the Crown Paladin, Swarmkeeper Ranger, Swashbuckler Rogue, Shadow Magic Sorcerer, Hexblade Warlock, Bladesinging Wizard. citeturn16view1turn16view0  
- Subclass unlock levels (Patch 8 summary table):
  - **Level 1:** Cleric, Sorcerer, Paladin, Warlock  
  - **Level 2:** Druid, Wizard  
  - **Level 3:** Barbarian, Bard, Fighter, Monk, Ranger, Rogue citeturn16view1turn9view0  

### Multiclassing rules that affect implementation

When leveling up, a character may take a level in a new class (multiclassing), subject to BG3-specific rules:

- Multiclassing is disabled on **Explorer** difficulty. citeturn9view0  
- Proficiency bonus always scales by **total character level**. citeturn9view0turn3view0turn15view0  
- Multiclassing **never grants saving throw proficiencies**. citeturn9view0turn20view3  
- A multiclassed character does **not** gain all proficiencies of the new class; BG3 provides a specific “multiclass proficiencies” table by class gained (e.g., Fighter grants light+medium armor, shields, simple+martial weapons; Rogue grants light armor and one skill; etc.). citeturn9view0  
- Spell slot progression for multiclass characters depends on **effective spellcaster level**, and “item spellcasting” uses the spellcasting modifier from the **most recent new class** taken (spells themselves are out-of-scope here, but the rule affects build math). citeturn9view0turn18view1  

### Complete class list with subclass rosters and progression tables

The sections below are organized as **data payloads you can store directly**: HP formula, subclass roster, and the per-level “feature/event” table that determines what to grant at level-up. citeturn15view0turn22view0turn23view0turn31view1  

**Barbarian** citeturn15view0  
- HP: level 1 = 12 + CON mod; per level = 7 + CON mod. citeturn15view0  
- Subclasses: Berserker, Giant (Path of the Giants), Wild Magic, Wildheart. citeturn15view0turn16view0  
- Progression (class-level features):
  - 1: Rage (2 charges; +2 rage damage), Unarmoured Defence  
  - 2: Reckless Attack, Danger Sense  
  - 3: Subclass choice; Rage charges 3  
  - 4: Feat  
  - 5: Extra Attack; Fast Movement; proficiency bonus becomes +3 at total level 5  
  - 6: Subclass feature; Rage charges 4  
  - 7: Feral Instinct  
  - 8: Feat  
  - 9: Brutal Critical; rage damage +3; proficiency bonus +4 at total level 9  
  - 10: Subclass feature  
  - 11: Relentless Rage  
  - 12: Feat; Rage charges 5 citeturn15view0  

**Bard** citeturn22view0  
- HP: level 1 = 8 + CON mod; per level = 5 + CON mod. citeturn22view0  
- Subclasses: College of Glamour, College of Lore, College of Swords, College of Valour. citeturn22view0turn16view1  
- Progression core (counts shown; spells referenced only):
  - 1: Spellcasting; Bardic Inspiration (3 uses, die d6)  
  - 2: Song of Rest; Jack of All Trades  
  - 3: Subclass choice; Expertise (2 skills)  
  - 4: Feat; spell slot progression continues  
  - 5: Improved Bardic Inspiration (d8); Font of Inspiration  
  - 6: Countercharm; Subclass feature  
  - 8: Feat  
  - 10: Improved Bardic Inspiration (d10); Expertise; Magical Secrets  
  - 12: Feat citeturn22view0turn18view0  

**Cleric** citeturn23view0  
- HP: level 1 = 8 + CON mod; per level = 5 + CON mod. citeturn23view0  
- Subclasses (Domains): Death, Knowledge, Life, Light, Nature, Tempest, Trickery, War. citeturn23view0turn16view1  
- Progression highlights:
  - 1: Spellcasting; Domain choice  
  - 2: Turn Undead; Channel Divinity (1 charge)  
  - 4: Feat  
  - 5: Destroy Undead  
  - 6: Domain feature; Channel Divinity charges become 2  
  - 8: Feat; Domain feature  
  - 10: Divine Intervention  
  - 12: Feat citeturn23view0  

**Druid** citeturn24view0  
- HP: level 1 = 8 + CON mod; per level = 5 + CON mod. citeturn24view0  
- Subclasses: Circle of the Land, Circle of the Moon, Circle of the Spores, Circle of the Stars. citeturn24view0turn16view0  
- Progression highlights:
  - 1: Spellcasting  
  - 2: Wild Shape; Subclass choice  
  - 4: Feat; Wild Shape Improvement  
  - 5: Wild Strike  
  - 6: Subclass feature  
  - 8: Feat; Wild Shape Improvement  
  - 10: Subclass feature; Improved Wild Strike  
  - 12: Feat; Wild Shape Improvement citeturn24view0  

**Fighter** citeturn25view0  
- HP: level 1 = 10 + CON mod; per level = 6 + CON mod. citeturn25view0  
- Subclasses: Arcane Archer, Battle Master, Champion, Eldritch Knight. citeturn25view0turn16view1  
- Progression:
  - 1: Second Wind; Fighting Style  
  - 2: Action Surge  
  - 3: Subclass choice  
  - 4: Feat  
  - 5: Extra Attack  
  - 6: **Feat (extra fighter feat level)**  
  - 7: Subclass feature  
  - 8: Feat  
  - 9: Indomitable  
  - 10: Subclass feature  
  - 11: Improved Extra Attack  
  - 12: Feat citeturn25view0turn20view3  

**Monk** citeturn26view0  
- HP: level 1 = 8 + CON mod; per level = 5 + CON mod. citeturn26view0  
- Subclasses: Way of the Drunken Master, Way of the Four Elements, Way of the Open Hand, Way of Shadow. citeturn26view0turn16view0  
- Progression (resource-heavy):
  - 1: Unarmoured Defence; Martial Arts package; Flurry of Blows; Ki Points 2; martial arts die 1d4  
  - 2: Unarmoured Movement; Patient Defence; Step of the Wind; Ki 3; movement +3m  
  - 3: Subclass choice; Deflect Missiles; Ki 4; martial arts die 1d6  
  - 4: Feat; Slow Fall; Ki 5  
  - 5: Extra Attack; Stunning Strike; Ki 6  
  - 6: Ki-Empowered Strikes; Subclass feature; movement +4.5m; Ki 7  
  - 7: Evasion; Stillness of Mind; Ki 8  
  - 8: Feat; Ki 9  
  - 9: Advanced Unarmoured Movement; Subclass feature; martial arts die 1d8; Ki 10  
  - 10: Purity of Body; movement +6m; Ki 11  
  - 11: Subclass feature; Ki 12  
  - 12: Feat; Ki 13 citeturn26view0  

**Paladin** citeturn27view0turn16view1  
- HP: level 1 = 10 + CON mod; per level = 6 + CON mod. citeturn27view0  
- Subclasses (Oaths): Oath of Devotion, Oath of the Ancients, Oath of the Crown, Oath of Vengeance, plus Oathbreaker (unlock via oathbreaking). citeturn27view0turn16view1  
- Progression highlights:
  - 1: Divine Sense; Lay on Hands (3 charges); Channel Oath (1 charge); choose oath  
  - 2: Fighting Style; Spellcasting (half-caster); Divine Smite  
  - 3: Divine Health; Subclass feature  
  - 4: Feat; lay on hands charges 4  
  - 5: Extra Attack  
  - 6: Aura of Protection; Channel Oath remains a per-short-rest resource  
  - 10: Aura of Courage  
  - 11: Improved Divine Smite  
  - 12: Feat citeturn27view0  

**Ranger** citeturn28view0  
- HP: level 1 = 10 + CON mod; per level = 6 + CON mod. citeturn28view0  
- Subclasses: Beast Master, Gloom Stalker, Hunter, Swarmkeeper. citeturn28view0turn16view0  
- Progression highlights:
  - 1: Favoured Enemy choice; Natural Explorer choice  
  - 2: Fighting Style; Spellcasting (half-caster)  
  - 3: Subclass choice  
  - 4: Feat  
  - 5: Extra Attack; Subclass feature  
  - 6: additional Favoured Enemy + Natural Explorer selections  
  - 8: Feat; Land’s Stride  
  - 10: additional Favoured Enemy + Natural Explorer; Hide in Plain Sight  
  - 11: Subclass feature  
  - 12: Feat citeturn28view0  

**Rogue** citeturn31view0  
- HP: level 1 = 8 + CON mod; per level = 5 + CON mod. citeturn31view0  
- Subclasses: Arcane Trickster, Assassin, Swashbuckler, Thief. citeturn31view0turn16view0  
- Progression (plus Sneak Attack scaling):
  - 1: Expertise; Sneak Attack (melee + ranged), damage 1d6  
  - 2: Cunning Action (Dash/Disengage/Hide)  
  - 3: Subclass choice; Sneak Attack 2d6  
  - 4: Feat  
  - 5: Uncanny Dodge; Sneak Attack 3d6  
  - 6: Expertise (additional)  
  - 7: Evasion; Sneak Attack 4d6  
  - 8: Feat  
  - 9: Subclass feature; Sneak Attack 5d6  
  - 10: **Feat (extra rogue feat level)**  
  - 11: Reliable Talent; Sneak Attack 6d6  
  - 12: Feat citeturn31view0turn20view3  

**Sorcerer** citeturn30view1turn16view1  
- Subclasses: Draconic Bloodline, Shadow Magic, Storm Sorcery, Wild Magic. citeturn30view1turn16view0  
- Progression highlights:
  - 1: Spellcasting; choose subclass  
  - 2: Metamagic; Create Spell Slot; Create Sorcery Points; Sorcery Points 2  
  - 3: Metamagic; Sorcery Points 3  
  - 4: Feat; Sorcery Points 4  
  - 6: Subclass feature; Sorcery Points 6  
  - 8: Feat; Sorcery Points 8  
  - 10: Metamagic; Sorcery Points 10  
  - 11: Subclass feature; Sorcery Points 11  
  - 12: Feat; Sorcery Points 12 citeturn30view1  

**Warlock** citeturn31view1turn16view0  
- HP: level 1 = 8 + CON mod; per level = 5 + CON mod. citeturn31view1  
- Subclasses: The Archfey, The Fiend, The Great Old One, The Hexblade. citeturn31view1turn16view1  
- Progression highlights:
  - 1: Pact Magic; choose patron  
  - 2: Eldritch Invocations (+2); invocations known 2  
  - 3: Pact Boon; invocations known 4  
  - 4: Feat  
  - 5: Deepened Pact; invocations +1  
  - 6: Subclass feature  
  - 7: Invocation +1  
  - 8: Feat  
  - 9: Invocation +1  
  - 10: Subclass feature  
  - 11: Mystic Arcanum (6th level spell)  
  - 12: Feat; Invocation +1 (invocations known 6) citeturn31view1  

**Wizard** citeturn31view2turn16view0  
- HP: level 1 = 6 + CON mod; per level = 4 + CON mod. citeturn31view2  
- Subclasses (Schools): Abjuration, Bladesinging, Conjuration, Divination, Enchantment, Evocation, Illusion, Necromancy, Transmutation. citeturn29view3turn16view1  
- Progression highlights:
  - 1: Spellcasting; Arcane Recovery (charges); Transcribing scrolls  
  - 2: choose school  
  - 4: Feat  
  - 6: subclass feature  
  - 8: Feat  
  - 10: subclass feature  
  - 12: Feat citeturn31view2  

## Feat system: selection rules and the complete feat catalog

### When feats are chosen

In BG3:

- **All classes** choose a feat at class levels **4, 8, and 12**.  
- **Fighter** gains an additional feat at class level **6**.  
- **Rogue** gains an additional feat at class level **10**. citeturn20view3turn25view0turn31view0  

Feats are represented as feature bundles. Some are pure stat/proficiency changes; others add new **actions**, **reactions**, toggleable passives, or spell-grant hooks. citeturn20view1turn19view0turn20view2  

### Feat prerequisites model

BG3 feat prerequisites (when present) are mostly:

- **Armor proficiency gates** (e.g., medium armor required before heavy armor feats) citeturn19view0turn19view2  
- Requirement to already have a given proficiency (e.g., Medium Armour Master requires medium armor proficiency) citeturn19view1  

Implement prerequisites as a boolean expression over the character’s current proficiency set, ability minimums, and possibly tags (weapon categories, shield equipped, etc.).

### Complete feat list with implementable effects

All feats below are in BG3’s feat list. citeturn20view3turn21view0  

**Ability Improvement**  
Increase one ability score by **+2** or two ability scores by **+1**, max **20**. citeturn20view3  

**Actor**  
+1 CHA; gain **Expertise** in Deception and Performance (and proficiency if not already proficient). citeturn20view3  

**Alert**  
+5 initiative; cannot be Surprised. citeturn20view3turn18view0  

**Athlete**  
+1 STR or +1 DEX; standing from Prone uses significantly less movement; Jump distance +50%. citeturn20view3  

**Charger**  
Adds charge actions: Charge Weapon Attack and Charge Shove without provoking opportunity attacks (shove distance scales with STR and target weight). citeturn20view3  

**Crossbow Expert**  
Closing range doesn’t impose disadvantage on ranged attack rolls with crossbows; also enables bonus-action attack behavior with hand crossbows (implement per feat definition). citeturn1view0  

**Defensive Duellist**  
Reaction to increase AC against a melee attack when wielding a finesse weapon (implement as conditional reaction feature). citeturn1view0  

**Dual Wielder**  
+1 AC while dual-wielding; can dual-wield non-light one-handed weapons (implement equipment rule override). citeturn1view0  

**Dungeon Delver**  
Advantage on Perception/Investigation checks to detect traps; advantage on saving throws vs traps; resistance to trap damage. citeturn1view0  

**Durable**  
+1 CON; when you receive healing, minimum healing becomes 2 + CON mod (implement floor on healing instance). citeturn1view0  

**Elemental Adept**  
Choose a damage type; spells ignore resistance to that type; treat damage dice 1s as 2s for that type (“you cannot roll a 1” for damage dice of the chosen type). citeturn1view0turn19view0  

**Great Weapon Master**  
Bonus-action attack on crit or kill; toggle “All In” for -5 to hit with proficient two-handed/versatile (two-handed) melee weapons, +10 damage. citeturn19view0turn20view3  

**Heavily Armoured** *(requires Medium Armour Proficiency)*  
+1 STR; gain Heavy Armour proficiency. citeturn19view0  

**Heavy Armour Master** *(requires Heavy Armour Proficiency)*  
+1 STR; reduce incoming damage from **non-magical** bludgeoning/piercing/slashing attacks by 3 while wearing heavy armor. citeturn19view0  

**Lightly Armoured**  
+1 STR or +1 DEX; gain Light Armour proficiency. citeturn19view0  

**Lucky**  
Gain 3 Luck Points per long rest; spend to gain advantage on attack rolls/ability checks/saving throws, or force an enemy to reroll an attack roll. citeturn19view0  

**Mage Slayer**  
Advantage on saving throws vs spells cast within melee range; reaction to attack a caster when it casts in melee; targets you hit have disadvantage on concentration saves. citeturn19view0  

**Magic Initiate: Bard / Cleric / Druid / Sorcerer / Warlock / Wizard**  
Learn 2 cantrips + 1 level-1 spell from the chosen list; cast the level‑1 spell once per long rest; spellcasting ability depends on the chosen list (CHA/WIS/INT). Implement as `SpellGrant` + `CantripGrant` with list references only. citeturn19view0turn19view1  

**Martial Adept**  
Learn 2 Battle Master manoeuvres; gain 1 superiority die; superiority dice recover on short/long rest. citeturn19view1  

**Medium Armour Master** *(requires Medium Armour Proficiency)*  
Medium armor no longer imposes disadvantage on Stealth checks; max DEX bonus to AC in medium armor becomes **+3** instead of +2. citeturn19view1  

**Mobile**  
+3m movement; difficult terrain doesn’t slow you after Dash; after making a melee attack, you don’t provoke opportunity attacks from that target if you move. citeturn7view1turn7view4  

**Moderately Armoured** *(requires Light Armour Proficiency)*  
+1 STR or +1 DEX; gain medium armor proficiency and shields proficiency. citeturn7view1turn19view2  

**Performer**  
+1 CHA; gain Musical Instrument proficiency. citeturn7view1turn18view1  

**Polearm Master**  
Bonus-action butt attack with glaive/halberd/pike/quarterstaff/spear; opportunity attack when targets enter your reach; butt attack uses higher of STR/DEX for damage modifier per note. citeturn7view1turn7view2  

**Resilient**  
+1 to a chosen ability; gain proficiency in that ability’s saving throw (implement as selectable variant feat). citeturn7view2turn20view0  

**Ritual Caster**  
Learn two ritual spells; implement as two SpellGrants chosen from the limited list (spell mechanics out-of-scope here). citeturn7view2  

**Savage Attacker**  
For melee weapon attacks, roll damage dice twice and use the higher result. citeturn7view2turn20view0  

**Sentinel**  
Reaction attack when a nearby enemy attacks an ally; opportunity attacks reduce target movement to 0 for the rest of its turn; advantage on opportunity attacks. citeturn20view0  

**Sharpshooter**  
Ranged attacks ignore low-ground penalty (BG3 wording references high ground rules); toggle “All In” for -5 to hit with proficient ranged weapons, +10 damage. citeturn20view0turn18view1  

**Shield Master**  
+2 bonus to Dexterity saving throws while wielding a shield; reaction “Block” to reduce AoE spell damage (half on failed save, none on success per description). citeturn20view1  

**Skilled**  
Gain proficiency in any 3 skills. citeturn20view1turn6view1  

**Spell Sniper**  
Learn 1 cantrip from a limited list; reduce critical threshold by 1 for spell attacks (stacking). Cantrip uses the character’s spellcasting ability. citeturn20view1  

**Tavern Brawler**  
+1 STR or +1 CON; when making unarmed attacks, improvised weapon attacks, or throws, add STR modifier **twice** to attack and damage (with DEX-unarmed caveats noted). citeturn20view2  

**Tough**  
Max HP increases by **+2 per level**. citeturn20view2turn21view0  

**War Caster**  
Advantage on concentration saving throws; reaction opportunity spell: cast Shocking Grasp when a target leaves melee range (does not grant at-will Shocking Grasp). citeturn20view2turn21view0  

**Weapon Master**  
+1 STR or +1 DEX; gain proficiency with 4 weapon types of your choice. citeturn21view0  

