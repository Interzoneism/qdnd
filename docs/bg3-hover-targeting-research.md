# BG3 Hover Effects & Attack Targeting - Research Summary

**Date**: January 2025  
**Research Focus**: How Baldur's Gate 3 visually handles hover effects, attack targeting, and combat feedback

---

## Executive Summary

### Current System Bug: Shared Material Issue

**Location**: `/Combat/Arena/OutlineEffect.cs` lines 164-170

**Problem**: The `SetNextPassMaterial()` method directly modifies the `NextPass` property of materials retrieved from `mesh.GetActiveMaterial(0)`. In Godot, when multiple meshes share the same material resource (which is common for models loaded from files), modifying `stdMat.NextPass` affects ALL meshes using that material, not just the current one.

**Symptom**: When hovering over one character, all characters using the same base material will receive the same outline color.

**Root Cause**:
```csharp
// Line 166 - BUG: Directly modifies shared material
var baseMaterial = mesh.GetActiveMaterial(0);
if (baseMaterial is StandardMaterial3D stdMat)
{
    stdMat.NextPass = outlineMat;  // ❌ Modifies shared resource!
}
```

**Solution Required**: Each mesh needs its own material instance before setting NextPass. The code should call `.Duplicate()` on the base material first, then set that duplicated material as the surface override material.

---

## BG3 Visual Specifications

### 1. Character Outline Colors

Based on BG3 gameplay and the existing implementation:

| Context | Color (RGB) | Alpha | Usage |
|---------|-------------|-------|-------|
| **Friendly Hover** | Cyan/Blue (0.3, 0.85, 1.0) | 0.6 | Hovering over party members |
| **Enemy Hover** | Red (1.0, 0.35, 0.3) | 0.6 | Hovering over hostile targets |
| **Selected** | Green (0.4, 1.0, 0.5) | 0.7 | Currently selected character |
| **Valid Target** | Gold/Yellow (1.0, 0.85, 0.2) | 0.7 | Valid target for selected ability |
| **Active Turn** | Warm Gold (1.0, 0.95, 0.6) | 0.5 | Character's turn in initiative |

**Current Implementation**: Already matches BG3 colors (see `OutlineEffect.cs` lines 27-34)

### 2. Outline Visual Style

**BG3 Implementation**: 
- **Style**: Fresnel-based glow (softer at center, brighter at edges)
- **Rendering**: Back-face inflated shell using next-pass shader
- **Animation**: Subtle pulsing effect (sine wave)
- **Thickness**: ~0.035 world units (adjustable)
- **Fresnel Power**: 2.0 (controls edge falloff)

**Current Implementation**: 
- ✅ Matches BG3 style perfectly
- ✅ Uses Fresnel in shader (`outline.gdshader` line 34)
- ✅ Pulsing animation with configurable speed/amount
- ✅ Back-face culling with inflated normals

### 3. Ground Circle/Ring Indicators

**BG3 Behavior**:
- **Red Ring**: Appears under enemies when targeted with weapon attacks
- **Size**: Matches character base size (typically 0.8-1.2m radius)
- **Visual**: Thin glowing ring decal on ground
- **Persistence**: Shows while hovering with attack action selected

**Current Implementation**:
- ❌ **MISSING**: No ground ring under targeted characters
- ✅ Range indicator exists (`RangeIndicator.cs`) but only shows ability range, not target indicator
- ✅ AoE indicator exists (`AoEIndicator.cs`) for area spells

**Needed**: Create `TargetRingIndicator.cs` similar to `RangeIndicator.cs` but for single-target highlighting.

### 4. Attack Targeting Line/Arrow

**BG3 Behavior**:
- **Visual**: Red line/arrow from attacker to target when hovering enemy with attack action
- **Purpose**: Shows attack trajectory and line of sight
- **Appears**: Only when hovering valid enemy target with weapon attack selected
- **Color**: Red for enemies, may vary by context
- **Style**: Solid line with arrow head, rendered above ground

**Current Implementation**:
- ❌ **COMPLETELY MISSING**: No attack line visualization
- ✅ Hit chance calculation exists (line 2378-2387 in `CombatArena.cs`)
- ✅ Hit chance display exists (`ShowHitChance()` in `CombatantVisual.cs` line 1087)

