# Plan: CombatArena Polish and Gameplay Testing

**Created:** February 2, 2026
**Status:** Ready for Atlas Execution

## Summary

This plan transforms CombatArena.tscn into a polished, feature-rich combat testbed that feels like BG3/Divinity Original Sin 2. It covers three major areas: (1) UI/UX improvements for better player feedback, (2) Systems wiring to enable missing gameplay features, and (3) Comprehensive automated gameplay tests to verify combat scenarios work correctly without visual inspection.

## Context & Analysis

**Relevant Files:**
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): Main controller - needs systems wiring for reactions, LOS, range validation
- [Combat/Arena/CombatHUD.cs](Combat/Arena/CombatHUD.cs): HUD controller - needs enhancements for AoE preview, movement preview
- [Combat/Arena/CombatInputHandler.cs](Combat/Arena/CombatInputHandler.cs): Input handling - needs movement targeting mode
- [Combat/Arena/CombatantVisual.cs](Combat/Arena/CombatantVisual.cs): Unit visuals - has floating text, needs enhancement
- [Combat/Targeting/TargetValidator.cs](Combat/Targeting/TargetValidator.cs): Targeting - GetValidTargets() needs range checks
- [Combat/Reactions/ReactionSystem.cs](Combat/Reactions/ReactionSystem.cs): Exists but not wired into CombatArena
- [Combat/Movement/MovementService.cs](Combat/Movement/MovementService.cs): Exists but not wired for opportunity attacks
- [Tests/Simulation/SimulationRunner.cs](Tests/Simulation/SimulationRunner.cs): Headless test runner for gameplay verification
- [Tests/Simulation/InvariantChecker.cs](Tests/Simulation/InvariantChecker.cs): Invariant checking framework

**Key Functions/Classes:**
- `CombatArena.RegisterServices()`: Main composition root - missing reaction/LOS/combatant-provider wiring
- `TargetValidator.GetValidTargets()`: Returns valid targets but ignores range (vs ValidateSingleTarget which checks range)
- `EffectPipeline.Reactions`: Optional field - currently null, reactions disabled
- `EffectPipeline.GetCombatants`: Optional callback - currently null, reactions can't query combatants
- `MovementService.DetectOpportunityAttacks()`: Returns empty when `_reactionSystem` is null

**Dependencies:**
- Godot 4.5 with C# support
- Existing Phase A-J implementations provide solid foundation
- DataRegistry, RulesEngine, StatusManager fully functional
- Simulation testing infrastructure ready

**Patterns & Conventions:**
- Service-locator via `CombatContext.RegisterService/GetService`
- Optional service injection (null means feature disabled)
- Deterministic RNG for reproducible tests
- Timeline-based presentation separated from gameplay resolution
- All validation via tests/logs, not visual inspection

## Implementation Phases

---

### Phase 1: Wire Missing Systems into CombatArena

**Objective:** Enable reactions, LOS checks, and proper range validation in live combat by completing the service wiring in CombatArena.RegisterServices().

**Files to Modify:**
- `Combat/Arena/CombatArena.cs`: Wire ReactionSystem, set EffectPipeline.Reactions, EffectPipeline.GetCombatants, wire LOS/Height into TargetValidator
- `Combat/Targeting/TargetValidator.cs`: Add range checks to GetValidTargets()

**Steps:**
1. In `RegisterServices()`:
   - Create `ReactionSystem` instance
   - Register opportunity attack reaction definition
   - Set `_effectPipeline.Reactions = reactionSystem`
   - Set `_effectPipeline.GetCombatants = () => _combatants`
   - Create `LOSService` and `HeightService` instances
   - Set `_effectPipeline.LOS = losService` and `_effectPipeline.Heights = heightService`
   - Inject reaction system and combatant provider into `MovementService`

2. Update `TargetValidator.GetValidTargets()` to include range checks:
   - Calculate distance from actor to each potential target
   - Filter targets outside ability range
   - Use same logic as `ValidateSingleTarget()`

3. Write test: `Tests/Integration/CombatArenaSystemsWiringTests.cs`
   - Assert ReactionSystem is non-null in context
   - Assert EffectPipeline.Reactions is set
   - Assert opportunity attacks trigger during movement

**Acceptance Criteria:**
- [ ] ReactionSystem registered in CombatContext
- [ ] EffectPipeline properly wired with Reactions and GetCombatants
- [ ] GetValidTargets() respects ability range
- [ ] Opportunity attacks trigger when moving away from enemies
- [ ] All existing tests pass

---

### Phase 2: Movement Path Preview

**Objective:** Add visual movement path preview during movement targeting, showing cost and valid destinations like BG3.

