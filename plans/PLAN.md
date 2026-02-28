# BG3 Combat Parity Remediation Plan (Prioritized, Surprise/Ambush Excluded)

## Summary
This plan fixes the highest-impact combat parity gaps from the audit, in delivery order `P0 -> P1 -> P2`, and explicitly excludes surprise/ambush mechanics.  
Primary objective: make reaction legality/resource behavior and core turn-by-turn combat outcomes match BG3 expectations without broad refactors.

## Scope
1. In scope: reaction legality/resource enforcement, Shield vs Magic Missile timing, `blockedActions` normalization, death-save modifier path, concentration/prone behavior, mixed action economy attack-pool behavior, high-ground damage parity.
2. Out of scope: surprise and ambush flows (explicitly excluded).

## Public API / Interface / Type Changes
1. Extend [`ActionExecutionOptions`](/home/martin/repos/qdnd/Combat/Actions/ActionVariant.cs#L208):
`IgnoreReactionBudgetCheck` (bool, default `false`) and `SkipReactionBudgetConsumption` (bool, default `false`).
2. Extend [`CanUseAbilityWithCost(...)`](/home/martin/repos/qdnd/Combat/Actions/EffectPipeline.cs#L1912) signature:
add `ignoreReactionBudgetCheck` parameter (default `false`).
3. Extend [`ReactionSystem`](/home/martin/repos/qdnd/Combat/Reactions/ReactionSystem.cs#L11):
add optional callback `AdditionalEligibilityCheck` (`Func<Combatant, ReactionDefinition, ReactionTriggerContext, bool>`), invoked from `CanTrigger(...)`.
4. Add status action-block normalization helper in status runtime path (new static helper in `Combat/Statuses`, used by both [`StatusManager.RegisterStatus(...)`](/home/martin/repos/qdnd/Combat/Statuses/StatusSystem.cs#L949) and [`DataRegistry.RegisterStatus(...)`](/home/martin/repos/qdnd/Data/DataRegistry.cs#L144)).

## P0 (Blocker) — Implement First

### 1) Reaction legality/resource enforcement
Implementation:
1. Update reaction action execution in [`ReactionCoordinator.ExecuteReactionAction(...)`](/home/martin/repos/qdnd/Combat/Services/ReactionCoordinator.cs#L179) to run with `SkipCostValidation=false`, `IgnoreReactionBudgetCheck=true`, `SkipReactionBudgetConsumption=true`, `SkipRangeValidation=true`.
2. In [`EffectPipeline.ExecuteAction(...)`](/home/martin/repos/qdnd/Combat/Actions/EffectPipeline.cs#L570), when consuming budget, skip only budget-side reaction consumption if `SkipReactionBudgetConsumption=true`; continue BG3 resource consumption (`ConsumeBG3ResourceCost`) unchanged.
3. In [`CanUseAbilityWithCost(...)`](/home/martin/repos/qdnd/Combat/Actions/EffectPipeline.cs#L1912), keep status-block checks against original cost, but for `ActionBudget.CanPayCost` use a cloned cost with `UsesReaction=false` when `ignoreReactionBudgetCheck=true`.
4. Add `ReactionSystem.AdditionalEligibilityCheck`; wire it from combat init so reaction prompts are filtered with `_effectPipeline.CanUseAbility(reaction.ActionId, reactor)` before prompt creation.
5. Keep current semantics that confirming a reaction spends reaction budget; this pass prevents invalid prompt/execution paths rather than changing spend timing model.

Acceptance:
1. No reaction prompt if reactor is status-blocked, silenced, or cannot pay required resources/spell slot.
2. Reaction spells consume spell slots/resources correctly.
3. No double reaction-budget consumption.

### 2) Shield reaction for Magic Missile (auto-hit window)
Implementation:
1. In multi-projectile execution path [`ExecuteMultiProjectile(...)`](/home/martin/repos/qdnd/Combat/Actions/EffectPipeline.cs#L1645), for auto-hit projectiles (`!AttackType` + `auto_hit` tag), trigger `TryTriggerAttackReactions(...)` per projectile before effect execution with `attackWouldHit=true`.
2. Ignore AC/roll re-evaluation outputs for auto-hit actions; use this trigger only to allow reaction effects (Shield) to apply status/boost.
3. Keep damage-side shield nullification in [`DealDamageEffect`](/home/martin/repos/qdnd/Combat/Actions/Effects/DealDamageEffect.cs#L553).

Acceptance:
1. Magic Missile offers Shield reaction window.
2. If Shield is used, missile damage resolves to `0` while Shield status is active.
3. If Shield is not used, normal missile damage applies.

### 3) `blockedActions` normalization and enforcement consistency
Implementation:
1. Normalize `BlockedActions` tokens at registration time with canonical mapping:
`bonusAction`/`bonus_action` -> `bonus_action`; `action`, `reaction`, `movement`, `verbal_spell`, `*` -> lowercase canonical.
2. Apply normalization in both [`DataRegistry.RegisterStatus(...)`](/home/martin/repos/qdnd/Data/DataRegistry.cs#L144) and [`StatusManager.RegisterStatus(...)`](/home/martin/repos/qdnd/Combat/Statuses/StatusSystem.cs#L949).
3. Keep runtime checker in [`GetBlockedByStatusReason(...)`](/home/martin/repos/qdnd/Combat/Actions/EffectPipeline.cs#L2554) using canonical tokens only after normalization.
4. Add validation test that fails on unknown blocked-action tokens.

Acceptance:
1. Statuses authored with either `bonusAction` or `bonus_action` block bonus actions identically.
2. No regression on existing blocked action types.

## P1 (High Value)

### 1) Death-save roll path uses roll modifiers/advantage model
Implementation:
1. Replace raw RNG in [`TurnLifecycleService.ProcessDeathSave(...)`](/home/martin/repos/qdnd/Combat/Services/TurnLifecycleService.cs#L374) with a helper that rolls via rules/boost model:
advantage/disadvantage from `BoostEvaluator` (`RollType.DeathSave`), bonus dice from `GetRollBonusDice`, minimum roll floor from `GetMinimumRollResult`.
2. Preserve BG3/5e outcome semantics: natural 1 and natural 20 handled by natural die result before modifiers.

Acceptance:
1. Death-save-affecting boosts modify roll outcomes.
2. Natural 1/20 behavior remains unchanged.

### 2) Remove prone-triggered concentration checks
Implementation:
1. Remove prone branch in [`ConcentrationSystem.OnStatusApplied(...)`](/home/martin/repos/qdnd/Combat/Statuses/ConcentrationSystem.cs#L257).
2. Keep concentration checks on damage and incapacitating status events only.

Acceptance:
1. Applying Prone alone does not trigger concentration check.
2. Damage/incapacitation concentration behavior unchanged.

### 3) Fix mixed action-economy attack pool behavior
Implementation:
1. Replace non-weapon action flow in [`EffectPipeline.ExecuteAction(...)`](/home/martin/repos/qdnd/Combat/Actions/EffectPipeline.cs#L653) so attack-pool state derives from remaining action charges after non-weapon action consumption.
2. Add `ActionBudget.RefreshAttacksFromAvailableActions()`:
`AttacksRemaining = (ActionCharges > 0 ? MaxAttacks : 0)`.
3. Use this method after non-weapon action-cost consumption when `UsesAction=true`.

Acceptance:
1. Casting a spell after gaining an extra action still allows weapon attacks from remaining action.
2. Single-action turns still end with `AttacksRemaining=0` after non-weapon action use.

## P2 (Parity Tightening)

### 1) Remove high-ground ranged damage multiplier
Implementation:
1. In [`HeightService.GetDamageModifier(...)`](/home/martin/repos/qdnd/Combat/Environment/HeightService.cs#L104), return `1f` for all states.
2. Keep only attack-roll height modifier (`+2/-2`) in [`GetAttackModifier(...)`](/home/martin/repos/qdnd/Combat/Environment/HeightService.cs#L90).

Acceptance:
1. High ground influences hit chance, not damage, for ranged attacks.

### 2) Targeted BG3 status-boost parser support (combat-critical subset only)
Implementation:
1. Extend [`BG3StatusIntegration.ParseBoosts(...)`](/home/martin/repos/qdnd/Data/Statuses/BG3StatusIntegration.cs#L226) for exactly:
`ActionResourceBlock(ActionPoint|BonusActionPoint|ReactionActionPoint|Movement)`,
`IgnoreLeaveAttackRange`,
`AbilityFailedSavingThrow(Strength|Dexterity)`.
2. Map these to existing runtime constructs only; do not widen parser scope in this pass.

Acceptance:
1. Smoke log unsupported-boost volume is reduced for these specific patterns.
2. Corresponding combat effects are observable in targeted tests.

## Test Cases and Scenarios

1. Unit tests:
`Tests/Unit/ReactionSystemTests.cs`, `Tests/Unit/EffectPipelineTests.cs`, `Tests/Unit/TurnLifecycleServiceTests.cs`, `Tests/Unit/ConcentrationSystemTests.cs`, `Tests/Unit/ActionBudgetTests.cs`.
2. New scenario tests:
Magic Missile vs Shield reaction, silenced reactor cannot trigger shield/counterspell, reaction spell with/without slot availability, extra-action + spell + attack sequence.
3. Full validation gates (required):
`scripts/ci-build.sh`,
`scripts/ci-test.sh`,
`scripts/ci-godot-log-check.sh`.
4. Combat-path verification:
`./scripts/run_autobattle.sh --seed 42 --freeze-timeout 10 --loop-threshold 20`,
`./scripts/run_autobattle.sh --full-fidelity --seed 42`.

## Rollout Order
1. Ship `P0` in one PR (reaction legality + shield auto-hit + blockedActions normalization).
2. Ship `P1` in second PR (death saves + concentration/prone + attack-pool fix).
3. Ship `P2` in third PR (height damage parity + targeted boost parser subset).

## Assumptions and Defaults
1. Surprise/ambush remains excluded from this plan.
2. Reaction spending semantics remain “spent on confirmed reaction”; this pass prevents invalid prompts/executions instead of changing consumption timing model.
3. Shield should be available in auto-hit incoming windows (including Magic Missile) before damage application.
4. No broad refactor of the entire status/boost DSL in this iteration; only explicit P2 subset is added.
