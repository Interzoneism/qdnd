using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Initiative ribbon at top of screen showing turn order with combatant portraits.
    /// Displays all combatants centered horizontally.
    /// </summary>
    public partial class InitiativeRibbon : HudPanel
    {
        private static readonly Dictionary<string, Texture2D> _portraitCache = new();
        private HBoxContainer _portraitContainer;
        private CenterContainer _centerWrapper;
        private Label _roundLabel;
        private readonly Dictionary<string, PortraitEntry> _entries = new();
        private string _activeId;

        public InitiativeRibbon()
        {
            PanelTitle = "INITIATIVE";
            ShowDragHandle = true;
            Draggable = false;
        }

        public override void _Ready()
        {
            base._Ready();
            AddThemeStyleboxOverride("panel", HudTheme.CreateHotbarModuleStyle());
        }

        protected override void BuildContent(Control parent)
        {
            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 4);
            vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            parent.AddChild(vbox);

            // Header with round counter
            var header = new HBoxContainer();
            vbox.AddChild(header);

            _roundLabel = new Label();
            _roundLabel.Text = "R1";
            HudTheme.StyleHeader(_roundLabel, HudTheme.FontMedium);
            _roundLabel.AddThemeColorOverride("font_color", HudTheme.Gold);
            header.AddChild(_roundLabel);

            var spacer = new Control();
            spacer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            header.AddChild(spacer);

            // Center wrapper so the portrait row is always horizontally centered
            _centerWrapper = new CenterContainer();
            _centerWrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            vbox.AddChild(_centerWrapper);

            // Portrait container
            _portraitContainer = new HBoxContainer();
            _portraitContainer.AddThemeConstantOverride("separation", 6);
            _centerWrapper.AddChild(_portraitContainer);
        }

        /// <summary>
        /// Set the full turn order.
        /// </summary>
        public void SetTurnOrder(IReadOnlyList<TurnTrackerEntry> entries, string activeId)
        {
            // Clear existing
            foreach (var child in _portraitContainer.GetChildren())
            {
                child.QueueFree();
            }
            _entries.Clear();

            _activeId = activeId;

            // Create portraits for each combatant
            foreach (var entry in entries)
            {
                var portrait = CreatePortraitEntry(entry);
                _entries[entry.CombatantId] = portrait;
                _portraitContainer.AddChild(portrait.Container);
            }
        }

        /// <summary>
        /// Set the active combatant.
        /// </summary>
        public void SetActiveCombatant(string combatantId)
        {
            _activeId = combatantId;

            // Update highlights
            foreach (var kvp in _entries)
            {
                bool isActive = kvp.Key == combatantId;
                UpdateHighlight(kvp.Value, isActive);
            }
        }

        /// <summary>
        /// Update a specific entry.
        /// </summary>
        public void UpdateEntry(string combatantId, TurnTrackerEntry entry)
        {
            if (_entries.TryGetValue(combatantId, out var portraitEntry))
            {
                UpdatePortraitEntry(portraitEntry, entry);
            }
        }

        /// <summary>
        /// Set the round number.
        /// </summary>
        public void SetRound(int round)
        {
            _roundLabel.Text = $"R{round}";
        }

        private PortraitEntry CreatePortraitEntry(TurnTrackerEntry entry)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(64, 80);
            panel.MouseFilter = MouseFilterEnum.Ignore;

            panel.AddThemeStyleboxOverride("panel", HudTheme.CreatePortraitFrameStyle(false, entry.IsPlayer));

            var vbox = new VBoxContainer();
            vbox.AddThemeConstantOverride("separation", 2);
            panel.AddChild(vbox);

            // Initiative number
            var initLabel = new Label();
            initLabel.Text = entry.Initiative.ToString();
            initLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(initLabel, HudTheme.FontSmall, HudTheme.Gold);
            initLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(initLabel);

            // Portrait image (loaded from combatant's assigned portrait path)
            // TODO: Replace placeholder random portraits with proper character art
            var portrait = new TextureRect();
            portrait.CustomMinimumSize = new Vector2(48, 48);
            portrait.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            portrait.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
            portrait.MouseFilter = MouseFilterEnum.Ignore;

            var tex = LoadPortraitCached(entry.PortraitPath);
            if (tex != null)
            {
                portrait.Texture = tex;
            }

            // Tint fallback if no portrait texture loaded
            if (portrait.Texture == null)
            {
                portrait.Modulate = entry.IsPlayer
                    ? new Color(HudTheme.PlayerBlue.R, HudTheme.PlayerBlue.G, HudTheme.PlayerBlue.B, 0.3f)
                    : new Color(HudTheme.EnemyRed.R, HudTheme.EnemyRed.G, HudTheme.EnemyRed.B, 0.3f);
            }

            vbox.AddChild(portrait);

            // Name
            var nameLabel = new Label();
            nameLabel.Text = entry.DisplayName.Length > 10 
                ? entry.DisplayName[..10] + "\u2026"
                : entry.DisplayName;
            nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(nameLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
            nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(nameLabel);

            // HP bar
            var hpBar = new ProgressBar();
            hpBar.CustomMinimumSize = new Vector2(0, 4);
            hpBar.ShowPercentage = false;
            hpBar.Value = entry.HpPercent * 100;
            hpBar.MaxValue = 100;
            hpBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            hpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(
                HudTheme.GetHealthColor(entry.HpPercent * 100)));
            hpBar.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(hpBar);

            // Status icons row
            var statusRow = new HBoxContainer();
            statusRow.AddThemeConstantOverride("separation", 1);
            statusRow.Alignment = BoxContainer.AlignmentMode.Center;
            statusRow.MouseFilter = MouseFilterEnum.Ignore;
            vbox.AddChild(statusRow);

            var portraitEntry = new PortraitEntry
            {
                Container = panel,
                InitLabel = initLabel,
                NameLabel = nameLabel,
                HpBar = hpBar,
                Portrait = portrait,
                StatusRow = statusRow,
                Entry = entry
            };

            // Apply dead/active state
            if (entry.IsDead)
            {
                panel.Modulate = new Color(1, 1, 1, 0.3f);
            }

            UpdateHighlight(portraitEntry, entry.IsActive);

            // Populate initial status icons
            UpdateStatusIcons(portraitEntry, entry.StatusIcons);

            return portraitEntry;
        }

        private void UpdatePortraitEntry(PortraitEntry portraitEntry, TurnTrackerEntry entry)
        {
            portraitEntry.Entry = entry;

            // Update HP bar
            portraitEntry.HpBar.Value = entry.HpPercent * 100;
            portraitEntry.HpBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(
                HudTheme.GetHealthColor(entry.HpPercent * 100)));

            // Update status icons
            UpdateStatusIcons(portraitEntry, entry.StatusIcons);

            // Update dead state
            if (entry.IsDead)
            {
                portraitEntry.Container.Modulate = new Color(1, 1, 1, 0.3f);
            }
            else
            {
                portraitEntry.Container.Modulate = Colors.White;
            }

            UpdateHighlight(portraitEntry, entry.CombatantId == _activeId);
        }

        private static Texture2D LoadPortraitCached(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            
            if (_portraitCache.TryGetValue(path, out var cached))
                return cached;
            
            if (!ResourceLoader.Exists(path))
            {
                _portraitCache[path] = null;
                return null;
            }
            
            var texture = GD.Load<Texture2D>(path);
            _portraitCache[path] = texture;
            return texture;
        }

        private void UpdateHighlight(PortraitEntry portraitEntry, bool isActive)
        {
            portraitEntry.Container.AddThemeStyleboxOverride("panel",
                HudTheme.CreatePortraitFrameStyle(isActive, portraitEntry.Entry.IsPlayer));
            portraitEntry.Container.Scale = isActive
                ? new Vector2(1.05f, 1.05f)
                : Vector2.One;
        }

        /// <summary>
        /// Populate status icon row from the entry's StatusIcons list.
        /// Each icon is a small colored rectangle with a 1–2 letter abbreviation.
        /// </summary>
        private static void UpdateStatusIcons(PortraitEntry portraitEntry, List<string> statusIcons)
        {
            if (portraitEntry.StatusRow == null) return;

            // Clear existing icons
            foreach (var child in portraitEntry.StatusRow.GetChildren())
            {
                child.QueueFree();
            }

            if (statusIcons == null || statusIcons.Count == 0) return;

            foreach (var icon in statusIcons)
            {
                var iconPanel = new PanelContainer();
                iconPanel.CustomMinimumSize = new Vector2(16, 16);
                iconPanel.MouseFilter = MouseFilterEnum.Ignore;

                var bgColor = GetStatusColor(icon);
                iconPanel.AddThemeStyleboxOverride("panel",
                    HudTheme.CreatePanelStyle(bgColor, bgColor, 2, 0, 1));

                var label = new Label();
                label.Text = icon.Length > 2 ? icon.Substring(0, 2).ToUpper() : icon.ToUpper();
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Center;
                HudTheme.StyleLabel(label, HudTheme.FontTiny, HudTheme.WarmWhite);
                label.MouseFilter = MouseFilterEnum.Ignore;
                iconPanel.AddChild(label);

                portraitEntry.StatusRow.AddChild(iconPanel);
            }
        }

        /// <summary>
        /// Map status icon names to colors: buff=green, debuff=red, condition=orange, neutral=gray.
        /// Prefixes: "buff_", "debuff_", "cond_" recognized. Everything else is neutral.
        /// </summary>
        private static Color GetStatusColor(string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return new Color(0.4f, 0.4f, 0.4f, 0.8f);

            string lower = iconName.ToLower();
            if (lower.StartsWith("buff") || lower.Contains("bless") || lower.Contains("haste"))
                return new Color(0.2f, 0.7f, 0.3f, 0.85f); // Green
            if (lower.StartsWith("debuff") || lower.Contains("curse") || lower.Contains("bane"))
                return new Color(0.8f, 0.2f, 0.2f, 0.85f); // Red
            if (lower.StartsWith("cond") || lower.Contains("prone") || lower.Contains("stun") || lower.Contains("blind"))
                return new Color(0.9f, 0.6f, 0.1f, 0.85f); // Orange

            return new Color(0.4f, 0.4f, 0.4f, 0.8f); // Gray/neutral
        }

        private class PortraitEntry
        {
            public PanelContainer Container { get; set; }
            public Label InitLabel { get; set; }
            public Label NameLabel { get; set; }
            public ProgressBar HpBar { get; set; }
            public TextureRect Portrait { get; set; }
            public HBoxContainer StatusRow { get; set; }
            public TurnTrackerEntry Entry { get; set; }
        }
    }
}
