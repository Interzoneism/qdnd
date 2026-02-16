## Phase 2 Complete: Data Pipeline Fix — Spell Effect Converter & Functor Engine

Fixed the broken CreateSurface parser, expanded SpellEffectConverter to handle 21 BG3 functor types (up from 8), and built the StatusFunctorEngine + Data-layer BG3StatusIntegration to convert BG3 status functor strings into runtime StatusDefinition effects. All 1476 tests passing, 0 build warnings.

**Files created:**
- Data/Actions/FunctorTypes.cs — Enum/registry of 30+ BG3 functor types with parsing metadata
- Data/Statuses/StatusFunctorEngine.cs — Converts OnApply/OnTick/OnRemove functor strings to StatusTickEffect/StatusTriggerEffect
- Data/Statuses/BG3StatusIntegration.cs — Converts BG3StatusData → StatusDefinition (duration, stacking, boosts, functors, groups, remove events)
- Tests/Unit/CreateSurfaceParserTests.cs — 7 tests for CreateSurface parser
- Tests/Unit/StatusFunctorEngineTests.cs — 11 tests for StatusFunctorEngine
- Tests/Unit/BG3StatusIntegrationTests.cs — 14 tests for Data-layer status integration

**Files changed:**
- Data/Actions/SpellEffectConverter.cs — Fixed CreateSurface regex to (radius, duration, surfaceType), added 13 new functor parsers, negative duration support, SurfaceChange single-arg fallback
- Combat/Actions/Effects/Effect.cs — Added BreakConcentrationEffect, RestoreResourceEffect, GainTempHPEffect, CreateExplosionEffect
- Combat/Actions/EffectPipeline.cs — Registered 4 new effect handlers
- Combat/Statuses/StatusSystem.cs — Added OnApply to StatusTriggerType enum, added ExecuteOnApplyTriggerEffects()
- Combat/Statuses/BG3StatusIntegration.cs — Uses Data.Statuses.BG3StatusIntegration.ConvertToStatusDefinition()
- Combat/Arena/CombatArena.cs — Namespace disambiguation for BG3StatusIntegration
- Examples/BG3StatusExamples.cs — Namespace disambiguation
- Tests/SpellEffectConverterTests.cs — Fixed CreateSurface test, added 17 new functor tests
- Tests/Integration/BG3StatusIntegrationTests.cs — Namespace disambiguation
- Tests/Unit/PassiveToggleFunctorTests.cs — Namespace disambiguation
- Tests/Unit/PassiveToggleDebugTest.cs — Namespace disambiguation

**Functions created:**
- SpellEffectConverter: ParseSingleEffect handlers for RestoreResource, BreakConcentration, GainTemporaryHitPoints, CreateExplosion, SwitchDeathType, ExecuteWeaponFunctors, SurfaceChange (3-arg + 1-arg), Stabilize, Resurrect, RemoveStatusByGroup, Counterspell, SetAdvantage, SetDisadvantage
- StatusFunctorEngine: ParseOnApplyFunctors(), ParseOnTickFunctors() (returns tuple), ParseOnRemoveFunctors(), ConvertToTriggerEffects(), ConvertEffectDefinitionToTriggerEffect()
- BG3StatusIntegration (Data): ConvertToStatusDefinition(), RegisterBG3Statuses(), MapDuration(), MapStackingBehavior(), MapStatusGroups(), MapRemoveEvents(), ParseBoosts(), ParseFunctors()
- StatusManager: ExecuteOnApplyTriggerEffects()
- Effect classes: BreakConcentrationEffect.Execute(), RestoreResourceEffect.Execute(), GainTempHPEffect.Execute()/Preview(), CreateExplosionEffect.Execute()

**Tests created:**
- CreateSurfaceParserTests: 7 tests (basic, fire, empty duration, zero duration, decimal radius, fog cloud, GROUND: prefix)
- StatusFunctorEngineTests: 11 tests (OnApply, multi-functor, OnTick damage, OnTick heal, OnTick non-damage trigger, OnRemove, empty/null input, value per stack, resource restore, force)
- BG3StatusIntegrationTests: 14 tests (basic conversion, incapacitated, status groups, remove events ×3, stack types ×2, OnTick/OnApply/OnRemove functors, null/negative duration, batch registration)
- SpellEffectConverterTests: 17 new tests (all new functor types + SurfaceChange single-arg + CreateSurface negative duration)

**Review Status:** APPROVED

**Git Commit Message:**
```
feat: expand spell effect converter and build status functor engine

- Fix CreateSurface parser arg order to match BG3 (radius, duration, surfaceType)
- Add 13 new BG3 functor parsers (RestoreResource, BreakConcentration, etc.)
- Support negative duration (-1) and single-arg SurfaceChange patterns
- Create StatusFunctorEngine for OnApply/OnTick/OnRemove functor conversion
- Create Data-layer BG3StatusIntegration for BG3StatusData → StatusDefinition
- Add OnApply trigger type to StatusSystem with execution support
- Register 4 new effect handlers in EffectPipeline
- Add 49 new tests (1476 total passing)
```
