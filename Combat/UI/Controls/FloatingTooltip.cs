using Godot;
using QDND.Combat.Services;
using QDND.Combat.UI.Base;

namespace QDND.Combat.UI.Controls
{
    /// <summary>
    /// Global floating tooltip that follows the mouse cursor.
    /// Displays item details with rarity-colored headers, stat blocks,
    /// and optional equipment comparison deltas.
    /// </summary>
    public partial class FloatingTooltip : PanelContainer
    {
        private const float OffsetX = 14f;
        private const float OffsetY = -8f;
        private const float MaxWidth = 260f;
        private const float HoverDelayMs = 400f;  // ms before tooltip shows

        private VBoxContainer _content;
        private Label _nameLabel;
        private Label _typeLabel;
        private Label _statsLabel;
        private Label _descLabel;
        private Label _requiresLabel;
        private Label _comparisonLabel;

        private bool _isShowing;
        private float _hoverElapsedMs;
        private bool _pendingShow;
        private System.Action _pendingShowAction;

        public override void _Ready()
        {
            Visible = false;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = 100;
            CustomMinimumSize = new Vector2(160, 40);

            AddThemeStyleboxOverride("panel", HudTheme.CreatePanelStyle(
                new Color(0.025f, 0.02f, 0.04f, 0.96f),
                HudTheme.PanelBorder,
                HudTheme.CornerRadiusMedium, 2, 10));

            _content = new VBoxContainer();
            _content.AddThemeConstantOverride("separation", 4);
            _content.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_content);

            // Item name (rarity-colored)
            _nameLabel = new Label();
            _nameLabel.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(_nameLabel, HudTheme.FontMedium, HudTheme.Gold);
            _content.AddChild(_nameLabel);

            // Item type line
            _typeLabel = new Label();
            _typeLabel.MouseFilter = MouseFilterEnum.Ignore;
            HudTheme.StyleLabel(_typeLabel, HudTheme.FontSmall, HudTheme.MutedBeige);
            _content.AddChild(_typeLabel);

            // Separator
            var sep = new PanelContainer();
            sep.CustomMinimumSize = new Vector2(0, 1);
            sep.MouseFilter = MouseFilterEnum.Ignore;
            sep.AddThemeStyleboxOverride("panel", HudTheme.CreateSeparatorStyle());
            _content.AddChild(sep);

            // Stat block
            _statsLabel = new Label();
            _statsLabel.MouseFilter = MouseFilterEnum.Ignore;
            _statsLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            HudTheme.StyleLabel(_statsLabel, HudTheme.FontSmall, HudTheme.WarmWhite);
            _content.AddChild(_statsLabel);

            // Description
            _descLabel = new Label();
            _descLabel.MouseFilter = MouseFilterEnum.Ignore;
            _descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _descLabel.CustomMinimumSize = new Vector2(140, 0);
            HudTheme.StyleLabel(_descLabel, HudTheme.FontTiny, HudTheme.MutedBeige);
            _content.AddChild(_descLabel);

            // Requirements line
            _requiresLabel = new Label();
            _requiresLabel.MouseFilter = MouseFilterEnum.Ignore;
            _requiresLabel.Visible = false;
            HudTheme.StyleLabel(_requiresLabel, HudTheme.FontTiny, HudTheme.EnemyRed);
            _content.AddChild(_requiresLabel);

            // Comparison line
            _comparisonLabel = new Label();
            _comparisonLabel.MouseFilter = MouseFilterEnum.Ignore;
            _comparisonLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _comparisonLabel.Visible = false;
            HudTheme.StyleLabel(_comparisonLabel, HudTheme.FontTiny, HudTheme.WarmWhite);
            _content.AddChild(_comparisonLabel);
        }

        public override void _Process(double delta)
        {
            if (_pendingShow)
            {
                _hoverElapsedMs += (float)(delta * 1000.0);
                if (_hoverElapsedMs >= HoverDelayMs)
                {
                    _pendingShow = false;
                    _pendingShowAction?.Invoke();
                    _pendingShowAction = null;
                }
                return; // Don't update position while waiting
            }

            if (!_isShowing) return;

            var mousePos = GetViewport().GetMousePosition();
            var viewportSize = GetViewportRect().Size;
            var tooltipSize = Size;

            float x = mousePos.X + OffsetX;
            float y = mousePos.Y + OffsetY;

            if (x + tooltipSize.X > viewportSize.X)
                x = mousePos.X - tooltipSize.X - 4;
            if (y + tooltipSize.Y > viewportSize.Y)
                y = viewportSize.Y - tooltipSize.Y - 4;
            if (y < 0) y = 4;
            if (x < 0) x = 4;

            GlobalPosition = new Vector2(x, y);
        }

        private void BeginDelayedShow(System.Action showAction)
        {
            _pendingShow = true;
            _pendingShowAction = showAction;
            _hoverElapsedMs = 0f;
            SetProcess(true);
        }

