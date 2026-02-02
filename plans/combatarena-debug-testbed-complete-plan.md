# Plan: CombatArena Debug Testbed - Complete Implementation

**Created:** February 2, 2026
**Status:** ✅ COMPLETE (February 2, 2026)

## Summary

Make CombatArena.tscn work as intended when launched in Godot. This involves fixing the HUD/GUI to be fully functional and informative, ensuring all input actions work, adding a scenario selector so users can try different scenarios, and fixing scene file issues. CombatArena must serve as the debug testbed where all implemented features are accessible.

## Context & Analysis

**Relevant Files:**
- [Combat/Arena/CombatArena.tscn](Combat/Arena/CombatArena.tscn): Main scene file, mostly complete but HUD not fully wired
- [Combat/Arena/CombatArena.cs](Combat/Arena/CombatArena.cs): Scene controller, functional but needs minor enhancements
- [Combat/Arena/CombatHUD.cs](Combat/Arena/CombatHUD.cs): HUD controller, needs fixes for proper wiring and layout
- [Combat/Arena/CombatInputHandler.cs](Combat/Arena/CombatInputHandler.cs): Input handler, uses raw keys instead of input actions
- [Combat/Arena/CombatantVisual.tscn](Combat/Arena/CombatantVisual.tscn): Has missing collision shape resources
- [Combat/Arena/CombatantVisual.cs](Combat/Arena/CombatantVisual.cs): Visual representation, functional
- [project.godot](project.godot): Missing input action definitions
- [Combat/UI/ActionBarModel.cs](Combat/UI/ActionBarModel.cs): Model exists but not used by HUD
- [Combat/UI/TurnTrackerModel.cs](Combat/UI/TurnTrackerModel.cs): Model exists but not used by HUD
- [Combat/UI/ResourceBarModel.cs](Combat/UI/ResourceBarModel.cs): Model exists but not used by HUD
- [Combat/Services/CombatLog.cs](Combat/Services/CombatLog.cs): Combat log service, needs UI display

**Key Issues Identified:**

1. **CombatantVisual.tscn** - References undefined SubResources for collision shapes
2. **CombatHUD** - Turn tracker never populated with combatants, bottom bar layout wrong
3. **No Scenario Selector** - User cannot switch scenarios at runtime
4. **No Combat Log Display** - Combat log exists but not shown in UI
5. **No Input Actions** - project.godot has no input actions defined
6. **UI Models Not Used** - ActionBarModel, TurnTrackerModel, ResourceBarModel exist but aren't wired

**Dependencies:**
- Godot 4.5 with C# support
- Existing combat services (CombatContext, TurnQueue, etc.)

**Patterns & Conventions:**
- Scene files use .tscn format with text-safe edits
- UI follows Godot Control node hierarchy
- Models are separate from UI for testability

## Implementation Phases

### Phase 1: Fix CombatantVisual.tscn Collision Shapes

**Objective:** Fix the missing collision shape SubResources so combatants can be clicked

**Files to Modify:**
- `Combat/Arena/CombatantVisual.tscn`: Add missing CapsuleShape3D and CylinderShape3D resources

**Steps:**
1. Add CapsuleShape3D SubResource with matching dimensions (radius 0.4, height 1.8)
2. Add CylinderShape3D SubResource for ground collision 
3. Verify scene loads without errors

**Acceptance Criteria:**
- [ ] CombatantVisual.tscn has all required SubResources defined
- [ ] Scene loads without missing resource errors
- [ ] CI build passes

---

### Phase 2: Add Input Actions to project.godot

**Objective:** Define proper input actions for customizable controls

**Files to Modify:**
- `project.godot`: Add input action definitions

**Input Actions to Define:**
- `combat_end_turn` - Space/Enter
- `combat_cancel` - Escape
- `combat_ability_1` through `combat_ability_6` - Keys 1-6
- `camera_pan_left/right/up/down` - WASD or Arrow keys
- `camera_zoom_in/out` - Mouse wheel
- `camera_rotate_left/right` - Q/E

