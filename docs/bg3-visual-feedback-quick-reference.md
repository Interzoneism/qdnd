# BG3 Visual Feedback - Quick Reference

## 🎨 Outline Colors (Already Implemented ✅)

```
Friendly Hover:  Cyan    (0.3, 0.85, 1.0) α=0.6  💙
Enemy Hover:     Red     (1.0, 0.35, 0.3) α=0.6  ❤️
Selected:        Green   (0.4, 1.0, 0.5)  α=0.7  💚
Valid Target:    Gold    (1.0, 0.85, 0.2) α=0.7  💛
Active Turn:     Gold    (1.0, 0.95, 0.6) α=0.5  ⭐
```

## 🐛 Critical Bug: Shared Material

**File:** `OutlineEffect.cs:166`  
**Issue:** Direct modification of shared material's `NextPass` property  
**Impact:** All characters with same base material get same outline  
**Fix:** Duplicate material before setting NextPass

```csharp
// ❌ WRONG (current)
stdMat.NextPass = outlineMat;

// ✅ CORRECT (needed)
var uniqueMat = (StandardMaterial3D)stdMat.Duplicate();
uniqueMat.NextPass = outlineMat;
mesh.SetSurfaceOverrideMaterial(0, uniqueMat);
```

## 📍 Missing Features

### 1. Target Ring Indicator ❌
- **What:** Red/gold ring on ground under hovered target
- **When:** During attack action hover
- **Color:** Red for enemies, gold for valid targets
- **Size:** 0.8-1.2m radius (character-dependent)
- **Reference:** See `RangeIndicator.cs` for implementation pattern

### 2. Attack Line Indicator ❌
- **What:** Line/arrow from attacker to target
- **When:** Hovering enemy with attack action selected
- **Color:** Red (1.0, 0.35, 0.3, 0.8)
- **Style:** Box mesh + pyramid arrow head
- **Height:** Elevated above ground (~0.5m)

### 3. Hit Chance Display ✅ (Already Working)
- Shows "Name (75%)" format
- Updates dynamically
- Integrated with rules engine

## 🎯 Implementation Priority

1. **FIX MATERIAL BUG** ← Do this first! 🔥
2. Add Target Ring Indicator
3. Add Attack Line Indicator
4. Polish & testing

## 🔍 Files to Modify

```
Combat/Arena/OutlineEffect.cs          ← Fix material bug (line 156-181)
Combat/Arena/TargetRingIndicator.cs    ← CREATE NEW (like RangeIndicator)
Combat/Arena/AttackLineIndicator.cs    ← CREATE NEW
Combat/Arena/CombatArena.cs            ← Integrate indicators (line 2320+)
```

## ✨ Shader Details (Already Perfect!)

**File:** `assets/shaders/outline.gdshader`

- Fresnel-based edge glow ✅
- Pulsing animation (sine wave) ✅
- Back-face culling with normal inflation ✅
- Thickness: 0.035, Fresnel power: 2.0 ✅

No changes needed to shader!

