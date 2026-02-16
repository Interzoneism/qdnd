## Phase 3 Complete: Action/Spell Coverage Expansion

Expanded the spell infrastructure with multi-projectile execution, BG3-derived upcast scaling, dynamic formula resolution, and action validation wiring. The EffectPipeline now loops per-projectile with independent attack rolls, SpellUpcastRules provides accurate scaling for ~35 core spells, and CharacterResolver validates granted abilities against the ActionRegistry at scenario load time.

**Files created:**
- Data/Spells/SpellUpcastRules.cs — Upcast rules database for ~35 D&D 5e spells (levels 1-6) with BG3-derived dice/projectile/target/duration scaling, and BG3 spell ID normalization
- Tests/Unit/MultiProjectileTests.cs — Multi-projectile spell tests (Magic Missile, Scorching Ray, Eldritch Blast)
- Tests/Integration/DynamicFormulaResolutionTests.cs — Tests for dynamic formula resolution (SpellcastingAbilityModifier, MainMeleeWeapon, CharacterLevel)
- Tests/Integration/UpcastingTests.cs — Integration tests for upcast damage/projectile/target scaling

**Files changed:**
- Combat/Actions/EffectPipeline.cs — Added multi-projectile execution loop (`ExecuteMultiProjectile`), dynamic formula resolution in `BuildEffectiveEffects`, upcast projectile count scaling, upcast target count scaling with trimming, skip global attack roll for multi-projectile spells
- Combat/Actions/ActionVariant.cs — Added `ProjectilesPerLevel` and `TargetsPerLevel` fields to `UpcastScaling`
- Data/Actions/BG3ActionConverter.cs — Wired SpellUpcastRules lookup before heuristic fallback in `ConvertToAction`
- Data/Actions/SpellEffectConverter.cs — Added `ResolveDynamicFormula()` for runtime BG3 variable substitution (SpellcastingAbilityModifier, MainMeleeWeapon, CharacterLevel, LevelMapValue)
- Data/CharacterModel/CharacterResolver.cs — Implemented `SetActionRegistry()` and `ValidateActionIds()` for ability validation
- Data/ScenarioLoader.cs — Added `SetActionRegistry()` and wired into CharacterResolver during character resolution
- Combat/Arena/CombatArena.cs — Wired `_actionRegistry` into `_scenarioLoader` during initialization

**Functions created/changed:**
- EffectPipeline.ExecuteMultiProjectile() — Per-projectile execution with independent attack rolls, target cycling, rule window dispatch
- SpellEffectConverter.ResolveDynamicFormula() — Resolves BG3 dynamic tokens in dice formulas at runtime
- SpellUpcastRules.GetUpcastScaling() — Looks up upcast rules with BG3 spell ID normalization (strips type prefix, converts PascalCase to snake_case)
- SpellUpcastRules.NormalizeBG3SpellId() — Normalizes prefixed BG3 IDs (e.g., Projectile_MagicMissile → magic_missile)
- SpellUpcastRules.ApplyUpcastRule() — Merges explicit upcast rules onto ActionDefinition
- CharacterResolver.SetActionRegistry() / ValidateActionIds() / GetValidationReport() — Action validation pipeline
- ScenarioLoader.SetActionRegistry() — Injects ActionRegistry for ability validation

**Tests created:**
- MultiProjectileTests: 7 tests (Magic Missile auto-hit 3 darts, Scorching Ray 3 beams with attack rolls, Eldritch Blast level scaling, upcast projectile scaling, target cycling)
- DynamicFormulaResolutionTests: 5 tests (SpellcastingAbilityModifier, MainMeleeWeapon, CharacterLevel, passthrough for standard dice, combined formula)
- UpcastingTests: 8 tests (Burning Hands +1d6, Cure Wounds +1d8, Magic Missile +1 dart, Scorching Ray +1 beam, Hold Person +1 target, duration scaling, damage flat scaling, no-rule fallback)

**Review Status:** APPROVED (after 2 revision cycles fixing: dead code wiring, BG3 ID normalization, upcast target trimming regression)

**Git Commit Message:**
```
feat: add multi-projectile execution, upcast scaling, and formula resolution

- Implement per-projectile attack rolls in EffectPipeline for multi-beam spells
- Add SpellUpcastRules database with BG3-derived scaling for 35 core spells
- Wire BG3 spell ID normalization (Projectile_MagicMissile -> magic_missile)
- Add ResolveDynamicFormula for runtime SpellcastingAbilityModifier substitution
- Wire ActionRegistry into ScenarioLoader for ability validation at load time
- Implement CharacterResolver.ValidateActionIds for granted ability verification
- Add TargetsPerLevel and ProjectilesPerLevel upcast scaling to EffectPipeline
- Add 20 new tests (multi-projectile, upcast, dynamic formula, validation)
```
