# BG3 Combat Authenticity Roadmap

**Analysis Date**: 2026-02-14  
**Purpose**: Identify critical missing systems for authentic BG3 combat mechanics

---

## Executive Summary

Your current implementation has solid foundations but is missing **BG3's core stat modification architecture**. BG3 uses a layered Boost/Passive/Interrupt system where:
- **Boosts** are the atomic stat modifiers (AC+2, Advantage on attacks, damage resistance, etc.)
- **Passives** grant boosts and react to game events (OnAttack, OnDamaged, etc.)
- **Statuses** apply boosts with duration tracking
- **Interrupts** are reactive abilities that modify rolls or trigger effects

Your current system has pieces of this (modifiers, statuses, reactions) but they're not integrated the BG3 way. The critical missing piece is **the Boost DSL** - BG3's flexible string-based boost syntax that can modify ANY stat/roll/behavior.

---

## Phase 1: Core Boost System (CRITICAL - Do First)

### 1.1 Boost DSL Parser & Evaluator

**What BG3 Has:**
```
data "Boosts" "AC(2);Advantage(AttackRoll);Resistance(Fire,Resistant)"
data "Boosts" "IF(not DistanceToTargetGreaterThan(3)):Advantage(AttackTarget)"
data "Boosts" "WeaponDamage(1d4, Fire);StatusImmunity(BURNING)"
data "Boosts" "ActionResourceBlock(Movement);Disadvantage(SavingThrow,Dexterity)"
```

**What You Need:**
- `Combat/Rules/Boosts/BoostParser.cs` - Parse boost strings into structured data
- `Combat/Rules/Boosts/BoostEvaluator.cs` - Execute boosts in context (attacker, target, roll type)
- `Combat/Rules/Boosts/BoostDefinition.cs` - Internal representation
- `Combat/Rules/Boosts/ConditionEvaluator.cs` - Parse and evaluate IF() conditions

**Boost Types to Support (Priority Order):**

**Tier 1 (Core Combat):**
- `AC(value)` - Modify armor class
- `Advantage(AttackRoll)` / `Disadvantage(AttackRoll)` 
- `Advantage(SavingThrow, Ability)` / `Disadvantage(SavingThrow, Ability)`
- `Advantage(AllSavingThrows)` / `Disadvantage(AllAbilities)`
- `Resistance(DamageType, Resistant|Immune|Vulnerable)`
- `StatusImmunity(StatusID)`
- `DamageBonus(value, DamageType)` - Add damage to attacks
- `WeaponDamage(dice, DamageType)` - Extra weapon damage
- `Ability(AbilityName, modifier)` - Change ability score

**Tier 2 (Action Economy):**
- `ActionResourceBlock(ResourceType)` - Block action/bonus/reaction/movement
- `ActionResourceMultiplier(Movement, multiplier, base)` - Dash doubles movement
- `ActionResourceConsumeMultiplier(Movement, multiplier, base)` - Difficult terrain
- `ActionResource(Movement, value, base)` - Modify movement points

**Tier 3 (Advanced):**
- `UnlockSpell(SpellID)` - Grant a spell/ability
- `UnlockInterrupt(InterruptID)` - Grant a reaction
- `ProficiencyBonus(Type, Name)` - Grant proficiency
- `RollBonus(RollType, dice)` - Add to skill checks, etc.
- `CriticalHit(AttackRoll, Success, Never)` - Control crits
- `Attribute(AttributeName)` - Special flags (Grounded, Invulnerable, etc.)

**Integration Points:**
- `RulesEngine.RollAttack()` must query active boosts before rolling
- `RulesEngine.RollSavingThrow()` must check for Advantage/Disadvantage boosts
- `DamagePipeline.Calculate()` must apply resistance boosts
- `Combatant.ActionBudget` must respect ActionResourceBlock boosts

### 1.2 Boost Application System

**What You Need:**
- `Combat/Rules/Boosts/BoostApplicator.cs` - Apply boosts from sources to combatants
- Track boost ownership (which status/passive granted this boost?)
- Support boost removal when source expires
- Support conditional boosts (IF conditions evaluated per-query)

**Data Flow:**
```
Status "BLESSED" applied → Parse "Boosts" field → Store active boosts on combatant
↓
Player makes attack roll → RulesEngine collects active boosts → Apply modifiers
↓
Status expires → Remove all boosts from that status
```

---

