# HUD Analysis and Required Fixes
**Date:** 2026-02-04  
**Build:** CombatArena.tscn  
**Reference:** Baldur's Gate 3 Combat UI

---

## 1. INITIATIVE BAR (Top Center)

### Current State
- Four text boxes displaying: "15 Fight", "14 Goblin", "12 Mage", "10 Orc B"
- Simple rectangular borders with minimal styling
- Border colors: Yellow (active), Red, Blue, Red
- Height: ~50-55px
- Width: ~60-65px per box
- Font size: ~12-14px
- No spacing between boxes (touching edges)

### Critical Issues

**A. Missing Visual Elements**
1. **No character portraits** - BG3 uses circular or rectangular character avatars (minimum 48x48px recommended)
2. **No HP indicators** - Each portrait must show current/max HP below the initiative number
3. **No team identification** - Missing distinct border colors (Allies: Blue/Green, Enemies: Red, Neutrals: Yellow)
4. **No status effect icons** - Space needed below portraits for condition icons (16x16px minimum)
5. **No current turn highlight** - Active character needs enlargement (scale 1.2-1.5x) or glow effect

**B. Layout Problems**
1. **Boxes touch edges** - Need 8-12px margin between portraits
2. **Fixed width** - Should expand based on name length or use consistent portrait size
3. **No shared initiative grouping** - Characters with same initiative should be visually linked
4. **Vertical alignment** - All boxes should align to same baseline
5. **No turn progression indicator** - Missing arrow or flow direction (left-to-right)

**C. Typography**
1. **Initiative number position** - Should be top-right corner of portrait, not inline with name
2. **Name truncation** - "Fight" appears truncated from "Fighter"
3. **Font weight** - Initiative numbers need bold weight (700+)
4. **Font size** - Name text too small; recommend 16-18px for readability
5. **Color contrast** - White text on varied backgrounds may fail accessibility (4.5:1 ratio required)

**D. Positioning**
1. **Top margin** - Currently ~8-12px from screen top; should be 16-24px
2. **Horizontal centering** - Bar appears slightly left of center
3. **Z-index** - Must render above all 3D elements

**E. Recommended Specifications**
- **Container height:** 80-100px
- **Portrait dimensions:** 64x64px (circular) or 60x80px (rectangular with rounded corners)
- **Border width:** 3-4px for team identification
- **Active character scale:** 1.3x with 0-4px glow
- **Spacing between portraits:** 12px
- **HP bar dimensions:** Full width of portrait, 6-8px height, positioned 2-4px below portrait
- **Initiative badge:** 28x28px circle, positioned top-right corner with -8px offset

---

## 2. ACTION ECONOMY BAR (Bottom Left)

### Current State
- Four resource counters displaying:
  - "ACT 1/1" (green background)
  - "BNG 1/1" (orange background)
  - "MOV 30/30" (yellow background with progress bar)
  - "RXN 1/1" (purple background)
- Width: ~45-50px per counter
- Height: ~40-45px
- Located absolute bottom-left corner
- No spacing between counters

### Critical Issues

**A. Visual Design**
1. **Abbreviations unclear** - "BNG" should be "BONUS" or use icon instead
2. **Text-only representation** - Should use iconic symbols (BG3 uses circle for Action, triangle for Bonus)
3. **Flat color blocks** - Missing depth, shadows, or gradient treatment
4. **Progress bar only on MOV** - All depleting resources should show fill level
5. **No depletion animation** - Should animate when resources are consumed
6. **Hard to read at a glance** - Lacks visual hierarchy

**B. Layout Issues**
1. **Too far left** - Should be positioned 180-220px from left edge for ergonomics
2. **Bottom-aligned** - Should have 20-30px margin from bottom
3. **No grouping logic** - Movement should be separated from action economy
4. **Horizontal arrangement problematic** - Consider vertical stack or radial arrangement
5. **Counters touch edges** - Need 4-6px gap between elements

**C. Functionality Problems**
1. **No hover states** - Should display tooltip explaining resource type
2. **No cost preview** - When hovering over abilities, should show resource cost preview (flash/pulse effect)
3. **Missing state indicators:**
   - Greyed out when depleted
   - Pulsing when full and unused
   - Warning glow when action available but turn about to end

**D. Typography**
1. **Font size too small** - Resource values should be 18-22px bold
2. **Label text unnecessary** - "ACT", "BNG", etc. can be replaced with icons
3. **Fraction format** - "1/1" could be clearer as a fill bar with overlay number

