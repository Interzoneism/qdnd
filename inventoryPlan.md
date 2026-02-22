# Plan: BG3-Quality Unified Character & Inventory Screen

**TL;DR**: Replace the separate `CharacterSheetModal` and `InventoryPanel` with a single full-screen `CharacterInventoryScreen` — a unified overlay with 3 top-bar tabs (Equipment, Inventory, Character). The Equipment tab (shown in the screenshot) is the centerpiece: left panel with character stats & notable features, center 3D character model with melee/ranged weapon stats below, and right panel with a filterable item grid. This is a ~2500-line rewrite touching 6 existing files and creating ~10 new ones. The current `InventoryService` data layer stays intact; only UI is rebuilt.

---

## Phase 1: Unified Screen Shell & Tab Navigation

1. **Create** `Combat/UI/Screens/CharacterInventoryScreen.cs` — new top-level `Control` that fills the viewport. Structure:
   - **Top bar** (`HBoxContainer`): character name label (left-aligned gold text), 3 circular tab buttons (center), level/XP progress bar (right of tabs), weight display (far right)
   - **Content area** (`Control` that swaps children per tab): `EquipmentTab`, `InventoryTab`, `CharacterTab`
   - **Bottom bar** (`HBoxContainer`): weight progress bar (66/120 style) + encumbrance status
   - Toggle via "I" key (replaces current `ToggleInventory`). ESC closes.
   - Dim background overlay behind the screen (reuse `HudWindowManager` dimmer pattern).

2. **Create** `Combat/UI/Screens/ScreenTabButton.cs` — circular icon button matching BG3's top-bar tabs. Each tab button is a 48×48 `TextureButton` inside a `PanelContainer` with circular border (gold when active, muted when inactive). Use `HudTheme.Gold` for active border, `HudTheme.PanelBorderSubtle` for inactive. The three tabs use placeholder icons initially: sword+shield (Equipment), grid (Inventory), person (Character).

3. **Update** `Combat/UI/HudController.cs` — replace `_characterSheet` + `_inventoryPanel` with single `_characterInventoryScreen`. Change `ToggleInventory()` to open the unified screen. Remove `CharacterSheetModal` creation. Update `BuildCharacterSheetData()` to feed the new screen's character tab. Adjust layout persistence keys.

---

## Phase 2: Equipment Tab (The Main View — Matching the Screenshot)

