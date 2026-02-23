using System;
using System.Collections.Generic;
using Godot;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// Combat log panel showing scrollable battle events.
    /// </summary>
    public partial class CombatLogPanel : HudResizablePanel
    {
        private HBoxContainer _filterContainer;
        private ScrollContainer _scrollContainer;
        private RichTextLabel _logText;
        private readonly List<CombatLogEntry> _entries = new();
        private string _currentFilter = "all";
        private readonly Dictionary<string, Button> _filterButtons = new();

        private const int MaxEntries = 100;

        public CombatLogPanel()
        {
            PanelTitle = "COMBAT LOG";
            Draggable = false;
            Resizable = true;
            MinSize = new Vector2(220, 200);
            MaxSize = new Vector2(600, 1000);
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
            vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
            parent.AddChild(vbox);

            // Filter buttons
            _filterContainer = new HBoxContainer();
            _filterContainer.AddThemeConstantOverride("separation", 4);
            vbox.AddChild(_filterContainer);

            CreateFilterButton("All", "all");
            CreateFilterButton("Rolls", "rolls");
            CreateFilterButton("Damage", "damage");
            CreateFilterButton("Status", "conditions");

            // Scroll container with log
            _scrollContainer = new ScrollContainer();
            _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scrollContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _scrollContainer.FollowFocus = true;
            _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            vbox.AddChild(_scrollContainer);

            _logText = new RichTextLabel();
            _logText.BbcodeEnabled = true;
            _logText.FitContent = true;
            _logText.ScrollActive = false;
            _logText.SelectionEnabled = true;
            _logText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _logText.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _logText.SizeFlagsVertical = SizeFlags.ExpandFill;
            _logText.AddThemeFontSizeOverride("normal_font_size", HudTheme.FontSmall);
            _logText.AddThemeColorOverride("default_color", HudTheme.WarmWhite);
            _scrollContainer.AddChild(_logText);
        }

        private void CreateFilterButton(string label, string filter)
        {
            var button = new Button();
            button.Text = label;
            button.ToggleMode = true;
            button.ButtonPressed = filter == _currentFilter;
            button.CustomMinimumSize = new Vector2(50, 20);

            var activeStyle = HudTheme.CreateButtonStyle(HudTheme.Gold, HudTheme.Gold, borderWidth: 2);
            var inactiveStyle = HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder);

            button.AddThemeStyleboxOverride("normal", inactiveStyle);
            button.AddThemeStyleboxOverride("pressed", activeStyle);
            button.AddThemeFontSizeOverride("font_size", HudTheme.FontTiny);

            button.Toggled += (pressed) =>
            {
                if (pressed)
                {
                    SetFilter(filter);
                }
            };

            _filterContainer.AddChild(button);
            _filterButtons[filter] = button;
        }

        private void UpdateFilterButtons()
        {
            foreach (var kvp in _filterButtons)
            {
                var isActive = kvp.Key == _currentFilter;
                kvp.Value.ButtonPressed = isActive;

                var activeStyle = HudTheme.CreateButtonStyle(HudTheme.Gold, HudTheme.Gold, borderWidth: 2);
                var inactiveStyle = HudTheme.CreateButtonStyle(HudTheme.SecondaryDark, HudTheme.PanelBorder);

                kvp.Value.AddThemeStyleboxOverride("normal", inactiveStyle);
                kvp.Value.AddThemeStyleboxOverride("pressed", activeStyle);
            }
        }

        /// <summary>
        /// Add a log entry.
        /// </summary>
        public void AddEntry(CombatLogEntry entry)
        {
            _entries.Add(entry);

            // Trim to max entries
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
            }

            RefreshLog();
            ScrollToBottom();
        }

        /// <summary>
        /// Clear all entries.
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _logText.Clear();
        }

        /// <summary>
        /// Set the filter.
        /// </summary>
        public void SetFilter(string filter)
        {
            _currentFilter = filter;
            UpdateFilterButtons();
            RefreshLog();
        }

        private void RefreshLog()
        {
            _logText.Clear();

            var filteredEntries = FilterEntries(_entries);

            foreach (var entry in filteredEntries)
            {
                AppendFormattedEntry(entry);
            }
        }

        private List<CombatLogEntry> FilterEntries(List<CombatLogEntry> entries)
        {
            if (_currentFilter == "all")
                return entries;

            var filtered = new List<CombatLogEntry>();
            foreach (var entry in entries)
            {
                bool matches = _currentFilter switch
                {
                    "rolls" => entry.Type == CombatLogEntryType.AttackResolved,
                    "damage" => entry.Type == CombatLogEntryType.DamageDealt || entry.Type == CombatLogEntryType.HealingDone,
                    "conditions" => entry.Type == CombatLogEntryType.StatusApplied || entry.Type == CombatLogEntryType.StatusRemoved,
                    _ => true
                };

                if (matches)
                    filtered.Add(entry);
            }

            return filtered;
        }

        private void AppendFormattedEntry(CombatLogEntry entry)
        {
            var color = GetColorForEntry(entry);
            var timestamp = $"[color=#{HudTheme.TextDim.ToHtml(false)}][lb]R{entry.Round}[rb][/color]";
            var message = EscapeBbCode(entry.Format());

            if (entry.IsCritical)
            {
                message = $"[b]{message} [color=#{HudTheme.Gold.ToHtml(false)}](CRIT)[/color][/b]";
            }
            else if (entry.IsMiss)
            {
                message = $"[color=#{HudTheme.TextDim.ToHtml(false)}]{message} (MISS)[/color]";
            }

            _logText.AppendText($"{timestamp} [color=#{color.ToHtml(false)}]{message}[/color]\n");

            if (entry.Breakdown.TryGetValue("rollText", out var rollTextObj) && rollTextObj is string rollText && !string.IsNullOrEmpty(rollText))
            {
                _logText.AppendText($"  [color=#888888]\u2192 {EscapeBbCode(rollText)}[/color]\n");
            }
        }

        private static string EscapeBbCode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("]", "[rb]").Replace("[", "[lb]");
        }

        private Color GetColorForEntry(CombatLogEntry entry)
        {
            return entry.Type switch
            {
                CombatLogEntryType.DamageDealt => HudTheme.EnemyRed,
                CombatLogEntryType.HealingDone => HudTheme.HealthGreen,
                CombatLogEntryType.AttackResolved => entry.IsMiss ? HudTheme.TextDim : HudTheme.WarmWhite,
                CombatLogEntryType.StatusApplied => HudTheme.BonusOrange,
                CombatLogEntryType.StatusRemoved => HudTheme.MutedBeige,
                CombatLogEntryType.CombatantDowned => HudTheme.EnemyRed,
                CombatLogEntryType.RoundStarted => HudTheme.Gold,
                CombatLogEntryType.TurnStarted => HudTheme.PlayerBlue,
                _ => HudTheme.MutedBeige
            };
        }

        private void ScrollToBottom()
        {
            // Defer scroll to next frame to ensure layout is updated
            CallDeferred(nameof(DeferredScrollToBottom));
        }

        private void DeferredScrollToBottom()
        {
            if (_scrollContainer != null)
            {
                var vScrollBar = _scrollContainer.GetVScrollBar();
                if (vScrollBar != null)
                {
                    vScrollBar.Value = vScrollBar.MaxValue;
                }
            }
        }
    }
}
