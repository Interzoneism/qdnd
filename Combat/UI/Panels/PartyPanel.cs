using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        private const int MaxVisibleConditionIcons = 4;
        private const int ConditionIconSize = 22;
        private const int ConditionDotSpacing = 2;
        private const int ConditionIconOffset = 6;
        private static readonly Dictionary<string, string> ConditionIconIndex = new(StringComparer.OrdinalIgnoreCase);
        private static bool _conditionIconIndexBuilt;

        private VBoxContainer _memberContainer;
        private readonly Dictionary<string, PortraitCard> _cards = new();
        private string _selectedId;

        public PartyPanel()
        {
            PanelTitle = "";
            ShowDragHandle = false;
            Draggable = false;
            CustomMinimumSize = new Vector2(
                HudTheme.PortraitWidth + HudTheme.PortraitBorderSelected * 2 + GetConditionAreaWidth() + 4f,
                0f);
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
            float conditionAreaWidth = GetConditionAreaWidth();

            // Root button — the entire card is clickable
            var button = new Button();
            button.CustomMinimumSize = new Vector2(
                pw + bSel * 2 + conditionAreaWidth,
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
            HudTheme.StyleLabel(hpLabel, HudTheme.FontTiny, HudTheme.WarmWhite);
            hpLabel.HorizontalAlignment = HorizontalAlignment.Center;
            hpLabel.VerticalAlignment = VerticalAlignment.Bottom;
            hpLabel.Position = new Vector2(bSel, bSel);
            hpLabel.Size = new Vector2(pw, ph - 10);
            hpLabel.MouseFilter = MouseFilterEnum.Ignore;
            // Black shadow for readability over the portrait
            hpLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
            hpLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            hpLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            layers.AddChild(hpLabel);

            // Frame overlay — border rendered on top of portrait layers
            var frameOverlay = new PanelContainer();
            frameOverlay.Position = new Vector2(0, 0);
            frameOverlay.Size = new Vector2(pw + bSel * 2, ph + hpBarH + bSel * 2);
            frameOverlay.MouseFilter = MouseFilterEnum.Ignore;
            layers.AddChild(frameOverlay);

            // Condition icons container — to the RIGHT of the portrait
            var conditionContainer = new HBoxContainer();
            conditionContainer.AddThemeConstantOverride("separation", ConditionDotSpacing);
            conditionContainer.MouseFilter = MouseFilterEnum.Ignore;
            conditionContainer.Position = new Vector2(
                bSel + pw + ConditionIconOffset,
                bSel + (ph - ConditionIconSize) * 0.5f);
            conditionContainer.CustomMinimumSize = new Vector2(conditionAreaWidth, ConditionIconSize);
            layers.AddChild(conditionContainer);

            var card = new PortraitCard
            {
                Root = button,
                PortraitBg = portraitBg,
                Portrait = portrait,
                HpBar = hpBar,
                HpLabel = hpLabel,
                FrameOverlay = frameOverlay,
                ConditionContainer = conditionContainer,
                IsSelected = member.IsSelected,
                MemberId = member.Id
            };

            // Make button fully transparent — frame overlay handles the visual border
            var transparentStyle = new StyleBoxFlat();
            transparentStyle.BgColor = Colors.Transparent;
            transparentStyle.SetBorderWidthAll(0);
            button.AddThemeStyleboxOverride("normal", transparentStyle);
            button.AddThemeStyleboxOverride("hover", transparentStyle);
            button.AddThemeStyleboxOverride("pressed", transparentStyle);
            button.AddThemeStyleboxOverride("focus", transparentStyle);

            RebuildConditionDots(card, member.Conditions);
            ApplyBorder(card, member.IsSelected);

            return card;
        }

        // ── Helpers ────────────────────────────────────────────────

        private static void ApplyBorder(PortraitCard card, bool selected)
        {
            // Frame overlay shows the border; button itself stays transparent
            var frameStyle = HudTheme.CreatePortraitFrameStyle(selected);
            frameStyle.BgColor = Colors.Transparent; // border-only overlay
            card.FrameOverlay.AddThemeStyleboxOverride("panel", frameStyle);
        }

        /// <summary>
        /// Map conditions to 30×30 texture icons to the right of the portrait.
        /// Falls back to a centered 7×7 colored dot if no icon asset is found.
        /// </summary>
        private static void RebuildConditionDots(PortraitCard card, List<string> conditions)
        {
            foreach (var child in card.ConditionContainer.GetChildren())
            {
                child.QueueFree();
            }

            if (conditions == null || conditions.Count == 0) return;

            int count = System.Math.Min(conditions.Count, MaxVisibleConditionIcons);
            for (int i = 0; i < count; i++)
            {
                string iconPath = ResolveConditionIconPath(conditions[i]);
                if (iconPath != null)
                {
                    var icon = new TextureRect();
                    icon.CustomMinimumSize = new Vector2(ConditionIconSize, ConditionIconSize);
                    icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
                    icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    icon.Texture = GD.Load<Texture2D>(iconPath);
                    icon.MouseFilter = MouseFilterEnum.Ignore;
                    card.ConditionContainer.AddChild(icon);
                }
                else
                {
                    // Fallback: 7×7 dot centered inside a 30×30 container
                    var container = new Control();
                    container.CustomMinimumSize = new Vector2(ConditionIconSize, ConditionIconSize);
                    container.MouseFilter = MouseFilterEnum.Ignore;
                    var dot = new ColorRect();
                    dot.Position = new Vector2((ConditionIconSize - 7) / 2f, (ConditionIconSize - 7) / 2f);
                    dot.Size = new Vector2(7, 7);
                    dot.Color = GetConditionDotColor(conditions[i]);
                    dot.MouseFilter = MouseFilterEnum.Ignore;
                    container.AddChild(dot);
                    card.ConditionContainer.AddChild(container);
                }
            }
        }

        /// <summary>
        /// Convert a condition name to its icon resource path.
        /// Returns null if no matching icon file exists.
        /// </summary>
        private static string ResolveConditionIconPath(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return null;

            if (condition.StartsWith("res://", StringComparison.OrdinalIgnoreCase) &&
                ResourceLoader.Exists(condition))
            {
                return condition;
            }

            EnsureConditionIconIndex();
            foreach (var key in EnumerateConditionLookupKeys(condition))
            {
                if (ConditionIconIndex.TryGetValue(key, out var path) && ResourceLoader.Exists(path))
                    return path;
            }

            return null;
        }

        private static void EnsureConditionIconIndex()
        {
            if (_conditionIconIndexBuilt)
                return;

            _conditionIconIndexBuilt = true;
            var dir = DirAccess.Open("res://assets/Images/Icons Conditions");
            if (dir == null)
                return;

            foreach (var fileName in dir.GetFiles())
            {
                if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    continue;

                string stem = fileName[..^4];
                stem = StripConditionIconSuffix(stem);
                string key = NormalizeConditionLookupKey(stem);
                if (string.IsNullOrEmpty(key))
                    continue;

                string path = $"res://assets/Images/Icons Conditions/{fileName}";
                if (!ConditionIconIndex.ContainsKey(key))
                    ConditionIconIndex[key] = path;
            }
        }

        private static IEnumerable<string> EnumerateConditionLookupKeys(string condition)
        {
            string normalized = NormalizeConditionLookupKey(condition);
            if (!string.IsNullOrEmpty(normalized))
                yield return normalized;

            var words = condition.Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0)
            {
                string titleCase = string.Join("_", words.Select(w =>
                    char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..].ToLowerInvariant() : "")));
                string titleCaseKey = NormalizeConditionLookupKey(titleCase);
                if (!string.IsNullOrEmpty(titleCaseKey))
                    yield return titleCaseKey;
            }
        }

        private static string StripConditionIconSuffix(string value)
        {
            const string suffix = "_condition_icon";
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return value[..(value.Length - suffix.Length)];
            return value;
        }

        private static string NormalizeConditionLookupKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static float GetConditionAreaWidth()
        {
            return MaxVisibleConditionIcons * ConditionIconSize
                + (MaxVisibleConditionIcons - 1) * ConditionDotSpacing
                + ConditionIconOffset;
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
            public PanelContainer FrameOverlay { get; set; }
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
