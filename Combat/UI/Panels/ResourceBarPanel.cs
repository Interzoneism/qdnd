using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Panels
{
    /// <summary>
    /// BG3-style resource icon bar — shows resource icons with counts above the hotbar,
    /// with a fixed-width movement progress bar below the icons.
    /// Resources are: Action, Bonus Action, Reaction, Spell Slots (per level), class resources.
    /// </summary>
    public partial class ResourceBarPanel : HudPanel
    {
        private const int IconSize = 36;
        private const int IconGap = 6;
        private const int MovementBarWidth = 400;
        private const int MovementBarHeight = 8;

        private VBoxContainer _rootContainer;
        private HBoxContainer _iconRow;
        private ProgressBar _movementBar;
        private Label _movementLabel;

        // Track all resource icon widgets for dynamic updates
        private readonly Dictionary<string, ResourceIconWidget> _resourceIcons = new();
        // Track spell slot icons separately keyed by level
        private readonly Dictionary<int, ResourceIconWidget> _spellSlotIcons = new();
        private ResourceIconWidget _warlockSlotIcon;

        // Current resource values for rebuilding
        private readonly Dictionary<string, (int current, int max)> _resourceValues = new();
        private readonly Dictionary<int, (int current, int max)> _spellSlotValues = new();
        private (int current, int max, int level) _warlockSlotValue;
        private int _moveCurrent;
        private int _moveMax = 30;
        private bool _rebuildPending;

        // Icon path mapping
        private static readonly Dictionary<string, string> ResourceIconPaths = new()
        {
            ["action"] = "res://assets/Images/Icons Resources Hotbar/Action_Bar_Icon.png",
            ["bonus_action"] = "res://assets/Images/Icons Resources Hotbar/Bonus_Action_Bar_Icon.png",
            ["reaction"] = "res://assets/Images/Icons Resources Hotbar/Reaction_Bar_Icon.png",
            ["ki_points"] = "res://assets/Images/Icons Resources Hotbar/Ki_Point_Bar_Icon.png",
            ["rage"] = "res://assets/Images/Icons Resources Hotbar/Rage_Charge_Bar_Icon.png",
            ["sorcery_points"] = "res://assets/Images/Icons Resources Hotbar/Sorcery_Point_Bar_Icon.png",
            ["bardic_inspiration"] = "res://assets/Images/Icons Resources Hotbar/Bardic_Inspiration_Bar_Icon.png",
            ["channel_divinity"] = "res://assets/Images/Icons Resources Hotbar/Channel_Divinity_Bar_Icon.png",
            ["superiority_dice"] = "res://assets/Images/Icons Resources Hotbar/Superiority_Die_Bar_Icon.png",
            ["lay_on_hands"] = "res://assets/Images/Icons Resources Hotbar/Lay_On_Hands_Charge_Bar_Icon.png",
            ["wild_shape"] = "res://assets/Images/Icons Resources Hotbar/Wild_Shape_Charge_Bar_Icon.png",
            ["luck_points"] = "res://assets/Images/Icons Resources Hotbar/Luck_Point_Bar_Icon.png",
        };

        public ResourceBarPanel()
        {
            PanelTitle = "";
            ShowDragHandle = false;
            Draggable = false;
        }

        public override void _Ready()
        {
            base._Ready();
            var transparentStyle = new StyleBoxFlat();
            transparentStyle.BgColor = Colors.Transparent;
            transparentStyle.SetBorderWidthAll(0);
            AddThemeStyleboxOverride("panel", transparentStyle);
        }

        public override void _Process(double delta)
        {
            if (_rebuildPending)
            {
                _rebuildPending = false;
                RebuildIconRow();
            }
        }

        protected override void BuildContent(Control parent)
        {
            _rootContainer = new VBoxContainer();
            _rootContainer.AddThemeConstantOverride("separation", 4);
            _rootContainer.Alignment = BoxContainer.AlignmentMode.Center;
            parent.AddChild(_rootContainer);

            // Resource icons row (centered)
            _iconRow = new HBoxContainer();
            _iconRow.AddThemeConstantOverride("separation", IconGap);
            _iconRow.Alignment = BoxContainer.AlignmentMode.Center;
            _iconRow.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            _rootContainer.AddChild(_iconRow);

            // Movement bar (fixed width, centered)
            var moveContainer = new HBoxContainer();
            moveContainer.Alignment = BoxContainer.AlignmentMode.Center;
            moveContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _rootContainer.AddChild(moveContainer);

            var moveVBox = new VBoxContainer();
            moveVBox.AddThemeConstantOverride("separation", 1);
            moveVBox.CustomMinimumSize = new Vector2(MovementBarWidth, 0);
            moveVBox.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            moveContainer.AddChild(moveVBox);

            _movementBar = new ProgressBar();
            _movementBar.CustomMinimumSize = new Vector2(MovementBarWidth, MovementBarHeight);
            _movementBar.ShowPercentage = false;
            _movementBar.MaxValue = 30;
            _movementBar.Value = 30;
            _movementBar.AddThemeStyleboxOverride("background", HudTheme.CreateProgressBarBg());
            _movementBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(HudTheme.MoveYellow));
            moveVBox.AddChild(_movementBar);

            _movementLabel = new Label();
            _movementLabel.Text = "";
            _movementLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(_movementLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
            moveVBox.AddChild(_movementLabel);

            // Build initial icons for core resources
            RebuildIconRow();
        }

        // ── Public API ─────────────────────────────────────────────

        public void SetResource(string id, int current, int max)
        {
            if (id == "move") id = "movement";

            if (id == "movement")
            {
                _moveCurrent = current;
                _moveMax = max;
                UpdateMovementBar();
                return;
            }

            _resourceValues[id] = (current, max);

            if (_resourceIcons.TryGetValue(id, out var widget))
            {
                UpdateIconWidget(widget, current, max);
            }
            else
            {
                // New resource appeared - rebuild next frame
                _rebuildPending = true;
            }
        }

        public void SetSpellSlots(int level, int current, int max)
        {
            if (level < 1 || level > 9) return;

            if (max <= 0)
            {
                _spellSlotValues.Remove(level);
            }
            else
            {
                _spellSlotValues[level] = (current, max);
            }

            // Always mark rebuild pending - icon texture depends on current slot count
            _rebuildPending = true;
        }

        public void SetWarlockSlots(int current, int max, int level)
        {
            _warlockSlotValue = (current, max, level);
            // Always mark rebuild pending - icon texture depends on current slot count
            _rebuildPending = true;
        }

        public void InitializeDefaults(int maxMove)
        {
            SetResource("action", 1, 1);
            SetResource("bonus_action", 1, 1);
            SetResource("reaction", 1, 1);
            SetResource("movement", maxMove, maxMove);
        }

        // ── Icon Row Building ──────────────────────────────────────

        private void RebuildIconRow()
        {
            if (_iconRow == null) return;

            // Clear existing — RemoveChild detaches immediately so new children aren't doubled
            foreach (var child in _iconRow.GetChildren())
            {
                _iconRow.RemoveChild(child);
                child.QueueFree();
            }
            _resourceIcons.Clear();
            _spellSlotIcons.Clear();
            _warlockSlotIcon = null;

            // Core resources (always shown in this order)
            string[] coreResources = { "action", "bonus_action", "reaction" };
            foreach (var id in coreResources)
            {
                var (current, max) = _resourceValues.TryGetValue(id, out var val) ? val : (1, 1);
                var iconPath = ResourceIconPaths.GetValueOrDefault(id, "");
                var widget = CreateIconWidget(iconPath, current, max, null);
                _iconRow.AddChild(widget.Container);
                _resourceIcons[id] = widget;
            }

            // Spell slots (sorted by level)
            foreach (var level in _spellSlotValues.Keys.OrderBy(l => l))
            {
                var (current, max) = _spellSlotValues[level];
                if (max <= 0) continue;
                var iconPath = ResolveSpellSlotIconPath(current, level);
                var widget = CreateIconWidget(iconPath, current, max, ToRoman(level));
                // Hide redundant count label when per-level pip icon is loaded
                bool isGeneric = iconPath.EndsWith("Spell_Slot_Bar_Icon.png");
                if (!isGeneric)
                {
                    widget.CountLabel.Visible = false;
                }
                _iconRow.AddChild(widget.Container);
                _spellSlotIcons[level] = widget;
            }

            // Warlock slots
            if (_warlockSlotValue.max > 0)
            {
                var warlockIconPath = ResolveSpellSlotIconPath(_warlockSlotValue.current, _warlockSlotValue.level, true);
                _warlockSlotIcon = CreateIconWidget(
                    warlockIconPath,
                    _warlockSlotValue.current, _warlockSlotValue.max,
                    ToRoman(_warlockSlotValue.level));
                // Hide redundant count label when per-level pip icon is loaded
                bool isGenericWarlock = warlockIconPath.EndsWith("Spell_Slot_Bar_Icon.png");
                if (!isGenericWarlock)
                {
                    _warlockSlotIcon.CountLabel.Visible = false;
                }
                _iconRow.AddChild(_warlockSlotIcon.Container);
            }

            // Class resources (anything not in core or movement)
            foreach (var (id, (current, max)) in _resourceValues)
            {
                if (coreResources.Contains(id) || id == "movement") continue;
                var iconPath = ResourceIconPaths.GetValueOrDefault(id, "");
                if (string.IsNullOrEmpty(iconPath))
                {
                    // Try to find icon by name convention
                    string normalized = id.Replace(" ", "_");
                    string guessPath = $"res://assets/Images/Icons Resources Hotbar/{normalized}_Bar_Icon.png";
                    iconPath = ResourceLoader.Exists(guessPath) ? guessPath : "";
                }
                var widget = CreateIconWidget(iconPath, current, max, null);
                _iconRow.AddChild(widget.Container);
                _resourceIcons[id] = widget;
            }
        }

        private ResourceIconWidget CreateIconWidget(string iconPath, int current, int max, string levelLabel)
        {
            var container = new VBoxContainer();
            container.AddThemeConstantOverride("separation", 1);
            container.Alignment = BoxContainer.AlignmentMode.Center;

            // Level label (roman numeral, only for spell slots)
            var lvlLabel = new Label();
            lvlLabel.Text = levelLabel ?? "";
            lvlLabel.Visible = !string.IsNullOrEmpty(levelLabel);
            lvlLabel.HorizontalAlignment = HorizontalAlignment.Center;
            HudTheme.StyleLabel(lvlLabel, HudTheme.FontTiny, HudTheme.Gold);
            container.AddChild(lvlLabel);

            // Icon container (holds icon + count overlay)
            var iconContainer = new Control();
            iconContainer.CustomMinimumSize = new Vector2(IconSize, IconSize);
            container.AddChild(iconContainer);

            // Icon texture
            var iconRect = new TextureRect();
            iconRect.CustomMinimumSize = new Vector2(IconSize, IconSize);
            iconRect.Size = new Vector2(IconSize, IconSize);
            iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            iconRect.MouseFilter = MouseFilterEnum.Ignore;

            if (!string.IsNullOrEmpty(iconPath))
            {
                var tex = HudIcons.LoadTextureSafe(iconPath);
                if (tex != null) iconRect.Texture = tex;
                else iconRect.Texture = HudIcons.CreatePlaceholderTexture(HudTheme.GoldMuted, IconSize);
            }
            else
            {
                iconRect.Texture = HudIcons.CreatePlaceholderTexture(HudTheme.GoldMuted, IconSize);
            }
            iconContainer.AddChild(iconRect);

            // Count label (centered on icon, only visible if max > 1)
            var countLabel = new Label();
            countLabel.Text = max > 1 ? current.ToString() : "";
            countLabel.Visible = max > 1;
            countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            countLabel.VerticalAlignment = VerticalAlignment.Center;
            countLabel.SetAnchorsPreset(LayoutPreset.FullRect);
            HudTheme.StyleLabel(countLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            // Add text shadow for readability
            countLabel.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
            countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
            countLabel.AddThemeConstantOverride("shadow_offset_y", 1);
            iconContainer.AddChild(countLabel);

            var widget = new ResourceIconWidget
            {
                Container = container,
                IconRect = iconRect,
                CountLabel = countLabel,
                LevelLabel = lvlLabel,
                Current = current,
                Max = max,
            };

            // Apply depleted state
            ApplyDepletedState(widget, current);

            return widget;
        }

        private void UpdateIconWidget(ResourceIconWidget widget, int current, int max)
        {
            widget.Current = current;
            widget.Max = max;
            widget.CountLabel.Text = max > 1 ? current.ToString() : "";
            widget.CountLabel.Visible = max > 1;
            ApplyDepletedState(widget, current);
        }

        private void ApplyDepletedState(ResourceIconWidget widget, int current)
        {
            if (current <= 0)
            {
                // Depleted: desaturated + 75% opacity
                widget.IconRect.Modulate = new Color(0.5f, 0.5f, 0.5f, 0.75f);
                widget.CountLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f, 0.75f));
            }
            else
            {
                // Active: full color
                widget.IconRect.Modulate = new Color(1f, 1f, 1f, 1f);
                widget.CountLabel.AddThemeColorOverride("font_color", HudTheme.WarmWhite);
            }
        }

        private void UpdateMovementBar()
        {
            if (_movementBar == null) return;
            _movementBar.MaxValue = _moveMax;
            _movementBar.Value = _moveCurrent;
            _movementLabel.Text = $"{_moveCurrent} ft";

            float pct = _moveMax > 0 ? (float)_moveCurrent / _moveMax : 0;
            var color = pct > 0 ? HudTheme.MoveYellow : new Color(HudTheme.MoveYellow.R, HudTheme.MoveYellow.G, HudTheme.MoveYellow.B, 0.3f);
            _movementBar.AddThemeStyleboxOverride("fill", HudTheme.CreateProgressBarFill(color));
        }

        /// <summary>
        /// Clear all resource state. Call on combatant switch to avoid stale data.
        /// </summary>
        public void Reset()
        {
            _resourceValues.Clear();
            _spellSlotValues.Clear();
            _warlockSlotValue = default;
            _moveCurrent = 0;
            _moveMax = 30;
            RebuildIconRow();
            UpdateMovementBar();
        }

        /// <summary>
        /// Resolves the spell slot icon path based on current available slots and spell level.
        /// Uses specific icons from Spell Slots/ directory that visually show slot pips.
        /// Falls back to generic icon if specific one doesn't exist.
        /// </summary>
        private static string ResolveSpellSlotIconPath(int currentSlots, int level, bool isWarlock = false)
        {
            if (currentSlots <= 0) currentSlots = 1; // Show at least 1-slot icon even when depleted
            string warlockSuffix = isWarlock ? "Warlock_" : "";
            string path = $"res://assets/Images/Icons Resources Hotbar/Spell Slots/{currentSlots}_Level_{level}_{warlockSuffix}Spell_Slots.png";
            if (ResourceLoader.Exists(path)) return path;

            // Fallback: try without warlock suffix
            if (isWarlock)
            {
                path = $"res://assets/Images/Icons Resources Hotbar/Spell Slots/{currentSlots}_Level_{level}_Spell_Slots.png";
                if (ResourceLoader.Exists(path)) return path;
            }

            // Final fallback: generic icon
            return isWarlock
                ? "res://assets/Images/Icons Resources Hotbar/Warlock_Spell_Slot_Bar_Icon.png"
                : "res://assets/Images/Icons Resources Hotbar/Spell_Slot_Bar_Icon.png";
        }

        private static string ToRoman(int number) => number switch
        {
            1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
            6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => number.ToString()
        };

        private class ResourceIconWidget
        {
            public VBoxContainer Container { get; set; }
            public TextureRect IconRect { get; set; }
            public Label CountLabel { get; set; }
            public Label LevelLabel { get; set; }
            public int Current { get; set; }
            public int Max { get; set; }
        }
    }
}