**Steps:**
1. Add [input] section to project.godot
2. Define all combat input actions with appropriate key bindings

**Acceptance Criteria:**
- [ ] All input actions defined in project.godot
- [ ] CI build passes

---

### Phase 3: Update CombatInputHandler to Use Input Actions

**Objective:** Replace raw key detection with Input.IsActionPressed for configurable controls

**Files to Modify:**
- `Combat/Arena/CombatInputHandler.cs`: Use input actions instead of raw keycodes

**Steps:**
1. Change key detection to use InputMap action names
2. Add camera control handling (pan, zoom, rotate)
3. Add input action logging for debugging

**Acceptance Criteria:**
- [ ] All inputs use Input.IsActionJustPressed with action names
- [ ] Camera controls functional
- [ ] CI build and test pass

---

### Phase 4: Fix CombatHUD Layout and Wiring

**Objective:** Fix HUD layout issues and ensure proper connection to Arena

**Files to Modify:**
- `Combat/Arena/CombatHUD.cs`: Fix layout, wire up turn tracker

**Issues to Fix:**
1. Bottom bar uses negative position instead of anchor
2. Turn tracker never populated with combatants  
3. Arena reference may be null initially

**Steps:**
1. Fix bottom bar to use bottom anchor properly
2. Add initialization that populates turn tracker after Arena is ready
3. Call RefreshTurnTracker when combat starts
4. Update turn tracker on turn change

**Acceptance Criteria:**
- [ ] Bottom bar correctly anchored to bottom of screen
- [ ] Turn tracker shows all combatants at combat start
- [ ] Turn tracker highlights active combatant
- [ ] CI build passes

---

### Phase 5: Add Combat Log Display Panel

**Objective:** Display combat log entries in the HUD so player can see what's happening

**Files to Modify:**
- `Combat/Arena/CombatHUD.cs`: Add combat log panel

**UI Elements to Add:**
- ScrollContainer on right side of screen
- VBoxContainer for log entries
- Label entries for each log message (limited to last 20)

**Steps:**
1. Add combat log panel to HUD layout
2. Subscribe to CombatLog.OnEntryAdded event
3. Display formatted log entries with color coding
4. Auto-scroll to newest entry
5. Limit visible entries to prevent performance issues

**Acceptance Criteria:**
- [ ] Combat log panel visible on right side
- [ ] New entries appear as combat progresses
- [ ] Damage shown in red, healing in green, status in purple
- [ ] Panel scrolls automatically
- [ ] CI build passes

---

### Phase 6: Add Scenario Selector UI

**Objective:** Allow player to select and load different scenarios at runtime

**Files to Create:**
- `Combat/Arena/ScenarioSelector.cs`: UI component for scenario selection

**Files to Modify:**
- `Combat/Arena/CombatArena.tscn`: Add ScenarioSelector node
- `Combat/Arena/CombatArena.cs`: Add ReloadScenario method

**Steps:**
1. Create ScenarioSelector.cs with dropdown/list of available scenarios
2. Scan Data/Scenarios directory for .json files
3. Add "Restart" button to reload current scenario
4. Add method to CombatArena to reload with different scenario
5. Wire up selector to arena in scene

**Acceptance Criteria:**
- [ ] Scenario selector visible in top-left corner
- [ ] Lists all scenarios from Data/Scenarios/
- [ ] Selecting scenario reloads combat
- [ ] Restart button works
- [ ] CI build passes

---

### Phase 7: Add Resource Display Panel

**Objective:** Show action economy resources (Action, Bonus, Movement, Reaction) in HUD

**Files to Modify:**
- `Combat/Arena/CombatHUD.cs`: Add resource display

**Resources to Display:**
- Action Points (1/1)
- Bonus Action (1/1)  
- Movement (30 ft)
- Reaction (1/1)

**Steps:**
1. Add resource bar container near unit info panel
2. Display current/max for each resource type
3. Update when abilities are used
4. Color-code depleted resources

