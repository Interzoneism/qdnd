# BG3 Icon System — How Icons Are Stored, Named, and Connected to Game Data

## Summary

BG3 uses **three parallel icon storage systems**, all of which already use meaningful filenames that directly map to items, spells, actions, and conditions. You do **not** need complex parsing — the names are already the connection keys.

---

## The Three Icon Sources

### 1. Atlas Textures (64×64 hotbar icons)
- **DDS Files**: [Icons/Public/Shared/Assets/Textures/Icons/Icons_Items.dds](file:///c:/Users/Martin/Downloads/bg3-modders-multitool/UnpackedMods/Icons/Public/Shared/Assets/Textures/Icons/Icons_Items.dds) (through `_6`), [Icons_Skills.dds](file:///c:/Users/Martin/Downloads/bg3-modders-multitool/UnpackedMods/Icons/Public/Shared/Assets/Textures/Icons/Icons_Skills.dds)
- **LSX Mapping Files**: [Shared/Public/Shared/GUI/Icons_Items.lsx](file:///c:/Users/Martin/Downloads/bg3-modders-multitool/UnpackedMods/Shared/Public/Shared/GUI/Icons_Items.lsx) (through `_6`), [Icons_Skills.lsx](file:///c:/Users/Martin/Downloads/bg3-modders-multitool/UnpackedMods/Shared/Public/Shared/GUI/Icons_Skills.lsx)
- Each LSX entry maps a **named icon** → UV coordinates in the atlas:

```xml
<node id="IconUV">
    <attribute id="MapKey" type="FixedString" value="Item_ARM_Boots_Leather"/>
    <attribute id="U1" type="float" value="0.53149414"/>
    <attribute id="U2" type="float" value="0.56225586"/>
    <attribute id="V1" type="float" value="0.65649414"/>
    <attribute id="V2" type="float" value="0.68725586"/>
</node>
```

- The `MapKey` **IS** the icon name used everywhere else in the game data
- ~7000 item icons across 6 atlas files, ~7000 skill/action icons in the skills atlas

### 2. Tooltip Icons (380×380, individual DDS files)

Already exported as **individual named files**:

| Path | Count | Examples |
|------|-------|---------|
| `Game/Public/Game/GUI/Assets/Tooltips/Icons/` | ~1,521 | `Action_Barbarian_Rage.DDS`, `Action_Dash.DDS` |
| `Game/Public/Game/GUI/Assets/Tooltips/ItemIcons/` | ~3,010 | Individual item tooltip icons |

### 3. Controller UI Icons (144×144, individual PNG files)

Already exported as **individual named files**:

| Path | Count | Examples |
|------|-------|---------|
| `Game/Public/Game/GUI/Assets/ControllerUIIcons/skills_png/` | ~1,522 | Individual skill/action PNGs |
| `Game/Public/Game/GUI/Assets/ControllerUIIcons/items_png/` | ~3,045 | Individual item PNGs |

---

## How Icons Connect to Items/Spells/Actions

The connection is via the **icon name string**, which appears in multiple places:

| Data Type | Where the icon is referenced | Field name |
|-----------|------------------------------|------------|
| Items | Root Templates (`.lsx` files) | `Icon` attribute |
| Spells | Spell stats (`.txt` files in `Stats/Generated/Data/`) | `Icon` field |
| Statuses | Status data files | `Icon` field |
| Passives | Passive data files | `Icon` field |

For example, an item with `Icon = "Item_ARM_Boots_Leather"` would look up:
- The 64×64 atlas via the LSX `MapKey`
- The 380×380 tooltip via `Tooltips/ItemIcons/Item_ARM_Boots_Leather.DDS`
- The 144×144 controller icon via `ControllerUIIcons/items_png/Item_ARM_Boots_Leather.DDS`

---

## Recommended Approaches for Your Godot C# Clone

### Option A: Use the Pre-Extracted Individual Files (Easiest)

The **Controller UI icons** (`ControllerUIIcons/items_png/` and `skills_png/`) are already:
- Individual files ✅
- Named with the exact icon key ✅  
- 144×144 PNG — good resolution for most game UI ✅
- ~4,500+ icons total ✅

**Steps:**
1. Copy `ControllerUIIcons/items_png/` and `skills_png/` into your Godot project
2. Convert DDS → PNG if needed (many are already PNG despite the folder name)
3. In your data loader, when you read an item's `Icon` field, just load `res://icons/items/{iconName}.png`

### Option B: Parse the Atlas + Extract (More Control)

Write a Python script to:
1. Parse each `Icons_Items*.lsx` / `Icons_Skills.lsx` to extract `MapKey` + UV coords
2. Load the corresponding DDS atlas texture
3. Crop each icon using UV coordinates → save as `{MapKey}.png`

```python
# Pseudocode for atlas extraction
import xml.etree.ElementTree as ET
from PIL import Image

tree = ET.parse("Icons_Items.lsx")
atlas = Image.open("Icons_Items.dds")  # needs DDS plugin
width, height = atlas.size

for node in tree.findall(".//node[@id='IconUV']"):
    name = node.find("attribute[@id='MapKey']").get("value")
    u1 = float(node.find("attribute[@id='U1']").get("value"))
    u2 = float(node.find("attribute[@id='U2']").get("value"))
    v1 = float(node.find("attribute[@id='V1']").get("value"))
    v2 = float(node.find("attribute[@id='V2']").get("value"))
    
    x1, y1 = int(u1 * width), int(v1 * height)
    x2, y2 = int(u2 * width), int(v2 * height)
    
    icon = atlas.crop((x1, y1, x2, y2))
    icon.save(f"output/{name}.png")
```

### Option C: Runtime Atlas Parsing in Godot (Most Flexible)

Load the LSX as an XML resource at runtime, create an `AtlasTexture` for each icon:

```csharp
// In Godot C#
var doc = new XmlDocument();
doc.Load("res://data/Icons_Items.lsx");
var atlas = GD.Load<Texture2D>("res://textures/Icons_Items.dds");

foreach (XmlNode node in doc.SelectNodes("//node[@id='IconUV']"))
{
    string name = node.SelectSingleNode("attribute[@id='MapKey']")
                      .Attributes["value"].Value;
    float u1 = float.Parse(node.SelectSingleNode("attribute[@id='U1']")
                               .Attributes["value"].Value);
    // ... parse u2, v1, v2
    
    var atlasTexture = new AtlasTexture();
    atlasTexture.Atlas = atlas;
    atlasTexture.Region = new Rect2(
        u1 * atlas.GetWidth(), v1 * atlas.GetHeight(),
        (u2 - u1) * atlas.GetWidth(), (v2 - v1) * atlas.GetHeight()
    );
    IconCache[name] = atlasTexture;
}
```

> [!TIP]
> **Option A is the fastest path** — the files already exist with correct names. Option C is best for production since atlas textures are more GPU-efficient than thousands of individual textures.

---

## Icon Name Conventions

| Prefix | Type | Examples |
|--------|------|---------|
| `Item_ARM_` | Armor/clothing | `Item_ARM_Boots_Leather`, `Item_ARM_ChainMail` |
| `Item_ARR_` | Arrows/ammunition | `Item_ARR_Arrow_Of_Fire` |
| `Item_WPN_` | Weapons | `Item_WPN_Longsword` |
| `Item_CONS_` | Consumables | `Item_CONS_Potion_Healing` |
| `Item_ALCH_` | Alchemy ingredients | `Item_AlchemyIngredient_Mushroom_Barrelstalk` |
| `Action_` | Actions/abilities | `Action_Dash`, `Action_Barbarian_Rage` |
| `Spell_` | Spells | `Spell_Conjuration_MageArmor` |
| `Passive_` | Passive features | `Passive_DarkVision` |
| `Status_` | Status effects | `Status_Burning` |
| `GEN_` | Generic fallback icons | `GEN_Armor`, `GEN_Weapon`, `GEN_Loot` |

---

## File Inventory

| Source | Path | Format | Size | Count |
|--------|------|--------|------|-------|
| Item Atlases | `Icons/Public/Shared/Assets/Textures/Icons/Icons_Items*.dds` | DDS | 64×64 per icon | 6 files |
| Skill Atlas | `Icons/Public/Shared/Assets/Textures/Icons/Icons_Skills.dds` | DDS | 64×64 per icon | 1 file |
| Item Atlas Maps | `Shared/Public/Shared/GUI/Icons_Items*.lsx` | XML | — | 6 files |
| Skill Atlas Map | `Shared/Public/Shared/GUI/Icons_Skills.lsx` | XML | — | 1 file |
| Tooltip Icons | `Game/.../Tooltips/Icons/` | DDS | 380×380 | ~1,521 |
| Tooltip Item Icons | `Game/.../Tooltips/ItemIcons/` | DDS | 380×380 | ~3,010 |
| Controller Skills | `Game/.../ControllerUIIcons/skills_png/` | DDS/PNG | 144×144 | ~1,522 |
| Controller Items | `Game/.../ControllerUIIcons/items_png/` | DDS/PNG | 144×144 | ~3,045 |
