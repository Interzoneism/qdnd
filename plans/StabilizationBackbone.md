# Plan: BG3 Mechanics Stabilization Backbone (DRAFT)

Core strategy: first fix architecture seams that cause systemic drift (authority, events, persistence), then repair high-risk mechanic calculators and status/effect parity, then close UX truth gaps. This sequence minimizes churn and prevents patching symptoms repeatedly. Decisions reflected here: BG3_Data remains canonical mechanical baseline; wiki is behavioral tie-breaker; runtime should fail closed for unknown requirements; hotbar/feedback must be event-driven from backend state changes.

## Steps
1. Establish single authority contracts for character/action/passive data and normalize IDs/casing across [Data/DataRegistry.cs](Data/DataRegistry.cs), [Data/CharacterModel/CharacterDataRegistry.cs](Data/CharacterModel/CharacterDataRegistry.cs), [Data/Statuses/StatusRegistry.cs](Data/Statuses/StatusRegistry.cs), [Data/Statuses/BG3StatusIntegration.cs](Data/Statuses/BG3StatusIntegration.cs).
2. Create event-driven recomputation spine for equip/status/passive/known-action mutations (action-bar membership + passive providers + derived stats) centered on [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs), [Combat/Services/InventoryService.cs](Combat/Services/InventoryService.cs), [Combat/Statuses/StatusSystem.cs](Combat/Statuses/StatusSystem.cs), [Combat/Rules/PassiveRuleService.cs](Combat/Rules/PassiveRuleService.cs).
3. Correct core math and spell authority: ability modifier floor behavior, unified spellcasting ability/class selection, save DC bonus integration, and explicit SaveDC handling in [Combat/Entities/CombatantStats.cs](Combat/Entities/CombatantStats.cs), [Data/CharacterModel/CharacterSheet.cs](Data/CharacterModel/CharacterSheet.cs), [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs), [Data/Actions/BG3ActionConverter.cs](Data/Actions/BG3ActionConverter.cs), [Data/Actions/SpellEffectConverter.cs](Data/Actions/SpellEffectConverter.cs).
4. Repair equipment and feat mechanics end-to-end: enforce feat prerequisites/choice payloads, runtime propagation, and equip-time passives/statuses in [Combat/UI/CharacterCreation/FeatSelectionPanel.cs](Combat/UI/CharacterCreation/FeatSelectionPanel.cs), [Data/CharacterModel/CharacterResolver.cs](Data/CharacterModel/CharacterResolver.cs), [Combat/Services/InventoryService.cs](Combat/Services/InventoryService.cs), [Data/Stats/BG3WeaponData.cs](Data/Stats/BG3WeaponData.cs), [Data/Stats/BG3ArmorData.cs](Data/Stats/BG3ArmorData.cs).
5. Rebuild status/effect parity with BG3 remove semantics and deterministic status bridging (including HOLD_PERSON/SG_Paralyzed style tags and composite remove events) in [Combat/Statuses/StatusSystem.cs](Combat/Statuses/StatusSystem.cs), [Combat/Statuses/BG3StatusIntegration.cs](Combat/Statuses/BG3StatusIntegration.cs), [Combat/Actions/EffectPipeline.cs](Combat/Actions/EffectPipeline.cs), [BG3_Data/Statuses/Status_INCAPACITATED.txt](BG3_Data/Statuses/Status_INCAPACITATED.txt), [BG3_Data/Statuses/Status_INVISIBLE.txt](BG3_Data/Statuses/Status_INVISIBLE.txt).
6. Align UI feedback with backend truth: surface unusable reasons directly, remove string-heuristic mapping, and ensure failed/countered actions are visible in logs/UI via [Combat/UI/ActionBarModel.cs](Combat/UI/ActionBarModel.cs), [Combat/UI/Panels/ActionBarPanel.cs](Combat/UI/Panels/ActionBarPanel.cs), [Combat/UI/HudController.cs](Combat/UI/HudController.cs), [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs).
7. Extend persistence to include mechanical identity/state (passive toggles, known actions, resolved build facets, equipment-derived effects) in [Combat/Persistence/CombatantSnapshot.cs](Combat/Persistence/CombatantSnapshot.cs), [Combat/Persistence/CombatSaveService.cs](Combat/Persistence/CombatSaveService.cs), [Combat/Persistence/DeterministicExporter.cs](Combat/Persistence/DeterministicExporter.cs).
8. Add parity-focused regression suite by system (not content count), especially for fail-closed requirements, equip/status hotbar sync, DC/attack parity, and save/load equivalence.

## Verification
- Targeted tests first (unit/integration around touched symbols), then repo gates:
- `scripts/ci-build.sh`
- `scripts/ci-test.sh`
- `scripts/ci-godot-log-check.sh`
- Gameplay parity sweeps with seeded automation:
- `./scripts/run_autobattle.sh --seed 1234 --freeze-timeout 10 --loop-threshold 20`
- Optional full-fidelity smoke:
- `./scripts/run_autobattle.sh --full-fidelity --seed 42`

## Decisions
- BG3_Data is canonical for mechanics; local JSON is transitional and must be validated against LSX/text sources.
- Unknown requirement/functor/condition types should fail closed (diagnostic surfaced), not silently pass.
- Membership and legality are separate concerns; hotbar membership must update on state-change events, not only turn boundaries.