## Phase 2: Stats Functor System (Event-Driven Mechanics)

### 2.1 StatsFunctorContext Events

**What BG3 Has:**
```
data "StatsFunctorContext" "OnAttack"
data "StatsFunctors" "DealDamage(1d8, MainMeleeWeaponDamageType)"

data "StatsFunctorContext" "OnDamaged"  
data "StatsFunctors" "ApplyStatus(GAPING_WOUND_DAMAGE, 100, 0)"

data "StatsFunctorContext" "OnShortRest"
data "StatsFunctors" "RegainHitPoints(MaxHP/2)"
```

**What You Need:**
- Expand `RuleEventBus` with new event types:
  - `OnAttack` (pre-damage, can add effects)
  - `OnDamage` (post-hit, before damage applied)
  - `OnDamaged` (after taking damage)
  - `OnHeal` (when receiving healing)
  - `OnCast` (when casting a spell)
  - `OnAbilityCheck` (skill checks, ability checks)
  - `OnTurn` (start of turn)
  - `OnShortRest` / `OnLongRest`
  - `OnStatusApplied` / `OnStatusRemoved`

- `Combat/Rules/Functors/StatsFunctorDefinition.cs`
- `Combat/Rules/Functors/StatsFunctorExecutor.cs`
- Parse functors: `DealDamage()`, `ApplyStatus()`, `RegainHitPoints()`, `RemoveStatus()`, `RestoreResource()`

**Integration:**
- Passives can have StatsFunctors that execute on events
- StatusSystem triggers events when statuses are applied/removed
- Combat actions trigger OnAttack/OnDamage events

### 2.2 Condition DSL

**What BG3 Has:**
```
data "Conditions" "HasDamageEffectFlag(DamageFlags.Hit) and IsMeleeAttack()"
data "Conditions" "IsWeaponAttack() and not IsCriticalMiss()"
```

**What You Need:**
- `Combat/Rules/Conditions/ConditionParser.cs`
- `Combat/Rules/Conditions/ConditionEvaluator.cs`
- Support context variables: `context.Source`, `context.Target`, `context.Observer`
- Common functions:
  - `IsMeleeAttack()`, `IsRangedAttack()`, `IsWeaponAttack()`, `IsSpell()`
  - `HasStatus(StatusID)`, `HasPassive(PassiveID)`
  - `ClassLevelHigherOrEqualThan(level, className)`
  - `DistanceToTargetGreaterThan(distance)`
  - `IsDamageType*()` family

---

## Phase 3: Fields Missing from Combatant

### 3.1 Character-Level Fields (from BG3_Data/Stats/Character.txt)

**Add to `Combatant` or `CombatantStats`:**

```csharp
// Resistances (all 13 damage types)
public Dictionary<DamageType, ResistanceLevel> Resistances { get; set; }
// Enum: Vulnerable = 2x damage, Normal = 1x, Resistant = 0.5x, Immune = 0x

// Vision
public float Sight { get; set; } = 1600;  // Perception range
public float Hearing { get; set; } = 1100;  // Sound detection range
public float DarkvisionRange { get; set; } = 0;  // See in darkness up to X

// Proficiency Groups (from BG3)
public HashSet<string> ProficiencyGroups { get; set; }  
// e.g., "SimpleWeapons", "MartialWeapons", "LightArmor"

// Passive IDs
public List<string> PassiveIds { get; set; }
// Active passives granted by race/class/feats

// Default Boosts (always-on)
public string DefaultBoosts { get; set; }
// Parsed at combat start, e.g., "BlockRegainHP(Undead;Construct)"

// Status Immunities
public HashSet<string> PersonalStatusImmunities { get; set; }
// Cannot be affected by these statuses

// Difficulty Statuses (scaling by difficulty)
public string DifficultyStatuses { get; set; }
// "STATUS_EASY: HEALTHREDUCTION_EASYMODE; STATUS_HARD: HEALTHBOOST_HARDCORE"
```

### 3.2 Add to RulesEngine

**Proficiency Checks:**
```csharp
public bool IsProficientWith(Combatant combatant, string weaponCategory)
{
    return combatant.ResolvedCharacter?.Proficiencies
        .IsProficientInWeapon(weaponCategory) ?? false;
}

public bool IsProficientInArmor(Combatant combatant, ArmorType armorType)
{
    // Check ProficiencySet
}
```

