using Godot;
using QDND.Combat.Services;

namespace QDND.Combat.UI.Base
{
    /// <summary>
    /// Centralized icon path resolver for UI elements.
    /// Provides fallback textures when assets are missing.
    /// </summary>
    public static class HudIcons
    {
        // ── Equipment Slot Placeholders ────────────────────────────

        public static string GetSlotPlaceholder(EquipSlot slot)
        {
            return slot switch
            {
                EquipSlot.MainHand => "res://assets/Images/Icons Weapon Actions/Main_Hand_Attack_Unfaded_Icon.png",
                EquipSlot.OffHand => "res://assets/Images/Icons Actions/Shield_Bash_Unfaded_Icon.png",
                EquipSlot.RangedMainHand => "res://assets/Images/Icons Weapon Actions/Ranged_Attack_Unfaded_Icon.png",
                EquipSlot.RangedOffHand => "res://assets/Images/Icons Weapon Actions/Ranged_Attack_Unfaded_Icon.png",
                EquipSlot.Armor => "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
                EquipSlot.Helmet => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                EquipSlot.Gloves => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                EquipSlot.Boots => "res://assets/Images/Icons Actions/Boot_of_the_Giants_Unfaded_Icon.png",
                EquipSlot.Cloak => "res://assets/Images/Icons Actions/Cloak_of_Shadows_Unfaded_Icon.png",
                EquipSlot.Amulet => "res://assets/Images/Icons Actions/Talk_to_the_Sentient_Amulet_Unfaded_Icon.png",
                EquipSlot.Ring1 or EquipSlot.Ring2 => "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
                _ => "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            };
        }

        // ── Item Category Icons ────────────────────────────────────

        public static string GetItemCategoryIcon(ItemCategory category)
        {
            return category switch
            {
                ItemCategory.Weapon => "res://assets/Images/Icons Weapon Actions/Main_Hand_Attack_Unfaded_Icon.png",
                ItemCategory.Armor => "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
                ItemCategory.Clothing => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                ItemCategory.Shield => "res://assets/Images/Icons Actions/Shield_Bash_Unfaded_Icon.png",
                ItemCategory.Headwear => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                ItemCategory.Handwear => "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
                ItemCategory.Footwear => "res://assets/Images/Icons Actions/Boot_of_the_Giants_Unfaded_Icon.png",
                ItemCategory.Cloak => "res://assets/Images/Icons Actions/Cloak_of_Shadows_Unfaded_Icon.png",
                ItemCategory.Amulet => "res://assets/Images/Icons Actions/Talk_to_the_Sentient_Amulet_Unfaded_Icon.png",
                ItemCategory.Ring => "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
                ItemCategory.Potion => "res://assets/Images/Icons General/Generic_Healing_Unfaded_Icon.png",
                ItemCategory.Scroll => "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
                ItemCategory.Throwable => "res://assets/Images/Icons Actions/Throw_Weapon_Unfaded_Icon.png",
                _ => "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            };
        }

        // ── Item Icon Resolution ───────────────────────────────────

        /// <summary>
        /// Resolve the best icon path for an inventory item.
        /// Checks item-specific path first, then category fallback.
        /// </summary>
        public static string ResolveItemIcon(InventoryItem item)
        {
            if (!string.IsNullOrWhiteSpace(item?.IconPath) &&
                item.IconPath.StartsWith("res://", System.StringComparison.Ordinal))
                return item.IconPath;

            if (item == null)
                return string.Empty;

            return GetItemCategoryIcon(item.Category);
        }

        // ── Safe Texture Loading ───────────────────────────────────

        /// <summary>
        /// Load a texture, returning null if path is invalid or resource missing.
        /// </summary>
        public static Texture2D LoadTextureSafe(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith("res://", System.StringComparison.Ordinal))
                return null;

            if (!ResourceLoader.Exists(path))
                return null;

            return ResourceLoader.Load<Texture2D>(path);
        }

        /// <summary>
        /// Create a small solid-color placeholder texture.
        /// Used as fallback when icon assets don't exist.
        /// </summary>
        public static ImageTexture CreatePlaceholderTexture(Color color, int size = 32)
        {
            var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
            image.Fill(color);
            return ImageTexture.CreateFromImage(image);
        }
    }
}
