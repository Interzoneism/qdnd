# Implementation Agent Reference — Codebase Patterns & Entry Points

> Generated: 2026-02-10 | Read-only audit of `/home/martin/repos/qdnd`

---

## 1. Extra Attack

### Data Definition
- **File**: [Data/CharacterModel/ClassDefinition.cs](Data/CharacterModel/ClassDefinition.cs#L73)
- **Field**: `LevelProgression.ExtraAttacks` — `public int? ExtraAttacks { get; set; }`
- **JSON**: [Data/Classes/martial_classes.json](Data/Classes/martial_classes.json#L303) — `"ExtraAttacks": 1` at level 5 for Fighter, Barbarian, Paladin, Ranger; Fighter level 11 gets `"ExtraAttacks": 2`.

### Runtime Status: **NOT WIRED**
No C# code reads `ExtraAttacks` during ability execution. Every character gets exactly 1 attack per action.

### Ability Execution Flow (where to insert multi-attack)
1. **AI generates attack candidate**: [Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L265) `GenerateAttackCandidates()` creates `AIAction { ActionType = AIActionType.Attack, AbilityId = "basic_attack" }`.
2. **CombatArena dispatches to pipeline**: [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L1173) — In the AI action executor, the attack action calls `ExecuteAbility(actor.Id, abilityId)`.
3. **ExecuteAbility (single target)**: [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L1537) — `ExecuteAbility(string actorId, string abilityId, string targetId)` validates target, then calls `ExecuteResolvedAbility()`.
4. **ExecuteResolvedAbility**: [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L1700) — Creates `AbilityExecutionOptions`, calls `_effectPipeline.ExecuteAbility(ability.Id, actor, targets, executionOptions)`.
5. **EffectPipeline.ExecuteAbility**: [Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs#L211) — The main execution method. Consumes action budget, rolls attack, rolls save, runs effects.
6. **DealDamageEffect.Execute**: [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs#L223) — Rolls damage dice, applies sneak attack, conditional modifiers, reactions, then calls `target.Resources.TakeDamage()`.

### basic_attack Registration
[Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L804):
```csharp
var basicAttack = new AbilityDefinition {
    Id = "basic_attack",
    Name = "Basic Attack",
    Range = 1.5f,
    AttackType = AttackType.MeleeWeapon,
    Cost = new AbilityCost { UsesAction = true },
    Effects = new List<EffectDefinition> {
        new EffectDefinition { Type = "damage", DamageType = "physical", DiceFormula = "1d8+2" }
    }
};
```

### Where to Insert Multi-Attack
**Option A (recommended)**: In `CombatArena.ExecuteResolvedAbility()` (~line 1700), after the call to `_effectPipeline.ExecuteAbility()`, check if the ability was a weapon attack, look up `ExtraAttacks` from the actor's `ResolvedCharacter`, and loop additional attack calls. The action budget cost was already consumed on the first call.

**Option B**: In `EffectPipeline.ExecuteAbility()` (~line 211), after the ability resolves, check if it's a weapon attack and loop additional attacks internally. More encapsulated but mixes multi-attack logic into the generic pipeline.

### Key Method Signatures
```csharp
// EffectPipeline.cs:211
public AbilityExecutionResult ExecuteAbility(string abilityId, Combatant source, List<Combatant> targets, AbilityExecutionOptions options)

// CombatArena.cs:1537
public void ExecuteAbility(string actorId, string abilityId, string targetId)

// CombatArena.cs:1700
private void ExecuteResolvedAbility(Combatant actor, AbilityDefinition ability, List<Combatant> targets, string targetSummary, Vector3? targetPosition = null)
```

---

## 2. Death/Downed System

### LifeState Enum
[Combat/Entities/CombatantLifeState.cs](Combat/Entities/CombatantLifeState.cs):
```csharp
public enum CombatantLifeState { Alive, Downed, Unconscious, Dead }
```

### HP Reduction
[Combat/Entities/Combatant.cs](Combat/Entities/Combatant.cs#L34) `ResourceComponent.TakeDamage()`:
```csharp
public int TakeDamage(int amount) {
    // Consumes TemporaryHP first, then CurrentHP
    // Returns total damage dealt
}
```
- `IsDowned => CurrentHP <= 0`
- `IsAlive => CurrentHP > 0`

### Where HP Hit 0 Is Handled
**In DealDamageEffect.Execute** — [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs#L302):
```csharp
int actualDamageDealt = target.Resources.TakeDamage(finalDamage);
bool killed = target.Resources.IsDowned;

if (killed && target.LifeState == CombatantLifeState.Alive) {
    target.LifeState = CombatantLifeState.Downed;
}
```
This is the **only place** LifeState is set to Downed. Note: it sets `Downed`, never `Dead`. There is **no death save system** implemented.

### Healing Revives Downed
[Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs#L379) — `HealEffect.Execute()`:
```csharp
if (target.LifeState == CombatantLifeState.Downed && target.Resources.CurrentHP > 0) {
    target.LifeState = CombatantLifeState.Alive;
}
```

### Activity Checks
- `Combatant.CanAct` => `LifeState == Alive && ParticipationState == InFight`
- `Combatant.IsActive` => same as `CanAct`
- Used by turn queue to skip downed combatants and by AI to filter targets.

### Victory Check in CombatArena
[Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L2112) `EndCombat()`:
```csharp
var playerAlive = _combatants.Any(c => c.Faction == Faction.Player && c.IsActive);
var enemyAlive = _combatants.Any(c => c.Faction == Faction.Hostile && c.IsActive);
```
Uses `IsActive` (alive + in fight). No mid-combat death check loop exists; the turn driver naturally skips downed combatants via `CanAct`.

### CombatArena logging on kill
[Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L2315):
```csharp
if (killed) {
    _combatLog.LogCombatantDowned(result.SourceId, sourceName, targetId, targetName);
}
```

---

## 3. Height Advantage

### HeightService
[Combat/Environment/HeightService.cs](Combat/Environment/HeightService.cs):

```csharp
public float AdvantageThreshold = 3f;  // Height diff to trigger modifier

public int GetAttackModifier(Combatant attacker, Combatant target) {
    // Higher => +2, Lower => -2, Level => 0
}
```

### Where It's Used in Attack Resolution
[Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs#L275):
```csharp
int heightMod = 0;
if (Heights != null) {
    heightMod = Heights.GetAttackModifier(source, primaryTarget);
}
```
Then at line ~285: `BaseValue = GetAttackRollBonus(source, ability, effectiveTags) + heightMod`

The `heightMod` is added to the attack roll's `BaseValue` AND stored in `attackQuery.Parameters["heightModifier"]` for breakdown display.

### Position Access
Combatant positions are `Vector3`: `Combatant.Position` ([Combat/Entities/Combatant.cs](Combat/Entities/Combatant.cs#L110)). Height is `Position.Y`. HeightService reads `attacker.Position.Y - target.Position.Y`.

### Height Already Works
The +2/-2 modifier **is already implemented** in `HeightService.GetAttackModifier()`. It's wired into the `EffectPipeline` attack roll. The 3m threshold controls when it activates.

---

## 4. Dodge Mechanic (Advantage/Disadvantage Resolution)

### Modifier System
[Combat/Rules/Modifier.cs](Combat/Rules/Modifier.cs):
```csharp
public enum ModifierType { Flat, Percentage, Override, Advantage, Disadvantage }
public enum ModifierTarget { AttackRoll, DamageDealt, DamageTaken, HealingReceived, ArmorClass, SavingThrow, SkillCheck, ... }
```

### Creating Advantage/Disadvantage Modifiers
```csharp
Modifier.Advantage("source_name", ModifierTarget.AttackRoll, "source_id")
Modifier.Disadvantage("source_name", ModifierTarget.AttackRoll, "source_id")
```

### Advantage Resolution (5e rules: any adv + any dis = normal)
[Combat/Rules/Modifier.cs](Combat/Rules/Modifier.cs#L196) `ModifierStack.ResolveAdvantage()` and
[Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs#L262) `RollAttack()`:
```csharp
// Sources combined from: modifier stack + status-injected sources
allAdvSources.AddRange(GetStringListParameter(input.Parameters, "statusAdvantageSources"));
allDisSources.AddRange(GetStringListParameter(input.Parameters, "statusDisadvantageSources"));

if (allAdvSources.Count > 0 && allDisSources.Count > 0) combinedState = Normal;
else if (allAdvSources.Count > 0) combinedState = Advantage;
else if (allDisSources.Count > 0) combinedState = Disadvantage;
```

### Status-Based Advantage/Disadvantage on Attacks
[Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs#L489) `GetStatusAttackContext()`:
```csharp
// Checks target for: prone (adv melee), blinded (adv), stunned (adv), paralyzed (adv + auto-crit melee)
// Checks source for: threatened (dis on ranged/spell)
```
This is where **Patient Defence disadvantage** should be added. Pattern:
```csharp
if (Statuses.HasStatus(target.Id, "patient_defence")) {
    disadvantages.Add("Patient Defence");
}
```

### Status Modifiers Injected into RulesEngine
See Section 7 below. When a status is applied, `StatusInstance.CreateModifiers()` creates `Modifier` objects with the right `ModifierTarget` and adds them via `RulesEngine.AddModifier()`. For attacks on a target with "dodge", you'd use `GetStatusAttackContext()` in EffectPipeline instead, since the modifier needs to apply to the *attacker's* roll against *this specific target*.

---

## 5. AI Dash/Disengage — Disabled Code

### Exact Disabled Code
[Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L230):
```csharp
// Dash candidate - disabled until CombatArena.ExecuteDash() API is implemented
// if (actor.ActionBudget?.HasAction == true)
// {
//     candidates.Add(new AIAction { ActionType = AIActionType.Dash });
// }

// Disengage candidate - disabled until CombatArena.ExecuteDisengage() API is implemented
// if (actor.ActionBudget?.HasAction == true)
// {
//     var nearbyEnemies = GetEnemies(actor).Where(e => actor.Position.DistanceTo(e.Position) <= 5f);
//     if (nearbyEnemies.Any())
//     {
//         candidates.Add(new AIAction { ActionType = AIActionType.Disengage });
//     }
// }
```

### What API It Needs
Comments say: **`CombatArena.ExecuteDash()`** and **`CombatArena.ExecuteDisengage()`**. These methods do not exist in `CombatArena.cs` (confirmed via grep — no matches).

### Scoring Already Exists
- `ScoreDash()` at [AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L605) — scores based on distance-to-close.
- `ScoreDisengage()` at [AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L618) — scores based on nearby threat count, HP%, archetype.

### What Needs to Be Built
1. `CombatArena.ExecuteDash(string actorId)` — doubles remaining movement for the turn, costs action.
2. `CombatArena.ExecuteDisengage(string actorId)` — sets a flag so movement doesn't provoke opportunity attacks, costs action.
3. Uncomment the two blocks in `GenerateCandidates()`.

---

## 6. On-Hit Trigger Pattern / Damage Pipeline

### Where Damage Is Applied
[Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs#L223) `DealDamageEffect.Execute()`:

```
1. RollDice() → baseDamage                    (line ~237)
2. Sneak Attack check → baseDamage +=          (line ~248)
3. RulesEngine.RollDamage() → finalDamage      (line ~291) — applies modifier stack
4. ApplyConditionalDamageModifiers()            (line ~298) — Wet/Rage/Bear modifiers
5. OnBeforeDamage callback → damageModifier     (line ~302) — reaction hook (Shield etc)
6. target.Resources.TakeDamage(finalDamage)     (line ~315) — actual HP reduction
7. LifeState set to Downed if HP <= 0           (line ~318)
8. Events.DispatchDamage()                      (line ~322) — post-damage event
```

### Existing Hooks
- **`OnBeforeDamage`** callback (step 5): Already exists at [Effect.cs](Combat/Abilities/Effects/Effect.cs#L302). This is the "after hit confirmed, before damage applied" hook. Used by reactions (Shield). Returns a `float` damage modifier.
- **`DispatchDamage` event** (step 8): Post-damage event fired via `RuleEventBus`. Status system listens to this for `DamageTaken` trigger effects.

### Where to Add New On-Hit Hooks
- **Before damage application (step 5-6)**: Use the existing `OnBeforeDamage` callback or add a new event between steps 5 and 6.
- **After hit confirmed but before damage roll (between steps 1-2)**: Currently no hook. Would need a new callback like `OnAfterHitConfirmed`.
- **Smite pattern**: Could go between step 2 and 3 — check for features that add bonus damage on hit (Divine Smite, Hex, etc.).

### Event Bus for Post-Damage
Status system subscribes to `RuleEventType.DamageTaken` in [Combat/Statuses/StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L227) for trigger effects like `OnDamageTaken`.

---

## 7. Status → Modifier → RulesEngine Flow

### Full Trace

**1. Status Applied** — [StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L335) `ApplyStatus()`:
```csharp
var instance = new StatusInstance(definition, sourceId, targetId);
list.Add(instance);
ApplyModifiers(instance);
```

**2. Modifiers Created** — [StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L454) `ApplyModifiers()`:
```csharp
private void ApplyModifiers(StatusInstance instance) {
    foreach (var mod in instance.CreateModifiers()) {
        _rulesEngine.AddModifier(instance.TargetId, mod);
    }
}
```

**3. StatusInstance.CreateModifiers()** — [StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L130):
```csharp
// For each StatusModifier in the definition:
var mod = new Modifier {
    Name = definition.Name,
    Type = modDef.Type,       // e.g., ModifierType.Disadvantage
    Target = modDef.Target,   // e.g., ModifierTarget.AttackRoll
    Value = value,
    Source = $"status:{InstanceId}",
    Tags = new HashSet<string>(Definition.Tags)
};
```
**Special conditions** are hardcoded per-status (e.g., `threatened` disadvantage only applies to ranged/spell attacks via `mod.Condition` lambda).

**4. RulesEngine stores it** — [RulesEngine.cs](Combat/Rules/RulesEngine.cs#L232):
```csharp
public void AddModifier(string combatantId, Modifier modifier) {
    GetModifiers(combatantId).Add(modifier);
}
```

**5. Attack roll reads modifiers** — [RulesEngine.cs](Combat/Rules/RulesEngine.cs#L248) `RollAttack()`:
```csharp
var attackerMods = GetModifiers(input.Source.Id);
var attackerResolution = attackerMods.ResolveAdvantage(ModifierTarget.AttackRoll, context);
// + applies flat modifiers to the roll
```

**6. Additionally, EffectPipeline injects status context** — [EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs#L306):
```csharp
var statusAttackContext = GetStatusAttackContext(source, primaryTarget, ability);
// Adds advantage/disadvantage sources to attackQuery.Parameters
```

### Key Insight
There are **two paths** for status effects to modify attacks:
1. **Through ModifierStack**: Status modifiers auto-register in RulesEngine when status is applied. These apply to ALL rolls by that combatant (e.g., `blinded` gives disadvantage on all your attack rolls).
2. **Through GetStatusAttackContext()**: Explicitly checked per-attack in EffectPipeline. These are context-dependent (e.g., attacking a `prone` target gives advantage only on melee attacks).

For **Patient Defence**: Use path 2 — add a check in `GetStatusAttackContext()` for `Statuses.HasStatus(target.Id, "patient_defence")` → add to `disadvantages`.

---

## 8. Ability JSON Schema

Example from [Data/Abilities/bg3_mechanics_abilities.json](Data/Abilities/bg3_mechanics_abilities.json):
```json
{
  "id": "hold_person",
  "name": "Hold Person",
  "description": "Paralyze a humanoid target on failed Wisdom save.",
  "icon": "hold_person",
  "targetType": "singleUnit",
  "targetFilter": "enemies",
  "maxTargets": 1,
  "range": 18,
  "cost": {
    "usesAction": true,
    "usesBonusAction": false,
    "usesReaction": false,
    "movementCost": 0,
    "resourceCosts": {}
  },
  "saveType": "wisdom",
  "effects": [
    {
      "type": "apply_status",
      "statusId": "paralyzed",
      "statusDuration": 2,
      "condition": "on_save_fail"
    }
  ],
  "requiresConcentration": true,
  "concentrationStatusId": "paralyzed",
  "tags": ["spell", "control", "concentration"]
}
```

### Key Fields
| Field | Values |
|-------|--------|
| `targetType` | `self`, `singleUnit`, `multiUnit`, `circle`, `cone`, `line`, `point`, `all`, `none` |
| `targetFilter` | `"enemies"`, `"Self, Allies"`, `"all"` |
| `cost.usesAction/usesBonusAction/usesReaction` | bool |
| `cost.resourceCosts` | `{"spell_slot_1": 1}` etc. |
| `attackType` | `"meleeWeapon"`, `"rangedWeapon"`, `"meleeSpell"`, `"rangedSpell"` or absent |
| `saveType` | `"wisdom"`, `"dexterity"`, `"constitution"`, etc. |
| `effects[].type` | `"damage"`, `"heal"`, `"apply_status"`, `"remove_status"`, `"modify_resource"`, `"teleport"`, `"forced_move"`, `"spawn_surface"`, `"summon"`, `"interrupt"`, `"counter"`, `"grant_action"` |
| `effects[].condition` | `"on_hit"`, `"on_crit"`, `"on_save_fail"` |
| `tags` | Freeform: `"spell"`, `"melee_attack"`, `"finesse"`, `"ranged"`, `"concentration"`, etc. |

---

## 9. Status JSON Schema

Example from [Data/Statuses/bg3_mechanics_statuses.json](Data/Statuses/bg3_mechanics_statuses.json):
```json
{
  "id": "prone",
  "name": "Prone",
  "description": "Knocked to the ground.",
  "icon": "prone",
  "durationType": "turns",
  "defaultDuration": 1,
  "maxStacks": 1,
  "stacking": "refresh",
  "isBuff": false,
  "isDispellable": true,
  "tags": ["control", "debuff"],
  "modifiers": [
    { "target": "attackRoll", "type": "disadvantage", "value": 1 },
    { "target": "savingThrow", "type": "disadvantage", "value": 1 },
    { "target": "movementSpeed", "type": "percentage", "value": -50 }
  ],
  "blockedActions": []
}
```

### Key Fields
| Field | Values |
|-------|--------|
| `durationType` | `"turns"`, `"rounds"`, `"permanent"`, `"untilEvent"` |
| `stacking` | `"refresh"`, `"replace"`, `"extend"`, `"stack"`, `"unique"` |
| `modifiers[].target` | `"attackRoll"`, `"savingThrow"`, `"armorClass"`, `"movementSpeed"`, `"damageDealt"`, `"damageTaken"`, `"healingReceived"` |
| `modifiers[].type` | `"flat"`, `"percentage"`, `"advantage"`, `"disadvantage"` |
| `blockedActions` | `["*"]` (all), `["action"]`, `["bonus_action"]`, `["reaction"]`, `["movement"]`, or specific ability IDs |
| `tickEffects` | `[{ "effectType": "damage", "value": 5, "damageType": "fire" }]` |
| `triggerEffects` | `[{ "triggerOn": "OnDamageTaken", "effectType": "damage", ... }]` |
| `removeOnEvent` | `"DamageTaken"` etc. (for `untilEvent` duration) |

---

## 10. Build & Test Commands

| Task | Command | File |
|------|---------|------|
| **Build** | `./scripts/ci-build.sh` | [scripts/ci-build.sh](scripts/ci-build.sh) — Builds Release + Debug |
| **Test** | `./scripts/ci-test.sh` | [scripts/ci-test.sh](scripts/ci-test.sh) — `dotnet test` excluding benchmarks |
| **Auto-battle** | `./scripts/run_autobattle.sh --seed 42 --freeze-timeout 20` | Quick combat verification |
| **Headless tests** | `./scripts/run_headless_tests.sh` | Service/registry validation |
| **Screenshots** | `./scripts/run_screenshots.sh` | Visual capture under Xvfb |

### Build commands:
```bash
# ci-build.sh contents:
dotnet build "$SLN" -c Release
dotnet build QDND.csproj -c Debug -v q

# ci-test.sh contents:
dotnet test "$SLN" -c Release --no-build --filter "FullyQualifiedName!~CIBenchmarkGateTests"
```

---

## Quick-Reference: Key File Index

| Concern | Primary File | Key Method |
|---------|-------------|------------|
| Ability execution | [Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs#L211) | `ExecuteAbility()` |
| Attack rolls | [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs#L248) | `RollAttack()` |
| Save rolls | [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs#L365) | `RollSave()` |
| Damage application | [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs#L223) | `DealDamageEffect.Execute()` |
| Healing | [Combat/Abilities/Effects/Effect.cs](Combat/Abilities/Effects/Effect.cs#L356) | `HealEffect.Execute()` |
| HP/TempHP | [Combat/Entities/Combatant.cs](Combat/Entities/Combatant.cs#L34) | `ResourceComponent.TakeDamage()` |
| Life state | [Combat/Entities/CombatantLifeState.cs](Combat/Entities/CombatantLifeState.cs) | enum |
| Modifier stack | [Combat/Rules/Modifier.cs](Combat/Rules/Modifier.cs#L165) | `ModifierStack.Apply()` |
| Status management | [Combat/Statuses/StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L335) | `ApplyStatus()` |
| Status → modifiers | [Combat/Statuses/StatusSystem.cs](Combat/Statuses/StatusSystem.cs#L130) | `StatusInstance.CreateModifiers()` |
| Height modifiers | [Combat/Environment/HeightService.cs](Combat/Environment/HeightService.cs#L93) | `GetAttackModifier()` |
| Adv/dis on attacks | [Combat/Abilities/EffectPipeline.cs](Combat/Abilities/EffectPipeline.cs#L489) | `GetStatusAttackContext()` |
| AI decisions | [Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L161) | `MakeDecision()` |
| AI disabled Dash/Disengage | [Combat/AI/AIDecisionPipeline.cs](Combat/AI/AIDecisionPipeline.cs#L230) | commented blocks |
| Arena orchestration | [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L1537) | `ExecuteAbility()` |
| basic_attack definition | [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L804) | `RegisterDefaultAbilities()` |
| Victory check | [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs#L2112) | `EndCombat()` |
| Class defs (ExtraAttacks) | [Data/CharacterModel/ClassDefinition.cs](Data/CharacterModel/ClassDefinition.cs#L73) | `LevelProgression.ExtraAttacks` |
| Ability JSON | [Data/Abilities/bg3_mechanics_abilities.json](Data/Abilities/bg3_mechanics_abilities.json) | |
| Status JSON | [Data/Statuses/bg3_mechanics_statuses.json](Data/Statuses/bg3_mechanics_statuses.json) | |
| Contested checks | [Combat/Rules/RulesEngine.cs](Combat/Rules/RulesEngine.cs#L402) | `Contest()` |
