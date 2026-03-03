# BG3 Icon System — How Icons Are Stored, Named, and Connected to Game Data

## The hotbar/small icon source

### 1. Atlas Textures (64×64 hotbar icons)
- **DDS Files**: [Icons/Public/Shared/Assets/Textures/Icons/Icons_Items.dds](BG3_Data/Icons/Public/Shared/Assets/Textures/Icons/Icons_Items.dds) (through `_6`), [Icons_Skills.dds](BG3_Data/Icons/Public/Shared/Assets/Textures/Icons/Icons_Skills.dds)
- **LSX Mapping Files**: [Shared/Public/Shared/GUI/Icons_Items.lsx](BG3_Data/Shared/Public/Shared/GUI/Icons_Items.lsx) (through `_6`), [Icons_Skills.lsx](BG3_Data/Shared/Public/Shared/GUI/Icons_Skills.lsx)
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

---

## Recommended Approach for Your Godot C# Clone

### Runtime Atlas Parsing in Godot

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