**Files to Modify/Create:**
- `Combat/Arena/MovementPreview.cs` (new): Draws line from unit to cursor, shows distance cost
- `Combat/Arena/CombatInputHandler.cs`: Add movement targeting mode
- `Combat/Arena/CombatArena.cs`: Expose movement preview state
- `Combat/Movement/PathPreviewData.cs`: Data model already exists from Phase J

**Steps:**
1. Create `MovementPreview.cs`:
   - Node3D that draws a line mesh from actor position to target
   - Shows distance text at endpoint
   - Color-codes: green=reachable, yellow=partial, red=unreachable
   - Shows opportunity attack warnings when path crosses threatened squares

2. Add movement targeting mode to `CombatInputHandler.cs`:
   - New state `TargetingMode.Move`
   - On mouse move: update preview path
   - On left click: execute move command
   - On right click: cancel movement targeting

3. Wire up in CombatArena:
   - Add movement button/hotkey (M or right-click drag)
   - Show/hide preview based on targeting mode

4. Write tests: `Tests/Unit/MovementPreviewTests.cs`
   - Assert path cost calculation is correct
   - Assert OA warning zones are detected
   - Assert unreachable positions are marked

**Acceptance Criteria:**
- [ ] Movement preview shows path line from actor to cursor
- [ ] Distance cost displayed at endpoint
- [ ] Colors indicate reachability (green/yellow/red)
- [ ] Opportunity attack warning zones highlighted
- [ ] Movement can be cancelled with right-click
- [ ] Tests validate path calculations without visuals

---

### Phase 3: AoE/Range Targeting Indicators

**Objective:** Show AoE shapes and range circles during ability targeting like BG3/DOS2.

**Files to Modify/Create:**
- `Combat/Arena/AoEIndicator.cs` (new): Renders sphere/cone/line shapes as targeting preview
- `Combat/Arena/RangeIndicator.cs` (new): Shows range circle around actor
- `Combat/Targeting/AoEShapes.cs`: Contains shape definitions (sphere, cone, line from Phase J)
- `Combat/Arena/CombatInputHandler.cs`: Update targeting to show indicators

**Steps:**
1. Create `RangeIndicator.cs`:
   - Draws circle/torus decal at actor position
   - Radius = ability range
   - Fades out targets outside range

2. Create `AoEIndicator.cs`:
   - Sphere: Circle decal at cursor position with radius
   - Cone: Triangle mesh pointing from actor toward cursor
   - Line: Rectangle from actor to cursor with fixed width
   - Highlight affected units (friendly fire warnings)

3. Update `SelectAbility()` in CombatArena:
   - Spawn RangeIndicator centered on actor
   - Update AoEIndicator based on ability shape

4. Wire into targeting flow:
   - On mouse move: update AoE position
   - Recalculate affected targets using `TargetValidator.ResolveAreaTargets()`
   - Show friendly fire warning highlighting

5. Write tests: `Tests/Unit/AoEIndicatorTests.cs`
   - Assert affected targets list matches expected for given positions
   - Assert friendly fire detection works
   - Assert range limits respected

**Acceptance Criteria:**
- [ ] Range circle shows around actor when ability selected
- [ ] AoE shape follows cursor correctly for sphere/cone/line
- [ ] Affected units highlighted
- [ ] Friendly fire warning for allied units in AoE
- [ ] Out-of-range positions handled gracefully
- [ ] Geometry tests validate target inclusion

---

### Phase 4: Reaction Prompt UI

**Objective:** Add BG3-style reaction prompt when reactions are triggered (opportunity attacks, counterspell-like effects).

**Files to Modify/Create:**
- `Combat/Arena/ReactionPromptUI.cs` (new): Popup for reaction decisions
- `Combat/Reactions/ReactionPrompt.cs`: Data model (exists)
- `Combat/Arena/CombatArena.cs`: Subscribe to ReactionSystem.OnPromptCreated

**Steps:**
1. Create `ReactionPromptUI.cs`:
   - Modal popup showing: trigger description, reactor unit, reaction options
   - "Use Reaction" / "Skip" buttons
   - Auto-timeout option (configurable)
   - Keyboard shortcuts (Y/N or 1/2)

2. Wire CombatArena to reaction events:
   - Subscribe to `ReactionSystem.OnPromptCreated`
   - Pause combat flow (set state to `CombatState.ReactionPrompt`)
   - Show ReactionPromptUI
   - On decision: call `ReactionSystem.UseReaction()` or `SkipReaction()`
   - Resume combat flow

3. Add AI reaction policy:
   - AI units auto-decide based on profile settings
   - No UI shown for AI reactions

4. Write tests: `Tests/Unit/ReactionPromptUITests.cs`
   - Assert prompt appears when trigger fired
   - Assert combat pauses during prompt
   - Assert correct reaction executed on selection
   - Assert skip properly continues combat