**Damage Resistance Application:**
```csharp
// In DamagePipeline.Calculate():
// After modifiers, apply target's resistances
foreach (var damageEntry in damageByType)
{
    var resistance = target.Resistances[damageEntry.Key];
    damageEntry.Value = ApplyResistance(damageEntry.Value, resistance);
}
```

---

## Phase 4: Enhanced Status System

### 4.1 Status Boost Integration

**Current Gap**: Statuses have `StatusModifier` but don't use BG3's Boost DSL.

**Required Changes:**

```csharp
// StatusDefinition.cs
public class StatusDefinition
{
    // ADD:
    public string Boosts { get; set; }  // Raw boost string from BG3 data
    public List<ParsedBoost> ParsedBoosts { get; private set; }
    
    // ADD:
    public string OnApplyFunctors { get; set; }  // Execute when status applied
    public string OnRemoveFunctors { get; set; }  // Execute when status removed
    public string OnTickFunctors { get; set; }    // Execute each turn
    
    // ADD:
    public string AuraRadius { get; set; }  // For aura statuses
    public string AuraStatuses { get; set; }  // Apply statuses to nearby units
}
```

**Integration:**
- When status is applied, parse `Boosts` and register them with BoostApplicator
- When status expires, remove boosts
- Support auras (apply statuses to entities within radius)

### 4.2 Status Property Flags

**BG3 Has:**
```
data "StatusPropertyFlags" "DisableOverhead;DisableCombatlog;IgnoreResting"
data "StatusPropertyFlags" "LoseControl;OverheadOnTurn"
data "StatusPropertyFlags" "IsInvulnerable;IsInvulnerableVisible"
```

**Add enum:**
```csharp
[Flags]
public enum StatusPropertyFlags
{
    None = 0,
    DisableOverhead = 1 << 0,      // Don't show floating icon
    DisableCombatlog = 1 << 1,     // Don't log to combat log
    DisablePortraitIndicator = 1 << 2,  // Don't show on portrait
    IgnoreResting = 1 << 3,        // Persist through rests
    ApplyToDead = 1 << 4,          // Can apply to dead units
    LoseControl = 1 << 5,          // Unit loses control (charmed, etc.)
    IsInvulnerable = 1 << 6,       // Cannot take damage
    ToggleOnTurn = 1 << 7,         // Reapply each turn
    ForceOverhead = 1 << 8         // Always show overhead
}
```

---

## Phase 5: Interrupt/Reaction System Enhancements

### 5.1 Interrupt Context Types

**Current**: Basic trigger types  
**BG3 Has**: Rich interrupt contexts

**Add to `ReactionTrigger.cs`:**
```csharp
public enum InterruptContext
{
    OnSpellCast,           // Someone casts a spell (Counterspell)
    OnPostRoll,            // After d20 rolled, before resolution (Cutting Words)
    OnPreDamage,           // Before damage applied (Shield Master)
    OnAttackRoll,          // Someone makes attack roll
    OnSavingThrow,         // Someone makes saving throw
    OnMovement,            // Someone moves (Sentinel feat)
    OnCastFinished,        // Spell cast completes
    OnStatusApplied        // Status applied to you/ally
}
```

### 5.2 Reaction Properties (Roll Adjustment)

**BG3 Reactions Can:**
- Adjust d20 rolls: `AdjustRoll(OBSERVER_OBSERVER, +5)` (Bardic Inspiration)
- Set Advantage/Disadvantage: `SetAdvantage()`, `SetDisadvantage()` (Reckless Attack)
- Apply statuses: `ApplyStatus(OBSERVER_OBSERVER, STATUS_NAME, 100, 1)`
- Execute spells: `UseSpell(OBSERVER_SOURCE, SpellID, true, true, true)`

**Add to `ReactionDefinition.cs`:**
```csharp
public class ReactionDefinition
{
    // ADD:
    public string Properties { get; set; }  // BG3 reaction properties
    public string Conditions { get; set; }  // When can this trigger?
    public InterruptContext InterruptContext { get; set; }
    public InterruptContextScope InterruptContextScope { get; set; }  // Self, Nearby, Global
    
    public string EnableCondition { get; set; }  // Can be toggled on/off
    public string EnableContext { get; set; }    // When to re-evaluate enable
}
```

### 5.3 Reaction Evaluation

**Required:**
- Parse `Conditions` to check if reaction can trigger
- Parse `Properties` to execute reaction effects
- Support roll adjustment (modify attack/save rolls mid-resolution)
- Support advantage/disadvantage forcing

