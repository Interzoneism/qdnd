# HUD Layout Priority - Functional Placeholder Implementation
**Date:** 2026-02-04  
**Scope:** Visual placement and sizing only - no graphics/portraits/icons required  
**Resolution:** 1920×1080 base (scale proportionally for other resolutions)

---

## LAYOUT OVERVIEW (Non-Overlapping Zones)

```
┌─────────────────────────────────────────────────────────────┐
│  [     Initiative Bar - Top Center     ]                    │ Y: 16px
│                                                              │
│  ┌──────────┐                           ┌─────────────────┐ │
│  │          │                           │   Combat Log    │ │ Y: 80px
│  │  Party   │                           │                 │ │
│  │  Panel   │      (3D Viewport)        │                 │ │
│  │          │                           │                 │ │
│  │ (Debug)  │                           │                 │ │
│  └──────────┘                           │                 │ │
│                                         │                 │ │
│                                         └─────────────────┘ │
│                                                              │
│                                         ┌────────┐           │
│  ┌─────────┐  ┌──────────────────┐    │Portrait│ ┌───────┐│ Y: 948px
│  │ Actions │  │     Hotbar       │    └────────┘ │  END  ││
│  └─────────┘  └──────────────────┘               │ TURN  ││
└─────────────────────────────────────────────────────────────┘
   X: 200px     X: 660px                X: 1508px  X: 1716px
```

---

## CRITICAL: REMOVE OVERLAPS

### Current Problems
1. **Scenario panel (left)** overlaps gameplay area - HIDE or move
2. **Portrait + End Turn (bottom-right)** currently overlap - SEPARATE
3. **Combat Log** positioned correctly but needs defined bounds

### Solution
**Option A (Recommended):** Hide scenario/debug panel entirely
- Move to F3 debug overlay
- Frees up left side for unobstructed view

**Option B:** Collapse scenario panel to minimal size
- Width: 200px → 160px
- Show only essential debug info
- Add collapse/expand toggle button

---

## PANEL SPECIFICATIONS (Pixel-Perfect Measurements)

### 1. INITIATIVE BAR (Top Center)
**Purpose:** Show turn order with HP visibility

**Container:**
- **Position:** X: 660px, Y: 16px (from top-left origin)
- **Size:** 600px wide × 80px tall
- **Background:** rgba(0, 0, 0, 0.7)
- **Border:** 2px solid rgba(255, 255, 255, 0.2)
- **Border Radius:** 8px

**Character Boxes (Horizontal HBoxContainer):**
- **Per-box size:** 120px wide × 72px tall
- **Spacing:** 12px horizontal gap between boxes
- **Max visible:** 4 boxes (expand width if more combatants)
- **Border:** 3px solid, color-coded:
  - Active turn: #FFD700 (gold)
  - Allies: #4DABF7 (blue)
  - Enemies: #FF6B6B (red)

**Text Content (each box):**
```
┌─────────────────┐
│ 15       Fighter│ ← Initiative (top-left), Name (top-right)
│                 │
│  ██████░░░░ 12  │ ← HP bar + current HP (bottom)
│     /24         │ ← max HP (smaller, below)
└─────────────────┘
```