**Acceptance Criteria:**
- [ ] Reaction prompt appears when player-controlled reactor is eligible
- [ ] Combat pauses until decision made
- [ ] Use/Skip buttons work correctly
- [ ] AI reactors decide automatically
- [ ] Combat resumes after reaction resolves
- [ ] Tests verify event flow without UI

---

### Phase 5: Enhanced Combat Feedback

**Objective:** Improve combat feedback with better damage numbers, hit/miss indicators, and roll breakdowns.

**Files to Modify:**
- `Combat/Arena/CombatantVisual.cs`: Enhanced floating text with criticals, misses
- `Combat/Arena/CombatHUD.cs`: Add roll breakdown tooltip on log entries
- `Combat/UI/RollBreakdownData.cs`: Already exists from Phase J

**Steps:**
1. Enhance floating text in `CombatantVisual.cs`:
   - Critical hits: larger, gold color, "CRITICAL!" prefix
   - Misses: "MISS" text in gray
   - Healing: green with "+" prefix
   - Status effects: smaller text with status icon reference

2. Add roll breakdown to combat log:
   - Hover/click on log entry shows breakdown
   - Breakdown shows: base roll, modifiers, AC/DC, final result
   - Use `RollBreakdownData` from Phase J

3. Add hit chance preview on valid targets:
   - When targeting, show % hit chance on each valid target
   - Calculate using `RulesEngine.GetHitChance()`

4. Write tests:
   - Assert critical hit visual flag is set
   - Assert breakdown data is attached to log entries
   - Assert hit chance calculation matches expected

**Acceptance Criteria:**
- [ ] Critical hits show distinct visual treatment
- [ ] Misses clearly indicated
- [ ] Log entries have breakdown data attached
- [ ] Hit chance shown during targeting
- [ ] All feedback validated via event payloads, not visuals

---

### Phase 6: Comprehensive Gameplay Tests

**Objective:** Create automated tests that verify combat scenarios work correctly, catching issues like AI getting stuck, UI not updating, or abilities failing.

**Files to Create:**
- `Tests/Simulation/CombatGameplayTests.cs`: New test class for gameplay verification
- `Tests/Simulation/AIStuckDetectionTests.cs`: Tests for AI stuck conditions
- `Tests/Simulation/MultiRoundStabilityTests.cs`: Tests combat lasting multiple rounds
- `Tests/Simulation/AbilityComprehensiveTests.cs`: Tests all abilities work
- `Data/Scenarios/gameplay_ai_stress.json`: Scenario for AI stress testing
- `Data/Scenarios/gameplay_multi_round.json`: Scenario for multi-round testing

**Steps:**
1. Create `AIStuckDetectionTests.cs`:
   - Run AI turns and detect if same action repeated >3 times
   - Detect if AI has valid targets but takes no action
   - Detect if AI chooses invalid/out-of-range targets
   - Use fixed seed for reproducibility

2. Create `MultiRoundStabilityTests.cs`:
   - Run combat for 3+ rounds
   - Assert no exceptions thrown
   - Assert invariants maintained (HP bounds, resource bounds, turn order consistency)
   - Assert combat eventually ends (no infinite loop)

3. Create `AbilityComprehensiveTests.cs`:
   - For each ability in registry, execute it
   - Assert effects applied correctly
   - Assert no null reference exceptions
   - Assert damage/heal values within expected bounds

4. Create `UIUpdateVerificationTests.cs` (in Integration):
   - Subscribe to UI model events
   - Execute combat actions
   - Assert UI models receive updates (TurnTrackerModel, ActionBarModel, ResourceBarModel)
   - Assert no stale state

5. Create new test scenarios:
   - `gameplay_ai_stress.json`: 4v4 combat with varied abilities
   - `gameplay_multi_round.json`: High HP units for long combat

**Acceptance Criteria:**
- [ ] AI stuck detection test catches infinite loops
- [ ] Multi-round test runs 3+ rounds without crash
- [ ] All abilities execute without exception
- [ ] UI models receive all expected updates
- [ ] Tests are non-visual and CI-compatible
- [ ] Tests run in < 30 seconds total

---

### Phase 7: Surface Visualization (Optional Polish)

**Objective:** Show environmental surfaces (fire, grease, etc.) as visual areas in the arena.

**Files to Modify/Create:**
- `Combat/Arena/SurfaceVisual.cs` (new): Renders surface effects as decals/meshes
- `Combat/Environment/SurfaceManager.cs`: Already has surface tracking
- `Combat/Arena/CombatArena.cs`: Subscribe to surface events

