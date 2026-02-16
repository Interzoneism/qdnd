# Hotbar Activatable Container

## Overview

`ActivatableContainerControl` is the reusable UI container for anything the player can activate from square slots (currently actions, later items).

- File: `Combat/UI/Controls/ActivatableContainerControl.cs`
- Data model: `Combat/UI/Controls/ActivatableContainerData.cs`

## Features

- Square icon container with hover overlay.
- Selection square outline.
- Spinning square outline animation for active toggles.
- Greyed-out visual for unavailable entries.
- Hold + drag start and slot drop support.
- Activation event that is blocked when unavailable.

## Current Usage

`ActionBarPanel` now renders each hotbar slot using `ActivatableContainerControl` and maps `ActionBarEntry` into `ActivatableContainerData`.

- Reorder remains enabled only on the `All` category tab.
- Tooltip behavior remains centralized in `HudController`.
- Toggle passives use `IsSpinning` (`entry.IsToggle && entry.IsToggledOn`) so active toggle state is visible.

## Future Item Integration

Use the same control for inventory/spellbook item-like activatables by setting:

- `Kind = ActivatableContentKind.Item`
- item-specific `ContentId`, icon, availability, and labels.

Action-specific execution remains outside the container (owner panel/controller decides what activation does).