---

## Phase 6: Passive System Integration

### 6.1 Passive Loading

**Current**: No passive loading from data  
**Need**: `Data/Passives/PassiveLoader.cs`

**Structure:**
```csharp
public class PassiveDefinition
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
    
    // Permanent boosts
    public string Boosts { get; set; }
    
    // Conditional boosts (re-evaluated per context)
    public string BoostConditions { get; set; }
    public string BoostContext { get; set; }  // OnCreate, OnInventoryChanged
    
    // Event-driven functors
    public string StatsFunctorContext { get; set; }  // OnAttack, OnDamaged, etc.
    public string Conditions { get; set; }           // When to fire
    public string StatsFunctors { get; set; }        // What to execute
    
    // Toggles (like Non-Lethal Attacks)
    public bool IsToggled { get; set; }
    public string ToggleOnFunctors { get; set; }
    public string ToggleOffFunctors { get; set; }
    public string ToggleGroup { get; set; }  // Only one active in group
}
```

### 6.2 Passive Registry

**Need**: `Combat/Rules/PassiveRegistry.cs`

**Features:**
- Load all passives from `BG3_Data/Stats/Passive.txt`
- Grant passives to combatants based on race/class/feats
- Apply passive boosts when passive is active
- Subscribe passive functors to events
- Handle toggle passives (player can enable/disable)

### 6.3 Integration with ResolvedCharacter

**Add to character building:**
- When resolving race: Grant race passives (e.g., `Darkvision`, `FeyAncestry`)
- When resolving class levels: Grant class passives (e.g., `RagePassive`, `SneakAttack`)
- When resolving feats: Grant feat passives (e.g., `GreatWeaponMaster`)

---

## Phase 7: Proficiency System Enhancements

### 7.1 Weapon/Armor Proficiency Integration

**Current**: `ProficiencySet` has basic structure  
**Need**: Integration into combat resolution

**Where Used:**
1. **Attack Rolls**: Apply proficiency bonus if proficient with weapon
2. **AC Calculation**: Some armor requires proficiency
3. **Equip Checks**: Warn/prevent equipping non-proficient items

**Add to `RulesEngine`:**
```csharp
public int GetAttackBonus(Combatant attacker, string weaponId)
{
    var weapon = GetWeapon(weaponId);
    int abilityMod = GetRelevantAbilityModifier(attacker, weapon);
    
    // Check proficiency
    bool isProficient = attacker.ResolvedCharacter?.Proficiencies
        .IsProficientInWeapon(weapon.Category) ?? false;
    
    int profBonus = isProficient ? attacker.ProficiencyBonus : 0;
    
    return abilityMod + profBonus;
}
```

### 7.2 Proficiency Boost Parsing

**BG3 Boosts:**
```
data "Boosts" "Proficiency(Rapiers);Proficiency(Shortswords)"
data "Boosts" "ProficiencyBonus(Skill,History);ExpertiseBonus(History)"
```

**Add to BoostEvaluator:**
- `Proficiency(WeaponType)` → Grant weapon proficiency
- `Proficiency(ArmorType)` → Grant armor proficiency
- `ProficiencyBonus(Type, Name)` → Grant proficiency in skill/save
- `ExpertiseBonus(SkillName)` → Grant expertise (double proficiency)

---

## Critical Dependencies (What Depends on What)

```
PRIORITY 1: Boost System
├── Everything else depends on this
├── Implement boost parser/evaluator first
└── Get basic AC(), Advantage(), Resistance() working

PRIORITY 2: Passive System
├── Depends on: Boost System
├── Grants boosts to combatants
└── Unlocks race/class features

PRIORITY 3: Stats Functors
├── Depends on: Boost System, Event System
├── Powers event-driven abilities (on hit effects)
└── Required for spell damage riders, etc.

PRIORITY 4: Enhanced Reactions
├── Depends on: Boost System, Stats Functors
├── Reactions can adjust rolls, apply boosts
└── Complete the reaction loop

PRIORITY 5: Status Enhancements
├── Depends on: Boost System, Stats Functors
├── Statuses apply boosts properly
└── Aura statuses work

PRIORITY 6: Proficiency Integration
├── Depends on: Basic systems working
├── Fine-tunes attack rolls, AC
└── Polish for authenticity
```

---

## Implementation Roadmap

