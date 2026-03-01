# Baldur's Gate 3 — Surface System Deep Dive

> **Goal:** Understand BG3's surface architecture well enough to replicate it in Godot C#.

---

## 1. Core Architecture: Cell-Based Grid (Not Free-Form)

BG3 uses **a 2D cell/tile grid** for all surface logic. This is inherited from Divinity: Original Sin 2 and the Divinity Engine 4.0.

### The AI Grid
- The world is divided into a **uniform 2D grid of 0.5 m × 0.5 m cells** (patches).
- This same grid is used for **AI navigation/pathfinding**, surface tracking, and line-of-sight.
- Green cells = walkable, Red cells = blocked. Surfaces modify cell properties (e.g., difficult terrain halves movement speed).
- The grid must be regenerated after terrain edits.

### Surface Mask Map
- Surfaces are stored as a **large 2D texture** (the "Surface Mask Map") projected onto the world.
- **Each pixel in the mask = one 0.5 m cell** in the world (XZ plane).
- Different **color channels** encode different surface type layers, allowing multiple surface types to be tracked simultaneously.
- The mask encodes **opacity/presence** per surface type per cell — not a single enum per cell.

### Volumetric Surfaces (Clouds / Smoke)
- Fog, smoke, poison clouds, and darkness clouds use a **3D voxel grid** rather than the 2D surface mask.
- These are tracked separately and occupy vertical space (blocking line-of-sight, etc.).
- In the data files, cloud surfaces are distinguished by names like `FogCloud`, `StinkingCloud`, `DarknessCloud`.

---

## 2. How Surfaces Are Rendered

### Ground Surfaces (2D)
- The Surface Mask Map is **projected (decal-style)** onto the terrain from above.
- Each surface type is rendered as a **separate pass** during the gbuffer stage (deferred rendering).
- **Procedural edge textures** break up the hard pixel boundaries — giving organic, natural-looking edges.
- **Bicubic interpolation** is applied inside the Surface Mask node to smooth values and adjust opacity, preventing hard square edges on the 0.5 m cells.
- This is described as an "old-school forward decal" technique — affected geometry is re-sent to the GPU for an extra rendering pass per surface type.

### Each Surface Has a Root Template
Surface visuals are defined as `type="surface"` nodes in `RootTemplates/_merged.lsf.lsx` with:
- **InstanceVisual / FX** — the particle/shader effect displayed.
- **SurfaceCategory** (int) — engine-level category mapping.
- **FadeOutSpeed** / **DefaultLifeTime** — decay timing.

---

## 3. How Surfaces Are Spawned

### A. Via Spells — `CreateSurface(Radius, Duration, Type)`
The primary functor in stat files. Found in `Spell_Projectile.txt`, `Spell_Target.txt`, `Spell_Shout.txt`, `Spell_Zone.txt`.

```
GROUND:CreateSurface(Radius, Duration, SurfaceType)
```

| Parameter | Meaning |
|-----------|---------|
| **Radius** | Radius in meters (e.g. `2`, `4.5`, `6`, `9`). Fills all cells whose center falls within this circle. |
| **Duration** | Lifetime in turns. `0` = instant, `-1` = permanent, `10` = 10 rounds. |
| **SurfaceType** | Short name like `Fire`, `Water`, `WaterFrozen`, `Acid`, `Poison`, `Grease`, `Oil`, `Blood`, `Lava`, `Web`, `Mud`, etc. |

**Key insight:** The radius is continuous (even supports `4.5`), but it's resolved against the discrete 0.5 m grid. All cells within the radius are set.

A complementary function `CreatePuddle` exists for **irregular, non-symmetrical** surface shapes (e.g., spilled liquids).

### B. Via Item Destruction — `OnDestroyActions`
Items (bottles, barrels, bowls) spawn surfaces when destroyed:

```xml
<node id="Action">
    <attribute id="ActionType" type="int32" value="10" />
    <children>
        <node id="Attributes">
            <attribute id="SurfaceType" type="FixedString" value="SurfaceFire" />
            <attribute id="LifeTime" type="float" value="12" />
            <attribute id="TotalCells" type="int32" value="100" />
        </node>
    </children>
</node>
```

Here `TotalCells` explicitly sets how many grid cells the surface fills from the origin. This directly confirms the cell-based model.

### C. Permanent/Pre-placed Surfaces
Water bodies, lava pools, and deep water are pre-placed surfaces baked into the level. Characters have a `WadableSurfaceType` (usually `SurfaceDeepWater`) for wading behavior.

---

## 4. Surface Interactions (SurfaceChange)

The engine defines a set of **transformation rules** that change one surface type into another:

