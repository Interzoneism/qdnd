# Ability Fix Workflow

Quick-reference guide for diagnosing and fixing abilities that "don't do anything."

## Diagnostic Steps

### 1. Find the action definition
```bash
grep -r "\"id\": \"ACTION_NAME\"" Data/Actions/ Data/Spells/
# or
grep -r "ACTION_NAME" --include="*.json" Data/
```

### 2. Check the action structure
Read the JSON definition and check:
- Does it have a non-empty `effects` array?
- Does it have `variants` with `additionalEffects`?
- What is the `targetType`? (singleUnit, self, all, none, circle, cone, line, point)
- What is the `targetFilter`? (self, enemies, allies, none)
- What is the `cost`? (usesAction, usesBonusAction, etc.)

### 3. Identify the failure pattern

| Pattern | Symptoms | Root Cause |
|---------|----------|------------|
| **Empty base effects + variants** | Action executes but nothing happens | No variant selected, effects only in `additionalEffects` |
| **Point-targeted self-effect** | Can target ground but caster isn't moved/affected | Target resolution finds combatants at point, not caster |
| **Missing effect handler** | Action executes, effect type logged but no result | Effect type not registered in EffectPipeline |
| **Wrong targetType** | Can't target or auto-executes wrong | TargetType mismatch with intended behavior |
| **Condition not met** | Effect has `condition: "on_save_fail"` but save always passes | Save logic issue or missing saveType |
| **Service not wired** | Effect needs ForcedMovementService/SurfaceManager but it's null | Arena service wiring incomplete |

## Common Fixes

### Pattern A: Empty base effects with variants (Shove-style)
**Problem:** Action has `"effects": []` but variants contain `additionalEffects`.

**Solution:** Already fixed in `EffectPipeline.ExecuteAction()` - auto-selects first variant when:
- No variant ID provided
- Base effects array is empty
- Action has variants with effects

If a new action needs this, verify the JSON structure matches this pattern.

### Pattern B: Point-targeted self-affecting abilities (Misty Step, Jump)
**Problem:** Action has `targetType: "point"` and `targetFilter: "self"` but the caster doesn't get teleported/affected.

**Root cause:** `ExecuteAbilityAtPosition()` was using `ResolveAreaTargets()` which finds combatants *at the destination*, not the caster.

**Solution:** Fixed in `CombatArena.ExecuteAbilityAtPosition()` - for point-targeted abilities where `targetFilter` is `Self` or `None`, the caster is added as the target instead of resolving area targets.

**JSON pattern that triggers this:**
```json
{
  "targetType": "point",
  "targetFilter": "self",
  "effects": [{ "type": "teleport" }]
}
```

### Pattern C: Effect type not registered
**Problem:** Effect type string (e.g., `"teleport"`, `"forced_move"`) has no handler.

**Solution:** 
1. Check `EffectPipeline.RegisterDefaultEffects()` for the effect type
2. If missing, add: `RegisterEffect(new YourEffect());`
3. Implement the effect class in `Combat/Actions/Effects/Effect.cs`

### Pattern D: Missing service dependency
**Problem:** Effect handler needs a service (ForcedMovementService, SurfaceManager) but gets null.

**Solution:**
1. Check `CombatArena.InitializeArena()` for service registration
2. Check `EffectPipeline` constructor and property injection
3. Ensure service is passed to `EffectContext` in `ExecuteAction()`

### Pattern E: Targeting mismatch
**Problem:** Action can't be targeted or doesn't show up as valid.

**Solution:**
1. Check `targetType` in JSON matches intended behavior
2. Check `targetFilter` (enemies, allies, any)
3. Check `range` is appropriate
4. Verify `TargetValidator.ValidateSingleTarget()` logic

## Verification Steps

1. **Build:** `dotnet build QDND.csproj`
2. **Unit tests:** `dotnet test Tests/QDND.Tests.csproj --filter "FullyQualifiedName~RELEVANT_TEST"`
3. **Integration test:** Run autobattle with the action available
4. **Manual test:** Load game, use the action in combat

## Key Files

| File | Purpose |
|------|---------|
| `Data/Actions/common_actions.json` | Action definitions (common actions) |
| `Data/Actions/class_actions.json` | Class-specific actions |
| `Data/Spells/*.json` | Spell definitions |
| `Combat/Actions/EffectPipeline.cs` | Effect execution, variant handling |
| `Combat/Actions/Effects/Effect.cs` | Effect type implementations |
| `Combat/Arena/CombatArena.cs` | Action execution entry points |
| `Combat/Targeting/TargetValidator.cs` | Target validation logic |

## Example Investigation Flow

```
1. User reports: "Misty Step doesn't teleport"
2. grep "misty_step" Data/Actions/ -> find definition
3. Read JSON: targetType="point", targetFilter="self", effects=[{type:"teleport"}]
4. Identified: Pattern B (point-targeted self-affect)
5. Check CombatArena.ExecuteAbilityAtPosition() for self-target handling
6. Add fix: if targetType==point && targetFilter==self, add caster to targets
7. Build, test, verify
```
