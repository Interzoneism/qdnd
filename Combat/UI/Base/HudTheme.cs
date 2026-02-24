using Godot;
using QDND.Combat.Services;

namespace QDND.Combat.UI.Base
{
    /// <summary>
    /// Centralized HUD theme constants and style factories.
    /// BG3-inspired dark fantasy palette.
    /// </summary>
    public static class HudTheme
    {
        // ── Color Palette ──────────────────────────────────────────
        public static readonly Color PrimaryDark      = new(12f / 255f, 10f / 255f, 18f / 255f, 0.94f);
        public static readonly Color SecondaryDark     = new(18f / 255f, 15f / 255f, 25f / 255f, 0.95f);
        public static readonly Color TertiaryDark      = new(8f / 255f, 6f / 255f, 14f / 255f, 0.88f);
        public static readonly Color PanelBorder       = new(200f / 255f, 168f / 255f, 78f / 255f, 0.3f);
        public static readonly Color PanelBorderBright = new(200f / 255f, 168f / 255f, 78f / 255f, 0.6f);
        public static readonly Color PanelBorderSubtle = new(200f / 255f, 168f / 255f, 78f / 255f, 0.15f);
        public static readonly Color Gold              = new(200f / 255f, 168f / 255f, 78f / 255f);
        public static readonly Color GoldMuted         = new(160f / 255f, 136f / 255f, 72f / 255f);
        public static readonly Color WarmWhite         = new(232f / 255f, 224f / 255f, 208f / 255f);
        public static readonly Color MutedBeige        = new(170f / 255f, 160f / 255f, 140f / 255f);
        public static readonly Color TextDim           = new(112f / 255f, 104f / 255f, 88f / 255f);

        // Faction colors
        public static readonly Color PlayerBlue  = new(0.4f, 0.68f, 0.97f);
        public static readonly Color EnemyRed    = new(0.91f, 0.42f, 0.42f);

        // Resource colors
        public static readonly Color ActionGreen   = new(0.318f, 0.812f, 0.4f);
        public static readonly Color BonusOrange   = new(1.0f, 0.663f, 0.302f);
        public static readonly Color MoveYellow    = new(1.0f, 0.831f, 0.231f);
        public static readonly Color ReactionPurple = new(0.8f, 0.365f, 0.91f);

        // Health colors
        public static readonly Color HealthGreen  = new(0.318f, 0.8f, 0.318f);
        public static readonly Color HealthYellow = new(0.902f, 0.831f, 0.263f);
        public static readonly Color HealthRed    = new(0.902f, 0.263f, 0.263f);

        // Drag handle
        public static readonly Color DragHandleBg     = new(28f / 255f, 24f / 255f, 36f / 255f, 0.8f);
        public static readonly Color DragHandleHover   = new(48f / 255f, 42f / 255f, 56f / 255f, 0.9f);
        public static readonly Color DragHandleDots    = new(200f / 255f, 168f / 255f, 78f / 255f, 0.5f);

        // Snap
        public static readonly Color SnapGuide = new(0.4f, 0.7f, 1.0f, 0.5f);

        // ── Font ──────────────────────────────────────────────────
        private static FontFile _gameFont;
        public static FontFile GameFont
        {
            get
            {
                if (_gameFont == null)
                {
                    _gameFont = ResourceLoader.Load<FontFile>("res://assets/Fonts/quadraat-regular.ttf");
                }
                return _gameFont;
            }
        }

        // ── Font Sizes ─────────────────────────────────────────────
        public const int FontTiny   = 8;
        public const int FontSmall  = 10;
        public const int FontNormal = 12;
        public const int FontMedium = 14;
        public const int FontLarge  = 16;
        public const int FontTitle  = 18;

        // ── Dimensions ─────────────────────────────────────────────
        public const int CornerRadiusSmall  = 4;
        public const int CornerRadiusMedium = 8;
        public const int CornerRadiusLarge  = 10;
        public const int DragHandleHeight   = 24;
        public const float SnapDistance      = 10f;
        public const int ResizeHandleSize   = 8;
        public const int MinPanelWidth      = 120;
        public const int MinPanelHeight     = 80;

        // ── Style Factory Methods ──────────────────────────────────