| SurfaceChange Action | Effect |
|---------------------|--------|
| **Ignite** | Water → Steam Cloud, Oil → Fire, Grease → Fire, Poison → Fire, Blood → Fire, Alcohol → Fire |
| **Douse** | Fire → Smoke Cloud, removes burning |
| **Freeze** | Water → Ice, Blood → Frozen Blood, Poison → Frozen Poison |
| **Melt** | Ice → Water, Frozen Blood → Blood |
| **Electrify** | Water → Electrified Water, Blood → Electrified Blood |
| **Vaporize** | Water → Steam Cloud |
| **Bless** / **Curse** | DOS2-era modifiers (less used in BG3) |

These are invoked via `GROUND:SurfaceChange(Action)` in spell data. Multiple changes can chain:
```
GROUND:SurfaceChange(Ignite);GROUND:SurfaceChange(Melt);GROUND:CreateSurface(2,2,Fire)
```

### Surface-to-Surface Overlap Rules
- When two surfaces overlap, the engine resolves which one wins or transforms.
- The Surface Mask Map naturally handles this by encoding each surface type in separate channels — one cell can have partial overlap resolved through opacity blending.

---

## 5. Surface Effects on Entities

Surfaces apply **statuses** to creatures standing on or entering them:

| Surface | Status Applied | Notes |
|---------|---------------|-------|
| Fire | `BURNING` | Damage per turn |
| Water | `WET` | Vulnerability to Lightning/Cold |
| Ice / Frozen | `YOURWETDREAM` / Prone check | Difficult terrain + slip |
| Poison | `POISONED` | Damage per turn |
| Acid | `YOURWETDREAM` + Acid dmg | Armor penalty |
| Grease | Difficult terrain | Prone on fail |
| Web | `ENTANGLED` | Movement restricted |
| Electrified Water | `YOURWETDREAM` + Lightning dmg | Stun check |
| Oil | Difficult terrain | Flammable |
| Lava | Heavy fire damage | |

### Dipping System
Weapon dipping (coating a weapon in a surface) is controlled via `Spell_Target.txt`:
```
IF(Surface('SurfaceFire')):ApplyEquipmentStatus(SELF,MainHand,DIPPED_FIRE_SWITCH,100,0)
```
This checks what surface type is under the character and applies an equipment status accordingly.

---

## 6. All Known Surface Types (from BG3 data)

### Ground Surfaces
`Fire`, `Water`, `WaterFrozen`, `WaterElectrified`, `Blood`, `BloodFrozen`, `BloodElectrified`, `Poison`, `PoisonFrozen`, `Oil`, `Grease`, `Acid`, `Lava`, `Alcohol`, `Mud`, `Web`, `Vines`, `Overgrowth`, `SpikeGrowth`, `BlackPowder`, `CausticBrine`, `AlienOil`, `BloodSilver`, `DeepWater`, `None`

### Cloud / Volumetric Surfaces
`FogCloud`, `StinkingCloud`, `DarknessCloud`, `PotionHealingCloud`, `PotionHealingGreaterCloud`

---

## 7. Practical Guide: Implementing in Godot C#

### Recommended Architecture

```
┌──────────────────────────────────────────────┐
│              SurfaceManager                  │
│  - Dictionary<Vector2I, SurfaceCell> grid    │
│  - float cellSize = 0.5f                     │
│  - Manages lifetime, interactions            │
├──────────────────────────────────────────────┤
│              SurfaceCell                     │
│  - SurfaceType type                          │
│  - float lifetime (turns remaining)          │
│  - float opacity (for rendering blend)       │
├──────────────────────────────────────────────┤
│          SurfaceRenderer (visual)            │
│  - Texture2D surfaceMaskMap                  │
│  - One material/shader per surface type      │
│  - Projected decal or MeshInstance3D          │
├──────────────────────────────────────────────┤
│       SurfaceInteractionTable                │
│  - Dict<(SurfaceType, ChangeAction),         │
│         SurfaceType> transformRules          │
└──────────────────────────────────────────────┘
```

### Step-by-Step

#### 1. Grid Setup
```csharp
// SurfaceCell.cs
public class SurfaceCell {
    public SurfaceType Type;
    public float LifetimeRemaining; // in turns, -1 = permanent
    public float Opacity;           // 0..1 for blend
}

// SurfaceManager.cs
public partial class SurfaceManager : Node3D {
    private const float CellSize = 0.5f;
    private Dictionary<Vector2I, SurfaceCell> _grid = new();

    public Vector2I WorldToCell(Vector3 worldPos) {
        return new Vector2I(
            Mathf.FloorToInt(worldPos.X / CellSize),
            Mathf.FloorToInt(worldPos.Z / CellSize)
        );
    }

    public void CreateSurface(Vector3 origin, float radius,
                               float duration, SurfaceType type) {
        int cellRadius = Mathf.CeilToInt(radius / CellSize);
        Vector2I center = WorldToCell(origin);

        for (int dx = -cellRadius; dx <= cellRadius; dx++) {
            for (int dz = -cellRadius; dz <= cellRadius; dz++) {
                Vector2I cell = center + new Vector2I(dx, dz);
                Vector3 cellWorld = CellToWorld(cell);
                if (origin.DistanceTo(cellWorld) <= radius) {
                    SetCell(cell, type, duration);
                }
            }
        }
        UpdateMaskTexture();
    }
}
```

