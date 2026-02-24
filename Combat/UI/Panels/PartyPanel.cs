using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Data for a single party member.
    /// </summary>
    public class PartyMemberData
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int HpCurrent { get; set; }
        public int HpMax { get; set; }
        public List<string> Conditions { get; set; } = new();
        public bool IsSelected { get; set; }
        public string RaceClass { get; set; }
        public string PortraitPath { get; set; }
    }

    /// <summary>
    /// BG3-style compact party portrait panel.
    /// Renders as a vertical stack of portrait cards on the left edge of the screen.
    /// Each card shows a colored portrait placeholder, HP overlay text, a thin HP bar,
    /// condition indicator dots, and a gold border when selected.
    /// </summary>
    public partial class PartyPanel : HudPanel
    {
        public event Action<string> OnMemberClicked;

        private const int ConditionDotSize = 7;
        private const int ConditionDotSpacing = 2;

        private VBoxContainer _memberContainer;
        private readonly Dictionary<string, PortraitCard> _cards = new();
        private string _selectedId;

        public PartyPanel()
        {
            PanelTitle = "";
            ShowDragHandle = false;
            Draggable = false;
            CustomMinimumSize = new Vector2(
                HudTheme.PortraitWidth + HudTheme.PortraitBorderSelected * 2 + 4, 0);
        }

        protected override void BuildContent(Control parent)
        {
            _memberContainer = new VBoxContainer();
            _memberContainer.AddThemeConstantOverride("separation", HudTheme.PortraitSpacing);
            parent.AddChild(_memberContainer);
        }

        public override void _Ready()
        {
            base._Ready();

            // Make background transparent — no heavy panel chrome
            var transparentStyle = new StyleBoxFlat();
            transparentStyle.BgColor = Colors.Transparent;
            transparentStyle.SetBorderWidthAll(0);
            AddThemeStyleboxOverride("panel", transparentStyle);
        }

        // ── Public API ─────────────────────────────────────────────

        /// <summary>
        /// Set all party members. Rebuilds the portrait stack.
        /// </summary>
        public void SetPartyMembers(IReadOnlyList<PartyMemberData> members)
        {
            foreach (var child in _memberContainer.GetChildren())
            {
                child.QueueFree();
            }
            _cards.Clear();

            foreach (var member in members)
            {
                var card = CreatePortraitCard(member);
                _cards[member.Id] = card;
                _memberContainer.AddChild(card.Root);
            }
        }

        /// <summary>
        /// Update a member's HP and conditions.
        /// </summary>
        public void UpdateMember(string id, int currentHp, int maxHp, List<string> conditions)
        {
            if (!_cards.TryGetValue(id, out var card)) return;

            float hpPercent = maxHp > 0 ? (float)(currentHp / (double)maxHp) * 100f : 0f;

            // HP text
            card.HpLabel.Text = $"{currentHp}/{maxHp}";

            // HP bar
            card.HpBar.Value = hpPercent;
            card.HpBar.AddThemeStyleboxOverride("fill",
                HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPercent)));

            // Condition dots
            RebuildConditionDots(card, conditions);

            // Border style may change with health
            ApplyBorder(card, card.IsSelected);
        }

        /// <summary>
        /// Set the selected member (gold border highlight).
        /// </summary>
        public void SetSelectedMember(string id)
        {
            _selectedId = id;

            foreach (var kvp in _cards)
            {
                bool sel = kvp.Key == id;
                kvp.Value.IsSelected = sel;
                ApplyBorder(kvp.Value, sel);
            }
        }

        // ── Card Construction ──────────────────────────────────────

        private PortraitCard CreatePortraitCard(PartyMemberData member)
        {
            int pw = HudTheme.PortraitWidth;
            int ph = HudTheme.PortraitHeight;
            int hpBarH = HudTheme.PortraitHpBarHeight;
            int bSel = HudTheme.PortraitBorderSelected;

            // Root button — the entire card is clickable
            var button = new Button();
            button.CustomMinimumSize = new Vector2(
                pw + bSel * 2,
                ph + hpBarH + bSel * 2);
            button.ClipText = true;
            button.Pressed += () => OnMemberClicked?.Invoke(member.Id);

            // Layering container on top of button
            var layers = new Control();
            layers.SetAnchorsPreset(LayoutPreset.FullRect);
            layers.MouseFilter = MouseFilterEnum.Ignore;
            button.AddChild(layers);

            // Portrait background (fallback color)
            var portraitBg = new ColorRect();
            portraitBg.Position = new Vector2(bSel, bSel);
            portraitBg.Size = new Vector2(pw, ph);
            portraitBg.Color = new Color(
                HudTheme.PlayerBlue.R, HudTheme.PlayerBlue.G,
                HudTheme.PlayerBlue.B, 0.35f);
            portraitBg.MouseFilter = MouseFilterEnum.Ignore;
            layers.AddChild(portraitBg);

            // Portrait texture (loaded from PortraitPath)
            var portrait = new TextureRect();
            portrait.Position = new Vector2(bSel, bSel);
            portrait.Size = new Vector2(pw, ph);
            portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            portrait.MouseFilter = MouseFilterEnum.Ignore;
            if (!string.IsNullOrEmpty(member.PortraitPath) && ResourceLoader.Exists(member.PortraitPath))
            {
                portrait.Texture = GD.Load<Texture2D>(member.PortraitPath);
            }
            layers.AddChild(portrait);

            // HP bar — thin strip below the portrait
            var hpBar = new ProgressBar();
            hpBar.Position = new Vector2(bSel, bSel + ph);
            hpBar.Size = new Vector2(pw, hpBarH);
            hpBar.ShowPercentage = false;
            hpBar.MaxValue = 100;
            float hpPercent = member.HpMax > 0
                ? (float)(member.HpCurrent / (double)member.HpMax) * 100f
                : 0f;
            hpBar.Value = hpPercent;
            hpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            hpBar.AddThemeStyleboxOverride("fill",
                HudTheme.CreateProgressBarFill(HudTheme.GetHealthColor(hpPercent)));
            hpBar.MouseFilter = MouseFilterEnum.Ignore;
            layers.AddChild(hpBar);

            // HP text overlay — bottom of the portrait area, centered
            var hpLabel = new Label();
            hpLabel.Text = $"{member.HpCurrent}/{member.HpMax}";
            HudTheme.StyleLabel(hpLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hpLabel.VerticalAlignment = VerticalAlignment.Bottom;
            hpLabel.Position = new Vector2(bSel, bSel);
            hpLabel.Size = new Vector2(pw, ph);
            hpLabel.MouseFilter = MouseFilterEnum.Ignore;
            // Black shadow for readability over the portrait
            hpLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
            hpLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            hpLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            layers.AddChild(hpLabel);

            // Condition dots container (top-right corner of portrait)
            var conditionContainer = new HBoxContainer();
            conditionContainer.AddThemeConstantOverride("separation", ConditionDotSpacing);
            conditionContainer.MouseFilter = MouseFilterEnum.Ignore;
            conditionContainer.Position = new Vector2(bSel + pw - 2, bSel + 2);
            conditionContainer.LayoutDirection = LayoutDirectionEnum.Rtl;
            layers.AddChild(conditionContainer);

            var card = new PortraitCard
            {
                Root = button,
                PortraitBg = portraitBg,
                Portrait = portrait,
                HpBar = hpBar,
                HpLabel = hpLabel,
                ConditionContainer = conditionContainer,
                IsSelected = member.IsSelected,
                MemberId = member.Id
            };

            RebuildConditionDots(card, member.Conditions);
            ApplyBorder(card, member.IsSelected);

            return card;
        }

        // ── Helpers ────────────────────────────────────────────────

        private static void ApplyBorder(PortraitCard card, bool selected)
        {
            var frameStyle = HudTheme.CreatePortraitFrameStyle(selected);

            // Build hover / pressed variants
            var hoverStyle = HudTheme.CreatePortraitFrameStyle(selected);
            hoverStyle.BgColor = new Color(
                frameStyle.BgColor.R * 1.25f,
                frameStyle.BgColor.G * 1.25f,
                frameStyle.BgColor.B * 1.25f,
                frameStyle.BgColor.A);

            var pressedStyle = HudTheme.CreatePortraitFrameStyle(selected);
            pressedStyle.BgColor = new Color(
                frameStyle.BgColor.R * 0.8f,
                frameStyle.BgColor.G * 0.8f,
                frameStyle.BgColor.B * 0.8f,
                frameStyle.BgColor.A);

            card.Root.AddThemeStyleboxOverride("normal", frameStyle);
            card.Root.AddThemeStyleboxOverride("hover", hoverStyle);
            card.Root.AddThemeStyleboxOverride("pressed", pressedStyle);
        }

        /// <summary>
        /// Map conditions to small colored dots at the top-right of the portrait.
        /// </summary>
        private static void RebuildConditionDots(PortraitCard card, List<string> conditions)
        {
            foreach (var child in card.ConditionContainer.GetChildren())
            {
                child.QueueFree();
            }

            if (conditions == null || conditions.Count == 0) return;

            // Show up to 4 dot indicators
            int count = System.Math.Min(conditions.Count, 4);
            for (int i = 0; i < count; i++)
            {
                var dot = new ColorRect();
                dot.CustomMinimumSize = new Vector2(ConditionDotSize, ConditionDotSize);
                dot.Color = GetConditionDotColor(conditions[i]);
                dot.MouseFilter = MouseFilterEnum.Ignore;
                card.ConditionContainer.AddChild(dot);
            }
        }

        /// <summary>
        /// Simple heuristic color for condition dots.
        /// </summary>
        private static Color GetConditionDotColor(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return HudTheme.MutedBeige;

            var lower = condition.ToLowerInvariant();
            if (lower.Contains("poison") || lower.Contains("acid"))
                return new Color(0.3f, 0.85f, 0.3f);
            if (lower.Contains("fire") || lower.Contains("burn"))
                return new Color(0.95f, 0.45f, 0.15f);
            if (lower.Contains("frozen") || lower.Contains("cold") || lower.Contains("ice"))
                return new Color(0.4f, 0.75f, 0.95f);
            if (lower.Contains("bless") || lower.Contains("buff") || lower.Contains("haste"))
                return HudTheme.Gold;
            if (lower.Contains("curse") || lower.Contains("hex") || lower.Contains("frighten"))
                return new Color(0.6f, 0.2f, 0.8f);
            if (lower.Contains("stun") || lower.Contains("paralyze") || lower.Contains("incapacitate"))
                return new Color(0.95f, 0.9f, 0.3f);
            if (lower.Contains("prone") || lower.Contains("restrain"))
                return new Color(0.65f, 0.45f, 0.2f);

            return HudTheme.MutedBeige;
        }

        // ── Internal State ─────────────────────────────────────────

        private class PortraitCard
        {
            public Button Root { get; set; }
            public ColorRect PortraitBg { get; set; }
            public TextureRect Portrait { get; set; }
            public ProgressBar HpBar { get; set; }
            public Label HpLabel { get; set; }
            public HBoxContainer ConditionContainer { get; set; }
            public bool IsSelected { get; set; }
            public string MemberId { get; set; }
        }
    }

    // Extension method helper for button styling
    internal static class ButtonExtensions
    {
        public static void FlatStyleBox(this Button button, Color bg, Color border, int borderWidth = 1)
        {
            var normal = HudTheme.CreateButtonStyle(bg, border, borderWidth: borderWidth);
            var hover = HudTheme.CreateButtonStyle(
                new Color(bg.R * 1.2f, bg.G * 1.2f, bg.B * 1.2f),
                border,
                borderWidth: borderWidth);
            var pressed = HudTheme.CreateButtonStyle(
                new Color(bg.R * 0.8f, bg.G * 0.8f, bg.B * 0.8f),
                border,
                borderWidth: borderWidth);

            button.AddThemeStyleboxOverride("normal", normal);
            button.AddThemeStyleboxOverride("hover", hover);
            button.AddThemeStyleboxOverride("pressed", pressed);
        }
    }
}