        public static StyleBoxFlat CreatePanelStyle(
            Color? bgColor = null,
            Color? borderColor = null,
            int cornerRadius = CornerRadiusMedium,
            int borderWidth = 1,
            int contentMargin = 0)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = bgColor ?? PrimaryDark;
            sb.SetCornerRadiusAll(cornerRadius);
            sb.SetBorderWidthAll(borderWidth);
            sb.BorderColor = borderColor ?? PanelBorder;
            if (contentMargin > 0)
            {
                sb.ContentMarginLeft = contentMargin;
                sb.ContentMarginRight = contentMargin;
                sb.ContentMarginTop = contentMargin;
                sb.ContentMarginBottom = contentMargin;
            }
            return sb;
        }

        public static StyleBoxFlat CreateDragHandleStyle(bool hovered = false)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = hovered ? DragHandleHover : DragHandleBg;
            sb.CornerRadiusTopLeft = CornerRadiusMedium;
            sb.CornerRadiusTopRight = CornerRadiusMedium;
            sb.CornerRadiusBottomLeft = 0;
            sb.CornerRadiusBottomRight = 0;
            sb.SetBorderWidthAll(0);
            sb.BorderWidthBottom = 1;
            sb.BorderColor = PanelBorderSubtle;
            return sb;
        }

        public static StyleBoxFlat CreateSeparatorStyle()
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(Gold.R, Gold.G, Gold.B, 0.15f);
            sb.ContentMarginTop = 1;
            sb.ContentMarginBottom = 1;
            return sb;
        }

        public static StyleBoxFlat CreateButtonStyle(Color bg, Color border, int cornerRadius = 6, int borderWidth = 1)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = bg;
            sb.SetCornerRadiusAll(cornerRadius);
            sb.SetBorderWidthAll(borderWidth);
            sb.BorderColor = border;
            return sb;
        }

        public static StyleBoxFlat CreateProgressBarBg()
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(0.15f, 0.1f, 0.1f, 0.8f);
            sb.SetCornerRadiusAll(2);
            return sb;
        }

        public static StyleBoxFlat CreateProgressBarFill(Color color)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = color;
            sb.SetCornerRadiusAll(2);
            return sb;
        }

        public static Color GetHealthColor(float hpPercent)
        {
            if (hpPercent > 50f) return HealthGreen;
            if (hpPercent > 25f) return HealthYellow;
            return HealthRed;
        }

        public static Color GetEncumbranceColor(float percent)
        {
            if (percent < 50f) return HealthGreen;
            if (percent < 90f) return HealthYellow;
            return HealthRed;
        }

        /// <summary>
        /// Apply standard label styling.
        /// </summary>
        public static void StyleLabel(Label label, int fontSize = FontNormal, Color? color = null)
        {
            if (GameFont != null) label.AddThemeFontOverride("font", GameFont);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color ?? WarmWhite);
        }

        /// <summary>
        /// Apply gold header styling to a label.
        /// </summary>
        public static void StyleHeader(Label label, int fontSize = FontMedium)
        {
            if (GameFont != null) label.AddThemeFontOverride("font", GameFont);
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", Gold);
        }

        /// <summary>
        /// Apply standard font styling to a button.
        /// </summary>
        public static void StyleButton(Button button, int fontSize = FontNormal, Color? fontColor = null)
        {
            if (GameFont != null) button.AddThemeFontOverride("font", GameFont);
            button.AddThemeFontSizeOverride("font_size", fontSize);
            if (fontColor.HasValue) button.AddThemeColorOverride("font_color", fontColor.Value);
        }

        // ── Rarity Colors ──────────────────────────────────────────
        public static readonly Color RarityCommon    = new(0.53f, 0.53f, 0.53f);
        public static readonly Color RarityUncommon  = new(0.25f, 0.56f, 0.25f);
        public static readonly Color RarityRare      = new(0.29f, 0.48f, 0.73f);
        public static readonly Color RarityVeryRare  = new(0.54f, 0.23f, 0.73f);
        public static readonly Color RarityLegendary = new(0.78f, 0.66f, 0.31f);

        public static Color GetRarityColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Uncommon => RarityUncommon,
                ItemRarity.Rare => RarityRare,
                ItemRarity.VeryRare => RarityVeryRare,
                ItemRarity.Legendary => RarityLegendary,
                _ => WarmWhite,
            };
        }

        public static Color GetCategoryBackground(ItemCategory category, ItemRarity rarity)
        {
            var rarityTint = rarity switch
            {
                ItemRarity.Uncommon => new Color(0.10f, 0.25f, 0.10f, 1f),
                ItemRarity.Rare => new Color(0.10f, 0.12f, 0.30f, 1f),
                ItemRarity.VeryRare => new Color(0.20f, 0.10f, 0.30f, 1f),
                ItemRarity.Legendary => new Color(0.35f, 0.22f, 0.06f, 1f),
                _ => new Color(0.08f, 0.08f, 0.10f, 1f),
            };

            return category switch
            {
                ItemCategory.Weapon => rarityTint + new Color(0.05f, 0.02f, 0f, 0f),
                ItemCategory.Armor => rarityTint + new Color(0f, 0.03f, 0.05f, 0f),
                ItemCategory.Clothing => rarityTint + new Color(0.03f, 0.02f, 0.05f, 0f),
                ItemCategory.Shield => rarityTint + new Color(0f, 0.05f, 0.03f, 0f),
                ItemCategory.Accessory => rarityTint + new Color(0.04f, 0f, 0.05f, 0f),
                ItemCategory.Headwear => rarityTint + new Color(0.03f, 0.03f, 0.05f, 0f),
                ItemCategory.Handwear => rarityTint + new Color(0.02f, 0.04f, 0.05f, 0f),
                ItemCategory.Footwear => rarityTint + new Color(0.03f, 0.04f, 0.05f, 0f),
                ItemCategory.Cloak => rarityTint + new Color(0.04f, 0.03f, 0.05f, 0f),
                ItemCategory.Amulet => rarityTint + new Color(0.05f, 0.03f, 0.03f, 0f),
                ItemCategory.Ring => rarityTint + new Color(0.05f, 0.03f, 0.04f, 0f),
                ItemCategory.Potion => rarityTint + new Color(0.08f, 0f, 0f, 0f),
                ItemCategory.Scroll => rarityTint + new Color(0.04f, 0.03f, 0f, 0f),
                ItemCategory.Throwable => rarityTint + new Color(0.05f, 0.02f, 0f, 0f),
                _ => rarityTint,
            };
        }

        // ── Additional Style Factory Methods ───────────────────────

        public static StyleBoxFlat CreateFullScreenBg()
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(0.03f, 0.025f, 0.045f, 0.97f);
            sb.SetCornerRadiusAll(CornerRadiusLarge);
            sb.SetBorderWidthAll(2);
            sb.BorderColor = PanelBorder;
            sb.ContentMarginLeft = 16;
            sb.ContentMarginRight = 16;
            sb.ContentMarginTop = 8;
            sb.ContentMarginBottom = 8;
            return sb;
        }

        public static StyleBoxFlat CreateTabButtonStyle(bool active)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = active
                ? new Color(PrimaryDark.R, PrimaryDark.G, PrimaryDark.B, 0.95f)
                : new Color(TertiaryDark.R, TertiaryDark.G, TertiaryDark.B, 0.6f);
            sb.SetCornerRadiusAll(24);
            sb.SetBorderWidthAll(active ? 2 : 1);
            sb.BorderColor = active ? Gold : PanelBorderSubtle;
            sb.ContentMarginLeft = 4;
            sb.ContentMarginRight = 4;
            sb.ContentMarginTop = 4;
            sb.ContentMarginBottom = 4;
            return sb;
        }

        public static StyleBoxFlat CreateAbilityScoreBoxStyle()
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(SecondaryDark.R, SecondaryDark.G, SecondaryDark.B, 0.9f);
            sb.SetCornerRadiusAll(CornerRadiusSmall);
            sb.SetBorderWidthAll(1);
            sb.BorderColor = PanelBorderSubtle;
            sb.ContentMarginLeft = 4;
            sb.ContentMarginRight = 4;
            sb.ContentMarginTop = 2;
            sb.ContentMarginBottom = 2;
            return sb;
        }

        public static StyleBoxFlat CreateAcBadgeStyle()
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.95f);
            sb.SetCornerRadiusAll(CornerRadiusMedium);
            sb.SetBorderWidthAll(2);
            sb.BorderColor = Gold;
            sb.ContentMarginLeft = 8;
            sb.ContentMarginRight = 8;
            sb.ContentMarginTop = 4;
            sb.ContentMarginBottom = 4;
            return sb;
        }

        // ── Portrait / Slot / Hotbar Constants ─────────────────────

        // Portrait sizes
        public const int PortraitWidth = 80;
        public const int PortraitHeight = 100;
        public const int PortraitHpBarHeight = 5;
        public const int PortraitSpacing = 6;
        public const int PortraitBorderSelected = 3;
        public const int PortraitBorderNormal = 1;

        // New colors
        public static readonly Color PortraitBgDark = new(0.06f, 0.05f, 0.08f, 0.85f);
        public static readonly Color SelectionGlow = new(200f / 255f, 168f / 255f, 78f / 255f, 0.45f);
        public static readonly Color SlotInsetDark = new(0.04f, 0.035f, 0.06f, 0.95f);
        public static readonly Color SlotHover = new(200f / 255f, 168f / 255f, 78f / 255f, 0.25f);
        public static readonly Color SlotSelected = new(200f / 255f, 168f / 255f, 78f / 255f, 0.6f);

        // ── Portrait / Slot / Hotbar Style Factories ───────────────

        /// <summary>
        /// Portrait frame style — gold border when selected, faction-tinted subtle border otherwise.
        /// </summary>
        public static StyleBoxFlat CreatePortraitFrameStyle(bool selected, bool isPlayer = true)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = PortraitBgDark;
            sb.SetCornerRadiusAll(CornerRadiusSmall);
            sb.SetBorderWidthAll(selected ? PortraitBorderSelected : PortraitBorderNormal);
            sb.BorderColor = selected
                ? Gold
                : (isPlayer
                    ? new Color(PlayerBlue.R, PlayerBlue.G, PlayerBlue.B, 0.4f)
                    : new Color(EnemyRed.R, EnemyRed.G, EnemyRed.B, 0.4f));

            if (selected)
            {
                sb.ShadowColor = SelectionGlow;
                sb.ShadowSize = 4;
            }
            return sb;
        }

        /// <summary>
        /// Slot inset style (for action slots, inventory slots, equip slots).
        /// </summary>
        public static StyleBoxFlat CreateSlotInsetStyle(bool hovered = false, bool selected = false)
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = selected ? SlotSelected : (hovered ? SlotHover : SlotInsetDark);
            sb.SetCornerRadiusAll(3);
            sb.SetBorderWidthAll(selected ? 2 : 1);
            sb.BorderColor = selected ? Gold : PanelBorderSubtle;
            if (selected)
            {
                sb.ShadowColor = new Color(Gold.R, Gold.G, Gold.B, 0.2f);
                sb.ShadowSize = 2;
            }
            return sb;
        }

        /// <summary>
        /// Hotbar module frame style.
        /// </summary>
        public static StyleBoxFlat CreateHotbarModuleStyle()
        {
            var sb = new StyleBoxFlat();
            sb.BgColor = new Color(PrimaryDark.R, PrimaryDark.G, PrimaryDark.B, 0.96f);
            sb.SetCornerRadiusAll(CornerRadiusMedium);
            sb.SetBorderWidthAll(2);
            sb.BorderColor = PanelBorder;
            sb.ContentMarginLeft = 6;
            sb.ContentMarginRight = 6;
            sb.ContentMarginTop = 4;
            sb.ContentMarginBottom = 4;
            return sb;
        }

        /// <summary>
        /// Category tab bar style (built into hotbar bottom lip).
        /// </summary>
        public static StyleBoxFlat CreateCatTabStyle(bool active)
        {
            var sb = new StyleBoxFlat();
            if (active)
            {
                sb.BgColor = new Color(PrimaryDark.R * 1.3f, PrimaryDark.G * 1.3f, PrimaryDark.B * 1.3f, 0.98f);
                sb.SetBorderWidthAll(1);
                sb.BorderColor = Gold;
                sb.BorderWidthTop = 2;
            }
            else
            {
                sb.BgColor = new Color(TertiaryDark.R, TertiaryDark.G, TertiaryDark.B, 0.7f);
                sb.SetBorderWidthAll(0);
                sb.BorderWidthTop = 1;
                sb.BorderColor = PanelBorderSubtle;
            }
            sb.CornerRadiusBottomLeft = CornerRadiusSmall;
            sb.CornerRadiusBottomRight = CornerRadiusSmall;
            sb.CornerRadiusTopLeft = 0;
            sb.CornerRadiusTopRight = 0;
            sb.ContentMarginLeft = 8;
            sb.ContentMarginRight = 8;
            sb.ContentMarginTop = 3;
            sb.ContentMarginBottom = 4;
            return sb;
        }
    }
}
