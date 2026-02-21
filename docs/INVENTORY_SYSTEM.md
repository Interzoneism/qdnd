# Inventory System (BG3-Style)

## Overview
The inventory implementation now uses a slot-based drag/drop flow with two connected layers:

- `Combat/Services/InventoryService.cs`: state + validation + move rules.
- `Combat/UI/Panels/InventoryPanel.cs`: modal UI with paper-doll equipment and bag grid.

## Key Features
- Drag/drop between:
  - bag -> bag (reorder)
  - bag -> equipment slot
  - equipment slot -> bag
  - equipment slot -> equipment slot (swap when valid)
- Tooltip system for item details and empty-slot guidance.
- BG3-style equipment arrangement around a center model frame.
- Scrollable grid bag with fixed slot capacity.

## BG3 Data Source
`InventoryService` now accepts `StatsRegistry` and seeds starter items from `BG3_Data/Stats`:

- Weapons from `Weapon.txt`
- Armor/accessories from `Armor.txt`
- Slot mapping uses BG3 `Slot` fields (e.g., `Breast`, `Helmet`, `Ring`, `Amulet`, `Cloak`, `Boots`, `Gloves`, `Melee Offhand Weapon`).

Notes:
- Items are normalized into internal `InventoryItem` instances.
- Weapon/armor entries are mapped to existing combat definitions when possible.
- Consumables still use existing combat action links (`UseActionId`) for reliable in-combat usage.

## Item Taxonomy (BG3-aligned)
The inventory taxonomy now distinguishes BG3 equipment families directly:

- `Weapon`
- `Clothing` (body slot items without armor proficiency; includes robes/camp body variants)
- `Armor` (body slot items with armor proficiency requirements)
- `Shield`
- `Headwear`
- `Handwear`
- `Footwear`
- `Cloak`
- `Amulet`
- `Ring`
- `Potion`, `Scroll`, `Throwable`, `Consumable`, `Misc` (non-equipment)

### Slot mapping rules
- `Breast` maps to `Clothing` when no proficiency is required, otherwise `Armor`.
- `Helmet`, `Gloves`, `Boots`, `Cloak`, `Amulet`, `Ring` map to their specific category and paper-doll slot.
- `Melee Offhand Weapon`/`Offhand` from armor data maps to `Shield`.
- Cosmetic slots from BG3 armor data (`VanityBody`, `Underwear`, `VanityBoots`) are normalized into `Clothing`/`Footwear` for current slot model compatibility.
- Rings always expose both `Ring1` and `Ring2` as allowed equip slots.

### Icon resolution
- Weapon and armor items now attempt to resolve item-specific icons from:
  - `assets/Images/Icons Weapons and Other`
  - `assets/Images/Icons Armour`
- Resolution uses normalized item IDs/names plus weapon/armor type aliases.
- If no specific icon is found, the system falls back to existing generic category icons.

## Reliability Rules
- Slot compatibility is validated server-side in `InventoryService`.
- Main-hand two-handed weapons block off-hand equips.
- Ranged main-hand two-handed weapons block ranged off-hand equips.
- Equipping a two-handed weapon into `MainHand` or `RangedMainHand` auto-unequips the linked off-hand slot when moving from bag.
- Equipment changes always trigger stat/application refresh through `ApplyEquipment`.
- Inventory/equipment changes emit events used by UI refresh.

## Integration
`CombatArena` now constructs inventory service with stats support:

- `new InventoryService(charRegistry, _statsRegistry)`

This ensures BG3 stats data is available when combatant inventories are initialized.