        /// <summary>
        /// Show tooltip for an inventory item.
        /// </summary>
        public void ShowItem(InventoryItem item, string comparisonText = null)
        {
            if (item == null) { Hide(); return; }
            BeginDelayedShow(() => DoShowItem(item, comparisonText));
        }

        private void DoShowItem(InventoryItem item, string comparisonText)
        {
            _nameLabel.Text = item.Name ?? "Unknown";
            _nameLabel.AddThemeColorOverride("font_color", HudTheme.GetRarityColor(item.Rarity));

            _typeLabel.Text = FormatItemType(item);
            _typeLabel.Visible = !string.IsNullOrWhiteSpace(_typeLabel.Text);

            _statsLabel.Text = item.GetStatLine();
            _statsLabel.Visible = !string.IsNullOrWhiteSpace(_statsLabel.Text);

            string desc = item.Description ?? "";
            if (item.Weight > 0)
                desc += (desc.Length > 0 ? "\n" : "") + $"Weight: {item.Weight} lb";
            if (item.Quantity > 1)
                desc += (desc.Length > 0 ? "\n" : "") + $"Stack: {item.Quantity}";
            _descLabel.Text = desc;
            _descLabel.Visible = !string.IsNullOrWhiteSpace(desc);

            _requiresLabel.Visible = false;

            if (!string.IsNullOrWhiteSpace(comparisonText))
            {
                _comparisonLabel.Text = comparisonText;
                _comparisonLabel.Visible = true;
            }
            else
            {
                _comparisonLabel.Visible = false;
            }

            // Constrain max width
            CustomMinimumSize = new Vector2(Mathf.Min(MaxWidth, 200), 0);
            Size = Vector2.Zero; // Let it auto-size

            _isShowing = true;
            Visible = true;
            SetProcess(true);
        }

        /// <summary>
        /// Show tooltip for an empty equipment slot.
        /// </summary>
        public void ShowSlot(EquipSlot slot)
        {
            BeginDelayedShow(() => DoShowSlot(slot));
        }

        private void DoShowSlot(EquipSlot slot)
        {
            string slotName = slot switch
            {
                EquipSlot.MainHand => "Main Hand",
                EquipSlot.OffHand => "Off Hand",
                EquipSlot.RangedMainHand => "Ranged Main Hand",
                EquipSlot.RangedOffHand => "Ranged Off Hand",
                EquipSlot.Armor => "Armor",
                EquipSlot.Helmet => "Helmet",
                EquipSlot.Gloves => "Gloves",
                EquipSlot.Boots => "Boots",
                EquipSlot.Cloak => "Cloak",
                EquipSlot.Amulet => "Amulet",
                EquipSlot.Ring1 => "Ring 1",
                EquipSlot.Ring2 => "Ring 2",
                _ => slot.ToString(),
            };

            _nameLabel.Text = slotName;
            _nameLabel.AddThemeColorOverride("font_color", HudTheme.Gold);
            _typeLabel.Text = "Empty Slot";
            _typeLabel.Visible = true;
            _statsLabel.Text = "";
            _statsLabel.Visible = false;
            _descLabel.Text = "Drag an item here to equip it.";
            _descLabel.Visible = true;
            _requiresLabel.Visible = false;
            _comparisonLabel.Visible = false;

            _isShowing = true;
            Visible = true;
            SetProcess(true);
        }

        /// <summary>
        /// Show a simple text tooltip.
        /// </summary>
        public void ShowText(string title, string body, Color? titleColor = null)
        {
            BeginDelayedShow(() => DoShowText(title, body, titleColor));
        }

        private void DoShowText(string title, string body, Color? titleColor)
        {
            _nameLabel.Text = title;
            _nameLabel.AddThemeColorOverride("font_color", titleColor ?? HudTheme.Gold);
            _typeLabel.Visible = false;
            _statsLabel.Visible = false;
            _descLabel.Text = body ?? "";
            _descLabel.Visible = !string.IsNullOrWhiteSpace(body);
            _requiresLabel.Visible = false;
            _comparisonLabel.Visible = false;

            _isShowing = true;
            Visible = true;
            SetProcess(true);
        }

        /// <summary>
        /// Hide the tooltip.
        /// </summary>
        public new void Hide()
        {
            _isShowing = false;
            _pendingShow = false;
            _pendingShowAction = null;
            _hoverElapsedMs = 0f;
            Visible = false;
            SetProcess(false);
        }

        private static string FormatItemType(InventoryItem item)
        {
            if (item.WeaponDef != null)
                return $"{item.WeaponDef.WeaponType} Weapon";
            if (item.ArmorDef != null)
                return $"{item.ArmorDef.Category} Armor";
            return item.Category.ToString();
        }
    }
}
