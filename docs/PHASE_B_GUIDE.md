# Phase B Implementation Guide

## Overview

Phase B adds the rules engine, ability system, effect pipeline, status system, and data-driven content loading.

## Objectives (All Complete ✅)

- [x] Rule queries/modifiers/events
- [x] Ability definitions + effect pipeline  
- [x] Targeting + AoE preview (geometry + validation)
- [x] Damage/heal/status basics
- [x] Data registry + validation

## Architecture

### Rules Engine
Central hub for all dice rolls and combat math:
- `RulesEngine` - Attack/save/damage rolls
- `Modifier` - Buffs/debuffs that modify rolls
- `ModifierStack` - Resolves multiple modifiers
- `RuleEventBus` - Dispatches events for logging/reactions

### Ability System
Data-driven ability definitions:
- `AbilityDefinition` - Schema for abilities
- `EffectPipeline` - Executes abilities and effects
- `Effect` - Base class for effect handlers
- Implemented: damage, heal, apply_status, remove_status, modify_resource, teleport, forced_move, spawn_surface

### Status System
Robust status/buff/debuff management:
- `StatusDefinition` - Schema for statuses
- `StatusManager` - Tracks active statuses
- `StatusInstance` - Active status on a combatant
- Supports: stacking, durations, modifiers, tick effects

### Targeting
Target validation and AoE resolution:
- `TargetValidator` - Validates targets for abilities
- Faction filtering, range checks, AoE geometry

### Data Registry
Central content repository:
- `DataRegistry` - Loads and validates content
- JSON schemas for abilities, statuses, scenarios
- Fail-fast validation on startup

## Data Formats

### Ability JSON
```json
{
    "id": "fireball",
    "name": "Fireball",
    "targetType": "Circle",
    "targetFilter": "All",
    "range": 20,
    "areaRadius": 4,
    "saveType": "dexterity",
    "saveDC": 14,
    "effects": [
        { "type": "damage", "diceFormula": "6d6", "damageType": "fire" }
    ]
}
```

### Status JSON
```json
{
    "id": "poisoned",
    "name": "Poisoned",
    "durationType": "Turns",
    "defaultDuration": 3,
    "maxStacks": 3,
    "stacking": "Stack",
    "modifiers": [...],
    "tickEffects": [...]
}
```

## Test Strategy

Phase B tests use REAL implementations (not mocks):
- `RulesEngineIntegrationTests` - Real RulesEngine class
- `EffectPipelineIntegrationTests` - Real EffectPipeline class
- `TargetValidatorTests` - Real TargetValidator class
- `DataRegistryTests` - Real DataRegistry validation

## Phase B Services in CombatArena

CombatArena.RegisterServices() initializes:
1. DataRegistry (loads from Data/)
2. Registry validation (fails fast on errors)
3. RulesEngine with scenario seed
4. StatusManager connected to RulesEngine
5. EffectPipeline connected to Rules + Statuses
6. TargetValidator for ability targeting
7. Status tick processing for DOT effects

## Verification

Run the CI gates:
```bash
./scripts/ci-build.sh
./scripts/ci-test.sh
```

## Next: Phase C

Phase C adds:
- Full action economy
- Reactions/interrupts
- Surfaces/field effects
- Movement system (position-aware)
- LOS/cover/height