**Steps:**
1. Create `SurfaceVisual.cs`:
   - Node3D that renders a decal at surface position
   - Color-coded by surface type (fire=orange, ice=blue, grease=yellow)
   - Shows intensity/size based on surface data

2. Wire into CombatArena:
   - Subscribe to `SurfaceManager.OnSurfaceCreated/Removed/Transformed`
   - Spawn/destroy SurfaceVisual nodes accordingly

3. Write tests:
   - Assert visual spawned when surface created
   - Assert visual removed when surface destroyed
   - Assert transform event updates visual

**Acceptance Criteria:**
- [ ] Surfaces appear as colored areas on ground
- [ ] Surfaces removed when expired
- [ ] Transform events (fire→burned out) update visuals
- [ ] Tests verify via event subscription, not visuals

---

### Phase 8: Polish Pass and Debug Enhancements

**Objective:** Final polish including enhanced Debug Panel, quick combat actions, and quality-of-life improvements.

**Files to Modify:**
- `Combat/Arena/DebugPanel.cs`: Add more debug tools
- `Combat/Arena/CombatHUD.cs`: Add quick action buttons
- `Combat/Arena/CombatArena.cs`: Add debug commands

**Steps:**
1. Enhance DebugPanel (F1):
   - Add: Spawn unit, Spawn surface, Set initiative, Toggle fog of war
   - Add: Force roll result (for testing specific outcomes)
   - Add: Print state hash (for determinism verification)
   - Add: Export combat log to file

2. Add quick action buttons:
   - "Pass Turn" (end turn without action)
   - "Defend" (use action to boost AC, if implemented)
   - "Movement Only" mode button

3. Add keyboard shortcuts summary (? key shows help)

4. Write tests for debug commands:
   - Assert spawn unit adds to combatants list
   - Assert force roll produces expected outcome
   - Assert state hash is deterministic

**Acceptance Criteria:**
- [ ] Debug panel has comprehensive tools
- [ ] Quick action buttons visible and functional
- [ ] Help overlay shows all shortcuts
- [ ] Debug commands work correctly (verified by tests)

---

## Open Questions

1. **Reaction Definition Storage**: Where should reaction definitions live?
   - **Option A:** JSON in DataRegistry (parallel to abilities/statuses)
   - **Option B:** Hardcoded in code (current test approach)
   - **Recommendation:** Option A for consistency with data-driven design. Create `Data/Reactions/` directory.

2. **AoE Resolution Authority**: Who resolves AoE targets?
   - **Option A:** UI resolves targets before execution
   - **Option B:** EffectPipeline resolves targets during execution
   - **Recommendation:** Option A for better preview, but EffectPipeline should validate.

3. **Reaction Timeout**: Should there be an auto-timeout for reaction prompts?
   - **Option A:** No timeout (wait forever)
   - **Option B:** Configurable timeout (default 10s)
   - **Recommendation:** Option B with configurable setting.

## Risks & Mitigation

- **Risk:** Reaction wiring may break existing tests
  - **Mitigation:** Run full test suite after each wiring change; reactions are optional (null = disabled)

- **Risk:** AoE indicators may have performance issues with many units
  - **Mitigation:** Use simple geometry; profile with 20+ units

- **Risk:** Multi-round tests may be slow
  - **Mitigation:** Use low HP values for faster resolution; set reasonable maxTurns

## Success Criteria

- [ ] All 8 phases complete with passing tests
- [ ] CombatArena feels responsive with clear feedback
- [ ] Player can see valid targets, AoE shapes, and movement paths
- [ ] Reactions work for player and AI
- [ ] Automated tests catch AI stuck conditions and ability failures
- [ ] Multi-round combat (3+ rounds) runs without errors
- [ ] All tests pass in CI (`scripts/ci-test.sh`)
- [ ] Build succeeds (`scripts/ci-build.sh`)

## Notes for Atlas

**Execution Order:** Phases must be executed in order (1→8) as later phases depend on earlier wiring.

**Testing Strategy:** Each phase includes its own tests. Run `dotnet test` after each phase before proceeding. 

**Key Services:** The `CombatContext` service locator is the central composition root. All wiring changes should go through `RegisterServices()`.

**Parallel Opportunities:** Within phases:
- Phase 2-3 (movement preview + AoE) are somewhat independent once Phase 1 wiring is done
- Phase 6 tests can be written in parallel across multiple files

**Non-Visual Principle:** All verification must be via tests/logs/events. No "launch and look" validation allowed per project rules.

**File Naming:** Follow existing patterns:
- New arena components: `Combat/Arena/{ComponentName}.cs`
- New tests: `Tests/{Unit|Integration|Simulation}/{ComponentName}Tests.cs`
- New scenarios: `Data/Scenarios/{scenario_name}.json`
