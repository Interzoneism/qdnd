using System;
using System.Collections.Generic;
using System.Text;
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
        private const string FallbackPassiveFeatureIcon = "res://assets/Images/Icons General/Generic_Feature_Unfaded_Icon.png";
        private const string PassiveFeatureFolder = "res://assets/Images/Icons Passive Features";

        private static readonly string[] IconSearchFolders = new[]
        {
            "res://assets/Images/Icons Spells/",
            "res://assets/Images/Icons Actions/",
            "res://assets/Images/Icons Weapon Actions/",
            "res://assets/Images/Icons Passive Features/",
            "res://assets/Images/Icons Conditions/",
            "res://assets/Images/Icons General/",
            "res://assets/Images/Icons Weapons and Other/",
            "res://assets/Images/Icons Armour/",
        };

        private static readonly string[] PassiveFeatureSuffixes = new[]
        {
            "_unfaded_icon",
            "_passive_feature_unfaded_icon",
            "_bar_icon",
            "_condition_icon",
        };

        private static readonly string[] FeatureIdSuffixes = new[]
        {
            "feature",
            "passive",
            "ability",
            "resource",
            "toggle",
            "bonuses",
            "bonusattack",
            "bonusdamage",
        };

        private static readonly Dictionary<string, string> FeatureKeywordIcons = new(StringComparer.OrdinalIgnoreCase)
        {
            ["darkvision"] = "res://assets/Images/Icons General/Generic_Darkness_Unfaded_Icon.png",
            ["superior darkvision"] = "res://assets/Images/Icons General/Generic_Darkness_Unfaded_Icon.png",
            ["rage"] = "res://assets/Images/Icons General/Generic_Damage_Unfaded_Icon.png",
            ["sneak attack"] = "res://assets/Images/Icons General/Generic_Tactical_Unfaded_Icon.png",
            ["fey ancestry"] = "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
            ["brave"] = "res://assets/Images/Icons General/Generic_Buff_Unfaded_Icon.png",
            ["lucky"] = "res://assets/Images/Icons General/Generic_Buff_Unfaded_Icon.png",
            ["halfling nimbleness"] = "res://assets/Images/Icons General/Generic_Utility_Unfaded_Icon.png",
            ["stonecunning"] = "res://assets/Images/Icons General/Generic_Nature_Unfaded_Icon.png",
            ["dwarven resilience"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["trance"] = "res://assets/Images/Icons General/Generic_Ethereal_Unfaded_Icon.png",
            ["mask of the wild"] = "res://assets/Images/Icons General/Generic_Nature_Unfaded_Icon.png",
            ["natural explorer"] = "res://assets/Images/Icons General/Generic_Nature_Unfaded_Icon.png",
            ["favored enemy"] = "res://assets/Images/Icons General/Generic_Threat_Unfaded_Icon.png",
            ["spellcasting"] = "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
            ["unarmored defense"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["unarmoured defense"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["unarmored defence"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["unarmoured defence"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["fighting style"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["martial arts"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["ki"] = "res://assets/Images/Icons General/Generic_Radiant_Unfaded_Icon.png",
            ["divine sense"] = "res://assets/Images/Icons General/Generic_Radiant_Unfaded_Icon.png",
            ["lay on hands"] = "res://assets/Images/Icons General/Generic_Healing_Unfaded_Icon.png",
            ["divine smite"] = "res://assets/Images/Icons General/Generic_Radiant_Unfaded_Icon.png",
            ["channel divinity"] = "res://assets/Images/Icons General/Generic_Radiant_Unfaded_Icon.png",
            ["wild shape"] = "res://assets/Images/Icons General/Generic_Wild_Animal_Unfaded_Icon.png",
            ["metamagic"] = "res://assets/Images/Icons General/Generic_Magical_Unfaded_Icon.png",
            ["eldritch invocation"] = "res://assets/Images/Icons General/Generic_Darkness_Unfaded_Icon.png",
            ["bardic inspiration"] = "res://assets/Images/Icons General/Generic_Buff_Unfaded_Icon.png",
            ["cunning action"] = "res://assets/Images/Icons General/Generic_Tactical_Unfaded_Icon.png",
            ["uncanny dodge"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["evasion"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["action surge"] = "res://assets/Images/Icons General/Generic_Multiattack_Unfaded_Icon.png",
            ["second wind"] = "res://assets/Images/Icons General/Generic_Healing_Unfaded_Icon.png",
            ["extra attack"] = "res://assets/Images/Icons General/Generic_Multiattack_Unfaded_Icon.png",
            ["hellish rebuke"] = "res://assets/Images/Icons General/Generic_Fire_Unfaded_Icon.png",
            ["infernal legacy"] = "res://assets/Images/Icons General/Generic_Fire_Unfaded_Icon.png",
            ["breath weapon"] = "res://assets/Images/Icons General/Generic_Damage_Unfaded_Icon.png",
            ["draconic ancestry"] = "res://assets/Images/Icons General/Generic_Damage_Unfaded_Icon.png",
            ["relentless endurance"] = "res://assets/Images/Icons General/Generic_Death_Unfaded_Icon.png",
            ["savage attacks"] = "res://assets/Images/Icons General/Generic_Damage_Unfaded_Icon.png",
            ["healing"] = "res://assets/Images/Icons General/Generic_Healing_Unfaded_Icon.png",
            ["resistance"] = "res://assets/Images/Icons General/Generic_Physical_Unfaded_Icon.png",
            ["proficiency"] = "res://assets/Images/Icons General/Generic_Buff_Unfaded_Icon.png",
        };

        private static Dictionary<string, string> _passiveIconIndex;
        private static bool _passiveIconIndexBuilt;

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
                EquipSlot.Helmet => "res://assets/Images/Icons Helmets/Circlet_Unfaded_Icon.png",
                EquipSlot.Gloves => "res://assets/Images/Icons Gloves/Gloves_Leather_1_Unfaded_Icon.png",
                EquipSlot.Boots => "res://assets/Images/Icons Boots/Boots_Leather_Unfaded_Icon.png",
                EquipSlot.Cloak => "res://assets/Images/Icons Cloaks/Cloak_Unfaded_Icon.png",
                EquipSlot.Amulet => "res://assets/Images/Icons Amulets/Amulet_Necklace_A_Gold_A_Unfaded_Icon.png",
                EquipSlot.Ring1 or EquipSlot.Ring2 => "res://assets/Images/Icons Rings/Ring_A_Simple_Gold_Unfaded_Icon.png",
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
                ItemCategory.Shield => "res://assets/Images/Icons Weapons and Other/Wooden_Shield_Unfaded_Icon.png",
                ItemCategory.Headwear => "res://assets/Images/Icons Helmets/Circlet_Unfaded_Icon.png",
                ItemCategory.Handwear => "res://assets/Images/Icons Gloves/Gloves_Leather_1_Unfaded_Icon.png",
                ItemCategory.Footwear => "res://assets/Images/Icons Boots/Boots_Leather_Unfaded_Icon.png",
                ItemCategory.Cloak => "res://assets/Images/Icons Cloaks/Cloak_Unfaded_Icon.png",
                ItemCategory.Amulet => "res://assets/Images/Icons Amulets/Amulet_Necklace_A_Gold_A_Unfaded_Icon.png",
                ItemCategory.Ring => "res://assets/Images/Icons Rings/Ring_A_Simple_Gold_Unfaded_Icon.png",
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

        // ── Passive Feature Icon Resolution ───────────────────────

        /// <summary>
        /// Resolve passive feature icon from an explicit icon hint, feature ID, and/or feature name.
        /// Uses a resilient matcher so IDs like "fey_ancestry" can find "Fey_Ancestry_Unfaded_Icon.png".
        /// </summary>
        public static string ResolvePassiveFeatureIcon(string featureId, string featureName, string iconHint = null)
        {
            if (TryResolveIconPath(iconHint, out var resolved))
                return resolved;

            foreach (var token in BuildFeatureTokens(featureId, featureName))
            {
                if (TryResolveIconPath(token, out resolved))
                    return resolved;
            }

            foreach (var token in BuildFeatureTokens(featureId, featureName))
            {
                if (TryResolvePassiveIconFromIndex(token, out resolved))
                    return resolved;
            }

            // Try keyword-based mapping for common features
            string lowerName = featureName?.Trim().ToLowerInvariant();
            if (lowerName != null && FeatureKeywordIcons.TryGetValue(lowerName, out var keywordIcon))
                return keywordIcon;

            // Also try partial keyword matching (e.g., "Superior Darkvision" contains "darkvision")
            if (lowerName != null)
            {
                foreach (var kvp in FeatureKeywordIcons)
                {
                    if (kvp.Key.Length < 4) continue;
                    if (lowerName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                        return kvp.Value;
                }
            }

            return ResourceLoader.Exists(FallbackPassiveFeatureIcon)
                ? FallbackPassiveFeatureIcon
                : string.Empty;
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

        private static IEnumerable<string> BuildFeatureTokens(string featureId, string featureName)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    unique.Add(value.Trim());
            }

            Add(featureId);
            Add(featureName);

            if (!string.IsNullOrWhiteSpace(featureId))
            {
                Add(featureId.Replace('_', ' '));
                Add(SplitCamelCase(featureId));
            }

            if (!string.IsNullOrWhiteSpace(featureName))
            {
                Add(featureName.Replace('-', ' '));
            }

            return unique;
        }

        private static bool TryResolveIconPath(string iconName, out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(iconName))
                return false;

            iconName = iconName.Trim();
            if (iconName.StartsWith("res://", StringComparison.Ordinal))
            {
                if (ResourceLoader.Exists(iconName))
                {
                    resolvedPath = iconName;
                    return true;
                }

                if (iconName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    string pngPath = iconName[..^".webp".Length] + ".png";
                    if (ResourceLoader.Exists(pngPath))
                    {
                        resolvedPath = pngPath;
                        return true;
                    }
                }

                return false;
            }

            foreach (var variant in ExpandIconNameVariants(iconName))
            {
                foreach (var candidate in BuildIconFilenameCandidates(variant))
                {
                    foreach (var folder in IconSearchFolders)
                    {
                        string fullPath = folder + candidate;
                        if (ResourceLoader.Exists(fullPath))
                        {
                            resolvedPath = fullPath;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static IEnumerable<string> ExpandIconNameVariants(string iconName)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void Add(string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    unique.Add(value.Trim());
            }

            Add(iconName);
            Add(iconName.Replace(' ', '_'));
            Add(iconName.Replace(' ', '_').Replace('\'', '-').Replace(":", string.Empty));
            Add(SplitCamelCase(iconName).Replace(' ', '_'));
            Add(SplitCamelCase(iconName).Replace(' ', '_').Replace('\'', '-').Replace(":", string.Empty));
            return unique;
        }

        private static IEnumerable<string> BuildIconFilenameCandidates(string iconToken)
        {
            if (string.IsNullOrWhiteSpace(iconToken))
                yield break;

            if (iconToken.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                yield return iconToken;
                yield break;
            }

            yield return iconToken + "_Unfaded_Icon.png";
            yield return iconToken + "_passive_feature_Unfaded_Icon.png";
            yield return iconToken + ".png";
        }

        private static bool TryResolvePassiveIconFromIndex(string token, out string path)
        {
            path = null;
            if (string.IsNullOrWhiteSpace(token))
                return false;

            EnsurePassiveIconIndex();
            if (_passiveIconIndex == null || _passiveIconIndex.Count == 0)
                return false;

            foreach (var key in ExpandPassiveLookupKeys(token))
            {
                if (_passiveIconIndex.TryGetValue(key, out path))
                    return true;
            }

            return false;
        }

        private static void EnsurePassiveIconIndex()
        {
            if (_passiveIconIndexBuilt)
                return;

            _passiveIconIndexBuilt = true;
            _passiveIconIndex = new Dictionary<string, string>(StringComparer.Ordinal);

            var dir = DirAccess.Open(PassiveFeatureFolder);
            if (dir == null)
                return;

            foreach (var file in dir.GetFiles())
            {
                if (!file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                string key = NormalizePassiveLookupKey(file);
                if (string.IsNullOrEmpty(key) || _passiveIconIndex.ContainsKey(key))
                    continue;

                _passiveIconIndex[key] = $"{PassiveFeatureFolder}/{file}";
            }
        }

        private static IEnumerable<string> ExpandPassiveLookupKeys(string token)
        {
            string normalized = NormalizePassiveLookupKey(token);
            if (string.IsNullOrWhiteSpace(normalized))
                yield break;

            yield return normalized;

            foreach (var suffix in FeatureIdSuffixes)
            {
                if (normalized.EndsWith(suffix, StringComparison.Ordinal) && normalized.Length > suffix.Length + 2)
                    yield return normalized[..^suffix.Length];
            }
        }

        private static string NormalizePassiveLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string key = value.Trim();
            if (key.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                key = key[..^4];

            string lower = key.ToLowerInvariant();
            foreach (var suffix in PassiveFeatureSuffixes)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal))
                {
                    key = key[..^suffix.Length];
                    break;
                }
            }

            var sb = new StringBuilder(key.Length);
            foreach (char c in key)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        private static string SplitCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1])))
                    sb.Append(' ');
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
