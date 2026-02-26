# SH-2: Shove Contested Athletics Check — Implementation Brief

## Problem Summary

Shove currently uses a **DC-based saving throw** (attacker's computed DC vs target's STR save).  
BG3/5e requires a **contested check**: attacker rolls `d20 + Athletics` vs defender rolls `d20 + max(Athletics, Acrobatics)`.

**Audit reference**: [BG3_COMBAT_PARITY_AUDIT_2026.md](BG3_COMBAT_PARITY_AUDIT_2026.md) — SH-2

---

## 1. Current Code Path (How Shove Resolves Today)

### 1.1 Data Definition

**File**: [Data/Actions/common_actions.json](../Data/Actions/common_actions.json#L212-L265)

```json
{
    "id": "shove",
    "saveType": "athletics",       // ← treated as a STR saving throw via ParseAbilityType()
    "effects": [],                 // base has no effects — uses variants
    "variants": [
        {
            "variantId": "shove_push",
            "additionalEffects": [{
                "type": "forced_move",
                "value": 3,
                "condition": "on_save_fail"    // ← gated on save failure
            }]
        },
        {
            "variantId": "shove_prone",
            "additionalEffects": [{
                "type": "apply_status",
                "statusId": "prone",
                "condition": "on_save_fail"    // ← gated on save failure
            }]
        }
    ]
}
```

### 1.2 Resolution Flow

When `EffectPipeline.ExecuteAction()` runs for shove:

1. **Variant auto-selection** — [EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs#L591-L594): since `action.Effects.Count == 0` and variants exist, auto-selects first variant (`shove_push`).

2. **Save roll** — [EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs#L1010-L1120): because `action.SaveType == "athletics"` (non-empty), the standard save logic runs:
   - `ComputeSaveDC()` ([L2308](../Combat/Actions/EffectPipeline.cs#L2308)): computes `8 + proficiency + max(STR, DEX)` — **wrong for a contest**
   - `GetSavingThrowBonus()` ([L2288](../Combat/Actions/EffectPipeline.cs#L2288)): `ParseAbilityType("athletics")` → `AbilityType.Strength` → target's STR modifier + save proficiency bonus — **wrong; should be skill bonus, not save bonus**
   - `Rules.RollSave()` rolls d20 + save bonus vs DC — **should be two-sided d20 contest**

3. **Effect condition check** — [ForcedMoveEffect.cs](../Combat/Actions/Effects/ForcedMoveEffect.cs#L40) and [ApplyStatusEffect.cs](../Combat/Actions/Effects/ApplyStatusEffect.cs#L41): check `"on_save_fail"` → calls `context.DidTargetFailSave(target.Id)` → reads `PerTargetSaveResults` → uses `QueryResult.IsSuccess`.

### 1.3 What's Wrong

| Aspect | Current (wrong) | BG3/5e (correct) |
|--------|-----------------|-------------------|
| Attacker roll | Static DC (8+prof+mod) | d20 + Athletics bonus |
| Defender roll | d20 + STR save bonus | d20 + max(Athletics, Acrobatics) bonus |
| Roll type | One-sided save | Two-sided contested check |
| Proficiency | Save proficiency | Skill proficiency (Athletics/Acrobatics) |
| Advantage source | Hiding → invisible → advantage on save? | Hiding/invisible → advantage on attacker's check |

---

## 2. Existing Infrastructure (What's Already Built)

### 2.1 `RulesEngine.Contest()` — Fully Implemented

**File**: [Combat/Rules/RulesEngine.cs](../Combat/Rules/RulesEngine.cs#L849-L1009)

```csharp
public ContestResult Contest(
    Combatant attacker,
    Combatant defender,
    int attackerMod,
    int defenderMod,
    string attackerSkill = "Check",
    string defenderSkill = "Check",
    TiePolicy tiePolicy = TiePolicy.DefenderWins)
```

- Rolls d20 for both sides with full advantage/disadvantage resolution via `ModifierStack`
- Returns `ContestResult` with `AttackerWon`, `DefenderWon`, `BreakdownA`, `BreakdownB`, `Margin`
- **Currently unused anywhere** — zero callers in the codebase

### 2.2 `ContestResult` Class — Fully Implemented

**File**: [Combat/Rules/RulesEngine.cs](../Combat/Rules/RulesEngine.cs#L55-L86)

Has `RollA`, `RollB`, `NaturalRollA`, `NaturalRollB`, `Winner`, `BreakdownA`, `BreakdownB`, `Margin`, `AttackerWon`, `DefenderWon`.

### 2.3 `ResolvedCharacter.GetSkillBonus()` — Fully Implemented

**File**: [Data/CharacterModel/CharacterResolver.cs](../Data/CharacterModel/CharacterResolver.cs#L882-L893)

```csharp
public int GetSkillBonus(Skill skill, int proficiencyBonus)
{
    var ability = GetSkillAbility(skill);
    int abilityMod = GetModifier(ability);
    if (Proficiencies.HasExpertise(skill)) return abilityMod + proficiencyBonus * 2;
    if (Proficiencies.IsProficientInSkill(skill)) return abilityMod + proficiencyBonus;
    return abilityMod;
}
```

Handles proficiency and expertise correctly. Used by HUD display code already.

### 2.4 `Skill` Enum

**File**: [Data/CharacterModel/Enums.cs](../Data/CharacterModel/Enums.cs#L7)

```csharp
public enum Skill { Athletics, Acrobatics, ... }
```

### 2.5 BG3 Reference — `ShoveCheck()` in `CommonConditions.khn`

**File**: [BG3_Data/.../CommonConditions.khn](../BG3_Data/Shared%20(BG3%20lots%20of%20data)/Mods/Shared/Scripts/thoth/helpers/CommonConditions.khn#L1726-L1733)

```lua
function ShoveCheck()
    local result = Dead() | Item() | Ally()
    if not result.Result then
        local skillCheck = SkillCheck(
            Skill.Athletics,
            math.max(context.Target.GetPassiveSkill(Skill.Athletics),
                     context.Target.GetPassiveSkill(Skill.Acrobatics)),
            IsSneakingOrInvisible()
        )
        return ConditionResult(skillCheck.Result,{},{},skillCheck.Chance)
    end
    return result
end
```

Key observations:
- Attacker uses **Athletics**
- Defender uses **max(Athletics, Acrobatics)** as DC (but BG3 uses passive = 10 + bonus; in actual contest both roll)
- `IsSneakingOrInvisible()` grants **advantage** on the attacker's check

---

## 3. Implementation Plan

### 3.1 Add `GetSkillBonus()` to `Combatant`

**File**: [Combat/Entities/Combatant.cs](../Combat/Entities/Combatant.cs#L219) — add after `GetAbilityModifier()`

```csharp
/// <summary>Get the skill check bonus, including proficiency/expertise if applicable.</summary>
public int GetSkillBonus(Skill skill)
{
    if (ResolvedCharacter != null)
        return ResolvedCharacter.GetSkillBonus(skill, GetProficiencyBonus());
    // Fallback for old-style units: use raw ability modifier
    return GetAbilityModifier(ResolvedCharacter.GetSkillAbility(skill));
}
```

This is essential because currently only `ResolvedCharacter` has `GetSkillBonus()`, and the caller needs to manually pass `proficiencyBonus`. Having it on `Combatant` directly makes the contest code cleaner.

### 3.2 Add `ContestResult` Field to `EffectContext`

**File**: [Combat/Actions/Effects/Effect.cs](../Combat/Actions/Effects/Effect.cs#L65) — add alongside `SaveResult`

```csharp
/// <summary>
/// Result of a contested check (e.g., shove). When present, on_save_fail/on_contest_fail
/// conditions check this instead of SaveResult.
/// </summary>
public ContestResult ContestResult { get; set; }
```

Add a helper property:
```csharp
/// <summary>Whether the attacker lost a contested check (contest was rolled and defender won).</summary>
public bool ContestFailed => ContestResult != null && !ContestResult.AttackerWon;
```

### 3.3 Add `ContestResult` Field to `ActionExecutionResult`

**File**: [Combat/Actions/EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs#L64) — add alongside `SaveResult`

```csharp
public ContestResult ContestResult { get; set; }
```

### 3.4 Add `ResolutionType` to `ActionDefinition`

**File**: [Combat/Actions/ActionDefinition.cs](../Combat/Actions/ActionDefinition.cs#L184) — add before `SaveType`

```csharp
/// <summary>
/// How this action determines success/failure.
/// "save" (default): standard DC-based saving throw.
/// "contest": opposed skill check (e.g., shove: Athletics vs max(Athletics, Acrobatics)).
/// </summary>
public string ResolutionType { get; set; } = "save";

/// <summary>
/// For contest resolution: attacker's skill (e.g., "athletics").
/// </summary>
public string ContestAttackerSkill { get; set; }

/// <summary>
/// For contest resolution: defender's skill(s). If multiple, defender uses the highest.
/// Comma-separated, e.g., "athletics,acrobatics".
/// </summary>
public string ContestDefenderSkills { get; set; }
```

### 3.5 Update `common_actions.json` — Shove Action

**File**: [Data/Actions/common_actions.json](../Data/Actions/common_actions.json#L212-L265)

```json
{
    "id": "shove",
    "name": "Shove",
    "description": "Use your Athletics to push a target or knock them prone...",
    "resolutionType": "contest",
    "contestAttackerSkill": "athletics",
    "contestDefenderSkills": "athletics,acrobatics",
    "saveType": null,
    "effects": [],
    "variants": [
        {
            "variantId": "shove_push",
            "additionalEffects": [{
                "type": "forced_move",
                "value": 3,
                "condition": "on_contest_success"
            }]
        },
        {
            "variantId": "shove_prone",
            "additionalEffects": [{
                "type": "apply_status",
                "statusId": "prone",
                "statusDuration": 1,
                "condition": "on_contest_success"
            }]
        }
    ]
}
```

> **Backward compat note**: Existing `"on_save_fail"` conditions should continue to work. Alternatives:
> - Option A: Change to `"on_contest_success"` (cleaner semantics, more changes).
> - **Option B (recommended)**: Keep `"on_save_fail"` in the JSON. When a contest resolves, map `ContestResult.AttackerWon` → `SaveResult.IsSuccess = false` (defender failed). This makes ALL existing effect condition checks work without modification. See §3.6.

### 3.6 Add Contest Resolution to `EffectPipeline.ExecuteAction()` — Core Change

**File**: [Combat/Actions/EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs#L1009-L1120)

**Strategy**: Insert a new block **before** the existing save logic. When `resolutionType == "contest"`, run the contest via `RulesEngine.Contest()`, then **synthesize a SaveResult** from the `ContestResult` so that all downstream `on_save_fail` condition checks work unchanged.

Insert before line 1009 (`// Roll save if needed`):

```csharp
// --- Contested check resolution (e.g., Shove) ---
if (string.Equals(action.ResolutionType, "contest", StringComparison.OrdinalIgnoreCase)
    && targets.Count > 0)
{
    foreach (var target in targets)
    {
        // Compute attacker's skill bonus
        int attackerMod = GetContestSkillBonus(source, action.ContestAttackerSkill);
        
        // Compute defender's skill bonus (uses best of listed skills)
        int defenderMod = GetBestContestSkillBonus(target, action.ContestDefenderSkills);
        
        string attackerSkillName = action.ContestAttackerSkill ?? "Athletics";
        string defenderSkillName = action.ContestDefenderSkills ?? "Athletics";
        
        // Run the contest
        var contestResult = Rules.Contest(
            source, target,
            attackerMod, defenderMod,
            attackerSkillName, defenderSkillName,
            TiePolicy.DefenderWins);
        
        result.ContestResult = contestResult;
        context.ContestResult = contestResult;
        
        // Synthesize a SaveResult so downstream on_save_fail conditions work unchanged.
        // Convention: if attacker won the contest, the "save" failed (defender lost).
        var syntheticSave = new QueryResult
        {
            Input = new QueryInput
            {
                Type = QueryType.Contest,
                Source = source,
                Target = target,
                DC = contestResult.RollA,          // attacker's roll as "DC"
                BaseValue = defenderMod
            },
            BaseValue = defenderMod,
            NaturalRoll = contestResult.NaturalRollB,
            FinalValue = contestResult.RollB,
            IsSuccess = contestResult.DefenderWon   // defender "saved" = defender won
        };
        
        context.SaveResult = syntheticSave;
        result.SaveResult = syntheticSave;
        context.PerTargetSaveResults[target.Id] = syntheticSave;
    }
}
```

Refactor the existing save block to skip when contest already handled:

```csharp
// Roll save if needed (skip if contest already resolved)
if (!string.IsNullOrEmpty(action.SaveType) 
    && !string.Equals(action.ResolutionType, "contest", StringComparison.OrdinalIgnoreCase)
    && targets.Count > 0)
{
    // ... existing save logic unchanged ...
}
```

### 3.7 Add Skill Bonus Helper Methods to `EffectPipeline`

**File**: [Combat/Actions/EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs) — add near `GetSavingThrowBonus()` (around L2288)

```csharp
/// <summary>
/// Get the skill check bonus for a contested check attacker.
/// </summary>
private int GetContestSkillBonus(Combatant combatant, string skillName)
{
    if (combatant == null || string.IsNullOrEmpty(skillName))
        return 0;
    
    if (Enum.TryParse<Skill>(skillName, true, out var skill))
    {
        return combatant.GetSkillBonus(skill);
    }
    
    // Fallback: treat as raw ability
    var ability = ParseAbilityType(skillName);
    return ability.HasValue ? combatant.GetAbilityModifier(ability.Value) : 0;
}

/// <summary>
/// Get the best skill bonus for the defender from a comma-separated list of skills.
/// BG3 Shove: defender uses max(Athletics, Acrobatics).
/// </summary>
private int GetBestContestSkillBonus(Combatant combatant, string skillNames)
{
    if (combatant == null || string.IsNullOrEmpty(skillNames))
        return 0;
    
    int best = int.MinValue;
    foreach (var name in skillNames.Split(','))
    {
        int bonus = GetContestSkillBonus(combatant, name.Trim());
        if (bonus > best) best = bonus;
    }
    
    return best == int.MinValue ? 0 : best;
}
```

### 3.8 Add `ContestResult` to `EffectContext`

Already described in §3.2. Also wire up `DidTargetFailSave()` to check contest result:

The existing `DidTargetFailSave()` method already works via `PerTargetSaveResults` since we synthesize a `QueryResult` from the contest. No changes needed to `DidTargetFailSave()`.

### 3.9 JSON Deserialization — Add Contest Fields

**File**: Wherever `ActionDefinition` is deserialized from JSON (likely [Data/Actions/ActionRegistry.cs](../Data/Actions/) or a helper parser).

Find where `saveType`, `saveDC` are deserialized and add:
```csharp
action.ResolutionType = GetStringOrNull(actionObj, "resolutionType") ?? "save";
action.ContestAttackerSkill = GetStringOrNull(actionObj, "contestAttackerSkill");
action.ContestDefenderSkills = GetStringOrNull(actionObj, "contestDefenderSkills");
```

Search for the deserialization site:

```
grep -r "saveType" Combat/Actions/ Data/Actions/ --include="*.cs" -l
```

### 3.10 AI Scoring — Contest Success Probability

**File**: [Combat/AI/AIScorer.cs](../Combat/AI/AIScorer.cs#L428) — `ScoreShove()`

Add a contested check success probability estimate to weight the score:

```csharp
// Estimate contest success probability
float successProbability = EstimateContestProbability(actor, target);
score *= successProbability;
breakdown["contest_probability"] = successProbability;
```

Add helper method:
```csharp
/// <summary>
/// Estimate probability of winning a Shove contest.
/// Attacker: d20 + Athletics vs Defender: d20 + max(Athletics, Acrobatics).
/// Uses the simple formula: P(win) ≈ 0.5 + (attackerMod - defenderMod) * 0.05, clamped [0.05, 0.95].
/// </summary>
private float EstimateContestProbability(Combatant attacker, Combatant target)
{
    int atkBonus = attacker.GetSkillBonus(Skill.Athletics);
    int defAthletics = target.GetSkillBonus(Skill.Athletics);
    int defAcrobatics = target.GetSkillBonus(Skill.Acrobatics);
    int defBonus = Math.Max(defAthletics, defAcrobatics);
    
    float diff = atkBonus - defBonus;
    float probability = 0.5f + diff * 0.05f;
    return Math.Clamp(probability, 0.05f, 0.95f);
}
```

### 3.11 Combat Log / HUD Preview

Update the preview tooltip for shove to show "Athletics Check" instead of "Save DC":

**Files to check**:
- [Combat/UI/HudController.cs](../Combat/UI/HudController.cs) — action tooltip
- [Combat/Arena/CombatHUD.cs](../Combat/Arena/CombatHUD.cs) — combat log formatting

When `action.ResolutionType == "contest"`, display:
- "Your Athletics: +X vs Target's Best (Athletics/Acrobatics): +Y"

---

## 4. Edge Cases

### 4.1 Advantage/Disadvantage on Contest Checks

`RulesEngine.Contest()` already handles this via `ModifierStack.ResolveAdvantage(ModifierTarget.SkillCheck, ...)`. Sources of advantage:
- **Hiding/Invisible**: BG3 grants advantage on the attacker's Athletics check when sneaking. Need to add a modifier source for `"contest"` + `"athletics"` tags when attacker has `hidden` or `invisible` status.
- **Prone attacker**: In 5e, a prone creature has disadvantage on Athletics checks. Should be covered by status modifiers adding disadvantage on `SkillCheck`.
- **Enhance Ability (Bull's Strength)**: Grants advantage on STR checks. Should already work if status applies advantage to `SkillCheck` with STR tag.

### 4.2 Size Bonuses

BG3 does NOT add explicit size bonuses to shove checks (the size restriction in §SH-4 is binary: can/cannot shove). Already implemented in `TargetValidator.IsValidShoveSize()` ([Combat/Targeting/TargetValidator.cs](../Combat/Targeting/TargetValidator.cs#L231)).

### 4.3 Monsters Without `ResolvedCharacter`

`Combatant.GetSkillBonus()` needs a fallback for old-style units that don't have a `ResolvedCharacter`:
```csharp
if (ResolvedCharacter == null)
    return GetAbilityModifier(ResolvedCharacter.GetSkillAbility(skill));
```
This uses raw ability modifier (no proficiency), which is correct for basic monsters.

### 4.4 `charge_shove` (Charger Feat)

**File**: [Data/Actions/bg3_mechanics_actions.json](../Data/Actions/bg3_mechanics_actions.json#L5972)

Currently uses `"condition": "on_hit"` (no save at all). In BG3, Charge: Shove also uses `ShoveCheck()`. Decision point: should this also become a contest? If yes, update it similarly. If no, leave as-is (minor parity gap, lower priority).

### 4.5 Grapple (Future)

The same contest infrastructure can be reused for Grapple (STR Athletics vs target STR Athletics or DEX Acrobatics). The `resolutionType: "contest"` pattern is generic enough.

---

## 5. Files Modified (Summary)

| # | File | Change |
|---|------|--------|
| 1 | [Combat/Entities/Combatant.cs](../Combat/Entities/Combatant.cs) | Add `GetSkillBonus(Skill)` method |
| 2 | [Combat/Actions/ActionDefinition.cs](../Combat/Actions/ActionDefinition.cs) | Add `ResolutionType`, `ContestAttackerSkill`, `ContestDefenderSkills` properties |
| 3 | [Combat/Actions/EffectPipeline.cs](../Combat/Actions/EffectPipeline.cs) | Add contest resolution block before save logic; add skill bonus helpers; add `ContestResult` to `ActionExecutionResult` |
| 4 | [Combat/Actions/Effects/Effect.cs](../Combat/Actions/Effects/Effect.cs) | Add `ContestResult` field to `EffectContext` |
| 5 | [Data/Actions/common_actions.json](../Data/Actions/common_actions.json) | Update shove action: add `resolutionType`, `contestAttackerSkill`, `contestDefenderSkills`; remove `saveType` |
| 6 | JSON deserialization code | Parse new contest fields |
| 7 | [Combat/AI/AIScorer.cs](../Combat/AI/AIScorer.cs) | Add contest success probability to shove scoring |
| 8 | [Combat/AI/AIWeights.cs](../Combat/AI/AIWeights.cs) | (Optional) adjust `ShoveBaseCost` comment |
| 9 | Combat log / HUD preview | Show "Athletics Contest" instead of "Save DC" for contest actions |

---

## 6. Testing Plan

### 6.1 Unit Tests (New File: `Tests/Unit/ContestedCheckTests.cs`)

1. **Contest_AttackerHigherMod_WinsMoreOften** — Seed 100 contests, verify attacker with +5 vs defender +0 wins >60%.
2. **Contest_DefenderWinsOnTie** — Force identical rolls, verify defender wins with default `TiePolicy`.
3. **Contest_SkillBonusIncludesProficiency** — Create combatant with Athletics proficiency, verify bonus includes prof.
4. **Contest_DefenderUsesBestSkill** — Defender has Athletics +2, Acrobatics +5 → should use +5.
5. **Shove_UsesContestNotSave** — Execute shove action, verify `ContestResult` is populated and `SaveDC` is NOT used.
6. **Shove_Push_AppliesOnContestWin** — Mock contest win → forced_move effect fires.
7. **Shove_Push_NoEffectOnContestLoss** — Mock contest loss → no forced_move.
8. **Shove_Prone_AppliesOnContestWin** — Mock contest win → prone status applied.

### 6.2 Integration Tests

9. **AIScorer_ShoveScoring_IncludesContestProbability** — Verify score is modulated by probability.
10. **EffectPipeline_Shove_EndToEnd** — Full pipeline execution with seeded RNG.

### 6.3 Autobattle Verification

After implementation:
```bash
./scripts/run_autobattle.sh --seed 42 --freeze-timeout 15
./scripts/run_autobattle.sh --seed 1234 --loop-threshold 20
```

---

## 7. Risk Assessment

| Risk | Mitigation |
|------|-----------|
| Downstream code checks `SaveResult` for shove | Synthetic `QueryResult` maps contest → save result. All `on_save_fail` checks work unchanged. |
| Combat log formatting breaks | Contest has its own breakdown strings. May need log format update. |
| Monsters without `ResolvedCharacter` crash on `GetSkillBonus` | Fallback to raw ability modifier. |
| AI scoring changes shift balance | Contest probability is an additional multiplier, doesn't change tactical preferences. |
| `charge_shove` regression | Separate action, currently uses `on_hit` not save. Unchanged unless explicitly updated. |

---

## 8. Implementation Order

1. Add `GetSkillBonus(Skill)` to `Combatant` (§3.1) — zero risk, no callers yet
2. Add `ResolutionType` + contest fields to `ActionDefinition` (§3.4) — additive only
3. Add `ContestResult` field to `EffectContext` and `ActionExecutionResult` (§3.2, §3.3)
4. Add contest resolution block in `EffectPipeline.ExecuteAction()` + helpers (§3.6, §3.7)
5. Update JSON deserialization (§3.9)
6. Update `common_actions.json` (§3.5)
7. Update AI scorer (§3.10)
8. Write tests (§6)
9. Run CI gates + autobattle verification