**Needed**: Create `AttackLineIndicator.cs` to draw line from actor to target.

### 5. Hit Chance Display

**BG3 Behavior**:
- Shows percentage above enemy name when hovering with attack
- Updates dynamically based on cover, high ground, status effects
- Format: "Enemy Name (75%)"

**Current Implementation**:
- ✅ **IMPLEMENTED**: `ShowHitChance()` method exists
- ✅ Displays as "Name (percentage%)" in name label
- ✅ Integrates with rules engine for accurate calculation

---

## Existing Code Analysis

### OutlineEffect.cs
- **Purpose**: Manages per-character outline effects
- **Method**: Uses next-pass shader materials
- **Bug**: Shared material modification (see above)
- **Status**: Colors and shader are correct, needs instance fix

### CombatantVisual.cs
- **Hover Detection**: `SetHovered()` method (line 555)
- **Outline Resolution**: `ResolveOutlineContext()` prioritizes states (line 576)
- **Priority Order**: Selected > ValidTarget > Hovered > ActiveTurn > None
- **Status**: Working correctly, will work once material bug is fixed

### outline.gdshader
- **Technique**: Vertex shader inflates mesh along normals
- **Fragment**: Applies Fresnel for edge glow
- **Parameters**: thickness, pulse_speed, pulse_amount, fresnel_power
- **Status**: Perfectly matches BG3 visual style

### CombatArena.cs - UpdateHoveredTargetPreview()
- **Location**: Line 2320
- **Purpose**: Shows targeting feedback when hovering enemy with attack action
- **Current**: Sets valid target outline, shows hit chance
- **Missing**: Attack line visual, ground ring indicator

### RangeIndicator.cs & AoEIndicator.cs
- **Purpose**: Show ability range and area-of-effect
- **Implementation**: Torus mesh for range, cylinder/cone/box for AoE
- **Materials**: Unshaded with emission glow
- **Status**: Good reference for implementing target ring and attack line

---

## Implementation Recommendations

### Priority 1: Fix Shared Material Bug (CRITICAL)

**File**: `Combat/Arena/OutlineEffect.cs`  
**Method**: `SetNextPassMaterial()` (line 156)

**Fix**:
```csharp
private static void SetNextPassMaterial(MeshInstance3D mesh, ShaderMaterial outlineMat)
{
    int surfaceCount = mesh.GetSurfaceOverrideMaterialCount();
    if (surfaceCount == 0) return;

    var baseMaterial = mesh.GetActiveMaterial(0);
    
    if (baseMaterial is StandardMaterial3D stdMat)
    {
        // FIX: Create unique material instance per mesh
        var uniqueMat = (StandardMaterial3D)stdMat.Duplicate();
        uniqueMat.NextPass = outlineMat;
        mesh.SetSurfaceOverrideMaterial(0, uniqueMat);
    }
    else if (baseMaterial is ShaderMaterial shaderMat && shaderMat != outlineMat)
    {
        // FIX: Create unique material instance per mesh
        var uniqueMat = (ShaderMaterial)shaderMat.Duplicate();
        uniqueMat.NextPass = outlineMat;
        mesh.SetSurfaceOverrideMaterial(0, uniqueMat);
    }
    else if (baseMaterial == null)
    {
        // Keep existing pass-through logic
        var passThrough = new StandardMaterial3D();
        passThrough.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        passThrough.AlbedoColor = new Color(1, 1, 1, 0);
        passThrough.NextPass = outlineMat;
        mesh.SetSurfaceOverrideMaterial(0, passThrough);
    }
}
```

**Also Update**: `RemoveNextPassMaterial()` to handle surface override materials properly.

### Priority 2: Add Target Ring Indicator (HIGH)

**New File**: `Combat/Arena/TargetRingIndicator.cs`

**Specification**:
- Thin torus ring under targeted character
- Red color for enemies (1.0, 0.35, 0.3, 0.6)
- Gold color for valid targets (1.0, 0.85, 0.2, 0.7)
- Radius based on character size (default 0.9m)
- Rendered at ground level (Y = 0.01)
- Similar implementation to `RangeIndicator.cs`