**Acceptance Criteria:**
- [ ] Resource bars visible when player's turn
- [ ] Resources update after ability use
- [ ] Depleted resources shown differently
- [ ] CI build passes

---

### Phase 8: Add Inspect Panel for Combatant Details

**Objective:** Show detailed combatant information when hovering or selected

**Files to Modify:**
- `Combat/Arena/CombatHUD.cs`: Add inspect panel

**Information to Display:**
- Name and faction
- HP (current/max with bar)
- Active statuses with durations
- Available abilities
- Initiative value

**Steps:**
1. Add inspect panel container (collapsible)
2. Show when combatant is hovered or selected
3. List active statuses from StatusManager
4. Update dynamically as combat progresses

**Acceptance Criteria:**
- [ ] Inspect panel shows on hover/selection
- [ ] All combatant details displayed
- [ ] Statuses show remaining duration
- [ ] CI build passes

---

### Phase 9: Add Debug Controls Panel

**Objective:** Provide debug controls for testing (spawn units, apply status, skip turn, etc.)

**Files to Create:**
- `Combat/Arena/DebugPanel.cs`: Debug control panel

**Files to Modify:**
- `Combat/Arena/CombatArena.tscn`: Add DebugPanel node
- `Combat/Arena/CombatArena.cs`: Add debug methods

**Debug Controls:**
- Toggle Debug Panel visibility (F1)
- Deal damage to selected target
- Heal selected target
- Apply status by ID
- Force end combat
- Toggle verbose logging

**Steps:**
1. Create DebugPanel.cs with toggle visibility
2. Add buttons/inputs for each debug action
3. Wire to CombatArena methods
4. Only visible when VerboseLogging is enabled

**Acceptance Criteria:**
- [ ] F1 toggles debug panel
- [ ] All debug actions work
- [ ] Panel hidden when VerboseLogging is false
- [ ] CI build passes

---

### Phase 10: Wire UI Models and Final Integration

**Objective:** Connect ActionBarModel, TurnTrackerModel, ResourceBarModel to HUD

**Files to Modify:**
- `Combat/Arena/CombatHUD.cs`: Use UI models
- `Combat/Arena/CombatArena.cs`: Create and expose models

**Steps:**
1. Create UI models in CombatArena
2. Populate models from combat state
3. HUD subscribes to model signals
4. Update HUD reactively from model changes

**Acceptance Criteria:**
- [ ] HUD driven by UI models
- [ ] Model changes reflected in UI
- [ ] Signals properly connected
- [ ] CI build and test pass

---

### Phase 11: Final Polish and Verification

**Objective:** Run all tests, verify everything works, update documentation

**Steps:**
1. Run scripts/ci-build.sh
2. Run scripts/ci-test.sh
3. Verify all HUD elements display correctly (via logs/assertions)
4. Update READY_TO_START.md with new features
5. Mark plan complete

**Acceptance Criteria:**
- [ ] CI build passes
- [ ] All tests pass
- [ ] Documentation updated
- [ ] CombatArena is the complete debug testbed

---

## Implementation Order

Execute phases sequentially (1-11) as each builds on the previous.

## Open Questions

None - all requirements are clear from the codebase analysis.

## Risks & Mitigation

- **Risk:** Scene file edits may break if not text-safe
  - **Mitigation:** Make minimal, targeted edits to .tscn files

- **Risk:** UI layout may look wrong without visual testing
  - **Mitigation:** Use standard Godot anchor presets and emit layout debug events

## Success Criteria

- [ ] CombatArena.tscn loads and runs without errors
- [ ] HUD shows turn tracker, action bar, resources, combat log
- [ ] All input actions work (abilities, end turn, camera)
- [ ] Scenario selector allows switching scenarios
- [ ] Debug panel provides testing controls
- [ ] CI build and all tests pass

## Notes for Execution

- Execute phases sequentially to avoid conflicts
- Each phase should be committed separately
- Run ci-build.sh after each phase to catch issues early
- Focus on functional correctness over visual polish