**E. Recommended Specifications**
- **Container position:** X: 200px from left, Y: 40px from bottom
- **Icon size:** 48x48px per resource type
- **Fill indicator:** Radial progress (360° fill) or segmented display
- **Resource icons:**
  - Action: Solid green circle (⬤)
  - Bonus Action: Orange triangle (▲)
  - Movement: Yellow bar graph or footsteps icon
  - Reaction: Purple shield or spark icon
- **Spacing:** 8-10px horizontal gap between resources
- **Depth:** 2-3px drop shadow, inner highlight for active state
- **Animation:** 200ms ease-out when value changes

---

## 3. HOTBAR SYSTEM (Bottom Center)

### Current State
- Numbered grid showing slots 2-9, then 0
- Empty slots (no ability icons)
- Dark background with subtle grid lines
- Width: ~600-650px
- Height: ~50-55px
- Single row layout

### Critical Issues

**A. Missing Core Features**
1. **No ability icons** - Completely empty (critical blocker)
2. **No keybind overlays** - Numbers should be rendered on icon corners
3. **No category tabs** - Missing Common/Class/Spells/Items/Passives tabs
4. **No cooldown overlays** - Need radial sweep or darkening for abilities on cooldown
5. **No resource cost indicators** - Must show action point cost (green dot), bonus action (orange dot), spell slot level
6. **No range indicators** - Icon borders should show if target is in range (red = out of range)

**B. Layout Problems**
1. **Single row insufficient** - BG3 uses 12 slots × multiple rows or tabs
2. **Slot numbering starts at 2** - Should be 1-9, then 0, or use F1-F12
3. **No slot 1** - Missing first slot
4. **Fixed grid** - Should expand vertically when abilities overflow
5. **No visual separation** - All slots blend together; need 2-3px gaps

**C. Visual Design**
1. **Slots too small** - Recommend 56x56px minimum for touch/click targets
2. **Background too dark** - Low contrast makes it hard to see empty state
3. **No hover state** - Should brighten or outline on mouseover
4. **No selection state** - Active ability needs distinct highlight
5. **No disabled state** - Greyed out when ability cannot be used

**D. Interaction Feedback**
1. **No tooltip support** - Hovering should show:
   - Ability name
   - Description
   - Resource cost
   - Range/area of effect
   - Damage/effect preview
2. **No drag-and-drop** - Should allow ability reorganization
3. **No right-click context menu** - For examining ability details
4. **No selected ability indicator** - When targeting, show which ability is active

**E. Recommended Specifications**
- **Container position:** Centered horizontally, 24-32px from bottom
- **Slot dimensions:** 56x56px
- **Slot spacing:** 4px horizontal, 8px vertical between rows
- **Border radius:** 4-6px per slot
- **Layout:** 12 slots per row, up to 3 rows visible
- **Tab bar height:** 32-36px above hotbar
- **Tab labels:** 14-16px font, bold on active tab
- **Resource cost badge:** 14x14px circle, positioned bottom-right of icon
- **Keybind label:** 16x16px square, positioned top-left, semi-transparent black background
- **Cooldown overlay:** Radial gradient from center, 70% opacity
- **Out-of-range overlay:** Red diagonal line or red border (3px)

**F. Empty State**
- Current empty slots should show:
  - Dashed border (2px, 40% opacity)
  - "+" icon in center (24x24px, 30% opacity)
  - Tooltip: "Drag ability here or press + to assign"

---

## 4. END TURN BUTTON (Bottom Right)

### Current State
- Large yellow/gold button with "END TURN" text
- Positioned bottom-right corner
- Width: ~180-200px
- Height: ~60-70px
- Dark brown/black text
- Simple rectangular shape with slight border

### Critical Issues

