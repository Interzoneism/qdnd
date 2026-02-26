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
                    "rolls" => entry.Type == CombatLogEntryType.AttackResolved || entry.Type == CombatLogEntryType.SavingThrow,
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
            string roundTag = $"[color=#{HudTheme.TextDim.ToHtml(false)}][lb]R{entry.Round}[rb][/color] ";

            string line = entry.Type switch
            {
                CombatLogEntryType.RoundStarted => FormatRoundStart(entry),
                CombatLogEntryType.TurnStarted => FormatTurnStart(entry),
                CombatLogEntryType.AbilityUsed => FormatAbilityUsed(entry),
                CombatLogEntryType.AttackResolved => FormatAttackResolved(entry),
                CombatLogEntryType.SavingThrow => FormatSavingThrow(entry),
                CombatLogEntryType.DamageDealt => FormatDamageDealt(entry),
                CombatLogEntryType.HealingDone => FormatHealingDone(entry),
                CombatLogEntryType.CombatantDowned => FormatCombatantDowned(entry),
                CombatLogEntryType.StatusApplied => FormatStatusApplied(entry),
                CombatLogEntryType.StatusRemoved => FormatStatusRemoved(entry),
                CombatLogEntryType.CombatStarted => FormatCombatMarker(entry, "Combat started"),
                CombatLogEntryType.CombatEnded => FormatCombatMarker(entry, "Combat ended"),
                CombatLogEntryType.TurnEnded => null, // Don't show turn end
                _ => FormatGeneric(entry)
            };

            if (line == null) return;

            _logText.AppendText($"{roundTag}{line}\n");

            // Show roll breakdown if available
            if (entry.Breakdown.TryGetValue("rollText", out var rollTextObj) && rollTextObj is string rollText && !string.IsNullOrEmpty(rollText))
            {
                _logText.AppendText($"  [color=#888888]\u2192 {EscapeBbCode(rollText)}[/color]\n");
            }
        }

        private static string EscapeBbCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            text = text.Replace("[", "\x01").Replace("]", "\x02");
            return text.Replace("\x01", "[lb]").Replace("\x02", "[rb]");
        }

        private static Color GetDamageTypeColor(string damageType)
        {
            if (string.IsNullOrEmpty(damageType))
                return HudTheme.EnemyRed;

            return damageType.ToLowerInvariant() switch
            {
                "fire" => new Color(1.0f, 0.45f, 0.2f),
                "cold" => new Color(0.4f, 0.75f, 1.0f),
                "lightning" => new Color(0.6f, 0.6f, 1.0f),
                "thunder" => new Color(0.65f, 0.55f, 0.85f),
                "acid" => new Color(0.6f, 0.85f, 0.2f),
                "poison" => new Color(0.4f, 0.75f, 0.3f),
                "necrotic" => new Color(0.5f, 0.8f, 0.5f),
                "radiant" => new Color(1.0f, 0.95f, 0.5f),
                "psychic" => new Color(0.8f, 0.4f, 0.8f),
                "force" => new Color(0.7f, 0.85f, 1.0f),
                "slashing" or "piercing" or "bludgeoning"
                    => new Color(0.85f, 0.75f, 0.65f),
                _ => HudTheme.EnemyRed
            };
        }

        private string ColorName(string name, bool isSource = true)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string hex = HudTheme.WarmWhite.ToHtml(false);
            return $"[color=#{hex}][b]{EscapeBbCode(name)}[/b][/color]";
        }

        private string ColorSpell(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string hex = HudTheme.PlayerBlue.ToHtml(false);
            return $"[color=#{hex}]{EscapeBbCode(name)}[/color]";
        }

        private string ColorStatus(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            string hex = HudTheme.BonusOrange.ToHtml(false);
            return $"[color=#{hex}]{EscapeBbCode(name)}[/color]";
        }

        private string ColorDamage(int amount, string damageType)
        {
            var color = GetDamageTypeColor(damageType);
            string typeLabel = string.IsNullOrEmpty(damageType) ? "" : $" {EscapeBbCode(damageType)}";
            return $"[color=#{color.ToHtml(false)}]{amount}{typeLabel}[/color]";
        }

        private string FormatRoundStart(CombatLogEntry entry)
        {
            string hex = HudTheme.Gold.ToHtml(false);
            return $"[color=#{hex}][b]═══ ROUND {entry.Round} ═══[/b][/color]";
        }

        private string FormatTurnStart(CombatLogEntry entry)
        {
            string nameHex = HudTheme.PlayerBlue.ToHtml(false);
            return $"[color=#{nameHex}][b]{EscapeBbCode(entry.SourceName ?? "???")}\u2019s turn[/b][/color]";
        }

        private string FormatAbilityUsed(CombatLogEntry entry)
        {
            string actionName = entry.Data.TryGetValue("actionName", out var n) ? n?.ToString() : "ability";
            string dimHex = HudTheme.MutedBeige.ToHtml(false);
            return $"{ColorName(entry.SourceName)} [color=#{dimHex}]uses[/color] {ColorSpell(actionName)}";
        }

        private string FormatAttackResolved(CombatLogEntry entry)
        {
            string dimHex = HudTheme.MutedBeige.ToHtml(false);
            if (entry.IsCritical)
            {
                string critHex = HudTheme.Gold.ToHtml(false);
                return $"[color=#{dimHex}]Attack Roll:[/color] {ColorName(entry.SourceName)} [color=#{dimHex}]\u2192[/color] {ColorName(entry.TargetName, false)} [color=#{critHex}][b](CRITICAL HIT)[/b][/color]";
            }
            if (entry.IsMiss)
            {
                string missHex = HudTheme.TextDim.ToHtml(false);
                return $"[color=#{missHex}]Attack Roll: {EscapeBbCode(entry.SourceName ?? "")} \u2192 {EscapeBbCode(entry.TargetName ?? "")} (MISS)[/color]";
            }
            return $"[color=#{dimHex}]Attack Roll:[/color] {ColorName(entry.SourceName)} [color=#{dimHex}]\u2192[/color] {ColorName(entry.TargetName, false)} [color=#{dimHex}](HIT)[/color]";
        }

        private string FormatSavingThrow(CombatLogEntry entry)
        {
            string saveType = entry.Data.TryGetValue("saveType", out var st) ? st?.ToString() : "Save";
            string dimHex = HudTheme.MutedBeige.ToHtml(false);

            if (entry.IsMiss) // IsMiss = failed the save
            {
                string failHex = HudTheme.EnemyRed.ToHtml(false);
                return $"[color=#{dimHex}]{EscapeBbCode(saveType)}:[/color] {ColorName(entry.TargetName, false)} [color=#{failHex}](FAIL)[/color]";
            }
            string successHex = HudTheme.HealthGreen.ToHtml(false);
            return $"[color=#{dimHex}]{EscapeBbCode(saveType)}:[/color] {ColorName(entry.TargetName, false)} [color=#{successHex}](SUCCESS)[/color]";
        }

        private string FormatDamageDealt(CombatLogEntry entry)
        {
            int damage = (int)entry.Value;
            string damageType = entry.Data.TryGetValue("damageType", out var dt) ? dt?.ToString() : null;
            string dimHex = HudTheme.MutedBeige.ToHtml(false);

            string result = $"{ColorName(entry.SourceName)} [color=#{dimHex}]deals[/color] {ColorDamage(damage, damageType)} [color=#{dimHex}]to[/color] {ColorName(entry.TargetName, false)}";

            if (entry.IsCritical)
            {
                string critHex = HudTheme.Gold.ToHtml(false);
                result += $" [color=#{critHex}][b](CRIT)[/b][/color]";
            }
            return result;
        }

        private string FormatHealingDone(CombatLogEntry entry)
        {
            int healed = (int)entry.Value;
            string healHex = HudTheme.HealthGreen.ToHtml(false);
            string dimHex = HudTheme.MutedBeige.ToHtml(false);
            return $"{ColorName(entry.SourceName)} [color=#{dimHex}]heals[/color] {ColorName(entry.TargetName, false)} [color=#{dimHex}]for[/color] [color=#{healHex}]{healed}[/color]";
        }

        private string FormatCombatantDowned(CombatLogEntry entry)
        {
            string redHex = HudTheme.EnemyRed.ToHtml(false);
            return $"[color=#{redHex}][b]{EscapeBbCode(entry.TargetName ?? "???")} is downed![/b][/color]";
        }

        private string FormatStatusApplied(CombatLogEntry entry)
        {
            string statusName = entry.Data.TryGetValue("statusId", out var s) ? s?.ToString() : "status";
            string dimHex = HudTheme.MutedBeige.ToHtml(false);
            return $"{ColorName(entry.TargetName, false)} [color=#{dimHex}]gains[/color] {ColorStatus(statusName)}";
        }

        private string FormatStatusRemoved(CombatLogEntry entry)
        {
            string statusName = entry.Data.TryGetValue("statusId", out var s) ? s?.ToString() : "status";
            string dimHex = HudTheme.TextDim.ToHtml(false);
            return $"[color=#{dimHex}]{EscapeBbCode(entry.TargetName ?? "???")} loses {EscapeBbCode(statusName)}[/color]";
        }

        private string FormatCombatMarker(CombatLogEntry entry, string label)
        {
            string hex = HudTheme.Gold.ToHtml(false);
            return $"[color=#{hex}][b]\u2500\u2500\u2500 {EscapeBbCode(label.ToUpperInvariant())} \u2500\u2500\u2500[/b][/color]";
        }

        private string FormatGeneric(CombatLogEntry entry)
        {
            string hex = HudTheme.MutedBeige.ToHtml(false);
            return $"[color=#{hex}]{EscapeBbCode(entry.Format())}[/color]";
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