4. **Create** `Combat/UI/Screens/EquipmentTab.cs` — the primary tab, a 3-column `HBoxContainer`:

   ### Left Column (Character Info Panel — ~30% width)

   - **Sub-tab row**: 4 small icon buttons at top (matching BG3's equipment sub-views). For now only the first is functional (stats overview); the rest are placeholders.
   - **Race/Class header**: Race name in gold (`HudTheme.Gold`), "Level N ClassName" below in `HudTheme.MutedBeige`. Both center-aligned.
   - **Ability scores row**: A horizontal `HBoxContainer` with 6 `AbilityScoreBox` controls — each is a small square panel (approx 42×42) with the 3-letter abbreviation (`STR`, `DEX`, etc.) as a top label in small font, and the score value (e.g., `8`, `14`, `16`) as the main centered number in larger white font. The box has a dark background with subtle gold border. Layout: all 6 in a row, evenly spaced.
   - **Resistances section**: Decorative divider (thin gold line with ornamental flourishes on each side, using `SectionDivider` control), header text "Resistances" in `HudTheme.Gold`, then value "None" in `HudTheme.MutedBeige` (or list resistance icons when applicable).
   - **Notable Features section**: Same ornamental divider pattern, header "Notable Features" in `HudTheme.Gold`, then a `VBoxContainer` listing each feature as an `HBoxContainer` with: a small 20×20 icon (passive/feat icon from `assets/Images/Icons`) + feature name in colored text (green for combat features, white for racial). Features sourced from `ResolvedCharacter.GrantedFeatures` + `Combatant.Passives`.

   ### Center Column (3D Model + Weapons — ~40% width)

   - **3D SubViewport** (same pattern as current `InventoryPanel.BuildModelFrame` — `SubViewportContainer` with `SubViewport`, `Camera3D`, `Node3D` model container). Camera positioned to show full body. Takes up ~70% of vertical space.
   - **Weapon Stats Panel** (bottom ~30%): Two weapon display areas side-by-side:
     - **Melee section** (left): Label "Melee" above a 48×48 equipment slot (shows main-hand weapon icon), below it: "+N" attack bonus label, "X~Y" damage range label
     - **AC Badge** (center): Shield-shaped `PanelContainer` with "AC" label on top and the AC value (e.g., `17`) as a large centered number. Dark background with gold border. Approximately 56×72px.
     - **Ranged section** (right): Label "Ranged" above a 48×48 equipment slot (shows ranged weapon icon), below it: "+N" attack bonus, "X~Y" damage range
     - Below weapon stats: centered labels "Attack Bonus" and "Damage" in `HudTheme.TextDim` as column headers
   - Weapon stats computed from `Combatant.Stats` + equipped weapon `WeaponDefinition` (attack bonus = proficiency + ability mod; damage = dice range + ability mod).

   ### Right Column (Equipment Slots — ~30% width)

   - **Paper doll slots** arranged in BG3 layout. From the screenshot, it's roughly a 4×5 grid of equipment slots occupying the right column. Each slot is the existing `ActivatableContainerControl` at 54×54px with item icon, stack count overlay, and rarity-colored background.
   - Slot arrangement (top-to-bottom, left-to-right): Helmet row, Amulet row, Armor + Cloak row, Gloves row, Boots + Ring1 + Ring2 row, MainHand + OffHand, RangedMainHand + RangedOffHand. Empty slots show the slot type icon in dim gray.
   - Drag/drop: reuse all existing drag/drop logic from `InventoryPanel` (the `DragDataProvider`/`CanDropDataProvider`/`DropDataHandler` pattern).

5. **Create** `Combat/UI/Controls/AbilityScoreBox.cs` — small square control displaying one ability. Constructor takes abbreviation string + score int. Layout: `PanelContainer` → `VBoxContainer` → top abbreviation `Label` (FontTiny, `HudTheme.GoldMuted`) + bottom score `Label` (FontLarge, `HudTheme.WarmWhite`). Background: `HudTheme.CreatePanelStyle(SecondaryDark, PanelBorderSubtle, cornerRadius: 4)`.

6. **Create** `Combat/UI/Controls/WeaponStatDisplay.cs` — displays one weapon set (melee or ranged). Takes weapon slot + `Combatant` ref. Shows: weapon icon slot, attack bonus, damage range. Computes values from `WeaponDefinition.DamageDice` + ability modifier + proficiency bonus.

7. **Create** `Combat/UI/Controls/AcBadge.cs` — shield-shaped AC display. A `PanelContainer` with "AC" label above and value below. Styled with a `StyleBoxFlat` darker than surrounding area, gold border, small corner radius.

8. **Create** `Combat/UI/Controls/SectionDivider.cs` — the ornamental section header from BG3. An `HBoxContainer` with: left ornament label (e.g., `~~` unicode tilde flourishes), gold `HSeparator`, center title label in `HudTheme.Gold`, gold `HSeparator`, right ornament label. This replaces the plain `CreateSectionHeader` pattern throughout the codebase.

---

## Phase 3: Inventory Tab

9. **Create** `Combat/UI/Screens/InventoryTab.cs` — dedicated bag inventory view (the right side of BG3 when viewing "Showing All"):
   - **Filter bar**: `HBoxContainer` with filter icon button + "Showing All" label / `OptionButton` dropdown for category filter (All, Weapons, Armor, Potions, Magic, Misc) + search `LineEdit` with magnifying glass icon + info `(i)` icon button on right
   - **Item grid**: `ScrollContainer` → `GridContainer` with dynamically computed column count. Each slot is `ActivatableContainerControl` at 54×54 with rarity background, stack count overlay (top-left corner, small bold white text with shadow), item icon
   - **Stack count rendering**: In `ActivatableContainerControl`, the `HotkeyText` field already renders top-left; ensure it uses a bold shadow font for item stacks (white text, 1px dark shadow offset). Currently wired — verify it matches BG3's top-left stack badges.
   - **Sort controls**: row of small icon buttons at bottom for sort mode (by type, weight, value, rarity)
   - All drag/drop and tooltip logic reused from current `InventoryPanel`

---

## Phase 4: Character Tab

10. **Create** `Combat/UI/Screens/CharacterTab.cs` — full character sheet (expands existing `CharacterSheetModal` content):
    - **Header**: Name, Race, Class/Level, XP bar
    - **Ability Scores**: Same 6-box horizontal layout as Equipment tab
    - **Combat Stats**: AC, Initiative, Speed, Proficiency Bonus in styled boxes
    - **Saving Throws**: Full 6-entry list with proficiency icons (filled diamond = proficient, empty = not)
    - **Skills**: Full 18-skill list with proficiency indicators + modifiers. Layout: 2-column grid, each row = proficiency icon + skill name + modifier value
    - **Resistances/Immunities/Vulnerabilities**: Section with damage type icons sourced from `Combatant` boosts
    - **Features & Traits**: List with icons, sourced from `ResolvedCharacter.GrantedFeatures`
    - **Resources**: Spell slots, class resources, action economy display

---

## Phase 5: Floating Tooltip System

11. **Create** `Combat/UI/Controls/FloatingTooltip.cs` — global floating tooltip that follows the mouse:
    - A `PanelContainer` parented to the screen's `CanvasLayer` top level
    - Styled: dark background with subtle gold border, 8px content margin, slight transparency
    - **Item tooltip layout**: Item name (rarity-colored), item type line, stat block (damage/AC/weight/value), description/flavor text, "Requires: X" line if proficiency needed
    - **Equipment comparison**: When hovering a bag item that could go in an equipment slot, show currently equipped item's stats alongside with green/red delta arrows (▲/▼)
    - Positioned at mouse + offset (12px right, −8px up), clamped to viewport bounds
    - Replaces the fixed bottom tooltip in current `InventoryPanel`

12. **Update** `Combat/UI/Controls/ActivatableContainerControl.cs` — emit `TooltipRequested(ActivatableContainerData, Vector2)` signal on hover (with global mouse position) so the floating tooltip can respond. Add rarity-colored border glow on hover (brighten `StyleBoxFlat.BorderColor` to rarity tint).

---

## Phase 6: Visual Polish & Theme Extensions

13. **Extend** `Combat/UI/Base/HudTheme.cs` with:
    - **Rarity colors** (centralized, moved from `InventoryPanel.GetRarityColor`):
      - Common: `#888888` (gray)
      - Uncommon: `#3f8f3f` (green)
      - Rare: `#4a7aba` (blue)
      - Very Rare: `#8a3aba` (purple)
      - Legendary: `#c8a84e` (gold — same as `HudTheme.Gold`)
    - **New style factories**: `CreateAbilityScoreBox()`, `CreateAcBadge()`, `CreateFullScreenOverlay()`, `CreateTabButtonStyle(bool active)`, `CreateTabButtonActiveStyle()`
    - **Screen background**: `CreateFullScreenBg()` — dark semi-transparent base panel with gold ornate border, to serve as the outer frame of the unified screen
    - Expose `GetRarityColor(ItemRarity)` and `GetCategoryBackground(ItemCategory, ItemRarity)` as static methods (remove duplication from `InventoryPanel`)

14. **Create** `Combat/UI/Base/HudIcons.cs` — centralized icon path resolver for UI elements:
    - Static `GetSlotPlaceholder(EquipSlot)` — returns path to a silhouette icon for empty equipment slots (helmet outline, ring outline, boot outline, etc.)
    - Static `GetTabIcon(int tabIndex)` — sword+shield / grid / person icons for tab buttons
    - Static `GetProficiencyIcon(bool proficient)` — filled vs. empty diamond icon for saving throw / skill lists
    - Static `GetDamageTypeIcon(DamageType)` — small icons for resistance display
    - Falls back to a small colored `ImageTexture` rectangle when asset not found (prevents null crashes)

---

## Phase 7: Data Layer Additions

15. **Extend** `CharacterSheetData` DTO in `Combat/UI/Overlays/CharacterSheetModal.cs` (or extract to new `Combat/UI/Screens/CharacterDisplayData.cs`):
    - Add `List<DamageType> Resistances`, `Immunities`, `Vulnerabilities`
    - Add `List<(string Name, string IconPath, string Description)> NotableFeatures`
    - Add `int MeleeAttackBonus`, `string MeleeDamageRange`, `string MeleeWeaponIconPath`
    - Add `int RangedAttackBonus`, `string RangedDamageRange`, `string RangedWeaponIconPath`
    - Add `int WeightCurrent`, `int WeightMax` (max = `Combatant.Stats.Strength × 15` per D&D rules)
    - Add `int Experience`, `int ExperienceToNextLevel`

16. **Update** `Combat/UI/HudController.cs` → `BuildCharacterSheetData()`:
    - Compute `MeleeAttackBonus` = prof bonus + STR or DEX mod (finesse) + enchantment bonus
    - Compute `MeleeDamageRange` = weapon dice min–max + ability mod (e.g., `"0~9"`)
    - Compute `RangedAttackBonus` / `RangedDamageRange` from `RangedMainHand` slot
    - Compute `WeightMax` = `Combatant.Stats.Strength × 15`
    - Populate `NotableFeatures` from `ResolvedCharacter.GrantedFeatures` + `Combatant` passive display names
    - Populate `Resistances` / `Immunities` from `Combatant` boost / race data

---

## Phase 8: Integration & Cleanup

17. **Update** `Combat/UI/HudController.cs`:
    - Remove `_characterSheet` field and all references
    - Remove `_inventoryPanel` field and all references
    - Add `private CharacterInventoryScreen _characterInventoryScreen`
    - In `InitializeOverlays()`: instantiate `CharacterInventoryScreen`, add to window manager
    - Update `ToggleInventory()` to call `_characterInventoryScreen.Toggle(combatant, invService)`
    - Update auto-show character sheet logic on turn start to open Equipment tab of unified screen
    - Update `HudLayoutService` persistence keys from `"character_sheet"` and `"inventory_panel"` to `"character_inventory_screen"`

18. **Deprecate** (do NOT delete yet):
    - `Combat/UI/Overlays/CharacterSheetModal.cs` — keep for reference until new screen verified
    - `Combat/UI/Panels/InventoryPanel.cs` — keep for reference until new screen verified
    - Delete both in follow-up PR once smoke tests pass

19. **Move** rarity color and category background methods from `InventoryPanel` to `HudTheme` — update all call sites.

---

## Phase 9: Keyboard & UX

20. **Keyboard shortcuts**:
    - `I` — toggle the unified screen open/closed
    - `Tab` / `Shift+Tab` — cycle between the 3 tabs (while screen is open)
    - `1` / `2` / `3` — jump to Equipment / Inventory / Character tab (while screen is open)
    - `Ctrl+F` — focus the search bar (while Inventory tab is active)
    - `ESC` — close the screen
    - **Right-click** equipped item → unequip to bag
    - **Double-click** bag item → auto-equip to appropriate slot (existing behavior, verified working)

21. **Screen transition**:
    - Opening: Tween `CanvasItem.Modulate.A` from 0 → 1 over 150ms
    - Closing: Tween 1 → 0 over 100ms, then `Visible = false`
    - Tab switches: instant (swap `Visible` on tab content nodes — no animation needed)

---

## Verification Checklist

- [ ] `scripts/ci-build.sh` — compiles cleanly with no errors or warnings
- [ ] `scripts/ci-godot-log-check.sh` — no `ERROR:`, `SCRIPT ERROR:`, or `Unhandled Exception:` on startup
- [ ] Press "I" in-game → unified screen opens, Equipment tab visible by default
- [ ] Click each of the 3 tabs → content switches correctly, no layout breakage
- [ ] Drag item from bag grid (Inventory tab) to equipment slot (Equipment tab or back in Inventory tab equip section) → equips, stats update
- [ ] Hover any item → floating tooltip appears at mouse position with correct name, stats, description
- [ ] Hover a bag item that matches an equipped slot → tooltip shows comparison with delta arrows
- [ ] Ability scores in Equipment tab match `Combatant.Stats` values exactly
- [ ] AC badge matches computed AC (base + armor + DEX mod clamped by armor type)
- [ ] Melee/Ranged attack bonuses and damage ranges are computed correctly
- [ ] Weight bar shows `WeightCurrent / WeightMax` and updates when equipping/unequipping
- [ ] ESC closes the screen; "I" reopens it to the same tab
- [ ] `dotnet test` — existing `InventoryServiceTests` and `EquipmentWeaponActionTests` pass (data layer untouched)

---

## File Summary

### New Files (~10)

| File | Purpose | Est. Lines |
|---|---|---|
| `Combat/UI/Screens/CharacterInventoryScreen.cs` | Shell + tabs + top/bottom bars | ~350 |
| `Combat/UI/Screens/EquipmentTab.cs` | Main equipment view (3 columns) | ~600 |
| `Combat/UI/Screens/InventoryTab.cs` | Bag grid + filters | ~400 |
| `Combat/UI/Screens/CharacterTab.cs` | Full character sheet | ~350 |
| `Combat/UI/Screens/ScreenTabButton.cs` | Circular tab button control | ~80 |
| `Combat/UI/Controls/AbilityScoreBox.cs` | Ability score display box | ~70 |
| `Combat/UI/Controls/WeaponStatDisplay.cs` | Melee/Ranged weapon stats | ~120 |
| `Combat/UI/Controls/AcBadge.cs` | Shield AC display | ~60 |
| `Combat/UI/Controls/SectionDivider.cs` | Ornamental section divider | ~50 |
| `Combat/UI/Controls/FloatingTooltip.cs` | Global mouse-following tooltip | ~250 |
| `Combat/UI/Base/HudIcons.cs` | Centralized icon path resolver | ~100 |

### Modified Files (~5)

| File | Changes |
|---|---|
| `Combat/UI/HudController.cs` | Replace dual-panel with unified screen, rewrite `BuildCharacterSheetData()` |
| `Combat/UI/Base/HudTheme.cs` | New style factories, rarity colors, screen background style |
| `Combat/UI/Controls/ActivatableContainerControl.cs` | `TooltipRequested` signal, rarity border glow on hover |
| `Combat/UI/Overlays/CharacterSheetModal.cs` | Extend `CharacterSheetData` DTO with new fields (or extract to new DTO) |
| `Combat/UI/Panels/InventoryPanel.cs` | Move rarity/category methods to `HudTheme`; deprecate panel itself |

---

## Key Constraints & Decisions

- **Unified screen over separate panels** — matches BG3 UX, reduces window management complexity, eliminates z-order conflicts between the two existing panels
- **3 tabs** (Equipment + Inventory + Character) — spellbook/prepared spells deferred to future iteration
- **Weight + Encumbrance** display, no gold currency tracking — currency system out of scope
- **Melee/Ranged weapon stat display** with computed attack bonus and damage ranges — functional, not decorative
- **Code-built UI** continues (no `.tscn` files) — consistent with existing project convention; all other HUD panels follow this pattern
- **InventoryService data layer untouched** — `Inventory`, `InventoryItem`, `EquipSlot`, all move/equip logic unchanged; only presentation layer rebuilt
- **Floating tooltip** replaces per-panel fixed-bottom tooltip — enables item comparison and follows BG3 UX exactly
- **No mass reformatting** of unrelated files — minimal diffs, only touch what is necessary

## Reference: Current State

- `InventoryPanel.cs` (1089 lines): paper-doll + bag grid as a resizable modal, fixed bottom tooltip, working drag/drop
- `CharacterSheetModal.cs` (337 lines): scrollable read-only stat list, no visuals, no ability scores displayed as boxes
- `HudController.cs`: manages both as separate modals opened independently; "I" key opens only inventory, character sheet shown automatically on turn start
- **The gap**: no unified experience; character info and inventory are separate concerns with no visual cohesion; character sheet has no icons, no visual hierarchy, no ability score boxes, and no weapon stats