**Text Specifications:**
- **Initiative number:** 18px bold, top-left corner, 4px padding
- **Name:** 14px regular, top-right corner, 4px padding, truncate if > 8 chars
- **HP bar:** Full width minus 8px padding, 10px tall, 4px from bottom
  - Color: Green (#51CF66) when > 50%, Yellow (#FFD43B) 25-50%, Red (#FF6B6B) < 25%
- **HP numbers:** 14px bold, centered over HP bar, white with 1px black text-shadow
- **Max HP:** 11px regular, below HP bar, centered, 60% opacity

---

### 2. ACTION ECONOMY (Bottom-Left)
**Purpose:** Show available action points

**Container:**
- **Position:** X: 200px, Y: 948px (from top-left)
- **Size:** 280px wide × 64px tall
- **Background:** rgba(0, 0, 0, 0.6)
- **Border Radius:** 8px

**Resource Boxes (Horizontal HBoxContainer):**
- **Per-resource size:** 62px wide × 56px tall
- **Spacing:** 6px horizontal gap
- **Count:** 4 resources (Action, Bonus, Movement, Reaction)

**Layout per resource:**
```
┌──────────┐
│  ACT     │ ← Label (12px, top)
│  ████    │ ← Fill bar or circle
│  1/1     │ ← Current/Max (16px bold, bottom)
└──────────┘
```

**Resource Colors:**
- **ACT (Action):** #51CF66 (green)
- **BNG (Bonus Action):** #FFA94D (orange)
- **MOV (Movement):** #FFD43B (yellow)
- **RXN (Reaction):** #CC5DE8 (purple)

**Fill Indicator:**
- **Background:** rgba(255, 255, 255, 0.1)
- **Fill:** Full color when available, 20% opacity when depleted
- **Size:** 54px wide × 32px tall (centered in box)
- **Type:** Simple horizontal bar (no icons needed for placeholder)

---

### 3. HOTBAR (Bottom-Center)
**Purpose:** Ability slots

**Container:**
- **Position:** X: 660px (centered), Y: 968px
- **Size:** 608px wide × 56px tall (single row, no tabs for now)
- **Background:** rgba(0, 0, 0, 0.7)
- **Border:** 2px solid rgba(255, 255, 255, 0.15)
- **Border Radius:** 6px

**Ability Slots (Horizontal GridContainer, 12 columns):**
- **Per-slot size:** 48px × 48px
- **Spacing:** 4px horizontal gap
- **Border:** 2px solid rgba(255, 255, 255, 0.3)
- **Border Radius:** 4px
- **Background:** rgba(60, 60, 60, 0.8)

**Slot Numbers:**
- **Position:** Top-left corner of each slot
- **Font:** 11px bold
- **Color:** White with 50% black background
- **Padding:** 2px
- **Labels:** 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, -, =

**Empty State:**
- Show slot number only
- Dashed border (2px, 30% opacity)
- Background: rgba(40, 40, 40, 0.5)

---

### 4. END TURN BUTTON (Bottom-Right)
**Purpose:** End active turn

**Position:** X: 1716px, Y: 956px
**Size:** 180px wide × 84px tall
**Background:** #00CED1 (cyan) - This is the most important color change!
**Border:** None
**Border Radius:** 8px
**Shadow:** 0 4px 8px rgba(0, 0, 0, 0.3)

**Text:**
- **Content:** "END TURN"
- **Font:** 22px bold
- **Color:** White (#FFFFFF)
- **Alignment:** Center
- **Text Shadow:** 1px 1px 2px rgba(0, 0, 0, 0.5)

**States:**
- **Normal:** Cyan background, no animation (for placeholder)
- **Hover:** Brighten by 15%
- **Disabled:** #666666 (grey), reduce opacity to 50%

---

### 5. COMBAT LOG (Right Panel)
**Purpose:** Show combat events

**Container:**
- **Position:** X: 1588px, Y: 80px
- **Size:** 312px wide × 840px tall
- **Background:** rgba(20, 20, 25, 0.9)
- **Border:** 2px solid rgba(255, 255, 255, 0.15)
- **Border Radius:** 8px

**Header:**
- **Height:** 40px
- **Background:** rgba(30, 30, 35, 0.95)
- **Border Bottom:** 1px solid rgba(255, 255, 255, 0.2)
- **Text:** "Combat Log", 16px bold, white, centered vertically, 12px left padding

**Content Area:**
- **Padding:** 12px all sides
- **Background:** Transparent
- **Scrollbar:** 8px wide (right edge)
  - Track: rgba(255, 255, 255, 0.05)
  - Thumb: rgba(255, 255, 255, 0.2)

**Log Entries:**
- **Font:** 13px regular
- **Line Height:** 1.4 (18.2px)
- **Padding:** 6px vertical per entry
- **Entry Separator:** 1px solid rgba(255, 255, 255, 0.05)
- **Text Color (basic color coding):**
  - System: #00CED1 (cyan)
  - Turn start: #FFD700 (gold)
  - Damage: #FF6B6B (red)
  - Healing: #51CF66 (green)
  - Default: #CCCCCC (light grey)

---

### 6. CHARACTER PORTRAIT (Bottom-Right, Left of End Turn)
**Purpose:** Show active character info

**Position:** X: 1508px, Y: 888px
**Size:** 144px wide × 144px tall
**Background:** rgba(40, 40, 45, 0.9)
**Border:** 4px solid (color based on health):
- Green #51CF66 when > 50% HP
- Yellow #FFD43B when 25-50% HP
- Red #FF6B6B when < 25% HP
**Border Radius:** 8px

**Content (placeholder - no portrait image):**
```
┌──────────────┐
│   AC: 15     │ ← Top: AC display (if available)
│              │
│    FIGHTER   │ ← Center: Character name (14px bold)
│              │
│ ████████░░   │ ← Bottom: HP bar
│    18/24     │ ← HP numbers
└──────────────┘
```

**Text Specifications:**
- **AC Label:** 12px regular, top-left, 8px padding
- **Name:** 14px bold, centered vertically and horizontally
- **HP Bar:** 128px wide (8px margin), 12px tall, 8px from bottom
- **HP Numbers:** 13px bold, centered over HP bar

---

### 7. PARTY/DEBUG PANEL (Left Side) - OPTIONAL
**Current Size:** ~220px wide × ~360px tall  
**Recommendation:** **HIDE THIS ENTIRELY FOR FUNCTIONAL HUD**

**If must keep for debugging:**
- **Position:** X: 12px, Y: 80px
- **Size:** 160px wide × 300px tall (reduced)
- **Background:** rgba(20, 20, 25, 0.7)
- **Border:** 2px solid rgba(255, 255, 255, 0.1)
- **Opacity:** 60% to reduce visual weight

**Add collapse button:**
- 24px × 24px, top-right corner
- Click to hide/show panel
- Default state: Hidden

---

## 3D VIEWPORT UI (World-Space Elements)

### Selection Rings (Ground Decals)
**Purpose:** Show which character is selected

**Specification:**
- **Type:** 3D circle mesh or decal on ground plane
- **Radius:** Character base radius + 0.3m (roughly 30% larger than character)
- **Line Thickness:** 0.08m (world units)
- **Color:**
  - Selected ally: #51CF66 (green)
  - Selected enemy: #FF6B6B (red)
  - Hovered: #FFFFFF (white) at 60% opacity
- **Material:** Unshaded, additive blend
- **Animation (optional):** None for placeholder (can add rotation later)

### Character Name Labels (Billboard UI)
**Purpose:** Show character names in 3D space

**Specification:**
- **Position:** Above character head, +1.8m vertical offset
- **Size:** Auto-width based on text, 32px height
- **Background:** rgba(0, 0, 0, 0.8)
- **Border:** 2px solid, color matches selection ring
- **Border Radius:** 4px
- **Padding:** 8px horizontal, 4px vertical

**Text:**
- **Font:** 14px bold
- **Color:** White
- **Alignment:** Center
- **Text Shadow:** 1px 1px 2px black

**Visibility:**
- Always show for selected character
- Show for hovered character
- Optional: Always show for all enemies (toggle)

### HP Bars (World-Space)
**Purpose:** Show HP above character heads

**Specification:**
- **Position:** Above character head, +1.5m vertical offset
- **Size:** 80px wide × 10px tall (in screen space, not world space)
- **Background:** rgba(0, 0, 0, 0.6)
- **Border:** 1px solid rgba(255, 255, 255, 0.3)

**Fill:**
- **Color:** Same as initiative bar colors (green/yellow/red based on %)
- **Direction:** Left to right
- **Smooth transition:** Animate when HP changes

**HP Text:**
- **Content:** "18/24" format
- **Font:** 11px bold
- **Color:** White with 1px black outline
- **Position:** Centered on bar

---

## MOVEMENT & TARGETING VISUALS

### Movement Path
**Purpose:** Show where character will move

**Specification:**
- **Type:** 3D line or series of ground decals
- **Width:** 0.15m (world units)
- **Color:** 
  - In range: #51CF66 (green) at 70% opacity
  - Out of range: #FF6B6B (red) at 70% opacity
- **Style:** Dashed line (0.3m dash, 0.15m gap) OR solid with arrows
- **Update:** Recalculate on mouse move

### Targeting Cursor
**Purpose:** Show where player is aiming

**Specification:**
- **Type:** 3D ground decal
- **Size:** 0.5m diameter (world units)
- **Design:** Circle with crosshair
- **Color:**
  - Valid target: #FFFFFF (white)
  - Invalid: #FF6B6B (red)
  - Friendly target: #FFD43B (yellow)
- **Opacity:** 60%
- **Material:** Unshaded, additive

---

## IMPLEMENTATION PRIORITY

### Phase 1 - Layout Fix (Immediate)
**Goal:** Stop overlapping elements

1. **Hide or collapse scenario/debug panel (left side)**
   - Frees up left 220px of screen
   - Time: 15 minutes

2. **Reposition character portrait to not overlap End Turn**
   - Current: Overlapping
   - New: X: 1508px (208px from right edge)
   - Time: 10 minutes

3. **Verify combat log doesn't overlap portrait**
   - Current position seems OK but confirm
   - Time: 5 minutes

**Total: 30 minutes**

---

### Phase 2 - Visual Clarity (Priority)
**Goal:** Make critical info readable

1. **Initiative bar improvements**
   - Add HP bars to each character box
   - Color-code borders (ally/enemy/active)
   - Show current/max HP numbers
   - Time: 2 hours

2. **Fix End Turn button color**
   - Change from yellow to cyan (#00CED1)
   - Ensure white text is readable
   - Time: 15 minutes

3. **Action economy visual fixes**
   - Make resource boxes clearly show current/max
   - Add fill bars or visual depletion indicator
   - Color code each resource type
   - Time: 1.5 hours

4. **Add selection rings to 3D characters**
   - Create circle mesh/decal
   - Color code (green ally, red enemy)
   - Show on selected character only
   - Time: 2 hours

**Total: 6 hours**

---

### Phase 3 - Functional Completeness
**Goal:** All panels working at basic level

1. **Hotbar slot states**
   - Empty state styling
   - Show slot numbers
   - (Icons/abilities not needed yet)
   - Time: 1 hour

2. **Combat log color coding**
   - System messages: cyan
   - Turn announcements: gold
   - Damage: red
   - Healing: green
   - Time: 1.5 hours

3. **Character portrait updates**
   - Show character name
   - HP bar with current/max
   - AC display (if data available)
   - Health-based border color
   - Time: 2 hours

4. **World-space name labels**
   - Billboard above character
   - Show on select/hover
   - Time: 1.5 hours

5. **Movement path visualization**
   - Draw line from character to cursor
   - Color based on range (green/red)
   - Time: 2.5 hours

**Total: 8.5 hours**

---

## MEASUREMENT SUMMARY TABLE

| Element | X Position | Y Position | Width | Height | Notes |
|---------|-----------|-----------|-------|--------|-------|
| **Initiative Bar** | 660px | 16px | 600px | 80px | Top-center, expandable |
| Initiative Box (each) | - | - | 120px | 72px | 12px spacing between |
| **Action Economy** | 200px | 948px | 280px | 64px | Bottom-left |
| Resource Box (each) | - | - | 62px | 56px | 6px spacing |
| **Hotbar** | 660px | 968px | 608px | 56px | Bottom-center |
| Hotbar Slot (each) | - | - | 48px | 48px | 4px spacing, 12 slots |
| **End Turn Button** | 1716px | 956px | 180px | 84px | Bottom-right corner |
| **Combat Log** | 1588px | 80px | 312px | 840px | Right panel |
| Combat Log Header | - | - | 312px | 40px | Inside log |
| **Character Portrait** | 1508px | 888px | 144px | 144px | Bottom-right, left of end turn |
| **Debug Panel** | 12px | 80px | 160px | 300px | HIDE by default |

**Margins/Spacing:**
- Top margin: 16px minimum
- Bottom margin: 52px minimum (1080 - 968 - 56 - 4)
- Left margin: 200px (with action economy visible)
- Right margin: 204px (1920 - 1716)
- Inter-panel spacing: 8-12px minimum

---

## SCALING FOR OTHER RESOLUTIONS

**Formula:** `actual_value = base_value × (screen_height / 1080)`

**Common Resolutions:**
- **1280×720:** Scale factor = 0.667
  - Example: Initiative bar Y = 16 × 0.667 = 11px
- **1920×1080:** Scale factor = 1.0 (base measurements)
- **2560×1440:** Scale factor = 1.333
  - Example: Hotbar width = 608 × 1.333 = 811px
- **3840×2160:** Scale factor = 2.0
  - Double all measurements

**Important:** Scale from top-left origin. Bottom-positioned elements need calculation:
- Formula: `Y_position = screen_height - (base_bottom_margin × scale_factor)`

---

## GODOT IMPLEMENTATION NOTES

### Scene Structure (Minimal)
```
CombatArena
└── HUD (CanvasLayer)
    ├── TopBar (MarginContainer - margins: 16,16,16,0)
    │   └── InitiativeBar (HBoxContainer)
    │       ├── CharacterBox1 (Panel > VBoxContainer)
    │       ├── CharacterBox2
    │       └── ...
    ├── BottomLeft (MarginContainer - margins: 200,0,0,52)
    │   └── ActionEconomy (HBoxContainer)
    │       ├── ActionResource (Panel > VBoxContainer)
    │       ├── BonusResource
    │       ├── MovementResource
    │       └── ReactionResource
    ├── BottomCenter (MarginContainer - margins: 660,0,660,52)
    │   └── Hotbar (GridContainer - 12 columns)
    │       ├── Slot1 (Panel)
    │       └── ... (12 slots)
    ├── BottomRight (MarginContainer - margins: 0,0,24,32)
    │   ├── Portrait (Panel)
    │   └── EndTurnButton (Button)
    └── RightPanel (MarginContainer - margins: 0,80,20,160)
        └── CombatLog (Panel > VBoxContainer)
            ├── Header (Panel)
            └── ScrollContainer
                └── LogEntries (VBoxContainer)
```

### Anchors & Layout
- **TopBar:** Anchor preset = Top Wide, grow horizontal
- **BottomLeft:** Anchor preset = Bottom Left, grow horizontal = Begin
- **BottomCenter:** Anchor preset = Bottom Wide, grow horizontal = Both
- **BottomRight:** Anchor preset = Bottom Right, grow horizontal = End
- **RightPanel:** Anchor preset = Right Wide, grow horizontal = End

### Control Sizing
- **Initiative boxes:** `custom_minimum_size = Vector2(120, 72)`
- **Resource boxes:** `custom_minimum_size = Vector2(62, 56)`
- **Hotbar slots:** `custom_minimum_size = Vector2(48, 48)`
- **End Turn button:** `custom_minimum_size = Vector2(180, 84)`

### Theme Overrides (For Consistency)
Create a theme resource with:
- **Panel StyleBox:** Background color, border, corner radius
- **Font sizes:** 22px (large), 16px (medium), 14px (normal), 11px (small)
- **Colors:** Define accent colors (cyan, green, yellow, red, etc.)

---

## VALIDATION CHECKLIST

After implementation, verify:

- [ ] No UI elements overlap (especially portrait + end turn button)
- [ ] All text is readable (contrast ratio > 4.5:1)
- [ ] Initiative bar shows HP for all combatants
- [ ] Action economy shows current/max for all resources
- [ ] End Turn button is CYAN, not yellow
- [ ] Combat log scrolls and doesn't overflow
- [ ] Selection rings visible on ground
- [ ] Name labels appear above selected characters
- [ ] All measurements match specification (± 4px tolerance)
- [ ] UI scales correctly at 1280×720 and 2560×1440
- [ ] Debug panel is hidden by default
- [ ] Hotbar shows 12 empty slots with numbers

---

## ESTIMATED TIME

**Total implementation time:** ~15 hours
- Layout fixes: 0.5 hours
- Initiative bar: 2 hours
- Action economy: 1.5 hours
- Hotbar: 1 hour
- End Turn button: 0.25 hours
- Combat log: 1.5 hours
- Character portrait: 2 hours
- 3D selection rings: 2 hours
- Name labels: 1.5 hours
- Movement path: 2.5 hours
- Testing & fixes: 0.25 hours

**Absolute minimum (just fix overlaps + colors):** ~3 hours
- Hide debug panel: 0.25 hours
- Reposition portrait: 0.25 hours
- Fix End Turn color: 0.15 hours
- Add basic HP bars to initiative: 1.5 hours
- Add selection rings: 1 hour

---

## NEXT STEPS

1. Implement Phase 1 (Layout Fix) - **30 minutes**
2. Change End Turn button color - **15 minutes**
3. Add HP bars to initiative boxes - **2 hours**
4. Add selection rings - **2 hours**
5. Test at different resolutions - **30 minutes**

**First playable checkpoint:** ~5.5 hours of work