#### 2. Rendering — Surface Mask Texture
```csharp
// Approach: Use an Image as your surface mask map
// Each pixel = one cell. Use R, G, B, A channels for different types.
// Project this texture onto terrain via a Decal node or a shader.

private Image _maskImage;
private ImageTexture _maskTexture;

private void UpdateMaskTexture() {
    foreach (var (cell, data) in _grid) {
        int px = cell.X - _gridOrigin.X;
        int py = cell.Y - _gridOrigin.Y;
        Color color = SurfaceTypeToMaskColor(data.Type, data.Opacity);
        _maskImage.SetPixel(px, py, color);
    }
    _maskTexture.Update(_maskImage);
}
```

In your terrain shader, sample this mask texture and blend the surface material:
```glsl
// terrain_surface.gdshader
uniform sampler2D surface_mask;
uniform sampler2D fire_texture;
uniform sampler2D water_texture;

void fragment() {
    vec2 uv = world_to_mask_uv(VERTEX);
    vec4 mask = texture(surface_mask, uv);

    // R = fire, G = water, B = poison, A = oil (example mapping)
    vec3 fire  = texture(fire_texture, UV).rgb * mask.r;
    vec3 water = texture(water_texture, UV).rgb * mask.g;

    ALBEDO = mix(ALBEDO, fire, mask.r);
    ALBEDO = mix(ALBEDO, water, mask.g);
}
```

#### 3. Interaction Table
```csharp
public static class SurfaceInteractions {
    private static Dictionary<(SurfaceType, ChangeAction), SurfaceType> _rules = new() {
        { (SurfaceType.Water, ChangeAction.Freeze),    SurfaceType.Ice },
        { (SurfaceType.Water, ChangeAction.Electrify), SurfaceType.WaterElectrified },
        { (SurfaceType.Water, ChangeAction.Ignite),    SurfaceType.SteamCloud },
        { (SurfaceType.Oil,   ChangeAction.Ignite),    SurfaceType.Fire },
        { (SurfaceType.Ice,   ChangeAction.Melt),      SurfaceType.Water },
        { (SurfaceType.Fire,  ChangeAction.Douse),     SurfaceType.SmokeCloud },
        // ... etc
    };

    public static SurfaceType? Transform(SurfaceType current, ChangeAction action) {
        return _rules.TryGetValue((current, action), out var result) ? result : null;
    }
}
```

#### 4. Entity Interaction (Turn-Based)
```csharp
// On turn start or when entity enters a cell:
public void OnEntityEnterCell(Entity entity, Vector2I cell) {
    if (_grid.TryGetValue(cell, out var surface)) {cgc mcp setup
        var status = SurfaceEffects.GetStatus(surface.Type);
        if (status != null) {
            entity.ApplyStatus(status);
        }
    }
}
```

### Key Design Decisions for Godot

| Decision | BG3 Approach | Godot Recommendation |
|----------|-------------|---------------------|
| Grid resolution | 0.5 m cells | 0.5 m cells (matches D&D 5ft = ~1.5m, so 3 cells per square) |
| Storage | Surface Mask Map (texture) | `Image` + `ImageTexture` updated per frame |
| Rendering | Projected decals per type | Terrain shader sampling mask, OR use Godot `Decal` nodes |
| Clouds | 3D voxel grid | `GpuParticles3D` + `Area3D` for detection |
| Interactions | Hardcoded transform table | Dictionary lookup as shown above |
| Lifetime | Turn-based countdown | Decrement on turn end, remove at 0 |

---

## 8. Summary

> **BG3's surface system is fundamentally a 2D tile/cell grid (0.5 m resolution) stored as an image mask, with volumetric clouds as a separate 3D system.** It is NOT a continuous fluid simulation — it's discrete cells with transformation rules and projected texture rendering.

Key takeaways for your implementation:
1. **Use a 2D grid** — a `Dictionary<Vector2I, SurfaceCell>` is the simplest approach.
2. **Render via a mask texture** projected onto terrain (shader or Decal).
3. **Surface interactions are a lookup table**, not physics — just `(Type, Action) → NewType`.
4. **Lifetime is turn-based** — decrement per turn, remove at zero.
5. **Clouds are separate** — use particles + Area3D, not the ground grid.