**A. Visual Design**
1. **Color too muted** - BG3 uses vibrant cyan/teal (#00CED1 or similar)
2. **No glow/prominence** - Should pulse or glow when ready
3. **Rectangular corners** - Needs rounded corners (8-12px radius) or hex shape
4. **No icon** - Should include hourglass or arrow icon
5. **Flat design** - Missing gradient or depth effect
6. **No shadow** - Should have prominent drop shadow for clickability

**B. State Management**
1. **Always visible** - Should change states:
   - **Active (resources available):** Subtle pulse animation
   - **Prompt (no actions left):** Bright pulse, larger glow
   - **Disabled (not player's turn):** Greyed out, unclickable
2. **No confirmation mode** - Should show "Confirm?" on first click if actions remain
3. **No cancel option** - After clicking with actions remaining, should allow cancel

**C. Positioning**
1. **Too close to corner** - Should have 20-30px margin from right edge
2. **Bottom alignment inconsistent** - Should align with action economy bar baseline
3. **No relationship to hotbar** - Position doesn't flow with overall bottom UI layout

**D. Typography**
1. **Font size** - Should be 20-24px bold for prominence
2. **Text color** - Black on yellow is poor contrast; use white or very dark color
3. **No transition states** - Text should change:
   - "END TURN" (normal)
   - "END TURN?" (confirmation)
   - "WAITING..." (when disabled)

**E. Accessibility**
1. **No keyboard shortcut indicator** - Should show "(Enter)" or "(Space)"
2. **No sound feedback** - Click should trigger audio cue
3. **No haptic feedback** - For gamepad support

**F. Recommended Specifications**
- **Dimensions:** 160x80px (wider, shorter for better button ratio)
- **Position:** X: 24px from right edge, Y: 24px from bottom
- **Border radius:** 8px
- **Colors:**
  - Normal state: Cyan gradient (#00CED1 to #20B2AA)
  - Hover: Brighten 10%
  - Prompt state: Pulsing glow (0-8px cyan shadow at 60Hz)
  - Disabled: Desaturated grey (#4A4A4A)
- **Font:** 22px, weight 700, white text with 1px black text-shadow
- **Icon:** 32x32px hourglass or fast-forward arrows, positioned left of text
- **Animation:** 
  - Idle pulse: 2s ease-in-out loop, scale 1.0 to 1.02
  - Prompt pulse: 1s ease-in-out loop, scale 1.0 to 1.08, glow 0-12px
- **Shadow:** 0 4px 12px rgba(0,0,0,0.4)

---

## 5. COMBAT LOG (Right Panel)

### Current State
- Header: "Combat Log" (white text)
- Content area showing:
  - Cyan text: "Combat started with 4 combatants, seed 42"
  - White text: State transitions
  - Yellow text: "Round 1, Turn 0: Fighter"
  - Mixed formatting
- Background: Dark grey/black
- Width: ~260-280px
- Height: ~360-400px
- Positioned top-right area

### Critical Issues

**A. Visibility & Scrolling**
1. **Fixed height** - Should scroll with combat events
2. **No scrollbar visible** - Users can't tell there's more content
3. **No auto-scroll** - Should jump to newest entry
4. **No scroll-lock toggle** - Players should be able to lock scroll to review history
5. **Text wrapping** - Long entries need proper word wrap

**B. Content Formatting**
1. **Inconsistent color coding:**
   - Cyan for system messages (good)
   - Yellow for turn announcements (good)
   - White for everything else (bad - needs granularity)
2. **Missing color codes for:**
   - Damage dealt (red/orange)
   - Healing (green)
   - Buffs applied (blue)
   - Debuffs applied (purple)
   - Misses/failures (grey)
   - Critical hits (bright yellow/gold)
   - Saving throws (cyan for success, red for failure)
3. **No dice roll expansion** - "Rolled 15 (d20) + 3..." should be collapsible detail
4. **No timestamps** - Useful for debugging and review
5. **No icons** - Should prefix entries with ability/action icons

**C. Layout & Spacing**
1. **Header too plain** - Needs visual separation from content
2. **No padding** - Text touches edges; needs 12-16px padding
3. **Line height too tight** - Should be 1.4-1.6 for readability
4. **No entry separation** - Individual log entries should have subtle dividers (1px, 10% opacity)
5. **No turn separators** - Major turn boundaries should have thicker dividers

**D. Functionality**
1. **No filtering** - Should allow hiding system messages, showing only actions
2. **No export/copy** - Players can't copy log for bug reports
3. **No right-click context** - Should allow "Copy Entry", "Copy All", "Clear Log"
4. **No combat summary** - After combat ends, should show damage dealt/taken, kills, etc.

**E. Information Density**
1. **Too verbose** - "NotInCombat -> CombatStart (Combat initiated)" is implementation detail
2. **State machine transitions visible** - Should hide internal state names
3. **Missing key information:**
   - Attack roll breakdown (Base d20 + Proficiency + Modifier + Situational)
   - Damage type breakdown (Slashing/Piercing/Bludgeoning/Elemental)
   - Advantage/Disadvantage source ("Advantage from High Ground")
   - AC comparison ("20 vs AC 15 - Hit!")

**F. Recommended Specifications**

**Container:**
- **Position:** X: 16px from right edge, Y: 80px from top
- **Dimensions:** 320px wide, viewport height - 200px
- **Background:** rgba(20, 20, 25, 0.92) with 2px border
- **Border color:** rgba(255, 255, 255, 0.15)
- **Border radius:** 8px

**Header:**
- **Height:** 40px
- **Background:** rgba(40, 40, 45, 0.95)
- **Font:** 16px bold
- **Border bottom:** 2px solid rgba(255, 255, 255, 0.2)
- **Padding:** 12px
- **Controls:** Minimize button (top-right), filter button, clear button

**Content Area:**
- **Padding:** 12px
- **Scrollbar:** 
  - Width: 8px
  - Track: rgba(255, 255, 255, 0.05)
  - Thumb: rgba(255, 255, 255, 0.3)
  - Hover: rgba(255, 255, 255, 0.5)
- **Auto-scroll:** Enabled by default, disabled when user scrolls up
- **Scroll indicator:** "↓ New Messages" button when scrolled up

**Entry Formatting:**
- **Line height:** 1.5
- **Font size:** 13px
- **Entry padding:** 6px vertical, 0px horizontal
- **Entry separator:** 1px solid rgba(255, 255, 255, 0.08)
- **Turn separator:** 3px solid rgba(255, 255, 255, 0.2) with 12px margin

**Color Coding:**
```
System messages: #00CED1 (cyan)
Turn announcements: #FFD700 (gold)
Damage dealt: #FF6B6B (red)
Healing: #51CF66 (green)
Buffs: #4DABF7 (blue)
Debuffs: #CC5DE8 (purple)
Misses: #868E96 (grey)
Critical hits: #FFD43B (bright yellow) with ★ prefix
Saves (success): #51CF66
Saves (fail): #FF6B6B
Environmental: #FFA94D (orange)
```

**Expandable Details:**
- **Trigger:** Click on entry to expand
- **Content:** Show full roll breakdown, modifiers, sources
- **Visual:** Indent 16px, use monospace font for numbers
- **Example:**
  ```
  Fighter attacks Goblin - Hit! (18 damage)
    ↳ d20: 15 + Proficiency: 3 + STR: 2 = 20 vs AC 14
    ↳ Damage: 1d8(6) + STR(2) + Enchantment(10) = 18 Slashing
  ```

---

## 6. SCENARIO CONTROL PANEL (Left Panel)

### Current State
- Dropdown: "Scenario: effect_combo_test"
- Buttons: "Load" and "Restart"
- Text: "18 scenarios found 50/50"
- Text: "Initiative: 15"
- Section: "Active Effects (none)"
- Background: Dark grey/black
- Width: ~220-240px
- Positioned top-left

### Critical Issues

**A. Purpose & Visibility**
1. **Development UI in game** - This entire panel should be:
   - Hidden in production builds
   - Moved to debug overlay (toggled with F3 or ~ key)
   - Collapsed by default
2. **Covers gameplay** - Overlaps potential character portraits/party UI
3. **Non-standard placement** - Left side should be reserved for party management

**B. Functionality**
1. **Initiative display redundant** - Already shown in top bar
2. **"50/50" meaning unclear** - Needs label or tooltip
3. **Active Effects shows "(none)"** - Should be hidden when empty or show as "No Active Effects"
4. **Load/Restart in combat** - Dangerous; should prompt confirmation

**C. Recommended Approach**

**Option 1: Debug Overlay Mode**
- Press F3 to toggle visibility
- Panel becomes semi-transparent (60% opacity)
- Movable/draggable
- Collapsible sections
- Add to this panel:
  - Current game state
  - Active scenario name
  - Combat seed (for reproduction)
  - Frame rate / performance metrics
  - Quick restart button

**Option 2: Party Panel (Production)**
Replace scenario panel with BG3-style party management:
- **Height:** Viewport height - 40px
- **Width:** 280-320px
- **Position:** 16px from left, 80px from top
- **Background:** rgba(20, 20, 25, 0.85)

**Party Member Cards (Vertical Stack):**
Each card shows:
- **Portrait:** 80x80px circular or 72x96px rectangular
- **Name:** 16-18px bold
- **HP bar:** Full width, 12px height
  - Current/Max HP numbers overlaid
  - Color gradient: Green (100% HP) → Yellow (50%) → Red (< 25%)
- **Resource bars:** Smaller bars for class resources (Ki, Spell Slots, Rage)
- **Status icons:** Grid of 24x24px icons below portrait
- **Chain icon:** Toggle to group/ungroup for movement

**Card Spacing:** 12px vertical gap between cards

**Collapsed State:** Minimize to just portraits (64x64px) when not in combat

---

## 7. CHARACTER/TARGET PORTRAITS (Bottom Right)

### Current State
- Small area showing "Orc" label
- Pink/red blob (appears to be enemy portrait)
- Width: ~80-100px
- Height: ~60-80px
- Positioned bottom-right corner, left of END TURN button

### Critical Issues

**A. Missing Information**
1. **No HP bar** - Critical information missing
2. **No AC display** - Should show Armor Class
3. **No status effects** - No visible buffs/debuffs
4. **No character level/class** - Useful context
5. **No resource display** - For spellcasters, show spell slots

**B. Visual Quality**
1. **Blob placeholder** - Needs proper character portrait
2. **No background frame** - Portrait needs distinct border/frame
3. **Too small** - At least 120x120px needed for visibility
4. **No depth** - Flat appearance

**C. Layout**
1. **Overlaps with END TURN** - Poor positioning
2. **Should expand for target** - When targeting enemy, show:
   - Your character portrait (left)
   - VS indicator (center)
   - Target portrait (right)

**D. Recommended Specifications**

**Active Character Portrait:**
- **Position:** Bottom-right, 210px from right edge, 24px from bottom
- **Dimensions:** 140x140px
- **Border:** 4px, color-coded by class or health state
- **Border radius:** 8px or circular
- **Background:** Gradient appropriate to character class

**Portrait Content:**
- **Image:** High-quality character render or avatar
- **HP bar:** 
  - Position: Bottom of portrait, 8px from edge
  - Height: 14px
  - Border: 2px solid black
  - Fill: Gradient (Green/Yellow/Red based on %)
  - Text: "23/45" centered, 12px bold, white with 1px black outline
- **AC badge:**
  - Position: Top-left corner, 8px offset
  - Dimensions: 36x36px
  - Background: Shield icon or circular badge
  - Text: AC value (e.g., "18"), 14px bold
- **Level/Class:**
  - Position: Top-right corner, 8px offset
  - Format: "Lvl 5" or class icon
  - Font: 11px
- **Status icons:**
  - Position: Below portrait, centered
  - Grid: 28x28px icons, 4px spacing
  - Max visible: 5 icons, scroll if more

**Targeting Display (When Ability Selected):**
- **Expand to:** 450px wide
- **Layout:** [Your Portrait 120x120px] [VS 40px] [Target Portrait 120x120px] [Details 150px]
- **VS Indicator:**
  - Font: 24px bold
  - Color: White
  - Animation: Subtle pulse
- **Details Panel:**
  - Hit chance: "75%" (large, 32px)
  - Modifiers:
    - +2 High Ground (green)
    - -1 Half Cover (red)
  - Expected damage: "12-18" or "5 (burning, 3 turns)"
- **Animation:** Slide in from right (300ms ease-out)

---

## 8. 3D VIEWPORT & WORLD-SPACE UI

### Current State
- Brown/tan ground plane
- Two cyan cylinder characters with labels ("Mage", "Fighter")
- Flat shading
- No combat grid
- No targeting indicators
- No line-of-sight visualization

### Critical Issues

**A. Ground Grid**
1. **No tactical grid** - D&D combat requires 5ft squares visualization
2. **Flat color** - Needs slight texture or variation
3. **No height indicators** - Difficult to judge elevation
4. **No cover indicators** - Half/full cover not visualized

**B. Character Representation**
1. **Cylinders** - Placeholder geometry (acceptable for prototype)
2. **No facing indicator** - Direction character is facing unclear
3. **No selection ring** - Hard to see which character is selected
4. **No height variation** - All characters same height

**C. Targeting & Combat Visualization**
1. **No ground decals** - When selecting ability:
   - Reticle at cursor position
   - Range ring around caster
   - Area of effect preview (cone, circle, line)
2. **No pathing line** - Movement should show:
   - Dotted line from character to cursor
   - Color: Bright when in range, red when out of range
   - Remaining movement shown on line
3. **No threatened areas** - Enemy reach/opportunity attack zones not shown
4. **No line of sight** - No indication of cover or vision blocking

**D. Labels & Tooltips**
1. **Name labels always visible** - Should only show for:
   - Selected character
   - Hovered character
   - All enemies (optional toggle)
2. **Label positioning** - Floating above head, but no HP bar
3. **No distance indicator** - When hovering, show range (e.g., "25ft")

**E. Recommended Specifications**

**Tactical Grid:**
- **Square size:** 5ft (standard D&D)
- **Line width:** 1-2px
- **Line color:** rgba(255, 255, 255, 0.15)
- **Highlight on hover:** Brighten hovered square to rgba(255, 255, 255, 0.35)
- **Movement preview:** Highlight reachable squares in blue (0.3 alpha)
- **Out of range:** Tint red (0.2 alpha)

**Selection Rings:**
- **Friendly character:**
  - **Diameter:** Character base + 20-30%
  - **Color:** Green (#51CF66) for allies
  - **Thickness:** 6-8px
  - **Animation:** Rotating dashed pattern (2s loop) or gentle pulse
- **Enemy character:**
  - **Color:** Red (#FF6B6B)
- **Neutral:**
  - **Color:** Yellow (#FFD43B)
- **Hover (non-selected):**
  - **Color:** White
  - **Thickness:** 4px
  - **Alpha:** 0.6

**Targeting Cursor:**
- **Type:** 3D ground decal
- **Design:** Circular reticle with crosshair
- **Size:** 32x32px at 5ft distance
- **Color:** White (valid target), Red (invalid), Yellow (ally)
- **Animation:** Pulse scale 0.95-1.05 (1s loop)

**Range Ring:**
- **Show when:** Ability selected
- **Radius:** Ability range (e.g., 60ft)
- **Line:** 3px dashed circle
- **Color:** Cyan for friendly abilities, red for enemy
- **Fill:** None (just outline)

**Area Preview (AoE):**
- **Cone:** Wedge shape emanating from caster
- **Circle:** Radius from target point
- **Line:** Rectangle showing width and length
- **Color:** rgba(255, 100, 100, 0.25) with brighter edge
- **Affected characters:** Highlight in preview color

**Movement Path:**
- **Line type:** Dashed or arrow sequence
- **Width:** 8-10px
- **Color:** 
  - Green: Within movement range
  - Yellow: Requires dash action
  - Red: Out of range
- **Elevation changes:** Show slope or steps in path
- **Obstacles:** Path breaks or curves around obstacles

**Character Labels:**
- **Show conditions:**
  - Always: Selected character
  - Hover: Any character under cursor
  - Optional: All enemies (toggle in settings)
- **Panel design:**
  - **Background:** rgba(0, 0, 0, 0.85)
  - **Border radius:** 6px
  - **Padding:** 8px horizontal, 6px vertical
  - **Border:** 2px, color-coded by team
- **Content:**
  - **Line 1:** Name (16px bold)
  - **Line 2:** HP bar (full width, 8px height)
  - **Line 3:** Status icons (20x20px, max 4 visible)
- **Positioning:** 
  - Anchor: Top of character head
  - Offset: +60-80px vertical (above character)
  - Bill-boarding: Always face camera

**Distance Indicator:**
- **Show when:** Hovering over character with ability selected
- **Format:** "45 ft" or "9 squares"
- **Position:** Below character name in tooltip
- **Color:** Green (in range), Red (out of range)

---

## 9. MISSING SYSTEMS

### A. Party Management
- **No party portraits** - Should be left sidebar (see section 6)
- **No character swapping** - Cannot switch between party members
- **No shared initiative handling** - When multiple characters have same initiative
- **No chain/unchain system** - For grouped movement outside combat

### B. Ability System UI
- **No ability assignment** - Cannot drag abilities to hotbar
- **No spell book** - Spellcasters need full spell list UI
- **No prepared spells indicator** - Clerics/Wizards prepare subset
- **No concentration tracker** - Visual indicator for concentration spells

### C. Targeting System
- **No targeting mode toggle** - Automatic vs manual targeting
- **No target cycling** - Tab to cycle through valid targets
- **No quick-target buttons** - "Self", "Nearest Enemy", "Mouse Target"
- **No threat indicators** - No way to see which enemy is targeting which ally

### D. Status Effects
- **No buff/debuff UI** - Icons needed on characters and portraits
- **No duration tracker** - Turns remaining on effects
- **No effect tooltips** - Hovering status icon should explain effect
- **No concentration indicator** - Special highlight for concentration spells

### E. Environmental
- **No cover system visualization** - Half/three-quarters/full cover
- **No verticality indicators** - Height advantage/disadvantage
- **No lighting visualization** - Bright/dim/dark areas affect combat
- **No difficult terrain** - Movement cost variations

### F. Audio & Feedback
- **No audio cues** - Need sounds for:
  - Turn start
  - Action point spent
  - Ability cast
  - Hit/miss
  - Critical hit
  - Level up
  - Death
- **No screen shake** - Critical hits should shake camera
- **No damage numbers** - Floating combat text above characters
- **No hit flash** - Characters should flash on taking damage

### G. Accessibility
- **No colorblind modes** - Team colors need alternatives
- **No UI scaling** - Fixed size doesn't accommodate different resolutions
- **No keybind display** - Hotbar doesn't show keyboard shortcuts
- **No controller support UI** - Radial menu for gamepad needed
- **No high contrast mode** - For visibility

### H. Performance
- **No LOD system** - Far characters need simplified rendering
- **No batching** - Each UI element separate draw call (assumption)
- **No pooling** - Damage numbers/effects likely create garbage

---

## 10. PRIORITY MATRIX

### P0 - Blocking (Must Fix Before Playable)
1. Initiative bar showing character portraits and HP
2. Hotbar with actual ability icons
3. Action economy visual clarity (icons not abbreviations)
4. Selection rings on characters
5. Movement path visualization
6. Target selection reticle
7. End Turn button state management
8. Combat log color coding and readability

### P1 - Critical (Needed for Alpha)
9. Status effect icons on portraits
10. HP bars in 3D world space
11. Tactical grid
12. Range indicators for abilities
13. AoE preview
14. Party panel (replace scenario debug panel)
15. Ability tooltips on hotbar
16. Resource cost indicators
17. Targeting "VS" display

### P2 - Important (Polish)
18. Combat log filtering and export
19. Shared initiative grouping
20. Keyboard shortcut overlays
21. Hover states on all interactive elements
22. Animation polish (pulses, glows, transitions)
23. Audio feedback system
24. Damage number floating text
25. Hit flash VFX

### P3 - Enhancement (Can Defer)
26. Colorblind modes
27. UI scaling options
28. Controller radial menu
29. Advanced combat log (expandable entries)
30. Replay/timeline scrubber
31. Screenshot mode (hide UI)
32. Custom HUD layouts

---

## 11. TECHNICAL IMPLEMENTATION NOTES

### Scene Structure Recommendations
```
CombatArena
├── WorldView (SubViewportContainer)
│   ├── Camera3D
│   ├── Arena (3D scene)
│   └── WorldSpaceUI (Canvas layer at z=100)
│       ├── SelectionRings
│       ├── TargetingDecals
│       ├── MovementPath
│       └── CharacterLabels
└── HUD (CanvasLayer)
    ├── TopBar
    │   ├── InitiativeBar
    │   │   ├── PortraitContainer (HBoxContainer)
    │   │   │   ├── Portrait1 (Panel > VBox)
    │   │   │   │   ├── InitiativeBadge
    │   │   │   │   ├── AvatarTexture
    │   │   │   │   ├── HPBar
    │   │   │   │   └── StatusIcons (HBox)
    │   │   │   └── ... (more portraits)
    │   └── RoundCounter ("Round 3")
    ├── BottomBar (MarginContainer)
    │   ├── LeftGroup
    │   │   └── ActionEconomy (HBox)
    │   │       ├── ActionResource
    │   │       ├── BonusResource
    │   │       ├── MovementResource
    │   │       └── ReactionResource
    │   ├── CenterGroup
    │   │   └── Hotbar (TabContainer)
    │   │       ├── CommonTab (GridContainer 12 cols)
    │   │       ├── ClassTab
    │   │       ├── SpellsTab
    │   │       └── ItemsTab
    │   └── RightGroup
    │       ├── ActivePortrait
    │       └── EndTurnButton
    ├── RightPanel
    │   └── CombatLog (Panel > ScrollContainer)
    │       ├── Header (HBox: Title + Controls)
    │       └── LogEntries (VBoxContainer)
    └── LeftPanel (Debug/Party)
        └── PartyPanel
            └── MemberCards (VBoxContainer)
```

### Style Resources Needed
- **Theme resource** with consistent:
  - Colors (primary, secondary, accent, danger, success)
  - Font sizes (h1: 24px, h2: 18px, body: 14px, small: 12px)
  - Border radius (4px small, 8px medium, 12px large)
  - Shadows (elevation 1/2/3)
  - Transitions (200ms ease-out standard)

### Custom Control Classes
- `CombatPortrait` - Character portrait with HP, status, initiative
- `ResourceIndicator` - Action point display with icon and fill
- `AbilitySlot` - Hotbar slot with icon, keybind, cooldown, cost
- `LogEntry` - Formatted combat log entry with expandable details
- `TargetingCursor` - 3D to 2D projected cursor with range
- `AoEPreview` - Area of effect visualization
- `StatusIcon` - Buff/debuff icon with duration and tooltip

### Shader Opportunities
- **Selection ring shader** - Rotating dash pattern
- **HP bar gradient** - Smooth color transition based on percentage
- **Glow effect** - For End Turn button and prompts
- **Fresnel rim** - Character outline on hover
- **Cooldown sweep** - Radial progress on abilities

### Font Recommendations
- **Primary:** Roboto or Inter (clean, readable)
- **Accent:** Cinzel or Trajan (fantasy flavor for titles)
- **Monospace:** Roboto Mono (for numbers, dice rolls)

### Resolution & Scaling
- **Base design:** 1920x1080 (scale factor 1.0)
- **Scaling strategy:** 
  - 1280x720: Scale 0.67
  - 2560x1440: Scale 1.33
  - 3840x2160: Scale 2.0
- **Min safe zones:**
  - Top: 80px
  - Bottom: 200px
  - Left: 340px (if party panel visible)
  - Right: 360px (if combat log visible)
- **Center safe area:** Viewport - margins = gameplay focus

---

## 12. PIXEL-PERFECT MEASUREMENTS (1920x1080 Base)

### Initiative Bar
- **Container:**
  - X: 760px (centered for 400px width)
  - Y: 16px from top
  - Width: 400px (expandable)
  - Height: 88px
  
- **Per Portrait:**
  - Width: 80px
  - Height: 88px
  - Margin: 12px horizontal
  - Border: 3px
  - Avatar: 64x64px (centered)
  - HP bar: 72px wide, 10px tall, 6px from bottom
  - Initiative badge: 28x28px, positioned (-8px, -8px) from top-right
  - Status icon grid: 20x20px icons, 2px spacing, max 3 visible

### Action Economy
- **Container:**
  - X: 200px from left
  - Y: 948px from top (72px from bottom at 1080p)
  - Width: 256px
  - Height: 56px

- **Per Resource:**
  - Icon size: 52x52px
  - Spacing: 8px horizontal
  - Fill indicator: 48x48px (4px inner margin)
  - Value label: 18px font, positioned bottom-right

### Hotbar
- **Container:**
  - X: Centered (600px width = 660px from left)
  - Y: 968px from top (52px from bottom)
  - Width: 600px (12 slots × 48px + 11 × 4px spacing)
  - Height: 52px (single row) or 108px (with tabs)

- **Per Slot:**
  - Size: 48x48px
  - Spacing: 4px
  - Border: 2px
  - Keybind label: 16x16px, top-left
  - Cost badge: 14x14px, bottom-right
  - Cooldown overlay: Full size

- **Tab Bar (if present):**
  - Height: 32px
  - Tab width: 100px
  - Tab spacing: 0px (touching)
  - Active tab: 2px bottom border

### End Turn Button
- **Position:**
  - X: 1716px from left (204px from right at 1920px width)
  - Y: 972px from top (108px from bottom)
- **Size:** 180px × 88px
- **Border radius:** 8px
- **Icon:** 32x32px, 12px from left
- **Text:** 22px, positioned right of icon
- **Glow (prompt state):** 12px radial blur

### Combat Log
- **Container:**
  - X: 1588px from left (332px from right)
  - Y: 80px from top
  - Width: 312px
  - Height: 840px

- **Header:**
  - Height: 44px
  - Padding: 12px
  - Font: 16px bold

- **Content:**
  - Padding: 12px
  - Scrollbar: 8px wide
  - Entry padding: 8px vertical
  - Entry separator: 1px, rgba(255,255,255,0.08)
  - Line height: 20px (13px font × 1.5)

### Active Portrait
- **Position:**
  - X: 1508px from left (412px from right)
  - Y: 888px from top (192px from bottom)
- **Size:** 152px × 152px
- **HP bar:** 136px wide, 16px tall, 8px from bottom
- **AC badge:** 40x40px, positioned (8px, 8px) from top-left
- **Status icons:** 28x28px, 4px spacing, positioned below portrait

### World Space (per character at std distance)
- **Selection ring:** Character diameter + 40%, 8px thick
- **Name label:** Auto-width, 36px height, +80px vertical offset
- **HP bar (world):** 100px wide, 12px tall, +60px offset
- **Grid square:** Scaled to 5ft = ~80px at default camera distance

---

## SUMMARY

This HUD requires comprehensive overhaul across all systems. The current implementation has placeholders in position but lacks:

1. **Visual Polish:** Everything is too basic, lacking depth, color, and feedback
2. **Information Density:** Critical combat data is missing or unclear
3. **User Experience:** No tooltips, hover states, animations, or guidance
4. **Accessibility:** Fixed sizes, no scaling, poor contrast in places
5. **Functionality:** No ability system, status effects, or targeting feedback

**Immediate blockers:**
- Initiative portraits need HP and visual clarity
- Hotbar needs icons and interaction
- Selection/targeting system completely missing
- Action economy needs iconic representation

**Estimated work:** This is 60-80 hours of UI development + 20-30 hours of integration testing.

**Recommended approach:**
1. Build component library (20 hrs)
2. Implement priority P0 items (25 hrs)
3. Wire up interaction systems (15 hrs)
4. Polish and P1 items (20 hrs)
5. Testing and refinement (20 hrs)

All measurements assume 1920×1080 base resolution with responsive scaling.
