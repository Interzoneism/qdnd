# ⛔ DEPRECATED — Rules Windows & Passive Pipeline

> **STATUS: DELETED SYSTEM — DO NOT IMPLEMENT**
>
> This document described the legacy `PassiveRuleService` + `bg3_passive_rules.json` pipeline.
> That system has been **removed from the codebase**.
>
> ## Current System
>
> Passives are now handled by:
> - **`Data/Passives/PassiveRegistry`** — loads passive definitions from BG3 data
> - **`Combat/Rules/Boosts/BoostApplicator`** — applies parsed boosts to combatants
> - **`Combat/Rules/Boosts/BoostParser`** — parses BG3 boost DSL strings
>
> Rule windows (`RuleWindowBus`, `IRuleProvider`) still exist and are the correct
> mechanism for passive effects that need combat-event hooks. But the registration
> and data-loading path is through `PassiveRegistry`, not the deleted `PassiveRuleService`.
>
> See: `AGENTS.md` § Architecture Rules, `CODING_STANDARDS.md` § Architecture Rules
