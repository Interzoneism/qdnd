# Plan: Phase J - Tactical UI & AI Depth

**Created:** 2026-02-02
**Status:** Ready for Atlas Execution

## Summary

Phase J bridges the gap between robust core rules (Phases A-I) and the user/AI experience. Focus areas: AI tactical awareness (jump/shove), UI polish (tooltips, path previews), and geometry expansion (AoE shapes).

## Context & Analysis

**What's Working:**
- Core combat loop complete
- LOS/height/cover affect combat
- Reactions trigger on movement/damage
- Concentration system active

**User Experience Gaps:**
- No hover tooltips showing breakdown
- No movement path preview
- Limited AoE shape support
- AI doesn't use jump/shove tactically

## Implementation Phases

### Phase 1: Advanced AoE Shapes

**Objective:** Expand geometry library with Cone, Line, and Chain targeting

**Files to Modify:**
- Combat/Targeting/TargetValidator.cs
- Combat/Targeting/AoEShapes.cs (create if needed)

**Shapes to Add:**
1. **Cone** - Origin point, direction, angle, length
2. **Line** - Start, end, width
3. **Chain** - Bounces between targets, max bounces, range per bounce

**Changes:**
- Add shape calculation methods
- Wire into ResolveAreaTargets
- Add shape definitions to AbilityDefinition

**Tests:**
- Cone includes targets in arc
- Line includes targets along path
- Chain bounces correctly

---

### Phase 2: AI Tactical Movement Awareness

**Objective:** AI uses jump/shove/climb when beneficial

**Files to Modify:**
- Combat/AI/AIMovementEvaluator.cs
- Combat/AI/AIScorer.cs

**Changes:**
1. Check if jump would reach better position
2. Consider shove to push enemy off ledge
3. Evaluate climb to gain height advantage
4. Score positions considering these options

**Tests:**
- AI prefers high ground when available
- AI considers shove near ledges
- AI uses jump to cross gaps

---

### Phase 3: Movement Path Preview Data

**Objective:** Calculate path with costs for UI display

**Files to Modify:**
- Combat/Movement/MovementService.cs
- Combat/Movement/PathPreview.cs (create)

**Changes:**
1. Create PathPreview class with waypoints and costs
2. Calculate terrain costs along path
3. Mark difficult terrain sections
4. Include elevation changes
5. Return preview data for UI consumption

**Tests:**
- Path includes terrain costs
- Difficult terrain marked correctly
- Preview matches actual move cost

---

### Phase 4: Status Effect Triggers

**Objective:** Wire remaining status triggers (on move, on cast)

**Files to Modify:**
- Combat/Statuses/StatusSystem.cs

**Changes:**
1. Wire MovementCompleted event to status triggers
2. Wire AbilityCast event to status triggers
3. Execute status-defined effects when triggered
4. Support "on enter surface" triggers from status

**Tests:**
- Status with "on move" trigger fires
- Status with "on cast" trigger fires
- Triggers execute correct effects

---

### Phase 5: Breakdown Tooltip Data Model

**Objective:** Ensure all breakdown data is available for UI tooltips

**Files to Modify:**
- Combat/Rules/RulesEngine.cs
- Combat/UI/BreakdownModels.cs (create if needed)

**Changes:**
1. Ensure every roll creates structured breakdown
2. Attack breakdown: base roll, modifiers, total, hit/miss
3. Damage breakdown: dice, modifiers, type, total
4. Save breakdown: DC, roll, modifiers, success/fail
5. Create BreakdownEntry with source/value/description

**Tests:**
- All breakdown fields populated
- Sources correctly attributed
- Format suitable for UI display

---

### Phase 6: Ability Variant Support

**Objective:** Support sub-abilities and upcasting

**Files to Modify:**
- Combat/Abilities/AbilityDefinition.cs
- Combat/Abilities/AbilityVariant.cs (create)

**Changes:**
1. Add Variants list to AbilityDefinition
2. AbilityVariant: modifier, cost change, effect changes
3. Support upcast scaling (higher cost = more damage)
4. UI selection of variant before execution

**Tests:**
- Variant modifies base ability
- Upcast increases damage correctly
- Costs adjusted per variant

---

## Success Criteria

- [ ] Cone/Line/Chain AoE shapes work
- [ ] AI considers jump/shove tactically
- [ ] Path preview with terrain costs available
- [ ] Status triggers on move/cast fire
- [ ] All rolls have structured breakdowns
- [ ] Ability variants supported
- [ ] All CI gates pass

## Notes for Atlas

- Phase 1-2 can be parallelized
- Phase 3-4 can be parallelized
- Each phase is independently testable
- Focus on data models over visual UI