### Week 1-2: Boost System Foundation
- [ ] Create `Combat/Rules/Boosts/` folder
- [ ] Implement `BoostParser` for basic boosts (AC, Advantage, Resistance)
- [ ] Implement `BoostEvaluator` 
- [ ] Create `BoostApplicator` to track active boosts
- [ ] Integrate with `RulesEngine.RollAttack()` and `RulesEngine.RollSavingThrow()`
- [ ] Add boost collection/removal to StatusSystem
- [ ] **BUILD GATE**: All basic boosts parse and apply correctly

### Week 3: Condition DSL
- [ ] Implement `ConditionParser` for IF() statements
- [ ] Support basic conditions (distance checks, status checks, damage type checks)
- [ ] Add conditional boost evaluation
- [ ] **BUILD GATE**: Conditional boosts work

### Week 4: Stats Functors
- [ ] Expand `RuleEventBus` with new event types
- [ ] Implement `StatsFunctorParser`
- [ ] Implement `StatsFunctorExecutor`
- [ ] Support basic functors (DealDamage, ApplyStatus, RegainHitPoints)
- [ ] **BUILD GATE**: On-hit effects work

### Week 5-6: Passive System
- [ ] Create `Data/Passives/PassiveLoader.cs`
- [ ] Load all passives from BG3_Data
- [ ] Create `Combat/Rules/PassiveRegistry.cs`
- [ ] Grant race/class passives during character resolution
- [ ] Apply passive boosts to combatants
- [ ] Subscribe passive functors to events
- [ ] **BUILD GATE**: Race passives (Darkvision, FeyAncestry) work

### Week 7: Enhanced Status System
- [ ] Add `Boosts` field to `StatusDefinition`
- [ ] Parse and apply status boosts
- [ ] Implement aura statuses
- [ ] Add status property flags
- [ ] **BUILD GATE**: Status effects modify combat properly

### Week 8: Enhanced Reactions
- [ ] Add new interrupt contexts
- [ ] Implement roll adjustment (mid-roll modification)
- [ ] Add advantage/disadvantage forcing
- [ ] Parse reaction conditions and properties
- [ ] **BUILD GATE**: Cutting Words, Shield, Counterspell work

### Week 9-10: Proficiency Integration
- [ ] Integrate proficiency checks into attack rolls
- [ ] Add proficiency to AC calculations where needed
- [ ] Implement proficiency-granting boosts
- [ ] Add equipment proficiency warnings
- [ ] **BUILD GATE**: Non-proficient attacks miss proficiency bonus

### Week 11-12: Polish & Testing
- [ ] Comprehensive autobattle tests
- [ ] Load complex BG3 character builds
- [ ] Verify all class features work
- [ ] Performance optimization
- [ ] **BUILD GATE**: `./scripts/ci-build.sh` and `./scripts/ci-test.sh` pass

---

## Tools/Infrastructure Needed

### 1. Boost String Tester
- CLI tool to parse and evaluate boost strings
- Input: boost string + context
- Output: parsed structure + evaluation result
- Useful for debugging complex boosts

### 2. Passive Definition Browser
- UI to browse all loaded passives
- Show: name, description, boosts, functors
- Filter by race/class/feat
- Helps designers verify passive loading

### 3. Combat State Inspector
- Runtime tool to inspect active boosts on a combatant
- Show: source (status/passive), value, conditions
- Toggle boosts on/off for testing
- Verify boost application/removal

### 4. Event Trace Logger
- Log all RuleEventBus events during combat
- Show: trigger type, source, target, result
- Filter by event type or combatant
- Debug why functors aren't firing

---

## Specific BG3 Data Files to Parse

**Priority 1:**
- `BG3_Data/Stats/Passive.txt` - All race/class/feat passives
- `BG3_Data/Statuses/Status_BOOST.txt` - All boost-type statuses

**Priority 2:**
- `BG3_Data/Stats/Interrupt.txt` - All reactions/interrupts
- `BG3_Data/Stats/Character.txt` - Enemy stat blocks for testing

**Priority 3:**
- `BG3_Data/ActionResourceDefinitions.lsx` - Resource definitions (already have loader)
- `BG3_Data/Spells/*.txt` - Spell definitions (for spell effects)

---

## Key Insights from BG3 Data

1. **Everything is a Boost**: BG3 models stat changes, advantage, resistance, immunities, etc. ALL as boosts. This is their core abstraction.

