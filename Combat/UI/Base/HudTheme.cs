using Godot;

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
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color ?? WarmWhite);
        }

        /// <summary>
        /// Apply gold header styling to a label.
        /// </summary>
        public static void StyleHeader(Label label, int fontSize = FontMedium)
        {
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", Gold);
        }
    }
}