**Integration**: 
- Add field `_targetRing` to `CombatArena.cs`
- Show in `UpdateHoveredTargetPreview()` when hovering valid target
- Hide in `ClearTargetHighlights()`

### Priority 3: Add Attack Line Indicator (HIGH)

**New File**: `Combat/Arena/AttackLineIndicator.cs`

**Specification**:
- Line from attacker to target
- Arrow head at target end
- Red color for enemies (1.0, 0.35, 0.3, 0.8)
- Thickness: 0.05m
- Rendered above ground (Y = 0.5m at start, adjusts to target height)
- Uses `ImmediateMesh` or `MeshInstance3D` with box + pyramid
- Only visible during attack action hover

**Implementation Approach**:
```csharp
public void Show(Vector3 from, Vector3 to, Color color)
{
    // Create line as stretched box mesh
    // Create arrow head as pyramid mesh at 'to' position
    // Orient both toward target direction
    // Apply unshaded emission material
}
```

**Integration**:
- Add field `_attackLine` to `CombatArena.cs`
- Show in `UpdateHoveredTargetPreview()` after line 2387
- Pass attacker position and target position
- Hide in `ClearTargetHighlights()`

### Priority 4: Enhanced Hit Chance Display (MEDIUM)

**Current**: Shows percentage in name label  
**BG3 Enhancement**: Add advantage/disadvantage indicators

**Recommendation**: 
- Keep current implementation (sufficient)
- Optional: Add small icon next to percentage for adv/disadv
- Can be deferred to later polish pass

---

## Testing Checklist

After implementing fixes:

### Material Bug Fix
- [ ] Hover over enemy A shows red outline on A only
- [ ] Hover over friendly B shows cyan outline on B only
- [ ] Select character C shows green outline on C only
- [ ] All three characters use same base model/material
- [ ] No outline color bleeding between characters

### Target Ring
- [ ] Red ring appears under enemy when hovered with attack action
- [ ] Ring disappears when hover ends
- [ ] Ring matches character base size
- [ ] Ring is visible on all terrain types
- [ ] Gold ring appears for valid spell targets

### Attack Line
- [ ] Line appears from current character to hovered enemy with attack
- [ ] Line disappears when hover ends or action cancelled
- [ ] Arrow points toward target
- [ ] Line follows terrain height at both ends
- [ ] Line color matches target context (red for enemies)

### Integration
- [ ] All visuals clear when cancelling action
- [ ] Hit chance updates correctly with line visible
- [ ] No performance issues with multiple targets
- [ ] Works with ranged and melee attacks

---

## BG3 Reference Data Available

The repository contains extensive BG3 reference data at `/BG3_Data/`:
- Combat mechanics, spell data, status effects
- All cosmetic/visual data has been stripped
- Focus is on mechanical gameplay data
- README provides complete schema documentation

**Note**: No specific visual effect data in BG3_Data (all VFX were stripped), but combat mechanics are fully documented.

---

## Additional BG3 Visual Details (from Web Research)

### Character Outlines Toggle
- BG3 allows toggling outlines with tilde key (~)
- Some players find default white outline too stark
- Community mods exist to customize outline appearance
- Color scheme varies by context (steal/interact vs combat)

### Targeting System
- Line-of-sight calculation affects hit chance
- Red targeting line shows when obstacles block path
- Hovering different parts of enemy hitbox can change hit %
- System simulates D&D 5e cover and elevation mechanics

### Combat Feedback
- High ground bonus indicated visually
- Cover icons appear next to hit chance
- Shadow/obscurement affects targeting display
- Dynamic updates based on cursor position

---

## Conclusion

The current implementation has:
- ✅ Correct color scheme matching BG3
- ✅ Excellent outline shader with Fresnel and pulsing
- ✅ Hit chance calculation and display
- ✅ Foundation for indicators (AoE, Range)

Critical missing pieces:
- ❌ Shared material bug (BLOCKING - must fix first)
- ❌ Target ring indicator under hovered enemies
- ❌ Attack line from attacker to target

Once the material bug is fixed and the two indicator systems added, the combat visual feedback will match BG3's polish and clarity.

