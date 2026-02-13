# Rules Windows & Passive Pipeline

This document describes the Workstream 2 declarative rules core that now powers passive effects.

## Canonical Trigger Windows

`Combat/Rules/RuleWindow.cs` defines the extensible combat windows:

- `BeforeAttackRoll`, `AfterAttackRoll`
- `BeforeDamage`, `AfterDamage`
- `BeforeSavingThrow`, `AfterSavingThrow`
- `OnTurnStart`, `OnTurnEnd`
- `OnMove`, `OnLeaveThreateningArea`, `OnEnterSurface`
- `OnConcentrationCheck`, `OnConcentrationBroken`
- `OnDeclareAction`, `OnActionComplete`

All window payload and mutation data flows through `Combat/Rules/RuleEventContext.cs`.

## Passive Registration Pipeline

`Combat/Rules/PassiveRuleService.cs` loads and registers passive rule providers into `RulesEngine.RuleWindows`.

Data file:

- `Data/Passives/bg3_passive_rules.json`

Selector sources supported by the pipeline:

- class/race/feature IDs and tags
- feat IDs
- equipment item IDs
- status IDs (dynamic registration on `StatusManager` apply/remove events)

## Implemented Passives via Rule Windows

Current data-backed providers include:

- War Caster concentration advantage (`BeforeSavingThrow`)
- Aura of Protection save bonus (`BeforeSavingThrow`)
- Fast Hands extra bonus action (`OnTurnStart`)
- Hasted extra action (`OnTurnStart`, status-driven)
- Fighting Style: Dueling damage bonus (`BeforeDamage`)
- Savage Attacker damage reroll (`BeforeDamage`)

Race/class damage resistances and immunities are now applied by the passive registration service instead of `CombatArena` core-loop hardcoding.

## Integration Points

Rule windows are dispatched from:

- `EffectPipeline` action declaration/completion, attack roll, save roll
- `DealDamageEffect` damage pre/post windows
- `CombatArena` turn start/end and movement windows
- `ConcentrationSystem` concentration check/broken windows

## Notes

- Dueling currently binds to the existing `fighting_style`/`fighting_style_bard` feature IDs.
- Because style choice data is not yet represented in character build JSON, this behaves as a default style binding for those features.