2. **Conditionals are Everywhere**: Most boosts are conditional (IF distance > 3, IF has status X, IF weapon has property Y). You need robust condition evaluation.

3. **Events Drive Abilities**: Passive abilities react to events (OnAttack, OnDamaged). This is how on-hit effects, retaliation, and triggered abilities work.

4. **Layers are Explicit**: Damage resistance is explicit (Vulnerable = 2x, Resistant = 0.5x, Immune = 0x). AC bonuses stack. Initiative is base + dex + bonuses.

5. **Proficiency is Binary**: Either you have proficiency or you don't. It doesn't stack. Expertise doubles it.

6. **Resources are Strings Initially**: BG3 defines resources as strings, then parses them. Your `ResourcePool` system is good, just need to populate it from character data.

7. **Reactions are Rich**: BG3 reactions can modify rolls mid-resolution, force advantage/disadvantage, apply statuses, or execute spells. They're not just simple counteractions.

---

## Testing Strategy

### Unit Tests
- Boost parser: Parse all boost types correctly
- Boost evaluator: Apply boosts to rolls/stats
- Condition evaluator: Evaluate complex IF conditions
- Functor executor: Execute all functor types

### Integration Tests
- Character with Darkvision passive grants DarkvisionRange boost
- Blessed status grants +1d4 to attack rolls
- Shield spell increases AC when reaction triggered
- Reckless Attack grants advantage but enemies get advantage

### System Tests (Autobattle)
- Level 5 Fighter with Extra Attack makes 2 attacks per action
- Rogue with Sneak Attack deals extra damage when flanking
- Cleric with Bless buff affects party attack rolls
- Counterspell interrupts enemy spell casts

### Data-Driven Tests
- Load all BG3 passives without errors
- Parse all boost strings from Status_BOOST.txt
- Apply complex boost chains (multiple IF conditions)
- Verify resistance stacking (resistant + vulnerable = normal)

---

## Risks & Mitigations

### Risk: BG3's Boost DSL is Complex
**Mitigation**: Implement incrementally. Start with simple boosts (AC, Advantage), then add conditions, then complex functors.

### Risk: Conditional Boosts Need Full Context
**Mitigation**: Build robust `BoostEvaluationContext` that captures attacker, target, weapon, distance, statuses, etc.

### Risk: Performance (Boost Evaluation at Every Roll)
**Mitigation**: Cache boost parsing. Only re-evaluate conditional boosts when context changes (status applied/removed, moved, etc.).

### Risk: BG3 Data is Incomplete/Buggy
**Mitigation**: Add override/patch system. Define custom passives/boosts where BG3 data is wrong or missing.

### Risk: Integration Breaks Existing Systems
**Mitigation**: Keep backward compatibility. Old-style modifiers coexist with boosts until full migration. Use feature flags.

---

## Success Criteria

You've achieved BG3 combat authenticity when:

1. ✅ Loading a BG3 character (Elf Ranger level 5) applies Darkvision, Elven Weapon Training, Extra Attack
2. ✅ Applying Bless status grants +1d4 to attack rolls (visible in roll breakdown)
3. ✅ Casting Shield reaction increases AC and causes incoming attack to miss
4. ✅ Rogue's Sneak Attack passive triggers when flanking, dealing extra damage
5. ✅ Character with Resistance(Fire) takes half fire damage
6. ✅ Character with Disadvantage on Dexterity saves (from Restrained) rolls twice and takes lower
7. ✅ Action economy respects ActionResourceBlock (Surprised = no actions/reactions)
8. ✅ Proficiency bonus applies to attack rolls when proficient with weapon
9. ✅ Complex passive with OnDamaged functor triggers correctly (e.g., Hellish Rebuke)
10. ✅ All autobattle tests pass with BG3-derived character builds

---

## Conclusion

Your current implementation is solid but lacks **BG3's universal Boost abstraction**. Implementing the Boost DSL unlocks everything:
- Race/class features work automatically
- Statuses modify stats properly
- Reactions can adjust rolls
- Equipment bonuses stack correctly
- Combat feels like BG3

**Start with Phase 1 (Boost System).** Once boosts work, the rest falls into place. The Boost system IS the foundation of BG3 combat.

**Estimated Total Effort:** 10-12 weeks for full authenticity, but you can ship incrementally. Boost basics + passive loading = 4-5 weeks, and you're 70% there.
