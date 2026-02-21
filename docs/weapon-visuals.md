# Weapon Visual System

## Overview

Equipped weapons are displayed as 3D mesh instances parented to the character's right-hand skeleton bone (prefers `hand_r`, with fallback aliases/heuristics), so they follow all character animations automatically.

The system lives in two files:

| File | Role |
|---|---|
| [Combat/Animation/WeaponVisualAttachment.cs](../Combat/Animation/WeaponVisualAttachment.cs) | Static helper — maps `WeaponType` → FBX path, finds skeleton, creates `BoneAttachment3D` |
| [Combat/Arena/CombatantVisual.cs](../Combat/Arena/CombatantVisual.cs) | Calls `AttachMainHandWeapon()` from `Initialize()` and `SetupWeaponVisual()` |

## How it works

1. `CombatantVisual.Initialize()` resolves the `Combatant.MainHandWeapon` definition.
2. `SetupWeaponVisual()` compares the weapon against `_attachedWeapon` and skips the rebuild when nothing has changed.
3. `WeaponVisualAttachment.AttachMainHandWeapon()`:
   - Searches the `ModelRoot` subtree for a `Skeleton3D`.
   - Resolves a right-hand bone (exact aliases first, then name heuristics for custom rigs).
   - Creates a `BoneAttachment3D` named `WeaponAttachment_MainHand` on the skeleton.
   - Loads the FBX `PackedScene`, instantiates it, applies rotation/offset/scale, and adds it under the attachment.

## Weapon model authoring convention

All FBX weapon models must be:
- **Vertical** — the weapon stands upright, tip pointing toward +Y.
- **Pivot at handle bottom** — the origin is at the base of the grip.

The system rotates them into the grip pose via `WeaponRotationDegrees` (default `X=-90°`).

## Configuring the grip pose

The `CombatantVisual` node exposes four `[Export]` properties under the **Weapon Attachment** group that can be tweaked per-scene in the Godot editor:

| Property | Default | Description |
|---|---|---|
| `WeaponRotationDegrees` | `(-90, 0, 0)` | Euler rotation applied to the weapon mesh in hand-local space. |
| `WeaponPositionOffset` | `(0, 0.06, 0)` | Shifts the handle into the palm centre. Positive Y → toward tip. |
| `WeaponOneHandedScale` | `1.0` | Uniform scale for one-handed weapons. |
| `WeaponTwoHandedScale` | `1.0` | Uniform scale for two-handed weapons. |

## WeaponType → FBX mapping

One representative mesh is chosen per weapon category. All meshes live under:

```
res://assets/3d models/Equipment/Low Poly Medieval Weapons/
```

| WeaponType | FBX |
|---|---|
| Dagger | Swords/Dagger.fbx |
| Shortsword | Swords/Sword.fbx |
| Scimitar | Swords/Falchion Cleaver.fbx |
| Rapier | Swords/Messer.fbx |
| Longsword | Swords/Falchion.fbx |
| Greatsword | Swords/Greatsword.fbx |
| Handaxe | Axes/Axe.fbx |
| Battleaxe | Axes/Battle Axe.fbx |
| Greataxe | Axes/Two handed Axe.fbx |
| Halberd / Glaive / Pike | Axes/Halberd.fbx |
| Spear / Trident | Spears/Spear.fbx |
| Javelin / Dart | Spears/Javelin.fbx |
| Lance | Spears/Lance.fbx |
| Club / Greatclub / Quarterstaff | Spears/Polearm.fbx |
| Mace | Maces/Mace.fbx |
| Morningstar | Maces/Spiked Mace.fbx |
| Flail | Maces/Flail.fbx |
| WarPick | Maces/Spiked Club.fbx |
| LightHammer | Hammers/Hammer.fbx |
| Warhammer | Hammers/Warhammer.fbx |
| Maul | Hammers/Two-Handed Hammer.fbx |
| Shortbow | Bows/Recurve Bow.fbx |
| Longbow | Bows/Long Bow.fbx |
| Light / Heavy / Hand Crossbow | Bows/Crossbow.fbx |
| Sickle | Farming/Sickle.fbx |

To add a new mapping, edit the `WeaponFbxPaths` dictionary in `WeaponVisualAttachment.cs`.

## FBX import (first-time setup)

The weapon FBX files are auto-detected and imported by the Godot editor on first open.  
Until they are imported, `WeaponVisualAttachment` logs a warning and leaves the character unarmed — no crash occurs.

**To import all weapons:** Open the Godot editor and wait for the importer to finish (progress bar in the bottom-right).

## Unarmed characters

If `Combatant.MainHandWeapon` is `null` (unarmed strike), the system removes any existing weapon attachment and the character renders with no mesh in hand.
